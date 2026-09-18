using System;
using System.Collections.Generic;
using System.Linq;
using HelloStardew.Calendar;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.Pathfinding;
using StardewValley.TokenizableStrings;

namespace HelloStardew.Npc;

/// <summary>
/// UI-free queries about where a villager is: the plan the game loaded for today, and the position
/// they're actually standing on.
/// </summary>
/// <remarks>
/// <para>
/// The two can disagree. <c>NPC.Schedule</c> is today's plan, already resolved by the game from
/// <c>Characters/schedules/&lt;Name&gt;</c> against the season, weekday, weather, and marriage state,
/// so no conditions need re-checking here. Its keys are the times a leg <em>starts</em>, in the same
/// domain as <see cref="Game1.timeOfDay" />, and the stop in effect now is the largest key that has
/// already passed.
/// </para>
/// <para>
/// Festivals and events override schedules entirely, and the plan is only reloaded on day change, so
/// the live position always wins when the two disagree.
/// </para>
/// <para>
/// Reads live game state, so it must run on the game's main thread.
/// </para>
/// </remarks>
internal static class NpcService
{
	/// <summary>Facing directions in the order the game numbers them: <c>Game1.up</c> and friends.</summary>
	private static readonly string[] FacingNames = { "up", "right", "down", "left" };


	/*********
	** Queries
	*********/

	/// <summary>Work out where one villager is now and where they're headed next.</summary>
	/// <param name="npc">The villager's internal or display name.</param>
	/// <exception cref="CalendarException">See <see cref="ResolveNpc"/> for the cases.</exception>
	public static NpcLocationInfo GetLocation(string npc)
	{
		EnsureSaveLoaded();

		NPC villager = ResolveNpc(npc);
		GameLocation? location = villager.currentLocation;
		Point tile = villager.TilePoint;
		SchedulePathDescription? directions = villager.DirectionsToNewLocation;
		string internalName = location?.NameOrUniqueName ?? string.Empty;

		(int Time, SchedulePathDescription Stop)? current = FindCurrentStop(villager.Schedule, Game1.timeOfDay);
		(int Time, SchedulePathDescription Stop)? next = FindNextStop(villager.Schedule, Game1.timeOfDay);

		// The plan says where they should be; this says where they actually are. A villager walking a
		// scheduled route counts as arrived only once they're standing on the target tile.
		bool hasReachedTarget = current is { } reached
			&& string.Equals(internalName, reached.Stop.targetLocationName, StringComparison.Ordinal)
			&& tile == reached.Stop.targetTile;

		return new NpcLocationInfo(
			Npc: villager.Name,
			DisplayName: villager.displayName ?? NPC.GetDisplayName(villager.Name),
			Now: ToClock(Game1.timeOfDay),
			Current: new NpcPresenceInfo(
				Location: internalName,
				LocationName: location?.DisplayName ?? internalName,
				Tile: new TileInfo(tile.X, tile.Y),
				IsMoving: villager.isMoving() || villager.controller is not null,
				IsTravelling: directions is not null,
				TravellingTo: directions?.targetLocationName,
				TravellingToName: directions is null ? null : GetLocationDisplayName(directions.targetLocationName),
				HasReachedTarget: hasReachedTarget,
				IsInEvent: Game1.eventUp || Game1.CurrentEvent is not null
			),
			Home: GetHome(villager),
			ScheduleKey: string.IsNullOrWhiteSpace(villager.ScheduleKey) ? null : villager.ScheduleKey,
			CurrentTarget: current is { } currentStop ? ToStop(currentStop.Time, currentStop.Stop) : null,
			NextTarget: next is { } nextStop ? ToStop(nextStop.Time, nextStop.Stop) : null
		);
	}


	/*********
	** Schedule lookups
	*********/

	/// <summary>
	/// The stop that should be in effect now: the latest one whose start time has passed. Returns
	/// <c>null</c> before the villager's day begins, which is when they're still at home.
	/// </summary>
	private static (int Time, SchedulePathDescription Stop)? FindCurrentStop(
		Dictionary<int, SchedulePathDescription>? schedule, int time)
	{
		if (schedule is null)
			return null;

		(int Time, SchedulePathDescription Stop)? best = null;

		foreach ((int key, SchedulePathDescription stop) in schedule)
		{
			if (key <= time && (best is null || key > best.Value.Time))
				best = (key, stop);
		}

		return best;
	}

