namespace DomainConsole;

// A delayed background save must never overwrite a newer manual result.
public sealed class LatestWrite58 {
 readonly object gate=new();long revision;
 public long Reserve()=>Interlocked.Increment(ref revision);
 public bool Commit(long ticket,Action write){lock(gate){if(ticket!=Interlocked.Read(ref revision))return false;write();return true;}}
}
