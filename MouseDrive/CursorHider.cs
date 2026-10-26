using System.Runtime.InteropServices;

namespace MouseDrive;

// Hides the pointer system-wide while driving by swapping every standard system cursor for a blank one.
// A game window can't hide another app's cursor, and ShowCursor only works for our own thread.
// Restore() reloads the user's cursor scheme.
internal static class CursorHider
{
	private const uint SPI_SETCURSORS = 0x0057;

	private const uint SPIF_SENDCHANGE = 0x02;

	// OCR_NORMAL, IBEAM, WAIT, CROSS, UP, SIZENWSE, SIZENESW, SIZEWE, SIZENS, SIZEALL, NO, HAND, APPSTARTING.
	private static readonly uint[] SystemCursors = new uint[13]
	{
		32512u, 32513u, 32514u, 32515u, 32516u, 32642u, 32643u, 32644u, 32645u, 32646u, 32648u, 32649u, 32650u
	};

	private static bool hidden;

	[DllImport("user32.dll")]
	private static extern nint CreateCursor(nint hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);

	[DllImport("user32.dll")]
	private static extern bool SetSystemCursor(nint hcur, uint id);

	[DllImport("user32.dll")]
	private static extern bool DestroyCursor(nint hCursor);

	[DllImport("user32.dll")]
	private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, nint pvParam, uint fWinIni);

	public static void Hide()
	{
		if (hidden)
		{
			return;
		}
		// A 32 x 32 monochrome cursor: AND all ones and XOR all zeros leave the screen untouched.
		byte[] and = new byte[128];
		for (int i = 0; i < and.Length; i++)
		{
			and[i] = 0xFF;
		}
		byte[] xor = new byte[128];
		foreach (uint id in SystemCursors)
		{
			// SetSystemCursor takes ownership of the handle, so each slot gets its own.
			nint blank = CreateCursor(0, 0, 0, 32, 32, and, xor);
			if (blank != 0 && !SetSystemCursor(blank, id))
			{
				DestroyCursor(blank);
			}
		}
		hidden = true;
	}

	public static void Restore()
	{
		if (hidden)
		{
			SystemParametersInfo(SPI_SETCURSORS, 0u, 0, SPIF_SENDCHANGE);
			hidden = false;
		}
	}
}
