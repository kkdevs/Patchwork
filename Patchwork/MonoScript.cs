using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.CSharp;
using System.Reflection.Emit;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Patchwork;

public class MonoScript : Evaluator
{
	public ReportPrinter reporter;
	public TextWriter tw;
	public Type initialBase;
	public MonoScript(CompilerContext ctx) : base(ctx) { }
	public static MonoScript New(TextWriter rw, Type ib)
	{
		var reporter = new StreamReportPrinter(rw);
		var ms = new MonoScript(BuildContext(reporter));
		ms.reporter = reporter;
		ms.tw = rw;
		ms.ImportAssemblies(ms.ReferenceAssembly);
		ms.InteractiveBaseClass = ms.initialBase = ib;
		AppDomain.CurrentDomain.AssemblyLoad += ms.asmLoaded;
		return ms;
	}

	public bool pause;
	void asmLoaded(object sender, AssemblyLoadEventArgs e)
	{
		if (pause) return;
		//tw.WriteLine("Referencing assembly " + e.LoadedAssembly.FullName);
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
			Platform = Platform.X64,
			Version = LanguageVersion.Experimental,
			GenerateDebugInfo = false,
			StdLib = true,
			//Unsafe = true,
			Target = Target.Library,
		};
		return new CompilerContext(settings, rp);
	}

	public void ImportAssemblies(Action<Assembly> into)
	{
		foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
		{
			// Don't import our past versions, it would end in tears.
			if (a is AssemblyBuilder)
				continue;
			var an = a.GetName().Name;
			if (IsStdLib(an))
				continue;
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
	/// <param name="tempdir"></param>
	/// <returns></returns>
	public Assembly StaticCompile(IEnumerable<object> sources, string tempdir = null)
	{
		reporter.Reset();
		var ctx = BuildContext(reporter);
		int i = 0;
		var md5 = SHA1.Create();
		var allBytes = new MemoryStream();
		foreach (var fo in sources)
		{
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
				f = "<eval>";
			}
			i++;
			ctx.Settings.SourceFiles.Add(new SourceFile(Path.GetFileName(f), f, i, (o) =>
			{
				return new SeekableStreamReader(new MemoryStream(fbuf), Encoding.UTF8);
			}));
		}
		string dllname = "compiled_scripts_" + (counter++) + ".dll";
		if (tempdir != null)
		{
			var hash = Convert.ToBase64String(md5.ComputeHash(allBytes.ToArray())).Replace("/", "").Replace(".", "").Replace("+", "").Substring(0, 16).ToLower() + ".dll";
			dllname = Path.Combine(tempdir, hash);
			if (File.Exists(dllname))
				return Assembly.Load(AssemblyName.GetAssemblyName(dllname));
		}

		var mod = new ModuleContainer(ctx);
		RootContext.ToplevelTypes = mod;
		Location.Initialize(ctx.SourceFiles);
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
		var ass = new AssemblyDefinitionDynamic(mod, Path.GetFileNameWithoutExtension(dllname), dllname);
		mod.SetDeclaringAssembly(ass);
		var importer = new ReflectionImporter(mod, ctx.BuiltinTypes);
		ass.Importer = importer;
		var loader = new DynamicLoader(importer, ctx);
		ImportAssemblies((a) => importer.ImportAssembly(a, mod.GlobalRootNamespace));
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

	/// <summary>
	/// Load the initial set of scripts for evaluator
	/// </summary>
	/// <param name="scripts"></param>
	/// <param name="tempdir"></param>
	/// <returns></returns>
	public Assembly LoadScripts(IEnumerable<string> scripts, string tempdir = null)
	{
		var newasm = StaticCompile(scripts.Cast<object>(), tempdir);

		// Look for a class deriving from the initial base, and set current ibase to it
		foreach (var t in newasm.GetTypes())
		{
			if (t.BaseType != initialBase)
				continue;
			InteractiveBaseClass = t;
			break;
		}
		return newasm;
	}

	public static int counter;
}