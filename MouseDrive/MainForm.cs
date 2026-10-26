using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseDrive;

// The steering loop, input, vJoy and profile logic. The screens are built in MainForm.Ui.cs.
public partial class MainForm : Form
{
	private struct RAWINPUTDEVICE
	{
		public ushort UsagePage;

		public ushort Usage;

		public uint Flags;

		public nint Target;
	}

#pragma warning disable CS0649 // filled by Marshal from native memory
	private struct RAWINPUTHEADER
	{
		public uint Type;

		public uint Size;

		public nint Device;

		public nint WParam;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct RAWMOUSE
	{
		[FieldOffset(0)]
		public ushort Flags;

		[FieldOffset(4)]
		public ushort ButtonFlags;

		[FieldOffset(6)]
		public ushort ButtonData;

		[FieldOffset(8)]
		public uint RawButtons;

		[FieldOffset(12)]
		public int LastX;

		[FieldOffset(16)]
		public int LastY;

		[FieldOffset(20)]
		public uint ExtraInformation;
	}

	private struct RAWINPUT
	{
		public RAWINPUTHEADER Header;

		public RAWMOUSE Mouse;
	}
#pragma warning restore CS0649

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const int WM_INPUT = 0x00FF;

	private const int WM_NCHITTEST = 0x0084;

	private const uint RID_INPUT = 0x10000003;

	private const uint RIM_TYPEMOUSE = 0;

	private const uint RIDEV_INPUTSINK = 0x00000100;

	private const ushort HID_USAGE_PAGE_GENERIC = 0x01;

	private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;

	private const ushort MOUSE_MOVE_ABSOLUTE = 0x01;

	private const ushort MOUSE_VIRTUAL_DESKTOP = 0x02;

	private const int SM_SWAPBUTTON = 23;

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	// Raw Input button-down flags and their VKs (left, right, middle, X1, X2); each button's up flag is the next bit.
	private static readonly (ushort Down, int Vk)[] MouseButtonFlags = new (ushort, int)[5]
	{
		(0x0001, 1),
		(0x0004, 2),
		(0x0010, 4),
		(0x0040, 5),
		(0x0100, 6)
	};

	private readonly KeyboardHook hook = new KeyboardHook();

	private readonly AppSettings settings = AppSettings.Load();

	private Profile profile;

	private int vjoyButtonCount = 16;

	private Timer loop;

	private Timer saveTimer;

	private Timer settingsTimer;

	private bool active;

	private bool acquired;

	private string vjoyDetails = "";

	// The wheel from the mouse alone, -1..1, before deadzone, expo and smoothing.
	private double wheel;

	// What goes to the vJoy X axis.
	private double steering;

	// Mouse counts from Raw Input since the last tick.
	private int rawDX;

	// The last pointer position from an absolute device (remote desktop...), in pixels; see OnAbsoluteMove.
	private int absoluteX;

	private bool hasAbsoluteX;

	// The mouse reports positions, not counts. The pointer must then move to steer, so it isn't locked.
	private bool absoluteInput;

	private bool rawRegistered;

	private int frame;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private double lastTick;

	private Rectangle driveScreen;

	private bool loading;

	private bool hudMoving;

	private bool exiting;

	private bool trayHintShown;

	private Action<int> capture;

	private int captureVersion;

