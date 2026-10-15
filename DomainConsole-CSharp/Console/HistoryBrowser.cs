using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class HistoryEntry:INotifyPropertyChanged {
 public RemoteJob Job {get;}public TargetRecord Target {get;}public string Key=>Job.Id+"|"+Target.Host;
 public HistoryEntry(RemoteJob job,TargetRecord target){Job=job;Target=target;}
 public DateTimeOffset CreatedAt=>DateTimeOffset.TryParse(Job.Created,out var at)?at:DateTimeOffset.MinValue;
 public string TimeText=>CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
 public string Client=>Target.Name.Length>0?Target.Name:Target.Host;public string Host=>Target.Host;
 public string Channel=>IsUpdate(Job)?"WSUS":"Команды";
 public static bool IsUpdate(RemoteJob job)=>job.OutputChannel=="WSUS"||(job.OutputChannel==""&&job.Steps.Any(s=>s.Kind is "Updates" or "UpdateScan" or "Diagnostics"));
 public string Action=>Job.Name;public string Command=>Job.CommandPreview;public string FullCommand=>string.Join("\n\n",Job.Steps.Select(s=>s.Kind+": "+(s.Code.Length>0?s.Code:s.Name)));
 public string Result=>ComputerRow.StatusText(Target.Status);
 public double DurationSeconds=>DateTimeOffset.TryParse(Target.State?.Started,out var start)&&DateTimeOffset.TryParse(Target.State?.Ended,out var end)?Math.Max(0,(end-start).TotalSeconds):0;
 public string Duration=>DurationSeconds>0?((int)(DurationSeconds/3600)).ToString("D2")+TimeSpan.FromSeconds(DurationSeconds).ToString(@"\:mm\:ss"):"—";
 public string Batch=>Job.Targets.Count>1?"Массовый запуск · ПК: "+Job.Targets.Count:"Один компьютер";
 public event PropertyChangedEventHandler? PropertyChanged;public void Refresh()=>PropertyChanged?.Invoke(this,new(null));
}
public partial class MainWindow {
 public ObservableCollection<HistoryEntry> HistoryRows {get;}=new();ICollectionView? historyView;bool rebuildingHistory;bool historyFilterVisited,lastWorkWasWsus;
 RemoteJob? SelectedHistoryJob()=>(HistoryGrid.SelectedItem as HistoryEntry)?.Job;
 TargetRecord? SelectedHistoryTarget()=>(HistoryGrid.SelectedItem as HistoryEntry)?.Target;
 void SetupHistory(){historyView=CollectionViewSource.GetDefaultView(HistoryRows);historyView.Filter=o=>o is HistoryEntry entry&&HistoryMatches(entry,HistoryChannel.SelectedIndex,HistoryClient.Text,HistoryQuery.Text,HistoryCurrent.IsChecked==true?(ComputerGrid.SelectedItem as ComputerRow)?.Host:null);historyView.SortDescriptions.Add(new(nameof(HistoryEntry.CreatedAt),ListSortDirection.Descending));RefreshHistoryRows();}
 public static bool HistoryMatches(HistoryEntry entry,int channel,string client,string query,string? current){return (channel==0||(channel==1?entry.Channel=="Команды":entry.Channel=="WSUS"))&&(string.IsNullOrEmpty(current)||entry.Host.Equals(current,StringComparison.OrdinalIgnoreCase))&&(client.Length==0||entry.Host.Contains(client,StringComparison.OrdinalIgnoreCase)||entry.Client.Contains(client,StringComparison.OrdinalIgnoreCase))&&(query.Length==0||(entry.FullCommand+" "+entry.Action+" "+entry.Result).Contains(query,StringComparison.OrdinalIgnoreCase));}
 void RefreshHistoryRows(){if(historyView==null||rebuildingHistory)return;rebuildingHistory=true;try{var key=(HistoryGrid.SelectedItem as HistoryEntry)?.Key;var wanted=History.SelectMany(j=>j.Targets.Select(t=>new HistoryEntry(j,t))).ToList();if(wanted.Count!=HistoryRows.Count||!wanted.Select(e=>e.Key).SequenceEqual(HistoryRows.Select(e=>e.Key))){HistoryRows.Clear();foreach(var row in wanted)HistoryRows.Add(row);}else foreach(var row in HistoryRows)row.Refresh();historyView.Refresh();if(key!=null)HistoryGrid.SelectedItem=HistoryRows.FirstOrDefault(e=>e.Key==key);}finally{rebuildingHistory=false;}}
 void HistoryFilter_Changed(object s,RoutedEventArgs e){if(historyView==null)return;historyView.Refresh();}
 void FocusHistoryTarget(RemoteJob job,bool results){var target=SelectedHistoryTarget();OpenJob(job,results);if(target!=null){ComputerGrid.SelectedItem=Computers.FirstOrDefault(c=>c.Host.Equals(target.Host,StringComparison.OrdinalIgnoreCase));ShowCurrent();RevealCurrent();}}
 async void StopHistory_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{var job=SelectedHistoryJob()??throw new Exception("Выберите строку истории.");FocusHistoryTarget(job,true);Cancel_Click(s,e);await Task.CompletedTask;});
}
