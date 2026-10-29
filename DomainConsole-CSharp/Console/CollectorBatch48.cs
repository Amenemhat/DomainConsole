namespace DomainConsole;

// Cancellation stops admission, not an already admitted remote cleanup. The latter
// must finish its bounded stop/ownership verification and report its real outcome.
internal sealed class CollectorBatch48
{
 public bool StopRequested { get; private set; }
 public int Started { get; private set; }
 public int Finished { get; private set; }
 public void Stop() => StopRequested = true;
 public async Task Run<T>(IReadOnlyList<T> targets, Func<bool> allowed, Func<T,Task> execute)
 {
  foreach(var target in targets)
  {
   if(StopRequested || !allowed()) break;
   Started++;
   try { await execute(target); }
   finally { Finished++; }
  }
 }
}
