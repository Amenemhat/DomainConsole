using System.Windows;
namespace DomainConsole;
public sealed class ClientConnectionState {
 public DateTimeOffset FailureObservedSince {get;set;}public DateTimeOffset LastSuccess {get;set;}public DateTimeOffset LastAttempt {get;set;}public string Error {get;set;}="";
 public DateTimeOffset EvidenceStarted38 {get;set;}public DateTimeOffset LastFailureCounted38 {get;set;}public DateTimeOffset LastRecoveryCounted38 {get;set;}
 public int Failures38 {get;set;}public int Recovery38 {get;set;}public bool Lost38 {get;set;}
 public bool Observe38(bool success,string error,DateTimeOffset started,DateTimeOffset now){
  if(started<EvidenceStarted38)return false;EvidenceStarted38=started;LastAttempt=now;
  if(success){LastSuccess=now;Error="";Failures38=0;FailureObservedSince=default;if(Lost38){if(LastRecoveryCounted38==default||now-LastRecoveryCounted38>=TimeSpan.FromSeconds(10)){Recovery38++;LastRecoveryCounted38=now;}if(Recovery38>=2){Lost38=false;Recovery38=0;LastRecoveryCounted38=default;}}return true;}
  Error=error;Recovery38=0;LastRecoveryCounted38=default;if(FailureObservedSince==default)FailureObservedSince=now;
  if(LastFailureCounted38==default||now-LastFailureCounted38>=TimeSpan.FromSeconds(10)){Failures38++;LastFailureCounted38=now;}
  if(Failures38>=3&&now-FailureObservedSince>=TimeSpan.FromSeconds(60))Lost38=true;
  return true;
 }
 public string Kind38(DateTimeOffset now){if(Error.Contains("введите учётные",StringComparison.OrdinalIgnoreCase))return "Unknown";if(MainWindow.AccessError38(Error))return "Access";if(Lost38)return LastSuccess!=default&&Error.Length==0?"Delayed":"Unreachable";if(Error.Length>0){if(LastSuccess!=default&&now-LastSuccess<TimeSpan.FromSeconds(60)&&now-FailureObservedSince<TimeSpan.FromSeconds(30))return "Connected";return "Delayed";}return LastSuccess==default?"Unknown":now-LastSuccess<TimeSpan.FromMinutes(5)?"Connected":"SavedConnected";}
}
public partial class MainWindow {
 readonly HashSet<string> liveConnections=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,ClientConnectionState> clientConnections=new(Store.Read("client-connections.json",new Dictionary<string,ClientConnectionState>()),StringComparer.OrdinalIgnoreCase);
 public static bool AccessError38(string text)=>text.Length>0&&AccessError(text);
 static bool StateFresh(DomainConsole.Shared.RemoteState? state)=>state!=null&&DateTimeOffset.TryParse(state.Updated,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
 bool ConnectionFresh(string host)=>clientConnections.TryGetValue(host,out var value)&&liveConnections.Contains(host)&&!value.Lost38&&DateTimeOffset.UtcNow-value.LastSuccess<TimeSpan.FromSeconds(90);
 void ConnectionSucceeded(string host,DateTimeOffset started=default){var now=DateTimeOffset.UtcNow;var value=clientConnections.GetValueOrDefault(host)??new();if(!value.Observe38(true,"",started==default?now:started,now))return;clientConnections[host]=value;Event31(host,"link","connected","Связь WinRM подтверждена","Success");liveConnections.Add(host);Store.Write("client-connections.json",clientConnections);}
 void ConnectionFailed(string host,string message,DateTimeOffset started=default){var now=DateTimeOffset.UtcNow;var value=clientConnections.GetValueOrDefault(host)??new();if(!value.Observe38(false,message,started==default?now:started,now))return;clientConnections[host]=value;Event31(host,"link",message,ConnectionBrief31(message),"Warning",message);liveConnections.Add(host);Store.Write("client-connections.json",clientConnections);}
 string ConnectionDescription(string host){var row=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase))??new(new(){Host=host});return LinkHint(row).Split('\n')[0]+(clientConnections.TryGetValue(host,out var c)&&c.LastSuccess!=default?" · ответ "+Age(DateTimeOffset.UtcNow-c.LastSuccess)+" назад":"");}
}
