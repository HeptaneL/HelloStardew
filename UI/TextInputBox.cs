using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace HelloStardew.UI;

/// <summary>
/// A word-wrapping text box with a caret, sized for writing a whole reply rather than a name.
/// Registered with <c>Game1.keyboardDispatcher</c> so the game routes keystrokes here.
/// </summary>
internal sealed class TextInputBox : IKeyboardSubscriber
{
	public event Action<TextInputBox>? OnSubmit;

	private readonly int _characterLimit;
	private int _caret;

	public TextInputBox(int characterLimit = 500)
	{
		this._characterLimit = characterLimit;
	}

	public Vector2 Position { get; set; }
	public Vector2 Extent { get; set; }
	public SpriteFont Font { get; set; } = Game1.dialogueFont;
	public Color TextColor { get; set; } = Game1.textColor;

	/// <summary>Set by the keyboard dispatcher; drives caret visibility.</summary>
	public bool Selected { get; set; } = true;

	public string Text { get; private set; } = "";

	public bool ContainsPoint(float x, float y)
	{
		return x >= this.Position.X && x <= this.Position.X + this.Extent.X
			&& y >= this.Position.Y && y <= this.Position.Y + this.Extent.Y;
	}

	public void Draw(SpriteBatch b)
	{
		IClickableMenu.drawTextureBox(
			b,
			(int)this.Position.X,
			(int)this.Position.Y,
			(int)this.Extent.X,
			(int)this.Extent.Y,
			Color.White
		);

		Rectangle area = this.TextArea();
		int lineHeight = (int)this.Font.MeasureString("A").Y;

		if (this.Text.Length > 0)
		{
			int y = area.Y;
			foreach (string line in this.Wrap(this.Text, area.Width))
			{
				if (y + lineHeight > area.Bottom)
					break;

				b.DrawString(this.Font, line, new Vector2(area.X, y), this.TextColor);
				y += lineHeight;
			}
		}

		if (this.Selected)
			this.DrawCaret(b, area, lineHeight);
	}

	private void DrawCaret(SpriteBatch b, Rectangle area, int lineHeight)
	{
		this._caret = Math.Clamp(this._caret, 0, this.Text.Length);

		string beforeCaret = this.Text[..this._caret];
		string[] lines = this.Wrap(beforeCaret, area.Width);

		int caretX = area.X;
		int caretY = area.Y;
		if (lines.Length > 0)
		{
			caretX += (int)this.Font.MeasureString(lines[^1]).X;
			caretY += (lines.Length - 1) * lineHeight;
		}

		b.Draw(Game1.staminaRect, new Rectangle(caretX, caretY, 2, lineHeight), this.TextColor);
	}

	private Rectangle TextArea()
	{
		return new Rectangle(
			(int)this.Position.X + 16,
			(int)this.Position.Y + 16,
			(int)this.Extent.X - 32,
			(int)this.Extent.Y - 32
		);
	}

	/// <summary>Greedy word wrap. Words longer than a line get their own line rather than being dropped.</summary>
	private string[] Wrap(string text, int maxWidth)
	{
		List<string> lines = new();
		StringBuilder current = new();

		foreach (string word in text.Split(' '))
		{
			string candidate = current.Length == 0 ? word : $"{current} {word}";
			if (this.Font.MeasureString(candidate).X <= maxWidth)
			{
				current.Clear();
				current.Append(candidate);
			}
			else
			{
				if (current.Length > 0)
					lines.Add(current.ToString());

				current.Clear();
				current.Append(word);
			}
		}

		if (current.Length > 0)
			lines.Add(current.ToString());

		return lines.ToArray();
	}

	private void Insert(char c)
	{
		if (this.Text.Length >= this._characterLimit)
			return;

		this.Text = this.Text.Insert(this._caret, c.ToString());
		this._caret++;
	}

	private void Backspace()
	{
		if (this._caret <= 0 || this.Text.Length == 0)
			return;

		this.Text = this.Text.Remove(this._caret - 1, 1);
		this._caret--;
	}

	private void Delete()
	{
		if (this._caret >= this.Text.Length)
			return;

		this.Text = this.Text.Remove(this._caret, 1);
	}

	// --- IKeyboardSubscriber -------------------------------------------------------------------

	public void RecieveTextInput(char inputChar)
	{
		switch (inputChar)
		{
			case '\b':
				this.Backspace();
				return;
			case '\r':
			case '\n':
				this.OnSubmit?.Invoke(this);
				return;
		}

		if (char.IsControl(inputChar))
			return;

		this.Insert(inputChar);
	}

	public void RecieveTextInput(string text)
	{
		foreach (char c in text)
			this.RecieveTextInput(c);
	}

	public void RecieveCommandInput(char command)
	{
		if ((Keys)command == Keys.Enter)
			this.OnSubmit?.Invoke(this);
	}

	public void RecieveSpecialInput(Keys key)
	{
		switch (key)
		{
			case Keys.Left:
				this._caret = Math.Max(0, this._caret - 1);
				break;
			case Keys.Right:
				this._caret = Math.Min(this.Text.Length, this._caret + 1);
				break;
			case Keys.Home:
				this._caret = 0;
				break;
			case Keys.End:
				this._caret = this.Text.Length;
				break;
			case Keys.Delete:
				this.Delete();
				break;
			case Keys.Back:
				this.Backspace();
				break;
			case Keys.Enter:
				this.OnSubmit?.Invoke(this);
				break;
		}
	}
}
