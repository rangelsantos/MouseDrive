using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MouseDrive;

// The three pedals side by side in a car's order (clutch, brake, throttle), each with its live force, its
// force range and its key. Clicking one, or the arrow keys, picks the pedal the editor below shows.
internal sealed class PedalPicker : PaintedControl
{
	// Pedal indexes (0 throttle, 1 brake, 2 clutch) from left to right.
	private static readonly int[] Order = { 2, 1, 0 };

	private const int Gap = 6;
	private const int BarWidth = 34;
	private const int BarTop = 30;
	private const int BarHeight = 80;
	private const int KeyTop = 140;
	private const int KeyHeight = 22;
	private const float BorderPen = 1.5f;
	private const float FocusPen = 2.5f;

	private readonly string[] names;
	private readonly string[] keys = { "", "", "" };
	private readonly double[] values = new double[3];
	private readonly int[] minimums = new int[3];
	private readonly int[] maximums = { 100, 100, 100 };
	private readonly bool[] enabled = new bool[3];
	private readonly bool[] held = new bool[3];
	private int selected;
	private int hot = -1;

	public event EventHandler SelectedChanged;

	public PedalPicker(string[] names)
	{
		this.names = names;
		SetStyle(ControlStyles.Selectable, true);
		TabStop = true;
		Height = 168;
		Cursor = Cursors.Hand;
		Text = "Pedal selector";
		AccessibleName = "Pedal selector";
		AccessibleRole = AccessibleRole.PageTabList;
	}

