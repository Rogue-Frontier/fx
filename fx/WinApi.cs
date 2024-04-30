using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fx;

using System.Diagnostics;

//https://stackoverflow.com/a/43640787
using System.Runtime.InteropServices;
using static Terminal.Gui.SpinnerStyle;
using HWND = nint;


public record WindowItem(HWND window, string name, uint pid, string path);
/// <summary>Contains functionality to get all the open windows.</summary>
public static class WinApi {
	[DllImport("user32.dll")]
	public static extern void SwitchToWnd (IntPtr hWnd, bool fAltTab);

	/// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
	/// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
	public static List<WindowItem> GetOpenWindows () {
		HWND shellWindow = GetShellWindow();

		var result = new List<WindowItem>();
		EnumWindows(delegate (HWND hWnd, int lParam)
		{
			if(hWnd == shellWindow) return true;
			if(!IsWindowVisible(hWnd)) return true;

			int length = GetWindowTextLength(hWnd);
			if(length == 0) return true;

			StringBuilder builder = new StringBuilder(length);
			GetWindowText(hWnd, builder, length + 1);
			var name = builder.ToString();

			var path = GetWindowModuleFileName(hWnd, out var pid);

			result.Add(new WindowItem(hWnd, name, pid, path));
			return true;
		}, 0);

		return result;
	}

	private delegate bool EnumWindowsProc (HWND hWnd, int lParam);

	[DllImport("USER32.DLL")]
	private static extern bool EnumWindows (EnumWindowsProc enumFunc, int lParam);

	[DllImport("USER32.DLL")]
	private static extern int GetWindowText (HWND hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("USER32.DLL")]
	private static extern int GetWindowTextLength (HWND hWnd);

	[DllImport("USER32.DLL")]
	private static extern bool IsWindowVisible (HWND hWnd);

	[DllImport("USER32.DLL")]
	private static extern IntPtr GetShellWindow ();
	[DllImport("user32.dll")]
	public static extern IntPtr GetWindowThreadProcessId (IntPtr hWnd, IntPtr ProcessId);






	[DllImport("user32.dll")]
	static extern uint GetWindowThreadProcessId (IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll")]
	static extern IntPtr OpenProcess (UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

	[DllImport("psapi.dll")]
	static extern uint GetModuleFileNameEx (IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool CloseHandle (IntPtr hObject);

	private static string GetWindowModuleFileName (IntPtr hWnd, out uint pid) {
		const int nChars = 1024;
		StringBuilder filename = new StringBuilder(nChars);
		GetWindowThreadProcessId(hWnd, out pid);
		IntPtr hProcess = OpenProcess(1040, 0, pid);
		GetModuleFileNameEx(hProcess, IntPtr.Zero, filename, nChars);
		CloseHandle(hProcess);
		return (filename.ToString());
	}
}