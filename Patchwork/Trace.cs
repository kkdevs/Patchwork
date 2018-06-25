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
			/*if (kind == "SPAM")
				System.Diagnostics.Debug.WriteLine($"[{kind}] [{watch.Elapsed.TotalSeconds}] {msg}");
			else
				System.Diagnostics.Trace.WriteLine($"[{kind}] [{watch.Elapsed.TotalSeconds}] {msg}");*/
		}

		[Conditional("GAME_DEBUG")]
		public static void Back(string msg)
		{
			if (Program.settings.enableTrace)
				Spam(msg + Environment.StackTrace);
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
		}
		public static void Error(string msg)
		{
			if (Program.settings.enableError)
				TimedMsg("ERROR", msg);
		}
	}
}
