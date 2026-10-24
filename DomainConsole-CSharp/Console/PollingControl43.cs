namespace DomainConsole;
public static class PollingControl43 {
 sealed record ReadContext(CancellationToken Token);
 static readonly AsyncLocal<ReadContext?> context=new();
 static CancellationTokenSource cycle=new();
 public static bool IsRead=>context.Value!=null;
 public static CancellationToken Token=>context.Value?.Token??cycle.Token;
 public static bool Cancelled=>IsRead&&Token.IsCancellationRequested;
 public static IDisposable Read(){if(TransportBudget41.Paused)throw new OperationCanceledException("Опрос выключен в этой консоли");var previous=context.Value;context.Value=previous??new ReadContext(cycle.Token);return new Scope(previous);}
 sealed class Scope(ReadContext? previous):IDisposable {public void Dispose()=>context.Value=previous;}
 public static void Pause(){TransportBudget41.Paused=true;cycle.Cancel();}
 public static void Resume(){cycle=new();TransportBudget41.Paused=false;}
 public static event Action<string,string>? Activity;
 public static void Show(string key,string text)=>Activity?.Invoke(key,text);
}