	private OverlayForm overlay;

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(ref RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(nint lpRect);

	[DllImport("user32.dll")]
	private static extern bool GetClipCursor(out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

	[DllImport("user32.dll")]
	private static extern uint GetRawInputData(nint hRawInput, uint uiCommand, nint pData, ref uint pcbSize, uint cbSizeHeader);

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

	public MainForm()
	{
		Text = "MouseDrive";
		Icon = BrandIcon.Window(active: false);
		AutoScaleMode = AutoScaleMode.Font;
		FormBorderStyle = FormBorderStyle.Sizable;
		MaximizeBox = false;
		SetWindowSize();
		BackColor = Theme.Bg;
		ForeColor = Theme.Text;
		Font = Theme.Body;
		profile = ProfileManager.Bootstrap();
		saveTimer = new Timer
		{
			Interval = 300
		};
		saveTimer.Tick += (object? s, EventArgs e) =>
		{
			saveTimer.Stop();
			WriteProfile();
		};
		settingsTimer = new Timer
		{
			Interval = 300
		};
		settingsTimer.Tick += (object? s, EventArgs e) =>
		{
			settingsTimer.Stop();
			settings.Save();
		};
		loading = true;
		BuildUi();
		loading = false;
		ApplySettingsToUi();
		ApplyProfileToUi();
		AcquireVjoy();
		hook.KeyDown += OnKeyDown;
		hook.KeyUp += OnKeyUp;
		hook.Install();
		AppDomain.CurrentDomain.ProcessExit += (object? s, EventArgs e) =>
		{
			CursorHider.Restore();
			ReleaseCursor();
		};
		loop = new Timer
		{
			Interval = 10
		};
		loop.Tick += Loop_Tick;
		loop.Start();
	}

	// ---------- vJoy ----------

	private void AcquireVjoy()
	{
		if (!VJoy.Enabled())
		{
			SetStatus("vJoy driver not enabled. Install/enable vJoy, then restart.", Color.IndianRed);
			SetVjoyChip("vJoy off", ChipKind.Bad, "The vJoy driver isn't installed or is turned off.\n\nInstall vJoy (see the manual), restart the PC if it asks, then start MouseDrive again.");
			btnToggle.Enabled = false;
			return;
		}
		VjdStat status = VJoy.Status(VJoy.Device);
		acquired = status == VjdStat.Own || (status == VjdStat.Free && VJoy.Acquire(VJoy.Device));
		if (!acquired)
		{
			SetStatus("vJoy device 1 not free. Open Configure vJoy and enable device 1.", Color.IndianRed);
			SetVjoyChip("vJoy 1 busy", ChipKind.Bad, "vJoy device 1 is missing or another app is using it.\n\nOpen Configure vJoy, make sure Device 1 is enabled, and close any other app that uses vJoy (MouseYokeEnhanced uses device 1 too).");
			btnToggle.Enabled = false;
			return;
		}
		VJoy.Reset(VJoy.Device);
		AcquirePedalAxes();
		int buttons = VJoy.ButtonCount(VJoy.Device);
		if (buttons > 0)
		{
			vjoyButtonCount = buttons;
		}
		// A device set up without X would otherwise fail silently in the game.
		bool x = VJoy.AxisExists(VJoy.Device, VJoy.AxisX);
		List<string> problems = new List<string>();
		if (!x)
		{
			problems.Add("The X axis is off, so the steering can't reach the game. Open Configure vJoy and tick X for Device 1.");
		}
		if (buttons < 1)
		{
			problems.Add("Device 1 has no buttons, so key → button mappings do nothing. Set Number of Buttons to 16 in Configure vJoy.");
		}
		string summary = "Driver enabled · Device 1 acquired\nSteering axis X " + (x ? "✓" : "✗ (off)") + "\nButtons: " + Math.Max(buttons, 0);
		SetVjoyChip((problems.Count == 0) ? "vJoy ready" : "vJoy needs setup", (problems.Count == 0) ? ChipKind.Ok : ChipKind.Warn, summary + ((problems.Count > 0) ? ("\n\n" + string.Join("\n\n", problems)) : ""));
		SetStatus((profile.ToggleVk != 0) ? "Ready. Key bindings are live whenever the app runs." : "Ready. Set a Drive / standby key on the Keys tab.", Theme.Muted);
		ClampButtonsToDevice();
		RebuildBindingRows();
	}

	private void ClampButtonsToDevice()
	{
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Button > vjoyButtonCount)
			{
				b.Button = vjoyButtonCount;
			}
		}
	}

	private void ReleaseAllButtons()
	{
		foreach (ButtonBinding b in profile.Buttons)
		{
			b.Pressed = false;
			if (acquired)
			{
				VJoy.SetButton(down: false, VJoy.Device, (byte)b.Button);
			}
		}
	}

	private void UpdateButtonState(int button)
	{
		if (!acquired)
		{
			return;
		}
		bool down = false;
		foreach (ButtonBinding b in profile.Buttons)
		{
			down |= b.Button == button && b.Pressed;
		}
		VJoy.SetButton(down, VJoy.Device, (byte)button);
	}

