using System.Windows;
namespace DomainConsole;
public sealed class ClientConnectionState {public DateTimeOffset LastSuccess {get;set;}public DateTimeOffset LastAttempt {get;set;}public string Error {get;set;}="";}
public partial class MainWindow {
 readonly Dictionary<string,ClientConnectionState> clientConnections=new(StringComparer.OrdinalIgnoreCase);
 static bool StateFresh(DomainConsole.Shared.RemoteState? state)=>state!=null&&DateTimeOffset.TryParse(state.Updated,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
 bool ConnectionFresh(string host)=>clientConnections.TryGetValue(host,out var value)&&value.Error.Length==0&&DateTimeOffset.UtcNow-value.LastSuccess<TimeSpan.FromSeconds(90);
 void ConnectionSucceeded(string host){clientConnections[host]=new(){LastSuccess=DateTimeOffset.UtcNow,LastAttempt=DateTimeOffset.UtcNow};}
 void ConnectionFailed(string host,string message){var previous=clientConnections.GetValueOrDefault(host)??new();previous.LastAttempt=DateTimeOffset.UtcNow;previous.Error=message;clientConnections[host]=previous;}
 string ConnectionDescription(string host){if(!clientConnections.TryGetValue(host,out var value))return "Текущая связь не подтверждена";return ConnectionFresh(host)?"Связь подтверждена · "+value.LastSuccess.ToLocalTime().ToString("HH:mm:ss"):"Связь / чтение состояния не подтверждены · последний ответ "+(value.LastSuccess==default?"отсутствует":value.LastSuccess.ToLocalTime().ToString("HH:mm:ss"))+" · "+value.Error;}
}
