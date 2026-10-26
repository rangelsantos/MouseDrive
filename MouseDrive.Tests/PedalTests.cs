using System;
using MouseDrive;

internal static class PedalTests
{
	public static void Run(Action<string, Action> check)
	{
		check("Pedal starts at its minimum and ramps at percent per second", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 20, Maximum = 80, ApplyRate = 40 };
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
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 40, ReleaseRate = 20 };
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
			PedalSettings settings = new PedalSettings { Enabled = true, ApplyRate = 30 };
			PedalState fast = new PedalState();
			PedalState slow = new PedalState();
			fast.SetHeld(true, settings);
			slow.SetHeld(true, settings);
			for (int i = 0; i < 100; i++) fast.Update(settings, 0.01);
			for (int i = 0; i < 20; i++) slow.Update(settings, 0.05);
			Near(fast.Value, 0.3);
			Near(fast.Value, slow.Value);
		});
		check("Repressing a releasing pedal never drops its current force", () =>
		{
			PedalSettings settings = new PedalSettings { Enabled = true, Minimum = 10, ApplyRate = 50, ReleaseRate = 10 };
			PedalState pedal = new PedalState();
			pedal.SetHeld(true, settings);
			pedal.Update(settings, 1);
			pedal.SetHeld(false, settings);
			pedal.Update(settings, 0.5);
			pedal.SetHeld(true, settings);
			Near(pedal.Value, 0.55);
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
