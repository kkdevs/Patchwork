// shims for common types referenced by plugins.
// this can't be easily put into a script as certain plugins self-inspect, and will
// choke on encounter with a type from AssemblyBuilder instead of plain Assembly
// when they fire up GetTypes().

using Patchwork;
using System;
using System.IO;
using UnityEngine;
namespace BepInEx
{
	public class BaseUnityPlugin : MonoBehaviour { }
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class BepInPlugin : Attribute
	{
		public BepInPlugin(string GUID, string Name, string Version) { }
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class BepInDependency : Attribute
	{
		public enum DependencyFlags : int
		{
			HardDependency = 1,
			SoftDependency = 2,
		}
		public BepInDependency(string a, DependencyFlags b = DependencyFlags.HardDependency)
		{
			Debug.Log("depcall");
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BepInProcess : Attribute
	{
		public string ProcessName { get; set; }
		public BepInProcess(string processName)
		{
			ProcessName = processName;
		}
	}
	namespace Logging
	{
		[Flags]
		public enum LogLevel
		{
			None = 0,
			Fatal = 1,
			Error = 2,
			Warning = 4,
			Message = 8,
			Info = 16,
			Debug = 32,
			All = Fatal | Error | Warning | Message | Info | Debug
		}
		public static class Logger
		{
			public static void Log(int level, object entry)
			{
				if ((level & 15) != 0)
					Trace.Log(entry.ToString());
				else
					Trace.Spam(entry.ToString());
			}

			public abstract class BaseLogger : TextWriter { };
			public static void SetLogger(BaseLogger logger)
			{
			}
		}
	}
	public static class BepInLogger
	{
		public static void Log(string entry, bool show = false)
		{
			if (show)
				Trace.Log(entry);
			else
				Trace.Spam(entry);
			
		}
		public static void Log(object entry, bool show, ConsoleColor color)
		{
			Log(entry.ToString(), show, color);
		}
		public static void Log(string entry, bool show, ConsoleColor color)
		{
			Log(entry, show);
		}
		public static void EntryLogger(string entry, bool show)
		{
			EntryLogged?.Invoke(entry, show);
		}
		public delegate void EntryLoggedEventHandler(string entry, bool show = false);
		public static event EntryLoggedEventHandler EntryLogged;
	}
	namespace Common
	{
		public static class Utility
		{
			public static string PluginsDirectory => Path.GetFullPath(Application.dataPath + "/../bepinex");
		}
	}
}

