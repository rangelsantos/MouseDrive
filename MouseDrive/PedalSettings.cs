using System;
using System.Text.Json.Serialization;

namespace MouseDrive;

public sealed class PedalSettings
{
	public const int MinTimeMs = 50;
	public const int MaxTimeMs = 10000;

	// The most an older profile could store as a rate, in percentage points per second.
	public const int MaxRate = 1000;

	private const int DefaultPressMs = 1000;
	private const int DefaultReleaseMs = 500;

	public bool Enabled { get; set; } = false;
	public int Vk { get; set; } = 0;
	public int Minimum { get; set; } = 0;
	public int Maximum { get; set; } = 100;

	// Time from the initial force to the maximum while the key is held, and from the maximum to zero after
	// release. Times, not rates, so changing the force range keeps how the pedal feels.
	public int PressMs { get; set; } = DefaultPressMs;
	public int ReleaseMs { get; set; } = DefaultReleaseMs;
	public bool Inverted { get; set; } = false;

	// Profiles before PressMs/ReleaseMs stored rates; Upgrade() turns them into times and they aren't written again.
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ApplyRate { get; set; } = null;

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ReleaseRate { get; set; } = null;

	internal double ApplyPerSecond => (Maximum - Minimum) / 100.0 / (PressMs / 1000.0);
	internal double ReleasePerSecond => Maximum / 100.0 / (ReleaseMs / 1000.0);

	internal bool IsValid => Vk >= 0 && Vk <= byte.MaxValue && Minimum >= 0 && Maximum >= Minimum && Maximum <= 100
		&& PressMs >= MinTimeMs && PressMs <= MaxTimeMs && ReleaseMs >= MinTimeMs && ReleaseMs <= MaxTimeMs
		&& (ApplyRate == null || (ApplyRate >= 1 && ApplyRate <= MaxRate))
		&& (ReleaseRate == null || (ReleaseRate >= 1 && ReleaseRate <= MaxRate));

	// An empty range has no ramp to time, so it keeps the default.
	internal void Upgrade()
	{
		if (ApplyRate is int apply)
		{
			PressMs = (Maximum > Minimum) ? ToTime(Maximum - Minimum, apply) : DefaultPressMs;
			ApplyRate = null;
		}
		if (ReleaseRate is int release)
		{
			ReleaseMs = (Maximum > 0) ? ToTime(Maximum, release) : DefaultReleaseMs;
			ReleaseRate = null;
		}
	}

	private static int ToTime(int points, int rate)
	{
		return Math.Clamp((int)Math.Round(points * 1000.0 / rate), MinTimeMs, MaxTimeMs);
	}
}
