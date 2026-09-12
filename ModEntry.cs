using System;
using HelloStardew.Bridge;
using HelloStardew.CyberJu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Network;
using StardewValley.Menus;

namespace HelloStardew;

internal sealed class ModEntry : Mod
{
	private MainThreadDispatcher _dispatcher = null!;
	private HttpBridge _bridge = null!;
	private CyberJuClient _cyberJuClient = null!;

	public override void Entry(IModHelper helper)
	{
		ModConfig config = helper.ReadConfig<ModConfig>();

		this._dispatcher = new MainThreadDispatcher();
		this._bridge = new HttpBridge(this.Monitor, this._dispatcher, config.BindAddress, config.Port);
		this._cyberJuClient = new CyberJuClient();

		// Drain cross-thread requests on the main thread every tick.
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

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
		this._dispatcher.Pump();
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
			string response = await this._cyberJuClient.ChatAsync(
				"CyberJu",
				message
			);

			this._dispatcher.Enqueue(() =>
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

			this._dispatcher.Enqueue(() =>
			{
				Game1.chatBox?.addInfoMessage(
					"CyberJu: Sorry, I couldn't reach the Agent."
				);
			});
		}
	}

}
