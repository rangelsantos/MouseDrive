using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MouseDrive;

// Whether a pedal is drawn on the HUD. Auto draws it when the active profile uses it (enabled, with a key).
public enum HudElementMode
{
	Auto,
	Show,
	Hide
}

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

	public HudStyle HudStyle { get; set; } = HudStyle.Classic;

	public HudWheel HudWheel { get; set; } = HudWheel.Classic;

	// Shows only the top 20% of the wheel, in the styles that have one.
	public bool HudZoom { get; set; } = false;

	public int HudOpacity { get; set; } = 90;

	// Opacity of the HUD's panel alone, 0..100; at 0 only the outlined marks and text remain.
	public int HudBackground { get; set; } = 100;

	public bool HudNumbers { get; set; } = true;

	public bool HudFade { get; set; } = false;

	public bool HudInStandby { get; set; } = false;

	public HudElementMode HudThrottle { get; set; } = HudElementMode.Auto;

	public HudElementMode HudBrake { get; set; } = HudElementMode.Auto;

	public HudElementMode HudClutch { get; set; } = HudElementMode.Auto;

	// The axis bar under the wheel in Full and Classic. Auto shows it when the profile's wheel turns more than a full turn.
	public HudElementMode HudPositionBar { get; set; } = HudElementMode.Auto;

	public bool TopMost { get; set; } = true;

	public bool CloseToTray { get; set; } = false;

	public bool Compact { get; set; } = false;

	// Client height of the full window in pixels, as the user dragged it; 0 means the default.
	public int WindowHeight { get; set; } = 0;

	// The app-wide part of the look; MainForm.CurrentLook() adds the profile's rotation and the bar.
	[JsonIgnore]
	public HudLook Look => new HudLook(HudStyle, HudWheel, HudZoom, HudNumbers, HudBackground);

	public static AppSettings Load()
	{
		try
		{
			if (File.Exists(PathName))
			{
				return Parse(File.ReadAllText(PathName)) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	// Earlier versions had the styles Wheel and Bar; they become Classic and Minimal instead of failing the whole file.
	internal static AppSettings Parse(string json)
	{
		JsonNode node = JsonNode.Parse(json);
		if (node is JsonObject o && o["HudStyle"] is JsonValue v && v.TryGetValue<string>(out var style))
		{
			if (style == "Wheel")
			{
				o["HudStyle"] = nameof(HudStyle.Classic);
			}
			else if (style == "Bar")
			{
				o["HudStyle"] = nameof(HudStyle.Minimal);
			}
		}
		AppSettings s = node.Deserialize<AppSettings>(Opts);
		if (s != null)
		{
			s.HudBackground = Math.Clamp(s.HudBackground, 0, 100);
		}
		return s;
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
