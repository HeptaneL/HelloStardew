using System.Collections.Generic;

namespace HelloStardew.Player;

/// <summary>One of the farmer's children.</summary>
/// <param name="AgeState">The game's growth stage: 0 newborn, 1 baby, 2 crawler, 3 toddler.</param>
/// <param name="AgeName">The <paramref name="AgeState"/> as a readable word.</param>
internal sealed record ChildInfo(
	string Name,
	string DisplayName,
	string Gender,
	int AgeState,
	string AgeName,
	int DaysOld);

/// <summary>The people, pet, and children the farmer shares the farm with.</summary>
/// <param name="SpouseName">The spouse's internal name, or <c>null</c> if the farmer isn't married or roommates.</param>
/// <param name="PetName">The name the player gave their pet, which is not the same as the pet type.</param>
/// <param name="PetType">The pet's breed type, e.g. <c>cat</c> or <c>dog</c>.</param>
internal sealed record HouseholdInfo(
	string FarmerName,
	string FarmName,
	string? SpouseName,
	string? SpouseDisplayName,
	int DaysMarried,
	string? PetName,
	string? PetType,
	IReadOnlyList<ChildInfo> Children);

/// <summary>How the farmer stands with one villager.</summary>
/// <param name="Relationship">One of <c>friendly</c>, <c>dating</c>, <c>engaged</c>, <c>married</c>, <c>roommate</c>, or <c>divorced</c>.</param>
/// <param name="Hearts">The game's own arithmetic: friendship points divided by 250.</param>
/// <param name="MaxHearts">How many hearts this villager can reach, which depends on whether they're datable and on the current relationship.</param>
/// <param name="DaysUntilWedding">Days until the wedding, or <c>null</c> when not engaged.</param>
/// <param name="BirthdaySeason">A season key such as <c>spring</c>, or <c>null</c> for villagers with no birthday.</param>
internal sealed record RelationshipInfo(
	string Npc,
	string DisplayName,
	string Relationship,
	int FriendshipPoints,
	int Hearts,
	int MaxHearts,
	int DaysMarried,
	int? DaysUntilWedding,
	bool TalkedToToday,
	int GiftsThisWeek,
	string? BirthdaySeason,
	int? BirthdayDay);

/// <summary>One of the farmer's six skills.</summary>
internal sealed record SkillInfo(
	string Name,
	int Level,
	int Experience);

/// <summary>One inventory slot's contents.</summary>
internal sealed record ItemStackInfo(
	string Name,
	string QualifiedItemId,
	int Stack);

/// <summary>What the farmer is carrying right now, so the agent can see what they have on hand.</summary>
internal sealed record InventoryInfo(
	int SlotsUsed,
	int SlotsTotal,
	int TotalItems,
	IReadOnlyList<ItemStackInfo> Items);

/// <summary>A snapshot of the farmer's situation: where they are, when, and how they're doing.</summary>
/// <param name="TimeOfDayName">The clock time formatted the way the game shows it, e.g. <c>6:00 PM</c>.</param>
/// <param name="TimeOfDayPhase">A coarse bucket the agent can reason about: <c>morning</c>, <c>afternoon</c>, <c>evening</c>, or <c>night</c>.</param>
/// <param name="Weather">One of <c>sunny</c>, <c>wind</c>, <c>rain</c>, <c>storm</c>, <c>snow</c>, or <c>green_rain</c>.</param>
/// <param name="Stamina">Remaining energy. The game stores this as a float.</param>
internal sealed record PlayerStateInfo(
	int Year,
	string Season,
	int DayOfMonth,
	string DayOfWeek,
	int TotalDays,
	int TimeOfDay,
	string TimeOfDayName,
	string TimeOfDayPhase,
	string LocationName,
	string LocationDisplayName,
	bool LocationIsOutdoors,
	bool LocationIsFarm,
	bool IsFestivalDay,
	int Money,
	float Stamina,
	int MaxStamina,
	int Health,
	int MaxHealth,
	string Weather,
	IReadOnlyList<SkillInfo> Skills,
	InventoryInfo Inventory,
	string? CurrentToolName,
	string? CurrentItemName);

/// <summary>
/// Something the farmer did, as recorded by <see cref="ActivityTracker"/>.
/// </summary>
/// <remarks>
/// This record doubles as the model persisted in the save file, so renaming its properties
/// invalidates activity logs written by older versions of the mod.
/// </remarks>
/// <param name="Type">What kind of action this was, e.g. <c>fish</c>, <c>gift</c>, or <c>location</c>.</param>
/// <param name="Text">A readable description, e.g. <c>Gave Abigail a gift</c>.</param>
/// <param name="Amount">How many times, or how much. Always at least 1.</param>
/// <param name="Npc">The villager this involved, for <c>talk</c> and <c>gift</c> entries.</param>
internal sealed record ActivityEntry(
	string Type,
	string Text,
	int Amount,
	string? Npc,
	int Year,
	string Season,
	int DayOfMonth,
	int TotalDays,
	int TimeOfDay);

/// <summary>The activity history as stored in the save file.</summary>
internal sealed record ActivityLog(ActivityEntry[] Entries);

/// <summary>A calendar event in the days around the current date.</summary>
/// <param name="DaysOffset">Days from today. Negative is past, 0 is today, positive is upcoming.</param>
internal sealed record RecentEventInfo(
	string Type,
	string Id,
	string DisplayName,
	bool Locked,
	string Season,
	int DayOfMonth,
	int Year,
	int DaysOffset,
	bool IsPast);
