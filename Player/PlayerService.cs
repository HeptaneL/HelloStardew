using System;
using System.Collections.Generic;
using System.Linq;
using HelloStardew.Calendar;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Pets;
using StardewValley.TokenizableStrings;

namespace HelloStardew.Player;

/// <summary>
/// UI-free queries about the farmer: who they live with, how they stand with each villager, and
/// what their situation is right now.
/// </summary>
/// <remarks>
/// All methods read live game state and must run on the game's main thread.
/// Use <see cref="HelloStardew.Bridge.MainThreadDispatcher"/> when calling from an HTTP thread.
/// </remarks>
internal static class PlayerService
{
	/// <summary>Friendship points per heart. This is the game's own divisor, not a tunable.</summary>
	private const int PointsPerHeart = 250;

	/// <summary>The skill keys in the order the game stores their experience points.</summary>
	private static readonly string[] SkillNames = { "farming", "fishing", "foraging", "mining", "combat", "luck" };


	/*********
	** Household
	*********/

	/// <summary>Get the farmer, farm, spouse, pet, and children.</summary>
	public static HouseholdInfo GetHousehold()
	{
		EnsureSaveLoaded();

		Farmer farmer = Game1.player;
		NPC? spouse = farmer.getSpouse();
		Pet? pet = farmer.getPet();

		return new HouseholdInfo(
			FarmerName: farmer.Name,
			FarmName: farmer.farmName.Value,
			SpouseName: spouse?.Name,
			SpouseDisplayName: spouse?.displayName,
			DaysMarried: GetDaysMarried(farmer, spouse),
			PetName: pet?.Name,
			PetType: GetPetTypeName(pet),
			Children: GetChildren(farmer)
		);
	}

	/// <summary>Get whether the given NPC is the farmer's spouse or roommate.</summary>
	/// <remarks>
	/// Deliberately not <see cref="EnsureSaveLoaded"/>: this answers "no" when there is no save to
	/// ask about, which is the honest answer, rather than treating it as an error.
	/// </remarks>
	public static bool IsSpouse(NPC? npc)
	{
		if (npc is null || !Game1.hasLoadedGame || Game1.player is null)
			return false;

		NPC? spouse = Game1.player.getSpouse();
		return spouse is not null && npc.Name == spouse.Name;
	}

	private static int GetDaysMarried(Farmer farmer, NPC? spouse)
	{
		if (spouse is null)
			return 0;

		return farmer.friendshipData.TryGetValue(spouse.Name, out Friendship? friendship) && friendship is not null
			? friendship.DaysMarried
			: 0;
	}

	/// <summary>Get the pet's breed type as a readable name, e.g. <c>cat</c> rather than <c>Cat</c>.</summary>
	private static string? GetPetTypeName(Pet? pet)
	{
		if (pet is null)
			return null;

		string petType = pet.petType.Value;
		if (Game1.petData.TryGetValue(petType, out PetData? data) && !string.IsNullOrWhiteSpace(data?.DisplayName))
			return TokenParser.ParseText(data.DisplayName);

		return petType;
	}

	private static IReadOnlyList<ChildInfo> GetChildren(Farmer farmer)
	{
		List<ChildInfo> children = new();

		foreach (Child child in farmer.getChildren())
		{
			int ageState = child.Age;
			children.Add(new ChildInfo(
				Name: child.Name,
				DisplayName: child.displayName ?? child.Name,
				Gender: child.Gender == Gender.Male ? "male" : "female",
				AgeState: ageState,
				AgeName: GetChildAgeName(ageState),
				DaysOld: child.daysOld.Value
			));
		}

		return children;
	}

	private static string GetChildAgeName(int ageState)
	{
		return ageState switch
		{
			Child.newborn => "newborn",
			Child.baby => "baby",
			Child.crawler => "crawler",
			Child.toddler => "toddler",
			_ => "unknown"
		};
	}


	/*********
	** Relationships
	*********/

	/// <summary>Get the farmer's relationship with every villager they've met, best friends first.</summary>
	public static IReadOnlyList<RelationshipInfo> GetRelationships()
	{
		EnsureSaveLoaded();

		return Game1.player.friendshipData.Pairs
			.Select(pair => CreateRelationship(pair.Key, pair.Value))
			.Where(relationship => relationship is not null)
			.Select(relationship => relationship!)
			.OrderByDescending(relationship => relationship.FriendshipPoints)
			.ThenBy(relationship => relationship.DisplayName, StringComparer.Ordinal)
			.ToList();
	}

