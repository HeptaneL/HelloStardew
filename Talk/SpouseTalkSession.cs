using HelloStardew.Agent;
using HelloStardew.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HelloStardew.Talk;

/// <summary>
/// Drives one exchange with the spouse: the player speaks, the agent answers, and the answer
/// comes back as a vanilla dialogue page with selectable replies.
/// </summary>
/// <remarks>
/// Every turn follows the same shape, so there is no separate "opening" turn — the player always
/// supplies the line that starts it, either by typing it or by picking a suggestion.
/// </remarks>
internal sealed class SpouseTalkSession
{
	private static SpouseTalkSession? _instance;

	public static SpouseTalkSession Instance => _instance ??= new SpouseTalkSession();

	private AgentClient? _client;
	private NPC? _spouse;
	private string? _pendingMessage;
	private ThinkingWindow? _thinking;

	private SpouseTalkSession()
	{
	}

	public void Initialize(IModHelper helper, AgentClient client)
	{
		this._client = client;
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
	}

	/// <summary>Get whether the given NPC is the player's spouse.</summary>
	public static bool IsSpouse(NPC? npc)
	{
		if (npc is null || !Context.IsWorldReady)
			return false;

		NPC? spouse = Game1.player.getSpouse();
		return spouse is not null && npc.Name == spouse.Name;
	}

	/// <summary>Open the reply box so the player can start (or continue) the conversation.</summary>
	public void RequestTypedInput(NPC npc)
	{
		this._spouse = npc;
		TextInputManager.Request(
			$"What do you want to say to {npc.displayName}?",
			text => this.OnPlayerSpoke(npc, text)
		);
	}

	/// <summary>Take a line the player typed or picked, and ask the agent for the reply.</summary>
	public void OnPlayerSpoke(NPC npc, string text)
	{
		this._spouse = npc;
		if (string.IsNullOrWhiteSpace(text))
			return;

		this._pendingMessage = text.Trim();
	}

	/// <summary>The player chose to say nothing, so the exchange is over.</summary>
	public void End()
	{
		this._pendingMessage = null;
		this._spouse = null;
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		// Wait until whatever owned the menu (dialogue box, save menu, ...) has let go of it.
		if (this._pendingMessage is null || Game1.activeClickableMenu is not null)
			return;

		NPC? npc = this._spouse;
		string message = this._pendingMessage;
		this._pendingMessage = null;

		if (npc is null)
			return;

		this._thinking = new ThinkingWindow($"{npc.displayName} is thinking...");
		Game1.activeClickableMenu = this._thinking;

		_ = this.GenerateAsync(npc, message);
	}

	private async Task GenerateAsync(NPC npc, string message)
	{
		SpouseReply reply;
		try
		{
			string raw = await this._client!.ChatAsync(npc.Name, message);
			reply = SpouseTalkScript.Parse(raw);
		}
		catch (Exception ex)
		{
			ModEntry.Log?.Log($"Spouse conversation failed: {ex.Message}", LogLevel.Error);
			reply = new SpouseReply("Sorry, I lost my train of thought.", Array.Empty<string>());
		}

		ModEntry.Dispatcher.Enqueue(() => this.ShowReply(npc, reply));
	}

	private void ShowReply(NPC npc, SpouseReply reply)
	{
		if (this._thinking is not null && ReferenceEquals(Game1.activeClickableMenu, this._thinking))
			Game1.activeClickableMenu.exitThisMenu(playSound: false);

		this._thinking = null;

		string script = SpouseTalkScript.Build(
			reply.NpcLine,
			reply.Suggestions,
			ModEntry.Config.OfferTypedResponse
		);

		// DrawDialogue pushes onto the NPC's stack and shows the top; pop it straight back off
		// so a conversation does not accumulate on the stack turn after turn.
		Dialogue dialogue = new(npc, null, script);
		Game1.DrawDialogue(dialogue);
		npc.CurrentDialogue.TryPop(out _);

		npc.faceTowardFarmerForPeriod(4000, 3, false, Game1.player);
	}
}
