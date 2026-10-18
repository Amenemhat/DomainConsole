using System.ComponentModel;
namespace DomainConsole;
public sealed class OuBranchState:INotifyPropertyChanged {
 public string DN {get;} public string Name {get;} public List<ComputerRow> Members {get;}
 int working;
 public bool? Selected {get {var eligible=Members.Where(r=>!r.Target.AdDisabled).ToList();return eligible.Count==0?false:eligible.All(r=>r.Selected)?true:eligible.Any(r=>r.Selected)?null:false;}}
 public bool Busy=>working>0;
 public string Activity=>working>0?" · в работе: "+working:"";
 public string Tooltip=>"Компьютеров: "+Members.Count+" · отмечено: "+Members.Count(r=>r.Selected)+" · в работе: "+working+"\nГалочка выбирает подразделение, включая скрытые поиском ПК; отключённые записи AD пропускаются.";
 public OuBranchState(string dn,string name,List<ComputerRow> members){DN=dn;Name=name;Members=members;}
 public void Refresh(HashSet<string> active){working=Members.Count(r=>active.Contains(r.Host));foreach(string name in new[]{nameof(Selected),nameof(Busy),nameof(Activity),nameof(Tooltip)})PropertyChanged?.Invoke(this,new(name));}
 public event PropertyChangedEventHandler? PropertyChanged;
}
