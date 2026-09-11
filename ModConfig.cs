namespace HelloStardew;

/// <summary>The mod's user-editable settings, saved to <c>config.json</c>.</summary>
internal sealed class ModConfig
{
	/// <summary>The address the HTTP API binds to. Keep it on localhost unless you know what you're doing.</summary>
	public string BindAddress { get; set; } = "127.0.0.1";

	/// <summary>The port the HTTP API listens on.</summary>
	public int Port { get; set; } = 8788;
}
