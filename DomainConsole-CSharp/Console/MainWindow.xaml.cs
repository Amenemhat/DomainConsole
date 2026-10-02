using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow:Window {
 public ObservableCollection<ComputerRow> Computers {get;}=new();
 public ObservableCollection<CommandStep> Steps {get;}=new();
 public ObservableCollection<LibraryEntry> Library {get;}=new();
 public ObservableCollection<RemoteJob> History {get;}=new();
 readonly RemoteService remote=new();readonly HashSet<string> polling=new();
 readonly Queue<(RemoteJob Job,TargetRecord Target)> queue=new();
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(10)};
 bool sending,closing;int limit;string ou="";
 public MainWindow(){InitializeComponent();DataContext=this;
  foreach(var e in Store.Read("library.json",new List<LibraryEntry>()))Library.Add(e);
  foreach(var e in Store.Read("history.json",new List<RemoteJob>()))History.Add(e);
  if(Library.Count==0)Seed();
  CollectionViewSource.GetDefaultView(Computers).Filter=o=>{var r=(ComputerRow)o;return (r.Name+" "+r.OS).Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase)&&(ou==""||r.OU.Equals(ou,StringComparison.OrdinalIgnoreCase)||r.OU.EndsWith(","+ou,StringComparison.OrdinalIgnoreCase));};
  CollectionViewSource.GetDefaultView(Library).Filter=o=>{var e=(LibraryEntry)o;return(e.Name+" "+e.Category+" "+e.Description).Contains(LibrarySearch.Text,StringComparison.OrdinalIgnoreCase);};
  if(Environment.GetEnvironmentVariable("DOMAINCONSOLE_UI_SMOKE")=="1"){for(int i=0;i<50;i++)Computers.Add(new(new TargetRecord{Name="TEST-"+i,Host="test.invalid",OS="Windows test",Status="Completed",State=new(){Status="Completed",Progress=i*2}}));Tree();}
  timer.Tick+=async(s,e)=>await Guard(async()=>{await Task.WhenAll(Computers.Where(r=>r.JobId!=""&&(r.Target.Status is "Submitted" or "Running" or "AwaitingReboot" or "RebootScheduled")).Select(Poll));await SendQueue();});timer.Start();Closing+=OnClosing;
 }
 void Notify(string text)=>StatusBar.Text=text;
 void CopyOutput_Click(object s,RoutedEventArgs e)=>Guard(()=>{Clipboard.SetText(OutputBox.Text);Notify("Результат скопирован.");});
 void CopyError_Click(object s,RoutedEventArgs e)=>Guard(()=>Clipboard.SetText(StatusBar.Text));
 void SaveOutput_Click(object s,RoutedEventArgs e)=>Guard(()=>{var d=new SaveFileDialog{Filter="Текст|*.txt",FileName="result.txt"};if(d.ShowDialog(this)==true)File.WriteAllText(d.FileName,OutputBox.Text,new UTF8Encoding(true));});
 async void CheckConnection_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{var r=Current();MainTabs.SelectedIndex=2;OutputBox.Text="Проверка подключения к "+r.Host+"…";try{OutputBox.Text=await remote.CheckConnection(r.Target);}catch(Exception ex){OutputBox.Text="Подключение к "+r.Host+" не выполнено.\n"+ex.Message+"\n\nУчётная запись приложения: "+Environment.UserDomainName+"\\"+Environment.UserName+"\nПроверьте эту учётную запись и разрешения WinRM на целевом компьютере. Команда не запускалась.";Notify("Ошибка подключения. Подробности во вкладке «Вывод и результат».");}});
 async void RunEditor_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{if(string.IsNullOrWhiteSpace(Editor.Text))throw new Exception("Введите команду.");await Submit(new(){FromEditor()});});
 async Task Guard(Func<Task> work){try{await work();}catch(Exception ex){Notify(ex.Message);MessageBox.Show(this,ex.Message,"Domain Console",MessageBoxButton.OK,MessageBoxImage.Error);}}
 void Guard(Action work){try{work();}catch(Exception ex){Notify(ex.Message);MessageBox.Show(this,ex.Message,"Domain Console",MessageBoxButton.OK,MessageBoxImage.Error);}}
 void Save(){LibraryGrid.CommitEdit();Store.Write("library.json",Library.ToList());Store.Write("history.json",History.ToList());}
 ComputerRow Current()=>ComputerGrid.SelectedItem as ComputerRow??throw new Exception("Выберите компьютер.");
 List<ComputerRow> Selected(){ComputerGrid.CommitEdit();return Computers.Where(c=>c.Selected).ToList();}
 void Seed(){
  foreach(var e in new[]{("DISM CheckHealth","CMD","DISM /Online /Cleanup-Image /CheckHealth",false),("DISM ScanHealth","CMD","DISM /Online /Cleanup-Image /ScanHealth",false),("DISM RestoreHealth","CMD","DISM /Online /Cleanup-Image /RestoreHealth",true),("SFC","CMD","sfc /scannow",false),("Обновления WSUS","Updates","",false),("WindowsUpdate.log","Diagnostics","",false),("Восстановление из WIM","CMD","DISM /Online /Cleanup-Image /RestoreHealth /Source:wim:{{ImagePath}}:{{ImageIndex}} /LimitAccess",false)})Library.Add(new(){Name=e.Item1,Category="Встроенные",MicrosoftSource=e.Item4,Steps=new(){new(){Name=e.Item1,Kind=e.Item2,Code=e.Item3}}});
  Library.Add(new(){Name="DISM → SFC",Category="Встроенные",MicrosoftSource=true,Steps=new(){new(){Name="Восстановление",Code="DISM /Online /Cleanup-Image /RestoreHealth"},new(){Name="SFC",Code="sfc /scannow"}}});Save();
 }
 async void LoadDomain_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{Notify("Загрузка AD…");var list=await Task.Run(DirectoryReader.Read);Computers.Clear();foreach(var t in list)Computers.Add(new(t));Tree();Notify("Компьютеров: "+list.Count);});
 void Tree(){OuTree.Items.Clear();var root=new TreeViewItem{Header="Весь домен",Tag="",IsExpanded=true};OuTree.Items.Add(root);var nodes=new Dictionary<string,TreeViewItem>(StringComparer.OrdinalIgnoreCase);
  foreach(var dn in Computers.Select(r=>r.OU).Distinct().Order()){var parts=Regex.Split(dn,@"(?<!\\),");var parent=root;for(int i=parts.Length-1;i>=0;i--){if(!parts[i].StartsWith("OU=")&&!parts[i].StartsWith("CN="))continue;var key=string.Join(",",parts.Skip(i));if(!nodes.TryGetValue(key,out var node)){node=new(){Header=parts[i][3..],Tag=key};nodes[key]=node;parent.Items.Add(node);}parent=node;}}
 }
 void OuTree_Changed(object s,RoutedPropertyChangedEventArgs<object> e){ou=(e.NewValue as TreeViewItem)?.Tag as string??"";CollectionViewSource.GetDefaultView(Computers).Refresh();}
 void Search_Changed(object s,TextChangedEventArgs e){if(ComputerGrid!=null)CollectionViewSource.GetDefaultView(Computers).Refresh();}
 void LibrarySearch_Changed(object s,TextChangedEventArgs e){if(LibraryGrid!=null)CollectionViewSource.GetDefaultView(Library).Refresh();}
 void SelectVisible_Click(object s,RoutedEventArgs e){foreach(ComputerRow r in CollectionViewSource.GetDefaultView(Computers))r.Selected=true;SelectedLabel.Text="Выбрано: "+Selected().Count;}
 void ClearSelection_Click(object s,RoutedEventArgs e){foreach(var r in Computers)r.Selected=false;SelectedLabel.Text="Выбор снят";}
 CommandStep FromEditor(string? code=null)=>new(){Name=CommandName.Text,Kind=((ComboBoxItem)KindBox.SelectedItem).Content.ToString()!,Code=code??Editor.Text};
 void AddStep_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(string.IsNullOrWhiteSpace(Editor.Text))throw new Exception("Введите команду.");Steps.Add(FromEditor());});
 void AddSelection_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(string.IsNullOrWhiteSpace(Editor.SelectedText))throw new Exception("Выделите фрагмент.");Steps.Add(FromEditor(Editor.SelectedText));});
 void EditStep_Click(object s,RoutedEventArgs e)=>Guard(()=>{int i=StepList.SelectedIndex;if(i<0)throw new Exception("Выберите шаг.");if(Steps[i].Kind is "Updates" or "Diagnostics")throw new Exception("Встроенный шаг не является текстовой командой.");Steps[i]=FromEditor();});
 void Step_Changed(object s,SelectionChangedEventArgs e){if(StepList.SelectedItem is CommandStep t){CommandName.Text=t.Name;Editor.Text=t.Code;KindBox.SelectedIndex=t.Kind=="PowerShell"?1:0;}}
 void RemoveStep_Click(object s,RoutedEventArgs e){if(StepList.SelectedItem is CommandStep t)Steps.Remove(t);}
 void Up_Click(object s,RoutedEventArgs e){int i=StepList.SelectedIndex;if(i>0){Steps.Move(i,i-1);StepList.SelectedIndex=i-1;}}
 void Down_Click(object s,RoutedEventArgs e){int i=StepList.SelectedIndex;if(i>=0&&i<Steps.Count-1){Steps.Move(i,i+1);StepList.SelectedIndex=i+1;}}
 string? Prompt(string title,string initial=""){var text=new TextBox{Text=initial,MinWidth=380,Margin=new(16)};var button=new Button{Content="Применить",Margin=new(16),IsDefault=true};var panel=new StackPanel();panel.Children.Add(text);panel.Children.Add(button);var w=new Window{Owner=this,Title=title,Content=panel,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};button.Click+=(s,e)=>w.DialogResult=true;return w.ShowDialog()==true?text.Text:null;}
 void ImportText_Click(object s,RoutedEventArgs e)=>Guard(()=>{var d=new OpenFileDialog{Filter="Команды|*.txt;*.cmd;*.bat;*.ps1|Все файлы|*.*"};if(d.ShowDialog(this)!=true)return;Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);using var reader=new StreamReader(d.FileName,Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage),true);Editor.Text=reader.ReadToEnd();CommandName.Text=Path.GetFileNameWithoutExtension(d.FileName);KindBox.SelectedIndex=Path.GetExtension(d.FileName)==".ps1"?1:0;Notify("Выделите команды и нажмите «Добавить выделенное».");});
 void SaveLibrary_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(Steps.Count==0)throw new Exception("Последовательность пуста.");var name=Prompt("Название",CommandName.Text);if(string.IsNullOrWhiteSpace(name))return;Library.Add(new(){Name=name,Category=Prompt("Категория","Мои команды")??"Мои команды",Description=Prompt("Описание")??"",MicrosoftSource=MicrosoftBox.IsChecked==true,Steps=Store.Clone(Steps.ToList())});Save();});
 void LoadLibrary_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(LibraryGrid.SelectedItem is not LibraryEntry t)throw new Exception("Выберите запись.");foreach(var step in Store.Clone(t.Steps))Steps.Add(step);MicrosoftBox.IsChecked=t.MicrosoftSource;});
 void DeleteLibrary_Click(object s,RoutedEventArgs e){if(LibraryGrid.SelectedItem is LibraryEntry t){Library.Remove(t);Save();}}
 void ImportLibrary_Click(object s,RoutedEventArgs e)=>Guard(()=>{var d=new OpenFileDialog{Filter="JSON|*.json"};if(d.ShowDialog(this)!=true)return;var list=JsonSerializer.Deserialize<List<LibraryEntry>>(File.ReadAllText(d.FileName),Store.Options)??throw new Exception("Пустая библиотека.");if(list.Any(t=>t.Steps.Count==0||t.Steps.Any(x=>x.Kind is not ("CMD" or "PowerShell" or "Updates" or "Diagnostics"))))throw new Exception("Неверный формат библиотеки.");foreach(var t in list)Library.Add(t);Save();});
 void ExportLibrary_Click(object s,RoutedEventArgs e)=>Guard(()=>{var d=new SaveFileDialog{Filter="JSON|*.json",FileName="commands.json"};if(d.ShowDialog(this)==true)File.WriteAllText(d.FileName,JsonSerializer.Serialize(Library,Store.Options));});
 async void Run_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{if(Steps.Count==0)throw new Exception("Последовательность пуста. Добавьте шаг или нажмите «Выполнить команду из редактора».");await Submit(Steps.ToList());});
 async Task Submit(List<CommandStep> source){var selected=Selected();if(selected.Count==0||source.Count==0)throw new Exception("Выберите цели и добавьте команды.");if(!int.TryParse(LimitBox.Text,out limit)||limit<0)throw new Exception("Лимит: целое число от 0.");if(MicrosoftBox.IsChecked==true&&source.Any(x=>x.Kind=="Updates"))throw new Exception("Разделите восстановление Microsoft и обновления WSUS.");var steps=Store.Clone(source);
  foreach(var name in Regex.Matches(string.Join("\n",steps.Select(x=>x.Code)),@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}").Select(m=>m.Groups[1].Value).Distinct()){var value=Prompt("Параметр "+name);if(value==null)return;foreach(var t in steps)t.Code=t.Code.Replace("{{"+name+"}}",value);}
  var preview="Цели: "+string.Join(", ",selected.Select(r=>r.Name))+"\nКонтекст: SYSTEM\nMicrosoft: "+MicrosoftBox.IsChecked+"\nАвтоперезагрузка: "+RebootBox.IsChecked+"\n\n"+string.Join("\n\n",steps.Select(t=>t.Name+" ["+t.Kind+"]\n"+t.Code));if(MessageBox.Show(this,preview,"Проверка задания",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
  var job=new RemoteJob{Name=steps[0].Name,Steps=steps,MicrosoftSource=MicrosoftBox.IsChecked==true,AutoReboot=RebootBox.IsChecked==true,ContinueOnError=ContinueBox.IsChecked==true};foreach(var r in selected){var t=Store.Clone(r.Target);t.Status="Queued";t.State=null;t.Output="";job.Targets.Add(t);r.JobId=job.Id;queue.Enqueue((job,t));}History.Insert(0,job);Save();Sync(job);ComputerGrid.SelectedItem=selected[0];OutputBox.Text="Задание поставлено в очередь. Ожидание передачи…";MainTabs.SelectedIndex=2;await SendQueue();
 }
 void Sync(RemoteJob job){foreach(var row in Computers.Where(r=>r.JobId==job.Id)){var t=job.Targets.FirstOrDefault(t=>t.Host==row.Host);if(t==null)continue;row.Target.Status=t.Status;row.Target.State=t.State;row.Target.Output=t.Output;row.Refresh();}}
 async Task SendQueue(){if(sending||closing)return;sending=true;try{while(queue.Count>0&&!closing){int active=History.SelectMany(j=>j.Targets).Count(t=>t.Status is "Sending" or "Submitted" or "Running");if(limit>0&&active>=limit)break;var next=queue.Dequeue();next.Target.Status="Sending";Sync(next.Job);Save();_=SendOne(next.Job,next.Target);await Task.Delay(80);}}finally{sending=false;}}
 async Task SendOne(RemoteJob job,TargetRecord target){try{await remote.Deploy(target,job);target.Status="Submitted";target.Output="Задание передано на "+target.Host+". Ожидание результата…";}catch(Exception ex){target.Status=ex.Message.Contains("Отказано в доступе",StringComparison.OrdinalIgnoreCase)||ex.Message.Contains("Access is denied",StringComparison.OrdinalIgnoreCase)?"AccessDenied":"ConnectionFailed";target.Output="Не удалось передать задание на "+target.Host+". Выполнение команды не подтверждено.\n\n"+ex.Message+"\n\nУчётная запись приложения: "+Environment.UserDomainName+"\\"+Environment.UserName+"\nИспользуйте «Проверить подключение» для отдельной проверки WinRM.";Notify(target.Host+": ошибка подключения. Подробности в результате компьютера.");}finally{Sync(job);if(ComputerGrid.SelectedItem is ComputerRow r&&r.JobId==job.Id&&r.Host==target.Host)OutputBox.Text=target.Output;Save();await SendQueue();}}
 async Task Poll(ComputerRow row){
  var id=row.JobId;var host=row.Host;var key=id+host;if(id==""||!polling.Add(key))return;
  try{var data=await remote.Poll(row.Target,id);if(data.State!=null){
   var output="Компьютер: "+host+"\nСостояние: "+ComputerRow.StatusText(data.State.Status)+"\nЭтап: "+data.State.Stage+"\nШаг: "+data.State.Step+" / "+data.State.Total+"\nОбновлено: "+data.State.Updated+"\nОшибка: "+data.State.Error+"\n\n"+string.Join("\n",data.State.Results.Select(t=>t.Name+": код завершения "+t.ExitCode+(t.Error==""?"":" — "+t.Error)))+"\n\n"+data.Output;
   foreach(var job in History.Where(j=>j.Id==id)){var t=job.Targets.FirstOrDefault(t=>t.Host==host);if(t!=null){t.Status=data.State.Status;t.State=data.State;t.Output=output;}Sync(job);}
   if(row.JobId==id){row.Target.State=data.State;row.Target.Status=data.State.Status;row.Target.Output=output;row.Refresh();if(ComputerGrid.SelectedItem==row)OutputBox.Text=output;}Save();
  }}catch(Exception ex){Notify(row.Name+": нет связи; последнее состояние сохранено. "+ex.Message);}finally{polling.Remove(key);}
 }
 async void Refresh_Click(object s,RoutedEventArgs e)=>await Guard(async()=>await Task.WhenAll(Computers.Where(r=>r.JobId!="").Select(Poll)));
 async void Computer_Changed(object s,SelectionChangedEventArgs e){if(ComputerGrid.SelectedItem is ComputerRow r){OutputBox.Text=r.Target.Output;await Guard(async()=>await Poll(r));}}
 async void Cancel_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{var r=Current();var items=queue.ToList();var match=items.FirstOrDefault(q=>q.Job.Id==r.JobId&&q.Target.Host==r.Host);if(match.Job!=null){queue.Clear();foreach(var q in items.Where(q=>q!=match))queue.Enqueue(q);match.Target.Status="Cancelled";Sync(match.Job);Save();}else Notify(await remote.Cancel(r.Target,r.JobId));});
 async void AbortReboot_Click(object s,RoutedEventArgs e)=>await Guard(async()=>Notify(await remote.AbortReboot(Current().Target)));
 async void Logs_Click(object s,RoutedEventArgs e)=>await Guard(async()=>Notify(await remote.Logs(Current().Target,Current().JobId)));
 async void Diagnostics_Click(object s,RoutedEventArgs e)=>await Guard(async()=>await Submit(new(){new(){Name="WindowsUpdate.log",Kind="Diagnostics"}}));
 void OpenHistory_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(HistoryGrid.SelectedItem is not RemoteJob j)throw new Exception("Выберите задание.");Computers.Clear();ou="";SearchBox.Text="";foreach(var t in j.Targets)Computers.Add(new(t,j.Id));Tree();});
 async void Resume_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{if(HistoryGrid.SelectedItem is not RemoteJob j)throw new Exception("Выберите задание.");foreach(var t in j.Targets.Where(t=>t.Status=="Queued")){if(!queue.Any(q=>q.Job.Id==j.Id&&q.Target.Host==t.Host))queue.Enqueue((j,t));}await SendQueue();});
 async void Retry_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{if(HistoryGrid.SelectedItem is not RemoteJob j)throw new Exception("Выберите задание.");MicrosoftBox.IsChecked=j.MicrosoftSource;RebootBox.IsChecked=j.AutoReboot;ContinueBox.IsChecked=j.ContinueOnError;await Submit(j.Steps);});
 void ExportCsv_Click(object s,RoutedEventArgs e)=>Guard(()=>{if(HistoryGrid.SelectedItem is not RemoteJob j)throw new Exception("Выберите задание.");var d=new SaveFileDialog{Filter="CSV|*.csv",FileName="results.csv"};if(d.ShowDialog(this)!=true)return;string Cell(string v)=>"\""+v.Replace("\"","\"\"")+"\"";File.WriteAllText(d.FileName,"Computer;Status;Updated;Error\r\n"+string.Join("\r\n",j.Targets.Select(t=>string.Join(";",new[]{Cell(t.Host),Cell(t.Status),Cell(t.State?.Updated??""),Cell(t.State?.Error??"")}))),new UTF8Encoding(true));});
 async void Wsus_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{if(!int.TryParse(WsusPort.Text,out int p)||p<1||p>65535||string.IsNullOrWhiteSpace(WsusHost.Text))throw new Exception("Укажите WSUS и порт.");WsusOutput.Text=await remote.Wsus(WsusHost.Text,p,WsusSsl.IsChecked==true,Current().Host);});
 void OnClosing(object? s,CancelEventArgs e){if(queue.Count>0||History.SelectMany(j=>j.Targets).Any(t=>t.Status=="Sending")){if(MessageBox.Show(this,"Переданные задания продолжат работу. Закрыть командный пункт?","Закрытие",MessageBoxButton.YesNo)!=MessageBoxResult.Yes){e.Cancel=true;return;}}closing=true;timer.Stop();Save();}
}
