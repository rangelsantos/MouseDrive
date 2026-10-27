using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace MouseDrive;

// The HUD's two car wheels, drawn from procedural textures so no image files are needed and any HUD size
// stays sharp: a classic wood-rim wheel and a modern GT race-car wheel. The light comes from the top left.
internal static class HudWheels
{
	private static readonly Bitmap Nap = MakeNoise(96, 22, 3);

	private static readonly Bitmap Weave = MakeCarbon();

	private static readonly Color Gold = Color.FromArgb(255, 222, 40);

	private const float VintageRimShare = 0.115f;

	private const float GtRimShare = 0.19f;

	// The GT wheel's flat bottom, as a share of the radius below the center.
	private const float GtFlat = 0.8f;

	// A layer covers the wheel plus its shadow and paddles.
	private const float LayerHalf = 1.3f;

	private const int MaxCached = 12;

	// A wheel takes about 10 ms to draw from scratch, too slow for every HUD frame on the UI thread. So each
	// one is drawn once per pixel size into layers, and a frame only turns and stacks them.
	private sealed class Layer
	{
		public Bitmap Image;

		public float Half;

		public bool Turns;
	}

	private static readonly Dictionary<string, Layer[]> Cache = new Dictionary<string, Layer[]>();

	private static float DeviceScale(Graphics g)
	{
		using Matrix m = g.Transform;
		float[] e = m.Elements;
		return Math.Max(0.1f, (float)Math.Sqrt(e[0] * e[0] + e[1] * e[1]));
	}

	private static Layer[] Cached(string kind, float r, float scale, bool outlined, bool active, Func<(bool Turns, Action<Graphics> Draw)[]> parts)
	{
		string key = string.Format(CultureInfo.InvariantCulture, "{0}|{1:F1}|{2}|{3}", kind, r * scale, outlined, active);
		if (Cache.TryGetValue(key, out var layers))
		{
			return layers;
		}
		if (Cache.Count >= MaxCached)
		{
			foreach (Layer[] old in Cache.Values)
			{
				foreach (Layer l in old)
				{
					l.Image.Dispose();
				}
			}
			Cache.Clear();
		}
		int px = (int)Math.Ceiling(r * LayerHalf * 2f * scale) + 2;
		(bool Turns, Action<Graphics> Draw)[] list = parts();
		layers = new Layer[list.Length];
		for (int i = 0; i < list.Length; i++)
		{
			Bitmap bmp = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
			using (Graphics lg = Graphics.FromImage(bmp))
			{
				lg.SmoothingMode = SmoothingMode.AntiAlias;
				lg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				lg.TranslateTransform((float)px / 2f, (float)px / 2f);
				lg.ScaleTransform(scale, scale);
				list[i].Draw(lg);
			}
			layers[i] = new Layer
			{
				Image = bmp,
				Half = (float)px / 2f / scale,
				Turns = list[i].Turns
			};
		}
		Cache[key] = layers;
		return layers;
	}

	private static void DrawLayers(Graphics g, Layer[] layers, float degrees)
	{
		foreach (Layer layer in layers)
		{
			GraphicsState s = g.Save();
			if (layer.Turns)
			{
				g.RotateTransform(degrees);
			}
			// A fixed layer is drawn at its own pixel size, so it needs no filtering; a turned one does.
			g.InterpolationMode = (layer.Turns ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor);
			g.PixelOffsetMode = PixelOffsetMode.Half;
			g.DrawImage(layer.Image, new RectangleF(0f - layer.Half, 0f - layer.Half, layer.Half * 2f, layer.Half * 2f));
			g.Restore(s);
		}
	}

