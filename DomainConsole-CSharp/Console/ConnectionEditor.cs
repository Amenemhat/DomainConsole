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
  var mode=new CheckBox{Content="Подключаться по указанному IP",IsChecked=row==null||old?.Address.Length>0,Margin=new(0,0,0,8)};panel.Children.Add(mode);
  panel.Children.Add(new TextBlock{Text="IP-адрес (IPv4 или IPv6)"});var address=new TextBox{Text=old?.Address??(IPAddress.TryParse(initial,out _)?initial:"")};panel.Children.Add(address);
  var ssl=new CheckBox{Content="WinRM через HTTPS (5986)",IsChecked=old?.Ssl??false,ToolTip="Нужен настроенный HTTPS listener и действительный сертификат для этого адреса."};panel.Children.Add(ssl);
  panel.Children.Add(new TextBlock{Text="Учётная запись для IP-подключения"});var user=new TextBox{Text=Environment.UserDomainName+"\\"+Environment.UserName};panel.Children.Add(user);panel.Children.Add(new TextBlock{Text="Пароль (только на текущий сеанс программы)"});var password=new PasswordBox{Margin=new(0,5,0,5)};panel.Children.Add(password);
  var explanation=new TextBlock{Text="Для HTTP адрес должен быть разрешён в TrustedHosts этого компьютера. Проверка соединения не запускает обслуживание. Сохранение IP не перенаправляет уже переданные задания.",TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};panel.Children.Add(explanation);
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};panel.Children.Add(status);ConnectionIdentity? verified=null;string verifiedAddress="";bool verifiedSsl=false;
  void Changed(){verified=null;status.Text="Параметры изменены; проверьте подключение.";}
  address.TextChanged+=(s,a)=>Changed();ssl.Checked+=(s,a)=>Changed();ssl.Unchecked+=(s,a)=>Changed();user.TextChanged+=(s,a)=>Changed();password.PasswordChanged+=(s,a)=>Changed();
  var actions=new WrapPanel();panel.Children.Add(actions);var test=new Button{Content="Проверить подключение"};actions.Children.Add(test);var trust=new Button{Content="Разрешить этот IP для HTTP…",ToolTip="Добавить только этот IP в TrustedHosts, сохранив остальные записи. Требуются права администратора."};actions.Children.Add(trust);
  trust.Click+=async(s,a)=>{try{var ip=ConnectionStore.Normalize(address.Text);if(MessageBox.Show(window,"Добавить "+ip+" в TrustedHosts на компьютере консоли? Настройка общая для всех пользователей. При HTTP подлинность сервера не подтверждается Kerberos или сертификатом. Остальные записи сохранятся.","Разрешение IP",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;await remote.TrustAddress(ip);status.Text="Адрес разрешён. Теперь проверьте подключение.";}catch(Exception ex){status.Text=ShortConnectionError(ex.Message);}};
  test.Click+=async(s,a)=>{test.IsEnabled=false;try{
   var ip=ConnectionStore.Normalize(address.Text);if(password.SecurePassword.Length>0){ConnectionStore.SetCredential(ip,ssl.IsChecked==true,user.Text.Trim(),password.SecurePassword);password.Clear();}
   if(!ConnectionStore.HasCredential(ip,ssl.IsChecked==true))throw new Exception("Введите учётную запись и пароль для текущего сеанса.");
   var target=new TargetRecord{Host=row?.Host??ip,ConnectionSnapshot=true,ConnectionAddress=ip,ConnectionSsl=ssl.IsChecked==true};var identity=await remote.ProbeEndpoint(target);
   if(identity.Identity.Length==0||identity.ComputerName.Length==0)throw new Exception("Клиент не вернул идентичность; привязка не сохранена.");
   if(row!=null&&!row.Host.Split('.')[0].Equals(identity.ComputerName,StringComparison.OrdinalIgnoreCase))throw new Exception("По адресу отвечает "+identity.ComputerName+", а выбран "+row.Name+". Для другого компьютера используйте добавление через плюс.");
   if(old?.Identity.Length>0&&old.Identity!=identity.Identity&&MessageBox.Show(window,"Имя совпало, но идентификатор системы изменился (возможна переустановка). Привязать этот адрес заново? Старые задания сохранят прежнюю идентичность.","Идентичность изменилась",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   verified=identity;verifiedAddress=ip;verifiedSsl=ssl.IsChecked==true;status.Text="Подключение успешно: "+identity.ComputerName+"\n"+identity.Fqdn+" · "+ip+"\nНажмите «Сохранить», чтобы использовать адрес.";
  }catch(Exception ex){verified=null;status.Text=ShortConnectionError(ex.Message);}finally{test.IsEnabled=true;}};
  var bottom=new WrapPanel{Margin=new(0,12,0,0)};panel.Children.Add(bottom);var save=new Button{Content="Сохранить",IsDefault=true};bottom.Children.Add(save);bottom.Children.Add(new Button{Content="Отмена",IsCancel=true});
  if(row!=null&&manualComputers.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase))){var remove=new Button{Content="Удалить ручную запись…"};bottom.Children.Add(remove);remove.Click+=(s,a)=>{if(History.Any(j=>j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&IsCurrentExecution(t.Status)))){status.Text="У компьютера есть активные задания. Сначала завершите или остановите их.";return;}if(MessageBox.Show(window,"Удалить ручную запись из списка? История и записи AD/WSUS сохранятся.","Удаление записи",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;manualComputers.RemoveAll(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));Store.Write("manual-computers.json",manualComputers);Computers.Remove(row);recentHosts.RemoveAll(h=>h.Equals(row.Host,StringComparison.OrdinalIgnoreCase));workHosts.RemoveAll(h=>h.Equals(row.Host,StringComparison.OrdinalIgnoreCase));Store.Write("recent-computers.json",recentHosts);Store.Write("work-computers.json",workHosts);Tree();RefreshRecent();window.DialogResult=true;};}
  save.Click+=(s,a)=>{try{
   if(mode.IsChecked!=true){if(row==null)throw new Exception("Для новой записи укажите IP и проверьте подключение.");ConnectionStore.Profiles.Remove(row.Host);ConnectionStore.Save();row.Target.ConnectionSnapshot=false;ShowCurrent();window.DialogResult=true;return;}
   var ip=ConnectionStore.Normalize(address.Text);if(verified==null||ip!=verifiedAddress||verifiedSsl!=(ssl.IsChecked==true))throw new Exception("Сначала проверьте подключение с этими параметрами.");
   var host=row?.Host??verified.Fqdn.TrimEnd('.');if(string.IsNullOrWhiteSpace(host))host=verified.ComputerName;var existing=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase));
   if(row==null&&existing!=null&&MessageBox.Show(window,"Этот компьютер уже есть в списке. Использовать IP для существующей записи "+existing.Name+"?", "Компьютер найден",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   ConnectionStore.Profiles[host]=new(){Address=ip,Ssl=verifiedSsl,Identity=verified.Identity,ComputerName=verified.ComputerName,Fqdn=verified.Fqdn};ConnectionStore.Save();
   if(row==null&&existing==null){var target=new TargetRecord{Name=verified.ComputerName,Host=host,OU="Ручные подключения"};manualComputers.Add(target);Store.Write("manual-computers.json",manualComputers);existing=WithHistory(target);Computers.Add(existing);}
   SearchBox.Text="";Tree();ComputerGrid.SelectedItem=row??existing;RememberComputer(host);ShowCurrent();window.DialogResult=true;
  }catch(Exception ex){status.Text=ex.Message;}};
  await Task.CompletedTask;window.ShowDialog();
 }
}
public partial class MainWindow {
 async Task<bool> EnsureEndpointCredentials(List<ComputerRow> rows){foreach(var row in rows){var t=ConnectionStore.Capture(row.Target);var address=ConnectionStore.Address(t);if(address.Length>0&&!ConnectionStore.HasCredential(address,ConnectionStore.Ssl(t))){await EditConnection(row,"");t=ConnectionStore.Capture(row.Target);address=ConnectionStore.Address(t);if(address.Length>0&&!ConnectionStore.HasCredential(address,ConnectionStore.Ssl(t)))return false;}}return true;}
}
