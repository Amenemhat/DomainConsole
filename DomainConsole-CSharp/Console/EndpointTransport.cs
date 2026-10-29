using System.IO;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ConnectionProfile {
 public string AuthMode {get;set;}="";public string Address {get;set;}="";public bool Ssl {get;set;}public string Identity {get;set;}="";public string ComputerName {get;set;}="";public string Fqdn {get;set;}="";
}
public sealed class ConnectionIdentity {public long SessionMilliseconds {get;set;}public long IdentityMilliseconds {get;set;}public string Identity {get;set;}="";public string ComputerName {get;set;}="";public string Fqdn {get;set;}="";}
public static partial class ConnectionStore {
 public static readonly Dictionary<string,ConnectionProfile> Profiles=new(Store.Read("connection-profiles.json",new Dictionary<string,ConnectionProfile>()),StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,(string User,string Secret)> credentials=new(StringComparer.OrdinalIgnoreCase);
 public static string Normalize(string input)=>IPAddress.TryParse(input.Trim(),out var ip)?ip.ToString():throw new ArgumentException("Укажите корректный IPv4 или IPv6 без порта.");
 public static string Address(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionAddress:Profiles.GetValueOrDefault(t.Host)?.Address??"";
 public static bool Ssl(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionSsl:Profiles.GetValueOrDefault(t.Host)?.Ssl??false;
 public static string Identity(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionIdentity:Profiles.GetValueOrDefault(t.Host)?.Identity??"";
 public static TargetRecord Capture(TargetRecord source){var t=Store.Clone(source);if(!t.ConnectionSnapshot){var p=Profiles.GetValueOrDefault(t.Host);t.ConnectionAuth=p?.AuthMode??"";t.ConnectionAddress=p?.Address??"";t.ConnectionSsl=p?.Ssl??false;t.ConnectionIdentity=p?.Identity??"";t.ConnectionName=p?.ComputerName??"";t.ConnectionSnapshot=true;}return t;}
 public static string WsusName(string host)=>Profiles.TryGetValue(host,out var p)&&p.Fqdn.Length>0?p.Fqdn:IPAddress.TryParse(host,out _)?"":host;
 public static string Description(string host)=>Profiles.TryGetValue(host,out var p)&&p.Address.Length>0?" · подключение: "+p.Address+(p.Ssl?" (HTTPS)":""):"";
 public static void Save()=>Store.Write("connection-profiles.json",Profiles);
 static string Key(string address,bool ssl)=>address+"|"+ssl;
 public static void SetCredential(string address,bool ssl,string user,SecureString password){IntPtr memory=IntPtr.Zero;byte[]? clear=null;try{memory=Marshal.SecureStringToGlobalAllocUnicode(password);clear=new byte[password.Length*2];Marshal.Copy(memory,clear,0,clear.Length);credentials[Key(address,ssl)]=(user,Convert.ToBase64String(ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser)));}finally{if(clear!=null)Array.Clear(clear);if(memory!=IntPtr.Zero)Marshal.ZeroFreeGlobalAllocUnicode(memory);}}
 public static bool HasCredential(string address,bool ssl)=>credentials.ContainsKey(Key(address,ssl));
 public static string CredentialCode(string address,bool ssl){if(!credentials.TryGetValue(Key(address,ssl),out var c))throw new InvalidOperationException("Учётная запись не задана");return LoginCode(new(){User=c.User,Secret=c.Secret});}

}
public sealed partial class RemoteService {
 public static bool Local(TargetRecord t)=>string.IsNullOrEmpty(ConnectionStore.Address(t))&&IsLocal(t.Host);
 const string IdentityScript="$dcOs=Get-CimInstance Win32_ComputerSystem -ErrorAction Stop;@{Identity=[string](Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Cryptography' -Name MachineGuid -ErrorAction Stop).MachineGuid;ComputerName=$env:COMPUTERNAME;Fqdn=$(if($dcOs.PartOfDomain){$env:COMPUTERNAME+'.'+$dcOs.Domain}else{$env:COMPUTERNAME})}";
 public static string SessionCode(TargetRecord t,bool bind=false){string address=ConnectionStore.Address(t);bool ip=address.Length>0;string destination=ip?ConnectionStore.Normalize(address):t.Host;
  if(!ip&&IPAddress.TryParse(destination,out _))throw new InvalidOperationException("Для IP задайте параметры подключения и подтвердите компьютер кнопкой проверки.");
  if(ip&&!bind&&ConnectionStore.Identity(t).Length==0)throw new InvalidOperationException("Компьютер по этому IP ещё не подтверждён. Откройте «Адрес подключения…» и проверьте соединение.");
  string mode=ConnectionStore.AuthMode(t);var login=ConnectionStore.Login(t);if(mode!="Current"&&login==null)throw new InvalidOperationException("Учётная запись не задана. Откройте настройки подключения и задайте общую запись один раз.");string auth=login!=null?ConnectionStore.LoginCode(login):"";
  string code="# DC-ENDPOINT41:"+TransportBudget41.Key(t)+"\n"+auth+"$dcConnectWatch=[Diagnostics.Stopwatch]::StartNew();try{$s=New-PSSession -ComputerName "+PowerShellBridge.Literal(destination)+((ip||login!=null)?" -Authentication Negotiate":"")+(login!=null?" -Credential $dcCredential":"")+(ip&&ConnectionStore.Ssl(t)?" -UseSSL":"")+" -SessionOption (New-PSSessionOption -OpenTimeout 12000 -OperationTimeout 45000 -CancelTimeout 5000 -IdleTimeout 60000) -ErrorAction Stop}catch{throw ('Этап WinRM/аутентификации: '+$_.Exception.Message)};$dcConnectWatch.Stop();";
  if(ip&&!bind){code+="try{$dcIdentity=Invoke-Command -Session $s -ScriptBlock {"+IdentityScript+"} -ErrorAction Stop;if($dcIdentity.Identity -ne "+PowerShellBridge.Literal(ConnectionStore.Identity(t))+(t.ConnectionName.Length>0?" -or $dcIdentity.ComputerName -ne "+PowerShellBridge.Literal(t.ConnectionName):"")+"){throw 'По адресу отвечает другой компьютер. Выполнение отменено; проверьте привязку IP.'}}catch{Remove-PSSession $s;throw};";}
  return code;
 }
 string Invoke(TargetRecord target,string body)=>Local(target)?"# DC-ENDPOINT41:"+TransportBudget41.Key(target)+"\n& { "+body+" }":SessionCode(ConnectionStore.Capture(target))+"try{Invoke-Command -Session $s -ScriptBlock { "+body+" } -ErrorAction Stop}finally{Remove-PSSession $s}";
 string Invoke(string host,string body)=>Invoke(new TargetRecord{Host=host},body);
 public async Task<ConnectionIdentity> ProbeEndpoint(TargetRecord t,Action<string>? progress=null){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));string destination=ConnectionStore.Address(t);if(destination.Length==0)destination=t.Host;var clock=System.Diagnostics.Stopwatch.StartNew();string network=await ProbeNetwork49(destination,ConnectionStore.Ssl(t),timeout.Token,progress);progress?.Invoke(network+"\nPSSession/аутентификация (открытие до 12 с): "+ConnectionStore.AccountLabel(t)+"…");
  var json=await PowerShellBridge.Execute(SessionCode(t,true)+"try{$dcIdentityWatch=[Diagnostics.Stopwatch]::StartNew();try{$dcIdentity=Invoke-Command $s { "+IdentityScript+" } -ErrorAction Stop}catch{throw ('Этап чтения идентичности: '+$_.Exception.Message)};$dcIdentity.SessionMilliseconds=$dcConnectWatch.ElapsedMilliseconds;$dcIdentity.IdentityMilliseconds=$dcIdentityWatch.ElapsedMilliseconds;$dcIdentity|ConvertTo-Json -Compress}finally{Remove-PSSession $s}",timeout.Token);var result=JsonSerializer.Deserialize<ConnectionIdentity>(json,Store.Options)??throw new Exception("Идентичность клиента не получена");progress?.Invoke(network+"\nPSSession/аутентификация: успешно · "+result.SessionMilliseconds+" мс\nЧтение имени и идентификатора: успешно · "+result.IdentityMilliseconds+" мс\nВсего: "+clock.Elapsed.TotalSeconds.ToString("0.0")+" сек");return result;}
 public static string TrustedAddressCheckScript(string address)=>"$ip="+PowerShellBridge.Literal(ConnectionStore.Normalize(address))+";$items=([string](Get-Item 'WSMan:\\localhost\\Client\\TrustedHosts' -ErrorAction Stop).Value).Split(',');$found=$false;foreach($item in $items){if($item.Trim() -and ([System.Management.Automation.WildcardPattern]::new($item.Trim(),[System.Management.Automation.WildcardOptions]::IgnoreCase)).IsMatch($ip)){$found=$true}};$found|ConvertTo-Json -Compress";
 public async Task<bool> IsTrustedAddress(string address){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));try{var value=await PowerShellBridge.Execute(TrustedAddressCheckScript(address),timeout.Token);return JsonSerializer.Deserialize<bool>(value);}catch(Exception ex){throw new IOException("Этап проверки TrustedHosts на компьютере консоли: "+ex.Message);}}
 public static string TrustAddressScript(string address)=>"$p='WSMan:\\localhost\\Client\\TrustedHosts';$old=[string](Get-Item $p -ErrorAction Stop).Value;$items=@($old.Split(',')|ForEach-Object {$_.Trim()}|Where-Object {$_});$ip="+PowerShellBridge.Literal(ConnectionStore.Normalize(address))+";if($items -notcontains $ip){$items+=$ip;Set-Item $p -Value ($items -join ',') -Force -ErrorAction Stop};'Добавлен конкретный адрес; прежние записи сохранены.'";
 public async Task TrustAddress(string address){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));try{await PowerShellBridge.Execute(TrustAddressScript(address),timeout.Token);}catch(Exception ex){throw new IOException("Этап разрешения IP в TrustedHosts: "+ex.Message);}}
}