	/// <summary>The next stop the villager hasn't started walking toward yet, or <c>null</c> once the day is done.</summary>
	private static (int Time, SchedulePathDescription Stop)? FindNextStop(
		Dictionary<int, SchedulePathDescription>? schedule, int time)
	{
		if (schedule is null)
			return null;

		(int Time, SchedulePathDescription Stop)? best = null;

		foreach ((int key, SchedulePathDescription stop) in schedule)
		{
			if (key > time && (best is null || key < best.Value.Time))
				best = (key, stop);
		}

		return best;
	}

	private static ScheduleStopInfo ToStop(int time, SchedulePathDescription stop)
	{
		return new ScheduleStopInfo(
			Time: ToClock(time),
			Destination: new LocationRefInfo(
				Location: stop.targetLocationName,
				LocationName: GetLocationDisplayName(stop.targetLocationName),
				Tile: new TileInfo(stop.targetTile.X, stop.targetTile.Y)
			),
			FacingDirection: stop.facingDirection,
			Facing: GetFacingName(stop.facingDirection),
			EndOfRouteBehavior: Blank(stop.endOfRouteBehavior),
			EndOfRouteMessage: Blank(stop.endOfRouteMessage)
		);
	}


	/*********
	** Villager and place lookups
	*********/

	/// <summary>
	/// Find a villager currently somewhere in the world, by internal or display name. Schedules only
	/// exist for NPCs that are actually instanced, so this can't answer for someone who isn't around.
	/// </summary>
	/// <exception cref="CalendarException">
	/// The name belongs to no villager at all (<c>unknown_npc</c>), or to one who exists in the game's
	/// data but has no presence in the world yet, such as Kent in year one (<c>npc_unavailable</c>).
	/// </exception>
	private static NPC ResolveNpc(string name)
	{
		List<NPC> matches = new();

		Utility.ForEachCharacter(candidate =>
		{
			if (IsMatch(candidate, name))
				matches.Add(candidate);

			return true;
		});

		if (matches.Count > 0)
			return matches[0];

		if (Game1.characterData.Keys.Any(key => IsName(key, name)))
			throw new CalendarException(
				"npc_unavailable",
				$"'{name}' is in the game's character data, but isn't in the world right now, so there's no schedule to read.",
				status: 404);

		throw new CalendarException("unknown_npc", $"No villager is called '{name}'.", status: 404);
	}

	private static bool IsMatch(NPC npc, string name)
	{
		return IsName(npc.Name, name)
			|| string.Equals(npc.displayName, name, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Match a caller-supplied name against a villager's internal and display names.</summary>
	private static bool IsName(string internalName, string name)
	{
		return string.Equals(internalName, name, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(NPC.GetDisplayName(internalName), name, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>The villager's own bed, which the game resolves from Data/Characters including any conditions.</summary>
	private static LocationRefInfo? GetHome(NPC villager)
	{
		string location = villager.DefaultMap;
		if (string.IsNullOrWhiteSpace(location))
			return null;

		Vector2 position = villager.DefaultPosition;

		return new LocationRefInfo(
			Location: location,
			LocationName: GetLocationDisplayName(location),
			Tile: new TileInfo((int)(position.X / 64f), (int)(position.Y / 64f))
		);
	}

	/// <summary>
	/// Turn a map's internal name into the name the game shows. A schedule only stores the internal
	/// name and the map may not be instanced, so this reads <c>Data/Locations</c> directly and falls
	/// back to the internal name for maps with no data.
	/// </summary>
	private static string GetLocationDisplayName(string location)
	{
		if (Game1.locationData.TryGetValue(location, out LocationData? data) && !string.IsNullOrWhiteSpace(data.DisplayName))
			return TokenParser.ParseText(data.DisplayName);

		return location;
	}

	private static ClockInfo ToClock(int time)
	{
		return new ClockInfo(Time: time, Name: Game1.getTimeOfDayString(time));
	}

	private static string GetFacingName(int direction)
	{
		return direction >= 0 && direction < FacingNames.Length ? FacingNames[direction] : "unknown";
	}

	private static string? Blank(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static void EnsureSaveLoaded()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			throw new CalendarException("no_save_loaded", "No save is currently loaded.", status: 503);
	}
}
