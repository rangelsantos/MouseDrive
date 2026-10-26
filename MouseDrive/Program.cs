using System;
using System.Windows.Forms;

namespace MouseDrive;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		if (!VJoy.InterfaceLoads(out string error))
		{
			MessageBox.Show("MouseDrive can't start: " + error + "\n\nvJoyInterface.dll must sit next to MouseDrive.exe (it ships with the app). vJoy itself is installed separately:\nhttps://github.com/jshafer817/vJoy/releases", "MouseDrive", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		Application.Run(new MainForm());
	}
}
