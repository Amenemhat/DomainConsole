using System.IO;
namespace DomainConsole;
public static class PollingControl43 {
 static readonly AsyncLocal<int> depth=new();
 static CancellationTokenSource cycle=new();
 public static bool IsRead=>depth.Value>0;
 public static CancellationToken Token=>cycle.Token;
 public static IDisposable Read(){if(TransportBudget41.Paused)throw new OperationCanceledException("Опрос выключен в этой консоли");depth.Value++;return new Scope();}
 sealed class Scope:IDisposable {public void Dispose()=>depth.Value=Math.Max(0,depth.Value-1);}
 public static void Pause(){TransportBudget41.Paused=true;cycle.Cancel();}
 public static void Resume(){cycle=new();TransportBudget41.Paused=false;}
 public static event Action<string,string>? Activity;
 public static void Show(string key,string text)=>Activity?.Invoke(key,text);
}
