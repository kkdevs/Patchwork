using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

	public static void Log(params object[] a)
	{
		DoLog("DEBUG", a);
	}

	public static void Spam(params object[] a)
	{
		DoLog("SPAM", a);
	}

	public static void Trace(params object[] a)
	{
		DoLog("TRACE", a);
	}

	public static void Info(params object[] a)
	{
		DoLog("INFO", a);
	}

	public static void Error(params object[] a)
	{
		DoLog("ERROR", a);
	}
	public static void Stack(params object[] a)
	{
		DoLog("STACK", a);
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

