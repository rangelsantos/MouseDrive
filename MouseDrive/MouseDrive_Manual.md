# MouseDrive — Steer with your mouse

**Drive racing games with your mouse as the steering wheel — no wheel needed.**

MouseDrive turns left and right mouse movement into a steering axis. The game sees it as an
ordinary controller, through **vJoy**, a free virtual joystick. It is made for racing games that
don't have mouse steering of their own.

> MouseDrive is in early development. Steering and progressive pedals work; tuning and per-game guides are
> still to come.

---

## What you need

1. **This app** (the MouseDrive folder).
2. **vJoy**, the free virtual joystick. One-time setup, explained below.
3. A racing game that accepts a generic joystick/wheel (DirectInput). Run it in **windowed** or
   **borderless** mode while you set things up.

---

## First-time setup

### Step 1 — Install vJoy

1. Download vJoy from **https://github.com/jshafer817/vJoy/releases** (the file ending in `.exe`).
2. Run it, click through the installer, and **restart your PC** if it asks.

### Step 2 — Set up vJoy (once)

1. Open the Start menu, search for **Configure vJoy** and open it.
2. Make sure **Device 1** is selected.
3. Tick **X** (the steering axis).
   For virtual pedals, also tick **Y** (throttle), **Z** (brake) and **Rz** (clutch).
4. Set **Number of Buttons** to **16**, for the keys you map to buttons.
5. Click **Apply**, then **OK**.

MouseDrive uses vJoy **device 1**, the same as MouseYokeEnhanced: close one before starting the other.

### Step 3 — Start MouseDrive

Double-click **MouseDrive.exe**.

- If Windows shows "Windows protected your PC", click **More info**, then **Run anyway**.
- If it asks for .NET, install the **.NET 8 Desktop Runtime (x64)** it points to, then start
  MouseDrive again.

The chip at the top right checks vJoy: green **vJoy ready** means all good. Amber or red means
something is missing; click it to see what and how to fix it.

### Step 4 — Bind the steering in the game (once)

1. In the game's controls, pick the **steering** axis and start binding it.
2. Press **F9** (MouseDrive starts driving), move the mouse left or right, then press **F9**
   again to get the pointer back.
3. In the game, set the steering **deadzone to 0** and **linearity / gamma to 1.0** (linear), so
   its curve doesn't stack on top of MouseDrive's. The car's steering lock is still the game's.

> If the game binds the F9 key instead of the axis, move the mouse first and press F9 only to stop.

---

## How to drive

1. Keep MouseDrive open (on a second screen, or behind the game).
2. Press **F9** or click **Activate**. The mouse is now the wheel: move right to steer right.
3. Press **F9** again for **standby**: the wheel goes back to center and the mouse is free.

While driving, the pointer is held on one pixel in the middle of the main screen (and hidden,
unless you turn **Hide cursor** off), so it can't slip onto another window or monitor. The mouse
still steers: MouseDrive reads its movement directly, not the pointer position.

Over **Remote Desktop** or a remote-control app, Windows only gets pointer positions, not mouse
movement. MouseDrive then steers from how far the pointer moves and leaves it free (not held in the
middle), so the steering range is limited by the screen width.

There is no physical center on a mouse: if the wheel drifts off center, press **Home**
(**Center steering**) to straighten it.

---

## The tabs

- **Drive** — what the game is receiving: the wheel, the steering in percent, the mouse input
  before expo and deadzone, and a lamp per vJoy button.
- **Steering** — the feel, saved per profile. On top, **Mouse to wheel** shows two rulers: mouse
  travel from center in millimeters (on a 1600 DPI mouse) above, wheel lock below. The lines join
  them at every 10% of lock, so you can see how far the mouse goes for each step: with Expo they
  bunch toward the ends, and the Deadzone shows as an amber band. The ends of the mouse ruler are
  full lock. While driving, a green line shows where the mouse and the wheel are now.
  - **Sensitivity** — how much mouse travel takes the wheel from center to full lock. The line
    under it says how many mouse counts that is (a 1600 DPI mouse makes about 630 per centimeter).
  - **Expo** — softer near center for small corrections, same full lock.
  - **Deadzone** — a band around center that gives no steering. Usually 0.
  - **Invert steering** — for a game that turns the wrong way.
  - **Smoothing** — irons out jitter at the cost of a little lag. Keep it low.
  - **Self-centering** — 0 keeps the wheel where you leave it. Above 0 the wheel drifts back to
    center when the mouse stops, a bit like a real car's self-aligning steering.
