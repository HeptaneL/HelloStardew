using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace HelloStardew.Player;

/// <summary>
/// Records what the farmer has been doing, by diffing cheap game-state snapshots on a timer and
/// keeping a rolling history that's persisted in the save file.
/// </summary>
/// <remarks>
/// This deliberately avoids Harmony patches and per-action SMAPI events. The counters and
/// friendship flags in the save already reveal when something happened, so diffing them is both
/// less invasive and more robust than hooking each action's code path.
/// </remarks>
internal sealed class ActivityTracker
{
	/*********
	** Consts
	*********/

	/// <summary>The key the history is stored under in the save file.</summary>
	private const string SaveKey = "helloStardewActivity";

	/// <summary>How many days of activity to keep, counting today.</summary>
	private const int RetentionDays = 3;

	/// <summary>A hard cap so a busy day can't grow the save data without bound.</summary>
	private const int MaxEntries = 300;

	/// <summary>
	/// How many ticks to wait between samples. At 60 ticks per second this is about once a second,
	/// which is far finer than the in-game clock while still costing nothing noticeable.
	/// </summary>
	private const int SampleIntervalTicks = 60;

	/// <summary>Ignore money swings smaller than this, so routine buying doesn't drown the log.</summary>
	private const int MoneyNoiseThreshold = 100;

	/// <summary>The in-game minutes per bucket used to collapse rapid movement into one entry.</summary>
	private const int LocationBucketMinutes = 10;

