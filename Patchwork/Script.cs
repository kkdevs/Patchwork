using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Mono.CSharp;
using System.CodeDom.Compiler;
using System.Reflection.Emit;

namespace Patchwork
{
	public class Script : MonoBehaviour
	{
		static int counter = 0;
		public static AssemblyBuilder Compile(string[] sources)
		{
			Trace.Log("Compiling scripts");
			var report = new StreamReportPrinter(Console.Out);
			var name = "dynamic_" + counter++;
			var set = new CompilerSettings()
			{
				GenerateDebugInfo = true,
				StdLibRuntimeVersion = RuntimeVersion.v4,
				Target = Target.Library,
			};
			//foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
			set.AssemblyReferences.Add("unityengine");

			int i = 0;
			foreach (var f in sources)
			{
				i++;
				set.SourceFiles.Add(new SourceFile(Path.GetFileName(f), f, i));
			}
			var ctx = new CompilerContext(set, report);
			var mod = new ModuleContainer(ctx);

			RootContext.ToplevelTypes = mod;
			Location.Initialize(ctx.SourceFiles);
			var session = new ParserSession()
			{
				UseJayGlobalArrays = true,
				LocatedTokens = new LocatedToken[15000]
			};
			foreach (var finfo in set.SourceFiles)
			{
				var fs = new SeekableStreamReader(File.OpenRead(finfo.FullPathName), Encoding.UTF8);
				var csrc = new CompilationSourceFile(mod, finfo);
				mod.AddTypeContainer(csrc);
				var parser = new CSharpParser(fs, csrc, session);
				parser.parse();
			}
			var ass = new AssemblyDefinitionDynamic(mod, name, name + ".dll");
			mod.SetDeclaringAssembly(ass);
			var importer = new ReflectionImporter(mod, ctx.BuiltinTypes);
			ass.Importer = importer;
			var loader = new DynamicLoader(importer, ctx);
			importer.ImportAssembly(Assembly.GetExecutingAssembly(), mod.GlobalRootNamespace);
			loader.LoadReferences(mod);
			ass.Create(AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave);
			mod.CreateContainer();
			loader.LoadModules(ass, mod.GlobalRootNamespace);
			mod.InitializePredefinedTypes();
			mod.Define();
			if (ctx.Report.Errors > 0)
			{
				Trace.Log($"{ctx.Report.Errors} errors, aborting.");
				return null;
			}
			ass.Resolve();
			ass.Emit();
			mod.CloseContainer();
			Trace.Log("Scripts compiled succesfuly.");
			return ass.Builder;
		}



		static GameObject pin;
		public static void Init()
		{
			pin = new GameObject("Scripts");
			DontDestroyOnLoad(pin);
			pin.AddComponent(typeof(Script));

			var scripts = Path.Combine(UserData.Path, "scripts");
			try { Directory.CreateDirectory(scripts); } catch { };
			var ass = Compile(Directory.GetFiles(scripts, "*.cs"));
			foreach (var t in ass.GetTypes())
			{
				if (!t.IsSubclassOf(typeof(MonoBehaviour)))
					continue;
				pin.AddComponent(t);
			}
		}


		int tick;
		public void Update()
		{
			tick++;
			if (tick < 60) return;
			tick = 0;
			Program.FixWindow();
		}
	}
}
	