using System.Text;
using StardewValley.BellsAndWhistles;

namespace HelloStardew.Talk;

/// <summary>
/// Translates between the agent's raw text and the vanilla dialogue script that renders a
/// spouse line together with its selectable responses.
/// </summary>
/// <remarks>
/// The script format is the vanilla one understood by <c>Dialogue.parseDialogueString</c>:
/// <code>
/// &lt;the line the spouse says&gt;#$r &lt;responseID&gt; &lt;friendshipChange&gt; &lt;responseKey&gt;#&lt;response text&gt;
/// </code>
/// Each <c>$r</c> segment adds one selectable option and sets <c>isLastDialogueInteractive</c>,
/// which is what makes <c>DialogueBox</c> treat the page as a question. Vanilla only matches
/// options by <c>responseKey</c>, so every generated suggestion deliberately shares one key and
/// is told apart by its text.
/// </remarks>
internal static class SpouseTalkScript
{
	/// <summary>Marks every response key we own, so we can recognise our own option menus.</summary>
	public const string KeyPrefix = "HSD_";

	/// <summary>Whether the page offering our options is ours.</summary>
	public const string KeySilent = KeyPrefix + "Silent";

	/// <summary>An agent-suggested farmer reply. Several options share this key.</summary>
	public const string KeySuggestion = KeyPrefix + "Suggestion";

	/// <summary>The "type your own reply" option.</summary>
	public const string KeyTyped = KeyPrefix + "Typed";

	/// <summary>
	/// Vanilla stores this id in <c>Farmer.DialogueQuestionsAnswered</c> when a response is
	/// resolved natively. A unique value keeps our options from ever colliding with real ones.
	/// </summary>
	private const string ResponseId = KeyPrefix + "R";

	/// <summary>Marks a page that carries on into the next one.</summary>
	/// <remarks>
	/// Vanilla strips this marker before drawing and remembers it as
	/// <c>Dialogue.isCurrentStringContinuedOnNextScreen</c>. Without it <c>DialogueBox</c> closes
	/// the box after the page instead of showing the next one.
	/// </remarks>
	private const char PageBreak = '{';

	/// <summary>The text area inside the box <c>DialogueBox</c> builds for an NPC dialogue.</summary>
	/// <remarks>
	/// That box is a fixed 1200x384, and its text stops 460px short of the right edge to leave room
	/// for the portrait. Mirrored from <c>DialogueBox.checkDialogue</c>, which is what otherwise
	/// decides where the line breaks.
	/// </remarks>
	private const int PageTextWidth = 1200 - 460 - 20;
	private const int PageTextHeight = 384 - 16;

	/// <summary>Build the spouse's line, split into the pages the dialogue box will show.</summary>
	/// <remarks>
	/// The line is broken up here instead of being left to <c>DialogueBox</c>. Left alone, a long
	/// line is a single entry of <c>Dialogue.dialogues</c> that the box silently splits across
	/// several screens, and the options ride along with the first screen: <c>Dialogue</c> treats an
	/// entry as the interactive one whenever it is the last entry, whether or not the box still has
	/// more of it to show. Emitting one entry per page puts the options on the entry that really is
	/// the last one.
	/// </remarks>
	public static string BuildNpcLine(string npcLine)
	{
		List<string> pages = SplitIntoPages(Sanitize(npcLine));
		if (pages.Count == 0)
			pages.Add("...");

		StringBuilder sb = new();
		for (int i = 0; i < pages.Count; i++)
		{
			if (i > 0)
				sb.Append('#');
			sb.Append(pages[i]);
			if (i < pages.Count - 1)
				sb.Append(PageBreak);
		}

		return sb.ToString();
	}

