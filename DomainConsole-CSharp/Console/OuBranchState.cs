using System.ComponentModel;
namespace DomainConsole;
public sealed class OuBranchState:INotifyPropertyChanged {
 public string DN {get;} public string Name {get;} public List<ComputerRow> Members {get;}
 int working,marked61,total61;bool? selected61;
 public bool? Selected {get {var eligible=Members.Where(r=>!r.Target.AdDisabled).ToList();return eligible.Count==0?false:eligible.All(r=>r.Selected)?true:eligible.Any(r=>r.Selected)?null:false;}}
 public bool Busy=>working>0;
 public string Activity=>working>0?" · в работе: "+working:"";
 public string Tooltip=>"Компьютеров: "+Members.Count+" · отмечено: "+Members.Count(r=>r.Selected)+" · в работе: "+working+"\nГалочка выбирает подразделение, включая скрытые поиском ПК; отключённые записи AD пропускаются.";
 public OuBranchState(string dn,string name,List<ComputerRow> members){DN=dn;Name=name;Members=members;selected61=Selected;marked61=Members.Count(r=>r.Selected);total61=Members.Count;}
 public void Refresh(HashSet<string> active){
  int next=Members.Count(r=>active.Contains(r.Host)),marked=Members.Count(r=>r.Selected);var selection=Selected;
  bool activity=next!=working,check=selection!=selected61,tip=activity||marked!=marked61||Members.Count!=total61;
  working=next;selected61=selection;marked61=marked;total61=Members.Count;
  if(check)PropertyChanged?.Invoke(this,new(nameof(Selected)));
  if(activity){PropertyChanged?.Invoke(this,new(nameof(Busy)));PropertyChanged?.Invoke(this,new(nameof(Activity)));}
  if(tip)PropertyChanged?.Invoke(this,new(nameof(Tooltip)));
 }
 public event PropertyChangedEventHandler? PropertyChanged;
}
