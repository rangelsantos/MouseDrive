namespace MouseDrive;

public partial class MainForm
{
	private static readonly string[] PedalNames = { "Throttle", "Brake", "Clutch" };
	private static readonly string[] PedalAxisNames = { "Y", "Z", "Rz" };
	private static readonly uint[] PedalAxes = { VJoy.AxisY, VJoy.AxisZ, VJoy.AxisRz };
	private readonly PedalState[] pedals = { new PedalState(), new PedalState(), new PedalState() };
	private readonly bool[] pedalAxisAvailable = new bool[3];

	private PedalSettings PedalConfig(int index) => index switch
	{
		0 => profile.Throttle,
		1 => profile.Brake,
		_ => profile.Clutch
	};

	// The HUD follows the pedal as it moves, standby included, like the preview on the Pedals tab.
	private double PedalForce(int index) => PedalConfig(index).Enabled ? pedals[index].Value : 0;

	private DrivePedal DrivePedalView(int index)
	{
		PedalSettings config = PedalConfig(index);
		string key = (config.Vk != 0) ? Profile.KeyName(config.Vk) : "";
		return new DrivePedal(PedalNames[index], PedalAxisNames[index], PedalForce(index), config.Enabled, config.Minimum / 100.0, config.Maximum / 100.0, key, acquired && !pedalAxisAvailable[index]);
	}

	internal static bool PedalOnHud(HudElementMode mode, PedalSettings config) =>
		mode == HudElementMode.Show || (mode == HudElementMode.Auto && config.Enabled && config.Vk != 0);

	private void AcquirePedalAxes()
	{
		for (int i = 0; i < pedals.Length; i++)
		{
			pedalAxisAvailable[i] = VJoy.AxisExists(VJoy.Device, PedalAxes[i]);
		}
		ResetPedals();
		RefreshPedalUi();
	}

	private void SetPedalKey(int vk, bool held)
	{
		for (int i = 0; i < pedals.Length; i++)
		{
			PedalSettings config = PedalConfig(i);
			if (vk != 0 && config.Vk == vk)
			{
				pedals[i].SetHeld(held, config);
			}
		}
	}

	private void StepPedals(double dt)
	{
		for (int i = 0; i < pedals.Length; i++)
		{
			pedals[i].Update(PedalConfig(i), dt);
		}
		WritePedalAxes(active);
	}

	private void WritePedalAxes(bool driving)
	{
		if (!acquired)
		{
			return;
		}
		for (int i = 0; i < pedals.Length; i++)
		{
			if (!pedalAxisAvailable[i])
			{
				continue;
			}
			PedalSettings config = PedalConfig(i);
			double value = (driving && config.Enabled) ? pedals[i].Value : 0;
			VJoy.SetAxis(VJoy.ToAxis((config.Inverted ? 1 - value : value) * 2 - 1), VJoy.Device, PedalAxes[i]);
		}
	}

	private void ResetPedals()
	{
		foreach (PedalState pedal in pedals)
		{
			pedal.Reset();
		}
		WritePedalAxes(false);
	}
}
