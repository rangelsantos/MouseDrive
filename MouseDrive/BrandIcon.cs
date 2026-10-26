using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MouseDrive;

// The app icon (Assets\Brand\MouseDrive.ico, embedded) for the window and the tray, and its
// "driving" variant with a red dot in the corner, built once from the same frames.
internal static class BrandIcon
{
	private static readonly int[] DotSizes = new int[7] { 16, 20, 24, 32, 40, 48, 64 };

	private static readonly Color Dot = Color.FromArgb(235, 52, 52);

	private static Icon idle;

	private static Icon driving;

	private static Icon idleTray;

	private static Icon drivingTray;

	// Title bar and taskbar: WinForms takes the small and large sizes from the frames.
	public static Icon Window(bool active)
	{
		if (idle == null)
		{
			using Stream s = typeof(BrandIcon).Assembly.GetManifestResourceStream("MouseDrive.ico");
			idle = new Icon(s);
			driving = WithDot(idle);
		}
		return (active ? driving : idle);
	}

	// NotifyIcon draws whatever single size it gets, so hand it the small one.
	public static Icon Tray(bool active)
	{
		if (idleTray == null)
		{
			idleTray = new Icon(Window(active: false), SystemInformation.SmallIconSize);
			drivingTray = new Icon(Window(active: true), SystemInformation.SmallIconSize);
		}
		return (active ? drivingTray : idleTray);
	}

	private static Icon WithDot(Icon icon)
	{
		Bitmap[] frames = new Bitmap[DotSizes.Length];
		for (int i = 0; i < DotSizes.Length; i++)
		{
			using Icon frame = new Icon(icon, DotSizes[i], DotSizes[i]);
			frames[i] = frame.ToBitmap();
			AddDot(frames[i]);
		}
		MemoryStream ms = new MemoryStream();
		WriteIco(ms, frames);
		foreach (Bitmap b in frames)
		{
			b.Dispose();
		}
		ms.Position = 0L;
		return new Icon(ms);
	}

	// The dot sits in the bottom-right corner with a transparent ring cut around it, so it stays
	// readable where it overlaps the wheel. Composited by hand: GDI+ can't antialias a cut-out.
	private static void AddDot(Bitmap b)
	{
		int n = b.Width;
		float r = n * 0.17f;
		float cut = r + Math.Max(1f, n / 16f);
		float c = n - r;
		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				float dx = x + 0.5f - c;
				float dy = y + 0.5f - c;
				float d = (float)Math.Sqrt(dx * dx + dy * dy);
				if (d > cut + 1f)
				{
					continue;
				}
				Color p = b.GetPixel(x, y);
				float under = p.A / 255f * Coverage(d - cut);
				float dot = Coverage(r - d);
				float a = dot + under * (1f - dot);
				if (a <= 0f)
				{
					b.SetPixel(x, y, Color.Transparent);
					continue;
				}
				b.SetPixel(x, y, Color.FromArgb((int)(a * 255f + 0.5f), Mix(Dot.R, p.R, dot, under, a), Mix(Dot.G, p.G, dot, under, a), Mix(Dot.B, p.B, dot, under, a)));
			}
		}
	}

	// How much of a pixel lies inside an edge, given its center's distance inside it.
	private static float Coverage(float inside)
	{
		return Math.Clamp(inside + 0.5f, 0f, 1f);
	}

	private static int Mix(int top, int bottom, float topA, float bottomA, float a)
	{
		return (int)((top * topA + bottom * bottomA * (1f - topA)) / a + 0.5f);
	}

	// 32-bit BMP frames: the color rows bottom-up, then an all-zero AND mask (alpha does the work).
	private static void WriteIco(Stream stream, Bitmap[] frames)
	{
		using BinaryWriter w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		w.Write((short)0);
		w.Write((short)1);
		w.Write((short)frames.Length);
		int offset = 6 + 16 * frames.Length;
		foreach (Bitmap b in frames)
		{
			int n = b.Width;
			int size = 40 + n * n * 4 + n * MaskStride(n);
			w.Write((byte)n);
			w.Write((byte)n);
			w.Write((short)0);
			w.Write((short)1);
			w.Write((short)32);
			w.Write(size);
			w.Write(offset);
			offset += size;
		}
		foreach (Bitmap b in frames)
		{
			int n = b.Width;
			w.Write(40);
			w.Write(n);
			w.Write(n * 2);
			w.Write((short)1);
			w.Write((short)32);
			for (int i = 0; i < 6; i++)
			{
				w.Write(0);
			}
			for (int y = n - 1; y >= 0; y--)
			{
				for (int x = 0; x < n; x++)
				{
					Color p = b.GetPixel(x, y);
					w.Write(p.B);
					w.Write(p.G);
					w.Write(p.R);
					w.Write(p.A);
				}
			}
			w.Write(new byte[n * MaskStride(n)]);
		}
	}

	private static int MaskStride(int n)
	{
		return (n + 31) / 32 * 4;
	}
}
