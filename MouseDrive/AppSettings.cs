using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseDrive;

// App-wide settings, the same for every profile. HUD position and size stay in overlay.cfg.
public class AppSettings
{
	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private static string PathName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

	public bool ShowHud { get; set; } = true;

	public HudStyle HudStyle { get; set; } = HudStyle.Wheel;

	public int HudOpacity { get; set; } = 90;

	public bool HudNumbers { get; set; } = true;

	public bool HudFade { get; set; }

	public bool HudInStandby { get; set; }

	public bool TopMost { get; set; } = true;

	public bool CloseToTray { get; set; }

	public bool Compact { get; set; }

	// Client height of the full window in pixels, as the user dragged it; 0 means the default.
	public int WindowHeight { get; set; }

	public static AppSettings Load()
	{
		try
		{
			if (File.Exists(PathName))
			{
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName), Opts) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	public void Save()
	{
		try
		{
			File.WriteAllText(PathName, JsonSerializer.Serialize(this, Opts));
		}
		catch
		{
		}
	}
}
