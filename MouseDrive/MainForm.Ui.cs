using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MouseDrive;

// The screens, header, status bar and compact window. All built in code; there is no designer.
public partial class MainForm
{
	private sealed class HotkeyRow
	{
		public Hotkey Hotkey;

		public Label Name;

		public Button Key;

		public Button Clear;

		public Control ConflictBar;

		public Label ConflictText;

		public Label Warn;
	}

	private sealed class BindRow
	{
		public ButtonBinding B;

		public Panel Lamp;

		public Label Warn;
	}

	private sealed class SliderRow
	{
		public Caption Caption;

		public Label Value;

		public string Unit;
	}

	private const int FullWidth = 480;

	private const float HudOptionWidth = 180f;

	private const float RotationStepperWidth = 96f;

	private const int RotationStep = 10;

	// The usual lock-to-lock settings in racing games; any step in between is a custom value.
	private static readonly int[] RotationPresets = new int[6] { 180, 360, 540, 720, 900, 1080 };

	// The full window is resizable in height only; the pages scroll.
	private const int DefaultHeight = 576;

	private const int MinHeight = 360;

	private const int MaxHeight = 720;

	private const int CompactWidth = 420;

	private const int CompactBodyHeight = 88;

	private const int StatusHeight = 28;

	private static readonly int[] HeaderRows = new int[3] { 46, 56, 42 };

	private ToolTip toolTip;

	private NotifyIcon tray;

	private TableLayoutPanel root;

	private Control[] headerParts;

	private Panel pageHost;

	private readonly TabButton[] tabButtons = new TabButton[5];

	private readonly Control[] pages = new Control[5];

	private int selectedTab;

	private ProfileButton profileButton;

	private Button moreButton;

	private Button settingsButton;

	private StatusChip vjoyChip;

	private ActivateButton btnToggle;

	private Label lblStatus;

	private Label lblSaved;

	private DriveMonitor monitor;

	private LampStrip lamps;

	private ToggleSwitch swHideCursor;

	private ToggleSwitch swShowHudDrive;

	private KeyLegend legend;

	private Stepper numRotation;

	private Segmented segRotation;

	private TravelRuler ruler;

	private Slider tbSensitivity;

	private Label lblFullLock;

	private Slider tbExpo;

	private Slider tbDeadzone;

	private Slider tbSmooth;

	private Slider tbSelfCenter;

	private ToggleSwitch swInvert;

	private readonly TabButton[] keyTabs = new TabButton[2];

	private Control keysCommands;

	private Control keysButtons;

	private readonly List<HotkeyRow> hotkeyRows = new List<HotkeyRow>();

	private HotkeyRow capturingRow;

	private HotkeyRow conflictRow;

	private int conflictVk;

	private Hotkey conflictWith;

	private FlowLayoutPanel bindPanel;

	private Label lblBindStatus;

	private Label lblBindCount;

	private readonly List<BindRow> bindRows = new List<BindRow>();

	private HudPreview hudPreview;

	private Segmented segStyle;

	private Segmented segWheel;

	private Caption capWheel;

	private Segmented segBar;

	private Caption capBar;

	private ToggleSwitch swHudZoom;

	private Slider tbHudSize;

	private Slider tbHudOpacity;

	private Slider tbHudBackground;

	private readonly Segmented[] segHudPedals = new Segmented[3];

	private readonly Caption[] capHudPedals = new Caption[3];

	private ToggleSwitch swShowHud;

	private ToggleSwitch swHudNumbers;

	private ToggleSwitch swHudFade;

	private ToggleSwitch swHudStandby;

	private Button btnPositionHud;

	private Control compactPanel;

	private SteeringMonitor compactMonitor;

	private Label lblCompactState;

	private Label lblCompactProfile;

	private Label lblCompactSteering;

	private bool compact;