	// Mostly dark pores with a few light fibers; drawn at half scale it reads as leather or alcantara.
	private static Bitmap MakeNoise(int size, int strength, int seed)
	{
		Random rng = new Random(seed);
		Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				bool light = rng.Next(256) > 225;
				b.SetPixel(x, y, light ? Color.FromArgb(rng.Next(strength / 2), 255, 255, 255) : Color.FromArgb(rng.Next(strength * 2), 0, 0, 0));
			}
		}
		return b;
	}

	// A 2x2 twill: each cell a short gradient, alternating direction, like carbon tows under a clear coat.
	private static Bitmap MakeCarbon()
	{
		const int Cell = 4;
		Bitmap b = new Bitmap(Cell * 2, Cell * 2, PixelFormat.Format32bppArgb);
		using Graphics g = Graphics.FromImage(b);
		for (int cy = 0; cy < 2; cy++)
		{
			for (int cx = 0; cx < 2; cx++)
			{
				Rectangle rect = new Rectangle(cx * Cell, cy * Cell, Cell, Cell);
				using LinearGradientBrush br = new LinearGradientBrush(new Rectangle(rect.X - 1, rect.Y - 1, Cell + 2, Cell + 2), Color.FromArgb(20, 21, 25), Color.FromArgb(58, 60, 68), ((cx + cy) % 2 == 0) ? 0f : 90f);
				br.SetBlendTriangularShape(0.5f);
				g.FillRectangle(br, rect);
			}
		}
		return b;
	}

	private static Color Lerp(Color a, Color b, float t)
	{
		return Color.FromArgb((int)((float)a.A + (float)(b.A - a.A) * t), (int)((float)a.R + (float)(b.R - a.R) * t), (int)((float)a.G + (float)(b.G - a.G) * t), (int)((float)a.B + (float)(b.B - a.B) * t));
	}

	private static GraphicsPath Circle(float cx, float cy, float r)
	{
		GraphicsPath p = new GraphicsPath();
		p.AddEllipse(cx - r, cy - r, r * 2f, r * 2f);
		return p;
	}

	// Concentric strokes from a dark edge to a light crown: a flat stroke reads as a round tube.
	private static void Tube(Action<Pen> stroke, float width, Color edge, Color crown)
	{
		const int Steps = 10;
		for (int i = 0; i < Steps; i++)
		{
			float t = (float)i / (float)(Steps - 1);
			using Pen p = new Pen(Lerp(edge, crown, (float)Math.Pow(t, 0.6)), width * (1f - t * 0.88f))
			{
				LineJoin = LineJoin.Round
			};
			stroke(p);
		}
	}

	private static void Shadow(Graphics g, Action<Pen> stroke, float width, float dx, float dy, int alpha)
	{
		GraphicsState s = g.Save();
		g.TranslateTransform(dx, dy);
		for (int i = 6; i >= 1; i--)
		{
			using Pen p = new Pen(Color.FromArgb(alpha / 6, 0, 0, 0), width * (1f + (float)i * 0.12f))
			{
				LineJoin = LineJoin.Round
			};
			stroke(p);
		}
		g.Restore(s);
	}

	private static void Light(Action<Pen> stroke, float width, float r, int high, int low)
	{
		using LinearGradientBrush light = new LinearGradientBrush(new RectangleF((0f - r) * 1.2f, (0f - r) * 1.2f, r * 2.4f, r * 2.4f), Color.FromArgb(high, 255, 255, 255), Color.FromArgb(low, 0, 0, 0), 45f);
		using Pen p = new Pen(light, width)
		{
			LineJoin = LineJoin.Round
		};
		stroke(p);
	}

	private static void Dome(Graphics g, float x, float y, float r, Color center, Color surround)
	{
		using GraphicsPath p = Circle(x, y, r);
		using PathGradientBrush b = new PathGradientBrush(p)
		{
			CenterColor = center,
			SurroundColors = new Color[1] { surround },
			CenterPoint = new PointF(x - r * 0.3f, y - r * 0.35f)
		};
		g.FillPath(b, p);
	}

	// Driving draws a thin cyan line around the wheel, the HUD's color for live output; standby a faint edge.
	private static void LiveOutline(Graphics g, bool active, bool outlined, GraphicsPath outline, float r)
	{
		using Pen p = (active ?new Pen(HudRenderer.A(Theme.Cyan, 0.63), Math.Max(1.5f, r * 0.022f)) : new Pen(Color.FromArgb(outlined ? 90 : 45, 255, 255, 255), 1f));
		g.DrawPath(p, outline);
	}

	// ---------- classic: varnished wood rim, polished slotted spokes, chrome horn button ----------

	// The shadow and the light come from a fixed direction, so they stay put; the rim's tube shading is the
	// same all round, so it turns with the spokes and the grain in one layer.
	public static void Vintage(Graphics g, HudState s, bool outlined, float cx, float cy, float r, float degrees)
	{
		Layer[] layers = Cached("vintage", r, DeviceScale(g), outlined, s.Active, () => new (bool, Action<Graphics>)[3]
		{
			(false, (Graphics l) => VintageShadow(l, r, outlined)),
			(true, (Graphics l) =>
			{
				VintageSpokes(l, r);
				VintageRim(l, r);
				VintageGrain(l, r);
			}),
			(false, (Graphics l) => VintageLight(l, r, outlined, s.Active))
		});
		GraphicsState saved = g.Save();
		g.TranslateTransform(cx, cy);
		DrawLayers(g, layers, degrees);
		g.Restore(saved);
	}

	private static void VintageRimStroke(Graphics g, Pen p, float r)
	{
		g.DrawEllipse(p, 0f - r, 0f - r, r * 2f, r * 2f);
	}

	private static void VintageSpokes(Graphics g, float r)
	{
		float rim = r * VintageRimShare;
		foreach (float a in new float[3] { 180f, 0f, 90f })
		{
			VintageSpoke(g, r, rim, a);
		}
		Dome(g, 0f, 0f, r * 0.25f, Color.FromArgb(250, 250, 255), Color.FromArgb(80, 82, 88));
		using (Pen ring = new Pen(Color.FromArgb(160, 30, 30, 34), r * 0.012f))
		{
			g.DrawEllipse(ring, (0f - r) * 0.18f, (0f - r) * 0.18f, r * 0.36f, r * 0.36f);
		}
		Dome(g, 0f, 0f, r * 0.1375f, Color.FromArgb(250, 244, 228), Color.FromArgb(196, 184, 156));
		using (SolidBrush dot = new SolidBrush(Color.FromArgb(220, 180, 120, 30)))
		{
			g.FillEllipse(dot, (0f - r) * 0.025f, (0f - r) * 0.025f, r * 0.05f, r * 0.05f);
		}
	}

	private static void VintageShadow(Graphics g, float r, bool outlined)
	{
		Shadow(g, (Pen p) => VintageRimStroke(g, p, r), r * VintageRimShare, r * 0.03f, r * 0.06f, outlined ? 170 : 120);
	}

	private static void VintageRim(Graphics g, float r)
	{
		Tube((Pen p) => VintageRimStroke(g, p, r), r * VintageRimShare, Color.FromArgb(52, 24, 10), Color.FromArgb(186, 112, 52));
	}

	private static void VintageGrain(Graphics g, float r)
	{
		float rim = r * VintageRimShare;
		// A fixed seed, so the grain is the same pattern every time the wheel is drawn.
		Random rng = new Random(11);
		for (int i = 0; i < 70; i++)
		{
			float rr = r + (float)(rng.NextDouble() - 0.5) * rim * 0.85f;
			float start = (float)(rng.NextDouble() * 360.0);
			float sweep = 25f + (float)(rng.NextDouble() * 110.0);
			bool dark = rng.Next(3) > 0;
			using Pen grain = new Pen(dark ? Color.FromArgb(40 + rng.Next(50), 60, 25, 8) : Color.FromArgb(25 + rng.Next(30), 255, 200, 140), 0.4f + (float)rng.NextDouble() * 0.8f);
			g.DrawArc(grain, 0f - rr, 0f - rr, rr * 2f, rr * 2f, start, sweep);
		}
		// The twelve o'clock mark is an amber inlay in the wood.
		Tube(delegate(Pen p)
		{
			g.DrawArc(p, 0f - r, 0f - r, r * 2f, r * 2f, 264f, 12f);
		}, rim * 1.01f, Color.FromArgb(140, 80, 10), Color.FromArgb(255, 200, 90));
		foreach (float a in new float[3] { 180f, 0f, 90f })
		{
			foreach (float off in new float[2] { -7f, 7f })
			{
				double t = (double)(a + off) * Math.PI / 180.0;
				float rr = r - rim * 0.18f;
				Dome(g, (float)Math.Cos(t) * rr, (float)Math.Sin(t) * rr, r * 0.022f, Color.White, Color.FromArgb(110, 112, 118));
			}
		}
	}

	private static void VintageLight(Graphics g, float r, bool outlined, bool active)
	{
		float rim = r * VintageRimShare;
		Light((Pen p) => VintageRimStroke(g, p, r), rim, r, 70, 110);
		using (Pen gloss = new Pen(Color.FromArgb(150, 255, 245, 230), rim * 0.12f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		})
		{
			float gr = r - rim * 0.18f;
			g.DrawArc(gloss, 0f - gr, 0f - gr, gr * 2f, gr * 2f, 195f, 60f);
		}
		using (GraphicsPath outline = new GraphicsPath())
		{
			outline.AddEllipse(0f - r - rim / 2f - 1.5f, 0f - r - rim / 2f - 1.5f, r * 2f + rim + 3f, r * 2f + rim + 3f);
			LiveOutline(g, active, outlined, outline, r);
		}
	}

	// A thin polished spoke with the long slot and round hole of a classic sports wheel.
	private static void VintageSpoke(Graphics g, float r, float rim, float angle)
	{
		GraphicsState s = g.Save();
		g.RotateTransform(angle);
		float x0 = r * 0.2f;
		float x1 = r - rim * 0.3f;
		float w0 = r * 0.19f;
		float w1 = r * 0.11f;
		using GraphicsPath path = new GraphicsPath(FillMode.Alternate);
		path.AddPolygon(new PointF[4]
		{
			new PointF(x0, (0f - w0) / 2f),
			new PointF(x1, (0f - w1) / 2f),
			new PointF(x1, w1 / 2f),
			new PointF(x0, w0 / 2f)
		});
		using (GraphicsPath slot = HudRenderer.Rounded(new RectangleF(r * 0.5f, (0f - r) * 0.028f, r * 0.3f, r * 0.056f), r * 0.028f))
		{
			path.AddPath(slot, connect: false);
		}
		path.AddEllipse(r * 0.34f, (0f - r) * 0.035f, r * 0.07f, r * 0.07f);
		using (SolidBrush shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
		{
			GraphicsState t = g.Save();
			g.TranslateTransform(r * 0.015f, r * 0.035f);
			g.FillPath(shadow, path);
			g.Restore(t);
		}
		using (LinearGradientBrush metal = new LinearGradientBrush(new RectangleF(x0, (0f - w0) / 2f - 1f, x1 - x0, w0 + 2f), Color.Black, Color.Black, 90f))
		{
			metal.InterpolationColors = new ColorBlend
			{
				Colors = new Color[5]
				{
					Color.FromArgb(105, 108, 115),
					Color.FromArgb(250, 251, 255),
					Color.FromArgb(185, 188, 195),
					Color.FromArgb(120, 123, 130),
					Color.FromArgb(70, 72, 78)
				},
				Positions = new float[5] { 0f, 0.3f, 0.5f, 0.78f, 1f }
			};
			g.FillPath(metal, path);
		}
		using (Pen edge = new Pen(Color.FromArgb(140, 40, 40, 46), 0.7f))
		{
			g.DrawPath(edge, path);
		}
		g.Restore(s);
	}

	// ---------- GT: a modern race-car wheel, round with a flat bottom, alcantara and carbon ----------

	// The flat bottom sits at y = flat * r; the arc covers the rest of the circle.
	private static GraphicsPath FlatBottom(float r, float flat)
	{
		double a = Math.Asin(Math.Clamp(flat, -1f, 1f)) * 180.0 / Math.PI;
		GraphicsPath p = new GraphicsPath();
		p.AddArc(0f - r, 0f - r, r * 2f, r * 2f, (float)(180.0 - a), (float)(180.0 + 2.0 * a));
		p.CloseFigure();
		return p;
	}

	// This wheel isn't round, so it turns whole. Its highlight is drawn unturned so the light stays put, and
	// the shift LEDs and the display are drawn live over the cached body.
	public static void RaceCar(Graphics g, HudState s, bool outlined, float cx, float cy, float r, float degrees)
	{
		Layer[] layers = Cached("gt", r, DeviceScale(g), outlined, s.Active, () => new (bool, Action<Graphics>)[1]
		{
			(true, (Graphics l) => RaceCarBody(l, r, outlined, s.Active))
		});
		GraphicsState saved = g.Save();
		g.TranslateTransform(cx, cy);
		DrawLayers(g, layers, degrees);
		float rim = r * GtRimShare;
		using (Pen spec = new Pen(Color.FromArgb(70, 255, 255, 255), rim * 0.1f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		})
		{
			float hr = r - rim * 0.15f;
			g.DrawArc(spec, 0f - hr, 0f - hr, hr * 2f, hr * 2f, 200f, 55f);
		}
		g.RotateTransform(degrees);
		RaceCarLive(g, s, r);
		g.Restore(saved);
	}

	private static void RaceCarBody(Graphics g, float r, bool outlined, bool active)
	{
		float rim = r * GtRimShare;
		using GraphicsPath shape = FlatBottom(r, GtFlat);
		void RimStroke(Pen p)
		{
			g.DrawPath(p, shape);
		}
		RectangleF R(float x, float y, float w, float h)
		{
			return new RectangleF(x * r, y * r, w * r, h * r);
		}
		Shadow(g, RimStroke, rim, r * 0.03f, r * 0.06f, outlined ? 170 : 120);
		// Shift paddles come out from behind the spoke bar.
		foreach (float side in new float[2] { -1f, 1f })
		{
			using GraphicsPath pad = HudRenderer.Rounded(R((side < 0f) ? (-0.7f) : 0.48f, -0.4f, 0.22f, 0.3f), 0.05f * r);
			RectangleF pb = pad.GetBounds();
			using LinearGradientBrush alu = new LinearGradientBrush(pb, Color.FromArgb(150, 154, 165), Color.FromArgb(40, 42, 48), 90f);
			g.FillPath(alu, pad);
			using Pen lip = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(0.6f, r * 0.008f));
			g.DrawLine(lip, pb.X + r * 0.03f, pb.Y + r * 0.015f, pb.Right - r * 0.03f, pb.Y + r * 0.015f);
		}
		// Carbon spokes: a wide horizontal bar, the center block and a short bottom spoke.
		using GraphicsPath spokes = new GraphicsPath(FillMode.Winding);
		foreach ((RectangleF rect, float rad) part in new (RectangleF, float)[3]
		{
			(R(-0.9f, -0.17f, 1.8f, 0.36f), 0.12f * r),
			(R(-0.38f, -0.34f, 0.76f, 0.8f), 0.16f * r),
			(R(-0.13f, 0.3f, 0.26f, 0.52f), 0.06f * r)
		})
		{
			using GraphicsPath p = HudRenderer.Rounded(part.rect, part.rad);
			spokes.AddPath(p, connect: false);
		}
		using (SolidBrush shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
		{
			GraphicsState t = g.Save();
			g.TranslateTransform(r * 0.015f, r * 0.035f);
			g.FillPath(shadow, spokes);
			g.Restore(t);
		}
		using (TextureBrush carbon = new TextureBrush(Weave)
		{
			WrapMode = WrapMode.Tile
		})
		{
			carbon.ScaleTransform(0.5f, 0.5f);
			g.FillPath(carbon, spokes);
		}
		using (LinearGradientBrush coat = new LinearGradientBrush(R(-0.9f, -0.36f, 1.8f, 1.2f), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(90, 0, 0, 0), 90f))
		{
			coat.Blend = new Blend
			{
				Factors = new float[4] { 0f, 0.5f, 0.55f, 1f },
				Positions = new float[4] { 0f, 0.22f, 0.4f, 1f }
			};
			g.FillPath(coat, spokes);
		}
		using (Pen bevel = new Pen(Color.FromArgb(80, 255, 255, 255), Math.Max(0.8f, r * 0.01f)))
		{
			g.DrawPath(bevel, spokes);
		}
		RaceCarScreen(g, r);
		Color[] caps = new Color[4] { HudRenderer.BrakeColor, Theme.Amber, HudRenderer.ClutchColor, HudRenderer.ThrottleColor };
		float[] capX = new float[4] { -0.66f, -0.5f, 0.5f, 0.66f };
		for (int i = 0; i < caps.Length; i++)
		{
			float x = capX[i] * r;
			float br = 0.055f * r;
			using (SolidBrush ring = new SolidBrush(Color.FromArgb(16, 16, 20)))
			{
				g.FillEllipse(ring, x - br * 1.3f, (0f - br) * 1.3f, br * 2.6f, br * 2.6f);
			}
			Dome(g, x, 0f, br, Lerp(caps[i], Color.White, 0.5f), Lerp(caps[i], Color.Black, 0.45f));
		}
		foreach (float kx in new float[2] { -0.18f, 0.18f })
		{
			Rotary(g, kx * r, 0.16f * r, 0.085f * r, r);
		}
		Dome(g, 0f, 0.56f * r, 0.05f * r, Color.FromArgb(255, 150, 140), Color.FromArgb(140, 20, 15));
		// Alcantara rim with fuller thumb grips, the yellow center stripe and contrast stitching.
		Tube(RimStroke, rim, Color.FromArgb(8, 8, 10), Color.FromArgb(74, 74, 80));
		foreach (float start in new float[2] { 155f, -25f })
		{
			Tube(delegate(Pen p)
			{
				g.DrawArc(p, 0f - r, 0f - r, r * 2f, r * 2f, start, 50f);
			}, rim * 1.1f, Color.FromArgb(6, 6, 8), Color.FromArgb(60, 60, 66));
		}
		using (TextureBrush nap = new TextureBrush(Nap)
		{
			WrapMode = WrapMode.Tile
		})
		{
			nap.ScaleTransform(0.5f, 0.5f);
			using Pen np = new Pen(nap, rim * 1.1f);
			g.DrawPath(np, shape);
		}
		Tube(delegate(Pen p)
		{
			g.DrawArc(p, 0f - r, 0f - r, r * 2f, r * 2f, 264f, 12f);
		}, rim * 1.01f, Color.FromArgb(170, 120, 0), Gold);
		float sr = r - rim * 0.36f;
		using (GraphicsPath inner = FlatBottom(sr, (GtFlat * r - rim * 0.36f) / sr))
		{
			using Pen stitch = new Pen(Color.FromArgb(170, Gold), Math.Max(0.6f, r * 0.01f))
			{
				DashPattern = new float[2] { 2f, 1.6f }
			};
			g.DrawPath(stitch, inner);
		}
		float or = r + rim / 2f + 1.5f;
		using (GraphicsPath outline = FlatBottom(or, (GtFlat * r + rim / 2f + 1.5f) / or))
		{
			LiveOutline(g, active, outlined, outline, r);
		}
	}

	private static void RaceCarScreen(Graphics g, float r)
	{
		using GraphicsPath dp = HudRenderer.Rounded(new RectangleF(-0.26f * r, -0.27f * r, 0.52f * r, 0.25f * r), 0.035f * r);
		using SolidBrush glass = new SolidBrush(Color.FromArgb(3, 5, 8));
		g.FillPath(glass, dp);
		using Pen frame = new Pen(Color.FromArgb(70, 72, 80), Math.Max(0.8f, r * 0.01f));
		g.DrawPath(frame, dp);
	}

	// What changes every frame on the display: shift LEDs that follow the throttle, and the steering in percent.
	private static void RaceCarLive(Graphics g, HudState s, float r)
	{
		const int Leds = 9;
		int lit = (int)Math.Round(Math.Clamp(s.Throttle, 0.0, 1.0) * (double)Leds);
		for (int i = 0; i < Leds; i++)
		{
			float x = (-0.2f + (float)i * 0.05f) * r;
			float y = -0.225f * r;
			float lr = 0.017f * r;
			Color on = ((i < 4) ? HudRenderer.ThrottleColor : ((i < 7) ? Theme.Amber : HudRenderer.BrakeColor));
			if (i < lit)
			{
				using GraphicsPath glow = Circle(x, y, lr * 2.4f);
				using PathGradientBrush gb = new PathGradientBrush(glow)
				{
					CenterColor = Color.FromArgb(150, on),
					SurroundColors = new Color[1] { Color.FromArgb(0, on) }
				};
				g.FillPath(gb, glow);
				using SolidBrush core = new SolidBrush(Lerp(on, Color.White, 0.45f));
				g.FillEllipse(core, x - lr, y - lr, lr * 2f, lr * 2f);
			}
			else
			{
				using SolidBrush off = new SolidBrush(Color.FromArgb(34, 36, 40));
				g.FillEllipse(off, x - lr, y - lr, lr * 2f, lr * 2f);
			}
		}
		HudRenderer.Text(g, HudRenderer.Percent(s.Steering), 0.13f * r, Theme.Cyan, 0f, -0.065f * r, StringAlignment.Center, outlined: false);
	}

	private static void Rotary(Graphics g, float x, float y, float kr, float r)
	{
		Dome(g, x, y, kr, Color.FromArgb(130, 132, 140), Color.FromArgb(28, 29, 34));
		using (Pen knurl = new Pen(Color.FromArgb(150, 10, 10, 12), Math.Max(0.5f, r * 0.005f)))
		{
			for (int a = 0; a < 24; a++)
			{
				double t = (double)a * Math.PI * 2.0 / 24.0;
				float c = (float)Math.Cos(t);
				float sn = (float)Math.Sin(t);
				g.DrawLine(knurl, x + c * kr * 0.78f, y + sn * kr * 0.78f, x + c * kr, y + sn * kr);
			}
		}
		using Pen pointer = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(0.8f, r * 0.012f))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		g.DrawLine(pointer, x, y - kr * 0.25f, x, y - kr * 0.75f);
	}
}
