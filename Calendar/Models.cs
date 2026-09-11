using System.Collections.Generic;

namespace HelloStardew.Calendar;

/// <summary>The current in-game date and its Stardew Valley week alignment.</summary>
internal sealed record DateInfo(
	int Year,
	string Season,
	string SeasonName,
	int DayOfMonth,
	string DayOfWeek,
	int WeekIndex,
	int WeekStart,
	int WeekEnd,
	int TotalDays);

/// <summary>An NPC birthday.</summary>
/// <param name="DaysUntil">Days from today until the birthday. Negative if it already passed this month.</param>
internal sealed record BirthdayInfo(
	string NpcName,
	string DisplayName,
	string Season,
	int Day,
	string DayOfWeek,
	int DaysUntil,
	bool IsToday);

/// <summary>An event shown on the in-game calendar.</summary>
/// <param name="Type">One of <c>festival</c>, <c>passive_festival</c>, <c>fishing_derby</c>, <c>bookseller</c>, or <c>birthday</c>.</param>
/// <param name="Locked">Whether the event exists but is hidden behind unmet conditions (shown as "???" in-game).</param>
internal sealed record CalendarEventInfo(
	string Type,
	string Id,
	string DisplayName,
	bool Locked);

/// <summary>One day of the season calendar.</summary>
internal sealed record DayCalendarInfo(
	int Day,
	string DayOfWeek,
	bool IsToday,
	bool IsPast,
	IReadOnlyList<CalendarEventInfo> Events);
