using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace HelloStardew.UI;

/// <summary>
/// The "write your reply" dialog: a title, a text box, and OK / Cancel buttons.
/// Not an <see cref="IClickableMenu"/> itself — <see cref="TextInputMenuWrapper"/> adapts it.
/// </summary>
internal sealed class TextInputMenu
{
	public delegate void SubmittedHandler(string text);

	private const int MenuWidth = 1000;
	private const int MenuHeight = 420;
	private const int BoxHeight = 160;
	private const int ButtonSize = 64;
	private const int Margin = 24;

	private readonly string _title;
	private readonly SubmittedHandler _onSubmit;
	private readonly TextInputBox _box;
	private readonly ClickableTextureComponent _okButton;
	private readonly ClickableTextureComponent _cancelButton;
	private readonly Vector2 _position;
	private readonly Rectangle _bounds;

	public TextInputMenu(string title, SubmittedHandler onSubmit)
	{
		this._title = title;
		this._onSubmit = onSubmit;

		this._position = new Vector2(
			(Game1.uiViewport.Width - MenuWidth) / 2,
			(Game1.uiViewport.Height - MenuHeight) / 2
		);
		this._bounds = new Rectangle((int)this._position.X, (int)this._position.Y, MenuWidth, MenuHeight);

		float titleHeight = Game1.dialogueFont.MeasureString(this._title).Y;

		this._box = new TextInputBox(500)
		{
			Position = new Vector2(this._position.X + 2 * Margin, this._position.Y + 4 * Margin + titleHeight),
			Extent = new Vector2(MenuWidth - 4 * Margin, BoxHeight),
			TextColor = Game1.textColor
		};
		this._box.OnSubmit += box => this.Submit(box.Text);

		// Route keystrokes to the text box instead of the game world.
		Game1.keyboardDispatcher.Subscriber = this._box;

		int buttonY = (int)this._position.Y + MenuHeight - 2 * Margin - ButtonSize;
		this._okButton = new ClickableTextureComponent(
			new Rectangle((int)this._position.X + MenuWidth - 2 * Margin - ButtonSize, buttonY, ButtonSize, ButtonSize),
			Game1.mouseCursors,
			Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1),
			1f
		);
		this._cancelButton = new ClickableTextureComponent(
			new Rectangle((int)this._position.X + MenuWidth - 3 * Margin - 2 * ButtonSize, buttonY, ButtonSize, ButtonSize),
			Game1.mouseCursors,
			Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47, -1, -1),
			1f
		);
	}

	/// <summary>Release the keyboard. Called when the menu is torn down.</summary>
	public void Close()
	{
		if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, this._box))
			Game1.keyboardDispatcher.Subscriber = null;
	}

	public void Draw(SpriteBatch b)
	{
		b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);
		Game1.drawDialogueBox(this._bounds.X, this._bounds.Y, this._bounds.Width, this._bounds.Height, false, true);

		float titleWidth = Game1.dialogueFont.MeasureString(this._title).X;
		b.DrawString(
			Game1.dialogueFont,
			this._title,
			new Vector2(this._position.X + (MenuWidth - titleWidth) / 2, this._position.Y + 2 * Margin),
			Game1.textColor
		);

		this._box.Draw(b);

		const string hint = "Press Enter to send, or Escape to cancel.";
		float hintWidth = Game1.smallFont.MeasureString(hint).X;
		b.DrawString(
			Game1.smallFont,
			hint,
			new Vector2(
				this._position.X + (MenuWidth - hintWidth) / 2,
				this._box.Position.Y + this._box.Extent.Y + Margin
			),
			Color.Gray
		);

		this._okButton.draw(b);
		this._cancelButton.draw(b);

		if (!Game1.options.hardwareCursor)
		{
			b.Draw(
				Game1.mouseCursors,
				new Vector2(Game1.getMouseX(), Game1.getMouseY()),
				Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16),
				Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f
			);
		}
	}

	public void ReceiveLeftClick(int x, int y)
	{
		if (this._okButton.containsPoint(x, y))
		{
			Game1.playSound("coin");
			this.Submit(this._box.Text);
		}
		else if (this._cancelButton.containsPoint(x, y))
		{
			Game1.playSound("cancel");
			this.Submit("");
		}
		else if (this._box.ContainsPoint(x, y))
		{
			Game1.keyboardDispatcher.Subscriber = this._box;
		}
	}

	public void ReceiveKeyPress(Keys key)
	{
		if (key == Keys.Escape)
			this.Submit("");
		else
			this._box.RecieveSpecialInput(key);
	}

	private void Submit(string text)
	{
		SubmittedHandler? callback = this._onSubmit;
		callback?.Invoke(text ?? "");
	}
}
