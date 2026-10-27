# MouseDrive

A .NET 8 WinForms x64 app that turns the mouse into a steering wheel for racing games through the
**vJoy** virtual joystick: mouse left/right → X axis, keys and mouse buttons → vJoy buttons.

It is a spin-off of **MouseYokeEnhanced** (the flight-sim app, `Desktop\MouseYokeEnhanced`,
github.com/rangelsantos/MouseYokeEnhanced). The UI framework (controls, HUD overlay, settings, About,
key lists) comes from there; the core (`MainForm.cs`, `KeyboardHook`, `CursorHider`, `VJoy`,
`Profile`, `ProfileManager`, `Program`) was written fresh, so that no code from the decompiled
original MouseYoke remains. Keep it that way: don't copy code from `MouseYoke/` or from files of
MouseYokeEnhanced that came from the decompile.

The user writes in Portuguese (pt-BR). The app UI and the manual are in English; keep them that way.

## Status and roadmap

- **Done (foundation):** new repo, steering on X from Raw Input deltas, cursor locked to one pixel,
  sensitivity/expo/deadzone/smoothing/self-centering, center key, vJoy buttons, Wheel/Bar HUD,
  profiles, tray, compact window, progressive throttle/brake/clutch on Y/Z/Rz with a Pedals tab.
- **Next (MVP):** steer from `WM_INPUT` directly (lower latency than the ~64 Hz timer), sensitivity in
  real units (cm or degrees, with the mouse DPI), an axis sweep to help binding in games, testing in
  real games and a setup section per game in the manual, screenshots for the README.
- **Phase 2 remaining:** gear shifts on the mouse wheel.
- **Phase 3, on demand:** XInput output (ViGEm) for games that only accept an Xbox pad; speed-sensitive
  steering from game telemetry.

## Layout

- `MouseDrive/` — the project (`MouseDrive.csproj`, sources directly in it, namespace `MouseDrive`).
- `MouseDrive/MouseDrive_Manual.md` — the user manual, copied next to the exe. When a feature changes,
  update it, and the README if it mentions it. Both UTF-8.
- `MouseDrive/Assets/Brand/` — `make-icon.ps1` draws the icon from vectors and writes
  `MouseDrive.ico` (exe icon and embedded resource) and `mousedrive-icon-512.png` (README). It is a
  placeholder; rerun the script after changing the drawing.
