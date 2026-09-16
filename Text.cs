using StardewModdingAPI;

namespace HelloStardew;

/// <summary>
/// Every line the mod itself shows the player.
/// </summary>
/// <remarks>
/// SMAPI reads these from <c>i18n/&lt;game language&gt;.json</c> and falls back to
/// <c>i18n/default.json</c>, so the game's own language option decides what the player reads, and
/// supporting a language means adding a file rather than adding a branch here.
/// <para>
/// Text the game already owns — NPC names, item names, key names — is deliberately absent: it
/// goes into a message as a token so that the game translates it, not us.
/// </para>
/// <para>
/// SMAPI console output is deliberately not here either. It is read by whoever is debugging the
/// mod, and a log is easier to search when it is in one language.
/// </para>
/// </remarks>
internal static class Text
{
	private static string Get(string key, object? tokens = null)
	{
		return ModEntry.ModHelper.Translation.Get(key, tokens);
	}

	// --- Spouse conversation ------------------------------------------------

	/// <summary>The title of the reply box. Tokens: <c>npc</c>.</summary>
	public static string SpousePromptTitle(string npc) => Get("spouse.prompt.title", new { npc });

	/// <summary>What the waiting window says. Tokens: <c>npc</c>.</summary>
	public static string SpouseThinking(string npc) => Get("spouse.prompt.thinking", new { npc });

	/// <summary>Shown in place of a reply when the agent could not be reached.</summary>
	public static string SpouseReplyFailed => Get("spouse.reply.failed");

	// --- Dialogue options ---------------------------------------------------

	/// <summary>The option that closes the conversation.</summary>
	public static string StaySilent => Get("dialogue.stay-silent");

	/// <summary>The option that reopens the reply box.</summary>
	public static string SomethingElse => Get("dialogue.something-else");

	// --- Input box ----------------------------------------------------------

	/// <summary>The line under the reply box explaining the keys.</summary>
	public static string InputHint => Get("input.hint");

	// --- CyberJu ------------------------------------------------------------

	/// <summary>What CyberJu says when the player runs the command with no message.</summary>
	public static string CyberJuIntro => Get("cyberju.intro");

	/// <summary>The chat line shown while CyberJu is answering.</summary>
	public static string CyberJuThinking => Get("cyberju.thinking");

	/// <summary>Shown in place of a reply when the agent could not be reached.</summary>
	public static string CyberJuUnreachable => Get("cyberju.unreachable");

	/// <summary>The CyberJu command as it appears in SMAPI's help. Tokens: <c>name</c>.</summary>
	public static string CommandCyberJu(string name) => Get("command.cj.description", new { name });

	// --- Config menu --------------------------------------------------------

	public static string ConfigConversationSection => Get("config.section.conversation");
	public static string ConfigEnableSpouseName => Get("config.enable-spouse.name");
	public static string ConfigEnableSpouseTooltip => Get("config.enable-spouse.tooltip");
	public static string ConfigTypeToSpouseName => Get("config.type-to-spouse.name");
	public static string ConfigTypeToSpouseTooltip => Get("config.type-to-spouse.tooltip");
	public static string ConfigOfferTypedName => Get("config.offer-typed.name");
	public static string ConfigOfferTypedTooltip => Get("config.offer-typed.tooltip");

	public static string ConfigAgentSection => Get("config.section.agent");
	public static string ConfigAgentEndpointName => Get("config.agent-endpoint.name");
	public static string ConfigAgentEndpointTooltip => Get("config.agent-endpoint.tooltip");
	public static string ConfigAgentTimeoutName => Get("config.agent-timeout.name");
	public static string ConfigAgentTimeoutTooltip => Get("config.agent-timeout.tooltip");

	public static string ConfigApiSection => Get("config.section.api");
	public static string ConfigApiNote => Get("config.api-note");
	public static string ConfigBindAddressName => Get("config.bind-address.name");
	public static string ConfigBindAddressTooltip => Get("config.bind-address.tooltip");
	public static string ConfigPortName => Get("config.port.name");
	public static string ConfigPortTooltip => Get("config.port.tooltip");
}
