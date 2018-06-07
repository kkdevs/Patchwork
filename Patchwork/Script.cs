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
using System.Windows;
using UnityEngine;
using System;

using Object = UnityEngine.Object;
using Forms = System.Windows.Forms;
using System.Collections;

namespace Patchwork
{
	public class Blah : Singleton<AssetBundleManager>
	{
		void Update()
		{
			Forms.Application.DoEvents();
		}
	}
	public partial class Script : InteractiveBase
	{
		const string usings = "using System.Linq; using System.Collections.Generic; using System.Collections; using Patchwork; using UnityEngine; using UnityEngine.SceneManagement;";
		public class Reporter : TextWriter
		{
			public static Action<string> print;
			public override Encoding Encoding => Encoding.UTF8;
			public override void Write(char c)
			{
				print(""+c);
			}
			public override void Write(string s)
			{
				print(s);
			}
			public override void WriteLine(string s)
			{
				print(s);
				print("\n");
			}
		}
		public static Reporter report;
		static void asmLoaded(object sender, AssemblyLoadEventArgs e)
		{
			try
			{
				Evaluator.ReferenceAssembly(e.LoadedAssembly);
			}
			catch (Exception ex) { Debug.Log(ex); };
		}
		public static CompiledMethod compile(string str, IEnumerable<string> sources = null)
		{
			if (report == null)
			{
				report = new Reporter();
				Output = report;
				Error = report;
			}
			// need to reload scripts?
			if (Evaluator == null || sources != null)
			{
				var settings = new CompilerSettings()
				{
					Unsafe = true,
					//ShowFullPaths = true,
					Target = Target.Library,
				};

				int i = 0;
				foreach (var f in sources)
				{
					if (!f.EndsWith(".cs"))
						continue;
					i++;
					var sf = new SourceFile(f, f, i);
					settings.SourceFiles.Add(sf);
				}
				var printer = new StreamReportPrinter(report);
				if (Evaluator != null)
					AppDomain.CurrentDomain.AssemblyLoad -= asmLoaded;
				var ctx = new CompilerContext(settings, printer);
				Evaluator = new Evaluator(ctx);
				AppDomain.CurrentDomain.AssemblyLoad += asmLoaded;
				Evaluator.InteractiveBaseClass = typeof(Script);
				Evaluator.DescribeTypeExpressions = true;
				foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (a.GetName().Name == "mscorlib" || a.GetName().Name == "System.Core" || a.GetName().Name == "System")
						continue;
					try
					{
						Evaluator.ReferenceAssembly(a);
					} catch (Exception ex) { Debug.Log(ex); };
				}
			}
			if (sources != null)
			{
				foreach (var dll in sources)
					if (dll.EndsWith(".dll"))
						LoadAssembly(dll);
			}
			CompiledMethod compiled = null;


			//print("compiling");
			if (Evaluator.GetUsingList().Count == 0)
			{
				object dummy;
				bool b;
				
				Evaluator.Evaluate(usings, out dummy, out b);
			}
			Evaluator.Compile(str, out compiled);
			//print("done?");
			return compiled;
		}

		public static class Sentinel { }
		public static void reload()
		{
			//var initstr = "1+1;";
			var scripts = Path.Combine(UserData.Path, "scripts");
			try { Directory.CreateDirectory(scripts); } catch { };
			var compiled = compile("\"Reload ok\"", Directory.GetFiles(scripts, "*.cs"));
			object retval = typeof(Sentinel);
			if (compiled == null)
			{
				report.WriteLine("Failed to reload scripts.");
				return;
			}
			compiled(ref retval);
			print(retval);
			//eval("print(\"Compiled ok\");");
			//compiled.Method.DeclaringType.Assembly
		}

		public static object eval(string str)
		{
			object ret = typeof(Sentinel);
			compile(str, null)?.Invoke(ref ret);
			return ret;
		}

		static void p(TextWriter output, string s)
		{
			output.Write(s);
		}

		public static void print(string s = "")
		{
			report.WriteLine(s);
		}

		public static void pp(object o)
		{
			PrettyPrint(report, o);
			print("");
		}

