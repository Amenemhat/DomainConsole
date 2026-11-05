using System;
using System.IO;
using System.Text.RegularExpressions;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // A single native servicing invocation needs no PowerShell formatting pipeline.
  // Compound scripts retain their original shell and semantics.
  static bool NativeProgress60(string code,string prefix,out int exit){
   exit=0;var match=Regex.Match(code.Trim(),@"^(?<exe>sfc|dism)(?:\.exe)?\s+(?<args>[/A-Za-z0-9_:.\\\-\s]+)$",RegexOptions.IgnoreCase);
   if(!match.Success||code.IndexOf('\n')>=0||code.IndexOf('\r')>=0)return false;
   string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),match.Groups["exe"].Value+".exe");
   exit=Run(exe,match.Groups["args"].Value,prefix);File.WriteAllText(FilePath(prefix+".native.json"),exit.ToString());return true;
  }
 }
}
