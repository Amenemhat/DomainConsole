using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace DomainConsole.Agent {
 // Windows tools can mix OEM command echoes and BOM-less UTF-16 output in one pipe.
 internal static class OutputReader {
  static readonly Encoding Utf8=new UTF8Encoding(false,true);
  static bool UnicodeLine(List<byte> bytes){if(bytes.Count>=2&&bytes[0]==255&&bytes[1]==254)return true;if(bytes.Count<2||bytes.Count%2!=0)return false;int high=0;for(int i=1;i<bytes.Count;i+=2)if(bytes[i]==0||bytes[i]==4)high++;return high>=Math.Max(1,bytes.Count/2*3/4);}
  static string Decode(List<byte> bytes,Encoding fallback,bool unicode){if(bytes.Count==0)return "";var data=bytes.ToArray();if(unicode)return Encoding.Unicode.GetString(data).TrimStart('\uFEFF');try{return Utf8.GetString(data).TrimStart('\uFEFF');}catch(DecoderFallbackException){return fallback.GetString(data);}}
  public static void Read(Stream input,Stream raw,Encoding fallback,Action<string> line){var pending=new List<byte>();var block=new byte[4096];bool skipZero=false,lastUnicode=false;int n;
   while((n=input.Read(block,0,block.Length))>0){raw.Write(block,0,n);raw.Flush();for(int i=0;i<n;i++){byte b=block[i];if(skipZero){skipZero=false;if(b==0)continue;}if(b==13||b==10){bool unicode=pending.Count==0?lastUnicode:UnicodeLine(pending);line(Decode(pending,fallback,unicode));pending.Clear();lastUnicode=unicode;skipZero=true;}else pending.Add(b);}}
   if(pending.Count>0)line(Decode(pending,fallback,UnicodeLine(pending)));
  }
 }
}
