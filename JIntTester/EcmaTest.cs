using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Jint;
using Jint.Runtime;
using Newtonsoft.Json.Linq;

namespace JIntTester
{
  public abstract class JsFileTest
  {

    public abstract void RunTestInternal(SourceFile sourceFile);
  }
  
  public class EcmaTest : JsFileTest
  {
    private string _lastError;
    private static string staSource;
    protected Action<string> Error;
    protected string code;
    protected bool negative;
    protected SourceFile sourceFile;

    protected string BasePath => sourceFile.BasePath;


    //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
    static TimeZoneInfo pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    public EcmaTest()
    {
      Error = s => { _lastError = s; };
    }

    public override void RunTestInternal(SourceFile sourceFile)
    {
      this.sourceFile = sourceFile;
      var fullName = Path.Combine(sourceFile.BasePath, sourceFile.Source);
      if (!File.Exists(fullName))
      {
        throw new ArgumentException("Could not find source file: " + fullName);
      }
      code = File.ReadAllText(fullName);
      negative = code.Contains("@negative");
      RunTestCode();
    }

    protected void RunTestCode()
    {
      _lastError = null;

      var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
      var engine = new Engine(cfg => cfg.LocalTimeZone(pacificTimeZone));

      // loading driver
      if (staSource == null)
      {
        var driverFilename = Path.Combine(BasePath, "sta.js");
        staSource = File.ReadAllText(driverFilename);
      }
      engine.Execute(staSource);
      if (negative)
      {
        try
        {
          engine.Execute(code);
          Assert.True(_lastError != null);
          Assert.False(true);
        }
        catch
        {
          // exception is expected
        }
      }
      else
      {
        try
        {
          engine.Execute(code);
        }
        catch (JavaScriptException j)
        {
          _lastError = TypeConverter.ToString(j.Error);
        }
        catch (Exception e)
        {
          _lastError = e.ToString();
        }
        Assert.Null(_lastError);
      }
    }

    public static List<SourceFile> SourceFiles(string prefix, bool skipped)
    {
      var assemblyPath = new Uri(typeof(SourceFile).GetTypeInfo().Assembly.CodeBase).LocalPath;
      var assemblyDirectory = new FileInfo(assemblyPath).Directory;

      var localPath = assemblyDirectory.Parent.Parent.Parent.Parent.FullName;

      var fixturesPath = Path.Combine(localPath, @"Jint.Tests.Ecma\TestCases\alltests.json");

      var content = File.ReadAllText(fixturesPath);
      var doc = JArray.Parse(content);
      var results = new List<SourceFile>();
      var path = Path.GetDirectoryName(fixturesPath);

      foreach (JObject entry in doc)
      {
        var sourceFile = new SourceFile(entry, path);

        if (prefix != null && !sourceFile.Source.StartsWith(prefix))
        {
          continue;
        }

        if (sourceFile.Skip
            && (sourceFile.Reason == "part of new test suite"
                || sourceFile.Reason.IndexOf("configurable", StringComparison.OrdinalIgnoreCase) > -1))
        {
          // we consider this obsolete and we don't need to process at all
          continue;
        }

        if (skipped == sourceFile.Skip)
        {
          results.Add(sourceFile);
        }
      }

      return results;
    }

  }

}
