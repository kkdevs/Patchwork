using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.CSharp;
using System.Reflection.Emit;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

public class MonoScript : Evaluator
{
	public ReportPrinter reporter;
	public TextWriter tw;
	public MonoScript(CompilerContext ctx) : base(ctx) { }
	public string tempdir;
	public static MonoScript New(TextWriter rw, Type ib, string tmp = null)
	{
		var reporter = new StreamReportPrinter(rw);
		var ms = new MonoScript(BuildContext(reporter));
		ms.reporter = reporter;
		ms.tw = rw;
		ms.tempdir = tmp;
		ms.ImportAssemblies(ms.ReferenceAssembly);
		AppDomain.CurrentDomain.AssemblyLoad += ms.asmLoaded;
		return ms;
	}

	public static List<string> unloaded = new List<string>();
	public static void Unload(Assembly n)
	{
		if (n == null) return;
		var ln = n.GetName().Name.ToLower();
		Debug.Log("unloading " + ln);
		unloaded.Add(ln);
	}

	public bool pause;
	void asmLoaded(object sender, AssemblyLoadEventArgs e)
	{
		Debug.Log("Referencing assembly " + e.LoadedAssembly.FullName);
		if (pause)
		{
			Debug.Log("Skip because of pause");
			return;
		}
		if (e.LoadedAssembly.FullName.StartsWith("eval-") || e.LoadedAssembly.FullName == "completions")
		{
			Debug.Log("skip blacklist");
			return;
		}
		try
		{
			ReferenceAssembly(e.LoadedAssembly);
		}
		catch (Exception ex) { tw.WriteLine(ex); };
	}

	
	public void Dispose()
	{
		AppDomain.CurrentDomain.AssemblyLoad -= asmLoaded;
	}

	public static CompilerContext BuildContext(ReportPrinter rp)
	{
		var settings = new CompilerSettings()
		{
			Version = LanguageVersion.Experimental,
			GenerateDebugInfo = false,
			StdLib = true,
			//Unsafe = true,
			Target = Target.Library,
		};
		return new CompilerContext(settings, rp);
	}

	public void ImportAssemblies(Action<Assembly> into, string ignore = ".")
	{
		foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
		{
			var an = a.GetName().Name;
			if (IsStdLib(an) || /*an == "BepInEx" || */an.StartsWith("eval-") || an.StartsWith("completion") || an.StartsWith(ignore) || unloaded.Contains(an.ToLower()))
			{
				Debug.Log("Skipping blacklisted reference " + an);
				continue;
			}
			Debug.Log("Referencing " + an);
			try
			{
				into(a);
			}
			catch (Exception ex) {
				tw.WriteLine("Failed to import " + an + " => " + ex.Message);
			};
		}
	}

	/// <summary>
	/// Check if name is standard library
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public bool IsStdLib(string name)
	{
		return name == "mscorlib" || name == "System.Core" || name == "System" || name == "System.Xml";
	}

	/// <summary>
	/// Statically compile list of files (or raw source, if sources[] item is a byte[])
	/// </summary>
	/// <param name="sources"></param>
	/// <returns></returns>
	public Assembly StaticCompile(IEnumerable<object> sources, string prefix = "compiled_")
	{
		Debug.Log($"[SCRIPT] Static compiling {prefix}");
		pause = true;
		var ret = DoStaticCompile(sources, prefix);
		pause = false;
		return ret;
	}
	public Assembly DoStaticCompile(IEnumerable<object> sources, string prefix = "compiled_")
	{
		reporter.Reset();
		Location.Reset();
		var ctx = BuildContext(reporter);
		ctx.Settings.SourceFiles.Clear();
		int i = 0;
		var allBytes = new MemoryStream();
		List<Assembly> imports = new List<Assembly>();
		foreach (var fo in sources)
		{
			Assembly impass = fo as Assembly;
			if (impass != null) {
				imports.Add(impass);
				continue;
			}
			var f = fo as string;
			byte[] fbuf = fo as byte[];
			if (f != null)
			{
				if (!f.EndsWith(".cs"))
					continue;
				var bname = (f + "\n").ToBytes();
				allBytes.Write(bname, 0, bname.Length);
				fbuf = File.ReadAllBytes(f);
				allBytes.Write(fbuf, 0, fbuf.Length);
			} else
			{
				allBytes.Write(fbuf, 0, fbuf.Length);
				f = null;
			}
			i++;
			ctx.Settings.SourceFiles.Add(new SourceFile(f == null ? "<eval>" : Path.GetFileName(f), f ?? "<eval>", i, (o) =>
			{
				return new SeekableStreamReader(new MemoryStream(fbuf), Encoding.UTF8);
			}));
		}
		string dllname = prefix + (counter++) + ".dll";
		if (tempdir != null)
		{
			var hash = prefix + Ext.HashToString(allBytes.ToArray()).Substring(0, 12).ToLower() + ".dll";
			dllname = Path.Combine(tempdir, hash);
			if (File.Exists(dllname))
			{
				var nam = AssemblyName.GetAssemblyName(dllname);
				unloaded.Remove(nam.Name.ToLower());
				return Assembly.Load(nam);
			}
		}

		var mod = new ModuleContainer(ctx);
		RootContext.ToplevelTypes = mod;
		Location.Initialize(ctx.Settings.SourceFiles);
		var session = new ParserSession()
		{
			UseJayGlobalArrays = true,
			LocatedTokens = new LocatedToken[15000]
		};
		mod.EnableRedefinition();
		foreach (var finfo in ctx.Settings.SourceFiles)
		{
			var fs = finfo.GetInputStream(finfo);
			var csrc = new CompilationSourceFile(mod, finfo);
			csrc.EnableRedefinition();
			mod.AddTypeContainer(csrc);
			var parser = new CSharpParser(fs, csrc, session);
			parser.parse();
		}
		Debug.Log("Defining new assembly " + dllname);
		var ass = new AssemblyDefinitionDynamic(mod, Path.GetFileNameWithoutExtension(dllname), dllname);
		mod.SetDeclaringAssembly(ass);
		var importer = new ReflectionImporter(mod, ctx.BuiltinTypes);
		ass.Importer = importer;
		var loader = new DynamicLoader(importer, ctx);
		ImportAssemblies((a) => importer.ImportAssembly(a, mod.GlobalRootNamespace), prefix);
		foreach (var impa in imports)
			importer.ImportAssembly(impa, mod.GlobalRootNamespace);
		loader.LoadReferences(mod);
		ass.Create(AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave);
		mod.CreateContainer();
		loader.LoadModules(ass, mod.GlobalRootNamespace);
		mod.InitializePredefinedTypes();
		mod.Define();
		if (ctx.Report.Errors > 0)
		{
			tw.WriteLine($"{ctx.Report.Errors} errors, aborting.");
			return null;
		}
		try
		{
			ass.Resolve();
			ass.Emit();
			mod.CloseContainer();
			ass.EmbedResources();
		}
		catch (Exception ex)
		{
			tw.WriteLine($"Link error: " + ex.ToString());
			return null;
		}
		if (tempdir != null)
			ass.Save();
		return ass.Builder;

	}

	public static int counter;
}