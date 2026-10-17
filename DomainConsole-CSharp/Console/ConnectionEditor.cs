using System.Net;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 readonly List<TargetRecord> manualComputers=Store.Read("manual-computers.json",new List<TargetRecord>());
 async void AddAddress_Click(object sender,RoutedEventArgs e)=>await EditConnection(null,SearchBox.Text);
 async void EditConnection_Click(object sender,RoutedEventArgs e)=>await EditConnection(ComputerGrid.SelectedItem as ComputerRow,"");
 async Task EditConnection(ComputerRow? row,string initial){
  var old=row==null?null:ConnectionStore.Profiles.GetValueOrDefault(row.Host);var window=new Window{Owner=this,Title=row==null?"Добавить компьютер по IP":"Адрес подключения · "+row.Name,Width=560,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,MaxHeight=820};
  var panel=new StackPanel{Margin=new(18)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  var mode=new CheckBox{Content="Подключаться по указанному IP",ToolTip="Если галочка снята, используется имя компьютера из AD. Ввод IP включает этот режим.",IsChecked=row==null||old?.Address.Length>0,Margin=new(0,0,0,8)};panel.Children.Add(mode);
  panel.Children.Add(new TextBlock{Text="IP-адрес (IPv4 или IPv6)"});var address=new TextBox{Text=old?.Address??(IPAddress.TryParse(initial,out _)?initial:"")};panel.Children.Add(address);
  var ssl=new CheckBox{Content="WinRM через HTTPS (5986)",IsChecked=old?.Ssl??false,ToolTip="Нужен настроенный HTTPS listener и действительный сертификат для этого адреса."};panel.Children.Add(ssl);
  panel.Children.Add(new TextBlock{Text="Использовать учётную запись"});var account=new ComboBox{ItemsSource=new[]{"Как в общих настройках","Текущий вход Windows","Общая сохранённая запись","Другая запись только для этого клиента"},SelectedIndex=old?.AuthMode switch {"Current"=>1,"Shared"=>2,"Specific"=>3,_=>0}};panel.Children.Add(account);
  var accountHint=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,6)};panel.Children.Add(accountHint);var common=new Button{Content="Общая учётная запись…",HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(common);
  var specific=new StackPanel();panel.Children.Add(specific);specific.Children.Add(new TextBlock{Text="Учётная запись этого клиента"});var user=new TextBox{Text=Environment.UserDomainName+"\\"+Environment.UserName};specific.Children.Add(user);var password=new PasswordBox();specific.Children.Add(new TextBlock{Text="Новый пароль (пусто — использовать сохранённый)"});specific.Children.Add(password);var remember=new CheckBox{Content="Запомнить средствами Windows",IsChecked=true};specific.Children.Add(remember);
  var forgetSpecific=new Button{Content="Забыть запись этого адреса"};specific.Children.Add(forgetSpecific);
  if(account.SelectedIndex==3&&address.Text.Length>0){try{var savedLogin=ConnectionStore.Login(new(){ConnectionSnapshot=true,ConnectionAddress=ConnectionStore.Normalize(address.Text),ConnectionSsl=ssl.IsChecked==true,ConnectionAuth="Specific"});if(savedLogin!=null)user.Text=savedLogin.User;}catch{}}
  var explanation=new TextBlock{Text="Кнопка подключения сама проверит WinRM и разрешение IP. Если HTTP требует TrustedHosts, программа один раз предложит добавить только этот IP. При текущем входе Windows прямое IP-подключение может требовать явный пароль — общую запись можно задать один раз для всех клиентов.",TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};panel.Children.Add(explanation);
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};panel.Children.Add(status);ConnectionIdentity? verified=null;string verifiedAddress="",verifiedAuth="";bool verifiedSsl=false;
  string AccountMode()=>account.SelectedIndex switch {1=>"Current",2=>"Shared",3=>"Specific",_=>""};
  TargetRecord Candidate(string ip)=>new(){Host=row?.Host??ip,ConnectionSnapshot=true,ConnectionAddress=ip,ConnectionSsl=ssl.IsChecked==true,ConnectionAuth=AccountMode()};
  void UpdateAccount(){specific.Visibility=account.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;try{var t=Candidate(address.Text);accountHint.Text="Сейчас: "+ConnectionStore.AccountLabel(t);}catch(Exception ex){accountHint.Text=ex.Message;}}
  void Changed(){verified=null;status.Text="Параметры изменены; подключение будет проверено перед сохранением.";UpdateAccount();}
  address.TextChanged+=(s,a)=>{if(address.Text.Trim().Length>0)mode.IsChecked=true;Changed();};ssl.Checked+=(s,a)=>Changed();ssl.Unchecked+=(s,a)=>Changed();account.SelectionChanged+=(s,a)=>Changed();user.TextChanged+=(s,a)=>Changed();password.PasswordChanged+=(s,a)=>Changed();
  common.Click+=(s,a)=>{if(EditSharedCredentials(window)){account.SelectedIndex=0;Changed();}};forgetSpecific.Click+=(s,a)=>{try{ConnectionStore.ForgetSpecific(ConnectionStore.Normalize(address.Text),ssl.IsChecked==true);Changed();}catch(Exception ex){status.Text=ex.Message;}};
  bool verifying=false;
  async Task<bool> VerifyConnection(){if(verifying)return false;verifying=true;var inputs=new UIElement[]{mode,address,ssl,account,common,specific};foreach(var input in inputs)input.IsEnabled=false;try{
   var ip=ConnectionStore.Normalize(address.Text);var target=Candidate(ip);
   if(account.SelectedIndex==3&&password.SecurePassword.Length>0){ConnectionStore.SetSpecific(ip,ssl.IsChecked==true,user.Text.Trim(),password.SecurePassword,remember.IsChecked==true);password.Clear();}
   if(!ConnectionStore.CanConnect(target))throw new Exception("Учётная запись не задана. Нажмите «Общая учётная запись…» и введите её один раз для разных клиентов.");
   if(account.SelectedIndex==3&&!user.Text.Trim().Equals(ConnectionStore.Login(target)?.User,StringComparison.OrdinalIgnoreCase))throw new Exception("Для другой учётной записи этого клиента введите её пароль.");
   status.Text="Проверка параметров WinRM…";
   if(ssl.IsChecked!=true&&!await remote.IsTrustedAddress(ip)){
    if(MessageBox.Show(window,"Для подключения по IP/HTTP нужно разрешить "+ip+" в TrustedHosts на этом компьютере. Изменится общая настройка Windows; остальные адреса сохранятся. Добавить этот адрес и продолжить?","Подключение по IP",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){status.Text="Подключение не выполнялось: адрес не разрешён. Можно использовать настроенный HTTPS либо подключение по доменному имени.";return false;}
    await remote.TrustAddress(ip);
   }
   var identity=await remote.ProbeEndpoint(target,text=>status.Text=text);
   if(identity.Identity.Length==0||identity.ComputerName.Length==0)throw new Exception("Клиент не вернул идентичность; привязка не сохранена.");
   if(row!=null&&!row.Host.Split('.')[0].Equals(identity.ComputerName,StringComparison.OrdinalIgnoreCase))throw new Exception("По адресу отвечает "+identity.ComputerName+", а выбран "+row.Name+". Для другого компьютера используйте добавление через плюс.");
   if(old?.Identity.Length>0&&old.Identity!=identity.Identity&&MessageBox.Show(window,"Имя совпало, но идентификатор системы изменился (возможна переустановка). Привязать этот адрес заново? Старые задания сохранят прежнюю идентичность.","Идентичность изменилась",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return false;
   verified=identity;verifiedAddress=ip;verifiedSsl=ssl.IsChecked==true;verifiedAuth=AccountMode();status.Text+="\nКомпьютер подтверждён: "+identity.ComputerName+" · "+identity.Fqdn;return true;
  }catch(Exception ex){verified=null;status.Text+="\n"+ShortConnectionError(ex.Message)+"\n"+ConnectionStore.AuthenticationAdvice(ex.Message);return false;}finally{verifying=false;foreach(var input in inputs)input.IsEnabled=true;}}
  var test=new Button{Content="Проверить без сохранения",HorizontalAlignment=HorizontalAlignment.Left};var advanced=new Expander{Header="Дополнительная проверка",Content=test};panel.Children.Add(advanced);test.Click+=async(s,a)=>{test.IsEnabled=false;try{await VerifyConnection();}finally{test.IsEnabled=true;}};
  UpdateAccount();
  var moveJobs=new CheckBox{Content="Обновить адрес существующих заданий этого же компьютера",IsChecked=false,Visibility=row==null?Visibility.Collapsed:Visibility.Visible,ToolTip="Только задания с тем же подтверждённым идентификатором. Выполнение не перезапускается; для незавершённых заданий меняется адрес чтения и управления."};panel.Children.Add(moveJobs);
  var bottom=new WrapPanel{Margin=new(0,12,0,0)};panel.Children.Add(bottom);var save=new Button{Content="Подключить и сохранить",IsDefault=true};bottom.Children.Add(save);bottom.Children.Add(new Button{Content="Отмена",IsCancel=true});
  if(row!=null&&manualComputers.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase))){var remove=new Button{Content="Удалить ручную запись…"};bottom.Children.Add(remove);remove.Click+=(s,a)=>{if(History.Any(j=>j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&IsCurrentExecution(t.Status)))){status.Text="У компьютера есть активные задания. Сначала завершите или остановите их.";return;}if(MessageBox.Show(window,"Удалить ручную запись из списка? История и записи AD/WSUS сохранятся.","Удаление записи",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;manualComputers.RemoveAll(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));Store.Write("manual-computers.json",manualComputers);Computers.Remove(row);recentHosts.RemoveAll(h=>h.Equals(row.Host,StringComparison.OrdinalIgnoreCase));workHosts.RemoveAll(h=>h.Equals(row.Host,StringComparison.OrdinalIgnoreCase));Store.Write("recent-computers.json",recentHosts);Store.Write("work-computers.json",workHosts);Tree();RefreshRecent();window.DialogResult=true;};}
  void SaveVerified(){try{
   if(mode.IsChecked!=true){if(row==null)throw new Exception("Для новой записи укажите IP и проверьте подключение.");ConnectionStore.Profiles.Remove(row.Host);ConnectionStore.Save();row.Target.ConnectionSnapshot=false;ShowCurrent();window.DialogResult=true;return;}
   var ip=ConnectionStore.Normalize(address.Text);if(verified==null||ip!=verifiedAddress||verifiedSsl!=(ssl.IsChecked==true)||verifiedAuth!=AccountMode())throw new Exception("Сначала проверьте подключение с этими параметрами.");
   var host=row?.Host??verified.Fqdn.TrimEnd('.');if(string.IsNullOrWhiteSpace(host))host=verified.ComputerName;var existing=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase));
   if(row==null&&existing!=null&&MessageBox.Show(window,"Этот компьютер уже есть в списке. Использовать IP для существующей записи "+existing.Name+"?", "Компьютер найден",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   if(ConnectionStore.Profiles.TryGetValue(host,out var previous)&&previous.Identity!=verified.Identity){rebootSnapshots.Remove(host);clientConnections.Remove(host);liveConnections.Remove(host);liveReports.Remove(host);Store.Write("reboot-snapshots.json",rebootSnapshots);Store.Write("client-connections.json",clientConnections);}
   ConnectionStore.Profiles[host]=new(){AuthMode=verifiedAuth,Address=ip,Ssl=verifiedSsl,Identity=verified.Identity,ComputerName=verified.ComputerName,Fqdn=verified.Fqdn};ConnectionStore.Save();
   if(moveJobs.IsChecked==true&&row!=null){int moved=0;foreach(var target in History.SelectMany(j=>j.Targets).Where(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&t.ConnectionIdentity==verified.Identity)){target.ConnectionAuth=verifiedAuth;target.ConnectionAddress=ip;target.ConnectionSsl=verifiedSsl;target.ConnectionName=verified.ComputerName;target.ConnectionSnapshot=true;moved++;}Save();Notify("Адрес обновлён для заданий с подтверждённой идентичностью: "+moved+". Задания не перезапускались.");}
   if(row==null&&existing==null){var target=new TargetRecord{Name=verified.ComputerName,Host=host,OU="Ручные подключения"};manualComputers.Add(target);Store.Write("manual-computers.json",manualComputers);existing=WithHistory(target);Computers.Add(existing);}
   SearchBox.Text="";Tree();ComputerGrid.SelectedItem=row??existing;RememberComputer(host);ShowCurrent();window.DialogResult=true;
  }catch(Exception ex){status.Text=ex.Message;}}
  bool connecting=false;save.Click+=async(s,a)=>{if(connecting)return;connecting=true;save.IsEnabled=false;test.IsEnabled=false;try{if(mode.IsChecked!=true||await VerifyConnection())SaveVerified();}finally{connecting=false;save.IsEnabled=true;test.IsEnabled=true;}};
  await Task.CompletedTask;window.ShowDialog();
 }
}
public partial class MainWindow {
 async Task<bool> EnsureEndpointCredentials(List<ComputerRow> rows){foreach(var row in rows){var t=ConnectionStore.Capture(row.Target);var address=ConnectionStore.Address(t);if(address.Length>0&&!ConnectionStore.CanConnect(t)){await EditConnection(row,"");t=ConnectionStore.Capture(row.Target);address=ConnectionStore.Address(t);if(address.Length>0&&!ConnectionStore.CanConnect(t))return false;}}return true;}
}
