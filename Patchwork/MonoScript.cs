using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.CSharp;
using System.Reflection.Emit;
using System.IO;
using System.Reflection;


public class MonoScript : Evaluator
{
	ReportPrinter reporter;
	TextWriter tw;
	Type initialBase;
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
	void asmLoaded(object sender, AssemblyLoadEventArgs e)
	{
		try
		{
			ReferenceAssembly(e.LoadedAssembly);
		}
		catch (Exception ex) { tw.WriteLine(ex); };
	}

	~MonoScript()
	{
		AppDomain.CurrentDomain.AssemblyLoad -= asmLoaded;
	}

	public static CompilerContext BuildContext(ReportPrinter rp)
	{
		var settings = new CompilerSettings()
		{
			Unsafe = true,
			Target = Target.Library,
		};
		return new CompilerContext(settings, rp);
	}

	public void ImportAssemblies(Action<Assembly> into)
	{
		foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (IsBadLib(a.GetName().Name))
				continue;
			try
			{
				into(a);
			}
			catch (Exception ex) { tw.WriteLine(ex); };
		}
	}

	public static bool IsBadLib(string name)
	{
		return name == "mscorlib" || name == "System.Core" || name == "System";
	}

	public AssemblyBuilder LoadScripts(IEnumerable<string> scripts)
	{
		var ctx = BuildContext(reporter);
		int i = 0;
		foreach (var f in scripts)
		{
			i++;
			ctx.Settings.SourceFiles.Add(new SourceFile(Path.GetFileName(f), f, i));
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
			var fs = new SeekableStreamReader(File.OpenRead(finfo.FullPathName), Encoding.UTF8);
			var csrc = new CompilationSourceFile(mod, finfo);
			csrc.EnableRedefinition();
			mod.AddTypeContainer(csrc);
			var parser = new CSharpParser(fs, csrc, session);
			parser.parse();
		}
		var asmname = "dynamic_scripts_" + counter++;
		var ass = new AssemblyDefinitionDynamic(mod, asmname, asmname + ".dll");
		mod.SetDeclaringAssembly(ass);
		var importer = new ReflectionImporter(mod, ctx.BuiltinTypes);
		ass.Importer = importer;
		var loader = new DynamicLoader(importer, ctx);
		ImportAssemblies((a) => importer.ImportAssembly(a, mod.GlobalRootNamespace));
		mod.InitializePredefinedTypes();
		mod.Define();
		if (ctx.Report.Errors > 0)
		{
			tw.WriteLine($"{ctx.Report.Errors} errors, aborting.");
			return null;
		}
		ass.Resolve();
		ass.Emit();
		mod.CloseContainer();
		var newasm = ass.Builder;
		// Find new base for repl if there is any
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