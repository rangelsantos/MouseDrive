using System;

namespace MouseDrive;

internal sealed class PedalState
{
	public double Value { get; private set; } = 0;
	public bool Held { get; private set; } = false;

	public void SetHeld(bool held, PedalSettings settings)
	{
		if (!settings.Enabled)
		{
			Reset();
			return;
		}
		if (held && !Held)
		{
			Value = Math.Clamp(Math.Max(Value, settings.Minimum / 100.0), 0, settings.Maximum / 100.0);
		}
		Held = held;
	}

	public void Update(PedalSettings settings, double dt)
	{
		if (!settings.Enabled)
		{
			Reset();
			return;
		}
		dt = Math.Max(0, dt);
		Value = Held
			? Math.Min(settings.Maximum / 100.0, Value + settings.ApplyRate / 100.0 * dt)
			: Math.Max(0, Value - settings.ReleaseRate / 100.0 * dt);
	}

	public void Reset()
	{
		Value = 0;
		Held = false;
	}
}