	private static readonly string[] SkillNames = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };


	/*********
	** Fields
	*********/

	private readonly IMonitor _monitor;
	private readonly List<ActivityEntry> _entries = new();
	private Snapshot? _previous;
	private int _ticksSinceSample;


	/*********
	** Public methods
	*********/

	public ActivityTracker(IMonitor monitor)
	{
		this._monitor = monitor;
	}

	/// <summary>
	/// Sample the game state and record anything new. Call every tick on the game's main thread;
	/// it samples on its own schedule and cheaply does nothing in between.
	/// </summary>
	public void OnUpdateTicked()
	{
		if (++this._ticksSinceSample < SampleIntervalTicks)
			return;

		this._ticksSinceSample = 0;

		try
		{
			this.Capture();
		}
		catch (Exception ex)
		{
			// The activity log is a nice-to-have. Never let it break the game loop or the HTTP API.
			this._monitor.LogOnce("Sampling player activity failed; the activity log will be incomplete.", LogLevel.Warn);
			this._monitor.Log($"Activity sample failed: {ex}", LogLevel.Trace);
			this._previous = null;
		}
	}

	/// <summary>Drop entries that have fallen outside the retention window.</summary>
	public void OnDayStarted()
	{
		this.Prune();
	}

	/// <summary>Load the stored history. Call after a save is loaded.</summary>
	public void OnSaveLoaded()
	{
		this._entries.Clear();
		this._previous = null;

		if (!Context.IsMainPlayer)
			return;

		try
		{
			ActivityLog? log = ModEntry.ModHelper.Data.ReadSaveData<ActivityLog>(SaveKey);
			if (log?.Entries is not null)
				this._entries.AddRange(log.Entries);
		}
		catch (Exception ex)
		{
			this._monitor.Log($"Could not read the activity history from the save: {ex}", LogLevel.Warn);
		}

		this.Prune();
	}

	/// <summary>Write the history into the save file. Call while the game is saving.</summary>
	public void OnSaving()
	{
		// Only the host owns save data, and there's nothing to write outside a loaded game.
		if (!Context.IsMainPlayer || !Game1.hasLoadedGame || Game1.player is null)
			return;

		try
		{
			this.Prune();
			ModEntry.ModHelper.Data.WriteSaveData(SaveKey, new ActivityLog(this._entries.ToArray()));
		}
		catch (Exception ex)
		{
			this._monitor.Log($"Could not write the activity history to the save: {ex}", LogLevel.Warn);
		}
	}

	/// <summary>Get the recorded activity, oldest first.</summary>
	public IReadOnlyList<ActivityEntry> GetRecent()
	{
		return this._entries.ToArray();
	}


	/*********
	** Sampling
	*********/

	private void Capture()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			return;

		Snapshot current = Snapshot.Create();

		// The first sample after loading a save has nothing to compare against.
		if (this._previous is not null)
			this.Compare(this._previous, current);

		this._previous = current;
	}

	private void Compare(Snapshot previous, Snapshot current)
	{
		this.RecordCounter(previous.FishCaught, current.FishCaught, "fish", count => $"Caught {count} fish");
		this.RecordCounter(previous.ItemsShipped, current.ItemsShipped, "ship", count => $"Shipped {count} items");
		this.RecordCounter(previous.MonstersKilled, current.MonstersKilled, "combat", count => $"Defeated {count} monsters");
		this.RecordCounter(previous.ItemsForaged, current.ItemsForaged, "forage", count => $"Foraged {count} items");
		this.RecordCounter(previous.ItemsCrafted, current.ItemsCrafted, "craft", count => $"Crafted {count} items");
		this.RecordCounter(previous.ItemsCooked, current.ItemsCooked, "cook", count => $"Cooked {count} items");
		this.RecordCounter(previous.GeodesCracked, current.GeodesCracked, "geode", count => $"Cracked {count} geodes");
		this.RecordCounter(
			previous.QuestsCompleted,
			current.QuestsCompleted,
			"quest",
			count => count == 1 ? "Completed a quest" : $"Completed {count} quests"
		);

		this.RecordMoney(previous.Money, current.Money);
		this.RecordSkillLevels(previous.SkillLevels, current.SkillLevels);
		this.RecordFriendship(previous, current);
		this.RecordLocation(previous.Location, current.Location);
	}

	private void RecordCounter(uint previous, uint current, string type, Func<int, string> describe)
	{
		// Counters only ever climb, but a reload or a mod can still reset one.
		if (current <= previous)
			return;

		int delta = (int)(current - previous);
		this.Add(type, describe(delta), delta, npc: null);
	}

	private void RecordMoney(int previous, int current)
	{
		int delta = current - previous;

		if (delta >= MoneyNoiseThreshold)
			this.Add("money", $"Earned {delta}g", delta, npc: null);
		else if (delta <= -MoneyNoiseThreshold)
			this.Add("money", $"Spent {-delta}g", -delta, npc: null);
	}

	private void RecordSkillLevels(int[] previous, int[] current)
	{
		int count = Math.Min(previous.Length, current.Length);

		for (int i = 0; i < count; i++)
		{
			if (current[i] > previous[i])
				this.Add("levelup", $"{SkillNames[i]} reached level {current[i]}", current[i], npc: null);
		}
	}

	/// <summary>Detect conversations and gifts from the per-villager friendship flags.</summary>
	private void RecordFriendship(Snapshot previous, Snapshot current)
	{
		foreach ((string npc, bool talkedToday) in current.TalkedToToday)
		{
			if (!talkedToday || previous.TalkedToToday.TryGetValue(npc, out bool wasTalked) && wasTalked)
				continue;

			this.Add("talk", $"Talked to {NPC.GetDisplayName(npc)}", 1, npc);
		}

		foreach ((string npc, int giftsToday) in current.GiftsToday)
		{
			previous.GiftsToday.TryGetValue(npc, out int giftsBefore);
			if (giftsToday <= giftsBefore)
				continue;

			this.Add("gift", $"Gave {NPC.GetDisplayName(npc)} a gift", giftsToday - giftsBefore, npc);
		}
	}

	private void RecordLocation(string previous, string current)
	{
		if (string.IsNullOrEmpty(previous) || string.Equals(previous, current, StringComparison.Ordinal))
			return;

		string locationName = Game1.currentLocation.DisplayName ?? current;
		ActivityEntry? last = this._entries.Count > 0 ? this._entries[^1] : null;

		// Collapse fast movement such as descending mine levels into one entry per time bucket,
		// so a trip underground doesn't flush the rest of the day out of the log.
		if (last is not null
			&& last.Type == "location"
			&& last.TotalDays == Game1.Date.TotalDays
			&& last.TimeOfDay / LocationBucketMinutes == Game1.timeOfDay / LocationBucketMinutes)
		{
			this._entries[^1] = this.CreateEntry("location", $"Went to {locationName}", 1, npc: null);
			return;
		}

		this.Add("location", $"Went to {locationName}", 1, npc: null);
	}

	private void Add(string type, string text, int amount, string? npc)
	{
		this._entries.Add(this.CreateEntry(type, text, amount, npc));
	}

	private ActivityEntry CreateEntry(string type, string text, int amount, string? npc)
	{
		WorldDate date = Game1.Date;

		return new ActivityEntry(
			Type: type,
			Text: text,
			Amount: amount,
			Npc: npc,
			Year: date.Year,
			Season: Utility.getSeasonKey(date.Season),
			DayOfMonth: date.DayOfMonth,
			TotalDays: date.TotalDays,
			TimeOfDay: Game1.timeOfDay
		);
	}

	private void Prune()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			return;

		// RetentionDays counts today, so keep today and the two days before it.
		int cutoff = Game1.Date.TotalDays - (RetentionDays - 1);
		this._entries.RemoveAll(entry => entry.TotalDays < cutoff);

		if (this._entries.Count > MaxEntries)
			this._entries.RemoveRange(0, this._entries.Count - MaxEntries);
	}


	/*********
	** Snapshots
	*********/

	/// <summary>The cheap-to-read game values compared between samples.</summary>
	private sealed class Snapshot
	{
		public uint FishCaught { get; init; }
		public uint ItemsShipped { get; init; }
		public uint MonstersKilled { get; init; }
		public uint ItemsForaged { get; init; }
		public uint ItemsCrafted { get; init; }
		public uint ItemsCooked { get; init; }
		public uint GeodesCracked { get; init; }
		public uint QuestsCompleted { get; init; }
		public int Money { get; init; }
		public string Location { get; init; } = "";
		public int[] SkillLevels { get; init; } = Array.Empty<int>();
		public Dictionary<string, bool> TalkedToToday { get; init; } = new();
		public Dictionary<string, int> GiftsToday { get; init; } = new();

		public static Snapshot Create()
		{
			Farmer farmer = Game1.player;
			Stats stats = farmer.stats;

			Dictionary<string, bool> talkedToToday = new();
			Dictionary<string, int> giftsToday = new();
			foreach ((string npc, Friendship friendship) in farmer.friendshipData.Pairs)
			{
				talkedToToday[npc] = friendship.TalkedToToday;
				giftsToday[npc] = friendship.GiftsToday;
			}

			return new Snapshot
			{
				FishCaught = stats.FishCaught,
				ItemsShipped = stats.ItemsShipped,
				MonstersKilled = stats.MonstersKilled,
				ItemsForaged = stats.ItemsForaged,
				ItemsCrafted = stats.ItemsCrafted,
				ItemsCooked = stats.ItemsCooked,
				GeodesCracked = stats.GeodesCracked,
				QuestsCompleted = stats.QuestsCompleted,
				Money = farmer.Money,
				Location = Game1.currentLocation.Name,

				// Read the raw levels, not Farmer.FarmingLevel and friends, which add buffs and
				// would otherwise look like a level-up every time a buff wore off.
				SkillLevels = new[]
				{
					farmer.farmingLevel.Value,
					farmer.fishingLevel.Value,
					farmer.foragingLevel.Value,
					farmer.miningLevel.Value,
					farmer.combatLevel.Value,
					farmer.luckLevel.Value
				},

				TalkedToToday = talkedToToday,
				GiftsToday = giftsToday
			};
		}
	}
}
