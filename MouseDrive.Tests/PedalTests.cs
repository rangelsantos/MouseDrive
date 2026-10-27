using System;
using MouseDrive;

internal static class PedalTests
{
	public static void Run(Action<string, Action> check)
	{
		check("Pedal starts at its minimum and ramps to the maximum in the press time", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 20, Maximum = 80, PressMs = 1500 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			Near(pedal.Value, 0.2);
			pedal.Update(settings, 0.5);
			Near(pedal.Value, 0.4);
			pedal.Update(settings, 10);
			Near(pedal.Value, 0.8);
		});
		check("Pedal release ramps all the way to zero, below its starting minimum", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 40, ReleaseMs = 5000 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			pedal.SetHeld(false, settings);
			pedal.Update(settings, 1);
			Near(pedal.Value, 0.2);
			pedal.Update(settings, 10);
			Near(pedal.Value, 0);
		});
		check("Pedal rates are independent of timer frequency", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, PressMs = 2000 };
			PedalState fast = new PedalState();
			PedalState slow = new PedalState();
			fast.SetHeld(true, settings);
			slow.SetHeld(true, settings);
			for (int i = 0; i < 100; i++) fast.Update(settings, 0.01);
			for (int i = 0; i < 20; i++) slow.Update(settings, 0.05);
			Near(fast.Value, 0.5);
			Near(fast.Value, slow.Value);
		});
		check("Repressing a releasing pedal never drops its current force", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 10, PressMs = 1800, ReleaseMs = 10000 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			pedal.Update(settings, 1);
			pedal.SetHeld(false, settings);
			pedal.Update(settings, 0.5);
			pedal.SetHeld(true, settings);
			Near(pedal.Value, 0.55);
		});
		check("Changing the force range keeps the press and release times", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 50, Maximum = 60, PressMs = 1000, ReleaseMs = 600 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			pedal.Update(settings, 0.5);
			Near(pedal.Value, 0.55);
			pedal.Update(settings, 0.5);
			Near(pedal.Value, 0.6);
			pedal.SetHeld(false, settings);
			pedal.Update(settings, 0.3);
			Near(pedal.Value, 0.3);
		});
		check("Older rates become times, and an empty range keeps the default time", () =>
		{
			PedalSettings settings = new PedalSettings { Minimum = 20, Maximum = 80, ApplyRate = 40, ReleaseRate = 200 };
			settings.Upgrade();
			if (settings.PressMs != 1500 || settings.ReleaseMs != 400 || settings.ApplyRate != null || settings.ReleaseRate != null) throw new Exception("Rates not converted");
			PedalSettings flat = new PedalSettings { Minimum = 70, Maximum = 70, ApplyRate = 1 };
			flat.Upgrade();
			if (flat.PressMs != new PedalSettings().PressMs) throw new Exception("Empty range got a time");
			PedalSettings slow = new PedalSettings { ApplyRate = 1, ReleaseRate = 1000 };
			slow.Upgrade();
			if (slow.PressMs != PedalSettings.MaxTimeMs || slow.ReleaseMs != 100) throw new Exception("Converted time out of range");
		});
		check("Disabling and resetting a pedal clear force and held state", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 30 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			settings.Enabled = false;
			pedal.Update(settings, 0.1);
			Near(pedal.Value, 0);
			if (pedal.Held) throw new Exception("Disabled pedal stayed held");
			settings.Enabled = true;
			pedal.SetHeld(true, settings);
			pedal.Reset();
			Near(pedal.Value, 0);
			if (pedal.Held) throw new Exception("Reset pedal stayed held");
		});
	}

	private static void Near(double actual, double expected)
	{
		if (Math.Abs(actual - expected) > 0.000001) throw new Exception($"Expected {expected}, got {actual}");
	}
}
