namespace DomainConsole;

// The admission token can cancel a local wait only. Once the transport starts,
// cancellation must address the specific persisted job on the client.
internal sealed class RebootAdmission : IDisposable {
 internal static readonly AsyncLocal<RebootAdmission?> Current=new();
 readonly CancellationTokenSource waiting=new();
 int phase;
 internal CancellationToken Token=>waiting.Token;
 internal bool Cancelled=>Volatile.Read(ref phase)==2;
 internal bool Cancel(){
  if(Interlocked.CompareExchange(ref phase,2,0)!=0)return false;
  waiting.Cancel();return true;
 }
 internal void Enter(){
  if(Interlocked.CompareExchange(ref phase,1,0)==2)throw new OperationCanceledException(waiting.Token);
 }
 public void Dispose()=>waiting.Dispose();
}
