using System;
using System.Diagnostics;
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

		static void Main(string[] args)
		{
			Console.WriteLine("JintTester");
			sw = Stopwatch.StartNew();

			var tests = EcmaTest.SourceFiles(null, false);
			WriteLineWithColor($"{tests.Count} Ecma tests loaded", DefaultColor);
			var tester = new EcmaTest();
			Parallel.For(0, tests.Count, i =>
			//for (int i = 0; i < 100; i++)
			{
				TestSource(tester, tests[i]);
			});
			End();
			//var tests=new EcmaTest()											

		}
		static long maxSingleTime;

		static void TestSource(JsFileTest tester, SourceFile source)
		{
			nTests++;
			int len = 70;
			try
			{
				var watch = Stopwatch.StartNew();
				tester.RunTestInternal(source);
				nOk++;
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
				nFails++;
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
			sw.Stop();
			WriteLineWithColor("", DefaultColor);
			WriteLineWithColor("", DefaultColor);
			WriteWithColor("Result   : ", OkColor);

			if (nFails == 0)
				WriteWithColor("All tests passed!", DefaultColor);
			else
				WriteWithColor("Failed!", ErrorColor);

			WriteLineWithColor("", DefaultColor);

			WriteWithColor("Tests run: ", OkColor);
			WriteWithColor(nTests.ToString(), DefaultColor);
			WriteWithColor(", Passed: ", OkColor);
			WriteWithColor(nOk.ToString(), DefaultColor);
			WriteWithColor(", Failed: ", OkColor);
			WriteLineWithColor(nFails.ToString(), nFails == 0 ? DefaultColor : ErrorColor);

			WriteWithColor("End time : ", OkColor);
			WriteLineWithColor(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), DefaultColor);

			WriteWithColor("Duration : ", OkColor);
			WriteLineWithColor($"{sw.ElapsedMilliseconds / 1000.0:f2} s", DefaultColor);

			WriteLineWithColor("--- End Testing ---", DefaultColor);
		}

	}
}
