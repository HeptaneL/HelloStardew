using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HelloStardew.UI;

/// <summary>
/// Shows the reply box at a safe moment and hands the typed text back to the caller.
/// </summary>
/// <remarks>
/// The box cannot be opened the instant it is requested: the request usually arrives from inside
/// a dialogue box, and replacing <c>Game1.activeClickableMenu</c> right then would leave the old
/// menu's cleanup half-finished. So the request is parked and the box appears on the first tick
/// where nothing else owns the menu.
/// </remarks>
internal static class TextInputManager
{
	private static bool _awaiting;
	private static string _title = "";
	private static Action<string>? _onSubmit;

	public static void Initialize(IModHelper helper)
	{
		helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
	}

	/// <summary>Queue the reply box. <paramref name="onSubmit"/> gets the text, or "" if cancelled.</summary>
	public static void Request(string title, Action<string> onSubmit)
	{
		_title = title;
		_onSubmit = onSubmit;
		_awaiting = true;
	}

	private static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		if (!_awaiting || Game1.activeClickableMenu is not null)
			return;

		_awaiting = false;
		Game1.activeClickableMenu = new TextInputMenuWrapper(new TextInputMenu(_title, Submit));
	}

	private static void Submit(string text)
	{
		Action<string>? callback = _onSubmit;
		_onSubmit = null;
		_awaiting = false;

		// exitThisMenu runs cleanupBeforeExit, which is what releases the keyboard subscriber.
		// Game1.exitActiveMenu only nulls the reference and would leak it.
		if (Game1.activeClickableMenu is TextInputMenuWrapper menu)
			menu.exitThisMenu(playSound: false);
		else
			Game1.exitActiveMenu();

		callback?.Invoke(text);
	}
}
