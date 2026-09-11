using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HelloStardew.Calendar;

/// <summary>
/// UI-free calendar queries. This mirrors the logic in <c>StardewValley.Menus.Billboard</c>
/// (the in-game calendar menu) but returns plain data and draws nothing.
/// </summary>
/// <remarks>
/// All methods read live game state and must run on the game's main thread.
/// Use <see cref="HelloStardew.Bridge.MainThreadDispatcher"/> when calling from an HTTP thread.
/// </remarks>
internal static class CalendarService
{
	/*********
	** Date
	*********/

	/// <summary>Get the current in-game date.</summary>
	public static DateInfo GetCurrentDate()
	{
		EnsureSaveLoaded();
		WorldDate date = Game1.Date;
		return CreateDate(date.Year, date.Season, date.DayOfMonth, date.TotalDays);
	}


	/*********
	** Stardew Valley week helpers
	*********/

	/// <summary>Get the zero-based index of the week within the season (0-3). Day 1 is Monday, day 7 is Sunday.</summary>
	public static int GetWeekIndex(int dayOfMonth)
	{
		return (dayOfMonth - 1) / 7;
	}

	/// <summary>Get the first day of the week containing the given day (1, 8, 15, or 22).</summary>
	public static int GetWeekStart(int dayOfMonth)
	{
		return GetWeekIndex(dayOfMonth) * 7 + 1;
	}

	/// <summary>Get the last day of the week containing the given day (7, 14, 21, or 28).</summary>
	public static int GetWeekEnd(int dayOfMonth)
	{
		return GetWeekStart(dayOfMonth) + 6;
	}

	/// <summary>Get the short English day name for a day of the season, e.g. "Mon".</summary>
	public static string GetDayName(int dayOfMonth)
	{
		return Game1.shortDayNameFromDayOfSeason(dayOfMonth);
	}

	/// <summary>Parse a season key, or return the current season when the value is blank.</summary>
	public static bool TryParseSeason(string? value, out Season season)
	{
		season = Game1.season;
		if (string.IsNullOrWhiteSpace(value))
			return true;

		if (string.Equals(value, "autumn", StringComparison.OrdinalIgnoreCase))
		{
			season = Season.Fall;
			return true;
		}

		return Utility.TryParseEnum(value, out season);
	}


	/*********
	** Birthdays
	*********/

	/// <summary>
	/// Get the birthdays in the current Stardew Valley week.
	/// By default this only returns today and the remaining days of the week, not days that already passed.
	/// </summary>
	/// <param name="includePast">Whether to include days earlier in the same week that already passed.</param>
	public static IReadOnlyList<BirthdayInfo> GetBirthdaysForWeek(bool includePast = false)
	{
		EnsureSaveLoaded();

		int today = Game1.dayOfMonth;
		int from = includePast ? GetWeekStart(today) : today;
		int to = GetWeekEnd(today);

		return CollectBirthdays(Game1.season, from, to, today);
	}

	/// <summary>Get the birthdays on a specific day.</summary>
	public static IReadOnlyList<BirthdayInfo> GetBirthdaysForDay(Season season, int day)
	{
		EnsureSaveLoaded();
		return CollectBirthdays(season, day, day, Game1.dayOfMonth);
	}

	private static IReadOnlyList<BirthdayInfo> CollectBirthdays(Season season, int fromDay, int toDay, int today)
	{
		string seasonKey = Utility.getSeasonKey(season);
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		List<BirthdayInfo> birthdays = new();

		// Mirrors Billboard.GetBirthdays: only villagers, filtered by calendar visibility, de-duplicated.
		Utility.ForEachVillager(npc =>
		{
			if (npc.Birthday_Season != seasonKey)
				return true;
			if (!IsVisibleOnCalendar(npc))
				return true;
			if (!seen.Add(npc.Name))
				return true;

			int day = npc.Birthday_Day;
			if (day < fromDay || day > toDay)
				return true;

			birthdays.Add(new BirthdayInfo(
				NpcName: npc.Name,
				DisplayName: npc.displayName,
				Season: seasonKey,
				Day: day,
				DayOfWeek: GetDayName(day),
				DaysUntil: day - today,
				IsToday: day == today
			));
			return true;
		});

		return birthdays
			.OrderBy(b => b.Day)
			.ThenBy(b => b.DisplayName, StringComparer.Ordinal)
			.ToList();
	}

	/// <summary>Mirror the calendar visibility rules used by the in-game calendar.</summary>
	private static bool IsVisibleOnCalendar(NPC npc)
	{
		CalendarBehavior? behavior = npc.GetData()?.Calendar;
		if (behavior == CalendarBehavior.HiddenAlways)
			return false;
		if (behavior == CalendarBehavior.HiddenUntilMet && !Game1.player.friendshipData.ContainsKey(npc.Name))
			return false;
		return true;
	}


