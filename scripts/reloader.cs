﻿//@INFO: Reload scripts on file change
//@VER: 1

using static Patchwork;
using System.Collections.Generic;
using System.IO;

public class Reloader : ScriptEvents
{
	bool stop;
	Dictionary<string, System.DateTime> tss = new Dictionary<string, System.DateTime>();
	public override void Occasion()
	{
		if (stop) return;
		foreach (var script in ScriptEntry.list)
		{
			if (script.ass != null) continue;

			// Not modified?
			var src = script.source;
			var cts = File.GetLastWriteTime(src);
			if (!tss.TryGetValue(src, out System.DateTime last) || last == cts)
			{
				tss[src] = cts;
				continue;
			}
			tss[src] = cts;
			// this script gets reloaded too, along with tss
			stop = true;
			try
			{
				if (!Script.reload())
					stop = false;
			} catch (System.Exception ex)
			{
				stop = false;
				print(ex);
			}
			break;
		}
	}
}
