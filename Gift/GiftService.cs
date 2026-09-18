using System;
using System.Collections.Generic;
using System.Linq;
using HelloStardew.Calendar;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;

namespace HelloStardew.Gift;

/// <summary>
/// UI-free queries about gifts: what each villager likes, and which of the farmer's own items is
/// worth handing over.
/// </summary>
/// <remarks>
/// Reads <c>Data/NPCGiftTastes</c> and the live inventory, so it must run on the game's main thread.
/// Use <see cref="HelloStardew.Bridge.MainThreadDispatcher"/> when calling from an HTTP thread.
/// </remarks>
internal static class GiftService
{
	/// <summary>
	/// The game's own taste constants. They are deliberately not in order: dislike and hate are 4
	/// and 6, while neutral is 8.
	/// </summary>
	private const int TasteLove = 0;

	private const int TasteLike = 2;
	private const int TasteDislike = 4;
	private const int TasteHate = 6;

	/// <summary>The taste a Stardrop Tea always gets, which beats every normal gift.</summary>
	private const int TasteStardropTea = 7;

	/// <summary>Friendship points each taste is worth, before the quality and date multipliers.</summary>
	private const int LovePoints = 80;

	private const int LikePoints = 45;
	private const int NeutralPoints = 20;
	private const int DislikePoints = -20;
	private const int HatePoints = -40;

	/// <summary>A Stardrop Tea's flat gain, which the game caps and never scales with quality.</summary>
	private const int StardropTeaPoints = 250;

	private const int StardropTeaMaxPoints = 750;

	/// <summary>What the game multiplies a birthday gift by.</summary>
	private const float BirthdayMultiplier = 8f;

	/// <summary>The keys in <c>Data/NPCGiftTastes</c> that describe everyone rather than one villager.</summary>
	private const string UniversalPrefix = "Universal_";


	/*********
	** Taste catalog
	*********/

	/// <summary>Get every villager's gift tastes, plus the universal lists that apply to all of them.</summary>
	public static GiftTasteCatalogInfo GetGiftTastes()
	{
		EnsureSaveLoaded();

		return new GiftTasteCatalogInfo(
			Universal: ReadUniversalTastes(),
			Villagers: GetVillagerNames().Select(CreateVillagerTastes).ToList()
		);
	}

	/// <summary>Get one villager's gift tastes, named by internal or display name.</summary>
	/// <exception cref="CalendarException">No villager goes by that name.</exception>
	public static VillagerGiftTastesInfo GetGiftTastes(string npc)
	{
		EnsureSaveLoaded();
		return CreateVillagerTastes(ResolveVillagerName(npc));
	}

	/// <summary>Read the five <c>Universal_*</c> lists, which hold bare tokens rather than the slash format.</summary>
	private static GiftTasteSetInfo ReadUniversalTastes()
	{
		return new GiftTasteSetInfo(
			Love: ParseTokens(GetUniversalRaw("Universal_Love")),
			Like: ParseTokens(GetUniversalRaw("Universal_Like")),
			Dislike: ParseTokens(GetUniversalRaw("Universal_Dislike")),
			Hate: ParseTokens(GetUniversalRaw("Universal_Hate")),
			Neutral: ParseTokens(GetUniversalRaw("Universal_Neutral"))
		);
	}

	private static string GetUniversalRaw(string key)
	{
		return Game1.NPCGiftTastes.TryGetValue(key, out string? raw) ? raw : string.Empty;
	}

	private static VillagerGiftTastesInfo CreateVillagerTastes(string npc)
	{
		string raw = Game1.NPCGiftTastes.TryGetValue(npc, out string? value) ? value : string.Empty;
		string[] fields = raw.Split('/');

		// The value alternates a response line with an item list, so the items sit at the odd
		// indexes: 1 love, 3 like, 5 dislike, 7 hate, 9 neutral.
		return new VillagerGiftTastesInfo(
			Npc: npc,
			DisplayName: NPC.GetDisplayName(npc),
			Tastes: new GiftTasteSetInfo(
				Love: ParseTasteField(fields, 1),
				Like: ParseTasteField(fields, 3),
				Dislike: ParseTasteField(fields, 5),
				Hate: ParseTasteField(fields, 7),
				Neutral: ParseTasteField(fields, 9)
			)
		);
	}