- `third_party/vJoy/vJoyInterface.dll` — the vJoy SDK library (x64), copied next to the exe by the build.
- `.github/workflows/release.yml` — on every push to `main`, publishes a release `v<MAJOR>.<n>` (MAJOR
  is 0 for now) with `MouseDrive-v<MAJOR>.<n>.zip`, whose folder is always `MouseDrive\`. It stamps the
  version into `AssemblyInfo.cs` in CI only (git keeps `0.0.0`, shown as "Development build").
  **There is no remote yet**: the repo is local only until the user creates one.

## Build

```powershell
dotnet build MouseDrive\MouseDrive.csproj -c Release
```

- Needs the **.NET 10 SDK** (`LangVersion` 14.0); the target is `net8.0-windows7.0`, so running
  needs only the .NET 8 Desktop runtime.
- `nuget.config` exists because this machine has no NuGet sources configured. Keep it.
- Output: `MouseDrive/bin/Release/net8.0-windows7.0/` (ignored by git). Profiles, `overlay.cfg` (HUD
  position and size) and `settings.json` (app-wide settings) are written next to the exe at runtime.
- The build has **zero warnings**; keep it so. `<Nullable>annotations</Nullable>` allows the `object?`
  lambdas without CS8632, and the P/Invoke structs silence CS0649 locally.

Run binding/profile regression checks with `dotnet run --project MouseDrive.Tests -c Release`.
They use the real forms with substitutes for vJoy, cursor hiding and global hooks; test data stays
in the test build output. To check UI changes, copy the build output to a scratch folder, launch it, open a
tab by invoking its button through UI Automation (`InvokePattern` on "Drive", "Steering", "Pedals", "Keys", "HUD";
the Keys sub-tabs "Commands" and "vJoy buttons · n" are `TabButton`s too; on Pedals, click a pedal in "Pedal selector" (columns: clutch, brake, throttle); "Settings" opens the ⚙
menu), screenshot the window with `Graphics.CopyFromScreen`, then `CloseMainWindow()`. Owner-drawn
controls (`Segmented`, `SteeringMonitor`...) are found by their `Text`; click a segment with
`PostMessage(WM_LBUTTONDOWN/UP)` on its handle so the real mouse doesn't move. Running the app
installs a global keyboard hook, acquires vJoy device 1 and stays on top. **Never activate driving
(F9) in an automated run**: it hides the system cursor and locks it to the middle of the screen.

## Architecture

| File | Role |
|---|---|
| `MainForm.cs` | Logic: steering loop, Raw Input, vJoy setup and checks, key handling, profile actions, HUD visibility |
| `MainForm.Ui.cs` | The screens, built in code (no designer): header, tabs Drive/Steering/Pedals/Keys/HUD, status bar, compact window, tray, hotkey and button lists, ⚙ menu and About |
| `DarkControls.cs` | Owner-drawn controls: `Slider`, `RangeSlider`, `ToggleSwitch`, `Segmented`, `TabButton`, `ActivateButton`, `SteeringMonitor`, `TravelRuler`, `LampStrip`, `KeyLegend`, `Stepper`, `HudPreview`, dark menus |
| `MainForm.Pedals.cs` / `MainForm.Pedals.Ui.cs` | Pedal input/output and the dedicated Pedals tab |
| `PedalSettings.cs` / `PedalState.cs` / `PedalControls.cs` | Per-profile pedal settings, elapsed-time ramps, the pedal selector (`PedalPicker`, live bars) and the force-over-time graph |
| `HudRenderer.cs` | Draws the HUD styles (`HudStyle`: Wheel, Bar) and the Drive monitor from a `HudState` |
| `OverlayForm.cs` | Click-through HUD: per-pixel-alpha layered window (`UpdateLayeredWindow`), never activates; position mode with Done; notices |
| `Theme.cs` | Palette and fonts |
| `BrandIcon.cs` | Window and tray icons from the embedded `MouseDrive.ico`, and the "driving" variant with a red dot |
| `AppSettings.cs` | App-wide settings in `<exe>\settings.json` (HUD, on top, tray, compact, window height) |
| `Profile.cs` / `ProfileManager.cs` | Per-car/game settings, JSON files in `<exe>\Profiles\`, `_active.txt`; import/export, rename |
| `Hotkey.cs` | The rebindable commands (`Hotkey.All`) |
| `KeyboardHook.cs` | `WH_KEYBOARD_LL` hook; drops auto-repeat; `ReleaseStaleKeys()`; mouse buttons join through `MouseButton()` |
| `CursorHider.cs` | Swaps the system cursors for a blank one; `Restore()` reloads the scheme |
| `VJoy.cs` | P/Invoke into `vJoyInterface.dll`; device 1 (`VJoy.Device`), `ToAxis()` |

- **Threading:** the loop (`Loop_Tick`, a WinForms `Timer` at 10 ms that really runs at ~64 Hz), the
  hook callback and `WM_INPUT` all run on the UI thread. Keep hook handlers fast: a slow UI thread
  lags the whole system's keyboard.
- **Steering:** `WM_INPUT` adds the mouse's X counts to `rawDX`; each tick, while active, `wheel`
  (-1..1) gains `dx / CountsToFullLock(sensitivity)`, decays toward 0 if Self-centering is on, goes
  through `Shape()` (deadzone + expo) and smoothing into `steering`, which goes to the vJoy X axis.
  Rates are per second and integrated over the real `dt`; never go back to per-tick constants.
  Toggling drive/standby, Center steering, Invert and a profile switch all center the wheel.
- **Cursor:** while active, `LockCursor()` clips the pointer to one pixel in the middle of the primary
  screen (re-asserted every tick) and `CursorHider` hides it if the profile asks; both are undone on
  standby and on close.
- **Absolute input:** remote desktop (this dev machine is used through it: its only mouse is the
  "Remote Desktop Mouse Device"), remote-control apps and pen tablets send positions, not counts.
  `OnAbsoluteMove()` turns the change in position (pixels) into `rawDX`, and `absoluteInput` skips the
  cursor lock, since the pointer must move for the position to change. A relative move switches back.
  When testing with `SendInput`, the `INPUT` struct is 40 bytes on x64 (no trailing padding field).
- **Stuck keys:** `KeyboardHook.ReleaseStaleKeys()` runs every tick and fires the missing `KeyUp`.
- **Live UI:** every third tick the loop builds a `HudState`, updates the overlay and calls
  `RefreshLiveUi()`, which only touches the visible tab.
- **HUD visibility** is decided in one place, `UpdateOverlayVisibility()`: while driving, while being
  positioned, in standby if "Show in standby" is on, and briefly for a notice (`HudToast`).
- **Saving:** `SaveActiveProfile()` copies the UI into the profile at once and writes the file 300 ms
  later (`saveTimer`); `FlushSave()` writes now. `UseProfile()` writes the old profile before switching.
- **Window size:** fixed width; the full window resizes in height only (`MinHeight`..`MaxHeight`),
  enforced by `SetWindowSize()` and `SizingHit()`. Every page must scroll (`Scroller()`).
- `OverlayForm` takes the style in its constructor: `overlay.cfg` stores the top-left corner, and a
  later style change keeps the center.

## Pedals

- Throttle, brake and clutch use fixed vJoy Y/Z/Rz axes. New/older profiles default to disabled and unassigned.
- Initial force applies on key-down. The profile stores times (`PressMs` initial→max, `ReleaseMs` max→zero), not rates, so a new force range keeps the feel; rates are derived. Older profiles with `ApplyRate`/`ReleaseRate` are converted on load (`PedalSettings.Upgrade()`) and not written again. Release reaches zero.
- Standby runs the visual input preview but writes released axes. Commands win over pedal keys; shared pedal/button keys operate both.
- Reset pedal force and held state on driving transitions, profile switches, rebinds and pedal setting changes. Never send a missing axis.

## Hotkeys (per profile)

- The keys live on `Profile` (`ToggleVk`, `CenterVk`); `0` means unassigned. Defaults come from the
  property initializers, and `Hotkey.DefaultVk` reads them from a fresh `Profile`.
- **To add a command:** add an `int` property with a default to `Profile`, an entry to `Hotkey.All`,
  handle it in `MainForm.OnKeyDown`/`OnKeyUp`, and list it in `RefreshHotkeyUi()` if it belongs in
  "Keys in use".
- In key capture, Esc cancels and Backspace clears, so neither can be bound.
- Mouse buttons (right, middle, X1, X2 as VK 2/4/5/6) come from Raw Input and go through the same
  `OnKeyDown`/`OnKeyUp`. The primary button is left out. Raw Input can't swallow the click.
- Conflicts among the hotkeys block the change (Swap/Cancel). A hotkey that is also a vJoy button
  only warns (the command wins). `CommandOn()` mirrors `OnKeyDown`; keep them in step.
- After any rebind or profile switch, call `ReleaseHeldKeys()`.
- **Global hotkeys that work in every app are intentional** (inherited decision). Don't add a
  foreground-window filter unless asked.

## Conventions

- Code style follows the UI code it shares with MouseYokeEnhanced: tabs, file-scoped
  `namespace MouseDrive;`, explicit lambdas `(object? s, EventArgs e) => { ... }`, `nint`, ternaries in
  parentheses. Comments are rare and explain *why*. Named constants, not magic numbers.
- UI palette lives in `Theme` (background `18,18,22`, bars `28,28,36`, rows `30,30,38`, inputs
  `45,45,55`, primary `45,55,75`, blue `45,70,110`, accent `90,154,214`, cyan for live output, amber
  for the wheel's top mark, green for the marker). Use the `Theme` fields, not new literals.
- Build UI with the helpers in `MainForm.Ui.cs`: `NewColumn()` + `Scroller()`, `Card()`, `AddRow()`,
  `AddSliderRow()`, `Switch()`, `SwitchPair()`, `Header()`, `Styled()`/`ActionButton()`.
- Page rules: every group is a `Card()` with its `Header()`; a settings page's first header carries
  `ScopeTag()`; `Theme.Blue` for the one main action of a screen, `Theme.Primary` for the others;
  on/off is a `ToggleSwitch`; app-wide window options live in the ⚙ menu. The UI says "profile" and
  "game", never "sim" or "aircraft".
- Per-car/game settings go on `Profile`; app-wide ones (HUD, window) on `AppSettings`. New `Profile`,
  `ButtonBinding` and `AppSettings` properties **must** have initializers.

## Known open issues

- The cursor stays hidden/locked after a crash or kill (restored only on a normal exit).
- Profiles are stored next to the exe, so under Program Files nothing is saved (the status bar shows
  "Not saved"); a corrupt profile JSON is replaced with defaults on the next save.
- The HUD and window sizes are in raw pixels; at high DPI they look small (the HUD has its Size slider).
- Not yet tested in a real game.

## Git

- Local repo only, branch `main`, no remote yet. Commit or push only when the user asks.
- `vJoyInterface.dll` is redistributed from the vJoy SDK; confirm its license before publishing.
