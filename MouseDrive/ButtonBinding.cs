using System.Text.Json.Serialization;

namespace MouseDrive;

// A key or mouse button that holds down a vJoy button while it is held.
public class ButtonBinding
{
	[JsonIgnore]
	public bool Pressed;

	public int Vk { get; set; } = 0;

	public int Button { get; set; } = 1;

	// Optional name shown on the Keys tab ("Shift up").
	public string Label { get; set; } = "";
}