	/// <summary>Read one slash-delimited field, tolerating mods that supply fewer than ten of them.</summary>
	private static IReadOnlyList<GiftEntryInfo> ParseTasteField(string[] fields, int index)
	{
		return index < fields.Length ? ParseTokens(fields[index]) : Array.Empty<GiftEntryInfo>();
	}

	private static IReadOnlyList<GiftEntryInfo> ParseTokens(string raw)
	{
		List<GiftEntryInfo> entries = new();

		foreach (string token in ArgUtility.SplitBySpace(raw))
		{
			GiftEntryInfo? entry = ResolveEntry(token);
			if (entry is not null)
				entries.Add(entry);
		}

		return entries;
	}

	/// <summary>
	/// Classify one raw token the way the game does: a negative number is an object category, a
	/// digit or <c>(</c> starts an item ID, and anything else is a context tag.
	/// </summary>
	private static GiftEntryInfo? ResolveEntry(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
			return null;

		// Categories are only ever matched against Object.Category, so they never name an item.
		if (token[0] == '-' && int.TryParse(token, out int category))
		{
			string name = StardewValley.Object.GetCategoryDisplayName(category);
			return new GiftEntryInfo(
				Id: token,
				Name: string.IsNullOrWhiteSpace(name) ? $"category {category}" : name,
				Kind: "category");
		}

		if (token[0] == '(' || char.IsDigit(token[0]))
		{
			ParsedItemData? data = ItemRegistry.GetData(token);
			return new GiftEntryInfo(
				Id: data?.QualifiedItemId ?? token,
				Name: data?.DisplayName ?? token,
				Kind: "item");
		}

		return new GiftEntryInfo(Id: token, Name: token, Kind: "context_tag");
	}


	/*********
	** Gift advice
	*********/

	/// <summary>
	/// Score everything the farmer owns against one villager, and rank what is worth giving them.
	/// </summary>
	/// <param name="npc">The villager's internal or display name.</param>
	/// <param name="limit">The most suggestions and the most warnings to return.</param>
	/// <exception cref="CalendarException">No villager goes by that name, or they aren't in the world.</exception>
	public static GiftAdviceInfo SuggestGifts(string npc, int limit)
	{
		EnsureSaveLoaded();

		string resolved = ResolveVillagerName(npc);
		NPC villager = Game1.getCharacterFromName(resolved)
			?? throw new CalendarException(
				"npc_unavailable",
				$"'{resolved}' has gift tastes but isn't in the world right now, so they can't be given anything.",
				status: 404);

		GiftLimitInfo limits = GetLimits(villager);

		// The same item can sit in the inventory and in several chests, and at different qualities,
		// so group them and remember every place the farmer can find one.
		Dictionary<string, CandidateGroup> groups = new();

		foreach ((Item Item, GiftItemSourceInfo Source) candidate in FindGiftCandidates())
		{
			string key = $"{candidate.Item.QualifiedItemId}|{candidate.Item.Quality}";
			if (!groups.TryGetValue(key, out CandidateGroup? group))
			{
				group = new CandidateGroup(candidate.Item);
				groups[key] = group;
			}

			group.Add(candidate.Source);
		}

		List<GiftOptionInfo> suggestions = new();
		List<GiftOptionInfo> avoid = new();

		foreach (CandidateGroup group in groups.Values)
		{
			int taste = villager.getGiftTasteForThisItem(group.Item);
			GiftOptionInfo option = CreateOption(group, taste, limits.FriendshipMultiplier);

			if (taste is TasteLove or TasteLike or TasteStardropTea)
				suggestions.Add(option);
			else if (taste is TasteDislike or TasteHate)
				avoid.Add(option);
		}

		return new GiftAdviceInfo(
			Npc: resolved,
			DisplayName: NPC.GetDisplayName(resolved),
			Limits: limits,
			Suggestions: suggestions
				.OrderByDescending(option => option.Points)
				.ThenBy(option => option.Name, StringComparer.Ordinal)
				.Take(limit)
				.ToList(),
			Avoid: avoid
				.OrderBy(option => option.Points)
				.ThenBy(option => option.Name, StringComparer.Ordinal)
				.Take(limit)
				.ToList()
		);
	}

