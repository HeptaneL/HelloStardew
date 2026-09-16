using System.Text;

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

	private const string StaySilentText = "*Stay silent*";
	private const string SomethingElseText = "*Something else*";

	/// <summary>Build the dialogue script for one turn.</summary>
	public static string Build(string npcLine, IReadOnlyList<string> suggestions, bool offerTypedResponse)
	{
		StringBuilder sb = new();
		sb.Append(Sanitize(npcLine));

		AppendOption(sb, KeySilent, StaySilentText);
		foreach (string suggestion in suggestions)
		{
			string text = Sanitize(suggestion);
			if (text.Length > 0)
				AppendOption(sb, KeySuggestion, text);
		}

		if (offerTypedResponse)
			AppendOption(sb, KeyTyped, SomethingElseText);

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
			if (c is '#' or '$' or '^' or '¦' or '\r' or '\n')
				continue;
			sb.Append(c);
		}

		return sb.ToString().Trim();
	}
}