		public static void PrettyPrint(TextWriter output, object result)
		{
			if (result == null)
			{
				p(output, "null");
				return;
			}

			if (result is Array)
			{
				Array a = (Array)result;

				p(output, "{ ");
				int top = a.GetUpperBound(0);
				for (int i = a.GetLowerBound(0); i <= top; i++)
				{
					PrettyPrint(output, a.GetValue(i));
					if (i != top)
						p(output, ", ");
				}
				p(output, " }");
			}
			else if (result is bool)
			{
				if ((bool)result)
					p(output, "true");
				else
					p(output, "false");
			}
			else if (result is string)
			{
				p(output, "\"");
				EscapeString(output, (string)result);
				p(output, "\"");
			}
			else if (result is IDictionary)
			{
				IDictionary dict = (IDictionary)result;
				int top = dict.Count, count = 0;

				p(output, "{");
				foreach (DictionaryEntry entry in dict)
				{
					count++;
					p(output, "{ ");
					PrettyPrint(output, entry.Key);
					p(output, ", ");
					PrettyPrint(output, entry.Value);
					if (count != top)
						p(output, " }, ");
					else
						p(output, " }");
				}
				p(output, "}");
			}
			else if (WorksAsEnumerable(result))
			{
				int i = 0;
				p(output, "{ ");
				foreach (object item in (IEnumerable)result)
				{
					if (i++ != 0)
						p(output, ", ");

					PrettyPrint(output, item);
				}
				p(output, " }");
			}
			else if (result is char)
			{
				EscapeChar(output, (char)result);
			}
			else
			{
				p(output, result.ToString());
			}
		}
		static void EscapeString(TextWriter output, string s)
		{
			foreach (var c in s)
			{
				if (c >= 32)
				{
					output.Write(c);
					continue;
				}
				switch (c)
				{
					case '\"':
						output.Write("\\\""); break;
					case '\a':
						output.Write("\\a"); break;
					case '\b':
						output.Write("\\b"); break;
					case '\n':
						output.Write("\n");
						break;

					case '\v':
						output.Write("\\v");
						break;

					case '\r':
						output.Write("\\r");
						break;

					case '\f':
						output.Write("\\f");
						break;

					case '\t':
						output.Write("\\t");
						break;

					default:
						output.Write("\\x{0:x}", (int)c);
						break;
				}
			}
		}
		static void EscapeChar(TextWriter output, char c)
		{
			if (c == '\'')
			{
				output.Write("'\\''");
				return;
			}
			if (c >= 32)
			{
				output.Write("'{0}'", c);
				return;
			}
			switch (c)
			{
				case '\a':
					output.Write("'\\a'");
					break;

				case '\b':
					output.Write("'\\b'");
					break;

				case '\n':
					output.Write("'\\n'");
					break;

				case '\v':
					output.Write("'\\v'");
					break;

				case '\r':
					output.Write("'\\r'");
					break;

				case '\f':
					output.Write("'\\f'");
					break;

				case '\t':
					output.Write("'\\t");
					break;

				default:
					output.Write("'\\x{0:x}'", (int)c);
					break;
			}
		}

		// Some types (System.Json.JsonPrimitive) implement
		// IEnumerator and yet, throw an exception when we
		// try to use them, helper function to check for that
		// condition
		static bool WorksAsEnumerable(object obj)
		{
			IEnumerable enumerable = obj as IEnumerable;
			if (enumerable != null)
			{
				try
				{
					enumerable.GetEnumerator();
					return true;
				}
				catch
				{
					// nothing, we return false below
				}
			}
			return false;
		}
	}
}

#if false
namespace Patchwork
{
	public class Script : MonoBehaviour
	{

		public static GameObject pin;
		public static Script instance;

		CompilerContext ctx;
		ModuleContainer module;
		ReflectionImporter importer;
		CompilationSourceFile source_file; // "source" of the repl line

		public class Reporter : TextWriter
		{
			public Action<string> print;
			public override Encoding Encoding => Encoding.UTF8;
			public override void Write(string s)
			{
				print(s);
			}
		}

		public static void Init()
		{
			pin = new GameObject("Scripts");
			DontDestroyOnLoad(pin);
			instance = pin.AddComponent<Script>();
		}

