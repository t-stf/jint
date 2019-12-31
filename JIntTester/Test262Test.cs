using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;
using Newtonsoft.Json.Linq;

namespace JIntTester
{
	public class Test262Test : JsFileTest
	{
		private static readonly Dictionary<string, string> Sources;

		private static readonly string BasePath;

		private static readonly TimeZoneInfo _pacificTimeZone;

		private static readonly Dictionary<string, string> _skipReasons =
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private static readonly HashSet<string> _strictSkips =
				new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private static readonly Dictionary<string, string> extraSourceFiles = new Dictionary<string, string>()
				{
						{ "built-ins/Array/isArray/descriptor.js", "propertyHelper.js"}
				};

		private static string[] whiteList;

		static Test262Test()
		{
			//NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
			_pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

			var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.CodeBase).LocalPath;
			var assemblyDirectory = new FileInfo(assemblyPath).Directory;

			BasePath = assemblyDirectory.Parent.Parent.Parent.Parent.FullName;
			BasePath = Path.Combine(BasePath, "Jint.Tests.Test262");

			string[] files =
			{
								"sta.js",
								"assert.js",
								"propertyHelper.js",
								"compareArray.js",
								"decimalToHexString.js",
								"proxyTrapsHelper.js",
						};

			Sources = new Dictionary<string, string>(files.Length);
			for (var i = 0; i < files.Length; i++)
			{
				Sources[files[i]] = File.ReadAllText(Path.Combine(BasePath, "harness", files[i]));
			}

			var content = File.ReadAllText(Path.Combine(BasePath, "test/skipped.json"));
			var doc = JArray.Parse(content);
			foreach (var entry in doc.Values<JObject>())
			{
				var source = entry["source"].Value<string>();
				_skipReasons[source] = entry["reason"].Value<string>();
				if (entry.TryGetValue("mode", out var mode) && mode.Value<string>() == "strict")
				{
					_strictSkips.Add(source);
				}
			}
			content = File.ReadAllText(Path.Combine(BasePath, "test/Whitelist.txt"));
			whiteList = content.Split('\n').Where(s => !string.IsNullOrEmpty(s)).Select(s => s.Trim().Replace('\\','/').Replace("//", "/").ToLower()+"/").ToArray();
		}

		private static bool InWhiteList(string name)
		{
			name = name.ToLower();
			foreach (var s in whiteList)
			{
				if (name.StartsWith(s))
					return true;
			}
			return false;
		}

		protected void RunTestCode(string code, bool strict)
		{
			var engine = new Engine(cfg => cfg
					.LocalTimeZone(_pacificTimeZone)
					.Strict(strict)
			);

			engine.Execute(Sources["sta.js"]);
			engine.Execute(Sources["assert.js"]);

			var includes = Regex.Match(code, @"includes: \[(.+?)\]");
			if (includes.Success)
			{
				var files = includes.Groups[1].Captures[0].Value.Split(',');
				foreach (var file in files)
				{
					engine.Execute(Sources[file.Trim()]);
				}
			}

			if (code.IndexOf("propertyHelper.js", StringComparison.OrdinalIgnoreCase) != -1)
			{
				engine.Execute(Sources["propertyHelper.js"]);
			}

			string lastError = null;

			bool negative = code.IndexOf("negative:", StringComparison.Ordinal) > -1;
			try
			{
				engine.Execute(code);
			}
			catch (JavaScriptException j)
			{
				lastError = TypeConverter.ToString(j.Error);
			}
			catch (Exception e)
			{
				lastError = e.ToString();
			}

			if (negative)
			{
				Assert.NotNull(lastError);
			}
			else
			{
				Assert.Null(lastError);
			}
		}

		public override void RunTestInternal(SourceFile sourceFile)
		{
			if (sourceFile.Skip)
				return;
			var fullName = Path.Combine(sourceFile.BasePath, sourceFile.Source);
			if (!File.Exists(fullName))
			{
				throw new ArgumentException("Could not find source file: " + fullName);
			}
			var code = File.ReadAllText(fullName);

			if (code.IndexOf("onlyStrict", StringComparison.Ordinal) < 0)
			{
				RunTestCode(code, strict: false);
			}

			if (!_strictSkips.Contains(sourceFile.Source) && code.IndexOf("noStrict", StringComparison.Ordinal) < 0)
			{
				RunTestCode(code, strict: true);
			}
		}