	public int Selected
	{
		get => selected;
		set
		{
			if (value == selected || value < 0 || value >= names.Length) return;
			selected = value;
			Invalidate();
			SelectedChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public void SetPedal(int index, string key, double value, int minimum, int maximum, bool on, bool pressed)
	{
		if (keys[index] == key && Math.Abs(values[index] - value) < 0.001 && minimums[index] == minimum && maximums[index] == maximum && enabled[index] == on && held[index] == pressed) return;
		keys[index] = key;
		values[index] = value;
		minimums[index] = minimum;
		maximums[index] = maximum;
		enabled[index] = on;
		held[index] = pressed;
		AccessibleDescription = string.Join(", ", Array.ConvertAll(Order, (int i) => names[i] + (enabled[i] ? $" {values[i] * 100:0}%" : " off")));
		Invalidate();
	}

	private float ColumnWidth => (Width - Gap * 2) / 3f;

	private int PedalAt(int x)
	{
		int column = Math.Clamp((int)(x / (ColumnWidth + Gap)), 0, Order.Length - 1);
		return Order[column];
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int next = PedalAt(e.X);
		if (next != hot)
		{
			hot = next;
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
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			Selected = PedalAt(e.X);
		}
		base.OnMouseDown(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys k = keyData & Keys.KeyCode;
		return k == Keys.Left || k == Keys.Right || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		int column = Array.IndexOf(Order, selected);
		if (e.KeyCode == Keys.Left && column > 0) Selected = Order[column - 1];
		else if (e.KeyCode == Keys.Right && column < Order.Length - 1) Selected = Order[column + 1];
		else
		{
			base.OnKeyDown(e);
			return;
		}
		e.Handled = true;
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

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		for (int column = 0; column < Order.Length; column++)
		{
			int i = Order[column];
			RectangleF cell = new RectangleF(column * (ColumnWidth + Gap), 0, ColumnWidth, Height);
			// The border is drawn centered on its path, so the outer columns stay inside the control by half
			// the widest pen; otherwise the throttle's right edge and every top edge get clipped.
			RectangleF edge = RectangleF.Inflate(cell, -FocusPen / 2, -FocusPen / 2);
			if (i == selected || i == hot)
			{
				FillRounded(g, Theme.RowHover, edge, 4);
			}
			if (i == selected)
			{
				using System.Drawing.Drawing2D.GraphicsPath border = HudRenderer.Rounded(edge, 4);
				using Pen pen = new Pen(Theme.Accent, (Focused && ShowFocusCues) ? FocusPen : BorderPen);
				g.DrawPath(pen, border);
			}
			int cx = (int)(cell.X + cell.Width / 2);
			TextRenderer.DrawText(g, names[i], (i == selected) ? Theme.Strong : Theme.Body, new Rectangle((int)cell.X, 6, (int)cell.Width, 20),
				enabled[i] ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			RectangleF bar = new RectangleF(cx - BarWidth / 2, BarTop, BarWidth, BarHeight);
			FillRounded(g, Theme.Track, bar, 3);
			if (enabled[i])
			{
				float fill = (float)(BarHeight * Math.Clamp(values[i], 0, 1));
				if (fill > 0.5f) FillRounded(g, held[i] ? Theme.Cyan : Theme.Accent, new RectangleF(bar.X, bar.Bottom - fill, bar.Width, fill), 3);
				// The force range, when it is narrower than the full pedal: a white tick where a press starts, an
				// amber line at the highest force it reaches.
				if (minimums[i] > 0)
				{
					float y = bar.Bottom - BarHeight * minimums[i] / 100f;
					using Pen tick = new Pen(Color.FromArgb(110, 255, 255, 255), 1f);
					g.DrawLine(tick, bar.X - 4, y, bar.Right + 4, y);
				}
				if (maximums[i] < 100)
				{
					float top = bar.Bottom - BarHeight * maximums[i] / 100f;
					using Pen limit = new Pen(Theme.Amber, 1.5f);
					g.DrawLine(limit, bar.X - 4, top, bar.Right + 4, top);
				}
			}
			else
			{
				TextRenderer.DrawText(g, "Off", Theme.Strong, Rectangle.Round(bar), Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			if (enabled[i])
			{
				TextRenderer.DrawText(g, $"{values[i] * 100:0}%", Theme.Big, new Rectangle((int)cell.X, BarTop + BarHeight + 2, (int)cell.Width, 24),
					Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			bool set = keys[i] != "";
			string key = set ? keys[i] : "No key";
			int keyWidth = Math.Min((int)cell.Width - 16, Math.Max(48, TextRenderer.MeasureText(key, Theme.Strong).Width + 16));
			RectangleF cap = new RectangleF(cx - keyWidth / 2, KeyTop, keyWidth, KeyHeight);
			FillRounded(g, set ? Theme.Input : BackColor, cap, 3);
			using (System.Drawing.Drawing2D.GraphicsPath outline = HudRenderer.Rounded(cap, 3))
			using (Pen pen = new Pen(set ? Color.FromArgb(80, 80, 96) : Theme.Track))
			{
				g.DrawPath(pen, outline);
			}
			TextRenderer.DrawText(g, key, set ? Theme.Strong : Theme.Small, Rectangle.Round(cap), set ? Theme.Text : Theme.Faint,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		}
	}
}

// Force over time for one key press, with each setting named where it acts and a dot for the live force.
internal sealed class PedalCurve : PaintedControl
{
	private static readonly double[] TimeSteps = { 0.1, 0.2, 0.5, 1, 2, 5 };
	private const int MaxTimeTicks = 6;

	private int minimum;
	private int maximum = 100;
	private int pressMs = 1000;
	private int releaseMs = 500;
	private string key = "";
	private double value;
	private bool held;

	public PedalCurve()
	{
		Height = 150;
		AccessibleName = "Pedal force over time";
		AccessibleRole = AccessibleRole.Chart;
	}

	public void SetSettings(PedalSettings config)
	{
		string name = (config.Vk != 0) ? Profile.KeyName(config.Vk) : "Key";
		if (minimum == config.Minimum && maximum == config.Maximum && pressMs == config.PressMs && releaseMs == config.ReleaseMs && key == name) return;
		minimum = config.Minimum;
		maximum = config.Maximum;
		pressMs = config.PressMs;
		releaseMs = config.ReleaseMs;
		key = name;
		Invalidate();
	}

	public void SetLive(double next, bool pressed)
	{
		if (Math.Abs(value - next) < 0.001 && held == pressed) return;
		value = next;
		held = pressed;
		Invalidate();
	}

	private static string Seconds(double s) => s.ToString("0.00", CultureInfo.InvariantCulture) + " s";

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		Rectangle plot = new Rectangle(38, 18, Math.Max(1, Width - 48), Height - 60);
		double rise = (maximum > minimum) ? pressMs / 1000.0 : 0;
		double release = (maximum > 0) ? releaseMs / 1000.0 : 0;
		double hold = Math.Max(0.25, (rise + release) * 0.25);
		double keyUp = rise + hold;
		double duration = keyUp + release + Math.Max(0.15, (rise + release) * 0.1);
		float X(double seconds) => plot.Left + (float)(seconds / duration * plot.Width);
		float Y(double force) => plot.Bottom - (float)(force / 100 * plot.Height);
		using Pen grid = new Pen(Theme.Line);
		using Pen axis = new Pen(Theme.Track);
		foreach (int tick in new[] { 0, 50, 100 })
		{
			g.DrawLine(tick == 0 ? axis : grid, plot.Left, Y(tick), plot.Right, Y(tick));
			TextRenderer.DrawText(g, tick + "%", Theme.Small, new Rectangle(0, (int)Y(tick) - 8, plot.Left - 4, 16), Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
		}
		double step = Array.Find(TimeSteps, (double s) => duration / s <= MaxTimeTicks);
		if (step == 0) step = TimeSteps[^1];
		for (double t = 0; t <= duration + 1e-9; t += step)
		{
			g.DrawLine(axis, X(t), plot.Bottom, X(t), plot.Bottom + 3);
			string label = t.ToString("0.#", CultureInfo.InvariantCulture) + (t == 0 ? " s" : "");
			TextRenderer.DrawText(g, label, Theme.Small, new Rectangle((int)X(t) - 20, plot.Bottom + 3, 40, 16), Theme.Faint, TextFormatFlags.HorizontalCenter);
		}
		// The key strip: how long the key is held in this example.
		Rectangle strip = new Rectangle(plot.Left, plot.Bottom + 22, (int)(X(keyUp) - plot.Left), 15);
		FillRounded(g, Color.FromArgb(60, Theme.Accent), strip, 3);
		TextRenderer.DrawText(g, key + " held", Theme.Small, strip, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		Rectangle after = new Rectangle(strip.Right, strip.Y, plot.Right - strip.Right, strip.Height);
		if (TextRenderer.MeasureText("released", Theme.Small).Width <= after.Width)
		{
			TextRenderer.DrawText(g, "released", Theme.Small, after, Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
		using (Pen limit = new Pen(Color.FromArgb(110, Theme.Amber)) { DashPattern = new[] { 3f, 3f } })
		{
			g.DrawLine(limit, plot.Left, Y(maximum), plot.Right, Y(maximum));
		}
		PointF[] line = { new(X(0), Y(0)), new(X(0), Y(minimum)), new(X(rise), Y(maximum)), new(X(keyUp), Y(maximum)), new(X(keyUp + release), Y(0)), new(X(duration), Y(0)) };
		using (SolidBrush area = new SolidBrush(Color.FromArgb(28, Theme.Accent)))
		{
			g.FillPolygon(area, line);
		}
		using (Pen pen = new Pen(Theme.Accent, 2))
		{
			g.DrawLines(pen, line);
		}
		using (SolidBrush mark = new SolidBrush(Theme.Amber))
		{
			if (minimum > 0 && minimum < maximum) g.FillEllipse(mark, X(0) - 3.5f, Y(minimum) - 3.5f, 7, 7);
			g.FillEllipse(mark, X(rise) - 3.5f, Y(maximum) - 3.5f, 7, 7);
		}
		if (minimum > 0 && minimum < maximum)
		{
			TextRenderer.DrawText(g, $"initial {minimum}%", Theme.Small, new Point((int)X(0) + 6, (int)Y(minimum) - 6), Theme.Amber);
		}
		DrawLabel(g, $"max {maximum}%", Theme.Amber, X(rise), Y(maximum) - 17, plot, center: true);
		if (rise > 0)
		{
			DrawLabel(g, "press " + Seconds(rise), Theme.Muted, (X(0) + X(rise)) / 2 + 14, (Y(minimum) + Y(maximum)) / 2, plot, center: false);
		}
		if (release > 0)
		{
			DrawLabel(g, "release " + Seconds(release), Theme.Muted, (X(keyUp) + X(keyUp + release)) / 2 + 10, (Y(maximum) + Y(0)) / 2, plot, center: false);
		}
		if (held || value > 0)
		{
			// Where on the example the live force is: on the way up while held, on the way down after release.
			double force = value * 100;
			double t = held
				? ((force >= maximum || maximum <= minimum) ? rise : rise * (force - minimum) / (maximum - minimum))
				: keyUp + release * (maximum - force) / Math.Max(1, maximum);
			using SolidBrush dot = new SolidBrush(Theme.Green);
			g.FillEllipse(dot, X(t) - 4, Y(force) - 4, 8, 8);
		}
	}

	// Text that stays inside the plot, so a label near an edge isn't cut off.
	private static void DrawLabel(Graphics g, string text, Color color, float x, float y, Rectangle plot, bool center)
	{
		Size size = TextRenderer.MeasureText(text, Theme.Small);
		float left = center ? x - size.Width / 2f : x;
		left = Math.Clamp(left, plot.Left, Math.Max(plot.Left, plot.Right - size.Width));
		TextRenderer.DrawText(g, text, Theme.Small, new Point((int)left, (int)Math.Max(0, y)), color);
	}
}
