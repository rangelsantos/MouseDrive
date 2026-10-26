using System;
using System.Drawing;
using System.Windows.Forms;

namespace MouseDrive;

internal sealed class PedalMeter : PaintedControl
{
	private double value;
	private bool enabled;
	private bool held;

	public PedalMeter()
	{
		Height = 30;
		AccessibleRole = AccessibleRole.ProgressBar;
	}

	public void SetValue(double next, bool on, bool pressed)
	{
		if (Math.Abs(value - next) < 0.001 && enabled == on && held == pressed) return;
		value = next;
		enabled = on;
		held = pressed;
		AccessibleName = Text + (on ? $" {value * 100:0}%" : " off");
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		Rectangle track = new Rectangle(82, 10, Math.Max(1, Width - 136), 10);
		using SolidBrush bg = new SolidBrush(Theme.Track);
		using SolidBrush fill = new SolidBrush(held ? Theme.Cyan : Theme.Accent);
		g.FillRectangle(bg, track);
		if (enabled) g.FillRectangle(fill, track.X, track.Y, (float)(track.Width * value), track.Height);
		TextRenderer.DrawText(g, Text, Theme.Body, new Rectangle(0, 0, 80, Height), enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(g, enabled ? $"{value * 100:0}%" : "Off", Theme.Strong,
			new Rectangle(Width - 50, 0, 50, Height), enabled ? Theme.Cyan : Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
	}
}

internal sealed class PedalCurve : PaintedControl
{
	private int minimum;
	private int maximum = 100;
	private int applyRate = 100;
	private int releaseRate = 200;

	public PedalCurve()
	{
		Height = 110;
		AccessibleName = "Pedal force over time";
	}

	public void SetSettings(PedalSettings config)
	{
		if (minimum == config.Minimum && maximum == config.Maximum && applyRate == config.ApplyRate && releaseRate == config.ReleaseRate) return;
		minimum = config.Minimum;
		maximum = config.Maximum;
		applyRate = config.ApplyRate;
		releaseRate = config.ReleaseRate;
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = Smooth(e);
		g.Clear(BackColor);
		Rectangle plot = new Rectangle(34, 22, Math.Max(1, Width - 46), Height - 44);
		double rise = (maximum - minimum) / (double)applyRate;
		double release = maximum / (double)releaseRate;
		double pause = Math.Max(0.15, (rise + release) * 0.15);
		double duration = Math.Max(0.5, rise + release + pause);
		PointF Point(double seconds, double force) => new PointF(plot.Left + (float)(seconds / duration * plot.Width), plot.Bottom - (float)(force / 100 * plot.Height));
		using Pen grid = new Pen(Theme.Track);
		using Pen line = new Pen(Theme.Accent, 2);
		using Pen marker = new Pen(Theme.Amber) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
		foreach (int tick in new[] { 0, 50, 100 })
		{
			float y = Point(0, tick).Y;
			g.DrawLine(grid, plot.Left, y, plot.Right, y);
			TextRenderer.DrawText(g, tick.ToString(), Theme.Small, new Rectangle(0, (int)y - 8, 30, 18), Theme.Muted, TextFormatFlags.Right);
		}
		g.DrawLines(line, new[] { Point(0, 0), Point(0, minimum), Point(rise, maximum), Point(rise + pause, maximum), Point(rise + pause + release, 0), Point(duration, 0) });
		float releaseX = Point(rise + pause, 0).X;
		g.DrawLine(marker, releaseX, plot.Top, releaseX, plot.Bottom);
		TextRenderer.DrawText(g, "Hold", Theme.Small, new Point(plot.Left, 1), Theme.Muted);
		TextRenderer.DrawText(g, "Release", Theme.Small, new Rectangle(plot.Right - 70, 1, 70, 18), Theme.Amber, TextFormatFlags.Right);
		TextRenderer.DrawText(g, "0 s", Theme.Small, new Point(plot.Left, plot.Bottom + 3), Theme.Muted);
		TextRenderer.DrawText(g, $"{duration:0.##} s", Theme.Small, new Rectangle(plot.Right - 74, plot.Bottom + 3, 74, 18), Theme.Muted, TextFormatFlags.Right);
	}
}