- **Keys** — **Commands** (Drive / standby, Center steering) and **vJoy buttons**: map any key or
  mouse button (right, middle, 4, 5) to a vJoy button and bind that button in the game, for
  shift up/down, pit limiter, lights… In key capture, **Esc** cancels and **Backspace** clears.
- **Pedals** — configure progressive throttle, brake and clutch from keys or mouse buttons.
  Each pedal has an initial force, maximum force, apply/release rates, axis inversion, a response
  graph and a live meter. Settings are saved per profile.
- **HUD** — an overlay over the game: **Wheel** or **Bar**, size, opacity, numbers, fade when
  straight, and position. It is click-through and never takes the focus.

When adding or editing a vJoy binding, **Esc** cancels. **Backspace** clears an existing key
without deleting its row; while adding, it exits without creating a binding. Switching profiles,
tabs or Keys sub-tabs cancels key capture. Changing a key releases held vJoy buttons; release and
press the keys again to use the updated bindings.

**+ Add key** chooses the first unused vJoy button number. If every button is assigned, remove a
binding or change an existing number first. You can set multiple bindings to the same button
number: the output stays pressed until the last held key for that button is released.

The **⚙** menu has **Always on top**, **Close to tray**, **Compact window** and **About**.

---

## Progressive pedals

On **Pedals**, choose **Throttle**, **Brake** or **Clutch**, turn on **Enable pedal**, click the key
button and press a key or mouse button (right, middle, 4 or 5). Pedals start disabled and unassigned
in new and older profiles. **Esc** cancels capture; **Backspace** or **Clear key** unassigns it.

| Pedal | vJoy axis on Device 1 |
|---|---|
| Throttle | Y |
| Brake | Z |
| Clutch | Rz |

Enable the axes you need in **Configure vJoy**, then restart MouseDrive. A warning on the pedal
page identifies an axis that is unavailable. In the game, bind each pedal as a separate axis and
turn off combined pedals if that option is present. **Invert pedal axis** reverses the output for
games that read the axis backwards.

- **Initial force** is applied once when you press the key. Set it to **0%** for a smooth start
  from zero, or raise it for an immediate small input.
- **Maximum force** caps the pedal while you hold the key. Initial force cannot exceed it;
  moving either limit past the other moves the other limit to match.
- **Apply rate** adds percentage points of force per second while held.
- **Release rate** removes percentage points per second after you let go, down to **0%**.
  Initial force is not a floor while releasing.

For example, initial **10%**, maximum **80%** and apply rate **35%/s** take **2 seconds** to reach
the maximum. A release rate of **200%/s** returns from 80% to zero in **0.4 seconds**. Repressing
the key during release continues from the remaining force rather than dropping back to the initial
value. Both rates use elapsed time, so the response does not depend on timer frequency.

The graph previews holding the key through full application and then releasing it. The meters
show the current input; you can hold the assigned keys in **standby** to test the ramps while
the game output stays at **0%**. Activate driving to send the pedal forces to vJoy.

Entering or leaving driving, switching profiles, rebinding keys or changing pedal settings releases
the pedals. Release and press the keys again afterward. Commands take priority over pedal keys;
the page warns about conflicts. Multiple pedals can share a key and will respond together. A key
also bound to a vJoy button operates both; choose separate keys if that is not intended. As with
other global bindings, keys still reach the foreground application.

---

## Profiles

Each profile keeps its own steering feel, keys and buttons; make one per car or game. Use the
profile button at the top to switch, and **⋯** to create, duplicate, rename, import, export or
delete. Profiles are JSON files in the `Profiles` folder next to `MouseDrive.exe`, so keep the
app in a folder you can write to (not Program Files).

Profiles with null binding entries, invalid key/button numbers or invalid pedal limits/rates are
rejected during import.

---

## Troubleshooting

- **The game doesn't react** — check the vJoy chip is green, that X is ticked for Device 1, and
  that the game's steering is bound to the vJoy device.
- **Keys do nothing in the game** — a game run as administrator hides its keys from normal apps.
  Run both the same way.
- **The pointer is stuck in the middle of the screen** — you are driving: press F9.
- **vJoy 1 busy** — another app (MouseYokeEnhanced, a feeder…) holds device 1. Close it.

---

MouseDrive is by Rangel Santos. Joystick output goes through [vJoy](https://github.com/jshafer817/vJoy).
