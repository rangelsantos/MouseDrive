using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseDrive;

// The click-through HUD. It is a per-pixel-alpha layered window (UpdateLayeredWindow), so the panel is truly
// translucent: the old TransparencyKey approach blended the panel with the magenta key and tinted it purple.
public class OverlayForm : Form
{
	private struct POINT
	{
		public int X;

		public int Y;
	}

	private struct SIZE
	{
		public int Cx;

		public int Cy;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct BLENDFUNCTION
	{
		public byte BlendOp;

		public byte BlendFlags;

		public byte SourceConstantAlpha;

		public byte AlphaFormat;
	}

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TOPMOST = 0x8;

	private const int WS_EX_TRANSPARENT = 0x20;

	private const int WS_EX_TOOLWINDOW = 0x80;

	private const int WS_EX_LAYERED = 0x80000;

	private const int WS_EX_NOACTIVATE = 0x8000000;

	private const int ULW_ALPHA = 2;

	private const byte AC_SRC_ALPHA = 1;

	public const int MinSizePercent = 50;

	public const int MaxSizePercent = 250;

	// Logical height of the "Drag to move · Done" bar shown under the HUD while positioning it.
	private const int MoveBarHeight = 34;

	private const int MoveMinWidth = 170;

	private const int ToastMs = 1200;

	// With "Fade when straight", the HUD dims to this share of its opacity while the wheel is near center.
	public const double FadedAlpha = 0.35;

	private HudState state;

	private HudStyle style;

	private bool numbers = true;

	private int opacity = 90;

	private bool fadeEnabled;

	private double fade = 1.0;

	private string toast;

	private long toastUntil;

	private bool movable;

	private bool dragging;

	private Point dragStart;

	private RectangleF doneRect;

	private int sizePercent = 100;

	private string lastFrame;

	private float HudScale => (float)sizePercent / 100f;

	private static string PosPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlay.cfg");

