using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MouseDrive;

internal static class PedalIntegrationTests
{
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	public static void Run(MainForm form, Action<string, Action> check)
	{
		check("Pedal keys preview in standby with released vJoy output", () =>
		{
			Use(form);
			Call(form, "OnKeyDown", (int)Keys.W);
			Call(form, "StepPedals", 0.5);
			Require(Math.Abs(States(form)[0].Value - 0.4) < 0.000001);
			Require(VJoy.Axes[VJoy.AxisY] == VJoy.ToAxis(-1));
			Call(form, "OnKeyUp", (int)Keys.W);
			Call(form, "StepPedals", 0.5);
			Require(States(form)[0].Value == 0);
		});
		check("Pedals send separate axes and support simultaneous keys", () =>
		{
			Use(form);
			Call(form, "OnKeyDown", (int)Keys.W);
			Call(form, "OnKeyDown", (int)Keys.S);
			Call(form, "OnKeyDown", (int)Keys.C);
			Call(form, "StepPedals", 0.5);
			Call(form, "WritePedalAxes", true);
			Require(VJoy.Axes[VJoy.AxisY] == VJoy.ToAxis(-0.2));
			Require(VJoy.Axes[VJoy.AxisZ] == VJoy.ToAxis(0));
			Require(VJoy.Axes[VJoy.AxisRz] == VJoy.ToAxis(0));
		});
		check("Inverted pedal idle and full force have opposite endpoints", () =>
		{
			Use(form);
			Field<Profile>(form, "profile").Throttle.Inverted = true;
			Call(form, "ResetPedals");
			Require(VJoy.Axes[VJoy.AxisY] == VJoy.ToAxis(1));
			Field<Profile>(form, "profile").Throttle.Maximum = 100;
			Call(form, "OnKeyDown", (int)Keys.W);
			Call(form, "StepPedals", 5.0);
			Call(form, "WritePedalAxes", true);
			Require(VJoy.Axes[VJoy.AxisY] == VJoy.ToAxis(-1));
		});
		check("Profile changes release all pedal inputs", () =>
		{
			Use(form);
			Call(form, "OnKeyDown", (int)Keys.W);
			Call(form, "StepPedals", 0.5);
			Use(form);
			Require(States(form).All(p => p.Value == 0 && !p.Held));
			Require(VJoy.Axes[VJoy.AxisY] == VJoy.ToAxis(-1));
		});
		check("Commands take priority over a pedal using the same key", () =>
		{
			Use(form);
			Field<Profile>(form, "profile").Throttle.Vk = (int)Keys.Home;
			Call(form, "OnKeyDown", (int)Keys.Home);
			Call(form, "StepPedals", 0.5);
			Require(!States(form)[0].Held && States(form)[0].Value == 0);
		});
		check("Pedal rebind clears held force and Backspace unassigns the key", () =>
		{
			Use(form);
			Call(form, "OnKeyDown", (int)Keys.W);
			Call(form, "BeginPedalCapture");
			Call(form, "OnKeyDown", (int)Keys.Back);
			Application.DoEvents();
			Require(Field<Profile>(form, "profile").Throttle.Vk == 0);
			Require(States(form)[0].Value == 0 && !States(form)[0].Held);
		});
		check("Escape cancels pedal capture without changing the key", () =>
		{
			Use(form);
			Call(form, "BeginPedalCapture");
			Call(form, "OnKeyDown", (int)Keys.Escape);
			Application.DoEvents();
			Require(Field<Profile>(form, "profile").Throttle.Vk == (int)Keys.W);
		});
		check("Force range never lets the initial force pass the maximum", () =>
		{
			Use(form);
			RangeSlider force = Field<RangeSlider>(form, "pedalForce");
			force.Low = 95;
			Require(Field<Profile>(form, "profile").Throttle.Minimum == 80 && Field<Profile>(form, "profile").Throttle.Maximum == 80);
			force.High = 15;
			Require(Field<Profile>(form, "profile").Throttle.Maximum == 80);
			force.SetRange(10, 90);
			Require(Field<Profile>(form, "profile").Throttle.Minimum == 10 && Field<Profile>(form, "profile").Throttle.Maximum == 90);
			Require(Field<Profile>(form, "profile").Brake.Minimum == 0);
		});
		check("Picking a pedal loads its settings and time sliders write the selected pedal", () =>
		{
			Use(form);
			Field<PedalPicker>(form, "pedalPicker").Selected = 1;
			Require(Field<int>(form, "selectedPedal") == 1);
			Require(Field<Label>(form, "pedalTitle").Text == "BRAKE · VJOY Z");
			Slider press = Field<Slider>(form, "pedalPress");
			press.Value = 0;
			Require(Field<Profile>(form, "profile").Brake.PressMs == PedalSettings.MinTimeMs);
			Require(Field<Profile>(form, "profile").Throttle.PressMs == 1500);
			press.Value = press.Maximum;
			Require(Field<Profile>(form, "profile").Brake.PressMs == PedalSettings.MaxTimeMs);
			Field<PedalPicker>(form, "pedalPicker").Selected = 0;
			Require(Field<Label>(form, "pedalTitle").Text == "THROTTLE · VJOY Y");
		});
		check("Unavailable pedal axes are skipped with a setup warning", () =>
		{
			Use(form);
			VJoy.UnavailableAxes.Add(VJoy.AxisY);
			try
			{
				VJoy.Axes.Clear();
				Call(form, "AcquirePedalAxes");
				Call(form, "OnKeyDown", (int)Keys.W);
				Call(form, "StepPedals", 0.5);
				Call(form, "WritePedalAxes", true);
				Require(!VJoy.Axes.ContainsKey(VJoy.AxisY));
				Require(Field<Label>(form, "pedalWarning").Text.Contains("Configure vJoy"));
			}
			finally { VJoy.UnavailableAxes.Clear(); Call(form, "AcquirePedalAxes"); }
		});
		check("Pedal settings round-trip through profile export and import", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "pedals.json");
			try
			{
				Profile p = new Profile { Throttle = new PedalSettings { Enabled = true, Vk = (int)Keys.W, Minimum = 15, Maximum = 85, PressMs = 750, ReleaseMs = 250, Inverted = true } };
				Require(ProfileManager.Export(p, path));
				PedalSettings result = ProfileManager.Import(path).Throttle;
				Require(result.Enabled && result.Vk == (int)Keys.W && result.Minimum == 15 && result.Maximum == 85 && result.PressMs == 750 && result.ReleaseMs == 250 && result.Inverted);
					Require(!File.ReadAllText(path).Contains("Rate"));
				Require(ProfileManager.Clone(p, "Copy").Throttle.Minimum == 15);
			}
			finally { File.Delete(path); }
		});
		check("Older and null pedal settings load safely disabled", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "pedals.json");
			try
			{
				foreach (string json in new[] { "{}", "{\"Throttle\":null,\"Brake\":null,\"Clutch\":null}" })
				{
					File.WriteAllText(path, json);
					Profile result = ProfileManager.Import(path);
					Require(!result.Throttle.Enabled && !result.Brake.Enabled && !result.Clutch.Enabled && result.Throttle.Vk == 0);
				}
			}
			finally { File.Delete(path); }
		});
		check("Profiles with older rates load as times and save without the rates", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "pedals.json");
			try
			{
				File.WriteAllText(path, "{\"Throttle\":{\"Enabled\":true,\"Minimum\":20,\"Maximum\":80,\"ApplyRate\":40,\"ReleaseRate\":200}}");
				Profile result = ProfileManager.Import(path);
				Require(result.Throttle.PressMs == 1500 && result.Throttle.ReleaseMs == 400 && result.Brake.PressMs == 1000);
				Require(ProfileManager.Export(result, path) && !File.ReadAllText(path).Contains("Rate"));
			}
			finally { File.Delete(path); }
		});
		check("Invalid pedal bounds, rates and times reject the profile", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "pedals.json");
			try
			{
				foreach (string json in new[] { "{\"Throttle\":{\"Minimum\":101}}", "{\"Brake\":{\"Minimum\":90,\"Maximum\":10}}", "{\"Clutch\":{\"ApplyRate\":0}}", "{\"Throttle\":{\"ReleaseRate\":1001}}", "{\"Brake\":{\"PressMs\":10}}", "{\"Clutch\":{\"ReleaseMs\":10001}}" })
				{
					File.WriteAllText(path, json);
					Require(ProfileManager.Import(path) == null);
				}
			}
			finally { File.Delete(path); }
		});
	}

	private static void Use(MainForm form)
	{
		Call(form, "UseProfile", new Profile
		{
			Name = "Pedal regression",
			Throttle = new PedalSettings { Enabled = true, Vk = (int)Keys.W, Minimum = 20, Maximum = 80, PressMs = 1500 },
			Brake = new PedalSettings { Enabled = true, Vk = (int)Keys.S },
			Clutch = new PedalSettings { Enabled = true, Vk = (int)Keys.C }
		});
		Call(form, "SelectTab", 2);
	}
	private static PedalState[] States(MainForm form) => Field<PedalState[]>(form, "pedals");
	private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Private).GetValue(form);
	private static void Call(MainForm form, string name, params object[] args) => typeof(MainForm).GetMethod(name, Private).Invoke(form, args);
	private static void Require(bool condition) { if (!condition) throw new Exception("Unexpected pedal result"); }
}
