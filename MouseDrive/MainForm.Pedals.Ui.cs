using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MouseDrive;

public partial class MainForm
{
	// The time sliders step on a log scale, so short times get as much travel as long ones.
	private const int TimeSteps = 100;
	private const int TimeRoundMs = 10;

	private int selectedPedal;
	private PedalPicker pedalPicker;
	private Label pedalMode;
	private Label pedalTitle;
	private Label pedalWarning;
	private ToggleSwitch pedalEnabled;
	private ToggleSwitch pedalInverted;
	private Button pedalKey;
	private Button pedalClear;
	private RangeSlider pedalForce;
	private Slider pedalPress;
	private Slider pedalRelease;
	private PedalCurve pedalCurve;

	private Control BuildPedalsPage()
	{
		TableLayoutPanel page = NewColumn();
		int row = 0;
		TableLayoutPanel live = Card();
		int lr = 0;
		AddRow(live, ref lr, Header("Pedals", ScopeTag(perProfile: true)), SizeType.AutoSize, 0, fill: true, 6);
		pedalPicker = new PedalPicker(PedalNames) { BackColor = Theme.Row, Margin = new Padding(0, 0, 0, 4) };
		pedalPicker.SelectedChanged += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			selectedPedal = pedalPicker.Selected;
			ApplyPedalToUi();
		};
		AddRow(live, ref lr, pedalPicker, SizeType.AutoSize, 0);
		pedalMode = PedalNote("");
		AddRow(live, ref lr, pedalMode, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(page, ref row, live, SizeType.AutoSize, 0);
		TableLayoutPanel editor = Card();
		int er = 0;
		pedalEnabled = Switch("Enabled", null, (object? s, EventArgs e) =>
		{
			if (loading) return;
			PedalConfig(selectedPedal).Enabled = pedalEnabled.Checked;
			PedalSettingsChanged();
		});
		pedalEnabled.Size = new Size(100, 28);
		Control head = Header("Throttle · vJoy Y", pedalEnabled);
		pedalTitle = (Label)head.Controls[0];
		AddRow(editor, ref er, head, SizeType.AutoSize, 0, fill: true, 4);
		AddRow(editor, ref er, BuildPedalKeyRow(), SizeType.AutoSize, 0, fill: true, 6);
		pedalWarning = WarnLabel();
		AddRow(editor, ref er, pedalWarning, SizeType.AutoSize, 0);
		pedalCurve = new PedalCurve { BackColor = Theme.Row };
		AddRow(editor, ref er, pedalCurve, SizeType.AutoSize, 0, fill: true, 10);
		pedalForce = AddRangeRow(editor, ref er, "Force", "initial → max", 0, 100, 0, 100, "%");
		pedalForce.ValueChanged += (object? s, EventArgs e) =>
		{
			if (loading) return;
			PedalSettings config = PedalConfig(selectedPedal);
			config.Minimum = pedalForce.Low;
			config.Maximum = pedalForce.High;
			PedalSettingsChanged();
		};
		pedalPress = AddSliderRow(editor, ref er, "Press time", "initial → max", 0, TimeSteps, TimeStep(new PedalSettings().PressMs), "", autosave: false, format: (int v) => PedalTime(TimeMs(v)));
		pedalPress.ValueChanged += (object? s, EventArgs e) =>
		{
			if (loading) return;
			PedalConfig(selectedPedal).PressMs = TimeMs(pedalPress.Value);
			PedalSettingsChanged();
		};
		pedalRelease = AddSliderRow(editor, ref er, "Release time", "max → zero", 0, TimeSteps, TimeStep(new PedalSettings().ReleaseMs), "", autosave: false, format: (int v) => PedalTime(TimeMs(v)));
		pedalRelease.ValueChanged += (object? s, EventArgs e) =>
		{
			if (loading) return;
			PedalConfig(selectedPedal).ReleaseMs = TimeMs(pedalRelease.Value);
			PedalSettingsChanged();
		};
		pedalInverted = Switch("Invert axis", "if the game reads the pedal backwards", (object? s, EventArgs e) =>
		{
			if (loading) return;
			PedalConfig(selectedPedal).Inverted = pedalInverted.Checked;
			PedalSettingsChanged();
		});
		pedalInverted.Margin = new Padding(0, 4, 0, 0);
		AddRow(editor, ref er, pedalInverted, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(page, ref row, editor, SizeType.AutoSize, 0);
		return Scroller(page);
	}

	// Key, then the key button (click it to capture, like the Keys tab) and × to clear it.
	private TableLayoutPanel BuildPedalKeyRow()
	{
		TableLayoutPanel line = new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 122f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		line.Controls.Add(SliderCaption(line, "Key", "key or mouse button"), 0, 0);
		pedalKey = Styled("", Theme.Input);
		pedalKey.Size = new Size(124, 24);
		pedalKey.Margin = new Padding(0, 4, 4, 4);
		pedalKey.AccessibleName = "Pedal key";
		toolTip.SetToolTip(pedalKey, "Click, then press a key or mouse button (Esc cancels)");
		pedalKey.Click += (object? s, EventArgs e) => BeginPedalCapture();
		line.Controls.Add(pedalKey, 1, 0);
		pedalClear = Styled("×", Theme.Row);
		pedalClear.ForeColor = Theme.Text;
		pedalClear.Font = new Font("Segoe UI", 11f);
		pedalClear.Size = new Size(28, 24);
		pedalClear.Margin = new Padding(0, 4, 0, 4);
		pedalClear.AccessibleName = "Remove the pedal key";
		toolTip.SetToolTip(pedalClear, "Remove the key");
		pedalClear.Click += (object? s, EventArgs e) => AssignPedalKey(0);
		line.Controls.Add(pedalClear, 2, 0);
		return line;
	}

	private static Label PedalNote(string text) => new Label
	{
		Text = text, AutoSize = true, MaximumSize = new Size(400, 0), Font = Theme.Small,
		ForeColor = Theme.Muted, Margin = new Padding(0, 4, 0, 2)
	};

	private static int TimeMs(int step)
	{
		double ms = PedalSettings.MinTimeMs * Math.Pow((double)PedalSettings.MaxTimeMs / PedalSettings.MinTimeMs, (double)step / TimeSteps);
		return Math.Clamp((int)Math.Round(ms / TimeRoundMs) * TimeRoundMs, PedalSettings.MinTimeMs, PedalSettings.MaxTimeMs);
	}

	private static int TimeStep(int ms)
	{
		return (int)Math.Round(Math.Log((double)ms / PedalSettings.MinTimeMs) / Math.Log((double)PedalSettings.MaxTimeMs / PedalSettings.MinTimeMs) * TimeSteps);
	}

	private static string PedalTime(int ms)
	{
		return (ms / 1000.0).ToString((ms < 10000) ? "0.00" : "0.0", CultureInfo.InvariantCulture) + " s";
	}

	private void ApplyPedalToUi()
	{
		bool wasLoading = loading;
		loading = true;
		PedalSettings config = PedalConfig(selectedPedal);
		pedalPicker.Selected = selectedPedal;
		pedalTitle.Text = (PedalNames[selectedPedal] + " · vJoy " + PedalAxisNames[selectedPedal]).ToUpperInvariant();
		pedalEnabled.Checked = config.Enabled;
		pedalForce.SetRange(config.Minimum, config.Maximum);
		pedalPress.Value = TimeStep(config.PressMs);
		pedalRelease.Value = TimeStep(config.ReleaseMs);
		// The steps are rounded; the label shows the time the profile really has.
		((SliderRow)pedalPress.Tag).Value.Text = PedalTime(config.PressMs);
		((SliderRow)pedalRelease.Tag).Value.Text = PedalTime(config.ReleaseMs);
		pedalInverted.Checked = config.Inverted;
		loading = wasLoading;
		RefreshPedalKey(capturing: false);
		RefreshPedalUi();
	}

	private void PedalSettingsChanged()
	{
		ResetPedals();
		SaveActiveProfile();
		RefreshHotkeyUi();
		RefreshPedalUi();
		RefreshHudElementHints();
	}

	private void RefreshPedalKey(bool capturing)
	{
		int vk = PedalConfig(selectedPedal).Vk;
		pedalKey.Text = capturing ? "Press a key…" : Profile.KeyName(vk);
		pedalKey.BackColor = capturing ? Theme.Blue : Theme.Input;
		pedalKey.ForeColor = (vk == 0 && !capturing) ? Theme.Faint : Color.White;
		pedalClear.Enabled = vk != 0;
	}

	private void BeginPedalCapture()
	{
		CancelHotkeyCapture();
		RefreshPedalKey(capturing: true);
		capture = (int vk) =>
		{
			if (vk == (int)Keys.Escape)
			{
				RefreshPedalKey(capturing: false);
				return;
			}
			AssignPedalKey((vk == (int)Keys.Back) ? 0 : vk);
		};
	}

	private void AssignPedalKey(int vk)
	{
		CancelHotkeyCapture();
		ReleaseHeldKeys();
		PedalConfig(selectedPedal).Vk = vk;
		RefreshPedalKey(capturing: false);
		SaveActiveProfile();
		RefreshHotkeyUi();
		RefreshPedalUi();
		RefreshHudElementHints();
	}

	private void RefreshPedalUi()
	{
		if (pedalMode == null) return;
		SetText(pedalMode, "Click a pedal to edit it · " + (active ? "driving: pedals go to the game" : "standby preview: the game gets 0%"));
		for (int i = 0; i < pedals.Length; i++)
		{
			PedalSettings each = PedalConfig(i);
			pedalPicker.SetPedal(i, (each.Vk != 0) ? Profile.KeyName(each.Vk) : "", pedals[i].Value, each.Minimum, each.Maximum, each.Enabled, pedals[i].Held);
		}
		PedalSettings config = PedalConfig(selectedPedal);
		pedalCurve.SetSettings(config);
		pedalCurve.SetLive(config.Enabled ? pedals[selectedPedal].Value : 0, config.Enabled && pedals[selectedPedal].Held);
		List<string> warnings = new List<string>();
		if (!acquired || !pedalAxisAvailable[selectedPedal])
		{
			warnings.Add("Enable " + PedalAxisNames[selectedPedal] + " for Device 1 in Configure vJoy, then restart MouseDrive.");
		}
		if (config.Enabled && config.Vk == 0) warnings.Add("Assign a key or mouse button to use this pedal.");
		if (config.Enabled && config.Vk != 0)
		{
			Hotkey command = CommandOn(config.Vk);
			if (command != null) warnings.Add("This key runs " + command.Label + "; the pedal won't respond. Choose another key.");
			if (profile.Buttons.Exists((ButtonBinding b) => b.Vk == config.Vk)) warnings.Add("This key also presses a vJoy button.");
			for (int i = 0; i < pedals.Length; i++)
			{
				if (i != selectedPedal && PedalConfig(i).Enabled && PedalConfig(i).Vk == config.Vk) warnings.Add("This key also operates " + PedalNames[i].ToLowerInvariant() + ".");
			}
		}
		SetText(pedalWarning, string.Join("\n", warnings));
		pedalWarning.Visible = warnings.Count > 0;
	}
}
