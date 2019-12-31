using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace JIntTester
{
  public class SourceFile 
  {
    public SourceFile()
    {

    }

    public SourceFile(JObject node, string basePath)
    {
      Skip = node["skip"].Value<bool>();
      Source = node["source"].ToString();
      Reason = node["reason"].ToString();
      BasePath = basePath;
    }

    public string Source { get; private set; }
    public bool Skip { get; private set; }
    public string Reason { get; private set; }
    public string BasePath { get; private set; }

    public SourceFile(string basePath, string source, bool skip, string reason)
    {
      this.BasePath = basePath;
      this.Source = source;
      this.Skip = skip;
      this.Reason = reason;
    }

    public override string ToString()
    {
      return Source;
    }

 

  }

}
