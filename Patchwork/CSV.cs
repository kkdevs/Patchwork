using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Patchwork
{
	public static class CSV
	{
		// Load lst from CSV - a slightly odd dance by making it exceldata first
		public static bool LoadLst(string bundle, string asset, out string[,] data, int fixcol = -1)
		{
			data = null;
			var ex = Load(bundle, asset, typeof(ExcelData), false) as ExcelData;
			if (ex == null)
				return false;
			if (fixcol == -1) {
				foreach (var line in ex.list)
					if (line.list.Count > fixcol)
						fixcol = line.list.Count;
			}
			data = new string[ex.list.Count, fixcol];
			var i = 0;
			foreach (var line in ex.list)
			{
				var j = 0;
				foreach (var cell in line.list)
					data[i, j++] = cell;
				i++;
			}
			return true;
		}

		// Load marhsalled type from csv
		public static object Load(string bundle, string asset, Type typ, bool islst = false)
		{
			var ex = ScriptableObject.CreateInstance(typ) as IDumpable;
			if (ex == null)
				return null;
			var str = Cache.LoadString(bundle, asset);
			if (str == null)
				return null;
			if (ex.Unmarshal(Parse(str)))
				return null;
			return ex;
		}

		// Save marshalled type to csv
		public static bool Save(object o, string bundle, string asset)
		{
			var ex = ScriptableObject.CreateInstance(o.GetType()) as IDumpable;
			if (ex == null)
				return false;
			var ie = ex.Marshal();
			if (ie == null)
				return false;
			var str = ToString(ie);
			return Cache.SaveString(str, bundle, asset);
		}

		// Retrieve lst iterator
		public static IEnumerable<IEnumerable<string>> ParseTSV(string source)
		{
			foreach (var line in source.Split('\n'))
				yield return line.Split('\t');
		}

		// Serialize csv iterator to actual string repr
		public static string ToString(IEnumerable<IEnumerable<string>> csv)
		{
			var sb = new StringBuilder();
			var row = new List<string>();
			foreach (var line in csv)
			{
				foreach (var s in line)
					row.Add("\"" + s.Replace("\"", "\"\"") + "\"");
				sb.Append(System.String.Join(",", row.ToArray()));
				sb.Append("\n");
				row.Clear();
			}
			return sb.ToString();
		}

		// Parse csv to iterator
		public static IEnumerable<IEnumerable<string>> Parse(string source)
		{
			StringBuilder bodyBuilder = new StringBuilder();

			// here we build rows, one by one
			int i = 0;
			var row = new List<string>();
			var limit = source.Length;
			bool inQuote = false;

			while (i < limit)
			{
				if (source[i] == '\r')
				{
					//( ͠° ͜ʖ °)
				}
				else if (source[i] == ',' && !inQuote)
				{
					row.Add(bodyBuilder.ToString());
					bodyBuilder.Length = 0; //.NET 2.0 ghetto clear
				}
				else if (source[i] == '\n' && !inQuote)
				{
					if (bodyBuilder.Length != 0 || row.Count != 0)
					{
						row.Add(bodyBuilder.ToString());
						bodyBuilder.Length = 0; //.NET 2.0 ghetto clear
					}

					yield return row;
					row.Clear();
				}
				else if (source[i] == '"')
				{
					if (!inQuote)
						inQuote = true;
					else
					{
						if (i + 1 < limit
							&& source[i + 1] == '"')
						{
							bodyBuilder.Append('"');
							i++;
						}
						else
							inQuote = false;
					}
				}
				else
				{
					bodyBuilder.Append(source[i]);
				}

				i++;
			}

			if (bodyBuilder.Length > 0)
				row.Add(bodyBuilder.ToString());

			if (row.Count > 0)
				yield return row;
		}
	}
}
