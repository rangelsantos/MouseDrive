namespace MouseDrive;

public sealed class PedalSettings
{
	public const int MaxRate = 1000;

	public bool Enabled { get; set; } = false;
	public int Vk { get; set; } = 0;
	public int Minimum { get; set; } = 0;
	public int Maximum { get; set; } = 100;
	public int ApplyRate { get; set; } = 100;
	public int ReleaseRate { get; set; } = 200;
	public bool Inverted { get; set; } = false;

	internal bool IsValid => Vk >= 0 && Vk <= byte.MaxValue && Minimum >= 0 && Maximum >= Minimum && Maximum <= 100
		&& ApplyRate >= 1 && ApplyRate <= MaxRate && ReleaseRate >= 1 && ReleaseRate <= MaxRate;
}
