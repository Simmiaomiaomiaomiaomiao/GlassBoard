using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GlassBoard;

public static class FullscreenState
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct MONITORINFO
	{
		public int Size;

		public RECT Monitor;

		public RECT Work;

		public uint Flags;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr window, out RECT rect);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr window, StringBuilder text, int length);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);

	public static bool Active()
	{
		Screen[] allScreens = Screen.AllScreens;
		foreach (Screen screen in allScreens)
		{
			Rectangle bounds = screen.Bounds;
			IntPtr ancestor = GetAncestor(WindowFromPoint(((long)(bounds.Y + bounds.Height / 2) << 32) | (uint)(bounds.X + bounds.Width / 2)), 2);
			if (ancestor == IntPtr.Zero)
			{
				continue;
			}
			GetWindowThreadProcessId(ancestor, out var id);
			if (id == (uint)Process.GetCurrentProcess().Id)
			{
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder(256);
			GetClassName(ancestor, stringBuilder, 256);
			if (new string[4] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(stringBuilder.ToString()) || (GetWindowLongW(ancestor, -16) & 0xC00000) != 0 || !GetWindowRect(ancestor, out var rect))
			{
				continue;
			}
			MONITORINFO info = new MONITORINFO
			{
				Size = Marshal.SizeOf(typeof(MONITORINFO))
			};
			if (GetMonitorInfo(MonitorFromWindow(ancestor, 2u), ref info) && rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom)
			{
				if (GetForegroundWindow() == GetShellWindow())
				{
					ShowWindow(ancestor, 6);
				}
				return true;
			}
		}
		return false;
	}

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int P_0);

	[DllImport("user32.dll")]
	private static extern IntPtr WindowFromPoint(long P_0);

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr P_0, int P_1);

	[DllImport("user32.dll")]
	private static extern int GetWindowLongW(IntPtr P_0, int P_1);

	[DllImport("user32.dll")]
	private static extern IntPtr GetShellWindow();

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr P_0, int P_1);
}
