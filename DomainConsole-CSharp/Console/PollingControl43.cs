namespace DomainConsole;
public static class PollingControl43 {
 sealed record ReadContext(CancellationToken Token);
 static readonly AsyncLocal<ReadContext?> context=new();
 static readonly AsyncLocal<string?> manual56=new();
 public static bool IsManual56=>manual56.Value!=null;
 public static string Lane56=>manual56.Value??"";
 public static bool BackgroundPaused56=>TransportBudget41.Paused&&!IsManual56;
 public static IDisposable Manual56(string lane="manual") {var old=manual56.Value;var read=context.Value;manual56.Value=lane;context.Value=null;return new ManualScope56(old,read);}
 sealed class ManualScope56(string? old,ReadContext? read):IDisposable {public void Dispose(){manual56.Value=old;context.Value=read;}}
 static CancellationTokenSource cycle=new();
 public static bool IsRead=>context.Value!=null;
 public static CancellationToken Token=>context.Value?.Token??(IsManual56?CancellationToken.None:cycle.Token);
 public static bool Cancelled=>IsRead&&Token.IsCancellationRequested;
 public static IDisposable Read(){if(BackgroundPaused56)throw new OperationCanceledException("Опрос выключен в этой консоли");var previous=context.Value;context.Value=previous??new ReadContext(IsManual56?CancellationToken.None:cycle.Token);return new Scope(previous);}
 sealed class Scope(ReadContext? previous):IDisposable {public void Dispose()=>context.Value=previous;}
 public static void Pause(){TransportBudget41.Paused=true;cycle.Cancel();}
 public static void Resume(){cycle=new();TransportBudget41.Paused=false;}
 public static event Action<string,string>? Activity;
 public static void Show(string key,string text)=>Activity?.Invoke(key,text);
}
