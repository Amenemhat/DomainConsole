using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
namespace DomainConsole.Shared {
 public static class ProgressText {
  static readonly Regex Bar=new Regex(@"^\s*\[[=\-\s]*(?<p>\d{1,3}(?:[.,]\d+)?)\s*%[=\-\s]*\]\s*$",RegexOptions.Compiled);
  static readonly Regex Percent=new Regex(@"^\s*(?<p>\d{1,3}(?:[.,]\d+)?)\s*%\s*$",RegexOptions.Compiled);
  static readonly Regex Verification=new Regex(@"^\s*(?:Verification\s+(?<p>\d{1,3}(?:[.,]\d+)?)\s*%\s+complete\.?|Проверка\s+(?<p>\d{1,3}(?:[.,]\d+)?)\s*%\s+завершена\.?|Завершение проверки\s+(?<p>\d{1,3}(?:[.,]\d+)?)\s*%\s*\.?)\s*$",RegexOptions.Compiled|RegexOptions.IgnoreCase);
  public static string Key(string line){foreach(var pair in new[]{Tuple.Create(Bar,"bar"),Tuple.Create(Percent,"percent"),Tuple.Create(Verification,"sfc")}){var m=pair.Item1.Match(line);double value;if(m.Success&&double.TryParse(m.Groups["p"].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value)&&value<=100)return pair.Item2;}return null;}
  public static string Compact(string text){var result=new List<string>();var blanks=new List<string>();string previous=null;int progressIndex=-1;
   foreach(string line in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')){string key=Key(line);if(key!=null){if(key==previous&&progressIndex>=0){result[progressIndex]=line;blanks.Clear();continue;}result.AddRange(blanks);blanks.Clear();result.Add(line);previous=key;progressIndex=result.Count-1;}else if(string.IsNullOrWhiteSpace(line)&&previous!=null){blanks.Add(line);}else{result.AddRange(blanks);blanks.Clear();result.Add(line);previous=null;progressIndex=-1;}}
   result.AddRange(blanks);return string.Join(Environment.NewLine,result);
  }
 }
}
