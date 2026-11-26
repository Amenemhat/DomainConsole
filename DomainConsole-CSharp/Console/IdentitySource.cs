using System.Text.Json;
using DomainConsole.Shared;

namespace DomainConsole;

public partial class MainWindow {
 // A manual pass validates each distinct policy address at most once.
 async Task ResolveIdentitySource(ClientIdentity58 item,WsusSettings settings,Dictionary<string,string> checkedSources,CancellationToken token,bool retry=false){
  if(!IdentityPolicy58.Usable(item)||SameServer(item.Server,settings)||string.IsNullOrWhiteSpace(settings.Host))return;
  var policy=new Uri(item.Server);
  if(policy.Port!=settings.Port||policy.Scheme!=(settings.Ssl?"https":"http")||policy.AbsolutePath!="/"||policy.UserInfo.Length!=0||policy.Query.Length!=0||policy.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))return;
  if(!checkedSources.TryGetValue(item.Server,out var verified)){
   if(checkedSources.Count>=64)return;
   for(int attempt=0;;attempt++){
    try{verified=await remote.VerifyWsusAlias(settings,policy.DnsSafeHost,token);break;}
    catch(TimeoutException) when(retry&&attempt==0&&!token.IsCancellationRequested){await Task.Delay(500,token);}
    catch(Exception) when(!token.IsCancellationRequested){verified="";break;}
   }
   checkedSources[item.Server]=verified;
  }
  if(Guid.TryParse(verified,out var id)&&id!=Guid.Empty){
   item.VerifiedSource=new UriBuilder(settings.Ssl?"https":"http",settings.Host,settings.Port).Uri.AbsoluteUri.TrimEnd('/');
   item.ServerId=verified;item.SourceCheckedAt=DateTimeOffset.UtcNow.ToString("o");
  }
 }
}

public sealed partial class RemoteService {
 public async Task<string> VerifyWsusAlias(WsusSettings settings,string alias,CancellationToken token){
  using var read=PollingControl43.Read();
  using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);stop.CancelAfter(TimeSpan.FromSeconds(30));
  var script="$aliasHost="+PowerShellBridge.Literal(alias)+";$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";"+VerifyWsusAliasScript;
  return JsonSerializer.Deserialize<string>(await PowerShellBridge.Execute(Invoke(settings.Host,script),stop.Token))??"";
 }
 internal const string VerifyWsusAliasScript="""
$ErrorActionPreference='Stop'
$local=[Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName.TrimEnd('.')
$address=$null
$sameHost=if([Net.IPAddress]::TryParse($aliasHost,[ref]$address)){
 @([Net.Dns]::GetHostAddresses($local)|Where-Object {$_.Equals($address)}).Count -gt 0
}else{
 [Net.Dns]::GetHostEntry($aliasHost).HostName.TrimEnd('.') -eq $local
}
$verified=''
if($local -and $sameHost){
 [void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
 $primary=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
 $alias=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($aliasHost,$ssl,$port)
 $a=$primary.GetConfiguration().ServerId
 $b=$alias.GetConfiguration().ServerId
 if($a -and $a -ne [guid]::Empty -and $a -eq $b){$verified=$a.ToString()}
}
ConvertTo-Json -InputObject $verified -Compress
""";
}