	/// <summary>Work out whether the villager will take a gift today, and what the game will multiply it by.</summary>
	private static GiftLimitInfo GetLimits(NPC villager)
	{
		bool isBirthday = villager.isBirthday();
		bool isSpouse = Game1.player.spouse == villager.Name;
		bool isChild = villager is Child;

		Friendship? friendship = Game1.player.friendshipData.TryGetValue(villager.Name, out Friendship? value) ? value : null;
		int giftsToday = friendship?.GiftsToday ?? 0;
		int giftsThisWeek = friendship?.GiftsThisWeek ?? 0;

		// The game's weekly cap doesn't apply to a spouse, a child, or a birthday gift.
		bool weeklyLimitReached = giftsThisWeek >= NPC.maxGiftsPerWeek && !isSpouse && !isChild && !isBirthday;

		// A birthday gift is worth eight times as much, and a gift to the farmer's own spouse is
		// worth half as much. Both can apply at once.
		float multiplier = (isBirthday ? BirthdayMultiplier : 1f) * (isSpouse ? 0.5f : 1f);

		string? blockedReason = null;
		if (!villager.CanReceiveGifts())
			blockedReason = "cannot_receive_gifts";
		else if (friendship?.IsDivorced() ?? false)
			blockedReason = "divorced";
		else if (weeklyLimitReached)
			blockedReason = "weekly_limit";
		else if (giftsToday >= 1)
			blockedReason = "daily_limit";

		return new GiftLimitInfo(
			GiftsToday: giftsToday,
			GiftsThisWeek: giftsThisWeek,
			MaxGiftsPerWeek: NPC.maxGiftsPerWeek,
			CanGiveToday: blockedReason is null,
			WeeklyLimitReached: weeklyLimitReached,
			IsBirthday: isBirthday,
			FriendshipMultiplier: multiplier,
			BlockedReason: blockedReason
		);
	}

	private static GiftOptionInfo CreateOption(CandidateGroup group, int taste, float multiplier)
	{
		return new GiftOptionInfo(
			Name: group.Item.DisplayName,
			QualifiedItemId: group.Item.QualifiedItemId,
			Taste: GetTasteName(taste),
			TasteLevel: taste,
			Quality: group.Item.Quality,
			QualityName: GetQualityName(group.Item.Quality),
			Points: GetPoints(taste, group.Item.Quality, multiplier),
			TotalCount: group.TotalCount,
			Sources: group.GetSources()
		);
	}

	/// <summary>Mirror the arithmetic in <c>NPC.receiveGift</c>, so the number matches what the game will do.</summary>
	private static int GetPoints(int taste, int quality, float multiplier)
	{
		float qualityMultiplier = quality switch
		{
			1 => 1.1f,
			2 => 1.25f,
			4 => 1.5f,
			_ => 1f
		};

		return taste switch
		{
			TasteStardropTea => Math.Min(StardropTeaMaxPoints, (int)(StardropTeaPoints * multiplier)),
			TasteLove => (int)(LovePoints * multiplier * qualityMultiplier),
			TasteLike => (int)(LikePoints * multiplier * qualityMultiplier),
			TasteDislike => (int)(DislikePoints * multiplier),
			TasteHate => (int)(HatePoints * multiplier),
			_ => (int)(NeutralPoints * multiplier)
		};
	}

	private static string GetTasteName(int taste)
	{
		return taste switch
		{
			TasteStardropTea => "stardrop_tea",
			TasteLove => "love",
			TasteLike => "like",
			TasteDislike => "dislike",
			TasteHate => "hate",
			_ => "neutral"
		};
	}

	private static string GetQualityName(int quality)
	{
		return quality switch
		{
			1 => "silver",
			2 => "gold",
			4 => "iridium",
			0 => "normal",
			_ => $"quality-{quality}"
		};
	}


	/*********
	** Where the farmer keeps things
	*********/

