using System.Windows;
namespace DomainConsole;
public sealed class ClientConnectionState {public DateTimeOffset FailureObservedSince {get;set;}public DateTimeOffset LastSuccess {get;set;}public DateTimeOffset LastAttempt {get;set;}public string Error {get;set;}="";}
public partial class MainWindow {
 readonly HashSet<string> liveConnections=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,ClientConnectionState> clientConnections=new(Store.Read("client-connections.json",new Dictionary<string,ClientConnectionState>()),StringComparer.OrdinalIgnoreCase);
 static bool StateFresh(DomainConsole.Shared.RemoteState? state)=>state!=null&&DateTimeOffset.TryParse(state.Updated,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
 bool ConnectionFresh(string host)=>clientConnections.TryGetValue(host,out var value)&&liveConnections.Contains(host)&&value.Error.Length==0&&DateTimeOffset.UtcNow-value.LastSuccess<TimeSpan.FromSeconds(90);
 void ConnectionSucceeded(string host){Event31(host,"link","connected","Связь WinRM подтверждена","Success");liveConnections.Add(host);clientConnections[host]=new(){LastSuccess=DateTimeOffset.UtcNow,LastAttempt=DateTimeOffset.UtcNow};Store.Write("client-connections.json",clientConnections);}
 void ConnectionFailed(string host,string message){Event31(host,"link",ConnectionBrief31(message),ConnectionBrief31(message),"Warning",message);var previous=clientConnections.GetValueOrDefault(host)??new();if(previous.Error.Length==0)previous.FailureObservedSince=DateTimeOffset.UtcNow;previous.LastAttempt=DateTimeOffset.UtcNow;liveConnections.Add(host);previous.Error=message;clientConnections[host]=previous;Store.Write("client-connections.json",clientConnections);}
 string ConnectionDescription(string host){var row=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase))??new(new(){Host=host});return LinkHint(row).Split('\n')[0]+(clientConnections.TryGetValue(host,out var c)&&c.LastSuccess!=default?" · ответ "+Age(DateTimeOffset.UtcNow-c.LastSuccess)+" назад":"");}
}

