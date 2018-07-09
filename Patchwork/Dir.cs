using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Patchwork
{
	public static class Dir
	{
		public static string root;
		public static string pw;
		public static string mod;
		public static string modcsv;
		public static string cache;
		public static string mat;
		public static string hist;
		public static string conf;
		public static void Init(string path)
		{
			root = path;
			pw = root + "patchwork/";
			mod = pw + "mod/";
			modcsv = mod + "!csv/";
			cache = pw + "cache/";
			mat = pw + "material/";
			hist = pw + "history.xml";
			conf = pw + "config.xml";
		}
	}
}
