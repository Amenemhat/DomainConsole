using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ProtectedLogin {public string User {get;set;}="";public string Secret {get;set;}="";}
public sealed class AuthenticationSettings {public string Mode {get;set;}="Current";}
public static class WindowsLoginVault {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Credential {public uint Flags,Type;public string TargetName;public string? Comment;public System.Runtime.InteropServices.ComTypes.FILETIME Written;public uint Size;public IntPtr Blob;public uint Persist,Count;public IntPtr Attributes;public string? Alias;public string UserName;}
 [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Write(ref Credential c,uint flags);
 [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Read(string target,uint type,uint flags,out IntPtr p);
 [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Delete(string target,uint type,uint flags);
 [DllImport("advapi32.dll")]static extern void CredFree(IntPtr p);
 public static ProtectedLogin Protect(string user,SecureString password){if(string.IsNullOrWhiteSpace(user)||password.Length==0)throw new ArgumentException("Укажите учётную запись и пароль.");var p=Marshal.SecureStringToGlobalAllocUnicode(password);byte[] bytes=new byte[password.Length*2];try{Marshal.Copy(p,bytes,0,bytes.Length);return new(){User=user.Trim(),Secret=Convert.ToBase64String(ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser))};}finally{Array.Clear(bytes);Marshal.ZeroFreeGlobalAllocUnicode(p);}}
 public static void Save(string key,string user,SecureString password){if(password.Length*2>2560)throw new ArgumentException("Пароль превышает размер записи Windows.");_ = Protect(user,password);var p=Marshal.SecureStringToGlobalAllocUnicode(password);try{var c=new Credential{Type=1,TargetName=key,Size=(uint)password.Length*2,Blob=p,Persist=2,UserName=user.Trim()};if(!Write(ref c,0))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows не сохранила учётные данные");}finally{Marshal.ZeroFreeGlobalAllocUnicode(p);}}
 public static ProtectedLogin? Load(string key){if(!Read(key,1,0,out var p)){int error=Marshal.GetLastWin32Error();if(error==1168)return null;throw new Win32Exception(error,"Хранилище учётных данных Windows недоступно");}byte[]? bytes=null;try{var c=Marshal.PtrToStructure<Credential>(p);if(c.Size==0||c.Size>2560||c.Size%2!=0)throw new InvalidOperationException("Повреждена запись учётных данных Windows");bytes=new byte[c.Size];Marshal.Copy(c.Blob,bytes,0,bytes.Length);return new(){User=c.UserName,Secret=Convert.ToBase64String(ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser))};}finally{if(bytes!=null)Array.Clear(bytes);CredFree(p);}}
 public static void Forget(string key){if(!Delete(key,1,0)&&Marshal.GetLastWin32Error()!=1168)throw new Win32Exception(Marshal.GetLastWin32Error(),"Не удалось удалить запись Windows");}
}
public static partial class ConnectionStore {
 public static AuthenticationSettings Authentication=Store.Read("authentication-settings.json",new AuthenticationSettings());
 public const string SharedVaultKey="DomainConsole/Remote/Shared";
 static ProtectedLogin? sharedSession;
 public static string AuthMode(TargetRecord target){string mode=target.ConnectionSnapshot?target.ConnectionAuth:Profiles.GetValueOrDefault(target.Host)?.AuthMode??"";return mode.Length==0?Authentication.Mode:mode;}
 static string SpecificKey(string address,bool ssl)=>"DomainConsole/Remote/Endpoint/"+Normalize(address)+"/"+(ssl?"https":"http");
 public static ProtectedLogin? Login(TargetRecord target)=>AuthMode(target)=="Shared"?sharedSession??WindowsLoginVault.Load(SharedVaultKey):AuthMode(target)=="Specific"?credentials.TryGetValue(Key(Address(target),Ssl(target)),out var c)?new(){User=c.User,Secret=c.Secret}:WindowsLoginVault.Load(SpecificKey(Address(target),Ssl(target))):null;
 public static string AccountLabel(TargetRecord t)=>AuthMode(t)=="Current"?"Текущий вход Windows: "+Environment.UserDomainName+"\\"+Environment.UserName:Login(t)?.User??"Учётная запись не задана";
 public static bool CanConnect(TargetRecord target)=>AuthMode(target)=="Current"||Login(target)!=null;
 public static void SetShared(string user,SecureString password,bool remember){var login=WindowsLoginVault.Protect(user,password);if(remember)WindowsLoginVault.Save(SharedVaultKey,user,password);else WindowsLoginVault.Forget(SharedVaultKey);sharedSession=login;}
 public static void ForgetShared(){WindowsLoginVault.Forget(SharedVaultKey);sharedSession=null;}
 public static void SetSpecific(string address,bool ssl,string user,SecureString password,bool remember){SetCredential(address,ssl,user,password);if(remember)WindowsLoginVault.Save(SpecificKey(address,ssl),user,password);else WindowsLoginVault.Forget(SpecificKey(address,ssl));}
 public static void ForgetSpecific(string address,bool ssl){WindowsLoginVault.Forget(SpecificKey(address,ssl));credentials.Remove(Key(address,ssl));}
 public static void UseDefault(string mode){if(mode is not ("Current" or "Shared"))throw new ArgumentException("Неизвестный режим входа");Authentication.Mode=mode;Store.Write("authentication-settings.json",Authentication);}
 public static string LoginCode(ProtectedLogin c)=>"Add-Type -AssemblyName System.Security;$dcBytes=[Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String("+PowerShellBridge.Literal(c.Secret)+"),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);$dcSecure=New-Object Security.SecureString;try{for($i=0;$i -lt $dcBytes.Length;$i+=2){$dcSecure.AppendChar([char]([int]$dcBytes[$i]+256*[int]$dcBytes[$i+1]))}}finally{[Array]::Clear($dcBytes,0,$dcBytes.Length)};$dcSecure.MakeReadOnly();$dcCredential=New-Object System.Management.Automation.PSCredential("+PowerShellBridge.Literal(c.User)+",$dcSecure);";
 public static string AuthenticationAdvice(string message){if(message.Contains("8009030e",StringComparison.OrdinalIgnoreCase))return "Не удалось создать сеанс аутентификации (0x8009030e). Это не доказывает неверный пароль: проверьте доступ к домену и политики NTLM. Для прямого IP WinRM может требовать явные данные — выберите общую учётную запись. Сохранённые данные не удалены.";if(message.Contains("8007052e",StringComparison.OrdinalIgnoreCase)||message.Contains("1326"))return "Windows отклонила вход (0x8007052e/1326). Проверьте имя, пароль и состояние учётной записи; сохранённую запись можно изменить.";if(message.Contains("Access is denied",StringComparison.OrdinalIgnoreCase)||message.Contains("Отказано в доступе",StringComparison.OrdinalIgnoreCase))return "Отказано в доступе. Проверьте права на WinRM; при необходимости выберите другую учётную запись. Повторный ввод того же пароля может не помочь.";return "При сетевом таймауте учётные данные сохраняются. Проверьте VPN и доступность WinRM; пароль менять не требуется без ошибки входа.";}
}
public partial class MainWindow {
 void ConnectionCredentials_Click(object sender,RoutedEventArgs e)=>EditSharedCredentials(this);
 bool EditSharedCredentials(Window owner){var window=new Window{Owner=owner,Title="Учётные данные подключения",Width=560,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(18)};window.Content=panel;
  var mode=new ComboBox{ItemsSource=new[]{"Текущая учётная запись Windows","Общая сохранённая учётная запись"},SelectedIndex=ConnectionStore.Authentication.Mode=="Shared"?1:0};panel.Children.Add(mode);
  panel.Children.Add(new TextBlock{Text="По имени домена текущий вход обычно использует Kerberos. При прямом IP WinRM может потребовать явный пароль; задайте общую запись один раз для разных клиентов.",TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)});
  ProtectedLogin? existing=null;string loadError="";try{existing=ConnectionStore.Login(new(){ConnectionSnapshot=true,ConnectionAuth="Shared"});}catch(Exception ex){loadError=ex.Message;}
  var user=new TextBox{Text=existing?.User??Environment.UserDomainName+"\\"+Environment.UserName};panel.Children.Add(new TextBlock{Text="Общая учётная запись"});panel.Children.Add(user);panel.Children.Add(new TextBlock{Text="Новый пароль (пусто — использовать уже сохранённый)"});var password=new PasswordBox();panel.Children.Add(password);var remember=new CheckBox{Content="Запомнить в защищённом хранилище Windows",IsChecked=true};panel.Children.Add(remember);var status=new TextBlock{Text=loadError.Length>0?loadError:existing==null?"Общая запись ещё не задана.":"Запись уже доступна: "+existing.User,TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);
  var buttons=new WrapPanel();panel.Children.Add(buttons);var save=new Button{Content="Использовать",IsDefault=true};buttons.Children.Add(save);var forget=new Button{Content="Забыть общую запись"};buttons.Children.Add(forget);buttons.Children.Add(new Button{Content="Отмена",IsCancel=true});
  void UpdateInputs(){user.IsEnabled=mode.SelectedIndex==1;password.IsEnabled=mode.SelectedIndex==1;remember.IsEnabled=mode.SelectedIndex==1;}mode.SelectionChanged+=(s,a)=>UpdateInputs();UpdateInputs();
  save.Click+=(s,a)=>{try{if(mode.SelectedIndex==1){if(password.SecurePassword.Length>0)ConnectionStore.SetShared(user.Text,password.SecurePassword,remember.IsChecked==true);else if(existing==null||!user.Text.Trim().Equals(existing.User,StringComparison.OrdinalIgnoreCase))throw new Exception("Введите пароль для новой учётной записи.");}ConnectionStore.UseDefault(mode.SelectedIndex==1?"Shared":"Current");window.DialogResult=true;}catch(Exception ex){status.Text=ex.Message;}};
  forget.Click+=(s,a)=>{try{ConnectionStore.ForgetShared();ConnectionStore.UseDefault("Current");existing=null;password.Clear();mode.SelectedIndex=0;status.Text="Общая запись удалена. Используется текущий вход Windows.";}catch(Exception ex){status.Text=ex.Message;}};
  return window.ShowDialog()==true;
 }
}
