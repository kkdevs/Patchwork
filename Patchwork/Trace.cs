using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;

namespace Patchwork
{
	public static class Trace
	{
		static Stopwatch watch;

		public static void TimedMsg(string kind, string msg)
		{
			// Count from first spam
			if (watch == null)
			{
				watch = new Stopwatch();
				watch.Start();
			}
			Console.WriteLine($"[{kind}] [{watch.Elapsed.TotalSeconds}] {msg}");
		}

		public static void Log(string msg)
		{
			if (Program.settings.enableTrace)
				TimedMsg("TRACE", msg);
		}
		public static void Spam(string msg)
		{
			if (Program.settings.enableSpam)
				TimedMsg("SPAM", msg);
		}
		public static void Info(string msg)
		{
			if (Program.settings.enableInfo)
				TimedMsg("INFO", msg);
			TimedMsg("INFO", msg);
		}
		public static void Error(string msg)
		{
			if (Program.settings.enableError)
				TimedMsg("ERROR", msg);
		}
	}
}
