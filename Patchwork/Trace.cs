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

		public static void Log(string msg)
		{
			Console.WriteLine("[TRACE] " + msg);
		}
		public static void Spam(string msg)
		{
			// Count from first spam
			if (watch == null) {
				watch = new Stopwatch();
				watch.Start();
			}
			Console.WriteLine($"[SPAM] [{watch.Elapsed.TotalSeconds}] {msg}");
		}
		public static void Info(string msg)
		{
			Console.WriteLine("[INFO] " + msg);
		}
		public static void Error(string msg)
		{
			Console.WriteLine("[ERROR] " + msg);
		}
	}
}
