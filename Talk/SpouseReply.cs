namespace HelloStardew.Talk;

/// <summary>One turn of agent output: the line the spouse says, plus optional suggested farmer replies.</summary>
/// <param name="NpcLine">The sanitized line the spouse will speak.</param>
/// <param name="Suggestions">Zero or more farmer replies the player can pick from.</param>
internal sealed record SpouseReply(string NpcLine, IReadOnlyList<string> Suggestions);
