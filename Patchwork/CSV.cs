using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// csv helpers
public static class CSV
{
	/// <summary>
	/// Convert csv ex to lst-format 2-dimensional array
	/// </summary>
	/// <param name="ex"></param>
	/// <param name="data"></param>
	/// <param name="fixcol"></param>
	/// <returns></returns>
	public static bool LoadLst(ExcelData ex, out string[,] data, int fixcol = 0)
	{
		data = null;
		if (fixcol == 0) {
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

	/// <summary>
	/// Parse TSV
	/// </summary>
	/// <param name="source"></param>
	/// <returns></returns>
	public static IEnumerable<IEnumerable<string>> ParseTSV(string source)
	{
		var lines = source.Replace("\r", "").Split('\n');
		var len = lines.Length;
		while (len > 0 && lines[len-1].Trim() == String.Empty)
			len--;
		for (int i = 0; i < len; i++)
		{
			yield return lines[i].Split('\t');
		}
	}

	/// <summary>
	/// Serialize csv/tsv to string
	/// </summary>
	/// <param name="csv"></param>
	/// <returns></returns>
	public static string ToString(IEnumerable<IEnumerable<string>> csv)
	{
		var sb = new StringBuilder();
		var row = new List<string>();
		foreach (var line in csv)
		{
			bool havecol = false;
			foreach (var s in line)
			{
				sb.Append('"');
				sb.Append(s.Replace("\"", "\"\""));
				sb.Append("\",");
				havecol = true;
			}
			if (havecol)
				sb.Length--;
			sb.Append("\n");
		}
		return sb.ToString();
	}

	/// <summary>
	/// Parse CSV or TSV (depending on extension)
	/// </summary>
	/// <param name="source"></param>
	/// <param name="ext"></param>
	/// <returns></returns>
	public static IEnumerable<IEnumerable<string>> Parse(string source, string ext)
	{
		if (ext == "lst")
			return ParseTSV(source);
		else
			return ParseCSV(source);
	}

	/// <summary>
	/// Parse CSV
	/// </summary>
	/// <param name="source"></param>
	/// <returns></returns>
	public static IEnumerable<IEnumerable<string>> ParseCSV(string source)
	{
		StringBuilder bodyBuilder = new StringBuilder();

		int i = 0;
		var row = new List<string>();
		var limit = source.Length;
		bool inQuote = false;

		while (i < limit)
		{
			if (source[i] == '\r' && !inQuote) { }
			else if (source[i] == ',' && !inQuote)
			{
				row.Add(bodyBuilder.ToString());
				bodyBuilder.Length = 0;
			}
			else if (source[i] == '\n' && !inQuote)
			{
				if (bodyBuilder.Length != 0 || row.Count != 0)
				{
					row.Add(bodyBuilder.ToString());
					bodyBuilder.Length = 0;
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

