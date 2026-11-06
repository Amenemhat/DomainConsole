using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
namespace DomainConsole;
public partial class App {
 internal static readonly DataBackup57 Backups57=new(Store.Root,Path.Combine(AppContext.BaseDirectory,"Backups"),Store.DataLock57);
 internal static string RecoveryHoldFile57=>Path.Combine(Backups57.ArchiveDirectory,"recovery-hold.txt");
 internal static bool RecoveryPending57=>File.Exists(RecoveryHoldFile57);
 async Task<bool> PrepareData57() {
  var damaged=await Task.Run(Backups57.DamagedFiles);
  if(damaged.Length>0){
   var candidate=await Task.Run(()=>Backups57.LatestFor(damaged));
   var choice=MessageBox.Show("Повреждены сохранённые данные: "+string.Join(", ",damaged)+".\n\n"+(candidate==null?"Проверенная резервная копия не найдена. Выбрать ZIP вручную?":"Восстановить повреждённые файлы из копии от "+candidate.Info.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")+"?\nИзменения после этой копии в восстановленных файлах будут недоступны.")+"\n\nПовреждённые оригиналы сохранятся отдельно. Автоматические действия после восстановления будут приостановлены.","Восстановление данных",MessageBoxButton.YesNo,MessageBoxImage.Warning);
   if(choice!=MessageBoxResult.Yes)return false;
   if(candidate==null){var picker=new OpenFileDialog{Filter="Резервная копия DomainConsole (*.zip)|*.zip",Title="Выберите архив данных"};if(picker.ShowDialog()!=true)return false;candidate=await Task.Run(()=>DataBackup57.Read(picker.FileName));}
   // Persist the hold before restoring: interrupted recovery must not resume automation.
   DataBackup57.DurableReplace(RecoveryHoldFile57,Encoding.UTF8.GetBytes("Восстановление из "+candidate.Path));
   var original=await Task.Run(()=>Backups57.Restore(candidate,damaged));
   MessageBox.Show("Восстановлены: "+string.Join(", ",damaged)+".\nКопия от "+candidate.Info.CreatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")+".\nОригиналы и сведения о восстановлении: "+original+"\n\nАвтоматические действия приостановлены. Ручные команды доступны. Возобновление — в настройках резервного копирования.","Данные восстановлены",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  if(damaged.Length==0&&RecoveryPending57)MessageBox.Show("После восстановления данных автоматические действия остаются приостановленными. Ручные команды и чтение доступны. Проверьте клиентов и очередь; возобновление — в настройках резервного копирования.","Восстановленные данные",MessageBoxButton.OK,MessageBoxImage.Information);
  if(Directory.GetFiles(Store.Root,"*.json").Length>0)try{await Task.Run(()=>Backups57.Create(true));}catch(Exception ex){MessageBox.Show("Данные прочитаны, но резервная копия не создана: "+ex.Message,"Резервное копирование",MessageBoxButton.OK,MessageBoxImage.Warning);}
  return true;
 }
 internal static string LogStartup57(Exception ex){
  var folder=Path.Combine(AppContext.BaseDirectory,"Diagnostics");Directory.CreateDirectory(folder);
  var path=Path.Combine(folder,"startup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".txt");File.WriteAllText(path,ex.ToString());return path;
 }
}
public partial class MainWindow {
 async Task BackupOnClose57(){
  if(Environment.GetEnvironmentVariable("DOMAINCONSOLE_UI_SMOKE")=="1"||Environment.GetEnvironmentVariable("DOMAINCONSOLE_LOCAL_SMOKE")=="1")return;
  try{await Task.Run(()=>App.Backups57.Create(true));}
  catch(Exception ex){MessageBox.Show(this,"Последняя резервная копия не создана: "+ex.Message+"\nПредыдущие архивы сохранены.","Резервное копирование",MessageBoxButton.OK,MessageBoxImage.Warning);}
 }
 async void CreateBackup57_Click(object sender,RoutedEventArgs e){
  var button=sender as System.Windows.Controls.Button;if(button!=null)button.IsEnabled=false;
  try{var path=await Task.Run(()=>App.Backups57.Create());MessageBox.Show(this,"Архив создан и проверен:\n"+path,"Резервная копия",MessageBoxButton.OK,MessageBoxImage.Information);}
  catch(Exception ex){MessageBox.Show(this,"Архив не создан: "+ex.Message,"Резервная копия",MessageBoxButton.OK,MessageBoxImage.Error);}
  finally{if(button!=null)button.IsEnabled=true;}
 }
 void OpenBackups57_Click(object sender,RoutedEventArgs e){Directory.CreateDirectory(App.Backups57.ArchiveDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(App.Backups57.ArchiveDirectory){UseShellExecute=true});}
 void ResumeAfterRecovery57_Click(object sender,RoutedEventArgs e){
  if(!App.RecoveryPending57){MessageBox.Show(this,"Приостановки после восстановления нет.");return;}
  if(MessageBox.Show(this,"Перед возобновлением проверьте состояние клиентов и отмените ненужные задания из восстановленной очереди. Возобновить автоматические действия, включая отправку сохранённой очереди?","После восстановления",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
  File.Delete(App.RecoveryHoldFile57);Notify("Автоматические действия после восстановления разрешены.");
 }
}
