﻿using System.Text;

namespace fx;

using System.ComponentModel;
using System.Diagnostics;

//https://stackoverflow.com/a/43640787
using System.Runtime.InteropServices;
using HWND = nint;

// ref: http://www.microsoft.com/whdc/system/Sysinternals/MoreThan64proc.mspx
public enum PROCESSINFOCLASS : int {
	ProcessBasicInformation = 0, // 0, q: PROCESS_BASIC_INFORMATION, PROCESS_EXTENDED_BASIC_INFORMATION
	ProcessWow64Information = 26, // q: ULONG_PTR
}
[Flags]
public enum PEB_OFFSET {
	CurrentDirectory,
	//DllPath,
	//ImagePathName,
	CommandLine,
	//WindowTitle,
	//DesktopInfo,
	//ShellInfo,
	//RuntimeData,
	//TypeMask = 0xffff,
	//Wow64 = 0x10000,
};

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
	static extern IntPtr OpenProcess (uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

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
//https://stackoverflow.com/a/23842609
public class Is64BitChecker {
	[DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWow64Process (
		[In] IntPtr hProcess,
		[Out] out bool wow64Process
	);

	public static bool GetProcessIsWow64 (IntPtr hProcess) {
		if((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
			Environment.OSVersion.Version.Major >= 6) {
			bool retVal;
			if(!IsWow64Process(hProcess, out retVal)) {
				return false;
			}
			return retVal;
		} else {
			return false;
		}
	}

	public static bool InternalCheckIsWow64 () {
		if((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
			Environment.OSVersion.Version.Major >= 6) {
			using(Process p = Process.GetCurrentProcess()) {
				bool retVal;
				if(!IsWow64Process(p.Handle, out retVal)) {
					return false;
				}
				return retVal;
			}
		} else {
			return false;
		}
	}
}
public static class ProcessUtilities {
	public static readonly bool Is64BitProcess = IntPtr.Size > 4;
	public static readonly bool Is64BitOperatingSystem = Is64BitProcess || Is64BitChecker.InternalCheckIsWow64();

	public static string GetCurrentDirectory (int processId) {
		return GetProcessParametersString(processId, PEB_OFFSET.CurrentDirectory);
	}

	public static string GetCurrentDirectory (this Process process) {
		if(process == null)
			throw new ArgumentNullException("process");

		return GetCurrentDirectory(process.Id);
	}

	#region GetCommandLine
	//public static string GetCommandLine(int processId)
	//{
	//    return null;// GetProcessParametersString(processId, Is64BitOperatingSystem ? 0x70 : 0x40);
	//}

	//public static string GetCommandLine(this Process process)
	//{
	//    if (process == null)
	//        throw new ArgumentNullException("process");

	//    return GetCommandLine(process.Id);
	//} 
	#endregion

	private static string GetProcessParametersString (int processId, PEB_OFFSET Offset) {
		IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
		if(handle == IntPtr.Zero)
			throw new Win32Exception(Marshal.GetLastWin32Error());

		bool IsWow64Process = Is64BitChecker.InternalCheckIsWow64();
		bool IsTargetWow64Process = Is64BitChecker.GetProcessIsWow64(handle);
		bool IsTarget64BitProcess = Is64BitOperatingSystem && !IsTargetWow64Process;

		long offset = 0;
		long processParametersOffset = IsTarget64BitProcess ? 0x20 : 0x10;
		switch(Offset) {
			case PEB_OFFSET.CurrentDirectory:
				offset = IsTarget64BitProcess ? 0x38 : 0x24;
				break;
			case PEB_OFFSET.CommandLine:
			default:
				return null;
		}

		try {
			long pebAddress = 0;
			if(IsTargetWow64Process) // OS : 64Bit, Cur : 32 or 64, Tar: 32bit
			{
				IntPtr peb32 = new IntPtr();

				int hr = NtQueryInformationProcess(handle, (int)PROCESSINFOCLASS.ProcessWow64Information, ref peb32, IntPtr.Size, IntPtr.Zero);
				if(hr != 0) throw new Win32Exception(hr);
				pebAddress = peb32.ToInt64();

				IntPtr pp = new IntPtr();
				if(!ReadProcessMemory(handle, new IntPtr(pebAddress + processParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				UNICODE_STRING_32 us = new UNICODE_STRING_32();
				if(!ReadProcessMemory(handle, new IntPtr(pp.ToInt64() + offset), ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				if((us.Buffer == 0) || (us.Length == 0))
					return null;

				string s = new string('\0', us.Length / 2);
				if(!ReadProcessMemory(handle, new IntPtr(us.Buffer), s, new IntPtr(us.Length), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				return s;
			} else if(IsWow64Process)//Os : 64Bit, Cur 32, Tar 64
			  {
				PROCESS_BASIC_INFORMATION_WOW64 pbi = new PROCESS_BASIC_INFORMATION_WOW64();
				int hr = NtWow64QueryInformationProcess64(handle, (int)PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero);
				if(hr != 0) throw new Win32Exception(hr);
				pebAddress = pbi.PebBaseAddress;

				long pp = 0;
				hr = NtWow64ReadVirtualMemory64(handle, pebAddress + processParametersOffset, ref pp, Marshal.SizeOf(pp), IntPtr.Zero);
				if(hr != 0)
					throw new Win32Exception(hr);

				UNICODE_STRING_WOW64 us = new UNICODE_STRING_WOW64();
				hr = NtWow64ReadVirtualMemory64(handle, pp + offset, ref us, Marshal.SizeOf(us), IntPtr.Zero);
				if(hr != 0)
					throw new Win32Exception(hr);

				if((us.Buffer == 0) || (us.Length == 0))
					return null;

				string s = new string('\0', us.Length / 2);
				hr = NtWow64ReadVirtualMemory64(handle, us.Buffer, s, us.Length, IntPtr.Zero);
				if(hr != 0)
					throw new Win32Exception(hr);

				return s;
			} else// Os,Cur,Tar : 64 or 32
			  {
				PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
				int hr = NtQueryInformationProcess(handle, (int)PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero);
				if(hr != 0) throw new Win32Exception(hr);
				pebAddress = pbi.PebBaseAddress.ToInt64();

				IntPtr pp = new IntPtr();
				if(!ReadProcessMemory(handle, new IntPtr(pebAddress + processParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				UNICODE_STRING us = new UNICODE_STRING();
				if(!ReadProcessMemory(handle, new IntPtr((long)pp + offset), ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				if((us.Buffer == IntPtr.Zero) || (us.Length == 0))
					return null;

				string s = new string('\0', us.Length / 2);
				if(!ReadProcessMemory(handle, us.Buffer, s, new IntPtr(us.Length), IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());

				return s;
			}
		} finally {
			CloseHandle(handle);
		}
	}

	private const int PROCESS_QUERY_INFORMATION = 0x400;
	private const int PROCESS_VM_READ = 0x10;

	[StructLayout(LayoutKind.Sequential)]
	private struct PROCESS_BASIC_INFORMATION {
		public IntPtr Reserved1;
		public IntPtr PebBaseAddress;
		public IntPtr Reserved2_0;
		public IntPtr Reserved2_1;
		public IntPtr UniqueProcessId;
		public IntPtr Reserved3;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct UNICODE_STRING {
		public short Length;
		public short MaximumLength;
		public IntPtr Buffer;
	}

	// for 32-bit process in a 64-bit OS only
	[StructLayout(LayoutKind.Sequential)]
	private struct PROCESS_BASIC_INFORMATION_WOW64 {
		public long Reserved1;
		public long PebBaseAddress;
		public long Reserved2_0;
		public long Reserved2_1;
		public long UniqueProcessId;
		public long Reserved3;
	}

	// for 32-bit process
	[StructLayout(LayoutKind.Sequential)]
	private struct UNICODE_STRING_WOW64 {
		public short Length;
		public short MaximumLength;
		public long Buffer;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct UNICODE_STRING_32 {
		public short Length;
		public short MaximumLength;
		public int Buffer;
	}

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess (IntPtr ProcessHandle, int ProcessInformationClass, ref PROCESS_BASIC_INFORMATION ProcessInformation, int ProcessInformationLength, IntPtr ReturnLength);

	//ProcessWow64Information, // q: ULONG_PTR
	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess (IntPtr ProcessHandle, int ProcessInformationClass, ref IntPtr ProcessInformation, int ProcessInformationLength, IntPtr ReturnLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory (IntPtr hProcess, IntPtr lpBaseAddress, ref IntPtr lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory (IntPtr hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory (IntPtr hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING_32 lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

	//[DllImport("kernel32.dll", SetLastError = true)]
	//private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref UNICODE_STRING_WOW64 lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory (IntPtr hProcess, IntPtr lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, IntPtr dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess (int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle (IntPtr hObject);

	// for 32-bit process in a 64-bit OS only
	[DllImport("ntdll.dll")]
	private static extern int NtWow64QueryInformationProcess64 (IntPtr ProcessHandle, int ProcessInformationClass, ref PROCESS_BASIC_INFORMATION_WOW64 ProcessInformation, int ProcessInformationLength, IntPtr ReturnLength);

	[DllImport("ntdll.dll")]
	private static extern int NtWow64ReadVirtualMemory64 (IntPtr hProcess, long lpBaseAddress, ref long lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("ntdll.dll")]
	private static extern int NtWow64ReadVirtualMemory64 (IntPtr hProcess, long lpBaseAddress, ref UNICODE_STRING_WOW64 lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);

	[DllImport("ntdll.dll")]
	private static extern int NtWow64ReadVirtualMemory64 (IntPtr hProcess, long lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer, long dwSize, IntPtr lpNumberOfBytesRead);
}