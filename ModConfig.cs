using StardewModdingAPI;

namespace HelloStardew;

/// <summary>The mod's user-editable settings, saved to <c>config.json</c>.</summary>
internal sealed class ModConfig
{
	/// <summary>The address the HTTP API binds to. Keep it on localhost unless you know what you're doing.</summary>
	public string BindAddress { get; set; } = "127.0.0.1";

	/// <summary>The port the HTTP API listens on.</summary>
	public int Port { get; set; } = 8788;

	/// <summary>Whether talking to your spouse through the AI agent is enabled.</summary>
	public bool EnableSpouseConversation { get; set; } = true;

	/// <summary>Hold this key while clicking your spouse to type a message instead of using the vanilla dialogue.</summary>
	public SButton InitiateTypedDialogueKey { get; set; } = SButton.LeftAlt;

	/// <summary>The agent endpoint that turns the farmer's message into a reply.</summary>
	public string AgentEndpoint { get; set; } = "http://127.0.0.1:8000/chat";

	/// <summary>How long to wait for the agent before giving up, in seconds.</summary>
	public int AgentTimeoutSeconds { get; set; } = 30;

	/// <summary>Whether the "*Something else*" option (type your own reply) is offered.</summary>
	public bool OfferTypedResponse { get; set; } = true;
}