	/// <summary>Get the farmer's relationship with one villager, named by internal or display name.</summary>
	/// <exception cref="CalendarException">The farmer hasn't met a villager by that name.</exception>
	public static RelationshipInfo GetRelationship(string npc)
	{
		EnsureSaveLoaded();

		string? resolved = ResolveVillagerName(npc);
		if (resolved is null)
			throw new CalendarException("unknown_npc", $"The farmer hasn't met a villager called '{npc}'.", status: 404);

		RelationshipInfo? relationship = CreateRelationship(resolved, Game1.player.friendshipData[resolved]);
		if (relationship is null)
			throw new CalendarException("unknown_npc", $"'{npc}' isn't a villager the farmer can befriend.", status: 404);

		return relationship;
	}

	/// <summary>
	/// Find the internal name matching a name the caller supplied, ignoring case and accepting a
	/// display name so a model can ask for "Haley" without knowing the internal keys.
	/// </summary>
	private static string? ResolveVillagerName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return null;

		if (Game1.player.friendshipData.ContainsKey(name))
			return name;

		foreach (string key in Game1.player.friendshipData.Keys)
		{
			if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
				return key;

			if (string.Equals(NPC.GetDisplayName(key), name, StringComparison.OrdinalIgnoreCase))
				return key;
		}

