using System.Collections.Generic;

namespace HelloStardew.Gift;

/// <summary>
/// One entry in a gift-taste list. The game's data mixes three things in the same space-separated
/// field, so each entry says which kind it is instead of pretending they're all items.
/// </summary>
/// <param name="Id">
/// The token as the game stores it: a qualified item ID such as <c>(O)66</c>, a negative category
/// number such as <c>-4</c>, or a context tag such as <c>category_fish</c>.
/// </param>
/// <param name="Name">
/// A readable name: the item's display name, the category's name, or the raw tag when the game has
/// no name for it.
/// </param>
/// <param name="Kind">One of <c>item</c>, <c>category</c>, or <c>context_tag</c>.</param>
internal sealed record GiftEntryInfo(
	string Id,
	string Name,
	string Kind);

/// <summary>One villager's tastes, one list per reaction. Each list may be empty.</summary>
internal sealed record GiftTasteSetInfo(
	IReadOnlyList<GiftEntryInfo> Love,
	IReadOnlyList<GiftEntryInfo> Like,
	IReadOnlyList<GiftEntryInfo> Dislike,
	IReadOnlyList<GiftEntryInfo> Hate,
	IReadOnlyList<GiftEntryInfo> Neutral);

/// <summary>One villager's gift tastes, keyed by the name the game uses internally.</summary>
internal sealed record VillagerGiftTastesInfo(
	string Npc,
	string DisplayName,
	GiftTasteSetInfo Tastes);

/// <summary>
/// The whole <c>Data/NPCGiftTastes</c> asset: the universal lists that apply to everyone, plus
/// every villager's own list.
/// </summary>
internal sealed record GiftTasteCatalogInfo(
	GiftTasteSetInfo Universal,
	IReadOnlyList<VillagerGiftTastesInfo> Villagers);

/// <summary>Where one item is sitting, so the agent can tell the farmer where to find it.</summary>
/// <param name="Kind">Either <c>inventory</c> or <c>chest</c>.</param>
/// <param name="Location">The location holding the chest, or the farmer's location for the inventory.</param>
/// <param name="Stack">How many are in this slot.</param>
internal sealed record GiftItemSourceInfo(
	string Kind,
	string Location,
	int Stack);

/// <summary>One item the farmer is holding, scored against a villager's tastes.</summary>
/// <param name="Taste">One of <c>love</c>, <c>like</c>, <c>dislike</c>, <c>hate</c>, or <c>stardrop_tea</c>.</param>
/// <param name="TasteLevel">The game's own taste constant, as returned by <c>NPC.getGiftTasteForThisItem</c>.</param>
/// <param name="Quality">The best quality the farmer owns of this item: 0 normal, 1 silver, 2 gold, 4 iridium.</param>
/// <param name="Points">
/// Friendship points one gift of this item is worth right now, already including the quality
/// multiplier and any birthday or spouse multiplier.
/// </param>
/// <param name="TotalCount">How many the farmer owns across the inventory and every chest.</param>
internal sealed record GiftOptionInfo(
	string Name,
	string QualifiedItemId,
	string Taste,
	int TasteLevel,
	int Quality,
	string QualityName,
	int Points,
	int TotalCount,
	IReadOnlyList<GiftItemSourceInfo> Sources);

/// <summary>Whether the villager will still accept a gift, and what one is worth today.</summary>
/// <param name="GiftsToday">Gifts already given to this villager today.</param>
/// <param name="GiftsThisWeek">Gifts already given to this villager this week.</param>
/// <param name="MaxGiftsPerWeek">The game's own weekly cap for a normal gift.</param>
/// <param name="CanGiveToday">Whether a gift today would still count toward friendship.</param>
/// <param name="WeeklyLimitReached">Whether the weekly cap is used up for a normal gift.</param>
/// <param name="IsBirthday">Whether today is this villager's birthday.</param>
/// <param name="FriendshipMultiplier">
/// What the game will multiply this villager's friendship gain by: 8 on their birthday, halved when
/// the farmer is married to them, and 1 otherwise.
/// </param>
/// <param name="BlockedReason">
/// Why the villager won't accept any gift, or <c>null</c> when they will. One of
/// <c>cannot_receive_gifts</c> or <c>divorced</c>.
/// </param>
internal sealed record GiftLimitInfo(
	int GiftsToday,
	int GiftsThisWeek,
	int MaxGiftsPerWeek,
	bool CanGiveToday,
	bool WeeklyLimitReached,
	bool IsBirthday,
	float FriendshipMultiplier,
	string? BlockedReason);

/// <summary>What to give one villager, and what to keep out of the box.</summary>
/// <param name="Suggestions">Held items they love or like, best first.</param>
/// <param name="Avoid">Held items they dislike or hate, worst first.</param>
internal sealed record GiftAdviceInfo(
	string Npc,
	string DisplayName,
	GiftLimitInfo Limits,
	IReadOnlyList<GiftOptionInfo> Suggestions,
	IReadOnlyList<GiftOptionInfo> Avoid);
