using System.Collections.Generic;
using System.Windows.Forms;

namespace MouseDrive;

// The settings of one car or game, saved as Profiles\<Name>.json. Every property needs an initializer:
// a file written by an older version must still load, with defaults for what it doesn't have.
public class Profile
{
	public const int MinWheelRotation = 180;

	public const int MaxWheelRotation = 1080;

	public string Name { get; set; } = "Default";

	// Lock to lock, in degrees, as set in the game: the HUD wheel turns half of it each way. Only the
	// drawing uses it; the vJoy axis is the same at any rotation.
	public int WheelRotation { get; set; } = 360;

	// 1..100: how little mouse travel takes the wheel from center to full lock (see MainForm.CountsToFullLock).
	public int Sensitivity { get; set; } = 50;

	// Percent of the travel around center that gives no steering.
	public int Deadzone { get; set; } = 0;

	// Percent of cubic in the response: softer near center, same full lock.
	public int Expo { get; set; } = 20;

	public int Smoothing { get; set; } = 10;

	// 0 = off. How strongly the wheel drifts back to center when the mouse stops.
	public int SelfCentering { get; set; } = 0;

	public bool InvertSteering { get; set; } = false;

	public bool HideCursor { get; set; } = true;

	public int ToggleVk { get; set; } = (int)Keys.F9;

	public int CenterVk { get; set; } = (int)Keys.Home;

	public List<ButtonBinding> Buttons { get; set; } = new List<ButtonBinding>();

	public PedalSettings Throttle { get; set; } = new PedalSettings();

	public PedalSettings Brake { get; set; } = new PedalSettings();

	public PedalSettings Clutch { get; set; } = new PedalSettings();

	public static string KeyName(int vk)
	{
		Keys k = (Keys)vk;
		return k switch
		{
			Keys.None => "Not set",
			Keys.LButton => "Mouse left",
			Keys.RButton => "Mouse right",
			Keys.MButton => "Mouse middle",
			Keys.XButton1 => "Mouse 4",
			Keys.XButton2 => "Mouse 5",
			Keys.PageUp => "Page Up",
			Keys.PageDown => "Page Down",
			Keys.Back => "Backspace",
			Keys.Return => "Enter",
			Keys.Capital => "Caps Lock",
			Keys.LShiftKey => "Left Shift",
			Keys.RShiftKey => "Right Shift",
			Keys.LControlKey => "Left Ctrl",
			Keys.RControlKey => "Right Ctrl",
			Keys.LMenu => "Left Alt",
			Keys.RMenu => "Right Alt",
			>= Keys.D0 and <= Keys.D9 => ((char)vk).ToString(),
			>= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (vk - (int)Keys.NumPad0),
			Keys.OemOpenBrackets => "Bracket [",
			Keys.OemCloseBrackets => "Bracket ]",
			Keys.OemPipe => "Backslash \\",
			Keys.OemQuotes => "Quote '",
			Keys.Oemcomma => "Comma ,",
			Keys.OemPeriod => "Period .",
			Keys.OemQuestion => "Slash /",
			Keys.OemSemicolon => "Semicolon ;",
			Keys.Oemtilde => "Backtick `",
			Keys.OemMinus => "Minus -",
			Keys.Oemplus => "Equals =",
			_ => k.ToString(),
		};
	}
}
