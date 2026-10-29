using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
		check("Every HUD style draws with and without panel, zoom, wheel, bar and pedals", () =>
		{
			foreach (HudStyle style in Enum.GetValues<HudStyle>())
				foreach (HudWheel wheel in Enum.GetValues<HudWheel>())
					foreach (bool zoom in new[] { false, true })
						foreach (int background in new[] { 0, 100 })
							foreach (bool pedals in new[] { false, true })
								foreach (bool bar in new[] { false, true })
								{
									HudLook look = new HudLook(style, wheel, zoom, true, background, bar ? 1080 : 180, bar);
									HudState state = new HudState { Steering = -1, Active = pedals, Throttle = 0.7, Brake = 1, ShowThrottle = pedals, ShowBrake = pedals, ShowClutch = pedals, Profile = "GT3" };
									Require(Painted(look, state) > 0);
								}
			Require(HudRenderer.LogicalSize(new HudLook(HudStyle.Column, HudWheel.Classic, true, true, 100)) == HudRenderer.LogicalSize(new HudLook(HudStyle.Column, HudWheel.Classic, false, true, 100)));
		});
		check("Older profiles turn the wheel 360°, and a rotation out of range is clamped", () =>
		{
			Require(new Profile().WheelRotation == 360);
			string path = Path.Combine(AppContext.BaseDirectory, "rotation.json");
			try
			{
				File.WriteAllText(path, "{}");
				Require(ProfileManager.Import(path).WheelRotation == 360);
				File.WriteAllText(path, "{\"WheelRotation\":5000}");
				Require(ProfileManager.Import(path).WheelRotation == Profile.MaxWheelRotation);
				File.WriteAllText(path, "{\"WheelRotation\":0}");
				Require(ProfileManager.Import(path).WheelRotation == Profile.MinWheelRotation);
				File.WriteAllText(path, "{\"WheelRotation\":900}");
				Require(ProfileManager.Import(path).WheelRotation == 900);
			}
			finally { File.Delete(path); }
		});
		check("Auto shows the position bar only past one full turn", () =>
		{
			Require(!MainForm.HudBarOn(HudElementMode.Auto, 180) && !MainForm.HudBarOn(HudElementMode.Auto, 360));
			Require(MainForm.HudBarOn(HudElementMode.Auto, 370) && MainForm.HudBarOn(HudElementMode.Auto, 1080));
			Require(MainForm.HudBarOn(HudElementMode.Show, 180) && !MainForm.HudBarOn(HudElementMode.Hide, 1080));
			Require(AppSettings.Parse("{}").HudPositionBar == HudElementMode.Auto);
		});
		check("The position bar makes only Classic taller", () =>
		{
			foreach (HudStyle style in Enum.GetValues<HudStyle>())
				foreach (bool zoom in new[] { false, true })
				{
					Size without = HudRenderer.LogicalSize(new HudLook(style, HudWheel.Classic, zoom, true, 100, 900, false));
					Size with = HudRenderer.LogicalSize(new HudLook(style, HudWheel.Classic, zoom, true, 100, 900, true));
					Require(with.Width == without.Width && (style == HudStyle.Classic ? with.Height > without.Height : with.Height == without.Height));
				}
		});
		check("The Drive monitor draws with every wheel, with pedals on, off and without a key", () =>
		{
			DrivePedal on = new DrivePedal("Throttle", "Y", 0.6, true, 0.4, 0.8, "W", false);
			DrivePedal noKey = new DrivePedal("Brake", "Z", 0, true, 0, 1, "", true);
			DrivePedal off = new DrivePedal("Clutch", "Rz", 0, false, 0, 1, "Mouse 4", false);
			foreach (HudWheel wheel in Enum.GetValues<HudWheel>())
				foreach (int width in new[] { 200, 420 })
				{
					using Bitmap bmp = new Bitmap(width, HudRenderer.DriveHeight, PixelFormat.Format32bppArgb);
					using (Graphics g = Graphics.FromImage(bmp))
					{
						g.Clear(Theme.Row);
						g.SmoothingMode = SmoothingMode.AntiAlias;
						HudRenderer.DrawDrive(g, new DriveView(-0.4, 0.5, true, wheel, on, noKey, off, 1080), width);
					}
					int changed = 0;
					for (int y = 0; y < bmp.Height; y += 4)
						for (int x = 0; x < bmp.Width; x += 4)
							if (bmp.GetPixel(x, y).ToArgb() != Theme.Row.ToArgb()) changed++;
					Require(changed > 0);
				}
		});
		check("A realistic wheel frame draws fast enough for the HUD loop", () =>
		{
			HudLook look = new HudLook(HudStyle.Full, HudWheel.Gt, false, true, 100, 1080, true);
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
		check("The wheel rotation follows the profile, the presets and the HUD look", () =>
		{
			AppSettings settings = (AppSettings)typeof(MainForm).GetField("settings", Private).GetValue(form);
			HudElementMode saved = settings.HudPositionBar;
			try
			{
				settings.HudPositionBar = HudElementMode.Auto;
				typeof(MainForm).GetMethod("UseProfile", Private).Invoke(form, new object[] { new Profile { Name = "Rotation regression", WheelRotation = 900 } });
				Stepper rotation = (Stepper)typeof(MainForm).GetField("numRotation", Private).GetValue(form);
				Segmented presets = (Segmented)typeof(MainForm).GetField("segRotation", Private).GetValue(form);
				Require(rotation.Value == 900 && presets.SelectedIndex == 4);
				Require(Look(form) is { Rotation: 900, Bar: true });
				rotation.Value = 910;
				Require(presets.SelectedIndex == -1 && Look(form).Rotation == 910);
				presets.SelectedIndex = 0;
				Require(rotation.Value == 180 && Look(form) is { Rotation: 180, Bar: false });
				Profile active = (Profile)typeof(MainForm).GetField("profile", Private).GetValue(form);
				Require(active.WheelRotation == 180);
			}
			finally { settings.HudPositionBar = saved; }
		});
	}

	private static HudLook Look(MainForm form) => (HudLook)typeof(MainForm).GetMethod("CurrentLook", Private).Invoke(form, null);

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