	private void BuildUi()
	{
		toolTip = new ToolTip
		{
			InitialDelay = 400
		};
		root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 6,
			BackColor = Theme.Bg,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		foreach (int h in HeaderRows)
		{
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
		}
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
		root.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusHeight));
		Controls.Add(root);
		Control appBar = BuildAppBar();
		Control activateRow = BuildActivateRow();
		Control tabBar = BuildTabBar();
		headerParts = new Control[3] { appBar, activateRow, tabBar };
		root.Controls.Add(appBar, 0, 0);
		root.Controls.Add(activateRow, 0, 1);
		root.Controls.Add(tabBar, 0, 2);
		pageHost = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Margin = Padding.Empty
		};
		root.Controls.Add(pageHost, 0, 3);
		pages[0] = BuildDrivePage();
		pages[1] = BuildSteeringPage();
		pages[2] = BuildPedalsPage();
		pages[3] = BuildKeysPage();
		pages[4] = BuildHudPage();
		foreach (Control p in pages)
		{
			p.Visible = false;
			pageHost.Controls.Add(p);
		}
		compactPanel = BuildCompact();
		compactPanel.Visible = false;
		root.Controls.Add(compactPanel, 0, 4);
		root.Controls.Add(BuildStatusBar(), 0, 5);
		BuildTray();
		SelectTab(0);
	}

	// ---------- building blocks ----------

	private TableLayoutPanel NewColumn()
	{
		return new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			AutoScroll = true,
			Padding = new Padding(12, 8, 12, 8),
			BackColor = Theme.Bg,
			GrowStyle = TableLayoutPanelGrowStyle.AddRows,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
	}

	// A rounded-looking group: the Row color on the page background, with padding.
	private TableLayoutPanel Card(int padding = 10)
	{
		return new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			Padding = new Padding(padding),
			GrowStyle = TableLayoutPanelGrowStyle.AddRows,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
	}

	private void AddRow(TableLayoutPanel t, ref int row, Control c, SizeType type, int size, bool fill = true, int gap = 8)
	{
		t.RowStyles.Add(new RowStyle(type, size));
		if (fill)
		{
			c.Dock = DockStyle.Fill;
		}
		else
		{
			c.Anchor = AnchorStyles.Left;
		}
		if (c.Margin == new Padding(3))
		{
			c.Margin = new Padding(0, 0, 0, gap);
		}
		t.Controls.Add(c, 0, row);
		row++;
	}

	// Puts a column in a scrolling panel. The column sizes itself to its content (so the last row doesn't
	// stretch to the bottom) and the panel scrolls when that is taller than the window; AutoScroll on a
	// TableLayoutPanel with Percent rows doesn't show a scrollbar reliably.
	private static Panel Scroller(TableLayoutPanel t)
	{
		t.AutoScroll = false;
		t.AutoSize = true;
		t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		t.Dock = DockStyle.Top;
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = t.BackColor,
			Margin = Padding.Empty
		};
		p.Controls.Add(t);
		return p;
	}

	private static Label HeaderLabel(string text)
	{
		return new Label
		{
			Text = text.ToUpperInvariant(),
			Font = Theme.Header,
			ForeColor = Theme.Muted,
			AutoSize = true,
			UseMnemonic = false
		};
	}

	// A group header: an uppercase label, with an optional control on the right.
	private static Control Header(string text, Control right = null)
	{
		if (right == null)
		{
			return HeaderLabel(text);
		}
		TableLayoutPanel t = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		Label l = HeaderLabel(text);
		l.Anchor = AnchorStyles.Left;
		l.Margin = Padding.Empty;
		right.Anchor = AnchorStyles.Right;
		right.Margin = Padding.Empty;
		t.Controls.Add(l, 0, 0);
		t.Controls.Add(right, 1, 0);
		return t;
	}

	// Goes on the right of a page's first header, so it's clear which settings follow the profile.
	private static Label ScopeTag(bool perProfile)
	{
		return new Label
		{
			Text = (perProfile ? "per profile" : "all profiles"),
			AutoSize = true,
			Font = Theme.Small,
			ForeColor = Theme.Faint
		};
	}

	// One line: caption (with an optional hint), slider, value with unit. The row matches its parent's color.
	private Slider AddSliderRow(TableLayoutPanel t, ref int row, string label, string hint, int min, int max, int val, string unit, bool autosave = true, Func<int, string>? format = null)
	{
		Func<int, string> text = format ?? ((int v) => v + unit);
		TableLayoutPanel line = SliderLine(t);
		Caption caption = SliderCaption(t, label, hint);
		Slider s = new Slider
		{
			Minimum = min,
			Maximum = max,
			Value = val,
			DefaultValue = val,
			BackColor = t.BackColor,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = new Padding(4, 0, 6, 0),
			AccessibleName = label
		};
		Label value = SliderValue(text(val));
		s.Tag = new SliderRow
		{
			Caption = caption,
			Value = value,
			Unit = unit
		};
		s.ValueChanged += (object? o, EventArgs e) =>
		{
			value.Text = text(s.Value);
		};
		if (autosave)
		{
			s.ValueChanged += (object? o, EventArgs e) =>
			{
				SaveActiveProfile();
			};
		}
		toolTip.SetToolTip(s, "Double-click to reset to " + text(val));
		line.Controls.Add(caption, 0, 0);
		line.Controls.Add(s, 1, 0);
		line.Controls.Add(value, 2, 0);
		AddRow(t, ref row, line, SizeType.AutoSize, 0, fill: true, 4);
		return s;
	}

	// The same row with a two-thumb slider; the value reads "low–high" plus the unit.
	private RangeSlider AddRangeRow(TableLayoutPanel t, ref int row, string label, string hint, int min, int max, int low, int high, string unit)
	{
		TableLayoutPanel line = SliderLine(t);
		Caption caption = SliderCaption(t, label, hint);
		RangeSlider s = new RangeSlider
		{
			Minimum = min,
			Maximum = max,
			DefaultLow = low,
			DefaultHigh = high,
			BackColor = t.BackColor,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = new Padding(4, 0, 6, 0),
			AccessibleName = label
		};
		s.SetRange(low, high);
		Label value = SliderValue(low + "–" + high + unit);
		s.ValueChanged += (object? o, EventArgs e) =>
		{
			value.Text = s.Low + "–" + s.High + unit;
		};
		toolTip.SetToolTip(s, "Double-click to reset to " + low + "–" + high + unit);
		line.Controls.Add(caption, 0, 0);
		line.Controls.Add(s, 1, 0);
		line.Controls.Add(value, 2, 0);
		AddRow(t, ref row, line, SizeType.AutoSize, 0, fill: true, 4);
		return s;
	}

	private static TableLayoutPanel SliderLine(TableLayoutPanel t)
	{
		return new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = t.BackColor,
			Margin = new Padding(0, 0, 0, 4),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 118f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 58f)
			}
		};
	}

	private static Caption SliderCaption(TableLayoutPanel t, string label, string hint)
	{
		return new Caption
		{
			Text = label,
			Hint = hint,
			BackColor = t.BackColor,
			Dock = DockStyle.Fill,
			Margin = Padding.Empty
		};
	}

	private static Label SliderValue(string text)
	{
		return new Label
		{
			Text = text,
			AutoSize = false,
			Size = new Size(58, 22),
			TextAlign = ContentAlignment.MiddleRight,
			BackColor = Theme.Input,
			ForeColor = Color.White,
			Padding = new Padding(0, 0, 4, 0),
			Anchor = AnchorStyles.Right,
			Margin = Padding.Empty
		};
	}

	private Button Styled(string text, Color back)
	{
		Button button = new Button();
		button.Text = text;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = back;
		button.ForeColor = Color.White;
		button.AutoSize = false;
		button.Font = Theme.Body;
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.15f);
		return button;
	}

	private Button ActionButton(string text, Color back)
	{
		Button b = Styled(text, back);
		b.AutoSize = true;
		b.Height = 28;
		b.Padding = new Padding(8, 1, 8, 1);
		b.Margin = new Padding(0, 0, 6, 0);
		return b;
	}

	private ToggleSwitch Switch(string text, string subtitle, EventHandler changed)
	{
		ToggleSwitch sw = new ToggleSwitch
		{
			Text = text,
			Subtitle = subtitle,
			Dock = DockStyle.Fill
		};
		sw.CheckedChanged += changed;
		return sw;
	}

	// Two switches side by side.
	private static TableLayoutPanel SwitchPair(ToggleSwitch a, ToggleSwitch b)
	{
		TableLayoutPanel t = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 50f),
				new ColumnStyle(SizeType.Percent, 50f)
			}
		};
		a.Margin = new Padding(0, 0, 3, 0);
		b.Margin = new Padding(3, 0, 0, 0);
		t.Controls.Add(a, 0, 0);
		t.Controls.Add(b, 1, 0);
		return t;
	}

	private static void SetText(Label l, string text)
	{
		if (l.Text != text)
		{
			l.Text = text;
		}
	}

	// ---------- header, tabs, status bar ----------

	private Control BuildAppBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			BackColor = Theme.Bar,
			Padding = new Padding(10, 8, 10, 8),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 38f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.Absolute, 38f)
			}
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		profileButton = new ProfileButton
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 0, 6, 0)
		};
		profileButton.Click += (object? s, EventArgs e) =>
		{
			ShowProfileMenu();
		};
		moreButton = Styled("⋯", Theme.Input);
		moreButton.Font = new Font("Segoe UI", 11f);
		moreButton.Dock = DockStyle.Fill;
		moreButton.Margin = new Padding(0, 0, 6, 0);
		moreButton.AccessibleName = "Profile actions";
		toolTip.SetToolTip(moreButton, "Profile actions");
		moreButton.Click += (object? s, EventArgs e) =>
		{
			ShowMoreMenu();
		};
		vjoyChip = new StatusChip
		{
			Text = "vJoy…",
			Anchor = AnchorStyles.Right,
			Margin = Padding.Empty
		};
		vjoyChip.Click += (object? s, EventArgs e) =>
		{
			MessageBox.Show(this, vjoyDetails, "vJoy status", MessageBoxButtons.OK, (vjoyChip.Kind == ChipKind.Ok) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
		};
		settingsButton = Styled("", Theme.Input);
		// Drawn as a path centered on its ink: as button text, the glyph sits low and left at this size.
		settingsButton.Paint += (object? s, PaintEventArgs e) =>
		{
			using GraphicsPath gear = new GraphicsPath();
			using FontFamily symbol = new FontFamily("Segoe UI Symbol");
			gear.AddString("⚙", symbol, 0, 21f, Point.Empty, StringFormat.GenericTypographic);
			RectangleF b = gear.GetBounds();
			using Matrix m = new Matrix();
			m.Translate(((float)settingsButton.Width - b.Width) / 2f - b.X, ((float)settingsButton.Height - b.Height) / 2f - b.Y);
			gear.Transform(m);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using SolidBrush brush = new SolidBrush(settingsButton.ForeColor);
			e.Graphics.FillPath(brush, gear);
		};
		settingsButton.Dock = DockStyle.Fill;
		settingsButton.Margin = new Padding(6, 0, 0, 0);
		settingsButton.AccessibleName = "Settings";
		toolTip.SetToolTip(settingsButton, "Window options and About");
		settingsButton.Click += (object? s, EventArgs e) =>
		{
			ShowSettingsMenu();
		};
		bar.Controls.Add(profileButton, 0, 0);
		bar.Controls.Add(moreButton, 1, 0);
		bar.Controls.Add(vjoyChip, 2, 0);
		bar.Controls.Add(settingsButton, 3, 0);
		return bar;
	}

	private void SetVjoyChip(string text, ChipKind kind, string details)
	{
		vjoyChip.Text = text;
		vjoyChip.Kind = kind;
		vjoyDetails = details;
		toolTip.SetToolTip(vjoyChip, details);
	}

	private Control BuildActivateRow()
	{
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Padding = new Padding(10, 10, 10, 0),
			Margin = Padding.Empty
		};
		btnToggle = new ActivateButton
		{
			Dock = DockStyle.Fill
		};
		btnToggle.Click += (object? s, EventArgs e) =>
		{
			ToggleActive();
		};
		p.Controls.Add(btnToggle);
		return p;
	}

	private Control BuildTabBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 5,
			BackColor = Theme.Bg,
			Padding = new Padding(10, 8, 10, 1),
			Margin = Padding.Empty
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		bar.Paint += (object? s, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Theme.Line);
			e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
		};
		string[] names = { "Drive", "Steering", "Pedals", "Keys", "HUD" };
		for (int i = 0; i < names.Length; i++)
		{
			int index = i;
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
			tabButtons[i] = new TabButton
			{
				Text = names[i],
				Dock = DockStyle.Fill
			};
			tabButtons[i].Click += (object? s, EventArgs e) =>
			{
				SelectTab(index);
			};
			bar.Controls.Add(tabButtons[i], i, 0);
		}
		return bar;
	}

	private void SelectTab(int index)
	{
		if (selectedTab != index)
		{
			CancelHotkeyCapture();
		}
		selectedTab = index;
		for (int i = 0; i < pages.Length; i++)
		{
			tabButtons[i].Selected = i == index;
			pages[i].Visible = i == index;
		}
	}

	private Control BuildStatusBar()
	{
		TableLayoutPanel bar = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			BackColor = Theme.Bar,
			Padding = new Padding(12, 0, 12, 0),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		lblStatus = new Label
		{
			Dock = DockStyle.Fill,
			AutoEllipsis = true,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			Margin = Padding.Empty
		};
		lblSaved = new Label
		{
			Text = "Saved",
			AutoSize = true,
			Anchor = AnchorStyles.Right,
			ForeColor = Theme.Faint,
			Font = Theme.Small,
			Margin = new Padding(8, 0, 0, 0)
		};
		bar.Controls.Add(lblStatus, 0, 0);
		bar.Controls.Add(lblSaved, 1, 0);
		return bar;
	}

	// ---------- profile menus ----------

	// The header menus are kept and refilled on every open: an item's Click can open a modal
	// dialog (Rename, Import, About...), so the menu must still exist when the handler returns.
	private ContextMenuStrip profileMenu;

	private ContextMenuStrip moreMenu;

	private ContextMenuStrip settingsMenu;

	private static ContextMenuStrip Refill(ref ContextMenuStrip menu)
	{
		if (menu == null)
		{
			menu = DarkMenuRenderer.NewMenu();
		}
		ToolStripItem[] old = new ToolStripItem[menu.Items.Count];
		menu.Items.CopyTo(old, 0);
		menu.Items.Clear();
		foreach (ToolStripItem item in old)
		{
			item.Dispose();
		}
		return menu;
	}

	private static void ShowMenu(ContextMenuStrip m, Control under)
	{
		m.Show(under, new Point(0, under.Height + 2));
	}

	private void ShowProfileMenu()
	{
		ContextMenuStrip m = Refill(ref profileMenu);
		m.MinimumSize = new Size(profileButton.Width, 0);
		foreach (string n in ProfileManager.List())
		{
			string name = n;
			ToolStripMenuItem item = new ToolStripMenuItem(name)
			{
				Checked = (name == profile.Name)
			};
			item.Click += (object? s, EventArgs e) =>
			{
				SwitchProfile(name);
			};
			m.Items.Add(item);
		}
		ShowMenu(m, profileButton);
	}

	private void ShowMoreMenu()
	{
		ContextMenuStrip m = Refill(ref moreMenu);
		m.Items.Add("New profile…", null, (object? s, EventArgs e) =>
		{
			NewProfile();
		});
		m.Items.Add("Duplicate…", null, (object? s, EventArgs e) =>
		{
			DuplicateProfile();
		});
		m.Items.Add("Rename…", null, (object? s, EventArgs e) =>
		{
			RenameProfile();
		});
		m.Items.Add(new ToolStripSeparator());
		m.Items.Add("Import…", null, (object? s, EventArgs e) =>
		{
			ImportProfile();
		});
		m.Items.Add("Export…", null, (object? s, EventArgs e) =>
		{
			ExportProfile();
		});
		m.Items.Add("Open profiles folder", null, (object? s, EventArgs e) =>
		{
			OpenProfilesFolder();
		});
		m.Items.Add(new ToolStripSeparator());
		ToolStripItem del = m.Items.Add("Delete “" + profile.Name + "”…", null, (object? s, EventArgs e) =>
		{
			DeleteProfile();
		});
		del.Tag = "danger";
		ShowMenu(m, moreButton);
	}

	// Window options are app-wide, so they live here and not on a page with per-profile settings.
	private void ShowSettingsMenu()
	{
		ContextMenuStrip m = Refill(ref settingsMenu);
		m.ShowItemToolTips = true;
		ToolStripMenuItem top = new ToolStripMenuItem("Always on top", null, (object? s, EventArgs e) =>
		{
			settings.TopMost = !settings.TopMost;
			TopMost = settings.TopMost;
			SaveSettingsSoon();
		})
		{
			Checked = settings.TopMost
		};
		ToolStripMenuItem toTray = new ToolStripMenuItem("Close to tray", null, (object? s, EventArgs e) =>
		{
			settings.CloseToTray = !settings.CloseToTray;
			tray.Visible = settings.CloseToTray;
			SaveSettingsSoon();
		})
		{
			Checked = settings.CloseToTray,
			ToolTipText = "Closing the window keeps the app in the tray; keys keep working"
		};
		m.Items.Add(top);
		m.Items.Add(toTray);
		m.Items.Add("Compact window", null, (object? s, EventArgs e) =>
		{
			SetCompact(on: true);
		}).ToolTipText = "Just the essentials, for a second screen";
		m.Items.Add(new ToolStripSeparator());
		m.Items.Add("About MouseDrive…", null, (object? s, EventArgs e) =>
		{
			ShowAbout();
		});
		m.Show(settingsButton, new Point(settingsButton.Width, settingsButton.Height + 2), ToolStripDropDownDirection.BelowLeft);
	}

	private void RefreshProfileUi()
	{
		profileButton.ProfileName = profile.Name;
		lblCompactProfile.Text = profile.Name;
		string tip = "MouseDrive — " + profile.Name;
		tray.Text = ((tip.Length > 63) ? tip.Substring(0, 63) : tip);
	}

	private string Prompt(string title, string label, string defaultValue)
	{
		using Form dlg = new Form();
		dlg.Text = title;
		dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
		dlg.StartPosition = FormStartPosition.CenterParent;
		dlg.TopMost = true;
		dlg.ShowInTaskbar = false;
		dlg.ClientSize = new Size(340, 120);
		dlg.MaximizeBox = false;
		dlg.MinimizeBox = false;
		dlg.BackColor = Theme.Bar;
		dlg.ForeColor = Theme.Text;
		dlg.Font = Font;
		dlg.Controls.Add(new Label
		{
			Text = label,
			Bounds = new Rectangle(14, 14, 312, 20),
			AutoSize = false
		});
		TextBox tb = new TextBox
		{
			Text = defaultValue,
			Bounds = new Rectangle(14, 38, 312, 24),
			BackColor = Theme.Input,
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		dlg.Controls.Add(tb);
		Button ok = new Button
		{
			Text = "OK",
			DialogResult = DialogResult.OK,
			Bounds = new Rectangle(150, 78, 80, 28),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(45, 90, 70),
			ForeColor = Color.White
		};
		Button cancel = new Button
		{
			Text = "Cancel",
			DialogResult = DialogResult.Cancel,
			Bounds = new Rectangle(240, 78, 80, 28),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(60, 60, 70),
			ForeColor = Color.White
		};
		dlg.Controls.Add(ok);
		dlg.Controls.Add(cancel);
		dlg.AcceptButton = ok;
		dlg.CancelButton = cancel;
		dlg.HandleCreated += (object? s, EventArgs e) =>
		{
			int dark = 1;
			DwmSetWindowAttribute(dlg.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
		};
		tb.SelectAll();
		return (dlg.ShowDialog(this) == DialogResult.OK) ? tb.Text : null;
	}

	// ---------- About ----------

	private const string CoffeeUrl = "https://ko-fi.com/rangelsantosss";

	private static string ManualPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MouseDrive_Manual.md");

	// "1.4" from the 1.4.0 that the release workflow stamps; null for a local build, which keeps 0.0.0.
	private static string AppVersion()
	{
		string v = Application.ProductVersion;
		int plus = v.IndexOf('+');
		if (plus >= 0)
		{
			v = v.Substring(0, plus);
		}
		if (!Version.TryParse(v, out Version parsed) || (parsed.Major == 0 && parsed.Minor == 0))
		{
			return null;
		}
		return parsed.Major + "." + parsed.Minor + ((parsed.Build > 0) ? ("." + parsed.Build) : "");
	}

	private LinkLabel Link(string text, Font font)
	{
		LinkLabel l = new LinkLabel
		{
			Text = text,
			AutoSize = true,
			Font = font,
			LinkColor = Theme.Accent,
			ActiveLinkColor = Color.White,
			VisitedLinkColor = Theme.Accent,
			LinkBehavior = LinkBehavior.HoverUnderline,
			Margin = new Padding(0, 0, 0, 6)
		};
		l.LinkClicked += (object? s, LinkLabelLinkClickedEventArgs e) =>
		{
			OpenPath((string)e.Link.LinkData);
		};
		return l;
	}

	private void ShowAbout()
	{
		string version = AppVersion();
		using Form dlg = new Form();
		dlg.Text = "About MouseDrive";
		dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
		dlg.StartPosition = FormStartPosition.CenterParent;
		dlg.TopMost = true;
		dlg.ShowInTaskbar = false;
		dlg.ClientSize = new Size(360, 260);
		dlg.MaximizeBox = false;
		dlg.MinimizeBox = false;
		dlg.BackColor = Theme.Bar;
		dlg.ForeColor = Theme.Text;
		dlg.Font = Font;
		TableLayoutPanel t = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 5,
			Padding = new Padding(16, 16, 16, 14),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		dlg.Controls.Add(t);
		using Icon icon = new Icon(BrandIcon.Window(active: false), 48, 48);
		PictureBox logo = new PictureBox
		{
			Image = icon.ToBitmap(),
			Size = new Size(48, 48),
			SizeMode = PictureBoxSizeMode.CenterImage,
			Margin = new Padding(0, 0, 12, 0)
		};
		t.Controls.Add(logo, 0, 0);
		t.SetRowSpan(logo, 2);
		t.Controls.Add(new Label
		{
			Text = "MouseDrive",
			AutoSize = true,
			Font = Theme.Big,
			ForeColor = Color.White,
			Margin = new Padding(0, 4, 0, 2)
		}, 1, 0);
		t.Controls.Add(new Label
		{
			Text = ((version != null) ? ("Version " + version) : "Development build"),
			AutoSize = true,
			ForeColor = Theme.Muted,
			Margin = Padding.Empty
		}, 1, 1);
		Label credits = new Label
		{
			Text = "Created by Rangel Santos. Steer racing games with your mouse; joystick output goes through vJoy.",
			AutoSize = true,
			ForeColor = Theme.Text,
			MaximumSize = new Size(328, 0),
			Margin = new Padding(0, 16, 0, 14)
		};
		t.Controls.Add(credits, 0, 2);
		t.SetColumnSpan(credits, 2);
		FlowLayoutPanel links = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.TopDown,
			AutoSize = true,
			Margin = Padding.Empty
		};
		LinkLabel manual = Link("User manual", Theme.Strong);
		manual.Links[0].LinkData = ManualPath;
		toolTip.SetToolTip(manual, "Opens MouseDrive_Manual.md from the app folder");
		links.Controls.Add(manual);
		LinkLabel coffee = Link("☕ Buy me a coffee", Theme.Strong);
		coffee.LinkColor = Theme.Amber;
		coffee.VisitedLinkColor = Theme.Amber;
		coffee.Links[0].LinkData = CoffeeUrl;
		coffee.Margin = new Padding(0, 6, 0, 6);
		toolTip.SetToolTip(coffee, "Support MouseDrive on Ko-fi");
		links.Controls.Add(coffee);
		t.Controls.Add(links, 0, 3);
		t.SetColumnSpan(links, 2);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.RightToLeft,
			Dock = DockStyle.Fill,
			AutoSize = true,
			Margin = Padding.Empty
		};
		Button close = ActionButton("Close", Theme.Primary);
		close.DialogResult = DialogResult.OK;
		close.Margin = Padding.Empty;
		Button copy = ActionButton("Copy info", Theme.Input);
		toolTip.SetToolTip(copy, "Copy the version and setup, to paste into a bug report");
		copy.Click += (object? s, EventArgs e) =>
		{
			string info = "MouseDrive " + (version ?? "dev build") + "\r\n" + vjoyDetails.Replace("\n", "\r\n") + "\r\nWindows " + Environment.OSVersion.Version + "\r\n.NET " + Environment.Version;
			try
			{
				Clipboard.SetText(info);
				copy.Text = "Copied";
			}
			catch
			{
				copy.Text = "Clipboard busy";
			}
		};
		buttons.Controls.Add(close);
		buttons.Controls.Add(copy);
		t.Controls.Add(buttons, 0, 4);
		t.SetColumnSpan(buttons, 2);
		dlg.AcceptButton = close;
		dlg.CancelButton = close;
		dlg.HandleCreated += (object? s, EventArgs e) =>
		{
			int dark = 1;
			DwmSetWindowAttribute(dlg.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
		};
		dlg.ShowDialog(this);
		logo.Image.Dispose();
	}

	// ---------- Drive ----------

	private Control BuildDrivePage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		TableLayoutPanel card = Card();
		int cr = 0;
		AddRow(card, ref cr, Header("Live output"), SizeType.AutoSize, 0, fill: false, 6);
		monitor = new DriveMonitor
		{
			BackColor = Theme.Row
		};
		toolTip.SetToolTip(monitor, "What the game gets. The triangle is the mouse, before deadzone, expo and smoothing; on a pedal, the white tick is its initial force and the amber line its maximum");
		AddRow(card, ref cr, monitor, SizeType.AutoSize, 0, fill: true, 8);
		AddRow(card, ref cr, Header("vJoy buttons"), SizeType.AutoSize, 0, fill: false, 6);
		lamps = new LampStrip
		{
			BackColor = Theme.Row
		};
		AddRow(card, ref cr, lamps, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, card, SizeType.AutoSize, 0);
		swHideCursor = Switch("Hide cursor", "while driving · per profile", (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			if (active)
			{
				if (swHideCursor.Checked)
				{
					CursorHider.Hide();
				}
				else
				{
					CursorHider.Restore();
				}
			}
		});
		swShowHudDrive = Switch("Show HUD", "while driving · all profiles", (object? s, EventArgs e) =>
		{
			SetShowHud(swShowHudDrive.Checked);
		});
		AddRow(t, ref row, SwitchPair(swHideCursor, swShowHudDrive), SizeType.AutoSize, 0);
		TableLayoutPanel keys = Card();
		int kr = 0;
		LinkLabel change = new LinkLabel
		{
			Text = "change on Keys",
			AutoSize = true,
			Font = Theme.Small,
			LinkColor = Theme.Muted,
			ActiveLinkColor = Color.White,
			VisitedLinkColor = Theme.Muted,
			LinkBehavior = LinkBehavior.HoverUnderline
		};
		change.LinkClicked += (object? s, LinkLabelLinkClickedEventArgs e) =>
		{
			SelectTab(3);
		};
		AddRow(keys, ref kr, Header("Commands", change), SizeType.AutoSize, 0, fill: true, 6);
		legend = new KeyLegend
		{
			BackColor = Theme.Row
		};
		AddRow(keys, ref kr, legend, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, keys, SizeType.AutoSize, 0);
		return Scroller(t);
	}

	// ---------- Steering ----------

	private Control BuildSteeringPage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		TableLayoutPanel response = Card();
		int rr = 0;
		AddRow(response, ref rr, Header("Mouse to wheel", ScopeTag(perProfile: true)), SizeType.AutoSize, 0, fill: true, 6);
		ruler = new TravelRuler
		{
			Height = 90,
			BackColor = Theme.Row,
			Margin = new Padding(0, 0, 0, 8)
		};
		toolTip.SetToolTip(ruler, "How far the mouse travels for each 10% of wheel lock, in mm on a " + MainForm.AssumedDpi + " DPI mouse");
		AddRow(response, ref rr, ruler, SizeType.AutoSize, 0, fill: true);
		tbSensitivity = AddSliderRow(response, ref rr, "Sensitivity", "less mouse travel", 1, 100, 50, "");
		lblFullLock = new Label
		{
			AutoSize = true,
			ForeColor = Theme.Faint,
			Font = Theme.Small,
			Margin = new Padding(0, 0, 0, 6)
		};
		toolTip.SetToolTip(lblFullLock, "Raw mouse counts; a 1600 DPI mouse makes about 630 per centimeter");
		AddRow(response, ref rr, lblFullLock, SizeType.AutoSize, 0, fill: false);
		tbExpo = AddSliderRow(response, ref rr, "Expo", "softer near center", 0, 90, 20, "%");
		tbDeadzone = AddSliderRow(response, ref rr, "Deadzone", "around center", 0, 30, 0, "%");
		swInvert = Switch("Invert steering", "mouse left turns right", (object? s, EventArgs e) =>
		{
			SaveActiveProfile();
			CenterSteering();
		});
		swInvert.Margin = new Padding(0, 2, 0, 0);
		AddRow(response, ref rr, swInvert, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, response, SizeType.AutoSize, 0);
		TableLayoutPanel feel = Card();
		int fr = 0;
		AddRow(feel, ref fr, Header("Feel"), SizeType.AutoSize, 0, fill: false, 6);
		tbSmooth = AddSliderRow(feel, ref fr, "Smoothing", "less jitter, a little lag", 0, 80, 10, "%");
		tbSelfCenter = AddSliderRow(feel, ref fr, "Self-centering", "0 = wheel stays put", 0, 100, 0, "%");
		AddRow(t, ref row, feel, SizeType.AutoSize, 0);
		tbSensitivity.ValueChanged += (object? s, EventArgs e) =>
		{
			UpdateFullLock();
			UpdateRuler();
		};
		tbExpo.ValueChanged += (object? s, EventArgs e) =>
		{
			UpdateRuler();
		};
		tbDeadzone.ValueChanged += (object? s, EventArgs e) =>
		{
			UpdateRuler();
		};
		return Scroller(t);
	}

	// A rotation that isn't a preset leaves every preset unselected.
	private void SyncRotationPresets()
	{
		int i = Array.IndexOf(RotationPresets, numRotation.Value);
		if (i >= 0)
		{
			segRotation.SelectedIndex = i;
		}
		else
		{
			segRotation.ClearSelection();
		}
	}

	private void UpdateRuler()
	{
		ruler.SetCurve((double)tbDeadzone.Value / 100.0, (double)tbExpo.Value / 100.0, FullLockMm(tbSensitivity.Value));
	}

	private void UpdateFullLock()
	{
		lblFullLock.Text = "Center to full lock: " + Math.Round(CountsToFullLock(tbSensitivity.Value)) + " mouse counts";
	}

	// ---------- Keys ----------

	private Control BuildKeysPage()
	{
		TableLayoutPanel t = NewColumn();
		t.AutoScroll = false;
		int row = 0;
		// Sub-tabs, drawn like the page tabs so they read as navigation and not as a setting.
		TableLayoutPanel bar = new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = new Padding(0, 0, 0, 8),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		bar.Paint += (object? s, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Theme.Line);
			e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
		};
		string[] names = new string[2] { "Commands", "vJoy buttons" };
		int[] widths = new int[2] { 100, 140 };
		for (int i = 0; i < 2; i++)
		{
			int index = i;
			keyTabs[i] = new TabButton
			{
				Text = names[i],
				Font = Theme.Body,
				Size = new Size(widths[i], 30),
				Margin = new Padding(0, 0, 6, 0)
			};
			keyTabs[i].Click += (object? s, EventArgs e) =>
			{
				SelectKeyList(index);
			};
			bar.Controls.Add(keyTabs[i], i, 0);
		}
		AddRow(t, ref row, bar, SizeType.AutoSize, 0);
		Panel host = new Panel
		{
			BackColor = Theme.Bg,
			Margin = Padding.Empty
		};
		keysCommands = BuildCommandsList();
		keysButtons = BuildButtonsList();
		host.Controls.Add(keysCommands);
		host.Controls.Add(keysButtons);
		AddRow(t, ref row, host, SizeType.Percent, 100, fill: true, 0);
		SelectKeyList(0);
		return t;
	}

	private void SelectKeyList(int index)
	{
		CancelHotkeyCapture();
		for (int i = 0; i < 2; i++)
		{
			keyTabs[i].Selected = i == index;
		}
		keysCommands.Visible = index == 0;
		keysButtons.Visible = index == 1;
	}

	private Control BuildCommandsList()
	{
		TableLayoutPanel t = NewColumn();
		t.Padding = Padding.Empty;
		int row = 0;
		string group = null;
		TableLayoutPanel card = null;
		int cr = 0;
		foreach (Hotkey hk in Hotkey.All)
		{
			if (hk.Group != group)
			{
				if (card != null)
				{
					AddRow(t, ref row, card, SizeType.AutoSize, 0);
				}
				group = hk.Group;
				card = Card(8);
				cr = 0;
				Control right = ((row == 0) ? ScopeTag(perProfile: true) : null);
				AddRow(card, ref cr, Header(group, right), SizeType.AutoSize, 0, right != null, 6);
			}
			AddRow(card, ref cr, BuildHotkeyRow(hk), SizeType.AutoSize, 0, fill: true, 3);
		}
		AddRow(t, ref row, card, SizeType.AutoSize, 0, fill: true, 0);
		TableLayoutPanel outer = NewColumn();
		outer.AutoScroll = false;
		outer.Padding = Padding.Empty;
		int orow = 0;
		AddRow(outer, ref orow, Scroller(t), SizeType.Percent, 100, fill: true, 0);
		TableLayoutPanel bottom = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = new Padding(0, 6, 0, 0),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		bottom.Controls.Add(new Label
		{
			Text = "Esc cancels · Backspace clears · mouse buttons work too",
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		}, 0, 0);
		Button restore = ActionButton("Restore defaults", Theme.Primary);
		restore.Margin = Padding.Empty;
		restore.Click += (object? s, EventArgs e) =>
		{
			RestoreDefaultHotkeys();
		};
		bottom.Controls.Add(restore, 1, 0);
		AddRow(outer, ref orow, bottom, SizeType.AutoSize, 0, fill: true, 0);
		return outer;
	}

	private Control BuildHotkeyRow(Hotkey hk)
	{
		HotkeyRow r = new HotkeyRow
		{
			Hotkey = hk
		};
		TableLayoutPanel box = new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		TableLayoutPanel line = new TableLayoutPanel
		{
			ColumnCount = 4,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = new Padding(2, 1, 0, 1),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		r.Name = new Label
		{
			Text = hk.Label,
			ForeColor = Theme.Text,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		};
		line.Controls.Add(r.Name, 0, 0);
		r.Key = Styled("", Theme.Input);
		r.Key.Width = 124;
		r.Key.Height = 24;
		r.Key.Margin = new Padding(2, 1, 2, 1);
		r.Key.FlatAppearance.BorderColor = Color.FromArgb(240, 140, 60);
		r.Key.AccessibleName = hk.Label + " key";
		r.Key.Click += (object? s, EventArgs e) =>
		{
			BeginHotkeyCapture(r);
		};
		line.Controls.Add(r.Key, 1, 0);
		r.Clear = Styled("×", Theme.Row);
		r.Clear.ForeColor = Theme.Text;
		r.Clear.Font = new Font("Segoe UI", 11f);
		r.Clear.Width = 28;
		r.Clear.Height = 24;
		r.Clear.Margin = new Padding(2, 1, 2, 1);
		r.Clear.AccessibleName = "Remove the key for " + hk.Label;
		toolTip.SetToolTip(r.Clear, "Remove the key (turns this command off)");
		r.Clear.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			AssignHotkey(r, 0);
		};
		line.Controls.Add(r.Clear, 2, 0);
		Button reset = Styled("↺", Theme.Row);
		reset.ForeColor = Theme.Text;
		reset.Font = new Font("Segoe UI", 11f);
		reset.Width = 28;
		reset.Height = 24;
		reset.Margin = new Padding(2, 1, 2, 1);
		reset.AccessibleName = "Reset " + hk.Label + " to default";
		toolTip.SetToolTip(reset, "Back to the default key (" + Profile.KeyName(hk.DefaultVk) + ")");
		reset.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			AssignHotkey(r, hk.DefaultVk);
		};
		line.Controls.Add(reset, 3, 0);
		box.Controls.Add(line, 0, 0);
		TableLayoutPanel bar = new TableLayoutPanel
		{
			ColumnCount = 3,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Dock = DockStyle.Fill,
			BackColor = Theme.WarnBg,
			Margin = Padding.Empty,
			Padding = new Padding(10, 2, 2, 2),
			Visible = false,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		r.ConflictText = new Label
		{
			ForeColor = Theme.Warn,
			AutoSize = true,
			Anchor = AnchorStyles.Left
		};
		bar.Controls.Add(r.ConflictText, 0, 0);
		Button swap = Styled("Swap", Theme.Blue);
		swap.Size = new Size(60, 24);
		swap.Click += (object? s, EventArgs e) =>
		{
			SwapHotkeyConflict();
		};
		bar.Controls.Add(swap, 1, 0);
		Button cancel = Styled("Cancel", Color.FromArgb(60, 60, 70));
		cancel.Size = new Size(60, 24);
		cancel.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
		};
		bar.Controls.Add(cancel, 2, 0);
		r.ConflictBar = bar;
		box.Controls.Add(bar, 0, 1);
		r.Warn = WarnLabel();
		box.Controls.Add(r.Warn, 0, 2);
		hotkeyRows.Add(r);
		return box;
	}

	private static Label WarnLabel()
	{
		return new Label
		{
			AutoSize = true,
			MaximumSize = new Size(420, 0),
			Dock = DockStyle.Fill,
			BackColor = Theme.WarnBg,
			ForeColor = Theme.Warn,
			Font = Theme.Small,
			Padding = new Padding(6, 2, 6, 2),
			Margin = new Padding(8, 0, 4, 3),
			Visible = false
		};
	}

	private void BeginHotkeyCapture(HotkeyRow r)
	{
		CancelHotkeyCapture();
		SetBindStatus("");
		capturingRow = r;
		RefreshHotkeyRows();
		capture = (int vk) =>
		{
			capturingRow = null;
			if (vk == (int)Keys.Escape)
			{
				RefreshHotkeyRows();
				return;
			}
			AssignHotkey(r, (vk == (int)Keys.Back) ? 0 : vk);
		};
	}

	private void CancelHotkeyCapture()
	{
		captureVersion++;
		if (pedalKey != null)
		{
			RefreshPedalKey(capturing: false);
		}
		capturingRow = null;
		capture = null;
		SetBindStatus("");
		conflictRow = null;
		conflictWith = null;
		RefreshHotkeyRows();
	}

	// A key already used by another command isn't applied directly: the row shows the clash
	// and the user picks Swap (the other command takes this one's old key) or Cancel.
	private void AssignHotkey(HotkeyRow r, int vk)
	{
		conflictRow = null;
		conflictWith = null;
		if (vk != 0)
		{
			foreach (Hotkey other in Hotkey.All)
			{
				if (other != r.Hotkey && other.Get(profile) == vk)
				{
					conflictRow = r;
					conflictVk = vk;
					conflictWith = other;
					RefreshHotkeyRows();
					return;
				}
			}
		}
		r.Hotkey.Set(profile, vk);
		HotkeysChanged();
	}

	private void SwapHotkeyConflict()
	{
		if (conflictRow != null)
		{
			conflictWith.Set(profile, conflictRow.Hotkey.Get(profile));
			conflictRow.Hotkey.Set(profile, conflictVk);
			conflictRow = null;
			conflictWith = null;
			HotkeysChanged();
		}
	}

	private void RestoreDefaultHotkeys()
	{
		if (MessageBox.Show("Restore the default hotkeys for “" + profile.Name + "”?", "MouseDrive", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			CancelHotkeyCapture();
			foreach (Hotkey hk in Hotkey.All)
			{
				hk.Set(profile, hk.DefaultVk);
			}
			HotkeysChanged();
		}
	}

	private void HotkeysChanged()
	{
		ReleaseHeldKeys();
		SaveActiveProfile();
		RefreshHotkeyUi();
	}

	// A held key's key-up is matched against the current bindings, so after a rebind or a profile switch
	// it may never match again: let go of every vJoy button instead.
	private void ReleaseHeldKeys()
	{
		ReleaseAllButtons();
		ResetPedals();
	}

	// What OnKeyDown does with a key that is both a command and a vJoy button: the command runs and the button doesn't.
	private Hotkey CommandOn(int vk)
	{
		if (vk == 0)
		{
			return null;
		}
		foreach (Hotkey hk in Hotkey.All)
		{
			if (hk.Get(profile) == vk)
			{
				return hk;
			}
		}
		return null;
	}

	private string HotkeyWarning(Hotkey hk)
	{
		int vk = hk.Get(profile);
		if (vk == 0 || CommandOn(vk) != hk)
		{
			return null;
		}
		List<string> buttons = new List<string>();
		foreach (ButtonBinding b in profile.Buttons)
		{
			if (b.Vk == vk)
			{
				buttons.Add("vJoy button " + b.Button + (string.IsNullOrWhiteSpace(b.Label) ? "" : (" (" + b.Label.Trim() + ")")));
			}
		}
		for (int i = 0; i < pedals.Length; i++)
		{
			if (PedalConfig(i).Enabled && PedalConfig(i).Vk == vk)
			{
				buttons.Add(PedalNames[i].ToLowerInvariant());
			}
		}
		if (buttons.Count == 0)
		{
			return null;
		}
		return "⚠  " + Profile.KeyName(vk) + " is also " + string.Join(", ", buttons) + ". This command wins; these bindings won't respond.";
	}

	private string BindingWarning(ButtonBinding b)
	{
		Hotkey hk = CommandOn(b.Vk);
		if (hk == null)
		{
			return null;
		}
		return "⚠  " + Profile.KeyName(b.Vk) + " is also " + hk.Label + ". The command wins; this button won't fire.";
	}

	private void RefreshHotkeyRows()
	{
		foreach (HotkeyRow r in hotkeyRows)
		{
			bool capturing = r == capturingRow;
			bool conflict = r == conflictRow;
			r.Key.Text = (capturing ? "Press a key…" : Profile.KeyName(conflict ? conflictVk : r.Hotkey.Get(profile)));
			r.Key.BackColor = (capturing ? Theme.Blue : (conflict ? Color.FromArgb(58, 44, 30) : Theme.Input));
			r.Key.ForeColor = ((r.Hotkey.Get(profile) == 0 && !capturing && !conflict) ? Theme.Faint : Color.White);
			r.Key.FlatAppearance.BorderSize = (conflict ? 1 : 0);
			r.Clear.Enabled = r.Hotkey.Get(profile) != 0;
			if (conflict)
			{
				r.ConflictText.Text = "⚠  " + Profile.KeyName(conflictVk) + " is already " + conflictWith.Label + ".";
			}
			r.ConflictBar.Visible = conflict;
			string warn = (conflict ? null : HotkeyWarning(r.Hotkey));
			r.Warn.Text = warn ?? "";
			r.Warn.Visible = warn != null;
		}
	}

	private void RefreshHotkeyUi()
	{
		RefreshHotkeyRows();
		UpdateBindWarnings();
		btnToggle.KeyText = ((profile.ToggleVk != 0) ? Profile.KeyName(profile.ToggleVk) : "");
		List<List<(string, bool)>> lines = new List<List<(string, bool)>>();
		List<(string, bool)> line = new List<(string, bool)>();
		AddLegendItems(line, "Drive / standby", profile.ToggleVk);
		AddLegendItems(line, "Center steering", profile.CenterVk);
		if (line.Count > 0)
		{
			lines.Add(line);
		}
		if (lines.Count == 0)
		{
			lines.Add(new List<(string, bool)> { ("No keys set. Assign them on the Keys tab.", false) });
		}
		legend.SetLines(lines);
	}

	private static void AddLegendItems(List<(string, bool)> line, string label, params int[] vks)
	{
		bool any = false;
		foreach (int vk in vks)
		{
			if (vk != 0)
			{
				line.Add((Profile.KeyName(vk), true));
				any = true;
			}
		}
		if (any)
		{
			line.Add((label, false));
		}
	}

	private Control BuildButtonsList()
	{
		TableLayoutPanel t = NewColumn();
		t.AutoScroll = false;
		t.Padding = Padding.Empty;
		int row = 0;
		TableLayoutPanel top = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		TableLayoutPanel info = new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 0, 8, 0)
		};
		lblBindCount = new Label
		{
			AutoSize = true,
			Font = Theme.Strong,
			ForeColor = Theme.Text,
			Margin = new Padding(0, 0, 0, 3)
		};
		info.Controls.Add(lblBindCount, 0, 0);
		info.Controls.Add(new Label
		{
			Text = "Map a key or mouse button to a vJoy button, then bind that button in the game (shift up/down, pit limiter, lights, horn…). The lamp lights while the key is down. Saved with this profile.",
			ForeColor = Theme.Muted,
			Font = Theme.Small,
			AutoSize = true,
			MaximumSize = new Size(330, 0),
			Margin = Padding.Empty
		}, 0, 1);
		top.Controls.Add(info, 0, 0);
		Button add = ActionButton("+  Add key", Theme.Blue);
		add.Margin = Padding.Empty;
		add.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		add.Click += (object? s, EventArgs e) =>
		{
			BeginAddBinding();
		};
		top.Controls.Add(add, 1, 0);
		AddRow(t, ref row, top, SizeType.AutoSize, 0, fill: true, 8);
		lblBindStatus = new Label
		{
			AutoSize = true,
			ForeColor = Color.LightGreen,
			Visible = false
		};
		AddRow(t, ref row, lblBindStatus, SizeType.AutoSize, 0, fill: false, 6);
		bindPanel = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			BackColor = Theme.Bg
		};
		bindPanel.Resize += (object? s, EventArgs e) =>
		{
			FitBindRows();
		};
		AddRow(t, ref row, bindPanel, SizeType.Percent, 100, fill: true, 0);
		return t;
	}

	private void SetBindStatus(string text)
	{
		lblBindStatus.Text = text;
		lblBindStatus.Visible = text.Length > 0;
	}

	private void BeginAddBinding()
	{
		CancelHotkeyCapture();
		SetBindStatus("Press the key to bind…");
		capture = (int vk) =>
		{
			SetBindStatus("");
			if (vk == (int)Keys.Escape || vk == (int)Keys.Back)
			{
				return;
			}
			int num = 1;
			while (num <= vjoyButtonCount && profile.Buttons.Exists((ButtonBinding b) => b.Button == num))
			{
				num++;
			}
			if (num > vjoyButtonCount)
			{
				SetBindStatus("All vJoy buttons are assigned. Remove a binding or change its button number first.");
				return;
			}
			ReleaseHeldKeys();
			profile.Buttons.Add(new ButtonBinding
			{
				Vk = vk,
				Button = num
			});
			SaveActiveProfile();
			RebuildBindingRows();
			RefreshHotkeyRows();
		};
	}

	private int BindRowWidth => Math.Max(200, bindPanel.ClientSize.Width - 2);

	private void FitBindRows()
	{
		foreach (Control c in bindPanel.Controls)
		{
			c.MinimumSize = new Size(BindRowWidth, 0);
			c.MaximumSize = new Size(BindRowWidth, 0);
		}
	}

	private void RebuildBindingRows()
	{
		bindPanel.SuspendLayout();
		foreach (Control c in bindPanel.Controls)
		{
			c.Dispose();
		}
		bindPanel.Controls.Clear();
		bindRows.Clear();
		if (profile.Buttons.Count == 0)
		{
			bindPanel.Controls.Add(new Label
			{
				Text = "No buttons yet. Click + Add key, then press the key you want to use.",
				ForeColor = Theme.Muted,
				AutoSize = true,
				MaximumSize = new Size(BindRowWidth, 0),
				Margin = new Padding(0, 16, 0, 0)
			});
		}
		foreach (ButtonBinding b in profile.Buttons)
		{
			bindPanel.Controls.Add(BuildBindRow(b));
		}
		FitBindRows();
		bindPanel.ResumeLayout();
		UpdateBindWarnings();
		lblBindCount.Text = profile.Buttons.Count + " of " + vjoyButtonCount + " vJoy buttons used";
		keyTabs[1].Text = "vJoy buttons · " + profile.Buttons.Count;
	}

	private Control BuildBindRow(ButtonBinding b)
	{
		TableLayoutPanel rowP = new TableLayoutPanel
		{
			ColumnCount = 6,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = Theme.Row,
			Margin = new Padding(0, 0, 0, 3),
			Padding = new Padding(6, 2, 2, 2),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 16f),
				new ColumnStyle(SizeType.Absolute, 100f),
				new ColumnStyle(SizeType.Absolute, 20f),
				new ColumnStyle(SizeType.Absolute, 74f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 30f)
			}
		};
		Panel lamp = new Panel
		{
			Width = 10,
			Height = 10,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(70, 70, 80)
		};
		rowP.Controls.Add(lamp, 0, 0);
		Button keyBtn = Styled(Profile.KeyName(b.Vk), Theme.Input);
		keyBtn.Size = new Size(96, 26);
		keyBtn.Margin = new Padding(0, 2, 2, 2);
		keyBtn.AccessibleName = "Key for vJoy button " + b.Button;
		keyBtn.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			SetBindStatus("Press the new key…");
			capture = (int vk) =>
			{
				SetBindStatus("");
				if (vk == (int)Keys.Escape)
				{
					return;
				}
				ReleaseHeldKeys();
				b.Vk = (vk == (int)Keys.Back) ? 0 : vk;
				SaveActiveProfile();
				RebuildBindingRows();
				RefreshHotkeyRows();
			};
		};
		rowP.Controls.Add(keyBtn, 1, 0);
		rowP.Controls.Add(new Label
		{
			Text = "→",
			ForeColor = Theme.Faint,
			AutoSize = true,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		}, 2, 0);
		Stepper num = new Stepper
		{
			Maximum = vjoyButtonCount,
			Value = Math.Min(Math.Max(b.Button, 1), vjoyButtonCount),
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 2, 4, 2)
		};
		num.ValueChanged += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			b.Pressed = false;
			UpdateButtonState(b.Button);
			b.Button = num.Value;
			SaveActiveProfile();
			RefreshHotkeyRows();
		};
		rowP.Controls.Add(num, 3, 0);
		Panel nameBox = new Panel
		{
			BackColor = Theme.Input,
			Height = 26,
			Dock = DockStyle.Fill,
			Padding = new Padding(6, 5, 6, 2),
			Margin = new Padding(0, 2, 2, 2)
		};
		TextBox name = new TextBox
		{
			Text = b.Label ?? "",
			BorderStyle = BorderStyle.None,
			BackColor = Theme.Input,
			ForeColor = Color.White,
			Dock = DockStyle.Fill,
			PlaceholderText = "Add a name…",
			AccessibleName = "Name for vJoy button " + b.Button
		};
		name.TextChanged += (object? s, EventArgs e) =>
		{
			b.Label = name.Text;
			SaveActiveProfile();
			RefreshHotkeyRows();
		};
		nameBox.Controls.Add(name);
		rowP.Controls.Add(nameBox, 4, 0);
		Button del = Styled("×", Theme.Row);
		del.ForeColor = Theme.Text;
		del.Font = new Font("Segoe UI", 11f);
		del.Size = new Size(26, 26);
		del.Margin = new Padding(2);
		del.AccessibleName = "Remove this button";
		toolTip.SetToolTip(del, "Remove this button");
		del.Click += (object? s, EventArgs e) =>
		{
			CancelHotkeyCapture();
			profile.Buttons.Remove(b);
			UpdateButtonState(b.Button);
			SaveActiveProfile();
			RebuildBindingRows();
			RefreshHotkeyRows();
		};
		rowP.Controls.Add(del, 5, 0);
		Label warn = WarnLabel();
		warn.Margin = new Padding(0, 0, 4, 2);
		rowP.Controls.Add(warn, 0, 1);
		rowP.SetColumnSpan(warn, 6);
		bindRows.Add(new BindRow
		{
			B = b,
			Lamp = lamp,
			Warn = warn
		});
		return rowP;
	}

	private void UpdateBindWarnings()
	{
		foreach (BindRow r in bindRows)
		{
			string w = BindingWarning(r.B);
			r.Warn.Text = w ?? "";
			r.Warn.Visible = w != null;
		}
	}

	// ---------- HUD ----------

	private Control BuildHudPage()
	{
		TableLayoutPanel t = NewColumn();
		int row = 0;
		hudPreview = new HudPreview
		{
			Height = 132
		};
		AddRow(t, ref row, hudPreview, SizeType.AutoSize, 0);
		TableLayoutPanel card = Card();
		int cr = 0;
		AddRow(card, ref cr, Header("Style", ScopeTag(perProfile: false)), SizeType.AutoSize, 0, fill: true, 6);
		segStyle = new Segmented
		{
			Items = new string[4] { "Full", "Classic", "Column", "Minimal" },
			Text = "HUD style",
			AccessibleName = "HUD style"
		};
		segStyle.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			settings.HudStyle = (HudStyle)segStyle.SelectedIndex;
			ApplyLook();
		};
		AddRow(card, ref cr, segStyle, SizeType.AutoSize, 0, fill: true, 8);
		capWheel = new Caption
		{
			Text = "Wheel",
			Hint = "Full and Classic"
		};
		segWheel = AddOptionRow(card, ref cr, capWheel, new string[2] { "Classic", "GT" }, "HUD wheel");
		toolTip.SetToolTip(segWheel, "Classic: a wood-rim wheel of an old sports car. GT: a modern race-car wheel");
		segWheel.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			settings.HudWheel = (HudWheel)segWheel.SelectedIndex;
			ApplyLook();
		};
		capBar = new Caption
		{
			Text = "Position bar",
			Hint = " "
		};
		segBar = AddOptionRow(card, ref cr, capBar, new string[3] { "Auto", "Show", "Hide" }, "Position bar");
		toolTip.SetToolTip(segBar, "The axis bar under the wheel, in Full and Classic. Auto shows it when this profile's wheel turns more than 360°, where the wheel alone looks the same a turn apart");
		segBar.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			settings.HudPositionBar = (HudElementMode)segBar.SelectedIndex;
			RefreshHudElementHints();
			ApplyLook();
		};
		swHudZoom = Switch("Wheel zoom", "only the top 20% of the rim", (object? s, EventArgs e) =>
		{
			settings.HudZoom = swHudZoom.Checked;
			ApplyLook();
		});
		swHudZoom.Margin = new Padding(0, 0, 0, 6);
		AddRow(card, ref cr, swHudZoom, SizeType.AutoSize, 0);
		// Size is global and lives in overlay.cfg with the position.
		tbHudSize = AddSliderRow(card, ref cr, "Size", null, OverlayForm.MinSizePercent, OverlayForm.MaxSizePercent, OverlayForm.LoadSizePercent(), "%", autosave: false);
		tbHudSize.DefaultValue = 100;
		toolTip.SetToolTip(tbHudSize, "Double-click to reset to 100%");
		tbHudSize.ValueChanged += (object? s, EventArgs e) =>
		{
			EnsureOverlay();
			overlay.SetSizePercent(tbHudSize.Value);
			ConfigurePreview();
		};
		tbHudOpacity = AddSliderRow(card, ref cr, "Opacity", null, 30, 100, 90, "%", autosave: false);
		tbHudOpacity.ValueChanged += (object? s, EventArgs e) =>
		{
			settings.HudOpacity = tbHudOpacity.Value;
			overlay?.SetOpacityPercent(settings.HudOpacity);
			ConfigurePreview();
			SaveSettingsSoon();
		};
		tbHudBackground = AddSliderRow(card, ref cr, "Background", "0% = outlines only", 0, 100, 100, "%", autosave: false);
		toolTip.SetToolTip(tbHudBackground, "The panel behind the HUD. Below 50%, text and marks get a dark outline so they still read");
		tbHudBackground.ValueChanged += (object? s, EventArgs e) =>
		{
			settings.HudBackground = tbHudBackground.Value;
			ApplyLook();
		};
		AddRow(t, ref row, card, SizeType.AutoSize, 0);
		TableLayoutPanel rotationCard = Card();
		int gr = 0;
		AddRow(rotationCard, ref gr, Header("Wheel rotation", ScopeTag(perProfile: true)), SizeType.AutoSize, 0, fill: true, 6);
		numRotation = new Stepper
		{
			Label = "",
			Unit = "°",
			Description = "Wheel rotation",
			Increment = RotationStep,
			Maximum = Profile.MaxWheelRotation,
			Minimum = Profile.MinWheelRotation,
			Value = new Profile().WheelRotation,
			Height = 30
		};
		toolTip.SetToolTip(numRotation, "The game's steering rotation, lock to lock. Click a side, use the arrow keys or the mouse wheel: " + RotationStep + "° a step");
		AddCaptionRow(rotationCard, ref gr, new Caption
		{
			Text = "Rotation",
			Hint = "lock to lock, as set in the game"
		}, numRotation, RotationStepperWidth);
		segRotation = new Segmented
		{
			Items = Array.ConvertAll(RotationPresets, (int d) => d + "°"),
			Text = "Rotation presets",
			AccessibleName = "Rotation presets",
			Height = 30,
			Margin = new Padding(0, 0, 0, 6)
		};
		AddRow(rotationCard, ref gr, segRotation, SizeType.AutoSize, 0);
		Label rotationNote = new Label
		{
			Text = "The HUD and Drive wheels turn like the game's. The vJoy axis doesn't change.",
			AutoSize = true,
			ForeColor = Theme.Faint,
			Font = Theme.Small,
			Margin = Padding.Empty
		};
		AddRow(rotationCard, ref gr, rotationNote, SizeType.AutoSize, 0, fill: false);
		AddRow(t, ref row, rotationCard, SizeType.AutoSize, 0);
		numRotation.ValueChanged += (object? s, EventArgs e) =>
		{
			SyncRotationPresets();
			if (!loading)
			{
				SaveActiveProfile();
				RefreshHudElementHints();
				RefreshLook();
			}
		};
		segRotation.SelectedIndexChanged += (object? s, EventArgs e) =>
		{
			if (segRotation.SelectedIndex >= 0)
			{
				numRotation.Value = RotationPresets[segRotation.SelectedIndex];
			}
		};
		TableLayoutPanel pedalCard = Card();
		int pc = 0;
		AddRow(pedalCard, ref pc, Header("Pedals on HUD"), SizeType.AutoSize, 0, fill: false, 6);
		for (int i = 0; i < segHudPedals.Length; i++)
		{
			int index = i;
			capHudPedals[i] = new Caption
			{
				Text = PedalNames[i],
				Hint = " "
			};
			segHudPedals[i] = AddOptionRow(pedalCard, ref pc, capHudPedals[i], new string[3] { "Auto", "Show", "Hide" }, PedalNames[i] + " on HUD");
			toolTip.SetToolTip(segHudPedals[i], "Auto shows it when this profile uses the pedal");
			segHudPedals[i].SelectedIndexChanged += (object? s, EventArgs e) =>
			{
				SetHudPedal(index, (HudElementMode)segHudPedals[index].SelectedIndex);
				RefreshHudElementHints();
				SaveSettingsSoon();
			};
		}
		AddRow(t, ref row, pedalCard, SizeType.AutoSize, 0);
		TableLayoutPanel display = Card();
		int dr = 0;
		AddRow(display, ref dr, Header("Display"), SizeType.AutoSize, 0, fill: false, 6);
		swShowHud = Switch("Show HUD", "while driving", (object? s, EventArgs e) =>
		{
			SetShowHud(swShowHud.Checked);
		});
		swHudNumbers = Switch("Show numbers", "steering and pedals", (object? s, EventArgs e) =>
		{
			settings.HudNumbers = swHudNumbers.Checked;
			ApplyLook();
		});
		swHudFade = Switch("Fade when straight", "dims at center", (object? s, EventArgs e) =>
		{
			settings.HudFade = swHudFade.Checked;
			overlay?.SetFade(settings.HudFade);
			SaveSettingsSoon();
		});
		swHudStandby = Switch("Show in standby", "gray, with the drive key", (object? s, EventArgs e) =>
		{
			settings.HudInStandby = swHudStandby.Checked;
			UpdateOverlayVisibility();
			SaveSettingsSoon();
		});
		AddRow(display, ref dr, SwitchPair(swShowHud, swHudNumbers), SizeType.AutoSize, 0, fill: true, 4);
		AddRow(display, ref dr, SwitchPair(swHudFade, swHudStandby), SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, display, SizeType.AutoSize, 0);
		TableLayoutPanel position = Card();
		int pr = 0;
		AddRow(position, ref pr, Header("Position"), SizeType.AutoSize, 0, fill: false, 6);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = false
		};
		btnPositionHud = ActionButton("Position HUD…", Theme.Blue);
		toolTip.SetToolTip(btnPositionHud, "Shows the HUD so you can drag it; click Done on the HUD when it's in place");
		btnPositionHud.Click += (object? s, EventArgs e) =>
		{
			if (hudMoving)
			{
				EndHudPosition();
			}
			else
			{
				BeginHudPosition();
			}
		};
		Button resetPos = ActionButton("Reset position", Theme.Primary);
		toolTip.SetToolTip(resetPos, "Back to the top center of the main screen");
		resetPos.Click += (object? s, EventArgs e) =>
		{
			EnsureOverlay();
			overlay.ResetPosition();
			SetStatus("HUD moved back to the top of the main screen.", Theme.Muted);
		};
		buttons.Controls.Add(btnPositionHud);
		buttons.Controls.Add(resetPos);
		AddRow(position, ref pr, buttons, SizeType.AutoSize, 0, fill: true, 0);
		AddRow(t, ref row, position, SizeType.AutoSize, 0, fill: true, 0);
		return Scroller(t);
	}

	// "Show HUD" appears on the Drive tab and the HUD tab; both switches are the same setting.
	private void SetShowHud(bool on)
	{
		settings.ShowHud = on;
		swShowHud.Checked = on;
		swShowHudDrive.Checked = on;
		UpdateOverlayVisibility();
		SaveSettingsSoon();
	}

	private void ConfigurePreview()
	{
		hudPreview.Configure(CurrentLook(), settings.HudOpacity, tbHudSize.Value);
	}

	// Style, wheel, zoom, bar, numbers and background all change the drawing; the wheel options only apply to
	// the styles that have one.
	private void ApplyLook()
	{
		bool hasWheel = settings.Look.HasWheel;
		segWheel.Enabled = hasWheel;
		capWheel.Dim = !hasWheel;
		segBar.Enabled = hasWheel;
		capBar.Dim = !hasWheel;
		swHudZoom.Enabled = hasWheel;
		if (loading)
		{
			return;
		}
		RefreshLook();
		SaveSettingsSoon();
	}

	// The look also follows the profile's rotation, so a profile switch or a new rotation redraws without
	// touching the app settings.
	private void RefreshLook()
	{
		overlay?.SetLook(CurrentLook());
		ConfigurePreview();
	}

	// One line: a caption with a hint on the left, a small segmented choice on the right.
	private Segmented AddOptionRow(TableLayoutPanel t, ref int row, Caption caption, string[] items, string name)
	{
		Segmented seg = new Segmented
		{
			Items = items,
			Text = name,
			AccessibleName = name,
			Height = 30
		};
		AddCaptionRow(t, ref row, caption, seg, HudOptionWidth);
		return seg;
	}

	// A caption with a hint on the left and a control of the given width on the right.
	private void AddCaptionRow(TableLayoutPanel t, ref int row, Caption caption, Control control, float width)
	{
		TableLayoutPanel line = new TableLayoutPanel
		{
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			BackColor = t.BackColor,
			Margin = new Padding(0, 0, 0, 4),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, width)
			}
		};
		caption.BackColor = t.BackColor;
		caption.Dock = DockStyle.Fill;
		caption.Margin = Padding.Empty;
		control.Dock = DockStyle.Fill;
		control.Margin = new Padding(0, 1, 0, 1);
		line.Controls.Add(caption, 0, 0);
		line.Controls.Add(control, 1, 0);
		AddRow(t, ref row, line, SizeType.AutoSize, 0, fill: true, 4);
	}

	private HudElementMode GetHudPedal(int index) => index switch
	{
		0 => settings.HudThrottle,
		1 => settings.HudBrake,
		_ => settings.HudClutch
	};

	private void SetHudPedal(int index, HudElementMode mode)
	{
		switch (index)
		{
		case 0:
			settings.HudThrottle = mode;
			break;
		case 1:
			settings.HudBrake = mode;
			break;
		default:
			settings.HudClutch = mode;
			break;
		}
	}

	// The hints say what Auto does with the active profile right now, so a hidden pedal doesn't look like a bug.
	private void RefreshHudElementHints()
	{
		if (capHudPedals[0] == null)
		{
			return;
		}
		for (int i = 0; i < capHudPedals.Length; i++)
		{
			PedalSettings config = PedalConfig(i);
			string auto = ((!config.Enabled) ? "Hidden: pedal off in this profile" : ((config.Vk == 0) ? "Hidden: no key set" : ("Shown: pedal on, key " + Profile.KeyName(config.Vk))));
			string hint = GetHudPedal(i) switch
			{
				HudElementMode.Show => "Always shown",
				HudElementMode.Hide => "Always hidden",
				_ => auto,
			};
			if (capHudPedals[i].Hint != hint)
			{
				capHudPedals[i].Hint = hint;
			}
		}
		int rotation = profile.WheelRotation;
		string barHint = settings.HudPositionBar switch
		{
			HudElementMode.Show => "Always shown",
			HudElementMode.Hide => "Always hidden",
			_ => (HudBarOn(HudElementMode.Auto, rotation) ? "Shown: " : "Hidden: ") + rotation + "° wheel",
		};
		if (capBar.Hint != barHint)
		{
			capBar.Hint = barHint;
		}
	}

	private void ApplySettingsToUi()
	{
		loading = true;
		segStyle.SelectedIndex = (int)settings.HudStyle;
		segWheel.SelectedIndex = (int)settings.HudWheel;
		swHudZoom.Checked = settings.HudZoom;
		tbHudBackground.Value = settings.HudBackground;
		for (int i = 0; i < segHudPedals.Length; i++)
		{
			segHudPedals[i].SelectedIndex = (int)GetHudPedal(i);
		}
		segBar.SelectedIndex = (int)settings.HudPositionBar;
		RefreshHudElementHints();
		ApplyLook();
		tbHudOpacity.Value = settings.HudOpacity;
		swShowHud.Checked = settings.ShowHud;
		swShowHudDrive.Checked = settings.ShowHud;
		swHudNumbers.Checked = settings.HudNumbers;
		swHudFade.Checked = settings.HudFade;
		swHudStandby.Checked = settings.HudInStandby;
		TopMost = settings.TopMost;
		tray.Visible = settings.CloseToTray;
		ConfigurePreview();
		loading = false;
		if (settings.Compact)
		{
			SetCompact(on: true);
		}
	}

	private void ApplyProfileToUi()
	{
		loading = true;
		tbSensitivity.Value = profile.Sensitivity;
		tbDeadzone.Value = profile.Deadzone;
		tbExpo.Value = profile.Expo;
		tbSmooth.Value = profile.Smoothing;
		tbSelfCenter.Value = profile.SelfCentering;
		swInvert.Checked = profile.InvertSteering;
		swHideCursor.Checked = profile.HideCursor;
		numRotation.Value = profile.WheelRotation;
		SyncRotationPresets();
		ApplyPedalToUi();
		CenterSteering();
		RebuildBindingRows();
		ReleaseHeldKeys();
		CancelHotkeyCapture();
		RefreshHotkeyUi();
		UpdateRuler();
		UpdateFullLock();
		RefreshHudElementHints();
		RefreshProfileUi();
		loading = false;
		RefreshLook();
		lblSaved.Text = "Saved";
		lblSaved.ForeColor = Theme.Faint;
	}

	// ---------- live values (every third tick) ----------

	private void RefreshLiveUi(HudState st)
	{
		if (!Visible || WindowState == FormWindowState.Minimized)
		{
			return;
		}
		if (compact)
		{
			compactMonitor.Rotation = profile.WheelRotation;
			compactMonitor.State = st;
			SetText(lblCompactSteering, "Steering " + HudRenderer.Percent(st.Steering));
			return;
		}
		switch (selectedTab)
		{
		case 0:
		{
			monitor.View = new DriveView(st.Steering, wheel, st.Active, settings.HudWheel, DrivePedalView(0), DrivePedalView(1), DrivePedalView(2), profile.WheelRotation);
			int n = Math.Min(vjoyButtonCount, LampStrip.MaxShown);
			bool[] bound = new bool[n];
			bool[] on = new bool[n];
			string[] names = new string[n];
			foreach (ButtonBinding b in profile.Buttons)
			{
				int i = b.Button - 1;
				if (i >= 0 && i < n)
				{
					bound[i] = true;
					on[i] |= b.Pressed;
					if (names[i] == null && b.Vk != 0)
					{
						names[i] = Profile.KeyName(b.Vk);
					}
				}
			}
			lamps.SetLamps(bound, on, names);
			break;
		}
		case 1:
			ruler.SetInput(wheel, active);
			break;
		case 2:
			RefreshPedalUi();
			break;
		case 3:
			foreach (BindRow r in bindRows)
			{
				Color c = (r.B.Pressed ? Theme.Green : Color.FromArgb(70, 70, 80));
				if (r.Lamp.BackColor != c)
				{
					r.Lamp.BackColor = c;
				}
			}
			break;
		case 4:
		{
			string note = ((!settings.ShowHud) ? "The HUD is off: turn on Show HUD" : ((!active && !settings.HudInStandby && !hudMoving) ? "Hidden in standby: turn on Show in standby" : ""));
			hudPreview.ShowFrame(st, (settings.HudFade && active && OverlayForm.Straight(st)) ? OverlayForm.FadedAlpha : 1.0, note);
			break;
		}
		}
	}

	// ---------- compact window and tray ----------

	private Control BuildCompact()
	{
		TableLayoutPanel t = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			BackColor = Theme.Bg,
			Padding = new Padding(12, 10, 12, 10),
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 120f),
				new ColumnStyle(SizeType.Absolute, 80f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize)
			}
		};
		t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		TableLayoutPanel state = new TableLayoutPanel
		{
			ColumnCount = 1,
			AutoSize = true,
			Anchor = (AnchorStyles.Left | AnchorStyles.Right),
			Margin = Padding.Empty
		};
		lblCompactState = new Label
		{
			AutoSize = true,
			Font = Theme.Header,
			Margin = new Padding(0, 0, 0, 2)
		};
		lblCompactProfile = new Label
		{
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(116, 20),
			Font = Theme.Strong,
			ForeColor = Color.White,
			Margin = Padding.Empty
		};
		state.Controls.Add(lblCompactState, 0, 0);
		state.Controls.Add(lblCompactProfile, 0, 1);
		t.Controls.Add(state, 0, 0);
		compactMonitor = new SteeringMonitor
		{
			Size = new Size(64, 67),
			BackColor = Theme.Bg,
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		};
		t.Controls.Add(compactMonitor, 1, 0);
		lblCompactSteering = new Label
		{
			AutoSize = true,
			ForeColor = Theme.Text,
			Font = Theme.Small,
			Anchor = AnchorStyles.Left,
			Margin = Padding.Empty
		};
		t.Controls.Add(lblCompactSteering, 2, 0);
		Button expand = ActionButton("Expand", Theme.Primary);
		expand.Anchor = AnchorStyles.Right;
		expand.Margin = Padding.Empty;
		toolTip.SetToolTip(expand, "Back to the full window");
		expand.Click += (object? s, EventArgs e) =>
		{
			SetCompact(on: false);
		};
		t.Controls.Add(expand, 3, 0);
		return t;
	}

	private void SetCompact(bool on)
	{
		compact = on;
		settings.Compact = on;
		SaveSettingsSoon();
		SuspendLayout();
		root.SuspendLayout();
		for (int i = 0; i < HeaderRows.Length; i++)
		{
			root.RowStyles[i].Height = (on ? 0 : HeaderRows[i]);
			headerParts[i].Visible = !on;
		}
		pageHost.Visible = !on;
		root.RowStyles[4].Height = (on ? CompactBodyHeight : 0);
		compactPanel.Visible = on;
		SetWindowSize();
		root.ResumeLayout();
		ResumeLayout();
		RefreshCompact();
	}

	private int FullHeight => ((settings.WindowHeight == 0) ? DefaultHeight : Math.Clamp(settings.WindowHeight, MinHeight, MaxHeight));

	// Sizes the window for the current mode and sets the limits the user can drag it to (none in compact).
	private void SetWindowSize()
	{
		MinimumSize = Size.Empty;
		MaximumSize = Size.Empty;
		ClientSize = (compact ? new Size(CompactWidth, CompactBodyHeight + StatusHeight) : new Size(FullWidth, FullHeight));
		Size frame = Size - ClientSize;
		MinimumSize = (compact ? Size : new Size(Size.Width, MinHeight + frame.Height));
		MaximumSize = (compact ? Size : new Size(Size.Width, MaxHeight + frame.Height));
	}

	protected override void OnResizeEnd(EventArgs e)
	{
		base.OnResizeEnd(e);
		if (!compact && ClientSize.Height != FullHeight)
		{
			settings.WindowHeight = ClientSize.Height;
			SaveSettingsSoon();
		}
	}

	private void RefreshCompact()
	{
		lblCompactState.Text = (active ? "● DRIVING" : "● STANDBY");
		lblCompactState.ForeColor = (active ? Color.LightGreen : Theme.Muted);
	}

	private void BuildTray()
	{
		tray = new NotifyIcon
		{
			Text = "MouseDrive",
			Icon = BrandIcon.Tray(active: false),
			Visible = false
		};
		ContextMenuStrip m = DarkMenuRenderer.NewMenu();
		ToolStripMenuItem show = new ToolStripMenuItem("Show MouseDrive", null, (object? s, EventArgs e) =>
		{
			ShowFromTray();
		})
		{
			Font = Theme.Strong
		};
		ToolStripMenuItem drive = new ToolStripMenuItem("Activate", null, (object? s, EventArgs e) =>
		{
			ToggleActive();
		});
		m.Items.Add(show);
		m.Items.Add(drive);
		m.Items.Add(new ToolStripSeparator());
		m.Items.Add("Exit", null, (object? s, EventArgs e) =>
		{
			ExitApp();
		});
		m.Opening += (object? s, System.ComponentModel.CancelEventArgs e) =>
		{
			drive.Text = (active ? "Stop driving" : "Activate");
			drive.Enabled = acquired;
		};
		tray.ContextMenuStrip = m;
		tray.DoubleClick += (object? s, EventArgs e) =>
		{
			ShowFromTray();
		};
	}
}
