using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Patchwork
{
	public class Trace
	{
		public static void Log(string msg)
		{
			Console.WriteLine("[TRACE] " + msg);
		}
		public static void Spam(string msg)
		{
			Console.WriteLine("[SPAM] " + msg);
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
