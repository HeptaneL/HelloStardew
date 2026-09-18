using System.Collections.Generic;

namespace HelloStardew.Quests;

/// <summary>One line of a quest's checklist, as the journal shows it.</summary>
/// <param name="Text">The already-localized objective text, e.g. <c>Catch 3 Flounder</c>.</param>
/// <param name="CurrentCount">
/// How far along this objective is, or <c>null</c> when the game doesn't expose a counter.
/// Normal quests keep their progress in a pre-formatted string, so only special orders have this.
/// </param>
/// <param name="RequiredCount">What <paramref name="CurrentCount"/> has to reach, or <c>null</c> when there's no counter.</param>
internal sealed record QuestObjectiveInfo(
	string Text,
	int? CurrentCount,
	int? RequiredCount,
	bool IsComplete);

/// <summary>
/// One quest the farmer hasn't finished yet. Normal quests and special orders share this shape so
/// the agent can read the journal as a single list.
/// </summary>
/// <param name="Id">The quest ID from <c>Data/Quests</c>, or the special order's quest key.</param>
/// <param name="Type">
/// A readable kind: <c>basic</c>, <c>crafting</c>, <c>item_delivery</c>, <c>monster</c>,
/// <c>socialize</c>, <c>location</c>, <c>fishing</c>, <c>building</c>, <c>harvest</c>,
/// <c>resource</c>, <c>weeding</c>, or <c>special_order</c>.
/// </param>
/// <param name="QuestType">The game's own quest type constant, or <c>null</c> for a special order.</param>
/// <param name="IsDailyQuest">True for a billboard quest, which expires after a couple of days.</param>
/// <param name="IsSpecialOrder">True for a <c>Data/SpecialOrders</c> order from the special orders board.</param>
/// <param name="IsTimed">Whether the quest has a deadline at all; <see cref="DaysLeft"/> is meaningless when false.</param>
/// <param name="DaysLeft">Days remaining before it expires. Zero when it isn't timed.</param>
/// <param name="MoneyReward">Gold promised on completion, before the player claims it.</param>
/// <param name="RewardDescription">The non-money reward's description, if the quest has one.</param>
/// <param name="Requester">Who asked for a special order, as a readable name; <c>null</c> for normal quests.</param>
internal sealed record QuestInfo(
	string Id,
	string Title,
	string Description,
	string Type,
	int? QuestType,
	bool IsDailyQuest,
	bool IsSpecialOrder,
	bool CanBeCancelled,
	bool IsTimed,
	int DaysLeft,
	int MoneyReward,
	string? RewardDescription,
	string? Requester,
	IReadOnlyList<QuestObjectiveInfo> Objectives);
