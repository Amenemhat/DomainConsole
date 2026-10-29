using DomainConsole.Shared;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Diagnostics;
namespace DomainConsole;
public sealed partial class RemoteService {
 // A TCP connection confirms reachability to this port, not correctness of every route or VPN policy.
 public static async Task<string> ProbeNetwork49(string destination,bool ssl,CancellationToken cancellation,Action<string>? progress=null,int? port=null){
  var lines=new List<string>();var clock=Stopwatch.StartNew();
  void Report(string text){lines.Add(text);progress?.Invoke(string.Join("\n",lines));}
  using var dnsLimit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);dnsLimit.CancelAfter(TimeSpan.FromSeconds(5));
  if(IPAddress.TryParse(destination,out _))Report("DNS: не требуется — указан IP");
  else {try{var addresses=await Dns.GetHostAddressesAsync(destination,dnsLimit.Token);if(addresses.Length==0)throw new IOException("Адреса не получены");Report("DNS: "+string.Join(", ",addresses.Select(a=>a.ToString()))+" · "+clock.ElapsedMilliseconds+" мс");}catch(Exception ex){throw new IOException("Этап DNS (предел 5 с): "+ex.Message,ex);}}
  using var tcp=new TcpClient();using var tcpLimit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);tcpLimit.CancelAfter(TimeSpan.FromSeconds(5));
  int endpointPort=port??(ssl?5986:5985);clock.Restart();
  try{await tcp.ConnectAsync(destination,endpointPort,tcpLimit.Token);Report("Маршрут/TCP: соединение с "+destination+":"+endpointPort+" установлено · "+clock.ElapsedMilliseconds+" мс");}
  catch(Exception ex){throw new IOException("Этап маршрут/TCP WinRM (предел 5 с): "+ex.Message+". Причина не разделена между маршрутом, VPN, фильтрацией и listener; аутентификация ещё не выполнялась.",ex);}
  if(ssl){using var tls=new SslStream(tcp.GetStream(),false);using var tlsLimit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);tlsLimit.CancelAfter(TimeSpan.FromSeconds(10));clock.Restart();
   try{await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost=destination},tlsLimit.Token);Report("HTTPS/TLS: сертификат и имя проверены средствами Windows · "+clock.ElapsedMilliseconds+" мс");}
   catch(Exception ex){throw new IOException("Этап HTTPS/TLS-доверия (предел 10 с): "+ex.Message+". Проверка сертификата не отключалась; аутентификация WinRM ещё не выполнялась.",ex);}
  }else Report("HTTPS/TLS: не используется (WinRM HTTP)");
  return string.Join("\n",lines);
 }
 public static string ExternalConnectionCheck49(TargetRecord target){
  string address=ConnectionStore.Address(target);bool ip=address.Length>0;string destination=ip?address:target.Host;
  bool credential=ConnectionStore.AuthMode(target)!="Current";
  return "# Windows PowerShell 5.1 на компьютере консоли; параметры доверия не изменяются\n"+
   (credential?"$credential = Get-Credential # та же учётная запись, что выбрана в консоли\n":"# Используется текущий вход Windows\n")+
   "$session = New-PSSession -ComputerName "+PowerShellBridge.Literal(destination)+(ip||credential?" -Authentication Negotiate":"")+(credential?" -Credential $credential":"")+(ip&&ConnectionStore.Ssl(target)?" -UseSSL":"")+" -SessionOption (New-PSSessionOption -OpenTimeout 12000 -OperationTimeout 45000 -CancelTimeout 5000) -ErrorAction Stop\ntry { Invoke-Command -Session $session { $env:COMPUTERNAME } -ErrorAction Stop } finally { Remove-PSSession $session }";
 }
}

public partial class MainWindow {
 static async Task VerifyEndpointDiagnostics49(){
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
  foreach(var host in new[]{"127.0.0.1","localhost"}){
   var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
   try{var accept=listener.AcceptTcpClientAsync(deadline.Token);string result=await RemoteService.ProbeNetwork49(host,false,deadline.Token,port:((IPEndPoint)listener.LocalEndpoint).Port);using var peer=await accept;
    if(!result.Contains("Маршрут/TCP: соединение")||!result.Contains("HTTPS/TLS: не используется")||!result.Contains("DNS:"))throw new Exception("Endpoint network stages missing: "+result);
    if((host=="localhost")==result.Contains("DNS: не требуется"))throw new Exception("DNS stage did not distinguish hostname from literal IP");
   }finally{listener.Stop();}
  }
  var tlsListener=new TcpListener(IPAddress.Loopback,0);tlsListener.Start();
  try{var accepted=tlsListener.AcceptTcpClientAsync(deadline.Token);var probe=RemoteService.ProbeNetwork49("127.0.0.1",true,deadline.Token,port:((IPEndPoint)tlsListener.LocalEndpoint).Port);using(var peer=await accepted){peer.Close();}
   bool rejected=false;try{await probe;}catch(IOException ex){rejected=ex.Message.Contains("Этап HTTPS/TLS-доверия")&&ex.Message.Contains("не отключалась");}if(!rejected)throw new Exception("Invalid TLS endpoint did not report its own stage");
  }finally{tlsListener.Stop();}
  var closed=new TcpListener(IPAddress.Loopback,0);closed.Start();int port=((IPEndPoint)closed.LocalEndpoint).Port;closed.Stop();
  bool tcpRejected=false;try{await RemoteService.ProbeNetwork49("127.0.0.1",false,deadline.Token,port:port);}catch(IOException ex){tcpRejected=ex.Message.Contains("маршрут/TCP")&&ex.Message.Contains("аутентификация ещё не выполнялась");}if(!tcpRejected)throw new Exception("Closed endpoint misclassified as authentication failure");
  var command=RemoteService.ExternalConnectionCheck49(new(){Host="fixture.invalid",ConnectionSnapshot=true,ConnectionAuth="Current",ConnectionAddress="127.0.0.1",ConnectionSsl=true});
  if(!command.Contains("-Authentication Negotiate")||!command.Contains("-UseSSL")||command.Contains("-Credential")||command.Contains("Set-Item"))throw new Exception("External comparison changed transport/authentication or trust settings");
 }
}
