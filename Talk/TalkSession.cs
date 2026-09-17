using HelloStardew.Agent;
using HelloStardew.Player;
using HelloStardew.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HelloStardew.Talk;

/// <summary>
/// Drives one exchange with a villager: the player speaks, the agent answers, and the answer
/// comes back as a vanilla dialogue page with selectable replies.
/// </summary>
/// <remarks>
/// Every turn follows the same shape, so there is no separate "opening" turn — the player always
/// supplies the line that starts it, either by typing it or by picking a suggestion.
/// <para>
/// The spouse and every other villager go through here alike. Which character the agent answers as
/// is its own decision, keyed on the name sent with each turn.
/// </para>
/// </remarks>
internal sealed class TalkSession
{
	private static TalkSession? _instance;

	public static TalkSession Instance => _instance ??= new TalkSession();

	private AgentClient? _client;
	private NPC? _npc;
	private string? _pendingMessage;
	private ThinkingWindow? _thinking;

	/// <summary>
	/// Identifies the conversation currently under way, or null when none is. Every turn of one
	/// chat sends the same value so the agent can recall it; the next chat mints a new one.
	/// </summary>
	private string? _threadId;

	private TalkSession()
	{
	}

	public void Initialize(IModHelper helper, AgentClient client)
	{
		this._client = client;
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
	}

	/// <summary>Open the reply box so the player can start (or continue) the conversation.</summary>
	/// <param name="startNewConversation">
	/// True when the player is walking up to the villager to strike up a chat, which must open a new
	/// thread. False for a later turn of the chat already under way — including "Something else",
	/// which is a continuation even though it reopens the box.
	/// </param>
	public void RequestTypedInput(NPC npc, bool startNewConversation = false)
	{
		this._npc = npc;

		if (startNewConversation || this._threadId is null)
			this._threadId = AgentClient.NewThreadId(npc.Name);

		TextInputManager.Request(
			Text.TalkPromptTitle(npc.displayName),
			text => this.OnPlayerSpoke(npc, text)
		);
	}

	/// <summary>Take a line the player typed or picked, and ask the agent for the reply.</summary>
	public void OnPlayerSpoke(NPC npc, string text)
	{
		this._npc = npc;
		if (string.IsNullOrWhiteSpace(text))
			return;

		this._pendingMessage = text.Trim();
	}

	/// <summary>The player chose to say nothing, so the exchange is over.</summary>
	public void End()
	{
		this._pendingMessage = null;
		this._npc = null;

		// Dropping the id is what ends the conversation: the next one asks for a new thread, so the
		// agent starts it with no memory of this chat.
		this._threadId = null;
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		// Wait until whatever owned the menu (dialogue box, save menu, ...) has let go of it.
		if (this._pendingMessage is null || Game1.activeClickableMenu is not null)
			return;

		NPC? npc = this._npc;
		string message = this._pendingMessage;

		// Copy the id now: the player can end the chat while the request is in flight, and this turn
		// still belongs to the thread it was asked in.
		string? threadId = this._threadId;
		this._pendingMessage = null;

		if (npc is null || threadId is null)
			return;

		this._thinking = new ThinkingWindow(Text.TalkThinking(npc.displayName));
		Game1.activeClickableMenu = this._thinking;

		_ = this.GenerateAsync(npc, message, threadId);
	}

	private async Task GenerateAsync(NPC npc, string message, string threadId)
	{
		TalkReply reply;
		try
		{
			string raw = await this._client!.ChatAsync(
				npc.Name,
				message,
				threadId,
				isSpouse: PlayerService.IsSpouse(npc)
			);
			reply = TalkScript.Parse(raw);
		}
		catch (Exception ex)
		{
			ModEntry.Log?.Log($"Conversation with {npc.Name} failed: {ex.Message}", LogLevel.Error);
			reply = new TalkReply(Text.TalkReplyFailed, Array.Empty<string>());
		}

		ModEntry.Dispatcher.Enqueue(() => this.ShowReply(npc, reply));
	}

	private void ShowReply(NPC npc, TalkReply reply)
	{
		if (this._thinking is not null && ReferenceEquals(Game1.activeClickableMenu, this._thinking))
			Game1.activeClickableMenu.exitThisMenu(playSound: false);

		this._thinking = null;

		// The line comes first and the options follow it, so the box only offers them once the
		// villager has finished speaking; see BuildNpcLine for why the line is split up here.
		string script = TalkScript.BuildNpcLine(reply.NpcLine)
			+ TalkScript.BuildChoices(reply.Suggestions, ModEntry.Config.OfferTypedResponse);

		// DrawDialogue pushes onto the NPC's stack and shows the top; pop it straight back off
		// so a conversation does not accumulate on the stack turn after turn.
		Dialogue dialogue = new(npc, null, script);

		Game1.DrawDialogue(dialogue);
		npc.CurrentDialogue.TryPop(out _);

		npc.faceTowardFarmerForPeriod(4000, 3, false, Game1.player);
	}
}
