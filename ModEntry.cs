using HelloStardew.Bridge;
using HelloStardew.Agent;
using HelloStardew.Spouse;
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
	internal static IMonitor? Log { get; private set; }
	internal static MainThreadDispatcher Dispatcher { get; private set; } = null!;

	public override void Entry(IModHelper helper)
	{
		Log = this.Monitor;
		ModConfig config = helper.ReadConfig<ModConfig>();
		Dispatcher = new MainThreadDispatcher();
		this._bridge = new HttpBridge(this.Monitor, Dispatcher, config.BindAddress, config.Port);
		this._agentClient = new AgentClient();
		var harmony = new Harmony(this.ModManifest.UniqueID);

		harmony.Patch(
			original: AccessTools.Constructor(
				typeof(DialogueBox),
				new[] { typeof(Dialogue)}
			),
			prefix: new HarmonyMethod(
				typeof(SpouseDialoguePatch),
				nameof(SpouseDialoguePatch.Prefix)
			)
		);

		// Drain cross-thread requests on the main thread every tick.
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
		//helper.Events.Display.MenuChanged += this.OnMenuChanged;

		ChatCommands.Register(
			"cj",
			this.OnCyberJuCommand,
			name => $"{name} [message]: talk to CyberJu."
		);

		this._bridge.Start();

		this.Monitor.Log($"Calendar API ready. Try: curl http://{config.BindAddress}:{config.Port}/health", LogLevel.Info);
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		Dispatcher.Pump();
	}

	private void OnCyberJuCommand(string[] command, ChatBox chat)
	{
		string message = ArgUtility.GetRemainder(command, 1);

		if (string.IsNullOrWhiteSpace(message))
		{
			chat.addInfoMessage(
				"CyberJu: I am CyberJu, the AI assistant of a Stardew Valley farm. " + 
				"My job is to help the farmer understand the current game state " +
			   	"and decide what matters most. What would you like to talk about?"
			);
			return;
		}
		chat.addInfoMessage($"{Game1.player.Name}: {message}");
		chat.addInfoMessage("CyberJu is thinking...");
		
		_ = this.HandleCyberJuCommandAsync(message);
	}

	private async Task HandleCyberJuCommandAsync(string message)
	{
		try
		{
			string response = await this._agentClient.ChatAsync(
				"CyberJu",
				message
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
					"CyberJu: Sorry, I couldn't reach the Agent."
				);
			});
		}
	}

}
