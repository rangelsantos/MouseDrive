using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using MouseDrive;

internal static class HudTests
{
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	public static void Run(Action<string, Action> check)
	{
		check("Settings from before the new HUD styles load as Classic and Minimal", () =>
		{
			Require(AppSettings.Parse("{\"HudStyle\":\"Wheel\",\"HudOpacity\":70}") is { HudStyle: HudStyle.Classic, HudOpacity: 70 });
			Require(AppSettings.Parse("{\"HudStyle\":\"Bar\"}").HudStyle == HudStyle.Minimal);
			AppSettings old = AppSettings.Parse("{\"HudStyle\":\"Wheel\"}");
			Require(old.HudWheel == HudWheel.Classic && !old.HudZoom && old.HudBackground == 100);
			Require(old.HudThrottle == HudElementMode.Auto && old.HudBrake == HudElementMode.Auto && old.HudClutch == HudElementMode.Auto);
			Require(AppSettings.Parse("{\"HudBackground\":150}").HudBackground == 100);
		});
		check("HUD settings round-trip and the derived look isn't written", () =>
		{
			AppSettings s = new AppSettings { HudStyle = HudStyle.Column, HudWheel = HudWheel.Gt, HudZoom = true, HudBackground = 0, HudClutch = HudElementMode.Hide };
			string json = JsonSerializer.Serialize(s, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
			Require(!json.Contains("\"Look\""));
			AppSettings back = AppSettings.Parse(json);
			Require(back.HudStyle == HudStyle.Column && back.HudWheel == HudWheel.Gt && back.HudZoom && back.HudBackground == 0 && back.HudClutch == HudElementMode.Hide);
		});
		check("Auto shows a pedal on the HUD only when the profile uses it", () =>
		{
			PedalSettings on = new PedalSettings { Enabled = true, Vk = (int)Keys.W };
			Require(MainForm.PedalOnHud(HudElementMode.Auto, on));
			Require(!MainForm.PedalOnHud(HudElementMode.Auto, new PedalSettings { Enabled = false, Vk = (int)Keys.W }));
			Require(!MainForm.PedalOnHud(HudElementMode.Auto, new PedalSettings { Enabled = true }));
			Require(MainForm.PedalOnHud(HudElementMode.Show, new PedalSettings()));
			Require(!MainForm.PedalOnHud(HudElementMode.Hide, on));
		});
		check("Every HUD style draws with and without panel, zoom, wheel and pedals", () =>
		{
			foreach (HudStyle style in Enum.GetValues<HudStyle>())
				foreach (HudWheel wheel in Enum.GetValues<HudWheel>())
					foreach (bool zoom in new[] { false, true })
						foreach (int background in new[] { 0, 100 })
							foreach (bool pedals in new[] { false, true })
							{
								HudLook look = new HudLook(style, wheel, zoom, true, background);
								HudState state = new HudState { Steering = -1, Active = pedals, Throttle = 0.7, Brake = 1, ShowThrottle = pedals, ShowBrake = pedals, ShowClutch = pedals, Profile = "GT3" };
								Require(Painted(look, state) > 0);
							}
			Require(HudRenderer.LogicalSize(new HudLook(HudStyle.Column, HudWheel.Classic, true, true, 100)) == HudRenderer.LogicalSize(new HudLook(HudStyle.Column, HudWheel.Classic, false, true, 100)));
		});
		check("A realistic wheel frame draws fast enough for the HUD loop", () =>
		{
			HudLook look = new HudLook(HudStyle.Full, HudWheel.Gt, false, true, 100);
			HudState state = new HudState { Steering = 0.3, Active = true, Throttle = 0.6, ShowThrottle = true, ShowBrake = true };
			Painted(look, state);
			Stopwatch watch = Stopwatch.StartNew();
			const int Frames = 40;
			for (int i = 0; i < Frames; i++)
			{
				state.Steering = (double)i / Frames;
				DrawOnly(look, state, 1.5f);
			}
			double ms = watch.Elapsed.TotalMilliseconds / Frames;
			Console.WriteLine($"     ({ms:0.0} ms per HUD frame at 150%)");
			// The overlay redraws at up to about 21 Hz on the UI thread, which also runs the keyboard hook.
			Require(ms < 8);
		});
	}

	public static void RunForm(MainForm form, Action<string, Action> check)
	{
		check("The HUD state follows the pedal visibility settings and the profile", () =>
		{
			AppSettings settings = (AppSettings)typeof(MainForm).GetField("settings", Private).GetValue(form);
			HudElementMode saved = settings.HudClutch;
			try
			{
				typeof(MainForm).GetMethod("UseProfile", Private).Invoke(form, new object[] { new Profile
				{
					Name = "HUD regression",
					Throttle = new PedalSettings { Enabled = true, Vk = (int)Keys.W },
					Clutch = new PedalSettings { Enabled = false, Vk = (int)Keys.C }
				} });
				settings.HudClutch = HudElementMode.Auto;
				HudState st = State(form);
				Require(st.ShowThrottle && !st.ShowBrake && !st.ShowClutch && st.Profile == "HUD regression");
				settings.HudClutch = HudElementMode.Show;
				Require(State(form).ShowClutch);
			}
			finally { settings.HudClutch = saved; }
		});
	}

	private static HudState State(MainForm form) => (HudState)typeof(MainForm).GetMethod("CurrentHudState", Private).Invoke(form, null);

	private static void DrawOnly(HudLook look, HudState state, float scale)
	{
		Size size = HudRenderer.LogicalSize(look);
		using Bitmap bmp = new Bitmap((int)(size.Width * scale) + 1, (int)(size.Height * scale) + 1, PixelFormat.Format32bppArgb);
		using Graphics g = Graphics.FromImage(bmp);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.ScaleTransform(scale, scale);
		HudRenderer.Draw(g, look, state);
	}

	// Draws the HUD into a transparent bitmap and returns how many pixels it covered.
	private static int Painted(HudLook look, HudState state, float scale = 1f)
	{
		Size size = HudRenderer.LogicalSize(look);
		using Bitmap bmp = new Bitmap((int)(size.Width * scale) + 1, (int)(size.Height * scale) + 1, PixelFormat.Format32bppArgb);
		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.ScaleTransform(scale, scale);
			HudRenderer.Draw(g, look, state);
		}
		int count = 0;
		for (int y = 0; y < bmp.Height; y += 4)
			for (int x = 0; x < bmp.Width; x += 4)
				if (bmp.GetPixel(x, y).A > 0) count++;
		return count;
	}

	private static void Require(bool condition) { if (!condition) throw new Exception("Unexpected HUD result"); }
}