	/*********
	** Events
	*********/

	/// <summary>Get all calendar events for today.</summary>
	public static IReadOnlyList<CalendarEventInfo> GetEventsToday()
	{
		EnsureSaveLoaded();
		return GetEventsForDay(Game1.season, Game1.dayOfMonth);
	}

	/// <summary>Get all calendar events for a given day, mirroring the in-game calendar.</summary>
	public static IReadOnlyList<CalendarEventInfo> GetEventsForDay(Season season, int day)
	{
		EnsureSaveLoaded();

		List<CalendarEventInfo> events = new();
		string seasonKey = Utility.getSeasonKey(season);

		// Active festival (e.g. Egg Festival).
		if (Utility.isFestivalDay(day, season))
		{
			string id = seasonKey + day;
			events.Add(new CalendarEventInfo("festival", id, GetFestivalName(id), Locked: false));
		}

		// Passive festival (e.g. Night Market).
		if (Utility.TryGetPassiveFestivalDataForDay(day, season, null, out string passiveId, out PassiveFestivalData passiveData, ignoreConditionsCheck: true)
			&& passiveData?.ShowOnCalendar == true)
		{
			bool locked = !GameStateQuery.CheckConditions(passiveData.Condition);
			string name = locked ? "???" : TokenParser.ParseText(passiveData.DisplayName) ?? passiveId;
			events.Add(new CalendarEventInfo("passive_festival", passiveId, name, locked));
		}

		// Fishing derbies are hardcoded in the game (see Billboard.GetEventsForDay).
		if (season == Season.Summer && (day == 20 || day == 21))
			events.Add(new CalendarEventInfo("fishing_derby", "TroutDerby", LoadString("Strings\\1_6_Strings:TroutDerby"), Locked: false));
		else if (season == Season.Winter && (day == 12 || day == 13))
			events.Add(new CalendarEventInfo("fishing_derby", "SquidFest", LoadString("Strings\\1_6_Strings:SquidFest"), Locked: false));

		// Bookseller days are seeded from the save and only defined for the current season.
		if (season == Game1.season && Utility.getDaysOfBooksellerThisSeason().Contains(day))
			events.Add(new CalendarEventInfo("bookseller", "Bookseller", LoadString("Strings\\1_6_Strings:Bookseller"), Locked: false));

		// Birthdays.
		foreach (BirthdayInfo birthday in CollectBirthdays(season, day, day, Game1.dayOfMonth))
			events.Add(new CalendarEventInfo("birthday", birthday.NpcName, birthday.DisplayName, Locked: false));

		return events;
	}


	/*********
	** Season calendar
	*********/

	/// <summary>Get the full 28-day calendar for a season, like the in-game calendar menu.</summary>
	/// <remarks>Unlike <see cref="GetBirthdaysForWeek"/>, this does not skip days that already passed.</remarks>
	public static IReadOnlyList<DayCalendarInfo> GetSeasonCalendar(Season season)
	{
		EnsureSaveLoaded();

		int today = Game1.dayOfMonth;
		bool isCurrentSeason = season == Game1.season;
		List<DayCalendarInfo> days = new(28);

		for (int day = 1; day <= 28; day++)
		{
			days.Add(new DayCalendarInfo(
				Day: day,
				DayOfWeek: GetDayName(day),
				IsToday: isCurrentSeason && day == today,
				IsPast: isCurrentSeason && day < today,
				Events: GetEventsForDay(season, day)
			));
		}

		return days;
	}


	/*********
	** Shared helpers
	*********/

	private static DateInfo CreateDate(int year, Season season, int day, int totalDays)
	{
		return new DateInfo(
			Year: year,
			Season: Utility.getSeasonKey(season),
			SeasonName: Utility.getSeasonNameFromNumber((int)season),
			DayOfMonth: day,
			DayOfWeek: GetDayName(day),
			WeekIndex: GetWeekIndex(day),
			WeekStart: GetWeekStart(day),
			WeekEnd: GetWeekEnd(day),
			TotalDays: totalDays
		);
	}

	private static string GetFestivalName(string festivalId)
	{
		try
		{
			Dictionary<string, string> data = Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + festivalId);
			return data.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name)
				? name
				: festivalId;
		}
		catch
		{
			return festivalId;
		}
	}

	private static string LoadString(string key)
	{
		try
		{
			return Game1.content.LoadString(key);
		}
		catch
		{
			return key;
		}
	}

	private static void EnsureSaveLoaded()
	{
		if (!Game1.hasLoadedGame || Game1.player is null)
			throw new CalendarException("no_save_loaded", "No save is currently loaded.", status: 503);
	}
}
