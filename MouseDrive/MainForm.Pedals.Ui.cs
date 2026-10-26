using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MouseDrive;

public partial class MainForm
{
	private int selectedPedal;
	private readonly PedalMeter[] pedalMeters = new PedalMeter[3];
	private readonly TabButton[] pedalTabs = new TabButton[3];
	private Label pedalMode;
	private Label pedalTitle;
	private Label pedalWarning;
	private Label pedalTiming;
	private ToggleSwitch pedalEnabled;
	private ToggleSwitch pedalInverted;
	private Button pedalKey;
	private Slider pedalMinimum;
	private Slider pedalMaximum;
	private Slider pedalApplyRate;
	private Slider pedalReleaseRate;
	private PedalCurve pedalCurve;

	private Control BuildPedalsPage()
	{
		TableLayoutPanel page = NewColumn();
		int row = 0;
		TableLayoutPanel live = Card();
		int lr = 0;
		AddRow(live, ref lr, Header("Pedal input", ScopeTag(perProfile: true)), SizeType.AutoSize, 0);
		for (int i = 0; i < pedalMeters.Length; i++)
		{
			pedalMeters[i] = new PedalMeter { Text = PedalNames[i], BackColor = Theme.Row };
			AddRow(live, ref lr, pedalMeters[i], SizeType.AutoSize, 0);
		}
		pedalMode = PedalNote("Standby preview · game output stays at 0%");
		AddRow(live, ref lr, pedalMode, SizeType.AutoSize, 0);
		AddRow(page, ref row, live, SizeType.AutoSize, 0);
		TableLayoutPanel tabs = new TableLayoutPanel { ColumnCount = 3, Height = 32, Margin = new Padding(0, 0, 0, 8) };
		for (int i = 0; i < pedalTabs.Length; i++)
		{
			int index = i;
			tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
			pedalTabs[i] = new TabButton { Text = PedalNames[i], Dock = DockStyle.Fill, Selected = i == 0 };
			pedalTabs[i].Click += (object? s, EventArgs e) =>
			{
				CancelHotkeyCapture();
				selectedPedal = index;
				ApplyPedalToUi();
			};
			tabs.Controls.Add(pedalTabs[i], i, 0);
		}
		AddRow(page, ref row, tabs, SizeType.Absolute, 32);
		TableLayoutPanel editor = Card();
		int er = 0;
		pedalTitle = HeaderLabel("Throttle · vJoy Y");
		AddRow(editor, ref er, pedalTitle, SizeType.AutoSize, 0);
		pedalEnabled = Switch("Enable pedal", "keys and mouse buttons", (object? s, EventArgs e) => SavePedalFromUi());
		AddRow(editor, ref er, pedalEnabled, SizeType.AutoSize, 0);
		FlowLayoutPanel input = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 8) };
		pedalKey = ActionButton("Not set", Theme.Blue);
		pedalKey.AccessibleName = "Pedal key";
		pedalKey.Click += (object? s, EventArgs e) => BeginPedalCapture();
		input.Controls.Add(pedalKey);
		Button clear = ActionButton("Clear key", Theme.Primary);
		clear.Click += (object? s, EventArgs e) => AssignPedalKey(0);
		input.Controls.Add(clear);
		AddRow(editor, ref er, input, SizeType.AutoSize, 0);
		pedalWarning = WarnLabel();
		AddRow(editor, ref er, pedalWarning, SizeType.AutoSize, 0);
		pedalCurve = new PedalCurve { BackColor = Theme.Row };
		AddRow(editor, ref er, pedalCurve, SizeType.AutoSize, 0);
		pedalTiming = PedalNote("");
		AddRow(editor, ref er, pedalTiming, SizeType.AutoSize, 0);
		pedalMinimum = AddSliderRow(editor, ref er, "Initial force", "at key press", 0, 100, 0, "%", autosave: false);
		pedalMaximum = AddSliderRow(editor, ref er, "Maximum force", "while held", 0, 100, 100, "%", autosave: false);
		pedalApplyRate = AddSliderRow(editor, ref er, "Apply rate", "force added per second", 1, PedalSettings.MaxRate, 100, "%/s", autosave: false);
		pedalReleaseRate = AddSliderRow(editor, ref er, "Release rate", "force lost per second", 1, PedalSettings.MaxRate, 200, "%/s", autosave: false);
		pedalMinimum.ValueChanged += (object? s, EventArgs e) => SavePedalFromUi(minimumChanged: true);
		pedalMaximum.ValueChanged += (object? s, EventArgs e) => SavePedalFromUi();
		pedalApplyRate.ValueChanged += (object? s, EventArgs e) => SavePedalFromUi();
		pedalReleaseRate.ValueChanged += (object? s, EventArgs e) => SavePedalFromUi();
		pedalInverted = Switch("Invert pedal axis", "if the game reads it backwards", (object? s, EventArgs e) => SavePedalFromUi());
		AddRow(editor, ref er, pedalInverted, SizeType.AutoSize, 0);
		AddRow(editor, ref er, PedalNote("Hold the assigned key to preview. Activate driving to send pedals to the game."), SizeType.AutoSize, 0);
		AddRow(page, ref row, editor, SizeType.AutoSize, 0);
		return Scroller(page);
	}

	private static Label PedalNote(string text) => new Label
	{
		Text = text, AutoSize = true, MaximumSize = new Size(400, 0), Font = Theme.Small,
		ForeColor = Theme.Muted, Margin = new Padding(0, 4, 0, 6)
	};

	private void ApplyPedalToUi()
	{
		bool wasLoading = loading;
		loading = true;
		PedalSettings config = PedalConfig(selectedPedal);
		pedalTitle.Text = (PedalNames[selectedPedal] + " · vJoy " + PedalAxisNames[selectedPedal]).ToUpperInvariant();
		pedalEnabled.Checked = config.Enabled;
		pedalKey.Text = Profile.KeyName(config.Vk);
		pedalMinimum.Value = config.Minimum;
		pedalMaximum.Value = config.Maximum;
		pedalApplyRate.Value = config.ApplyRate;
		pedalReleaseRate.Value = config.ReleaseRate;
		pedalInverted.Checked = config.Inverted;
		for (int i = 0; i < pedalTabs.Length; i++) pedalTabs[i].Selected = i == selectedPedal;
		loading = wasLoading;
		RefreshPedalUi();
	}

	private void SavePedalFromUi(bool minimumChanged = false)
	{
		if (loading) return;
		loading = true;
		if (pedalMinimum.Value > pedalMaximum.Value)
		{
			if (minimumChanged) pedalMaximum.Value = pedalMinimum.Value;
			else pedalMinimum.Value = pedalMaximum.Value;
		}
		loading = false;
		PedalSettings config = PedalConfig(selectedPedal);
		config.Enabled = pedalEnabled.Checked;
		config.Minimum = pedalMinimum.Value;
		config.Maximum = pedalMaximum.Value;
		config.ApplyRate = pedalApplyRate.Value;
		config.ReleaseRate = pedalReleaseRate.Value;
		config.Inverted = pedalInverted.Checked;
		ResetPedals();
		SaveActiveProfile();
		RefreshHotkeyUi();
		RefreshPedalUi();
	}

	private void BeginPedalCapture()
	{
		CancelHotkeyCapture();
		pedalKey.Text = "Press a key…";
		capture = (int vk) =>
		{
			if (vk == (int)Keys.Escape)
			{
				pedalKey.Text = Profile.KeyName(PedalConfig(selectedPedal).Vk);
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
		pedalKey.Text = Profile.KeyName(vk);
		SaveActiveProfile();
		RefreshHotkeyUi();
		RefreshPedalUi();
	}

	private void RefreshPedalUi()
	{
		if (pedalMode == null) return;
		SetText(pedalMode, active ? "Driving · pedal output sent to vJoy" : "Standby preview · game output stays at 0%");
		for (int i = 0; i < pedals.Length; i++)
		{
			pedalMeters[i].SetValue(pedals[i].Value, PedalConfig(i).Enabled, pedals[i].Held);
		}
		PedalSettings config = PedalConfig(selectedPedal);
		pedalCurve.SetSettings(config);
		double rise = (config.Maximum - config.Minimum) / (double)config.ApplyRate;
		double release = config.Maximum / (double)config.ReleaseRate;
		SetText(pedalTiming, $"{rise:0.##} s from initial to max · {release:0.##} s from max to zero");
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
