using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace MouseDrive;

public enum HudStyle
{
	Wheel,
	Bar
}

// What the HUD and the Drive monitor show: the steering sent to vJoy, plus context.
public struct HudState
{
	// The vJoy X axis, -1 (full left) .. 1 (full right).
	public double Steering;

	public bool Active;

	public string ToggleKey;
}

// Draws every HUD style in its own logical units; callers scale the Graphics to the size they want.
internal static class HudRenderer
{
	private static readonly string FontName = (FontFamily.Families.Any((FontFamily f) => f.Name == "Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : "Segoe UI Semibold");

	private static readonly Dictionary<float, Font> Fonts = new Dictionary<float, Font>();

	private static readonly StringFormat Typographic = StringFormat.GenericTypographic;

	public const int MonitorWidth = 240;

	public const int MonitorHeight = 200;

	// The drawn wheel turns this far at full lock. It is a picture of the vJoy axis, not of the car's
	// real steering lock, which the game decides.
	private const float WheelTurnDegrees = 90f;

	public static Size LogicalSize(HudStyle style, bool numbers)
	{
		return style switch
		{
			HudStyle.Bar => new Size(numbers ? 330 : 280, 44),
			_ => new Size(200, numbers ? 222 : 200),
		};
	}

	public static void Draw(Graphics g, HudStyle style, HudState s, bool numbers)
	{
		switch (style)
		{
		case HudStyle.Bar:
			DrawBar(g, s, numbers);
			break;
		default:
			DrawWheelPanel(g, s, numbers);
			break;
		}
	}

	// The Drive tab monitor, 240 x 200: a big wheel and the axis bar under it, without panel or text.
	public static void DrawMonitor(Graphics g, HudState s)
	{
		DrawWheel(g, 120f, 86f, 74f, s.Steering, s.Active);
		DrawTrack(g, 30f, 180f, 180f, 8f, s.Steering, s.Active);
	}

	// A short notice in the middle of the HUD ("DRIVING", "CENTER"), drawn over everything else.
	public static void DrawToast(Graphics g, string text, Size area)
	{
		Font f = F(14f);
		SizeF ts = g.MeasureString(text, f, PointF.Empty, Typographic);
		float w = ts.Width + 24f;
		float h = 28f;
		float x = ((float)area.Width - w) / 2f;
		float y = ((float)area.Height - h) / 2f;
		FillRounded(g, Color.FromArgb(225, 10, 13, 19), x, y, w, h, 6f);
		using (Pen edge = new Pen(A(Theme.Cyan, 0.6), 1f))
		{
			using GraphicsPath p = Rounded(new RectangleF(x, y, w, h), 6f);
			g.DrawPath(edge, p);
		}
		Text(g, text, 14f, Color.White, (float)area.Width / 2f, y + 19f, StringAlignment.Center);
	}

	public static string Percent(double v)
	{
		int n = (int)Math.Round(Math.Abs(v) * 100.0);
		if (n == 0)
		{
			return "0%";
		}
		return ((v < 0.0) ? "−" : "+") + n + "%";
	}

	public static string Signed(double v)
	{
		string r = Math.Abs(v).ToString("0.00", CultureInfo.InvariantCulture);
		return ((v < 0.0 && r != "0.00") ? "−" : "+") + r;
	}

	private static Font F(float px)
	{
		if (!Fonts.TryGetValue(px, out var f))
		{
			f = new Font(FontName, px, FontStyle.Regular, GraphicsUnit.Pixel);
			Fonts[px] = f;
		}
		return f;
	}

	private static Color A(Color c, double alpha)
	{
		return Color.FromArgb((int)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0), c.R, c.G, c.B);
	}

	private static Color FrameColor(bool active)
	{
		return active ? A(Theme.Cyan, 0.95) : Color.FromArgb(191, 160, 168, 178);
	}

	private static Color TextColor(bool active)
	{
		return active ? Theme.Green : Color.FromArgb(230, 200, 205, 210);
	}

	// Text placed on its baseline, like a canvas fillText.
	private static void Text(Graphics g, string text, float px, Color color, float x, float baseline, StringAlignment align = StringAlignment.Near)
	{
		Font f = F(px);
		float ascent = px * (float)f.FontFamily.GetCellAscent(f.Style) / (float)f.FontFamily.GetEmHeight(f.Style);
		float left = x;
		if (align != StringAlignment.Near)
		{
			float w = g.MeasureString(text, f, PointF.Empty, Typographic).Width;
			left = ((align == StringAlignment.Far) ? (x - w) : (x - w / 2f));
		}
		using SolidBrush b = new SolidBrush(color);
		g.DrawString(text, f, b, left, baseline - ascent, Typographic);
	}

	public static GraphicsPath Rounded(RectangleF r, float rad)
	{
		float d = rad * 2f;
		GraphicsPath p = new GraphicsPath();
		p.AddArc(r.X, r.Y, d, d, 180f, 90f);
		p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		p.CloseFigure();
		return p;
	}

	private static void FillRounded(Graphics g, Color c, float x, float y, float w, float h, float rad)
	{
		using GraphicsPath p = Rounded(new RectangleF(x, y, w, h), rad);
		using SolidBrush b = new SolidBrush(c);
		g.FillPath(b, p);
	}

	private static void Dot(Graphics g, Color c, float x, float y, float r)
	{
		using SolidBrush b = new SolidBrush(c);
		g.FillEllipse(b, x - r, y - r, r * 2f, r * 2f);
	}

	// A three-spoke wheel turned by the steering, with the amber top mark that racing wheels have at
	// twelve o'clock. Every stroke gets a dark outline first, so it reads over a bright track.
	private static void DrawWheel(Graphics g, float cx, float cy, float r, double steering, bool active)
	{
		GraphicsState saved = g.Save();
		g.TranslateTransform(cx, cy);
		g.RotateTransform((float)Math.Clamp(steering, -1.0, 1.0) * WheelTurnDegrees);
		float rim = r * 0.16f;
		float hub = r * 0.26f;
		Color color = FrameColor(active);
		for (int layer = 0; layer < 2; layer++)
		{
			bool shadow = layer == 0;
			Color c = (shadow ? Color.FromArgb(120, 0, 0, 0) : color);
			float grow = (shadow ? 2.5f : 0f);
			using (Pen ring = new Pen(c, rim + grow))
			{
				g.DrawEllipse(ring, 0f - r, 0f - r, r * 2f, r * 2f);
			}
			using (Pen spoke = new Pen(c, rim * 0.8f + grow)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			})
			{
				g.DrawLine(spoke, 0f - r, 0f, 0f - hub, 0f);
				g.DrawLine(spoke, hub, 0f, r, 0f);
				g.DrawLine(spoke, 0f, hub, 0f, r);
			}
			Dot(g, c, 0f, 0f, hub + grow / 2f);
		}
		Dot(g, Color.FromArgb(200, 10, 13, 19), 0f, 0f, hub * 0.45f);
		using (Pen mark = new Pen(Theme.Amber, rim))
		{
			g.DrawArc(mark, 0f - r, 0f - r, r * 2f, r * 2f, 261f, 18f);
		}
		g.Restore(saved);
	}

