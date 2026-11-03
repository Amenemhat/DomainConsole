using DomainConsole.Shared;
namespace DomainConsole;
public static class SavedEvidence55 {
 // 0.53 persisted host|address|identity. 0.54 added TLS and connection generation
 // to EndpointKey for in-flight transport guards. Read old evidence only before
 // an explicit reconnect; never relax the transport key or migrate timestamps.
 public static bool Matches(string saved,TargetRecord target,string suffix=""){
  if(saved==Identity40.EndpointKey(target)+suffix)return true;
  if(ConnectionStore.Generation54(target)!=0)return false;
  string legacy=target.Host.ToUpperInvariant()+"|"+ConnectionStore.Address(target)+"|"+ConnectionStore.Identity(target);
  return saved==legacy+suffix;
 }
}
