using System;
using System.IO;
using System.Text;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal sealed class ProgressLogWriter:IDisposable {
  readonly FileStream file;readonly Encoding encoding=new UTF8Encoding(true);long progressAt=-1;string previous;
  public ProgressLogWriter(string path){file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);file.Seek(0,SeekOrigin.End);if(file.Length==0){var bom=encoding.GetPreamble();file.Write(bom,0,bom.Length);}}
  public void WriteLine(string line){var key=ProgressText.Key(line);if(key==null&&previous!=null&&string.IsNullOrWhiteSpace(line))return;
   if(key!=null&&key==previous&&progressAt>=0){file.Position=progressAt;file.SetLength(progressAt);}else progressAt=key==null?-1:file.Position;
   var bytes=encoding.GetBytes(line+Environment.NewLine);file.Write(bytes,0,bytes.Length);file.Flush();previous=key;
  }
  public void Dispose(){file.Dispose();}
 }
}