	// A horizontal track for the axis: the fill grows from the center tick toward the steering.
	private static void DrawTrack(Graphics g, float x, float y, float w, float h, double steering, bool active)
	{
		float mid = x + w / 2f;
		float px = mid + (float)Math.Clamp(steering, -1.0, 1.0) * w / 2f;
		FillRounded(g, Color.FromArgb(31, 255, 255, 255), x, y, w, h, h / 2f);
		using (SolidBrush fill = new SolidBrush(active ? A(Theme.Cyan, 0.8) : Color.FromArgb(128, 160, 168, 178)))
		{
			g.FillRectangle(fill, Math.Min(mid, px), y + 1f, Math.Abs(px - mid), h - 2f);
		}
		using (SolidBrush tick = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
		{
			g.FillRectangle(tick, mid - 0.75f, y - 4f, 1.5f, h + 8f);
		}
		FillRounded(g, active ? Theme.Green : Color.FromArgb(230, 190, 195, 200), px - 2f, y - 4f, 4f, h + 8f, 2f);
	}

	// 200 x 222 (200 without numbers): status line, the wheel, and the steering in percent.
	private static void DrawWheelPanel(Graphics g, HudState s, bool numbers)
	{
		Size size = LogicalSize(HudStyle.Wheel, numbers);
		using (GraphicsPath p = Rounded(new RectangleF(4f, 4f, size.Width - 8, size.Height - 8), 14f))
		{
			using SolidBrush bg = new SolidBrush(Color.FromArgb(160, 10, 13, 19));
			using Pen edge = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
			g.FillPath(bg, p);
			g.DrawPath(edge, p);
		}
		Color tc = TextColor(s.Active);
		Dot(g, tc, 22f, 22f, 3.5f);
		string standby = (string.IsNullOrEmpty(s.ToggleKey) ? "STANDBY" : ("STANDBY · " + s.ToggleKey));
		Text(g, s.Active ? "DRIVING" : standby, 12f, tc, 32f, 26f);
		DrawWheel(g, 100f, 112f, 62f, s.Steering, s.Active);
		if (numbers)
		{
			Text(g, "STEER " + Percent(s.Steering), 12f, A(Theme.Cyan, 0.9), 100f, 205f, StringAlignment.Center);
		}
	}

	// 330 x 44 (280 without numbers): a thin bar for a screen edge.
	private static void DrawBar(Graphics g, HudState s, bool numbers)
	{
		Size size = LogicalSize(HudStyle.Bar, numbers);
		FillRounded(g, Color.FromArgb(168, 10, 13, 19), 2f, 2f, size.Width - 4, size.Height - 4, 10f);
		Color tc = TextColor(s.Active);
		Dot(g, tc, 16f, 22f, 3.5f);
		Text(g, s.Active ? "DRIVE" : "STBY", 12f, tc, 25f, 26.5f);
		DrawTrack(g, 76f, 18f, 192f, 8f, s.Steering, s.Active);
		if (numbers)
		{
			Text(g, Percent(s.Steering), 12f, A(Theme.Cyan, 0.9), size.Width - 14, 26.5f, StringAlignment.Far);
		}
	}
}
