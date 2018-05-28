using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Patchwork
{
	public static partial class Program
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		[PreserveSig]
		public static extern uint GetModuleFileName
		(
			[In]
			IntPtr hModule,

			[Out]
			StringBuilder lpFilename,

			[In]
			[MarshalAs(UnmanagedType.U4)]
			int nSize
		);
		[DllImport("user32.dll")]
		public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();
		public delegate bool EnumThreadDelegate(IntPtr Hwnd, IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr w, int cmd);
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetWindowText(IntPtr hwnd, String lpString);
		[DllImport("user32.dll")]
		private static extern ulong SetWindowLongPtr(IntPtr hWnd, int nIndex, ulong dwNewLong);
		[DllImport("user32.dll")]
		private static extern ulong GetWindowLongPtr(IntPtr hWnd, int nIndex);
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
		[DllImport("shell32.dll")]
		public static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);
		public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder lpszFile, uint cch);
		[DllImport("shell32.dll")]
		public static extern void DragFinish(IntPtr hDrop);
		[DllImport("shell32.dll")]
		public static extern void DragQueryPoint(IntPtr hDrop, out POINT pos);
	}


	[StructLayout(LayoutKind.Sequential)]
	public struct CWPSTRUCT
	{
		public IntPtr lParam;
		public IntPtr wParam;
		public uint message;
		public IntPtr hwnd;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int x, y;
		public POINT(int aX, int aY)
		{
			x = aX;
			y = aY;
		}
		public override string ToString()
		{
			return "(" + x + ", " + y + ")";
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public ushort time;
		public POINT pt;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left, Top, Right, Bottom;

		public RECT(int left, int top, int right, int bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}
		public override string ToString()
		{
			return "(" + Left + ", " + Top + ", " + Right + ", " + Bottom + ")";
		}
	}
}
