using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ConnectionProfile {
 public string Address {get;set;}="";public bool Ssl {get;set;}public string Identity {get;set;}="";public string ComputerName {get;set;}="";public string Fqdn {get;set;}="";
}
public sealed class ConnectionIdentity {public string Identity {get;set;}="";public string ComputerName {get;set;}="";public string Fqdn {get;set;}="";}
public static class ConnectionStore {
 public static readonly Dictionary<string,ConnectionProfile> Profiles=new(Store.Read("connection-profiles.json",new Dictionary<string,ConnectionProfile>()),StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,(string User,string Secret)> credentials=new(StringComparer.OrdinalIgnoreCase);
 public static string Normalize(string input)=>IPAddress.TryParse(input.Trim(),out var ip)?ip.ToString():throw new ArgumentException("Укажите корректный IPv4 или IPv6 без порта.");
 public static string Address(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionAddress:Profiles.GetValueOrDefault(t.Host)?.Address??"";
 public static bool Ssl(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionSsl:Profiles.GetValueOrDefault(t.Host)?.Ssl??false;
 public static string Identity(TargetRecord t)=>t.ConnectionSnapshot?t.ConnectionIdentity:Profiles.GetValueOrDefault(t.Host)?.Identity??"";
 public static TargetRecord Capture(TargetRecord source){var t=Store.Clone(source);if(!t.ConnectionSnapshot){var p=Profiles.GetValueOrDefault(t.Host);t.ConnectionAddress=p?.Address??"";t.ConnectionSsl=p?.Ssl??false;t.ConnectionIdentity=p?.Identity??"";t.ConnectionName=p?.ComputerName??"";t.ConnectionSnapshot=true;}return t;}
 public static string WsusName(string host)=>Profiles.TryGetValue(host,out var p)&&p.Fqdn.Length>0?p.Fqdn:IPAddress.TryParse(host,out _)?"":host;
 public static string Description(string host)=>Profiles.TryGetValue(host,out var p)&&p.Address.Length>0?" · подключение: "+p.Address+(p.Ssl?" (HTTPS)":""):"";
 public static void Save()=>Store.Write("connection-profiles.json",Profiles);
 static string Key(string address,bool ssl)=>address+"|"+ssl;
 public static void SetCredential(string address,bool ssl,string user,SecureString password){IntPtr memory=IntPtr.Zero;byte[]? clear=null;try{memory=Marshal.SecureStringToGlobalAllocUnicode(password);clear=new byte[password.Length*2];Marshal.Copy(memory,clear,0,clear.Length);credentials[Key(address,ssl)]=(user,Convert.ToBase64String(ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser)));}finally{if(clear!=null)Array.Clear(clear);if(memory!=IntPtr.Zero)Marshal.ZeroFreeGlobalAllocUnicode(memory);}}
 public static bool HasCredential(string address,bool ssl)=>credentials.ContainsKey(Key(address,ssl));
 public static string CredentialCode(string address,bool ssl){if(!credentials.TryGetValue(Key(address,ssl),out var c))throw new InvalidOperationException("Для подключения по IP откройте «Адрес подключения…» и введите учётные данные на текущий сеанс.");return "Add-Type -AssemblyName System.Security;$dcBytes=[Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String("+PowerShellBridge.Literal(c.Secret)+"),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);$dcSecure=New-Object Security.SecureString;try{foreach($dcChar in [Text.Encoding]::Unicode.GetString($dcBytes).ToCharArray()){$dcSecure.AppendChar($dcChar)}}finally{[Array]::Clear($dcBytes,0,$dcBytes.Length)};$dcSecure.MakeReadOnly();$dcCredential=New-Object Management.Automation.PSCredential("+PowerShellBridge.Literal(c.User)+",$dcSecure);";}
}
public sealed partial class RemoteService {
 public static bool Local(TargetRecord t)=>string.IsNullOrEmpty(ConnectionStore.Address(t))&&IsLocal(t.Host);
 const string IdentityScript="$dcOs=Get-CimInstance Win32_ComputerSystem -ErrorAction Stop;@{Identity=[string](Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Cryptography' -Name MachineGuid -ErrorAction Stop).MachineGuid;ComputerName=$env:COMPUTERNAME;Fqdn=$(if($dcOs.PartOfDomain){$env:COMPUTERNAME+'.'+$dcOs.Domain}else{$env:COMPUTERNAME})}";
 public static string SessionCode(TargetRecord t,bool bind=false){string address=ConnectionStore.Address(t);bool ip=address.Length>0;string destination=ip?ConnectionStore.Normalize(address):t.Host;
  if(!ip&&IPAddress.TryParse(destination,out _))throw new InvalidOperationException("Для IP задайте параметры подключения и подтвердите компьютер кнопкой проверки.");
  if(ip&&!bind&&ConnectionStore.Identity(t).Length==0)throw new InvalidOperationException("Компьютер по этому IP ещё не подтверждён. Откройте «Адрес подключения…» и проверьте соединение.");
  string auth=ip?ConnectionStore.CredentialCode(destination,ConnectionStore.Ssl(t)):"";
  string code=auth+"$s=New-PSSession -ComputerName "+PowerShellBridge.Literal(destination)+(ip?" -Authentication Negotiate -Credential $dcCredential":"")+(ip&&ConnectionStore.Ssl(t)?" -UseSSL":"")+" -SessionOption (New-PSSessionOption -OpenTimeout 12000 -OperationTimeout 30000) -ErrorAction Stop;";
  if(ip&&!bind){code+="try{$dcIdentity=Invoke-Command -Session $s -ScriptBlock {"+IdentityScript+"} -ErrorAction Stop;if($dcIdentity.Identity -ne "+PowerShellBridge.Literal(ConnectionStore.Identity(t))+(t.ConnectionName.Length>0?" -or $dcIdentity.ComputerName -ne "+PowerShellBridge.Literal(t.ConnectionName):"")+"){throw 'По адресу отвечает другой компьютер. Выполнение отменено; проверьте привязку IP.'}}catch{Remove-PSSession $s;throw};";}
  return code;
 }
 string Invoke(TargetRecord target,string body)=>Local(target)?"& { "+body+" }":SessionCode(ConnectionStore.Capture(target))+"try{Invoke-Command -Session $s -ScriptBlock { "+body+" } -ErrorAction Stop}finally{Remove-PSSession $s}";
 string Invoke(string host,string body)=>Invoke(new TargetRecord{Host=host},body);
 public async Task<ConnectionIdentity> ProbeEndpoint(TargetRecord t){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var json=await PowerShellBridge.Execute(SessionCode(t,true)+"try{Invoke-Command $s { "+IdentityScript+"|ConvertTo-Json -Compress } -ErrorAction Stop}finally{Remove-PSSession $s}",timeout.Token);return JsonSerializer.Deserialize<ConnectionIdentity>(json,Store.Options)??throw new Exception("Идентичность клиента не получена");}
 public async Task TrustAddress(string address){address=ConnectionStore.Normalize(address);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await PowerShellBridge.Execute("$p='WSMan:\\localhost\\Client\\TrustedHosts';$old=[string](Get-Item $p -ErrorAction Stop).Value;$items=@($old.Split(',')|ForEach-Object {$_.Trim()}|Where-Object {$_});$ip="+PowerShellBridge.Literal(address)+";if($items -notcontains $ip){$items+=$ip;Set-Item $p -Value ($items -join ',') -Force -ErrorAction Stop};'Добавлен конкретный адрес; прежние записи сохранены.'",timeout.Token);}
}