		/// <summary>
		/// Initialize compiler context boilerplate
		/// </summary>
		/// <param name="print">Printer for compiler errors</param>
		public void InitCompiler(Action<string> print) {
			var set = new CompilerSettings()
			{
				GenerateDebugInfo = false,
				StdLib = false,
				Target = Target.Library,
			};
			var scripts = Path.Combine(UserData.Path, "scripts");
			try { Directory.CreateDirectory(scripts); } catch { };
			int i = 0;
			foreach (var f in Directory.GetFiles(scripts, "*.cs"))
			{
				i++;
				var sf = new SourceFile(Path.GetFileName(f), f, i);
				set.SourceFiles.Add(sf);
			}
			var tw = new Reporter() { print = print };
			ctx = new CompilerContext(set, new StreamReportPrinter(tw));
			module = new ModuleContainer(ctx);
			source_file = new CompilationSourceFile(module, null);
			module.AddTypeContainer(source_file);
			module.SetDeclaringAssembly(new AssemblyDefinitionDynamic(module, "evaluator"));
			importer = new ReflectionImporter(module, ctx.BuiltinTypes);

			var loader = new DynamicLoader(importer, ctx);
			CompilerCallableEntryPoint.Reset();
			RootContext.ToplevelTypes = module;

			Location.Initialize(ctx.SourceFiles);
			var session = new ParserSession()
			{
				UseJayGlobalArrays = true,
				LocatedTokens = new LocatedToken[15000]
			};
			module.EnableRedefinition();
			foreach (var finfo in set.SourceFiles)
			{
				var fs = new SeekableStreamReader(File.OpenRead(finfo.FullPathName), Encoding.UTF8);
				var csrc = new CompilationSourceFile(module, finfo);
				csrc.EnableRedefinition();
				module.AddTypeContainer(csrc);
				var parser = new CSharpParser(fs, csrc, session);
				parser.parse();
			}

			loader.LoadReferences(module);
			ctx.BuiltinTypes.CheckDefinitions(module);
			module.InitializePredefinedTypes();
			source_file.EnableRedefinition();
		}

		/// <summary>
		/// reload scripts
		/// </summary>
		public void reload()
		{
		}

		/*
		public class Con : ILogHandler
		{
			public Action<string> printer;
			public void LogException(Exception exception, Object context)
			{
				printer("["+context+"]" + exception.ToString());
			}
			public void LogFormat(LogType logType, Object context, string format, params object[] args)
			{
				printer("[" + logType.ToString() + "]" + String.Format(format, args));
			}
		}
		*/

		static int counter = 0;
		public static AssemblyBuilder Compile(string[] sources)
		{
			Trace.Log("Loading scripts");
			var report = new StreamReportPrinter(Console.Out);
			var name = "dynamic_" + counter++;
			var set = new CompilerSettings()
			{
				GenerateDebugInfo = false,
				StdLib = false,
				Target = Target.Library,
			};
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

			// import all assemblies via (lazy) reflection - this is way faster than re-parsing
			// the metadata from scratch again.
			foreach (var impass in AppDomain.CurrentDomain.GetAssemblies())
			{
				var aname = impass.GetName().Name;
				if (aname.EndsWith("-firstpass"))
					continue;
				try
				{
					importer.ImportAssembly(impass, mod.GlobalRootNamespace);
				} catch (Exception ex)
				{
					ctx.Report.Warning(9999, 0, $"$Failed to import {name}. Hope types from there are not needed.");
					Debug.Log(ex);
				}
			}
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




		public static void reload_old()
		{
			try
			{
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
			catch (Exception ex)
			{
				Trace.Error(ex.ToString());
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

		public object eval(string str)
		{
			return null;
		}

		public void SetupRepl(Forms.TextBox fin, Forms.RichTextBox fout)
		{
			var set = new CompilerSettings()
			{
				GenerateDebugInfo = true,
				StdLib = false,
				Target = Target.Library,
			};
			fin.KeyDown += (o,e) => {
				if (e.KeyCode == Forms.Keys.Enter)
				{
					print(eval(fin.Text));
					fin.Text = "";
				}
			};
			Application.logMessageReceived += (s, trace, typ) =>
			{
				fout.AppendText(s + "\r\n");
				fout.ScrollToCaret();
			};
		}
	}
}
#endif