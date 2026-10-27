using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MouseDrive;

internal static class Program
{
	private static int failures;
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	[STAThread]
	private static int Main()
	{
		Application.EnableVisualStyles();
		PedalTests.Run(Check);
		HudTests.Run(Check);
		Check("Reject a profile containing a null binding", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "invalid.json");
			try
			{
				File.WriteAllText(path, "{\"Buttons\":[null]}");
				Require(ProfileManager.Import(path) == null);
			}
			finally { File.Delete(path); }
		});
		using MainForm form = new MainForm();
		form.Show();
		Field<Timer>(form, "loop").Stop();
		Check("Keep a shared button down until its final key is released", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A }, new ButtonBinding { Vk = (int)Keys.B });
			Call(form, "OnKeyDown", (int)Keys.A);
			Call(form, "OnKeyDown", (int)Keys.B);
			Call(form, "OnKeyUp", (int)Keys.A);
			Require(VJoy.Buttons[1]);
			Call(form, "OnKeyUp", (int)Keys.B);
			Require(!VJoy.Buttons[1]);
		});
		Check("Rebinding a held key releases its previous output", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A });
			Call(form, "OnKeyDown", (int)Keys.A);
			Edit(form);
			Capture(form, Keys.B);
			Call(form, "OnKeyUp", (int)Keys.A);
			Require(!VJoy.Buttons[1]);
		});
		Check("Changing profile cancels pending Add key capture", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Use(form);
			Require(Field<Action<int>>(form, "capture") == null);
		});
		Check("Changing profile discards an already queued capture", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Call(form, "OnKeyDown", (int)Keys.A);
			Use(form);
			Application.DoEvents();
			Require(Field<Profile>(form, "profile").Buttons.Count == 0);
		});
		Check("Leaving Keys cancels pending capture", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Call(form, "SelectTab", 0);
			Require(Field<Action<int>>(form, "capture") == null);
		});
		Check("Switching Keys lists cancels pending capture", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Call(form, "SelectKeyList", 0);
			Require(Field<Action<int>>(form, "capture") == null);
		});
		Check("Deleting one shared binding preserves the other held output", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A }, new ButtonBinding { Vk = (int)Keys.B });
			Call(form, "OnKeyDown", (int)Keys.A);
			Call(form, "OnKeyDown", (int)Keys.B);
			Find(form).OfType<Button>().First(b => b.AccessibleName == "Remove this button").PerformClick();
			Require(VJoy.Buttons[1]);
			Call(form, "OnKeyUp", (int)Keys.B);
			Require(!VJoy.Buttons[1]);
		});
		Check("Renumbering one shared binding preserves the other held output", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A }, new ButtonBinding { Vk = (int)Keys.B });
			Call(form, "OnKeyDown", (int)Keys.A);
			Call(form, "OnKeyDown", (int)Keys.B);
			Find(form).OfType<Stepper>().First().Value = 2;
			Require(VJoy.Buttons[1]);
			Call(form, "OnKeyUp", (int)Keys.B);
			Require(!VJoy.Buttons[1]);
		});
		Check("Add key chooses a free output after deletion", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A, Button = 2 });
			Call(form, "BeginAddBinding");
			Capture(form, Keys.B);
			Require(Field<Profile>(form, "profile").Buttons.Last().Button == 1);
		});
		Check("Escape cancels adding a binding", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Capture(form, Keys.Escape);
			Require(Field<Profile>(form, "profile").Buttons.Count == 0);
		});
		Check("Adding at capacity does not silently share the last output", () =>
		{
			Use(form, Enumerable.Range(1, 16).Select(n => new ButtonBinding { Vk = (int)Keys.A, Button = n }).ToArray());
			Call(form, "BeginAddBinding");
			Capture(form, Keys.B);
			Require(Field<Profile>(form, "profile").Buttons.Count == 16);
		});
		Check("Backspace does not add an empty binding", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Capture(form, Keys.Back);
			Require(Field<Profile>(form, "profile").Buttons.Count == 0);
		});
		Check("Escape preserves a binding being edited", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A });
			Edit(form);
			Capture(form, Keys.Escape);
			Require(Field<Profile>(form, "profile").Buttons[0].Vk == (int)Keys.A);
		});
		Check("Backspace clears a binding and releases its output", () =>
		{
			Use(form, new ButtonBinding { Vk = (int)Keys.A });
			Call(form, "OnKeyDown", (int)Keys.A);
			Edit(form);
			Capture(form, Keys.Back);
			Require(Field<Profile>(form, "profile").Buttons[0].Vk == 0 && !VJoy.Buttons[1]);
		});
		Check("A captured key reaches a binding through the queued callback", () =>
		{
			Use(form);
			Call(form, "BeginAddBinding");
			Call(form, "OnKeyDown", (int)Keys.B);
			Application.DoEvents();
			Require(Field<Profile>(form, "profile").Buttons.Single().Vk == (int)Keys.B);
		});
		Check("Older profiles without bindings retain defaults", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "legacy.json");
			try
			{
				File.WriteAllText(path, "{\"Sensitivity\":65}");
				Profile profile = ProfileManager.Import(path);
				Require(profile != null && profile.Sensitivity == 65 && profile.Buttons.Count == 0 && profile.ToggleVk == (int)Keys.F9);
				File.WriteAllText(path, "{\"Buttons\":null}");
				Require(ProfileManager.Import(path).Buttons.Count == 0);
			}
			finally { File.Delete(path); }
		});
		Check("Reject bindings outside the native parameter ranges", () =>
		{
			string path = Path.Combine(AppContext.BaseDirectory, "invalid.json");
			try
			{
				foreach (string json in new[] { "{\"Buttons\":[{\"Button\":0}]}", "{\"Buttons\":[{\"Button\":256}]}", "{\"Buttons\":[{\"Vk\":-1}]}", "{\"Buttons\":[{\"Vk\":256}]}" })
				{
					File.WriteAllText(path, json);
					Require(ProfileManager.Import(path) == null);
				}
			}
			finally { File.Delete(path); }
		});
		PedalIntegrationTests.Run(form, Check);
		HudTests.RunForm(form, Check);
		Call(form, "ExitApp");
		Console.WriteLine($"Failures: {failures}");
		return (failures == 0) ? 0 : 1;
	}

	private static void Use(MainForm form, params ButtonBinding[] buttons)
	{
		Call(form, "UseProfile", new Profile { Name = "Regression", Buttons = new List<ButtonBinding>(buttons) });
		Call(form, "SelectTab", 3);
		Call(form, "SelectKeyList", 1);
	}
	private static void Edit(MainForm form) => Find(form).OfType<Button>().First(b => b.AccessibleName == "Key for vJoy button 1").PerformClick();
	private static IEnumerable<Control> Find(Control root)
	{
		foreach (Control child in root.Controls)
		{
			yield return child;
			foreach (Control descendant in Find(child)) yield return descendant;
		}
	}
	private static void Capture(MainForm form, Keys key)
	{
		Action<int> capture = Field<Action<int>>(form, "capture");
		typeof(MainForm).GetField("capture", Private).SetValue(form, null);
		capture((int)key);
	}
	private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Private).GetValue(form);
	private static void Call(MainForm form, string name, params object[] args) => typeof(MainForm).GetMethod(name, Private).Invoke(form, args);
	private static void Require(bool condition) { if (!condition) throw new Exception("Unexpected result"); }
	private static void Check(string name, Action test)
	{
		try { test(); Console.WriteLine("PASS " + name); }
		catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.GetBaseException().Message); }
	}
}
