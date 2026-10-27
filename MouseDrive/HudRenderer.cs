using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace MouseDrive;

public enum HudStyle
{
	Full,
	Classic,
	Column,
	Minimal
}

public enum HudWheel
{
	Classic,
	Gt
}

// How the HUD looks: the app-wide options that change the drawing but not the live values.
public readonly record struct HudLook(HudStyle Style, HudWheel Wheel, bool Zoom, bool Numbers, int Background)
{
	public bool HasWheel => Style == HudStyle.Full || Style == HudStyle.Classic;

	public bool Zoomed => Zoom && HasWheel;

	// Without much panel behind them, marks and text get a dark outline so they read over a bright track.
	public bool Outlined => Background < 50;
}

// What the HUD and the Drive monitor show: the vJoy output, plus context.
public struct HudState
{
	// The vJoy X axis, -1 (full left) .. 1 (full right).
	public double Steering;

	public bool Active;

	public string ToggleKey;

	public string Profile;

	// Pedal force 0..1, and whether each pedal is drawn (decided from the HUD setting and the profile).
	public double Throttle;

	public double Brake;

	public double Clutch;

	public bool ShowThrottle;

	public bool ShowBrake;

	public bool ShowClutch;
}

// Draws every HUD style in its own logical units; callers scale the Graphics to the size they want.
internal static class HudRenderer
{
	private static readonly string FontName = (FontFamily.Families.Any((FontFamily f) => f.Name == "Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : "Segoe UI Semibold");

	private static readonly Dictionary<float, Font> Fonts = new Dictionary<float, Font>();

	private static readonly StringFormat Typographic = StringFormat.GenericTypographic;

	public static readonly Color ThrottleColor = Color.FromArgb(80, 225, 120);

	public static readonly Color BrakeColor = Color.FromArgb(255, 84, 72);

	public static readonly Color ClutchColor = Color.FromArgb(90, 165, 255);

	private static readonly Color PanelColor = Color.FromArgb(10, 13, 19);

	private static readonly Color LabelColor = Color.FromArgb(230, 200, 205, 210);

	private const int PanelAlpha = 170;

	public const int MonitorWidth = 240;

	public const int MonitorHeight = 200;

	// The drawn wheel turns this far at full lock. It is a picture of the vJoy axis, not of the car's
	// real steering lock, which the game decides.
	private const float WheelTurnDegrees = 90f;

	// Zoomed, only the top 20% of the rim shows; at 45° the top mark is still inside it.
	private const float ZoomTurnDegrees = 45f;

	// A circle's top 20% is 0.4 r deep and 1.6 r wide.
	private const float ZoomDepth = 0.4f;

	private const float ZoomChord = 1.6f;

	public static Size LogicalSize(HudLook look)
	{
		return look.Style switch
		{
			HudStyle.Full => look.Zoomed ? new Size(360, 132) : new Size(320, 232),
			HudStyle.Column => new Size(72, 272),
			HudStyle.Minimal => new Size(260, 44),
			_ => look.Zoomed ? new Size(320, 112) : new Size(240, 236),
		};
	}

	public static void Draw(Graphics g, HudLook look, HudState s)
	{
		switch (look.Style)
		{
		case HudStyle.Full:
			DrawFull(g, look, s);
			break;
		case HudStyle.Column:
			DrawColumn(g, look, s);
			break;
		case HudStyle.Minimal:
			DrawMinimal(g, look, s);
			break;
		default:
			DrawClassic(g, look, s);
			break;
		}
	}

	// The Drive tab monitor, 240 x 200: a big wheel and the axis bar under it, without panel or text.
	public static void DrawMonitor(Graphics g, HudState s)
	{
		DrawFlatWheel(g, 120f, 86f, 74f, s.Steering, s.Active);
		DrawTrack(g, 30f, 180f, 180f, 8f, s.Steering, s.Active, outlined: false);
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
		Text(g, text, 14f, Color.White, (float)area.Width / 2f, y + 19f, StringAlignment.Center, outlined: false);
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

	private static string Force(double v)
	{
		return ((int)Math.Round(Math.Clamp(v, 0.0, 1.0) * 100.0)).ToString(CultureInfo.InvariantCulture);
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

	internal static Color A(Color c, double alpha)
	{
		return Color.FromArgb((int)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0), c.R, c.G, c.B);
	}

	private static Color FrameColor(bool active)
	{
		return active ? A(Theme.Cyan, 0.95) : Color.FromArgb(191, 160, 168, 178);
	}

	private static Color StatusColor(bool active)
	{
		return active ? Theme.Green : Color.FromArgb(230, 200, 205, 210);
	}

	// Text placed on its baseline, like a canvas fillText; outlined, it gets a dark stroke first.
	internal static void Text(Graphics g, string text, float px, Color color, float x, float baseline, StringAlignment align, bool outlined)
	{
		Font f = F(px);
		float ascent = px * (float)f.FontFamily.GetCellAscent(f.Style) / (float)f.FontFamily.GetEmHeight(f.Style);
		float left = x;
		if (align != StringAlignment.Near)
		{
			float w = g.MeasureString(text, f, PointF.Empty, Typographic).Width;
			left = ((align == StringAlignment.Far) ? (x - w) : (x - w / 2f));
		}
		using GraphicsPath path = new GraphicsPath();
		path.AddString(text, f.FontFamily, (int)f.Style, px, new PointF(left, baseline - ascent), Typographic);
		if (outlined)
		{
			using Pen stroke = new Pen(Color.FromArgb(215, 0, 0, 0), Math.Max(3f, px * 0.28f))
			{
				LineJoin = LineJoin.Round
			};
			g.DrawPath(stroke, path);
		}
		using SolidBrush b = new SolidBrush(color);
		g.FillPath(b, path);
	}

	public static GraphicsPath Rounded(RectangleF r, float rad)
	{
		float d = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
		GraphicsPath p = new GraphicsPath();
		if (d <= 0f)
		{
			p.AddRectangle(r);
			return p;
		}
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

	private static void Panel(Graphics g, HudLook look, Size size, float rad)
	{
		if (look.Background <= 0)
		{
			return;
		}
		double k = (double)look.Background / 100.0;
		using GraphicsPath p = Rounded(new RectangleF(4f, 4f, size.Width - 8, size.Height - 8), rad);
		using SolidBrush bg = new SolidBrush(Color.FromArgb((int)(PanelAlpha * k), PanelColor));
		using Pen edge = new Pen(Color.FromArgb((int)(22 * k), 255, 255, 255), 1f);
		g.FillPath(bg, p);
		g.DrawPath(edge, p);
	}

	private static void StatusLine(Graphics g, HudLook look, HudState s, float x, float baseline, float px)
	{
		Color tc = StatusColor(s.Active);
		if (look.Outlined)
		{
			Dot(g, Color.FromArgb(200, 0, 0, 0), x, baseline - 4f, 5.2f);
		}
		Dot(g, tc, x, baseline - 4f, 3.5f);
		string standby = (string.IsNullOrEmpty(s.ToggleKey) ? "STANDBY" : ("STANDBY · " + s.ToggleKey));
		Text(g, s.Active ? "DRIVING" : standby, px, tc, x + 10f, baseline, StringAlignment.Near, look.Outlined);
	}

	private static List<(string Letter, double Value, Color Color)> Pedals(HudState s)
	{
		List<(string, double, Color)> list = new List<(string, double, Color)>();
		if (s.ShowClutch)
		{
			list.Add(("C", s.Clutch, ClutchColor));
		}
		if (s.ShowBrake)
		{
			list.Add(("B", s.Brake, BrakeColor));
		}
		if (s.ShowThrottle)
		{
			list.Add(("T", s.Throttle, ThrottleColor));
		}
		return list;
	}

	// A pedal bar filling from the bottom. Outlined, the empty track turns dark so the scale still reads.
	private static void PedalBar(Graphics g, bool outlined, float x, float top, float w, float h, double v, Color c)
	{
		if (outlined)
		{
			FillRounded(g, Color.FromArgb(140, 0, 0, 0), x - 1.5f, top - 1.5f, w + 3f, h + 3f, 4f);
			using GraphicsPath edge = Rounded(new RectangleF(x - 1.5f, top - 1.5f, w + 3f, h + 3f), 4f);
			using Pen pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
			g.DrawPath(pen, edge);
		}
		else
		{
			FillRounded(g, Color.FromArgb(34, 255, 255, 255), x, top, w, h, 3f);
		}
		float fh = (float)((double)h * Math.Clamp(v, 0.0, 1.0));
		if (fh > 0.5f)
		{
			FillRounded(g, c, x, top + h - fh, w, fh, 3f);
		}
	}

	// A horizontal track for the axis: the fill grows from the center tick toward the steering.
	private static void DrawTrack(Graphics g, float x, float y, float w, float h, double steering, bool active, bool outlined)
	{
		float mid = x + w / 2f;
		float px = mid + (float)Math.Clamp(steering, -1.0, 1.0) * w / 2f;
		FillRounded(g, outlined ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(31, 255, 255, 255), x, y, w, h, h / 2f);
		using (SolidBrush fill = new SolidBrush(active ? A(Theme.Cyan, 0.8) : Color.FromArgb(128, 160, 168, 178)))
		{
			g.FillRectangle(fill, Math.Min(mid, px), y + 1f, Math.Abs(px - mid), h - 2f);
		}
		using (SolidBrush tick = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
		{
			g.FillRectangle(tick, mid - 0.75f, y - 4f, 1.5f, h + 8f);
		}
		if (outlined)
		{
			FillRounded(g, Color.FromArgb(200, 0, 0, 0), px - 3.5f, y - 5.5f, 7f, h + 11f, 3f);
		}
		FillRounded(g, active ? Theme.Green : Color.FromArgb(230, 190, 195, 200), px - 2f, y - 4f, 4f, h + 8f, 2f);
	}

	// The Drive monitor's line-art wheel, with the amber top mark that racing wheels have at twelve o'clock.
	private static void DrawFlatWheel(Graphics g, float cx, float cy, float r, double steering, bool active)
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

	private static void Wheel(Graphics g, HudLook look, HudState s, float cx, float cy, float r, float degrees)
	{
		if (look.Wheel == HudWheel.Gt)
		{
			HudWheels.RaceCar(g, s, look.Outlined, cx, cy, r, degrees);
		}
		else
		{
			HudWheels.Vintage(g, s, look.Outlined, cx, cy, r, degrees);
		}
	}

	private static float Turn(HudState s, float full)
	{
		return (float)Math.Clamp(s.Steering, -1.0, 1.0) * full;
	}

	// The top 20% of the wheel, scaled up to fill the strip; the turn is halved so the mark stays in view.
	private static void ZoomRim(Graphics g, HudLook look, HudState s, float x, float y, float w, float h)
	{
		GraphicsState saved = g.Save();
		g.SetClip(new RectangleF(x - 6f, y - 8f, w + 12f, h + 8f));
		float r = Math.Min(w / ZoomChord, h / ZoomDepth);
		Wheel(g, look, s, x + w / 2f, y + r * 1.08f, r, Turn(s, ZoomTurnDegrees));
		g.Restore(saved);
	}

	// ---------- Full: status and profile, wheel, steering, a track and every pedal with its number ----------

	private static void DrawFull(Graphics g, HudLook look, HudState s)
	{
		Size size = LogicalSize(look);
		bool o = look.Outlined;
		Panel(g, look, size, 14f);
		StatusLine(g, look, s, 22f, 27f, 12f);
		if (!string.IsNullOrEmpty(s.Profile))
		{
			Text(g, s.Profile, 11f, LabelColor, size.Width - 18, 27f, StringAlignment.Far, o);
		}
		List<(string Letter, double Value, Color Color)> list = Pedals(s);
		const float BarWidth = 22f;
		const float Gap = 12f;
		float barsRight = size.Width - 22;
		float barsLeft = ((list.Count == 0) ? barsRight : (barsRight - (float)list.Count * BarWidth - (float)(list.Count - 1) * Gap));
		float barTop = (look.Zoomed ? 46f : 58f);
		float barHeight = (look.Zoomed ? 60f : 136f);
		float letterY = (look.Zoomed ? 120f : 214f);
		for (int i = 0; i < list.Count; i++)
		{
			float x = barsLeft + (float)i * (BarWidth + Gap);
			PedalBar(g, o, x, barTop, BarWidth, barHeight, list[i].Value, list[i].Color);
			Text(g, list[i].Letter, 11f, LabelColor, x + BarWidth / 2f, letterY, StringAlignment.Center, o);
			if (look.Numbers)
			{
				Text(g, Force(list[i].Value), 12f, list[i].Color, x + BarWidth / 2f, barTop - 7f, StringAlignment.Center, o);
			}
		}
		float left = 22f;
		float right = barsLeft - 16f;
		if (look.Zoomed)
		{
			ZoomRim(g, look, s, left, 40f, right - left - 18f, 60f);
			if (look.Numbers)
			{
				Text(g, "STEER " + Percent(s.Steering), 12f, A(Theme.Cyan, 0.95), (left + right - 18f) / 2f, 94f, StringAlignment.Center, o);
			}
			DrawTrack(g, left + 4f, 112f, right - left - 26f, 6f, s.Steering, s.Active, o);
			return;
		}
		float cx = (left + right) / 2f;
		Wheel(g, look, s, cx, 112f, 56f, Turn(s, WheelTurnDegrees));
		if (look.Numbers)
		{
			Text(g, "STEER " + Percent(s.Steering), 12f, A(Theme.Cyan, 0.95), cx, 196f, StringAlignment.Center, o);
		}
		DrawTrack(g, left + 4f, 210f, right - left - 8f, 6f, s.Steering, s.Active, o);
	}

	// ---------- Classic: the wheel, with clutch and brake on the left and throttle on the right ----------

	private static void DrawClassic(Graphics g, HudLook look, HudState s)
	{
		Size size = LogicalSize(look);
		bool o = look.Outlined;
		Panel(g, look, size, 14f);
		float top = (look.Zoomed ? 34f : 56f);
		float height = (look.Zoomed ? 60f : 122f);
		float barWidth = (look.Zoomed ? 10f : 11f);
		StatusLine(g, look, s, 22f, look.Zoomed ? 25f : 27f, look.Zoomed ? 11f : 12f);
		float lx = 14f;
		float letterY = top + height + 14f;
		void Bar(string letter, double v, Color c, float x)
		{
			PedalBar(g, o, x, top, barWidth, height, v, c);
			if (!look.Zoomed)
			{
				Text(g, letter, 10f, LabelColor, x + barWidth / 2f, letterY, StringAlignment.Center, o);
			}
		}
		if (s.ShowClutch)
		{
			Bar("C", s.Clutch, ClutchColor, lx);
			lx += barWidth + 6f;
		}
		if (s.ShowBrake)
		{
			Bar("B", s.Brake, BrakeColor, lx);
			lx += barWidth + 6f;
		}
		float rx = size.Width - 14 - barWidth;
		if (s.ShowThrottle)
		{
			Bar("T", s.Throttle, ThrottleColor, rx);
		}
		if (look.Zoomed)
		{
			// The rim strip keeps the same width whichever pedals show, so the wheel doesn't jump.
			ZoomRim(g, look, s, 52f, 40f, size.Width - 104, 54f);
			if (look.Numbers)
			{
				Text(g, Percent(s.Steering), 13f, A(Theme.Cyan, 0.95), (float)size.Width / 2f, 100f, StringAlignment.Center, o);
			}
			return;
		}
		Wheel(g, look, s, (float)size.Width / 2f, 118f, 58f, Turn(s, WheelTurnDegrees));
		if (look.Numbers)
		{
			Text(g, "STEER " + Percent(s.Steering), 12f, A(Theme.Cyan, 0.95), (float)size.Width / 2f, 220f, StringAlignment.Center, o);
		}
	}

	// ---------- Column: a narrow vertical strip for a screen edge ----------

	private static void DrawColumn(Graphics g, HudLook look, HudState s)
	{
		Size size = LogicalSize(look);
		bool o = look.Outlined;
		float cx = (float)size.Width / 2f;
		Panel(g, look, size, 10f);
		Color tc = StatusColor(s.Active);
		if (o)
		{
			Dot(g, Color.FromArgb(200, 0, 0, 0), cx, 18f, 5.2f);
		}
		Dot(g, tc, cx, 18f, 3.5f);
		Text(g, s.Active ? "DRIVE" : "STBY", 10f, tc, cx, 36f, StringAlignment.Center, o);
		DrawTrack(g, 10f, 50f, size.Width - 20, 6f, s.Steering, s.Active, o);
		if (look.Numbers)
		{
			Text(g, Percent(s.Steering), 12f, A(Theme.Cyan, 0.95), cx, 78f, StringAlignment.Center, o);
		}
		List<(string Letter, double Value, Color Color)> list = Pedals(s);
		const float BarWidth = 12f;
		const float Gap = 8f;
		float x0 = ((float)size.Width - ((float)list.Count * BarWidth + (float)Math.Max(0, list.Count - 1) * Gap)) / 2f;
		for (int i = 0; i < list.Count; i++)
		{
			float x = x0 + (float)i * (BarWidth + Gap);
			PedalBar(g, o, x, 92f, BarWidth, 140f, list[i].Value, list[i].Color);
			Text(g, list[i].Letter, 10f, LabelColor, x + BarWidth / 2f, 248f, StringAlignment.Center, o);
			if (look.Numbers)
			{
				Text(g, Force(list[i].Value), 9f, list[i].Color, x + BarWidth / 2f, 262f, StringAlignment.Center, o);
			}
		}
	}

	// ---------- Minimal: hairlines, steering on top; brake grows left of center and throttle right below ----------

	private static void DrawMinimal(Graphics g, HudLook look, HudState s)
	{
		Size size = LogicalSize(look);
		bool o = look.Outlined;
		Panel(g, look, size, 10f);
		float x = 20f;
		float w = size.Width - (look.Numbers ? 70 : 40);
		float mid = x + w / 2f;
		void Line(float y, float x1, float x2, Color c, float th)
		{
			if (o)
			{
				using Pen halo = new Pen(Color.FromArgb(150, 0, 0, 0), th + 3f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round
				};
				g.DrawLine(halo, x1, y, x2, y);
			}
			using Pen p = new Pen(c, th)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			g.DrawLine(p, x1, y, x2, y);
		}
		float px = mid + (float)Math.Clamp(s.Steering, -1.0, 1.0) * w / 2f;
		Line(14f, x, x + w, Color.FromArgb(120, 255, 255, 255), 2f);
		Line(14f, mid, px, s.Active ? A(Theme.Cyan, 0.9) : Color.FromArgb(150, 160, 168, 178), 2f);
		if (o)
		{
			FillRounded(g, Color.FromArgb(200, 0, 0, 0), mid - 1.5f, 6.5f, 3f, 15f, 1.5f);
		}
		FillRounded(g, Color.FromArgb(220, 255, 255, 255), mid - 0.75f, 8f, 1.5f, 12f, 0.75f);
		if (o)
		{
			FillRounded(g, Color.FromArgb(200, 0, 0, 0), px - 3.5f, 5.5f, 7f, 17f, 3.5f);
		}
		FillRounded(g, StatusColor(s.Active), px - 2f, 7f, 4f, 14f, 2f);
		if (s.ShowBrake || s.ShowThrottle)
		{
			Line(27f, x, x + w, Color.FromArgb(60, 255, 255, 255), 3f);
			if (s.ShowBrake && s.Brake > 0.0)
			{
				Line(27f, mid, mid - (float)Math.Clamp(s.Brake, 0.0, 1.0) * w / 2f, BrakeColor, 3f);
			}
			if (s.ShowThrottle && s.Throttle > 0.0)
			{
				Line(27f, mid, mid + (float)Math.Clamp(s.Throttle, 0.0, 1.0) * w / 2f, ThrottleColor, 3f);
			}
		}
		if (s.ShowClutch)
		{
			Line(36f, x, x + w * 0.3f, Color.FromArgb(60, 255, 255, 255), 2f);
			if (s.Clutch > 0.0)
			{
				Line(36f, x, x + w * 0.3f * (float)Math.Clamp(s.Clutch, 0.0, 1.0), ClutchColor, 2f);
			}
		}
		if (look.Numbers)
		{
			Text(g, Percent(s.Steering), 11f, A(Theme.Cyan, 0.95), size.Width - 12, 19f, StringAlignment.Far, o);
		}
	}
}
