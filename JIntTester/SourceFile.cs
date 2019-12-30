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

    public string Source { get; set; }
    public bool Skip { get; set; }
    public string Reason { get; set; }
    public string BasePath { get; set; }



    public override string ToString()
    {
      return Source;
    }

 

  }

}
