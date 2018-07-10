using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

// Originally illusion class
public static class Debug
{
	public static string Format(object[] a)
	{
		return String.Join(" ", a.Select((x) => x == null ? "<null>" : x.ToString()).ToArray());
	}

	static Stopwatch watch;
	public static void DoLog(string marker, string line)
	{
		if (watch == null)
		{
			watch = new Stopwatch();
			watch.Start();
		}
		Console.WriteLine($"[{watch.Elapsed.TotalSeconds}] {marker} {line}");
	}

	public static void DoLog(string marker, object[] a)
	{
		DoLog(marker, Format(a));
	}

	[Conditional("GAME_DEBUG")]
#if GAME_DEBUG
	[MethodImpl(MethodImplOptions.NoInlining)]
#endif
	public static void Log(params object[] a)
	{
		DoLog(new StackFrame(1, true).GetMethod().Name, a);
	}

	public static void Spam(params object[] a)
	{
		if (Patchwork.settings.enableSpam)
			DoLog("SPAM", a);
	}

	public static void Trace(params object[] a)
	{
		if (Patchwork.settings.enableTrace)
			DoLog("TRACE", a);
	}

	public static void Info(params object[] a)
	{
		DoLog("INFO", a);
	}

	public static void Error(params object[] a)
	{
		DoLog("ERROR", a);
		if (Patchwork.settings.enableSpam)
			DoLog("TRACEBACK", new object[] { Environment.StackTrace });
	}

	[Conditional("GAME_DEBUG")]
#if GAME_DEBUG
	[MethodImpl(MethodImplOptions.NoInlining)]
#endif
	public static void Stack(params object[] a)
	{
		if (Patchwork.settings.enableTrace)
			DoLog("STACK", a);
		if (Patchwork.settings.enableSpam)
			DoLog("TRACEBACK", new object[] { Environment.StackTrace });
	}

	public static void Protect(Action a) {
		try
		{
			a();
		} catch (Exception ex)
		{
			Error(ex);
		}
	}
	public static void Protect(string name, Action a)
	{
		try
		{
			a();
		}
		catch (Exception ex)
		{
			Error(name, ex);
		}
	}
}

