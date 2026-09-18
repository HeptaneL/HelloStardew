namespace HelloStardew.Npc;

/// <summary>A position on the game's tile grid, not a pixel coordinate.</summary>
internal sealed record TileInfo(int X, int Y);

/// <summary>An in-game clock value, in the game's own <c>Game1.timeOfDay</c> domain (930 = 9:30 AM).</summary>
/// <param name="Name">The time as the game's clock would show it, already localised.</param>
internal sealed record ClockInfo(int Time, string Name);

/// <summary>A named place with an internal name, a readable name, and a tile.</summary>
/// <param name="Location">The map's internal name, e.g. <c>HaleyHouse</c>.</param>
/// <param name="LocationName">The map's display name, e.g. <c>Haley's House</c>.</param>
internal sealed record LocationRefInfo(string Location, string LocationName, TileInfo Tile);

/// <summary>Where a villager actually is right now, as opposed to where the plan says they should be.</summary>
/// <param name="IsMoving">Whether they're walking. True while a path is being followed, even between steps.</param>
/// <param name="IsTravelling">Whether they're part-way through a scheduled walk.</param>
/// <param name="TravellingTo">The map their current leg ends on, while <paramref name="IsTravelling"/> is true.</param>
/// <param name="HasReachedTarget">Whether they're standing on the current schedule stop's tile.</param>
/// <param name="IsInEvent">Whether a festival or event is running, which overrides schedules entirely.</param>
internal sealed record NpcPresenceInfo(
	string Location,
	string LocationName,
	TileInfo Tile,
	bool IsMoving,
	bool IsTravelling,
	string? TravellingTo,
	string? TravellingToName,
	bool HasReachedTarget,
	bool IsInEvent);

/// <summary>One stop in a villager's plan for today.</summary>
/// <param name="Time">The time the game starts walking toward this stop, not the time of arrival.</param>
/// <param name="FacingDirection">0 up, 1 right, 2 down, 3 left, as the game stores it.</param>
/// <param name="Facing">The <paramref name="FacingDirection"/> as a word.</param>
/// <param name="EndOfRouteBehavior">A scripted behaviour on arrival, e.g. <c>sleep</c>, or <c>null</c> for none.</param>
/// <param name="EndOfRouteMessage">A dialogue key the villager says on arrival, or <c>null</c>.</param>
internal sealed record ScheduleStopInfo(
	ClockInfo Time,
	LocationRefInfo Destination,
	int FacingDirection,
	string Facing,
	string? EndOfRouteBehavior,
	string? EndOfRouteMessage);

/// <summary>Where to find one villager now, and where they'll be later today.</summary>
/// <param name="Now">The current in-game time this answer was computed for.</param>
/// <param name="Current">The live position, which is what to trust when it disagrees with the plan.</param>
/// <param name="Home">The villager's own bed location, which is where they are before their day starts.</param>
/// <param name="ScheduleKey">Which schedule the game picked for today, e.g. <c>spring</c>, <c>rain</c>, or <c>marriage</c>.</param>
/// <param name="CurrentTarget">The stop that should be in effect now, or <c>null</c> before their day starts.</param>
/// <param name="NextTarget">The stop after that, or <c>null</c> once the day's plan is finished.</param>
internal sealed record NpcLocationInfo(
	string Npc,
	string DisplayName,
	ClockInfo Now,
	NpcPresenceInfo Current,
	LocationRefInfo? Home,
	string? ScheduleKey,
	ScheduleStopInfo? CurrentTarget,
	ScheduleStopInfo? NextTarget);
