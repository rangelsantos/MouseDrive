using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace MouseDrive;

// Owner-drawn controls for the dark UI. Each one paints itself completely; none relies on visual styles.
internal abstract class PaintedControl : Control
{
	protected bool hover;

	protected PaintedControl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, value: true);
		BackColor = Theme.Row;
		ForeColor = Theme.Text;
		Font = Theme.Body;
	}

	protected Color ParentBack => Parent?.BackColor ?? Theme.Bg;

	protected static Graphics Smooth(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		return g;
	}

	protected static void FillRounded(Graphics g, Color c, RectangleF r, float rad)
	{
		using GraphicsPath p = HudRenderer.Rounded(r, rad);
		using SolidBrush b = new SolidBrush(c);
		g.FillPath(b, p);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		Invalidate();
		base.OnEnabledChanged(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Invalidate();
		base.OnLostFocus(e);
	}
}

// A horizontal slider: arrows step by 1, Page Up/Down by a tenth, double-click resets to DefaultValue.
internal sealed class Slider : PaintedControl
{
	private const int Pad = 8;

	private int minimum;

	private int maximum = 100;

	private int value;

	private bool dragging;

	public int DefaultValue { get; set; }

	public int Minimum
	{
		get
		{
			return minimum;
		}
		set
		{
			minimum = value;
			Value = this.value;
			Invalidate();
		}
	}

	public int Maximum
	{
		get
		{
			return maximum;
		}
		set
		{
			maximum = value;
			Value = this.value;
			Invalidate();
		}
	}

