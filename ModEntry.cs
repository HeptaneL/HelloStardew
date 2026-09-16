using HelloStardew.Bridge;
using HelloStardew.Agent;
using HelloStardew.Player;
using HelloStardew.Talk;
using HelloStardew.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using HarmonyLib;

namespace HelloStardew;

internal sealed class ModEntry : Mod
{
	private HttpBridge _bridge = null!;
	private AgentClient _agentClient = null!;

	/// <summary>
	/// The thread the CyberJu chat command is on, created on first use. The agent does not recall
	/// CyberJu yet, but the id is required by the request and one per save is what the field means.
	/// </summary>
	private string? _cyberJuThreadId;

	internal static IMonitor? Log { get; private set; }
	internal static IModHelper ModHelper { get; private set; } = null!;
	internal static IManifest Manifest { get; private set; } = null!;
	internal static MainThreadDispatcher Dispatcher { get; private set; } = null!;
	internal static ActivityTracker Activity { get; private set; } = null!;

	/// <summary>
	/// The live config. Settable because Generic Mod Config Menu replaces the whole object when the
	/// player resets to defaults; always read it as <c>ModEntry.Config</c> rather than capturing it.
	/// </summary>
	internal static ModConfig Config { get; set; } = null!;

	public override void Entry(IModHelper helper)
	{
		Log = this.Monitor;
		ModHelper = helper;
		Manifest = this.ModManifest;
		Config = helper.ReadConfig<ModConfig>();

		Dispatcher = new MainThreadDispatcher();
		Activity = new ActivityTracker(this.Monitor);
		this._agentClient = new AgentClient();
		this.ApplyAgentConfig();

		TextInputManager.Initialize(helper);
		SpouseTalkSession.Instance.Initialize(helper, this._agentClient);

		// Picks up every [HarmonyPatch] class in this assembly.
		new Harmony(this.ModManifest.UniqueID).PatchAll();

		// Drain cross-thread requests on the main thread every tick.
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
		helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

		// Keep the activity log alive across loads and days.
		helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
		helper.Events.GameLoop.DayStarted += this.OnDayStarted;
		helper.Events.GameLoop.Saving += this.OnSaving;

		ChatCommands.Register(
			"cj",
			this.OnCyberJuCommand,
			name => Text.CommandCyberJu(name)
		);

		this._bridge = new HttpBridge(this.Monitor, Dispatcher, Config.BindAddress, Config.Port);
		this._bridge.Start();

		this.Monitor.Log($"Calendar API ready. Try: curl {this._bridge.Prefix}health", LogLevel.Info);
		this.Monitor.Log($"Hold [{Config.InitiateTypedDialogueKey}] and click your spouse to type a reply.", LogLevel.Info);
	}

	private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
	{
		// GMCM exposes its API during this event, so register only once the game is up.
		ModConfigMenu.Register(this);
	}

	/// <summary>
	/// Push the current config into the running services. Called after the config menu closes, so
	/// edits take effect without restarting the game.
	/// </summary>
	internal void ApplyRuntimeConfig()
	{
		this.ApplyAgentConfig();

		// HttpListener binds an address at Start, so a new address or port needs a new listener.
		// Only touch the running one if the target actually moved.
		HttpBridge replacement = new(this.Monitor, Dispatcher, Config.BindAddress, Config.Port);
		if (replacement.Prefix == this._bridge.Prefix)
			return;

		if (!replacement.Start())
		{
			this.Monitor.Log(
				$"Could not rebind the Calendar HTTP API, so it is still listening on {this._bridge.Prefix}.",
				LogLevel.Error
			);
			return;
		}

		this._bridge.Dispose();
		this._bridge = replacement;
	}

	private void ApplyAgentConfig()
	{
		// Ignore a blank endpoint so clearing the text box in the config menu can't break requests.
		if (!string.IsNullOrWhiteSpace(Config.AgentEndpoint))
			this._agentClient.Endpoint = Config.AgentEndpoint;

		this._agentClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, Config.AgentTimeoutSeconds));
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		Dispatcher.Pump();
		Activity.OnUpdateTicked();
	}

	private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
	{
		Activity.OnSaveLoaded();

		// A different farm is a different conversation, so the next command opens a new thread.
		this._cyberJuThreadId = null;
	}

	private void OnDayStarted(object? sender, DayStartedEventArgs e)
	{
		Activity.OnDayStarted();
	}

	private void OnSaving(object? sender, SavingEventArgs e)
	{
		Activity.OnSaving();
	}

	private void OnCyberJuCommand(string[] command, ChatBox chat)
	{
		string message = ArgUtility.GetRemainder(command, 1);

		if (string.IsNullOrWhiteSpace(message))
		{
			chat.addInfoMessage($"CyberJu: {Text.CyberJuIntro}");
			return;
		}
		chat.addInfoMessage($"{Game1.player.Name}: {message}");
		chat.addInfoMessage(Text.CyberJuThinking);

		_ = this.HandleCyberJuCommandAsync(message);
	}

	private async Task HandleCyberJuCommandAsync(string message)
	{
		try
		{
			string threadId = this._cyberJuThreadId ??= AgentClient.NewThreadId("CyberJu");

			string response = await this._agentClient.ChatAsync(
				"CyberJu",
				message,
				threadId
			);

			Dispatcher.Enqueue(() =>
			{
				Game1.chatBox?.addInfoMessage(
					$"CyberJu: {response}"
				);
			});
		}
		catch (Exception ex)
		{
			this.Monitor.Log(
				$"CyberJu request failed: {ex}",
				LogLevel.Error
			);

			Dispatcher.Enqueue(() =>
			{
				Game1.chatBox?.addInfoMessage(
					$"CyberJu: {Text.CyberJuUnreachable}"
				);
			});
		}
	}

}
