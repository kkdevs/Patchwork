//@INFO: Compatibility for zipmods
//@VER: 1

using Patchwork;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;

public class unzip : ScriptEvents
{
	static byte []entry2bytes(ZipFile zf, ZipEntry entry)
	{
		return new BinaryReader(zf.GetInputStream(entry)).ReadBytes((int)entry.Size);
	}
	[Prio(99999)]
	public override void Awake()
	{
		var target = Program.modbase;
		print("Unzipping mods into " + Path.GetFullPath(target));
		var zipdir = Program.BasePath + "/mods";
		foreach (var zipfn in Directory.GetFiles(zipdir, "*.zip", SearchOption.AllDirectories))
		{
			var modname = Path.GetFileNameWithoutExtension(zipfn);
			//print("@ " + modname);
			if (Directory.Exists(target + modname))
				continue;
			//print(Path.GetFileName(zipfn));
			using (var fs = File.Open(zipfn, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var zip = new ZipFile(fs);
				var guid = modname;

				// pick guid from manifest if its there
				var manifest = zip.GetEntry("manifest.xml");
				if (manifest != null)
				{
					XmlDocument doc = new XmlDocument();
					doc.LoadXml(Encoding.UTF8.GetString(entry2bytes(zip, manifest)).StripBOM());
					guid = doc.SelectSingleNode("//guid").InnerText;
				}
				foreach (ZipEntry entry in zip)
				{
					if (!entry.IsFile) continue;
					var efn = entry.Name;
					if (efn.ToLower().EndsWith("manifest.xml"))
						continue;
					
					if (efn.StartsWith("abdata/"))
						efn = efn.Substring(7);
					
					var prefix = "list/characustom/";
					var basename = Path.GetFileNameWithoutExtension(efn);
					var bytes = entry2bytes(zip, entry);

					// make the cat manifest csv canonical
					if (efn.StartsWith(prefix) && efn.EndsWith(".csv"))
					{
						var str = Encoding.UTF8.GetString(bytes);
						var ex = ScriptableObject.CreateInstance<ExcelData>().Import(CSV.ParseCSV(str));
						var firstrow = ex.list.First().list;
						var cat = -1;
						var dist = 0;
						if (firstrow.Count == 1)
						{
							try
							{
								cat = int.Parse(ex[0, 0].Trim());
								dist = int.Parse(ex[1, 0].Trim());
								ex.list.RemoveRange(0, 3);
							}
							catch (System.Exception exc) {
								print(exc.Message);
								print(ex[0, 0]);
								print(ex[1, 0]);
							};
						}
						else
						{
							try
							{
								cat = int.Parse(basename.Split('_')[0]);
							}
							catch { };
						}
						// if we can't figure out the category, bail on this csv
						if (cat == -1)
						{
							print(zipfn + " unzip failed");
							continue;
						}
						efn = $"{prefix}{dist:D2}_{guid}/{cat}_{basename}.csv";
						bytes = ex.Marshal().AddBOM().ToBytes();
					}
					if (efn.EndsWith(".unity3d"))
					{
						// match sideloader behavior and replace cab guid with some junk
						int pos = -1;
						for (int i = 0; i < 256; i++)
							if (bytes[i] == 'C' && bytes[i + 1] == 'A' && bytes[i + 2] == 'B' && bytes[i + 3] == '-')
							{
								pos = i;
								break;
							}
						if (pos == -1)
						{
							print($"WARNING: Unable to rewrite cab guid for {efn}");
							goto skip;
						}
						var ms = new MemoryStream();
						var hash = MD5.Create();
						ms.Write(bytes, 0, pos + 4); // CAB- + move past
						var cabstr = string.Join("", hash.ComputeHash((guid + efn).ToBytes()).Take(16).Select((x) => x.ToString("x2")).ToArray()).ToBytes();
						ms.Write(cabstr, 0, cabstr.Length);
						var rem = pos + 4 + cabstr.Length;
						ms.Write(bytes, rem, bytes.Length - rem);
						bytes = ms.ToArray();
					}
					skip:


					// if it is a hardmod, check that the contents of the bundle fully match the file overriden.
					var hardab = LoadedAssetBundle.basePath + efn;
					var oldefn = efn;
					int nmissing = 0;
					efn = target + modname + "/" + efn;
					Directory.CreateDirectory(Path.GetDirectoryName(efn));
					if (efn.EndsWith(".unity3d") && File.Exists(hardab))
					{
						var ab = AssetBundle.LoadFromMemory(bytes);
						if (ab == null)
						{
							print("WARNING: {efn} failed to load, skipping.");
							continue;
						}
						var abnames = new HashSet<string>(ab.GetAllAssetNames().Select((x) => Path.GetFileNameWithoutExtension(x)).ToList());
						ab.Unload(true);

						var origab = AssetBundle.LoadFromFile(hardab);
						if (origab == null)
							goto skip2;

						// Now load the original and check the replacement has all its assets
						foreach (var ass in origab.GetAllAssetNames())
						{
							var shass = Path.GetFileNameWithoutExtension(ass);
							if (!abnames.Contains(shass))
							{
								print($"WARNING: {Path.GetFileName(efn)}: {shass} is missing but should be there because {oldefn} has it!");
								nmissing++;
							}
						}
						origab.Unload(true);
					}
					skip2:

					//print(".. Extracted " + efn);
					if (nmissing == 0)
						File.WriteAllBytes(efn, bytes);
					else
					{
						print($"WARNING: Discarding corrupted {efn} because it is missing {nmissing} assets.");
					}
				}
			}
		}
		print("Done");
	}
}