	public int Value
	{
		get
		{
			return value;
		}
		set
		{
			int v = Math.Min(Math.Max(value, minimum), maximum);
			if (v != this.value)
			{
				this.value = v;
				Invalidate();
				this.ValueChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public event EventHandler ValueChanged;

	public Slider()
	{
		SetStyle(ControlStyles.Selectable, value: true);
		TabStop = true;
		Height = 22;
		Cursor = Cursors.Hand;
		AccessibleRole = AccessibleRole.Slider;
	}

	private float ThumbX => (float)Pad + (float)(value - minimum) / (float)Math.Max(1, maximum - minimum) * (float)(Width - Pad * 2);

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		float cy = (float)Height / 2f;
		float tx = ThumbX;
		Color fill = (Enabled ? Theme.Accent : Theme.Faint);
		FillRounded(g, Theme.Track, new RectangleF(Pad, cy - 2f, Width - Pad * 2, 4f), 2f);
		FillRounded(g, fill, new RectangleF(Pad, cy - 2f, Math.Max(4f, tx - (float)Pad), 4f), 2f);
		if (Enabled && (Focused || hover || dragging))
		{
			using SolidBrush halo = new SolidBrush(Color.FromArgb(90, Theme.Accent));
			g.FillEllipse(halo, tx - 10f, cy - 10f, 20f, 20f);
		}
		using SolidBrush thumb = new SolidBrush(Enabled ? Color.White : Theme.Muted);
		g.FillEllipse(thumb, tx - 7f, cy - 7f, 14f, 14f);
	}

	private void SetFromX(int x)
	{
		double t = (double)(x - Pad) / (double)Math.Max(1, Width - Pad * 2);
		Value = minimum + (int)Math.Round(Math.Min(Math.Max(t, 0.0), 1.0) * (double)(maximum - minimum));
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			dragging = true;
			SetFromX(e.X);
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			SetFromX(e.X);
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		dragging = false;
		Invalidate();
		base.OnMouseUp(e);
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		Value = DefaultValue;
		base.OnMouseDoubleClick(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)
		{
			return true;
		}
		return base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		int page = Math.Max(1, (maximum - minimum) / 10);
		switch (e.KeyCode)
		{
		case Keys.Left:
		case Keys.Down:
			Value--;
			break;
		case Keys.Right:
		case Keys.Up:
			Value++;
			break;
		case Keys.PageDown:
			Value -= page;
			break;
		case Keys.PageUp:
			Value += page;
			break;
		case Keys.Home:
			Value = minimum;
			break;
		case Keys.End:
			Value = maximum;
			break;
		default:
			base.OnKeyDown(e);
			return;
		}
		e.Handled = true;
	}
}

// A slider with two thumbs for a range (a pedal's initial and maximum force). The thumbs can meet but not
// cross. The keys move the active thumb, the last one dragged; Space switches to the other.
internal sealed class RangeSlider : PaintedControl
{
	private const int Pad = 8;

	private int minimum;

	private int maximum = 100;

	private int low;

	private int high = 100;

	private bool activeHigh = true;

	private bool dragging;

	public int DefaultLow { get; set; }

	public int DefaultHigh { get; set; } = 100;

	public int Minimum
	{
		get
		{
			return minimum;
		}
		set
		{
			minimum = value;
			SetRange(low, high);
		}
	}

	public int Maximum
	{
		get
		{
			return maximum;
		}
		set
		{
			maximum = value;
			SetRange(low, high);
		}
	}

	public int Low
	{
		get
		{
			return low;
		}
		set
		{
			SetRange(Math.Min(value, high), high);
		}
	}

	public int High
	{
		get
		{
			return high;
		}
		set
		{
			SetRange(low, Math.Max(value, low));
		}
	}

	public event EventHandler ValueChanged;

	public RangeSlider()
	{
		SetStyle(ControlStyles.Selectable, value: true);
		TabStop = true;
		Height = 22;
		Cursor = Cursors.Hand;
		AccessibleRole = AccessibleRole.Slider;
	}

	public void SetRange(int lowValue, int highValue)
	{
		int l = Math.Min(Math.Max(lowValue, minimum), maximum);
		int h = Math.Min(Math.Max(highValue, l), maximum);
		if (l != low || h != high)
		{
			low = l;
			high = h;
			AccessibleDescription = low + " to " + high;
			Invalidate();
			this.ValueChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private float XOf(int v)
	{
		return (float)Pad + (float)(v - minimum) / (float)Math.Max(1, maximum - minimum) * (float)(Width - Pad * 2);
	}

	private int ValueAt(int x)
	{
		double t = (double)(x - Pad) / (double)Math.Max(1, Width - Pad * 2);
		return minimum + (int)Math.Round(Math.Min(Math.Max(t, 0.0), 1.0) * (double)(maximum - minimum));
	}

	private void MoveActive(int v)
	{
		if (activeHigh)
		{
			High = v;
		}
		else
		{
			Low = v;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		float cy = (float)Height / 2f;
		float lx = XOf(low);
		float hx = XOf(high);
		FillRounded(g, Theme.Track, new RectangleF(Pad, cy - 2f, Width - Pad * 2, 4f), 2f);
		using (SolidBrush fill = new SolidBrush(Enabled ? Theme.Accent : Theme.Faint))
		{
			g.FillRectangle(fill, lx, cy - 2f, Math.Max(1f, hx - lx), 4f);
		}
		if (Enabled && (Focused || hover || dragging))
		{
			float ax = (activeHigh ? hx : lx);
			using SolidBrush halo = new SolidBrush(Color.FromArgb(90, Theme.Accent));
			g.FillEllipse(halo, ax - 10f, cy - 10f, 20f, 20f);
		}
		using SolidBrush thumb = new SolidBrush(Enabled ? Color.White : Theme.Muted);
		g.FillEllipse(thumb, lx - 7f, cy - 7f, 14f, 14f);
		g.FillEllipse(thumb, hx - 7f, cy - 7f, 14f, 14f);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			dragging = true;
			float lx = XOf(low);
			float hx = XOf(high);
			// When the thumbs meet, the side clicked says which one to pull.
			activeHigh = ((Math.Abs(hx - lx) < 1f) ? ((float)e.X >= hx) : (Math.Abs(e.X - hx) <= Math.Abs(e.X - lx)));
			if (low == high && high == maximum)
			{
				activeHigh = false;
			}
			if (low == high && low == minimum)
			{
				activeHigh = true;
			}
			MoveActive(ValueAt(e.X));
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			MoveActive(ValueAt(e.X));
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		dragging = false;
		Invalidate();
		base.OnMouseUp(e);
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		SetRange(DefaultLow, DefaultHigh);
		base.OnMouseDoubleClick(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Invalidate();
		base.OnLostFocus(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)
		{
			return true;
		}
		return base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		int page = Math.Max(1, (maximum - minimum) / 10);
		int current = (activeHigh ? high : low);
		switch (e.KeyCode)
		{
		case Keys.Left:
		case Keys.Down:
			MoveActive(current - 1);
			break;
		case Keys.Right:
		case Keys.Up:
			MoveActive(current + 1);
			break;
		case Keys.PageDown:
			MoveActive(current - page);
			break;
		case Keys.PageUp:
			MoveActive(current + page);
			break;
		case Keys.Space:
			activeHigh = !activeHigh;
			Invalidate();
			break;
		default:
			base.OnKeyDown(e);
			return;
		}
		e.Handled = true;
	}
}

// The label column of a slider row: a title and an optional smaller hint under it.
internal sealed class Caption : PaintedControl
{
	private string hint;

	private bool dim;

	public string Hint
	{
		get
		{
			return hint;
		}
		set
		{
			hint = value;
			Height = (string.IsNullOrEmpty(value) ? 22 : 32);
			Invalidate();
		}
	}

	public bool Dim
	{
		get
		{
			return dim;
		}
		set
		{
			dim = value;
			Invalidate();
		}
	}

	public Caption()
	{
		Height = 22;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		if (string.IsNullOrEmpty(hint))
		{
			TextRenderer.DrawText(g, Text, Font, ClientRectangle, dim ? Theme.Faint : Color.FromArgb(207, 207, 216), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			return;
		}
		TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, 17), dim ? Theme.Faint : Color.FromArgb(207, 207, 216), TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
		TextRenderer.DrawText(g, hint, Theme.Small, new Rectangle(0, 17, Width, 15), dim ? Color.FromArgb(70, 70, 80) : Theme.Faint, TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
	}

	protected override void OnTextChanged(EventArgs e)
	{
		Invalidate();
		base.OnTextChanged(e);
	}
}

// An on/off switch with a title and an optional subtitle. It is a CheckBox underneath, so Checked,
// keyboard handling and accessibility come for free.
internal sealed class ToggleSwitch : CheckBox
{
	private string subtitle;

	private bool hover;

	public string Subtitle
	{
		get
		{
			return subtitle;
		}
		set
		{
			subtitle = value;
			Height = (string.IsNullOrEmpty(value) ? 32 : 40);
			Invalidate();
		}
	}

	public ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
		AutoSize = false;
		Height = 32;
		BackColor = Theme.Row;
		ForeColor = Theme.Text;
		Font = Theme.Body;
		Cursor = Cursors.Hand;
		Margin = new Padding(0, 0, 0, 0);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Parent?.BackColor ?? Theme.Bg);
		using (GraphicsPath row = HudRenderer.Rounded(new RectangleF(0f, 0f, Width - 1, Height - 1), 5f))
		{
			using SolidBrush rb = new SolidBrush(hover ? Theme.RowHover : BackColor);
			g.FillPath(rb, row);
		}
		float cy = (float)Height / 2f;
		RectangleF knob = new RectangleF(10f, cy - 8f, 28f, 16f);
		using (GraphicsPath track = HudRenderer.Rounded(knob, 8f))
		{
			using SolidBrush tb = new SolidBrush((!Enabled) ? Theme.Input : (Checked ? Theme.Blue : Color.FromArgb(58, 58, 70)));
			g.FillPath(tb, track);
			if (Focused && ShowFocusCues)
			{
				using Pen fp = new Pen(Theme.Accent, 1.5f);
				g.DrawPath(fp, track);
			}
		}
		float kx = (Checked ? (knob.Right - 14f) : (knob.Left + 2f));
		using (SolidBrush kb = new SolidBrush(Checked ? Color.White : Color.FromArgb(185, 185, 196)))
		{
			g.FillEllipse(kb, kx, cy - 6f, 12f, 12f);
		}
		Color tc = (Enabled ? ForeColor : Theme.Faint);
		if (string.IsNullOrEmpty(subtitle))
		{
			TextRenderer.DrawText(g, Text, Font, new Rectangle(46, 0, Width - 50, Height), tc, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			return;
		}
		TextRenderer.DrawText(g, Text, Font, new Rectangle(46, 3, Width - 50, Height / 2), tc, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
		TextRenderer.DrawText(g, subtitle, Theme.Small, new Rectangle(46, Height / 2 + 1, Width - 50, Height / 2 - 3), Theme.Muted, TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
	}
}

// A row of mutually exclusive buttons (the HUD styles...).
internal sealed class Segmented : PaintedControl
{
	private string[] items = new string[0];

	private int selected;

	private int hot = -1;

	public string[] Items
	{
		get
		{
			return items;
		}
		set
		{
			items = value ?? new string[0];
			Invalidate();
		}
	}

	public int SelectedIndex
	{
		get
		{
			return selected;
		}
		set
		{
			if (value != selected && value >= 0 && value < items.Length)
			{
				selected = value;
				Invalidate();
				this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public event EventHandler SelectedIndexChanged;

	// UI Automation names a plain Control by its window text, so set Text to the group's name.
	public Segmented()
	{
		SetStyle(ControlStyles.Selectable, value: true);
		TabStop = true;
		Height = 34;
		Cursor = Cursors.Hand;
		Font = Theme.Strong;
	}

	public void SetItem(int index, string text)
	{
		items[index] = text;
		Invalidate();
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		Invalidate();
		base.OnEnabledChanged(e);
	}

	private RectangleF Segment(int i)
	{
		float w = (float)(Width - 6 - (items.Length - 1) * 3) / (float)Math.Max(1, items.Length);
		return new RectangleF(3f + (float)i * (w + 3f), 3f, w, Height - 6);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(ParentBack);
		FillRounded(g, Theme.Input, new RectangleF(0f, 0f, Width - 1, Height - 1), 5f);
		for (int i = 0; i < items.Length; i++)
		{
			RectangleF r = Segment(i);
			if (i == selected)
			{
				FillRounded(g, Enabled ? Theme.Blue : Theme.Primary, r, 3f);
			}
			else if (i == hot && Enabled)
			{
				FillRounded(g, Color.FromArgb(52, 52, 64), r, 3f);
			}
			Color tc = ((!Enabled) ? Theme.Faint : ((i == selected) ? Color.White : Theme.Muted));
			TextRenderer.DrawText(g, items[i], Font, Rectangle.Round(r), tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		}
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Theme.Accent, 1.5f);
			using GraphicsPath p = HudRenderer.Rounded(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 5f);
			g.DrawPath(fp, p);
		}
	}

	private int HitTest(Point p)
	{
		for (int i = 0; i < items.Length; i++)
		{
			if (Segment(i).Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int h = HitTest(e.Location);
		if (h != hot)
		{
			hot = h;
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hot = -1;
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		int h = HitTest(e.Location);
		if (e.Button == MouseButtons.Left && h >= 0)
		{
			Focus();
			SelectedIndex = h;
		}
		base.OnMouseDown(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Left || k == Keys.Right)
		{
			return true;
		}
		return base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Left)
		{
			SelectedIndex = Math.Max(0, selected - 1);
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Right)
		{
			SelectedIndex = Math.Min(items.Length - 1, selected + 1);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}
}

// A page tab: plain text with an accent underline when selected. A Button, so UI Automation can invoke it.
internal sealed class TabButton : Button
{
	private bool selected;

	private bool hover;

	public bool Selected
	{
		get
		{
			return selected;
		}
		set
		{
			selected = value;
			AccessibleDescription = (value ? "Selected" : null);
			Invalidate();
		}
	}

	public TabButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		BackColor = Theme.Bg;
		Font = Theme.Strong;
		Height = 32;
		Margin = new Padding(1, 0, 1, 0);
		Cursor = Cursors.Hand;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.Clear(BackColor);
		Color tc = (selected ? Color.White : (hover ? Color.FromArgb(213, 213, 220) : Theme.Muted));
		TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - 2), tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		if (selected)
		{
			using SolidBrush b = new SolidBrush(Theme.Accent);
			g.FillRectangle(b, 0, Height - 2, Width, 2);
		}
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Theme.Accent, 1f);
			g.DrawRectangle(fp, 1, 1, Width - 3, Height - 5);
		}
	}
}

// The big Activate button with the drive key shown as a key cap.
internal sealed class ActivateButton : Button
{
	private bool driving;

	private bool hover;

	private string keyText = "";

	public bool Driving
	{
		get
		{
			return driving;
		}
		set
		{
			driving = value;
			UpdateText();
		}
	}

	public string KeyText
	{
		get
		{
			return keyText;
		}
		set
		{
			keyText = value ?? "";
			UpdateText();
		}
	}

	public ActivateButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		Font = Theme.Big;
		Cursor = Cursors.Hand;
		UpdateText();
	}

	private string Caption => (!Enabled) ? "vJoy not ready" : (driving ? "Driving — press to stop" : "Activate");

	private void UpdateText()
	{
		Text = Caption + ((keyText.Length > 0) ? (" (" + keyText + ")") : "");
		Invalidate();
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		UpdateText();
		base.OnEnabledChanged(e);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Parent?.BackColor ?? Theme.Bg);
		Color back = ((!Enabled) ? Theme.Input : (driving ? Theme.Stop : Theme.Go));
		if (hover && Enabled)
		{
			back = ControlPaint.Light(back, 0.15f);
		}
		using (GraphicsPath p = HudRenderer.Rounded(new RectangleF(0f, 0f, Width - 1, Height - 1), 5f))
		{
			using SolidBrush b = new SolidBrush(back);
			g.FillPath(b, p);
		}
		string caption = Caption;
		Size ts = TextRenderer.MeasureText(g, caption, Font, Size.Empty, TextFormatFlags.NoPadding);
		bool chip = Enabled && keyText.Length > 0;
		Size ks = (chip ? TextRenderer.MeasureText(g, keyText, Theme.Strong, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty);
		int chipW = (chip ? (ks.Width + 12) : 0);
		int total = ts.Width + (chip ? (10 + chipW) : 0);
		int x = (Width - total) / 2;
		TextRenderer.DrawText(g, caption, Font, new Point(x, (Height - ts.Height) / 2), Enabled ? Color.White : Theme.Faint, TextFormatFlags.NoPadding);
		if (chip)
		{
			RectangleF cr = new RectangleF(x + ts.Width + 10, (float)(Height - 22) / 2f, chipW, 22f);
			using (GraphicsPath cp = HudRenderer.Rounded(cr, 4f))
			{
				using SolidBrush cb = new SolidBrush(Color.FromArgb(64, 0, 0, 0));
				using Pen pen = new Pen(Color.FromArgb(46, 255, 255, 255), 1f);
				g.FillPath(cb, cp);
				g.DrawPath(pen, cp);
			}
			TextRenderer.DrawText(g, keyText, Theme.Strong, Rectangle.Round(cr), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Color.FromArgb(160, 255, 255, 255), 1f)
			{
				DashStyle = DashStyle.Dot
			};
			g.DrawRectangle(fp, 3, 3, Width - 7, Height - 7);
		}
	}
}

// The profile button in the top bar: "Profile  <name>  ▾".
internal sealed class ProfileButton : Button
{
	private bool hover;

	private string profileName = "";

	public string ProfileName
	{
		get
		{
			return profileName;
		}
		set
		{
			profileName = value ?? "";
			Text = "Profile: " + profileName;
			Invalidate();
		}
	}

	public ProfileButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, value: true);
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		Cursor = Cursors.Hand;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(Parent?.BackColor ?? Theme.Bar);
		using (GraphicsPath p = HudRenderer.Rounded(new RectangleF(0f, 0f, Width - 1, Height - 1), 4f))
		{
			using SolidBrush b = new SolidBrush(hover ? Color.FromArgb(54, 54, 66) : Theme.Input);
			g.FillPath(b, p);
		}
		Size ls = TextRenderer.MeasureText(g, "Profile", Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, "Profile", Theme.Small, new Rectangle(10, 0, ls.Width + 2, Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		int nx = 10 + ls.Width + 8;
		TextRenderer.DrawText(g, profileName, Theme.Strong, new Rectangle(nx, 0, Width - nx - 24, Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, "▾", Theme.Body, new Rectangle(Width - 22, 0, 16, Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Theme.Accent, 1f);
			g.DrawRectangle(fp, 1, 1, Width - 3, Height - 3);
		}
	}
}

internal enum ChipKind
{
	Ok,
	Warn,
	Bad
}

// The vJoy status chip in the top bar. Sizes itself to its text.
internal sealed class StatusChip : PaintedControl
{
	private ChipKind kind;

	public ChipKind Kind
	{
		get
		{
			return kind;
		}
		set
		{
			kind = value;
			Invalidate();
		}
	}

	public StatusChip()
	{
		Height = 30;
		Cursor = Cursors.Hand;
		Font = Theme.Small;
		AccessibleRole = AccessibleRole.PushButton;
		SetStyle(ControlStyles.Selectable, value: true);
		TabStop = true;
	}

	protected override void OnTextChanged(EventArgs e)
	{
		Width = TextRenderer.MeasureText(Text, Font).Width + 22;
		AccessibleName = Text;
		Invalidate();
		base.OnTextChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(ParentBack);
		(Color back, Color dot, Color text) c = kind switch
		{
			ChipKind.Warn => (Theme.WarnBg, Theme.Amber, Theme.Warn),
			ChipKind.Bad => (Theme.BadBg, Color.FromArgb(230, 90, 80), Theme.Bad),
			_ => (Theme.OkBg, Color.FromArgb(63, 208, 127), Theme.Ok),
		};
		FillRounded(g, hover ? ControlPaint.Light(c.back, 0.2f) : c.back, new RectangleF(0f, 0f, Width - 1, Height - 1), 4f);
		using (SolidBrush db = new SolidBrush(c.dot))
		{
			g.FillEllipse(db, 9f, (float)Height / 2f - 3.5f, 7f, 7f);
		}
		TextRenderer.DrawText(g, Text, Font, new Rectangle(18, 0, Width - 18, Height), c.text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Theme.Accent, 1f);
			g.DrawRectangle(fp, 0, 0, Width - 1, Height - 1);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
		{
			OnClick(EventArgs.Empty);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}
}

// The live wheel and pedals on the Drive tab.
internal sealed class DriveMonitor : PaintedControl
{
	private DriveView view;

	public DriveView View
	{
		get
		{
			return view;
		}
		set
		{
			if (value != view)
			{
				view = value;
				Invalidate();
			}
		}
	}

	public DriveMonitor()
	{
		AccessibleName = "Steering and pedals";
		Text = "Steering and pedals";
		AccessibleRole = AccessibleRole.Graphic;
		Height = HudRenderer.DriveHeight;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		HudRenderer.DrawDrive(g, view, Width);
	}
}

// The live wheel in the compact window.
internal sealed class SteeringMonitor : PaintedControl
{
	private HudState state;

	public HudState State
	{
		get
		{
			return state;
		}
		set
		{
			if (value.Steering != state.Steering || value.Active != state.Active)
			{
				state = value;
				Invalidate();
			}
		}
	}

	public SteeringMonitor()
	{
		AccessibleName = "Steering position";
		Text = "Steering position";
		AccessibleRole = AccessibleRole.Graphic;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		float s = Math.Min((float)Width / (float)HudRenderer.MonitorWidth, (float)Height / (float)HudRenderer.MonitorHeight);
		g.TranslateTransform(((float)Width - (float)HudRenderer.MonitorWidth * s) / 2f, ((float)Height - (float)HudRenderer.MonitorHeight * s) / 2f);
		g.ScaleTransform(s, s);
		HudRenderer.DrawMonitor(g, state);
	}
}

// Mouse to wheel on the Steering tab: a ruler of mouse travel over a ruler of wheel lock, joined at every
// 10% of lock by Shape() for the current deadzone/expo. Mouse input is relative, so there is no position
// axis to plot a curve against; the rulers show how far the mouse goes for each step of lock instead.
internal sealed class TravelRuler : PaintedControl
{
	private const float W = 432f;

	private const float H = 90f;

	private const float RulerLeft = 56f;

	private const float RulerRight = 418f;

	private const float RulerCenter = (RulerLeft + RulerRight) / 2f;

	private const float RulerHalf = (RulerRight - RulerLeft) / 2f;

	private const float MouseY = 26f;

	private const float WheelY = 66f;

	private const int FanSteps = 10;

	private static readonly double[] TickSteps = new double[6] { 0.5, 1.0, 2.0, 5.0, 10.0, 20.0 };

	private const int MaxTicksPerSide = 6;

	private static readonly Font Label = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Pixel);

	private static readonly Font Title = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);

	private double deadzone;

	private double expo;

	private double fullLockMm = 10.0;

	private double input;

	private bool hasInput;

	public TravelRuler()
	{
		AccessibleName = "Mouse to wheel";
		Text = "Mouse to wheel";
		AccessibleRole = AccessibleRole.Chart;
	}

	public void SetCurve(double deadzone, double expo, double fullLockMm)
	{
		if (deadzone != this.deadzone || expo != this.expo || fullLockMm != this.fullLockMm)
		{
			this.deadzone = deadzone;
			this.expo = expo;
			this.fullLockMm = fullLockMm;
			Invalidate();
		}
	}

	public void SetInput(double input, bool hasInput)
	{
		if (Math.Abs(input - this.input) > 0.002 || hasInput != this.hasInput)
		{
			this.input = input;
			this.hasInput = hasInput;
			Invalidate();
		}
	}

	private static float X(double v)
	{
		return RulerCenter + (float)v * RulerHalf;
	}

	// The wheel position (-1..1) that gives this lock; Shape() only rises with |x|, so halving the range finds it.
	private double Inverse(double share)
	{
		double lo = 0.0;
		double hi = 1.0;
		for (int i = 0; i < 30; i++)
		{
			double mid = (lo + hi) / 2.0;
			if (MainForm.Shape(mid, deadzone, expo) < Math.Abs(share))
			{
				lo = mid;
			}
			else
			{
				hi = mid;
			}
		}
		return (double)Math.Sign(share) * hi;
	}

	// The smallest round step that keeps the mouse ruler to a few labels per side.
	private double TickStep()
	{
		foreach (double step in TickSteps)
		{
			if (fullLockMm / step <= (double)MaxTicksPerSide)
			{
				return step;
			}
		}
		return TickSteps[TickSteps.Length - 1];
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		g.Clear(BackColor);
		float s = Math.Min((float)Width / W, (float)Height / H);
		g.TranslateTransform(((float)Width - W * s) / 2f, ((float)Height - H * s) / 2f);
		g.ScaleTransform(s, s);
		if (deadzone > 0.0)
		{
			using SolidBrush band = new SolidBrush(Color.FromArgb(40, Theme.Amber));
			g.FillRectangle(band, X(0.0 - deadzone), MouseY - 4f, X(deadzone) - X(0.0 - deadzone), 8f);
		}
		for (int i = -FanSteps; i <= FanSteps; i++)
		{
			double share = (double)i / (double)FanSteps;
			bool major = i % 5 == 0;
			using Pen fan = new Pen(Color.FromArgb(major ? 120 : 55, Theme.Accent), major ? 1.4f : 1f);
			g.DrawLine(fan, X(Inverse(share)), MouseY + 4f, X(share), WheelY - 4f);
		}
		using (Pen rule = new Pen(Theme.Muted, 1.2f))
		{
			g.DrawLine(rule, RulerLeft, MouseY, RulerRight, MouseY);
			g.DrawLine(rule, RulerLeft, WheelY, RulerRight, WheelY);
		}
		using (Pen tick = new Pen(Theme.Muted))
		{
			using SolidBrush lb = new SolidBrush(Color.FromArgb(106, 106, 120));
			using StringFormat mid = new StringFormat
			{
				Alignment = StringAlignment.Center
			};
			double step = TickStep();
			int perSide = (int)Math.Floor(fullLockMm / step + 1E-09);
			for (int i = -perSide; i <= perSide; i++)
			{
				float x = X((double)i * step / fullLockMm);
				g.DrawLine(tick, x, MouseY - 4f, x, MouseY);
				g.DrawString(Math.Abs((double)i * step).ToString("0.#", CultureInfo.InvariantCulture), Label, lb, x, MouseY - 16f, mid);
			}
			for (int i = -4; i <= 4; i++)
			{
				float x = X((double)i / 4.0);
				g.DrawLine(tick, x, WheelY, x, WheelY + 4f);
				g.DrawString(Math.Abs(i * 25) + ((i == 0) ? "" : "%"), Label, lb, x, WheelY + 4f, mid);
			}
			using SolidBrush tb = new SolidBrush(Theme.Muted);
			g.DrawString("MOUSE", Title, tb, 4f, MouseY - 7f);
			g.DrawString("mm", Label, lb, 4f, MouseY + 5f);
			g.DrawString("WHEEL", Title, tb, 4f, WheelY - 7f);
			g.DrawString("lock", Label, lb, 4f, WheelY + 5f);
		}
		if (hasInput)
		{
			double clamped = Math.Min(Math.Max(input, -1.0), 1.0);
			float top = X(clamped);
			float bottom = X(MainForm.Shape(clamped, deadzone, expo));
			using (Pen link = new Pen(Theme.Green, 2f))
			{
				g.DrawLine(link, top, MouseY, bottom, WheelY);
			}
			using SolidBrush dot = new SolidBrush(Theme.Green);
			g.FillEllipse(dot, top - 3.5f, MouseY - 3.5f, 7f, 7f);
			g.FillEllipse(dot, bottom - 3.5f, WheelY - 3.5f, 7f, 7f);
		}
	}
}

// One lamp per vJoy button (up to 32): brighter when a key is mapped to it, green while pressed, with the key
// under it.
internal sealed class LampStrip : PaintedControl
{
	public const int MaxShown = 32;

	private const float KeyRow = 12f;

	private const float Gap = 3f;

	private bool[] bound = new bool[0];

	private bool[] on = new bool[0];

	private string[] keys = new string[0];

	// The row of key names under the lamps takes room only when some button has a key.
	private bool labeled;

	private static readonly Font Num = new Font("Segoe UI", 6.5f, FontStyle.Bold);

	private static readonly Font KeyFont = new Font("Segoe UI", 6.5f);

	public int Count => bound.Length;

	public LampStrip()
	{
		AccessibleName = "vJoy buttons";
		Text = "vJoy buttons";
		AccessibleRole = AccessibleRole.Graphic;
		Height = 24;
	}

	private float Cell => ((float)Width - 45f) / 16f;

	private float Pitch => Cell + (labeled ? KeyRow : 0f) + Gap;

	private int WantedHeight()
	{
		int rows = Math.Max(1, (Math.Min(bound.Length, MaxShown) + 15) / 16);
		return (int)Math.Ceiling((float)rows * Pitch - Gap);
	}

	public void SetLamps(bool[] bound, bool[] on, string[] keys)
	{
		bool changed = bound.Length != this.bound.Length;
		for (int i = 0; !changed && i < bound.Length; i++)
		{
			changed = bound[i] != this.bound[i] || on[i] != this.on[i] || keys[i] != this.keys[i];
		}
		if (changed)
		{
			this.bound = bound;
			this.on = on;
			this.keys = keys;
			labeled = Array.Exists(keys, (string k) => !string.IsNullOrEmpty(k));
			Height = WantedHeight();
			Invalidate();
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int h = WantedHeight();
		if (Height != h)
		{
			Height = h;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		float c = Cell;
		int n = Math.Min(bound.Length, MaxShown);
		for (int i = 0; i < n; i++)
		{
			RectangleF r = new RectangleF((float)(i % 16) * (c + 3f), (float)(i / 16) * Pitch, c, c);
			Color back = (on[i] ? Theme.Green : (bound[i] ? Color.FromArgb(52, 52, 66) : Color.FromArgb(42, 42, 52)));
			Color fore = (on[i] ? Color.FromArgb(16, 48, 26) : (bound[i] ? Color.FromArgb(169, 169, 182) : Color.FromArgb(106, 106, 118)));
			FillRounded(g, back, r, 2f);
			TextRenderer.DrawText(g, (i + 1).ToString(), Num, Rectangle.Round(r), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			if (!string.IsNullOrEmpty(keys[i]))
			{
				// A key name wider than the lamp spills into the gap on both sides before it is cut.
				Rectangle kr = Rectangle.Round(new RectangleF(r.X - 1.5f, r.Bottom + 1f, c + 3f, KeyRow));
				TextRenderer.DrawText(g, keys[i], KeyFont, kr, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
			}
		}
	}
}

// Lines of key caps and labels ("[F9] Drive / standby  [Home] Center steering"), wrapped to the width.
internal sealed class KeyLegend : PaintedControl
{
	private const int LineHeight = 26;

	private List<List<(string text, bool chip)>> lines = new List<List<(string, bool)>>();

	private readonly List<(Rectangle r, string text, bool chip)> laidOut = new List<(Rectangle, string, bool)>();

	private static readonly Font ChipFont = new Font("Segoe UI Semibold", 8f);

	public void SetLines(List<List<(string text, bool chip)>> lines)
	{
		this.lines = lines;
		Relayout();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Relayout();
	}

	private void Relayout()
	{
		laidOut.Clear();
		int y = 0;
		int width = Math.Max(Width, 100);
		foreach (List<(string text, bool chip)> line in lines)
		{
			int x = 0;
			foreach (var item in line)
			{
				Size s = TextRenderer.MeasureText(item.text, item.chip ? ChipFont : Font, Size.Empty, TextFormatFlags.NoPadding);
				int w = (item.chip ? (s.Width + 12) : s.Width);
				if (x > 0 && x + w > width)
				{
					x = 0;
					y += LineHeight;
				}
				laidOut.Add((new Rectangle(x, y + 2, w, 20), item.text, item.chip));
				x += w + (item.chip ? 4 : 12);
			}
			y += LineHeight;
		}
		int h = Math.Max(y, LineHeight);
		if (Height != h)
		{
			Height = h;
		}
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		foreach (var item in laidOut)
		{
			if (item.chip)
			{
				using (GraphicsPath p = HudRenderer.Rounded(item.r, 4f))
				{
					using SolidBrush b = new SolidBrush(Theme.Input);
					using Pen pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
					g.FillPath(b, p);
					g.DrawPath(pen, p);
				}
				TextRenderer.DrawText(g, item.text, ChipFont, item.r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
			else
			{
				TextRenderer.DrawText(g, item.text, Font, item.r, Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}
		}
	}
}

// "‹  BTN 4  ›": picks a vJoy button number. Click a side, use the arrows or the mouse wheel.
internal sealed class Stepper : PaintedControl
{
	private int value = 1;

	private int maximum = 16;

	public int Maximum
	{
		get
		{
			return maximum;
		}
		set
		{
			maximum = Math.Max(1, value);
			Value = this.value;
		}
	}

	public int Value
	{
		get
		{
			return value;
		}
		set
		{
			int v = Math.Min(Math.Max(value, 1), maximum);
			if (v != this.value)
			{
				this.value = v;
				AccessibleName = "vJoy button " + v;
				Text = AccessibleName;
				Invalidate();
				this.ValueChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public event EventHandler ValueChanged;

	public Stepper()
	{
		SetStyle(ControlStyles.Selectable, value: true);
		TabStop = true;
		BackColor = Theme.Input;
		Size = new Size(70, 26);
		Cursor = Cursors.Hand;
		AccessibleRole = AccessibleRole.SpinButton;
		AccessibleName = "vJoy button 1";
		Text = AccessibleName;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(ParentBack);
		FillRounded(g, BackColor, new RectangleF(0f, 0f, Width - 1, Height - 1), 3f);
		Color arrow = (hover ? Color.White : Theme.Muted);
		TextRenderer.DrawText(g, "‹", Theme.Body, new Rectangle(0, 0, 16, Height), (value > 1) ? arrow : Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(g, "›", Theme.Body, new Rectangle(Width - 16, 0, 16, Height), (value < maximum) ? arrow : Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		Size ls = TextRenderer.MeasureText(g, "BTN", Theme.Header, Size.Empty, TextFormatFlags.NoPadding);
		Size vs = TextRenderer.MeasureText(g, value.ToString(), Theme.Body, Size.Empty, TextFormatFlags.NoPadding);
		int x = (Width - ls.Width - 4 - vs.Width) / 2;
		TextRenderer.DrawText(g, "BTN", Theme.Header, new Point(x, (Height - ls.Height) / 2 + 1), Theme.Faint, TextFormatFlags.NoPadding);
		TextRenderer.DrawText(g, value.ToString(), Theme.Body, new Point(x + ls.Width + 4, (Height - vs.Height) / 2), Color.White, TextFormatFlags.NoPadding);
		if (Focused && ShowFocusCues)
		{
			using Pen fp = new Pen(Theme.Accent, 1f);
			g.DrawRectangle(fp, 0, 0, Width - 1, Height - 1);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			if (e.X < Width / 2)
			{
				Value--;
			}
			else
			{
				Value++;
			}
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		OnMouseDown(e);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		Value += Math.Sign(e.Delta);
		if (e is HandledMouseEventArgs h)
		{
			h.Handled = true;
		}
		base.OnMouseWheel(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down)
		{
			return true;
		}
		return base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down)
		{
			Value--;
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)
		{
			Value++;
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}
}

// The HUD tab preview: the chosen style over a track, at the chosen opacity, with a note saying when it's hidden.
internal sealed class HudPreview : PaintedControl
{
	private HudState state;

	private HudLook look;

	private int opacity = 90;

	private int sizePercent = 100;

	private double fade = 1.0;

	private string note = "";

	public HudPreview()
	{
		AccessibleName = "HUD preview";
		Text = "HUD preview";
		AccessibleRole = AccessibleRole.Graphic;
	}

	public void Configure(HudLook look, int opacity, int sizePercent)
	{
		this.look = look;
		this.opacity = opacity;
		this.sizePercent = sizePercent;
		Invalidate();
	}

	public void ShowFrame(HudState state, double fade, string note)
	{
		this.state = state;
		this.fade = fade;
		this.note = note ?? "";
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(ParentBack);
		Rectangle r = ClientRectangle;
		using (GraphicsPath clip = HudRenderer.Rounded(new RectangleF(0f, 0f, Width - 1, Height - 1), 6f))
		{
			g.SetClip(clip);
			using (LinearGradientBrush sky = new LinearGradientBrush(r, Color.Black, Color.Black, LinearGradientMode.Vertical))
			{
				sky.InterpolationColors = new ColorBlend
				{
					Colors = new Color[5]
					{
						Color.FromArgb(79, 127, 180),
						Color.FromArgb(160, 190, 215),
						Color.FromArgb(120, 130, 100),
						Color.FromArgb(70, 82, 50),
						Color.FromArgb(60, 72, 42)
					},
					Positions = new float[5] { 0f, 0.42f, 0.45f, 0.6f, 1f }
				};
				g.FillRectangle(sky, r);
			}
			// The road runs from a vanishing point on the horizon to the bottom edge, with a dashed center line.
			float horizon = (float)Height * 0.45f;
			float cx = (float)Width / 2f;
			using (SolidBrush asphalt = new SolidBrush(Color.FromArgb(58, 60, 66)))
			{
				g.FillPolygon(asphalt, new PointF[4]
				{
					new PointF(cx - 6f, horizon),
					new PointF(cx + 6f, horizon),
					new PointF(cx + (float)Width * 0.42f, Height),
					new PointF(cx - (float)Width * 0.42f, Height)
				});
			}
			using (Pen lane = new Pen(Color.FromArgb(150, 235, 235, 225), 2f)
			{
				DashPattern = new float[2] { 4f, 4f }
			})
			{
				g.DrawLine(lane, cx, horizon + 4f, cx, Height);
			}
			g.ResetClip();
		}
		Size ls = HudRenderer.LogicalSize(look);
		// The preview shows the HUD at a fixed share of its real size, so the Size slider still reads.
		float baseScale = ((look.Style == HudStyle.Minimal) ? 0.9f : 0.45f);
		float w = Math.Min(Math.Min((float)ls.Width * baseScale * (float)sizePercent / 100f, (float)(Height - 8) * (float)ls.Width / (float)ls.Height), Width - 16);
		float s = w / (float)ls.Width;
		int bw = Math.Max(1, (int)Math.Ceiling(w));
		int bh = Math.Max(1, (int)Math.Ceiling((float)ls.Height * s));
		using Bitmap bmp = new Bitmap(bw, bh, PixelFormat.Format32bppArgb);
		using (Graphics hg = Graphics.FromImage(bmp))
		{
			hg.SmoothingMode = SmoothingMode.AntiAlias;
			hg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			hg.ScaleTransform(s, s);
			HudRenderer.Draw(hg, look, state);
		}
		using ImageAttributes ia = new ImageAttributes();
		ia.SetColorMatrix(new ColorMatrix
		{
			Matrix33 = (float)((double)opacity / 100.0 * fade)
		});
		g.DrawImage(bmp, new Rectangle((Width - bw) / 2, (Height - bh) / 2, bw, bh), 0, 0, bw, bh, GraphicsUnit.Pixel, ia);
		// The preview always shows the style; a note at the bottom says when the real HUD is hidden.
		if (note.Length > 0)
		{
			Size ns = TextRenderer.MeasureText(note, Theme.Small);
			Rectangle nr = new Rectangle(8, Height - ns.Height - 14, ns.Width + 12, ns.Height + 6);
			FillRounded(g, Color.FromArgb(200, 10, 13, 19), nr, 4f);
			TextRenderer.DrawText(g, note, Theme.Small, nr, Theme.Warn, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
	}
}

internal sealed class DarkMenuColors : ProfessionalColorTable
{
	private static readonly Color Back = Color.FromArgb(38, 38, 47);

	private static readonly Color Hot = Color.FromArgb(52, 52, 64);

	public override Color ToolStripDropDownBackground => Back;

	public override Color ImageMarginGradientBegin => Back;

	public override Color ImageMarginGradientMiddle => Back;

	public override Color ImageMarginGradientEnd => Back;

	public override Color MenuBorder => Color.FromArgb(56, 56, 68);

	public override Color MenuItemBorder => Hot;

	public override Color MenuItemSelected => Hot;

	public override Color MenuItemSelectedGradientBegin => Hot;

	public override Color MenuItemSelectedGradientEnd => Hot;

	public override Color SeparatorDark => Color.FromArgb(60, 60, 72);

	public override Color SeparatorLight => Back;

	public override Color CheckBackground => Back;

	public override Color CheckSelectedBackground => Hot;

	public override Color CheckPressedBackground => Hot;
}

// Dark context menus. An item whose Tag is "danger" is drawn in red.
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
	public DarkMenuRenderer()
		: base(new DarkMenuColors())
	{
		RoundedEdges = false;
	}

	protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
	{
		e.TextColor = ((!e.Item.Enabled) ? Theme.Faint : ((e.Item.Tag as string == "danger") ? Theme.Bad : Theme.Text));
		base.OnRenderItemText(e);
	}

	protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
	{
		TextRenderer.DrawText(e.Graphics, "✓", Theme.Strong, e.ImageRectangle, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
	{
		e.ArrowColor = Theme.Muted;
		base.OnRenderArrow(e);
	}

	public static ContextMenuStrip NewMenu()
	{
		return new ContextMenuStrip
		{
			Renderer = new DarkMenuRenderer(),
			ShowImageMargin = true,
			Font = Theme.Body
		};
	}
}
