using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MouseDrive;

// A global low-level keyboard hook. It reports each key once per press (auto-repeat is dropped) and
// never swallows a key: the game still gets it. Mouse buttons join through MouseButton().
public sealed class KeyboardHook : IDisposable
{
	private delegate nint HookProc(int code, nint wParam, nint lParam);

#pragma warning disable CS0649 // filled by Marshal from native memory
	private struct KBDLLHOOKSTRUCT
	{
		public uint VkCode;

		public uint ScanCode;

		public uint Flags;

		public uint Time;

		public nint ExtraInfo;
	}
#pragma warning restore CS0649

	private const int WH_KEYBOARD_LL = 13;

	private const int WM_KEYDOWN = 0x0100;

	private const int WM_KEYUP = 0x0101;

	private const int WM_SYSKEYDOWN = 0x0104;

	private const int WM_SYSKEYUP = 0x0105;

	// Kept in a field: the hook calls back through it for as long as it is installed.
	private HookProc proc;

	private nint handle;

	private readonly HashSet<int> held = new HashSet<int>();

	// Held keys that read as up on the last ReleaseStaleKeys() call.
	private readonly HashSet<int> suspect = new HashSet<int>();

	public event Action<int> KeyDown;

	public event Action<int> KeyUp;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll")]
	private static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll")]
	private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern nint GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vKey);

	public void Install()
	{
		if (handle != 0)
		{
			return;
		}
		proc = OnHook;
		handle = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0u);
	}

	private nint OnHook(int code, nint wParam, nint lParam)
	{
		if (code >= 0)
		{
			int vk = (int)Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).VkCode;
			switch ((int)wParam)
			{
			case WM_KEYDOWN:
			case WM_SYSKEYDOWN:
				Down(vk);
				break;
			case WM_KEYUP:
			case WM_SYSKEYUP:
				Up(vk);
				break;
			}
		}
		return CallNextHookEx(handle, code, wParam, lParam);
	}

	// Mouse buttons come from Raw Input; going through here they get the same once-per-press and stale-key handling.
	public void MouseButton(int vk, bool down)
	{
		if (down)
		{
			Down(vk);
		}
		else
		{
			Up(vk);
		}
	}

	private void Down(int vk)
	{
		if (held.Add(vk))
		{
			KeyDown?.Invoke(vk);
		}
	}

	private void Up(int vk)
	{
		suspect.Remove(vk);
		if (held.Remove(vk))
		{
			KeyUp?.Invoke(vk);
		}
	}

	// Windows sometimes never delivers a key-up (Win+L, a UAC prompt, focus on an elevated window...).
	// A held key that reads as physically up on two calls in a row gets its KeyUp here; two calls,
	// so a key-up that is merely still on its way to the hook isn't fired twice.
	public void ReleaseStaleKeys()
	{
		if (held.Count == 0)
		{
			suspect.Clear();
			return;
		}
		List<int> stale = null;
		foreach (int vk in held)
		{
			if ((GetAsyncKeyState(vk) & 0x8000) != 0)
			{
				suspect.Remove(vk);
			}
			else if (!suspect.Add(vk))
			{
				(stale ??= new List<int>()).Add(vk);
			}
		}
		if (stale != null)
		{
			foreach (int vk in stale)
			{
				Up(vk);
			}
		}
	}

	public void Dispose()
	{
		if (handle != 0)
		{
			UnhookWindowsHookEx(handle);
			handle = 0;
		}
	}
}