	public bool ToastActive => toast != null && Environment.TickCount64 < toastUntil;

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
			return createParams;
		}
	}

	public event Action DoneClicked;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hWnd, nint hDC);

	[DllImport("gdi32.dll")]
	private static extern nint CreateCompatibleDC(nint hDC);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll")]
	private static extern nint SelectObject(nint hDC, nint hObject);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(nint hObject);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UpdateLayeredWindow(nint hwnd, nint hdcDst, ref POINT pptDst, ref SIZE psize, nint hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

	// The style comes in here so the first size is already right: the saved position is the top-left
	// corner, and resizing afterwards (which keeps the center) would shift the HUD on every start.
	public OverlayForm(HudStyle style, bool numbers, HudState state)
	{
		this.style = style;
		this.numbers = numbers;
		this.state = state;
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		TopMost = true;
		StartPosition = FormStartPosition.Manual;
		sizePercent = LoadSizePercent();
		Size = ScaledSize();
		Location = LoadPosition() ?? DefaultLocation();
	}

	private Size HudLogicalSize => HudRenderer.LogicalSize(style, numbers);

	public static bool Straight(HudState s)
	{
		return Math.Abs(s.Steering) < 0.04;
	}

	private Size LogicalSize
	{
		get
		{
			Size s = HudLogicalSize;
			if (!movable)
			{
				return s;
			}
			return new Size(Math.Max(s.Width, MoveMinWidth), s.Height + MoveBarHeight);
		}
	}

	private Size ScaledSize()
	{
		Size s = LogicalSize;
		return new Size((int)Math.Ceiling((float)s.Width * HudScale), (int)Math.Ceiling((float)s.Height * HudScale));
	}

	private Point DefaultLocation()
	{
		Rectangle scr = Screen.PrimaryScreen.Bounds;
		return new Point(scr.Left + (scr.Width - Width) / 2, scr.Top + 40);
	}

	// overlay.cfg is "x y [size%]"; files written before the size setting have only the position.
	private static string[] ReadConfig()
	{
		try
		{
			if (File.Exists(PosPath))
			{
				return File.ReadAllText(PosPath).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			}
		}
		catch
		{
		}
		return new string[0];
	}

	public static int LoadSizePercent()
	{
		string[] p = ReadConfig();
		if (p.Length >= 3 && int.TryParse(p[2], out var pct))
		{
			return Math.Min(Math.Max(pct, MinSizePercent), MaxSizePercent);
		}
		return 100;
	}

	private Point? LoadPosition()
	{
		string[] p = ReadConfig();
		if (p.Length >= 2 && int.TryParse(p[0], out var x) && int.TryParse(p[1], out var y))
		{
			Point pt = new Point(x, y);
			Screen[] allScreens = Screen.AllScreens;
			for (int i = 0; i < allScreens.Length; i++)
			{
				if (allScreens[i].Bounds.IntersectsWith(new Rectangle(pt, Size)))
				{
					return pt;
				}
			}
		}
		return null;
	}

	private void SaveConfig()
	{
		try
		{
			File.WriteAllText(PosPath, $"{Location.X} {Location.Y} {sizePercent}");
		}
		catch
		{
		}
	}

	// Applies a new size, either around the current center (size, style) or from the top-left corner
	// (entering/leaving move mode, so the HUD doesn't jump), kept inside that screen.
	private void Relayout(bool keepCenter)
	{
		Size sz = ScaledSize();
		Point anchor = (keepCenter ? new Point(Location.X + Width / 2, Location.Y + Height / 2) : Location);
		Rectangle scr = Screen.FromPoint(anchor).Bounds;
		int x = (keepCenter ? (anchor.X - sz.Width / 2) : anchor.X);
		int y = (keepCenter ? (anchor.Y - sz.Height / 2) : anchor.Y);
		x = Math.Min(Math.Max(x, scr.Left), Math.Max(scr.Right - sz.Width, scr.Left));
		y = Math.Min(Math.Max(y, scr.Top), Math.Max(scr.Bottom - sz.Height, scr.Top));
		Bounds = new Rectangle(x, y, sz.Width, sz.Height);
		Render(force: true);
	}

	public void SetSizePercent(int percent)
	{
		percent = Math.Min(Math.Max(percent, MinSizePercent), MaxSizePercent);
		if (percent != sizePercent)
		{
			sizePercent = percent;
			Relayout(keepCenter: true);
			SaveConfig();
		}
	}

	public void SetStyle(HudStyle style, bool numbers)
	{
		if (style != this.style || numbers != this.numbers)
		{
			this.style = style;
			this.numbers = numbers;
			Relayout(keepCenter: true);
		}
	}

	public void SetOpacityPercent(int percent)
	{
		opacity = Math.Min(Math.Max(percent, 10), 100);
		Render(force: true);
	}

	public void SetFade(bool on)
	{
		fadeEnabled = on;
		if (!on)
		{
			fade = 1.0;
		}
		Render(force: true);
	}

	public void ResetPosition()
	{
		Location = DefaultLocation();
		SaveConfig();
		Render(force: true);
	}

	public void SetMovable(bool on)
	{
		if (on == movable)
		{
			return;
		}
		movable = on;
		dragging = false;
		if (IsHandleCreated)
		{
			int ex = GetWindowLong(Handle, GWL_EXSTYLE);
			ex = ((!on) ? (ex | WS_EX_TRANSPARENT) : (ex & ~WS_EX_TRANSPARENT));
			SetWindowLong(Handle, GWL_EXSTYLE, ex);
		}
		Relayout(keepCenter: false);
		if (!on)
		{
			SaveConfig();
		}
	}

	public void Toast(string text)
	{
		toast = text;
		toastUntil = Environment.TickCount64 + ToastMs;
		Render(force: true);
	}

	public void UpdateValues(HudState s)
	{
		state = s;
		double target = ((fadeEnabled && s.Active && Straight(s) && !movable) ? FadedAlpha : 1.0);
		fade += (target - fade) * 0.25;
		if (Math.Abs(target - fade) < 0.01)
		{
			fade = target;
		}
		Render(force: false);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		if (Visible)
		{
			Render(force: true);
		}
	}

	private string FrameKey()
	{
		HudState s = state;
		return string.Format(CultureInfo.InvariantCulture, "{0:F3}|{1}|{2:F2}|{3}|{4}", s.Steering, s.Active, fade, ToastActive ? toast : "", s.ToggleKey);
	}

	private void Render(bool force)
	{
		if (!IsHandleCreated || !Visible)
		{
			return;
		}
		string key = FrameKey();
		if (!force && key == lastFrame)
		{
			return;
		}
		lastFrame = key;
		Size ls = LogicalSize;
		Size hs = HudLogicalSize;
		Size px = ScaledSize();
		using Bitmap bmp = new Bitmap(px.Width, px.Height, PixelFormat.Format32bppArgb);
		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			g.Clear(Color.Transparent);
			g.ScaleTransform(HudScale, HudScale);
			if (movable)
			{
				DrawMoveFrame(g, ls, hs);
			}
			HudRenderer.Draw(g, style, state, numbers);
			if (ToastActive)
			{
				HudRenderer.DrawToast(g, toast, hs);
			}
		}
		byte alpha = (byte)(movable ? 255 : ((int)Math.Round(255.0 * (double)opacity / 100.0 * fade)));
		Push(bmp, alpha);
	}

	// While positioning: a translucent fill (so the whole HUD can be grabbed), a dashed outline and a Done button.
	private void DrawMoveFrame(Graphics g, Size ls, Size hs)
	{
		using (GraphicsPath p = HudRenderer.Rounded(new RectangleF(1f, 1f, hs.Width - 2, hs.Height - 2), 12f))
		{
			using SolidBrush fill = new SolidBrush(Color.FromArgb(110, 10, 13, 19));
			using Pen edge = new Pen(Color.FromArgb(255, 90, 200, 255), 2f)
			{
				DashStyle = DashStyle.Dash
			};
			g.FillPath(fill, p);
			g.DrawPath(edge, p);
		}
		using (GraphicsPath bar = HudRenderer.Rounded(new RectangleF(0f, hs.Height + 4, ls.Width, MoveBarHeight - 4), 8f))
		{
			using SolidBrush bb = new SolidBrush(Color.FromArgb(230, 22, 24, 30));
			g.FillPath(bb, bar);
		}
		using Font f = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
		using Font fb = new Font("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
		using (SolidBrush tb = new SolidBrush(Color.FromArgb(220, 220, 228)))
		{
			g.DrawString("Drag to move", f, tb, 10f, (float)hs.Height + 12f);
		}
		doneRect = new RectangleF(ls.Width - 62, hs.Height + 9, 54f, MoveBarHeight - 14);
		using (GraphicsPath db = HudRenderer.Rounded(doneRect, 4f))
		{
			using SolidBrush b = new SolidBrush(Theme.Blue);
			g.FillPath(b, db);
		}
		using SolidBrush wb = new SolidBrush(Color.White);
		using StringFormat center = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};
		g.DrawString("Done", fb, wb, doneRect, center);
	}

	private void Push(Bitmap bmp, byte alpha)
	{
		nint screen = GetDC(IntPtr.Zero);
		nint mem = CreateCompatibleDC(screen);
		nint hbmp = bmp.GetHbitmap(Color.FromArgb(0));
		nint old = SelectObject(mem, hbmp);
		try
		{
			POINT dst = new POINT
			{
				X = Left,
				Y = Top
			};
			SIZE size = new SIZE
			{
				Cx = bmp.Width,
				Cy = bmp.Height
			};
			POINT src = default(POINT);
			BLENDFUNCTION blend = new BLENDFUNCTION
			{
				SourceConstantAlpha = alpha,
				AlphaFormat = AC_SRC_ALPHA
			};
			UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref blend, ULW_ALPHA);
		}
		finally
		{
			SelectObject(mem, old);
			DeleteObject(hbmp);
			DeleteDC(mem);
			ReleaseDC(IntPtr.Zero, screen);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (movable && e.Button == MouseButtons.Left)
		{
			PointF p = new PointF((float)e.X / HudScale, (float)e.Y / HudScale);
			if (doneRect.Contains(p))
			{
				this.DoneClicked?.Invoke();
			}
			else
			{
				dragging = true;
				dragStart = e.Location;
			}
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (dragging)
		{
			dragging = false;
			SaveConfig();
		}
		base.OnMouseUp(e);
	}
}
