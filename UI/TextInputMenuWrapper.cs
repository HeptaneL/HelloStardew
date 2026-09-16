using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley.Menus;

namespace HelloStardew.UI;

/// <summary>Adapts <see cref="TextInputMenu"/> to the game's menu system.</summary>
internal sealed class TextInputMenuWrapper : IClickableMenu
{
	private readonly TextInputMenu _innerMenu;

	public TextInputMenuWrapper(TextInputMenu innerMenu)
	{
		this._innerMenu = innerMenu;
	}

	public override void draw(SpriteBatch b) => this._innerMenu.Draw(b);

	public override void receiveLeftClick(int x, int y, bool playSound = true) => this._innerMenu.ReceiveLeftClick(x, y);

	public override void receiveKeyPress(Keys key) => this._innerMenu.ReceiveKeyPress(key);

	/// <summary>Stop the cursor being dragged to the menu's centre.</summary>
	public override bool overrideSnappyMenuCursorMovementBan() => true;

	protected override void cleanupBeforeExit()
	{
		this._innerMenu.Close();
		base.cleanupBeforeExit();
	}
}
