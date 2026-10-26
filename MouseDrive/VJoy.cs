using System;
using System.Runtime.InteropServices;

namespace MouseDrive;

// The device states reported by GetVJDStatus, in the SDK's order.
public enum VjdStat
{
	Own,
	Free,
	Busy,
	Miss,
	Unknown
}

// The few calls of the vJoy SDK (vJoyInterface.dll) that MouseDrive needs. Everything goes to device 1.
public static class VJoy
{
	private const string Dll = "vJoyInterface.dll";

	public const uint Device = 1u;

	// HID usages of the axes (Generic Desktop page).
	public const uint AxisX = 0x30;

	public const uint AxisY = 0x31;

	public const uint AxisZ = 0x32;

	public const uint AxisRz = 0x35;

	public const int AxisMin = 1;

	public const int AxisMax = 32768;

	public const int AxisCenter = 16384;

	[DllImport(Dll, EntryPoint = "vJoyEnabled", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Enabled();

	[DllImport(Dll, EntryPoint = "GetVJDStatus", CallingConvention = CallingConvention.Cdecl)]
	public static extern VjdStat Status(uint id);

	[DllImport(Dll, EntryPoint = "AcquireVJD", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Acquire(uint id);

	[DllImport(Dll, EntryPoint = "RelinquishVJD", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Relinquish(uint id);

	[DllImport(Dll, EntryPoint = "ResetVJD", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool Reset(uint id);

	[DllImport(Dll, EntryPoint = "GetVJDAxisExist", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool AxisExists(uint id, uint axis);

	[DllImport(Dll, EntryPoint = "GetVJDButtonNumber", CallingConvention = CallingConvention.Cdecl)]
	public static extern int ButtonCount(uint id);

	[DllImport(Dll, EntryPoint = "SetAxis", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool SetAxis(int value, uint id, uint axis);

	[DllImport(Dll, EntryPoint = "SetBtn", CallingConvention = CallingConvention.Cdecl)]
	public static extern bool SetButton(bool down, uint id, byte button);

	// -1..1 to the vJoy axis range.
	public static int ToAxis(double v)
	{
		double t = (Math.Clamp(v, -1.0, 1.0) + 1.0) / 2.0;
		return AxisMin + (int)Math.Round(t * (AxisMax - AxisMin));
	}

	// Without the DLL the first call throws, and the app would just vanish; this turns that into a message.
	public static bool InterfaceLoads(out string error)
	{
		try
		{
			Enabled();
			error = null;
			return true;
		}
		catch (DllNotFoundException)
		{
			error = "vJoyInterface.dll is missing.";
		}
		catch (BadImageFormatException)
		{
			error = "vJoyInterface.dll is damaged or not the 64-bit version.";
		}
		catch (EntryPointNotFoundException)
		{
			error = "vJoyInterface.dll is not a vJoy SDK library.";
		}
		return false;
	}
}