		return null;
	}

	/// <summary>Build one relationship, or <c>null</c> if the name doesn't belong to a real villager.</summary>
	private static RelationshipInfo? CreateRelationship(string npcName, Friendship friendship)
	{
		NPC? npc = Game1.getCharacterFromName(npcName);
		if (npc is null)
			return null;

		return new RelationshipInfo(
			Npc: npcName,
			DisplayName: npc.displayName ?? NPC.GetDisplayName(npcName),
			Relationship: GetRelationshipWord(friendship),
			FriendshipPoints: friendship.Points,
			Hearts: friendship.Points / PointsPerHeart,
			MaxHearts: Utility.GetMaximumHeartsForCharacter(npc),
			DaysMarried: friendship.DaysMarried,
			DaysUntilWedding: friendship.IsEngaged() ? friendship.CountdownToWedding : null,
			TalkedToToday: friendship.TalkedToToday,
			GiftsThisWeek: friendship.GiftsThisWeek,
			BirthdaySeason: string.IsNullOrWhiteSpace(npc.Birthday_Season) ? null : npc.Birthday_Season,
			BirthdayDay: npc.Birthday_Day > 0 ? npc.Birthday_Day : null
		);
	}

	private static string GetRelationshipWord(Friendship friendship)
	{
		// A roommate is a platonic marriage, so it has to be checked before the status word.
		if (friendship.IsRoommate())
			return "roommate";

		return friendship.Status switch
		{
			FriendshipStatus.Married => "married",
			FriendshipStatus.Engaged => "engaged",
			FriendshipStatus.Dating => "dating",
			FriendshipStatus.Divorced => "divorced",
			_ => "friendly"
		};
	}


	/*********
	** Current state
	*********/

	/// <summary>Get where the farmer is, when it is, and how they're doing.</summary>
	public static PlayerStateInfo GetCurrentState()
	{
		EnsureSaveLoaded();

		Farmer farmer = Game1.player;
		GameLocation location = Game1.currentLocation;

		return new PlayerStateInfo(
			Year: Game1.Date.Year,
			Season: Utility.getSeasonKey(Game1.season),
			DayOfMonth: Game1.dayOfMonth,
			DayOfWeek: Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth),
			TotalDays: Game1.Date.TotalDays,
			TimeOfDay: Game1.timeOfDay,
			TimeOfDayName: Game1.getTimeOfDayString(Game1.timeOfDay),
			TimeOfDayPhase: GetTimeOfDayPhase(Game1.timeOfDay),
			LocationName: location.Name,
			LocationDisplayName: location.DisplayName ?? location.Name,
			LocationIsOutdoors: location.IsOutdoors,
			LocationIsFarm: location.IsFarm,
			IsFestivalDay: Game1.isFestival(),
			Money: farmer.Money,
			Stamina: (float)Math.Round(farmer.stamina, 1),
			MaxStamina: farmer.MaxStamina,
			Health: farmer.health,
			MaxHealth: farmer.maxHealth,
			Weather: GetWeather(),
			Skills: GetSkills(farmer),
			Inventory: GetInventory(farmer),
			CurrentToolName: farmer.CurrentTool?.DisplayName,
			CurrentItemName: farmer.CurrentItem?.DisplayName
		);
	}

	private static string GetTimeOfDayPhase(int timeOfDay)
	{
		return timeOfDay switch
		{
			< 1200 => "morning",
			< 1700 => "afternoon",
			< 2000 => "evening",
			_ => "night"
		};
	}

	/// <summary>Report the weather as a single word. Lightning implies rain, so it's checked first.</summary>
	private static string GetWeather()
	{
		if (Game1.isGreenRain)
			return "green_rain";
		if (Game1.isLightning)
			return "storm";
		if (Game1.isSnowing)
			return "snow";
		if (Game1.isRaining)
			return "rain";
		if (Game1.isDebrisWeather)
			return "wind";

		return "sunny";
	}

	private static IReadOnlyList<SkillInfo> GetSkills(Farmer farmer)
	{
		// Read the raw levels rather than Farmer.FarmingLevel and friends, which include buffs.
		int[] levels =
		{
			farmer.farmingLevel.Value,
			farmer.fishingLevel.Value,
			farmer.foragingLevel.Value,
			farmer.miningLevel.Value,
			farmer.combatLevel.Value,
			farmer.luckLevel.Value
		};

		List<SkillInfo> skills = new(levels.Length);
		for (int i = 0; i < levels.Length; i++)
		{
			skills.Add(new SkillInfo(
				Name: SkillNames[i],
				Level: levels[i],
				Experience: i < farmer.experiencePoints.Count ? farmer.experiencePoints[i] : 0
			));
		}

		return skills;
	}

	private static InventoryInfo GetInventory(Farmer farmer)
	{
		List<ItemStackInfo> items = new();
		int totalItems = 0;

		foreach (Item? item in farmer.Items)
		{
			if (item is null)
				continue;

			totalItems += item.Stack;
			items.Add(new ItemStackInfo(
				Name: item.DisplayName,
				QualifiedItemId: item.QualifiedItemId,
				Stack: item.Stack
			));
		}

		return new InventoryInfo(
			SlotsUsed: items.Count,
			SlotsTotal: farmer.MaxItems,
			TotalItems: totalItems,
			Items: items
		);
	}


	/*********
	** Recent activity and events
	*********/

	/// <summary>Get what the farmer has done over the last few in-game days, oldest first.</summary>
	public static IReadOnlyList<ActivityEntry> GetRecentActivity()
	{
		EnsureSaveLoaded();
		return ModEntry.Activity.GetRecent();
	}

	/// <summary>Get the calendar events within <paramref name="days"/> days either side of today.</summary>
	/// <param name="days">How far back and forward to look. Zero returns only today.</param>
	public static IReadOnlyList<RecentEventInfo> GetRecentEvents(int days)
	{
		EnsureSaveLoaded();

		int today = Game1.Date.TotalDays;
		List<RecentEventInfo> events = new();

		for (int offset = -days; offset <= days; offset++)
		{
			// The save starts at day 1, so looking back from a fresh farm would run off the calendar.
			int target = Math.Max(1, today + offset);
			(int year, Season season, int day) = FromTotalDays(target);

			foreach (CalendarEventInfo calendarEvent in CalendarService.GetEventsForDay(season, day))
			{
				events.Add(new RecentEventInfo(
					Type: calendarEvent.Type,
					Id: calendarEvent.Id,
					DisplayName: calendarEvent.DisplayName,
					Locked: calendarEvent.Locked,
					Season: Utility.getSeasonKey(season),
					DayOfMonth: day,
					Year: year,
					DaysOffset: target - today,
					IsPast: target < today
				));
			}
		}

		return events;
	}

	/// <summary>Turn a save-relative day count back into a date. Day 1 is spring 1 of year 1.</summary>
	private static (int Year, Season Season, int Day) FromTotalDays(int totalDays)
	{
		int zeroBased = totalDays - 1;
		int year = zeroBased / WorldDate.DaysPerYear + 1;
		int dayOfYear = zeroBased % WorldDate.DaysPerYear;

		return (
			Year: year,
			Season: (Season)(dayOfYear / WorldDate.DaysPerMonth),
			Day: dayOfYear % WorldDate.DaysPerMonth + 1
		);
	}


	/*********
	** Shared helpers
	*********/

	private static void EnsureSaveLoaded()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			throw new CalendarException("no_save_loaded", "No save is currently loaded.", status: 503);
	}
}