	private void SetStatus(string text, Color color)
	{
		lblStatus.Text = text;
		lblStatus.ForeColor = color;
	}

	// ---------- saving ----------

	// Copies the UI into the profile now and writes the file a moment later, so dragging a slider
	// doesn't rewrite the JSON on every step.
	private void SaveActiveProfile()
	{
		if (loading)
		{
			return;
		}
		profile.Sensitivity = tbSensitivity.Value;
		profile.Deadzone = tbDeadzone.Value;
		profile.Expo = tbExpo.Value;
		profile.Smoothing = tbSmooth.Value;
		profile.SelfCentering = tbSelfCenter.Value;
		profile.InvertSteering = swInvert.Checked;
		profile.HideCursor = swHideCursor.Checked;
		saveTimer.Stop();
		saveTimer.Start();
		lblSaved.Text = "Saving…";
		lblSaved.ForeColor = Theme.Faint;
	}

	private void WriteProfile()
	{
		bool ok = ProfileManager.Save(profile);
		lblSaved.Text = (ok ? "Saved" : "Not saved");
		lblSaved.ForeColor = (ok ? Theme.Faint : Theme.Bad);
		toolTip.SetToolTip(lblSaved, ok ? null : ("Couldn't write the profile to " + ProfileManager.DirPath + ". Move MouseDrive to a folder you can write to (not Program Files)."));
	}

	private void FlushSave()
	{
		if (saveTimer.Enabled)
		{
			saveTimer.Stop();
			WriteProfile();
		}
	}

	private void SaveSettingsSoon()
	{
		if (!loading)
		{
			settingsTimer.Stop();
			settingsTimer.Start();
		}
	}

	// ---------- profiles ----------

	// Makes p the current profile. The old one is written first; its held buttons are let go, since their
	// key-ups would be matched against the new bindings.
	private void UseProfile(Profile p)
	{
		saveTimer.Stop();
		ProfileManager.Save(profile);
		ReleaseAllButtons();
		profile = p;
		ProfileManager.SetActive(p.Name);
		ClampButtonsToDevice();
		ApplyProfileToUi();
	}

	private void SwitchProfile(string name)
	{
		if (name == profile.Name)
		{
			return;
		}
		Profile next = ProfileManager.Load(name);
		if (next != null)
		{
			UseProfile(next);
			SetStatus("Loaded profile “" + profile.Name + "”.", Color.LightGreen);
		}
	}

	private void NewProfile()
	{
		string name = Prompt("New profile", "Profile name:", "New profile");
		if (!string.IsNullOrWhiteSpace(name))
		{
			Profile p = new Profile
			{
				Name = UniqueName(ProfileManager.Sanitize(name))
			};
			ProfileManager.Save(p);
			UseProfile(p);
		}
	}

	private void DuplicateProfile()
	{
		string name = Prompt("Duplicate profile", "Name for the copy:", profile.Name + " copy");
		if (!string.IsNullOrWhiteSpace(name))
		{
			SaveActiveProfile();
			Profile p = ProfileManager.Clone(profile, UniqueName(ProfileManager.Sanitize(name)));
			ProfileManager.Save(p);
			UseProfile(p);
		}
	}

