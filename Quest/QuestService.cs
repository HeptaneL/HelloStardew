using System;
using System.Collections.Generic;
using System.Linq;
using HelloStardew.Calendar;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace HelloStardew.Quests;

/// <summary>
/// UI-free queries about the farmer's journal: which quests are still open, and what each of them
/// is waiting on.
/// </summary>
/// <remarks>
/// Reads the live quest log and special orders, so it must run on the game's main thread.
/// Use <see cref="HelloStardew.Bridge.MainThreadDispatcher"/> when calling from an HTTP thread.
/// </remarks>
internal static class QuestService
{
	/// <summary>The shortest reward description the game counts as a real reward rather than a placeholder.</summary>
	private const int MinRewardDescriptionLength = 3;

	/// <summary>
	/// Get every quest the farmer has accepted but not completed: normal journal quests and
	/// special orders, most urgent first.
	/// </summary>
	/// <remarks>
	/// A quest whose objective is done but whose reward hasn't been claimed yet is <em>not</em>
	/// included, because the game already marks it complete. Those stay in the quest log until the
	/// farmer collects the reward.
	/// </remarks>
	public static IReadOnlyList<QuestInfo> GetIncompleteQuests()
	{
		EnsureSaveLoaded();

		List<QuestInfo> quests = new();

		foreach (Quest quest in Game1.player.questLog)
		{
			if (quest is null || quest.completed.Value || quest.destroy.Value || quest.IsHidden())
				continue;

			quests.Add(CreateQuest(quest));
		}

		foreach (SpecialOrder order in Game1.player.team.specialOrders)
		{
			if (order is null || order.questState.Value != SpecialOrderStatus.InProgress || order.IsHidden())
				continue;

			quests.Add(CreateSpecialOrder(order));
		}

		// Untimed quests have no deadline, so they sink below every timed one instead of pretending
		// their DaysLeft of 0 means "due now".
		return quests
			.OrderBy(quest => quest.IsTimed ? quest.DaysLeft : int.MaxValue)
			.ThenBy(quest => quest.Title, StringComparer.Ordinal)
			.ToList();
	}


	/*********
	** Normal quests
	*********/

	private static QuestInfo CreateQuest(Quest quest)
	{
		return new QuestInfo(
			Id: quest.id.Value,
			Title: quest.GetName(),
			Description: quest.GetDescription(),
			Type: GetQuestTypeName(quest.questType.Value),
			QuestType: quest.questType.Value,
			IsDailyQuest: quest.dailyQuest.Value,
			IsSpecialOrder: false,
			CanBeCancelled: quest.CanBeCancelled(),
			IsTimed: quest.IsTimedQuest(),
			DaysLeft: quest.GetDaysLeft(),
			MoneyReward: quest.GetMoneyReward(),
			RewardDescription: GetRewardDescription(quest),
			Requester: null,
			Objectives: GetObjectives(quest)
		);
	}

	/// <summary>
	/// Read the quest's objectives. The game exposes these as finished sentences with the progress
	/// already baked in (e.g. "0/5 caught"), so there's no counter left to report separately.
	/// </summary>
	private static IReadOnlyList<QuestObjectiveInfo> GetObjectives(Quest quest)
	{
		List<QuestObjectiveInfo> objectives = new();

		foreach (string description in quest.GetObjectiveDescriptions())
		{
			if (string.IsNullOrWhiteSpace(description))
				continue;

			objectives.Add(new QuestObjectiveInfo(
				Text: description,
				CurrentCount: null,
				RequiredCount: null,
				IsComplete: false
			));
		}

		return objectives;
	}

	/// <summary>
	/// Get the non-money reward's text, or <c>null</c> when the quest has none. The game uses
	/// strings of two characters or less as "no reward" placeholders.
	/// </summary>
	private static string? GetRewardDescription(Quest quest)
	{
		string? description = quest.rewardDescription.Value;
		return !string.IsNullOrEmpty(description) && description.Length >= MinRewardDescriptionLength
			? description
			: null;
	}

	/// <summary>
	/// Turn the game's quest type constant into a readable word. The game has no enum for this, only
	/// the <c>Quest.type_*</c> constants.
	/// </summary>
	private static string GetQuestTypeName(int questType)
	{
		return questType switch
		{
			Quest.type_basic => "basic",
			Quest.type_crafting => "crafting",
			Quest.type_itemDelivery => "item_delivery",
			Quest.type_monster => "monster",
			Quest.type_socialize => "socialize",
			Quest.type_location => "location",
			Quest.type_fishing => "fishing",
			Quest.type_building => "building",
			Quest.type_harvest => "harvest",
			Quest.type_resource => "resource",
			Quest.type_weeding => "weeding",
			_ => "unknown"
		};
	}


	/*********
	** Special orders
	*********/

	private static QuestInfo CreateSpecialOrder(SpecialOrder order)
	{
		return new QuestInfo(
			Id: order.questKey.Value,
			Title: order.GetName(),
			Description: order.GetDescription(),
			Type: "special_order",
			QuestType: null,
			IsDailyQuest: false,
			IsSpecialOrder: true,
			CanBeCancelled: order.CanBeCancelled(),
			IsTimed: order.IsTimedQuest(),
			DaysLeft: order.GetDaysLeft(),
			MoneyReward: order.GetMoneyReward(),
			RewardDescription: null,
			Requester: GetRequester(order),
			Objectives: GetObjectives(order)
		);
	}

	/// <summary>
	/// Get each objective's text with its token substitutions resolved, plus its progress counters so
	/// the agent can see how far along the order is.
	/// </summary>
	private static IReadOnlyList<QuestObjectiveInfo> GetObjectives(SpecialOrder order)
	{
		List<QuestObjectiveInfo> objectives = new();

		foreach (OrderObjective objective in order.objectives)
		{
			if (objective is null)
				continue;

			int maxCount = objective.GetMaxCount();
			bool hasCounter = maxCount > 1;

			objectives.Add(new QuestObjectiveInfo(
				Text: order.Parse(objective.GetDescription()),
				CurrentCount: hasCounter ? objective.GetCount() : null,
				RequiredCount: hasCounter ? maxCount : null,
				IsComplete: objective.IsComplete()
			));
		}

		return objectives;
	}

	/// <summary>Get the requester's readable name, falling back to the raw value if it isn't a known NPC.</summary>
	private static string? GetRequester(SpecialOrder order)
	{
		string requester = order.requester.Value;
		if (string.IsNullOrWhiteSpace(requester))
			return null;

		string displayName = NPC.GetDisplayName(requester);
		return string.IsNullOrWhiteSpace(displayName) ? requester : displayName;
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
