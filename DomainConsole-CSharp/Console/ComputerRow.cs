using System.ComponentModel;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ComputerRow : INotifyPropertyChanged {
 public TargetRecord Target {get;} public string JobId {get;set;}="";
 public System.Windows.Media.Brush NameBrush=>Target.AdDisabled?Theme.Brush("TextMuted"):Theme.Brush("TextPrimary");public string Name=>Target.Name;public string Host=>Target.Host;public string OS=>Target.OS;public string OU=>Target.OU;
 bool selected;public bool Selected {get=>selected;set{selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
 public static string StatusText(string status)=>status switch {"Queued"=>"В очереди","Sending"=>"Передача задания","SubmissionUnknown"=>"Передача не подтверждена","Submitted"=>"Задание передано","Running"=>"Выполняется","Completed"=>"Завершено","CompletedWithErrors"=>"Завершено с ошибками","RebootObserved"=>"Перезагрузка подтверждена; флаги не уточнены","VerifiedAfterReboot"=>"Проверено после перезагрузки","InterventionRequired"=>"Нужно вмешательство","Succeeded"=>"Успешно","Failed"=>"Ошибка выполнения","AccessDenied"=>"Отказано в доступе","ConnectionFailed"=>"Ошибка подключения","Unavailable"=>"Ошибка передачи","Cancelled"=>"Отменено","AwaitingReboot"=>"Нужна перезагрузка","RebootScheduled"=>"Перезагрузка назначена",_=>status};
 public string Status=>StatusText(Target.Status);public string Stage=>Target.State?.Stage??"";public int Progress=>Target.Status is "Completed" or "VerifiedAfterReboot" or "AwaitingReboot" or "RebootScheduled"?100:Target.State?.Progress??0;
 public bool Busy=>(Target.Status is "Sending" or "Submitted" or "Running") && Target.State?.Progress==null;
 public ComputerRow(TargetRecord target,string id=""){Target=target;JobId=id;}
 public void Refresh(){foreach(var name in new[]{"Status","Stage","Progress","Busy","Outcome","OutcomeIcon","OutcomeBrush","OutcomeHint","NameBrush"})PropertyChanged?.Invoke(this,new(name));}
 bool working,focused,workBusy;int workProgress;string recentHint="";
 public bool Working=>working;public bool WorkBusy=>workBusy;public int WorkProgress=>workProgress;public string RecentHint=>LinkKind is "Unreachable" or "Access"?(LinkKind=="Access"?"Нет доступа":"Нет связи")+" · "+(LinkHint.Split('\n').FirstOrDefault(t=>t.StartsWith("Последний ответ:"))?.Split(" · ")[0].Replace("Последний ответ:","ответ")??"нет успешных ответов"):recentHint;
 public System.Windows.Visibility ActivityVisibility=>working?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
 public System.Windows.Media.Brush ActivityBrush=>focused?(System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SelectionSurface"):working?Theme.Brush("ActiveSurface"):System.Windows.Media.Brushes.Transparent;
 public void SetActivity(bool active,int? progress,string hint,bool focus){working=active;workBusy=active&&!progress.HasValue;workProgress=progress??0;recentHint=hint;focused=focus;foreach(var n in new[]{"Working","WorkBusy","WorkProgress","RecentHint","ActivityVisibility","ActivityBrush"})PropertyChanged?.Invoke(this,new(n));}
 public string LinkKind {get;private set;}="Unknown";string linkHint="";public string LinkHint {get=>string.IsNullOrWhiteSpace(linkHint)?"Доступность WinRM ещё не проверена. Нажмите «Подключение» или «Обновить статусы». Это не означает отсутствие связи.":linkHint;private set=>linkHint=value;}public System.Windows.Media.Brush LinkBrush=>LinkKind=="Connected"?Theme.Brush("SuccessText"):LinkKind=="SavedConnected"?Theme.Brush("TextMuted"):LinkKind=="Access"?Theme.Brush("WarningText"):Theme.Brush("TextMuted");
 public void SetConnection(string kind,string hint){LinkKind=kind;LinkHint=hint;foreach(var n in new[]{"LinkKind","LinkHint","LinkBrush","RecentHint"})PropertyChanged?.Invoke(this,new(n));}
 public string Outcome {get;set;}="";string outcomeHint="";public string OutcomeHint {get=>string.IsNullOrWhiteSpace(outcomeHint)?"Состояние WSUS неизвестно: данные ещё не получены. Нажмите «Прочитать отчёт сервера» или «Обновить статусы».":outcomeHint;set=>outcomeHint=value;}
 public string OutcomeIcon=>Outcome switch {"Error" or "SyncError"=>"✖","Warning"=>"⚠","Success" or "Stale"=>"✓","Reboot"=>"⟳","NoWsus"=>"⊘","Unreachable"=>"⌁","Access"=>"🔒",_=>"?"};
 public System.Windows.Media.Brush OutcomeBrush=>Outcome switch {"Error" or "SyncError"=>Theme.Brush("ErrorText"),"Warning" or "Reboot"=>Theme.Brush("WarningText"),"Success"=>Theme.Brush("SuccessText"),"Stale"=>Theme.Brush("InfoText"),_=>Theme.Brush("TextMuted")};
 public void SetOutcome(string value,string hint){Outcome=value;OutcomeHint=hint;Refresh();}
 public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class LocalDateConverter : System.Windows.Data.IValueConverter {
 public static string Format(string value)=>DateTimeOffset.TryParse(value,out var time)?time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss zzz"):value;
 public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>parameter?.ToString()=="Short"&&DateTimeOffset.TryParse(value?.ToString(),out var time)?time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"):Format(value?.ToString()??"");
 public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}


