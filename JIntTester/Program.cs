using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JIntTester
{
	class Program
	{
		static object lck = new object();

		static ConsoleColor TestColor = ConsoleColor.Cyan,
			ErrorColor = ConsoleColor.Red,
			OkColor = ConsoleColor.Green,
			DefaultColor = ConsoleColor.White;

		static int nTests, nOk, nFails;

		static Stopwatch sw;
		static long maxSingleTime = 1000;

		static void Main(string[] args)
		{
			Console.WriteLine("JintTester");
			WriteLineWithColor("--- Start Testing ---", DefaultColor);

			sw = Stopwatch.StartNew();

			var tests = Test262Test.SourceFiles(null, false);
			WriteLineWithColor($"\r{tests.Count} Ecma-262 tests loaded", DefaultColor);
			RunTests(typeof(Test262Test), tests);
			End();

			tests = EcmaTest.SourceFiles(null, false);
			WriteLineWithColor($"{tests.Count} Ecma tests loaded (total:: {tests.Count+nTests})", DefaultColor);
			RunTests(typeof(EcmaTest), tests);
			End();
			WriteLineWithColor("--- End Testing ---", DefaultColor);
		}

		static void RunTests(Type testType, List<SourceFile> tests)
		{
			Parallel.For(0, tests.Count, i =>
			{
				TestSource(Activator.CreateInstance(testType) as JsFileTest, tests[tests.Count - i - 1]);
			});
		}

		static void TestSource(JsFileTest tester, SourceFile source)
		{
			Interlocked.Increment(ref nTests);
			int len = 70;
			try
			{
				var watch = Stopwatch.StartNew();
				tester.RunTestInternal(source);
				Interlocked.Increment(ref nOk);
				var ms = watch.ElapsedMilliseconds;
				bool newMaxSingle = false;
				if (ms > maxSingleTime)
				{
					newMaxSingle = true;
					maxSingleTime = ms;
				}
				if (nTests % 10 == 0 || newMaxSingle)
				{
					var s = $"\r{nTests}: {source.Source}: {ms} ms";
					if (s.Length > len)
						s = s.Substring(0, len);
					else
						s += new string(' ', len - s.Length);
					if (newMaxSingle)
						WriteLineWithColor(s, ConsoleColor.Yellow);
					else
						WriteWithColor(s, TestColor);
				}

			}
			catch (Exception ex)
			{
				Interlocked.Increment(ref nFails);
				LogException(source, ex);
			}

		}

		private static void LogException(SourceFile source, Exception ex)
		{
			lock (lck)
			{
				var oldColor = Console.ForegroundColor;
				Console.ForegroundColor = ErrorColor;
				Console.WriteLine();
				Console.WriteLine($"{nFails}: {source.Source}");
				Console.WriteLine(ex.GetType());
				Console.WriteLine(ex.Message);

				//var stlines = ex.StackTrace.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				//for (int i = 0; i < stlines.Length; i++)
				//{
				//	var line = stlines[i];
				//	if (line.Contains("NUnit.Framework"))
				//		continue;
				//	Console.WriteLine(line);
				//}
				Console.WriteLine();
				Console.ForegroundColor = oldColor;
			}
		}

		static void WriteHelperFunc(Action action, ConsoleColor color)
		{
			lock (lck)
			{
				var oldColor = Console.ForegroundColor;
				Console.ForegroundColor = color;
				action();
				Console.ForegroundColor = oldColor;
			}
		}

		static void WriteWithColor(string text, ConsoleColor color) => WriteHelperFunc(() => Console.Write(text), color);

		static void WriteLineWithColor(string text, ConsoleColor color) => WriteHelperFunc(() => Console.WriteLine(text), color);

		public static void End()
		{
			var ms = sw.ElapsedMilliseconds;
			WriteLineWithColor("", DefaultColor);
			WriteLineWithColor("", DefaultColor);
			WriteWithColor("Result   : ", OkColor);

			if (nFails == 0)
				WriteWithColor("All tests passed!", DefaultColor);
			else
				WriteWithColor("Failed!", ErrorColor);

			WriteLineWithColor("", DefaultColor);

			WriteWithColor("Tests run: ", OkColor);
			WriteWithColor(nTests.ToString("###,###,##0"), DefaultColor);
			WriteWithColor(", Passed: ", OkColor);
			WriteWithColor(nOk.ToString("###,###,##0"), DefaultColor);
			WriteWithColor(", Failed: ", OkColor);
			WriteLineWithColor(nFails.ToString("###,###,##0"), nFails == 0 ? DefaultColor : ErrorColor);

			WriteWithColor("End time : ", OkColor);
			WriteLineWithColor(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), DefaultColor);

			WriteWithColor("Duration : ", OkColor);
			WriteLineWithColor($"{ms / 1000.0:f2} s", DefaultColor);
			WriteLineWithColor("", OkColor);
		}

	}
}
