using System;
using System.Collections.Generic;

namespace MouseDrive;

public enum VjdStat { Own, Free, Busy, Miss, Unknown }

public static class VJoy
{
	public const uint Device = 1;
	public const uint AxisX = 0x30;
	public const uint AxisY = 0x31;
	public const uint AxisZ = 0x32;
	public const uint AxisRz = 0x35;
	public static readonly Dictionary<uint, int> Axes = new Dictionary<uint, int>();
	public static readonly HashSet<uint> UnavailableAxes = new HashSet<uint>();
	public static readonly Dictionary<byte, bool> Buttons = new Dictionary<byte, bool>();
	public static bool Enabled() => true;
	public static VjdStat Status(uint id) => VjdStat.Free;
	public static bool Acquire(uint id) => true;
	public static void Relinquish(uint id) { }
	public static bool Reset(uint id) { Buttons.Clear(); return true; }
	public static int ButtonCount(uint id) => 16;
	public static bool AxisExists(uint id, uint axis) => !UnavailableAxes.Contains(axis);
	public static bool SetButton(bool down, uint id, byte button) { Buttons[button] = down; return true; }
	public static bool SetAxis(int value, uint id, uint axis) { Axes[axis] = value; return true; }
	public static int ToAxis(double v) => 1 + (int)Math.Round((Math.Clamp(v, -1, 1) + 1) / 2 * 32767);
}

public sealed class KeyboardHook : IDisposable
{
	public event Action<int> KeyDown;
	public event Action<int> KeyUp;
	public void Install() { }
	public void ReleaseStaleKeys() { }
	public void MouseButton(int vk, bool down)
	{
		if (down) KeyDown?.Invoke(vk); else KeyUp?.Invoke(vk);
	}
	public void Dispose() { }
}

internal static class CursorHider
{
	public static void Hide() => throw new InvalidOperationException("Tests must never activate driving.");
	public static void Restore() { }
}