	/// <summary>Break <paramref name="text"/> into chunks that each fit on one screen.</summary>
	private static List<string> SplitIntoPages(string text)
	{
		List<string> pages = new();
		string remaining = text.Trim();

		while (remaining.Length > 0)
		{
			string overflow = SpriteText.getSubstringBeyondHeight(remaining, PageTextWidth, PageTextHeight);

			// The overflow starts at a space, so everything before that space is the page. When
			// nothing overflows there is no split to make; when the split would land at or past the
			// end there is no boundary to break on (a single word wider than the box), and keeping
			// the text whole is better than looping on it.
			int splitAt = remaining.Length - overflow.Length + 1;
			if (overflow.Length == 0 || splitAt >= remaining.Length)
			{
				pages.Add(remaining);
				break;
			}

			string page = remaining[..splitAt].Trim();
			if (page.Length == 0)
			{
				pages.Add(remaining);
				break;
			}

			pages.Add(page);
			remaining = remaining[splitAt..].Trim();
		}

		return pages;
	}

	/// <summary>
	/// Build the selectable replies, as a segment to append to <see cref="BuildNpcLine"/>.
	/// </summary>
	/// <remarks>
	/// The leading <c>#</c> closes the last page of the line, so this must follow it and must not
	/// be handed to a dialogue box on its own: with no page of its own to sit on, the options would
	/// have nothing to be shown against.
	/// </remarks>
	public static string BuildChoices(
		IReadOnlyList<string> suggestions,
		bool offerTypedResponse)
	{
		StringBuilder sb = new();
		AppendOption(sb, KeySilent, Text.StaySilent);

		foreach (string suggestion in suggestions)
		{
			string text = Sanitize(suggestion);
			if (text.Length > 0)
				AppendOption(sb, KeySuggestion, text);
		}

		if (offerTypedResponse)
			AppendOption(sb, KeyTyped, Text.SomethingElse);

		return sb.ToString();
	}

	/// <summary>Append one option. <paramref name="text"/> must already be sanitized.</summary>
	private static void AppendOption(StringBuilder sb, string key, string text)
	{
		sb.Append("#$r ").Append(ResponseId)
			.Append(" 0 ").Append(key)
			.Append('#').Append(text);
	}

	/// <summary>
	/// Split the agent's reply into a spoken line and suggested farmer replies.
	/// Convention mirrors the ValleyTalk format the Python agent may adopt: a leading '-'
	/// marks the spoken line, a leading '%' marks a suggested reply. A plain single-line
	/// reply still works, it just yields no suggestions.
	/// </summary>
	public static SpouseReply Parse(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return new SpouseReply("...", Array.Empty<string>());

		string? npcLine = null;
		List<string> suggestions = new();

		foreach (string rawLine in raw.Split('\n'))
		{
			string line = rawLine.Trim();
			if (line.Length == 0)
				continue;

			if (line.StartsWith('%'))
			{
				string suggestion = Sanitize(line[1..]);
				if (suggestion.Length > 0)
					suggestions.Add(suggestion);
			}
			else if (npcLine is null)
			{
				npcLine = Sanitize(line.TrimStart('-'));
			}
		}

		if (string.IsNullOrWhiteSpace(npcLine))
		{
			// Nothing the spouse can say. If the agent only sent suggestions, the suggestions
			// still make sense as options under a placeholder line.
			npcLine = suggestions.Count > 0 ? "..." : Sanitize(raw);
		}

		if (npcLine.Length == 0)
			npcLine = "...";

		return new SpouseReply(npcLine, suggestions);
	}

	/// <summary>
	/// Strip characters that the vanilla dialogue parser would treat as markup:
	/// '#' starts a new script segment; '$' starts an inline command such as an emotion token;
	/// '^' and '¦' terminate a line, so anything after one would silently disappear
	/// (see <c>Dialogue.applyGenderSwitch</c>, which <c>checkForSpecialCharacters</c> calls);
	/// '{' and '}' are the page-continuation and mail-flag markers the parser consumes without
	/// drawing, so text containing them would lose a page break or be cut short at the brace
	/// (<see cref="BuildNpcLine"/> adds its own page breaks after this runs);
	/// and line breaks would split the text into unexpected pages.
	/// </summary>
	/// <remarks>
	/// '@' is deliberately kept: vanilla expands it to the farmer's name.
	/// </remarks>
	public static string Sanitize(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return string.Empty;

		StringBuilder sb = new(text.Length);
		foreach (char c in text)
		{
			if (c is '#' or '$' or '^' or '¦' or '{' or '}' or '\r' or '\n')
				continue;
			sb.Append(c);
		}

		return sb.ToString().Trim();
	}
}
