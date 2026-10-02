using System.ComponentModel;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ComputerRow : INotifyPropertyChanged {
 public TargetRecord Target {get;} public string JobId {get;set;}="";
 public string Name=>Target.Name;public string Host=>Target.Host;public string OS=>Target.OS;public string OU=>Target.OU;
 bool selected;public bool Selected {get=>selected;set{selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
 public static string StatusText(string status)=>status switch {"Queued"=>"В очереди","Sending"=>"Передача задания","Submitted"=>"Задание передано","Running"=>"Выполняется","Completed"=>"Завершено","Succeeded"=>"Успешно","Failed"=>"Ошибка выполнения","AccessDenied"=>"Отказано в доступе","ConnectionFailed"=>"Ошибка подключения","Unavailable"=>"Ошибка передачи","Cancelled"=>"Отменено","AwaitingReboot"=>"Нужна перезагрузка","RebootScheduled"=>"Перезагрузка назначена",_=>status};
 public string Status=>StatusText(Target.Status);public string Stage=>Target.State?.Stage??"";public int Progress=>Target.State?.Progress??0;
 public bool Busy=>(Target.Status is "Sending" or "Submitted" or "Running") && Target.State?.Progress==null;
 public ComputerRow(TargetRecord target,string id=""){Target=target;JobId=id;}
 public void Refresh(){foreach(var name in new[]{"Status","Stage","Progress","Busy"})PropertyChanged?.Invoke(this,new(name));}
 public event PropertyChangedEventHandler? PropertyChanged;
}