		public static List<SourceFile> SourceFiles(string pathPrefix, bool skipped)
		{
			var results = new ConcurrentBag<SourceFile>();
			var fixturesPath = Path.Combine(BasePath, "test");
			var searchPath = pathPrefix == null ? fixturesPath : Path.Combine(fixturesPath, pathPrefix);
			var files = Directory.GetFiles(searchPath, "*.js", SearchOption.AllDirectories);


			//Parallel.ForEach(files, f => FileFunc(f));
			foreach (var f in files)
			{
				FileFunc(f);
			}

			void FileFunc(string file)
			{
				Console.Write($"\r{results.Count}");
				var name = file.Substring(fixturesPath.Length + 1).Replace("\\", "/");
				bool skip = _skipReasons.TryGetValue(name, out var reason);
				if (!skip)
					skip = !InWhiteList(name);

				var code = skip ? "" : File.ReadAllText(file);

				var flags = Regex.Match(code, "flags: \\[(.+?)\\]");
				if (flags.Success)
				{
					var items = flags.Groups[1].Captures[0].Value.Split(",");
					foreach (var item in items.Select(x => x.Trim()))
					{
						switch (item)
						{
							// TODO implement
							case "async":
								skip = true;
								reason = "async not implemented";
								break;
						}
					}
				}

				var features = Regex.Match(code, "features: \\[(.+?)\\]");
				if (features.Success)
				{
					var items = features.Groups[1].Captures[0].Value.Split(",");
					foreach (var item in items.Select(x => x.Trim()))
					{
						switch (item)
						{
							// TODO implement
							case "cross-realm":
								skip = true;
								reason = "realms not implemented";
								break;
							case "tail-call-optimization":
								skip = true;
								reason = "tail-calls not implemented";
								break;
							case "class":
								skip = true;
								reason = "class keyword not implemented";
								break;
							case "Symbol.species":
								skip = true;
								reason = "Symbol.species not implemented";
								break;
							case "object-spread":
								skip = true;
								reason = "Object spread not implemented";
								break;
							case "Symbol.unscopables":
								skip = true;
								reason = "Symbol.unscopables not implemented";
								break;
							case "Symbol.match":
								skip = true;
								reason = "Symbol.match not implemented";
								break;
							case "Symbol.matchAll":
								skip = true;
								reason = "Symbol.matchAll not implemented";
								break;
							case "Symbol.split":
								skip = true;
								reason = "Symbol.split not implemented";
								break;
							case "String.prototype.matchAll":
								skip = true;
								reason = "proposal stage";
								break;
							case "Symbol.search":
								skip = true;
								reason = "Symbol.search not implemented";
								break;
							case "Symbol.replace":
								skip = true;
								reason = "Symbol.replace not implemented";
								break;
							case "Symbol.toStringTag":
								skip = true;
								reason = "Symbol.toStringTag not implemented";
								break;
							case "BigInt":
								skip = true;
								reason = "BigInt not implemented";
								break;
							case "generators":
								skip = true;
								reason = "generators not implemented";
								break;
							case "let":
								skip = true;
								reason = "let not implemented";
								break;
							case "async-functions":
								skip = true;
								reason = "async-functions not implemented";
								break;
							case "async-iteration":
								skip = true;
								reason = "async not implemented";
								break;
							case "new.target":
								skip = true;
								reason = "MetaProperty not implemented";
								break;
							case "super":
								skip = true;
								reason = "super not implemented";
								break;
						}
					}
				}

				if (code.IndexOf("SpecialCasing.txt") > -1)
				{
					skip = true;
					reason = "SpecialCasing.txt not implemented";
				}

				if (name.StartsWith("language/expressions/object/dstr-async-gen-meth-"))
				{
					skip = true;
					reason = "Esprima problem, Unexpected token *";
				}

				if (file.EndsWith("tv-line-continuation.js")
									|| file.EndsWith("tv-line-terminator-sequence.js")
									|| file.EndsWith("special-characters.js"))
				{
					// LF endings required
					code = code.Replace("\r\n", "\n");
				}

				var sourceFile = new SourceFile(fixturesPath, name, skip, reason);
				if (skipped == sourceFile.Skip)
					results.Add(sourceFile);
			}
			return results.ToList();
		}
	}
}