	private void RenameProfile()
	{
		string name = Prompt("Rename profile", "New name:", profile.Name);
		if (string.IsNullOrWhiteSpace(name))
		{
			return;
		}
		name = ProfileManager.Sanitize(name);
		if (name == profile.Name)
		{
			return;
		}
		// A change of case only is the same file, so it isn't a clash.
		if (!string.Equals(name, profile.Name, StringComparison.OrdinalIgnoreCase))
		{
			name = UniqueName(name);
		}
		string old = profile.Name;
		saveTimer.Stop();
		profile.Name = name;
		if (!ProfileManager.Rename(profile, old))
		{
			profile.Name = old;
			MessageBox.Show("Couldn't rename the profile file.", "MouseDrive", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		ProfileManager.SetActive(name);
		RefreshProfileUi();
	}

	private void DeleteProfile()
	{
		if (ProfileManager.List().Count <= 1)
		{
			MessageBox.Show("You need at least one profile.", "MouseDrive", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		if (MessageBox.Show("Delete profile “" + profile.Name + "”?", "MouseDrive", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
		{
			return;
		}
		saveTimer.Stop();
		ReleaseAllButtons();
		ProfileManager.Delete(profile.Name);
		string first = ProfileManager.List()[0];
		profile = ProfileManager.Load(first) ?? new Profile
		{
			Name = first
		};
		ProfileManager.SetActive(profile.Name);
		ClampButtonsToDevice();
		ApplyProfileToUi();
	}

	private void ImportProfile()
	{
		using OpenFileDialog dlg = new OpenFileDialog
		{
			Title = "Import profile",
			Filter = "MouseDrive profile (*.json)|*.json|All files (*.*)|*.*"
		};
		if (dlg.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		Profile p = ProfileManager.Import(dlg.FileName);
		if (p == null)
		{
			MessageBox.Show("That file isn't a MouseDrive profile, or it can't be read.", "MouseDrive", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		p.Name = UniqueName(ProfileManager.Sanitize(Path.GetFileNameWithoutExtension(dlg.FileName)));
		ProfileManager.Save(p);
		UseProfile(p);
		SetStatus("Imported profile “" + p.Name + "”.", Color.LightGreen);
	}

	private void ExportProfile()
	{
		using SaveFileDialog dlg = new SaveFileDialog
		{
			Title = "Export profile",
			Filter = "MouseDrive profile (*.json)|*.json",
			FileName = profile.Name + ".json"
		};
		if (dlg.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		FlushSave();
		if (ProfileManager.Export(profile, dlg.FileName))
		{
			SetStatus("Exported “" + profile.Name + "”.", Color.LightGreen);
		}
		else
		{
			MessageBox.Show("Couldn't write " + dlg.FileName + ".", "MouseDrive", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void OpenProfilesFolder()
	{
		ProfileManager.EnsureDir();
		OpenPath(ProfileManager.DirPath);
	}

	private void OpenPath(string target)
	{
		try
		{
			Process.Start(new ProcessStartInfo(target)
			{
				UseShellExecute = true
			});
		}
		catch
		{
			SetStatus("Couldn't open " + target, Color.IndianRed);
		}
	}

	private static string UniqueName(string name)
	{
		if (!ProfileManager.Exists(name))
		{
			return name;
		}
		for (int i = 2; i < 100; i++)
		{
			string candidate = name + " " + i;
			if (!ProfileManager.Exists(candidate))
			{
				return candidate;
			}
		}
		return name + " " + DateTime.Now.Ticks;
	}

	// ---------- keys ----------

	private void OnKeyDown(int vk)
	{
		if (capture != null)
		{
			Action<int> cb = capture;
			int version = captureVersion;
			capture = null;
			BeginInvoke(() =>
			{
				if (version == captureVersion)
				{
					cb(vk);
				}
			});
			return;
		}
		if (vk == profile.ToggleVk)
		{
			ToggleActive();
			return;
		}
		if (vk == profile.CenterVk)
		{
			CenterSteering();
			HudToast("CENTER");
			return;
		}
		SetPedalKey(vk, true);
		if (!acquired)
		{
			return;
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk)
			{
				b.Pressed = true;
				VJoy.SetButton(down: true, VJoy.Device, (byte)b.Button);
			}
		}
	}

	private void OnKeyUp(int vk)
	{
		SetPedalKey(vk, false);
		if (!acquired)
		{
			return;
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk && b.Pressed)
			{
				b.Pressed = false;
				UpdateButtonState(b.Button);
			}
		}
	}

	// ---------- driving ----------

	private void ToggleActive()
	{
		if (!acquired)
		{
			return;
		}
		active = !active;
		ResetPedals();
		CenterSteering();
		if (active)
		{
			driveScreen = Screen.PrimaryScreen.Bounds;
			if (!absoluteInput)
			{
				LockCursor();
			}
			if (swHideCursor.Checked)
			{
				CursorHider.Hide();
			}
			SetStatus("Driving. Mouse = steering wheel." + ((profile.ToggleVk != 0) ? (" " + Profile.KeyName(profile.ToggleVk) + " to release.") : ""), Color.LightGreen);
		}
		else
		{
			CursorHider.Restore();
			ReleaseCursor();
			SetStatus("Standby. Steering centered; mouse free.", Theme.Muted);
		}
		btnToggle.Driving = active;
		Icon = BrandIcon.Window(active);
		tray.Icon = BrandIcon.Tray(active);
		RefreshCompact();
		HudToast(active ? "DRIVING" : "STANDBY");
		UpdateOverlayVisibility();
	}

	private void CenterSteering()
	{
		wheel = 0.0;
		steering = 0.0;
		rawDX = 0;
		hasAbsoluteX = false;
	}

	// While driving the pointer is held on one pixel in the middle of the main screen: Raw Input still
	// reports every mouse count, and the pointer can't wander onto another window or monitor. Windows and
	// other apps may drop the clip (focus changes, Ctrl+Alt+Del...), so the loop re-asserts it every tick.
	private void LockCursor()
	{
		int cx = driveScreen.Left + driveScreen.Width / 2;
		int cy = driveScreen.Top + driveScreen.Height / 2;
		GetClipCursor(out RECT cur);
		if (cur.Left != cx || cur.Top != cy || cur.Right != cx + 1 || cur.Bottom != cy + 1)
		{
			RECT r = new RECT
			{
				Left = cx,
				Top = cy,
				Right = cx + 1,
				Bottom = cy + 1
			};
			ClipCursor(ref r);
		}
	}

	// The pointer is released where it was locked, in the middle of the main screen.
	private static void ReleaseCursor()
	{
		ClipCursor(0);
	}

	// Mouse counts from center to full lock: 4000 at sensitivity 1, 100 at 100, on a geometric scale so
	// each step feels the same. 640 at the default 50 is about 10 cm on a 1600 DPI mouse.
	internal static double CountsToFullLock(int sensitivity)
	{
		return 4000.0 * Math.Pow(0.025, (double)(Math.Clamp(sensitivity, 1, 100) - 1) / 99.0);
	}

	private void Loop_Tick(object sender, EventArgs e)
	{
		// WM_TIMER is rounded to the ~15.6 ms system tick (~64 Hz, not 100 Hz), so every rate is
		// integrated over the real elapsed time. Capped so a UI stall can't cause a big jump.
		double now = clock.Elapsed.TotalSeconds;
		double dt = Math.Min(now - lastTick, 0.1);
		lastTick = now;
		hook.ReleaseStaleKeys();
		StepPedals(dt);
		if (!acquired)
		{
			if (selectedTab == 2 && ++frame % 3 == 0) RefreshPedalUi();
			return;
		}
		int dx = rawDX;
		rawDX = 0;
		if (active)
		{
			if (!absoluteInput)
			{
				LockCursor();
			}
			wheel = Math.Clamp(wheel + (double)(swInvert.Checked ? (-dx) : dx) / CountsToFullLock(tbSensitivity.Value), -1.0, 1.0);
			if (tbSelfCenter.Value > 0)
			{
				// Up to 4 per second: at 100% the wheel loses 63% of its angle in a quarter second.
				wheel *= Math.Exp((0.0 - (double)tbSelfCenter.Value) / 100.0 * 4.0 * dt);
			}
			double target = Shape(wheel, (double)tbDeadzone.Value / 100.0, (double)tbExpo.Value / 100.0);
			// The share of the gap left after 10 ms, compounded over dt.
			double a = 1.0 - Math.Pow((double)tbSmooth.Value / 100.0, dt * 100.0);
			steering += (target - steering) * a;
		}
		VJoy.SetAxis(VJoy.ToAxis(steering), VJoy.Device, VJoy.AxisX);
		if (++frame % 3 != 0)
		{
			return;
		}
		UpdateOverlayVisibility();
		HudState st = CurrentHudState();
		if (overlay != null && !overlay.IsDisposed && overlay.Visible)
		{
			overlay.UpdateValues(st);
		}
		RefreshLiveUi(st);
	}

	// Deadzone around center, then a blend of linear and cubic; the sign is kept and full lock stays full lock.
	internal static double Shape(double x, double dz, double expo)
	{
		double sign = Math.Sign(x);
		x = Math.Abs(x);
		if (x <= dz)
		{
			return 0.0;
		}
		x = (x - dz) / (1.0 - dz);
		return sign * ((1.0 - expo) * x + expo * x * x * x);
	}

	// ---------- HUD ----------

	private HudState CurrentHudState()
	{
		return new HudState
		{
			Steering = steering,
			Active = active,
			ToggleKey = ((profile.ToggleVk != 0) ? Profile.KeyName(profile.ToggleVk) : "")
		};
	}

	private void EnsureOverlay()
	{
		if (overlay == null || overlay.IsDisposed)
		{
			overlay = new OverlayForm(settings.HudStyle, settings.HudNumbers, CurrentHudState());
			overlay.SetOpacityPercent(settings.HudOpacity);
			overlay.SetFade(settings.HudFade);
			overlay.DoneClicked += EndHudPosition;
			if (hudMoving)
			{
				overlay.SetMovable(on: true);
			}
		}
	}

	// The HUD shows while driving, while being positioned, in standby if asked, and briefly for a notice.
	private void UpdateOverlayVisibility()
	{
		bool want = hudMoving || (settings.ShowHud && (active || settings.HudInStandby || (overlay != null && !overlay.IsDisposed && overlay.ToastActive)));
		if (want)
		{
			EnsureOverlay();
			if (!overlay.Visible)
			{
				overlay.UpdateValues(CurrentHudState());
				overlay.Show();
			}
		}
		else if (overlay != null && !overlay.IsDisposed && overlay.Visible)
		{
			overlay.Hide();
		}
	}

	private void HudToast(string text)
	{
		if (settings.ShowHud)
		{
			EnsureOverlay();
			overlay.Toast(text);
			UpdateOverlayVisibility();
		}
	}

	private void BeginHudPosition()
	{
		hudMoving = true;
		EnsureOverlay();
		overlay.SetMovable(on: true);
		UpdateOverlayVisibility();
		btnPositionHud.Text = "Done";
		SetStatus("Drag the HUD where you want it, then click Done.", Color.DeepSkyBlue);
	}

	private void EndHudPosition()
	{
		if (hudMoving)
		{
			hudMoving = false;
			overlay?.SetMovable(on: false);
			UpdateOverlayVisibility();
			btnPositionHud.Text = "Position HUD…";
			SetStatus("HUD position saved.", Theme.Muted);
		}
	}

	// ---------- window ----------

	private void ShowFromTray()
	{
		Show();
		if (WindowState == FormWindowState.Minimized)
		{
			WindowState = FormWindowState.Normal;
		}
		Activate();
	}

	private void ExitApp()
	{
		exiting = true;
		Close();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (settings.CloseToTray && !exiting && e.CloseReason == CloseReason.UserClosing)
		{
			e.Cancel = true;
			Hide();
			if (!trayHintShown)
			{
				trayHintShown = true;
				tray.ShowBalloonTip(3000, "MouseDrive is still running", "Your keys keep working. Right-click this icon to exit.", ToolTipIcon.Info);
			}
			return;
		}
		CursorHider.Restore();
		ReleaseCursor();
		SaveActiveProfile();
		FlushSave();
		settingsTimer.Stop();
		settings.Save();
		hook.Dispose();
		if (acquired)
		{
			VJoy.Reset(VJoy.Device);
			ResetPedals();
			VJoy.Relinquish(VJoy.Device);
		}
		overlay?.Close();
		tray.Visible = false;
		tray.Dispose();
		base.OnFormClosing(e);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int dark = 1;
		DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
		if (!rawRegistered)
		{
			// INPUTSINK: the mouse is read even while the game has the focus.
			RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1]
			{
				new RAWINPUTDEVICE
				{
					UsagePage = HID_USAGE_PAGE_GENERIC,
					Usage = HID_USAGE_GENERIC_MOUSE,
					Flags = RIDEV_INPUTSINK,
					Target = Handle
				}
			};
			rawRegistered = RegisterRawInputDevices(rid, 1u, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
			if (!rawRegistered)
			{
				SetStatus("Couldn't register for mouse input (Raw Input). Restart MouseDrive.", Color.IndianRed);
				btnToggle.Enabled = false;
			}
		}
	}

	// Raw Input sees the buttons even while the game has focus, but it can't swallow the click.
	// The primary button (left, or right when swapped) stays out: it is the one that clicks the UI and the game.
	private void OnMouseButtons(ushort flags)
	{
		int primary = ((GetSystemMetrics(SM_SWAPBUTTON) != 0) ? 2 : 1);
		foreach ((ushort down, int vk) in MouseButtonFlags)
		{
			if (vk == primary)
			{
				continue;
			}
			if ((flags & down) != 0)
			{
				hook.MouseButton(vk, down: true);
			}
			if ((flags & (down << 1)) != 0)
			{
				hook.MouseButton(vk, down: false);
			}
		}
	}

	// Remote desktop, remote-control apps and pen tablets report where the pointer is (0..65535 across the main
	// screen, or the virtual desktop), not mouse counts. The change in that position, in pixels, steers instead.
	private void OnAbsoluteMove(int x, bool virtualDesktop)
	{
		int width = (virtualDesktop ? SystemInformation.VirtualScreen.Width : Screen.PrimaryScreen.Bounds.Width);
		int px = (int)Math.Round((double)x / 65535.0 * (double)width);
		// The first report after a reset only sets the reference; a jump of more than half the width is the
		// pointer re-entering the remote window, not a hand movement.
		if (hasAbsoluteX && Math.Abs(px - absoluteX) <= width / 2)
		{
			rawDX += px - absoluteX;
		}
		absoluteX = px;
		hasAbsoluteX = true;
		if (!absoluteInput)
		{
			absoluteInput = true;
			if (active)
			{
				ReleaseCursor();
			}
		}
	}

	private void OnRawInput(nint handle)
	{
		uint size = 0u;
		uint header = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
		GetRawInputData(handle, RID_INPUT, 0, ref size, header);
		if (size == 0)
		{
			return;
		}
		nint buffer = Marshal.AllocHGlobal((int)size);
		try
		{
			if (GetRawInputData(handle, RID_INPUT, buffer, ref size, header) != size)
			{
				return;
			}
			RAWINPUT ri = Marshal.PtrToStructure<RAWINPUT>(buffer);
			if (ri.Header.Type != RIM_TYPEMOUSE)
			{
				return;
			}
			if ((ri.Mouse.Flags & MOUSE_MOVE_ABSOLUTE) == 0)
			{
				rawDX += ri.Mouse.LastX;
				if (ri.Mouse.LastX != 0 || ri.Mouse.LastY != 0)
				{
					absoluteInput = false;
				}
			}
			else
			{
				OnAbsoluteMove(ri.Mouse.LastX, (ri.Mouse.Flags & MOUSE_VIRTUAL_DESKTOP) != 0);
			}
			if (ri.Mouse.ButtonFlags != 0)
			{
				OnMouseButtons(ri.Mouse.ButtonFlags);
			}
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == WM_INPUT)
		{
			OnRawInput(m.LParam);
		}
		base.WndProc(ref m);
		if (m.Msg == WM_NCHITTEST)
		{
			m.Result = SizingHit((int)m.Result);
		}
	}

	private const int HTLEFT = 10;

	private const int HTRIGHT = 11;

	private const int HTTOP = 12;

	private const int HTTOPLEFT = 13;

	private const int HTTOPRIGHT = 14;

	private const int HTBOTTOM = 15;

	private const int HTBOTTOMLEFT = 16;

	private const int HTBOTTOMRIGHT = 17;

	private const int HTBORDER = 18;

	// The width is fixed, so the side edges and corners resize only vertically (or not at all in compact),
	// and the cursor doesn't offer a horizontal resize that would do nothing.
	private int SizingHit(int hit)
	{
		if (hit < HTLEFT || hit > HTBOTTOMRIGHT)
		{
			return hit;
		}
		if (compact || hit == HTLEFT || hit == HTRIGHT)
		{
			return HTBORDER;
		}
		return ((hit == HTTOPLEFT || hit == HTTOPRIGHT) ? HTTOP : ((hit == HTBOTTOMLEFT || hit == HTBOTTOMRIGHT) ? HTBOTTOM : hit));
	}
}
