//@INFO: Compatibility for zipmods
//@VER: 1

using Patchwork;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.Text;
using UnityEngine;
using System.Linq;

public class unzip : MonoBehaviour
{
	static byte []entry2bytes(ZipFile zf, ZipEntry entry)
	{
		return new BinaryReader(zf.GetInputStream(entry)).ReadBytes((int)entry.Size);
	}
	public void OnDestroy()
	{
		print("Destroyed");
	}
	public void Awake()
	{
		var target = UserData.Path + "mod/";
		var zipdir = Program.BasePath + "/mods";
		foreach (var zipfn in Directory.GetFiles(zipdir, "*.zip", SearchOption.TopDirectoryOnly))
		{
			var modname = Path.GetFileNameWithoutExtension(zipfn);
			//print("@ " + modname);
			if (Directory.Exists(target + modname))
				continue;
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
					if (efn.ToLower().EndsWith("/manifest.xml"))
						continue;
					
					if (efn.StartsWith("abdata/"))
						efn = efn.Substring(7);
					
					var prefix = "list/characustom/";
					var basename = Path.GetFileNameWithoutExtension(efn);
					var bytes = entry2bytes(zip, entry);

					// make the cat manifest csv canonical
					//print(efn);
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
					efn = target + modname + "/" + efn;
					Directory.CreateDirectory(Path.GetDirectoryName(efn));
					//print(".. Extracted " + efn);
					File.WriteAllBytes(efn, bytes);
				}
			}
		}
		print("Done");
	}
}
