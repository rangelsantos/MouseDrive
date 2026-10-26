using System.Drawing;

namespace MouseDrive;

internal static class Theme
{
	public static readonly Color Bg = Color.FromArgb(18, 18, 22);

	public static readonly Color Bar = Color.FromArgb(28, 28, 36);

	public static readonly Color Row = Color.FromArgb(30, 30, 38);

	public static readonly Color RowHover = Color.FromArgb(38, 38, 48);

	public static readonly Color Input = Color.FromArgb(45, 45, 55);

	public static readonly Color Primary = Color.FromArgb(45, 55, 75);

	public static readonly Color Blue = Color.FromArgb(45, 70, 110);

	public static readonly Color Danger = Color.FromArgb(90, 40, 40);

	public static readonly Color Go = Color.FromArgb(40, 120, 90);

	public static readonly Color Stop = Color.FromArgb(150, 40, 40);

	public static readonly Color Text = Color.Gainsboro;

	public static readonly Color Muted = Color.FromArgb(139, 139, 152);

	public static readonly Color Faint = Color.FromArgb(92, 92, 104);

	public static readonly Color Line = Color.FromArgb(38, 38, 47);

	public static readonly Color Track = Color.FromArgb(56, 56, 68);

	public static readonly Color Accent = Color.FromArgb(90, 154, 214);

	public static readonly Color Amber = Color.FromArgb(255, 190, 60);

	public static readonly Color Green = Color.FromArgb(120, 255, 140);

	public static readonly Color Cyan = Color.FromArgb(0, 220, 255);

	public static readonly Color WarnBg = Color.FromArgb(42, 34, 24);

	public static readonly Color Warn = Color.FromArgb(242, 210, 176);

	public static readonly Color OkBg = Color.FromArgb(26, 42, 34);

	public static readonly Color Ok = Color.FromArgb(159, 227, 185);

	public static readonly Color BadBg = Color.FromArgb(52, 28, 28);

	public static readonly Color Bad = Color.FromArgb(255, 154, 146);

	public static readonly Font Body = new Font("Segoe UI", 9f);

	public static readonly Font Small = new Font("Segoe UI", 8f);

	public static readonly Font Header = new Font("Segoe UI", 7.5f, FontStyle.Bold);

	public static readonly Font Strong = new Font("Segoe UI Semibold", 9f);

	public static readonly Font Big = new Font("Segoe UI", 11f, FontStyle.Bold);
}