	/// <summary>Every giftable item the farmer owns, tagged with where it is.</summary>
	private static IEnumerable<(Item Item, GiftItemSourceInfo Source)> FindGiftCandidates()
	{
		string here = Game1.currentLocation?.DisplayName ?? "";

		foreach (Item? item in Game1.player.Items)
		{
			if (item is StardewValley.Object obj && obj.canBeGivenAsGift())
				yield return (item, new GiftItemSourceInfo("inventory", here, item.Stack));
		}

		foreach ((Chest Chest, string Location) found in FindChests())
		{
			foreach (Item? item in found.Chest.Items)
			{
				if (item is StardewValley.Object obj && obj.canBeGivenAsGift())
					yield return (item, new GiftItemSourceInfo("chest", found.Location, item.Stack));
			}
		}
	}

	/// <summary>Every chest the farmer owns, anywhere in the world.</summary>
	private static IEnumerable<(Chest Chest, string Location)> FindChests()
	{
		HashSet<GameLocation> visited = new();

		foreach (GameLocation location in Game1.locations)
		{
			foreach ((Chest Chest, string Location) found in FindChestsIn(location, visited))
				yield return found;
		}
	}

	private static IEnumerable<(Chest Chest, string Location)> FindChestsIn(GameLocation location, HashSet<GameLocation> visited)
	{
		if (!visited.Add(location))
			yield break;

		string name = location.DisplayName ?? location.Name;

		foreach (var obj in location.Objects.Values)
		{
			if (obj is Chest chest)
				yield return (chest, name);
		}

		// A farm building's interior is its own location that never appears in Game1.locations.
		foreach (Building building in location.buildings)
		{
			GameLocation? indoors = building.GetIndoors();
			if (indoors is null)
				continue;

			foreach ((Chest Chest, string Location) found in FindChestsIn(indoors, visited))
				yield return found;
		}
	}


	/*********
	** Shared helpers
	*********/

	/// <summary>Every key in the gift-taste data that belongs to one villager, sorted by display name.</summary>
	private static IEnumerable<string> GetVillagerNames()
	{
		return Game1.NPCGiftTastes.Keys
			.Where(key => !key.StartsWith(UniversalPrefix, StringComparison.Ordinal))
			.OrderBy(NPC.GetDisplayName, StringComparer.Ordinal);
	}

	/// <summary>
	/// Find the villager whose internal or display name matches, ignoring case, so a model can ask
	/// for "Haley" without knowing the internal keys.
	/// </summary>
	/// <exception cref="CalendarException">Nothing in the gift-taste data goes by that name.</exception>
	private static string ResolveVillagerName(string name)
	{
		foreach (string key in GetVillagerNames())
		{
			if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(NPC.GetDisplayName(key), name, StringComparison.OrdinalIgnoreCase))
				return key;
		}

		throw new CalendarException("unknown_npc", $"No villager called '{name}' has gift tastes.", status: 404);
	}

	private static void EnsureSaveLoaded()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			throw new CalendarException("no_save_loaded", "No save is currently loaded.", status: 503);
	}

	/// <summary>One item at one quality, with every place the farmer is keeping it.</summary>
	private sealed class CandidateGroup
	{
		/// <summary>How many are in each place, keyed by kind and location so several chests in one room add up.</summary>
		private readonly Dictionary<(string Kind, string Location), int> _sources = new();

		public CandidateGroup(Item item)
		{
			this.Item = item;
		}

		public Item Item { get; }

		public int TotalCount { get; private set; }

		public void Add(GiftItemSourceInfo source)
		{
			(string Kind, string Location) key = (source.Kind, source.Location);
			this._sources[key] = this._sources.TryGetValue(key, out int stack) ? stack + source.Stack : source.Stack;
			this.TotalCount += source.Stack;
		}

		public IReadOnlyList<GiftItemSourceInfo> GetSources()
		{
			return this._sources
				.OrderBy(pair => pair.Key.Location, StringComparer.Ordinal)
				.ThenBy(pair => pair.Key.Kind, StringComparer.Ordinal)
				.Select(pair => new GiftItemSourceInfo(pair.Key.Kind, pair.Key.Location, pair.Value))
				.ToList();
		}
	}
}
