using System.ComponentModel;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ComputerRow : INotifyPropertyChanged {
 public TargetRecord Target {get;} public string JobId {get;set;}="";
 public string Name=>Target.Name;public string Host=>Target.Host;public string OS=>Target.OS;public string OU=>Target.OU;
 bool selected;public bool Selected {get=>selected;set{selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
 public static string StatusText(string status)=>status switch {"Queued"=>"В очереди","Sending"=>"Передача задания","Submitted"=>"Задание передано","Running"=>"Выполняется","Completed"=>"Завершено","CompletedWithErrors"=>"Завершено с ошибками","VerifiedAfterReboot"=>"Проверено после перезагрузки","InterventionRequired"=>"Нужно вмешательство","Succeeded"=>"Успешно","Failed"=>"Ошибка выполнения","AccessDenied"=>"Отказано в доступе","ConnectionFailed"=>"Ошибка подключения","Unavailable"=>"Ошибка передачи","Cancelled"=>"Отменено","AwaitingReboot"=>"Нужна перезагрузка","RebootScheduled"=>"Перезагрузка назначена",_=>status};
 public string Status=>StatusText(Target.Status);public string Stage=>Target.State?.Stage??"";public int Progress=>Target.Status is "Completed" or "VerifiedAfterReboot" or "AwaitingReboot" or "RebootScheduled"?100:Target.State?.Progress??0;
 public bool Busy=>(Target.Status is "Sending" or "Submitted" or "Running") && Target.State?.Progress==null;
 public ComputerRow(TargetRecord target,string id=""){Target=target;JobId=id;}
 public void Refresh(){foreach(var name in new[]{"Status","Stage","Progress","Busy","OutcomeIcon","OutcomeBrush","OutcomeHint"})PropertyChanged?.Invoke(this,new(name));}
 public string Outcome {get;set;}="";public string OutcomeHint {get;set;}="";
 public string OutcomeIcon=>Outcome=="Error"?"✖":Outcome=="Warning"?"⚠":Outcome=="Success"?"✓":"";
 public System.Windows.Media.Brush OutcomeBrush=>Outcome=="Error"?System.Windows.Media.Brushes.OrangeRed:Outcome=="Warning"?System.Windows.Media.Brushes.Orange:System.Windows.Media.Brushes.LightGreen;
 public void SetOutcome(string value,string hint){Outcome=value;OutcomeHint=hint;Refresh();}
 public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class LocalDateConverter : System.Windows.Data.IValueConverter {
 public static string Format(string value)=>DateTimeOffset.TryParse(value,out var time)?time.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss zzz"):value;
 public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Format(value?.ToString()??"");
 public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}
