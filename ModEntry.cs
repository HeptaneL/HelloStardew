using System;
using HelloStardew.Bridge;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace HelloStardew;

internal sealed class ModEntry : Mod
{
	private MainThreadDispatcher _dispatcher = null!;
	private HttpBridge _bridge = null!;

	public override void Entry(IModHelper helper)
	{
		ModConfig config = helper.ReadConfig<ModConfig>();

		this._dispatcher = new MainThreadDispatcher();
		this._bridge = new HttpBridge(this.Monitor, this._dispatcher, config.BindAddress, config.Port);

		// Drain cross-thread requests on the main thread every tick.
		helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

		this._bridge.Start();

		this.Monitor.Log($"Calendar API ready. Try: curl http://{config.BindAddress}:{config.Port}/health", LogLevel.Info);
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		this._dispatcher.Pump();
	}
}
