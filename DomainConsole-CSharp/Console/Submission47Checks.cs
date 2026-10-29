using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifySubmission47(){
  var rows=Computers.Take(3).ToList();var oldRow=ComputerGrid.SelectedItem;var oldTab=MainTabs.SelectedItem;
  var text=Editor.Text;int selectionStart=Editor.SelectionStart,selectionLength=Editor.SelectionLength;
  var recent=recentHosts.ToList();var work=workHosts.ToList();int oldLimit=recentLimit;bool expanded=RecentGroup.IsExpanded;var oldHistory=historyViewId;var microsoft=MicrosoftBox.IsChecked;var reboot=RebootBox.IsChecked;
  var saved=rows.Select(r=>(Row:r,Job:r.JobId,Target:Store.Clone(r.Target),Marked:r.Selected)).ToList();
  bool ticking=timer.IsEnabled;timer.Stop();var jobs=new List<RemoteJob>();
  try{
   var gates=new[]{new TaskCompletionSource<(bool Accepted,bool Force,bool Wait)>(),new TaskCompletionSource<(bool Accepted,bool Force,bool Wait)>()};
   var calls=new List<Task<bool>>();
   for(int i=0;i<2;i++){
    ComputerGrid.SelectedItem=rows[i];MainTabs.SelectedItem=WsusTab;
    var job=new RemoteJob{OutputChannel="Commands",Steps=new(){new(){Kind="CMD",Code="echo navigation fixture"}},MicrosoftSource=false,AutoReboot=false};jobs.Add(job);
    var frozen=new ComputerRow(ConnectionStore.Capture(rows[i].Target));frozen.Target.ConnectionAddress="192.0.2."+(i+1);
    int index=i;calls.Add(CompletePreparedSubmission47(job,new(){rows[i]},new(){frozen},()=>gates[index].Task,false));
    frozen.Target.ConnectionAddress="192.0.2.99";
   }
   ComputerGrid.SelectedItem=rows[2];MainTabs.SelectedItem=CommandsTab;rows[2].Selected=true;
   Editor.Text="Keep my editor and selection";Editor.Select(5,9);Editor.Focus();historyViewId="";
   MicrosoftBox.IsChecked=true;RebootBox.IsChecked=true;
   recentLimit=200;workHosts.Clear();recentHosts.Clear();recentHosts.AddRange(Computers.Reverse().Select(r=>r.Host));RecentGroup.IsExpanded=true;RefreshRecent();
   UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
   var viewer=Visual33<ScrollViewer>(RecentList).First();viewer.ScrollToVerticalOffset(10);UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
   if(viewer.VerticalOffset<1)throw new Exception("Navigation fixture failed to scroll");
   var anchor=RecentComputers[(int)viewer.VerticalOffset];
   Editor.Focus();var focus=Keyboard.FocusedElement;
   gates[1].SetResult((true,false,true));await calls[1];
   gates[0].SetResult((true,true,false));await calls[0];
   UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
   if(RecentComputers[(int)viewer.VerticalOffset]!=anchor)throw new Exception("Background submissions moved the recent-list reading anchor");
   if(ComputerGrid.SelectedItem!=rows[2]||MainTabs.SelectedItem!=CommandsTab||Editor.Text!="Keep my editor and selection"||Editor.SelectedText!="my editor"||Keyboard.FocusedElement!=focus||!rows[2].Selected)throw new Exception("Delayed A/B completion stole C navigation/editor/focus/marks");
   if(jobs[0].QueueAfterWsus||!jobs[0].AllowUpdateConflict||!jobs[1].QueueAfterWsus||jobs[1].AllowUpdateConflict)throw new Exception("Parallel submissions shared conflict decisions");
   for(int i=0;i<2;i++)if(jobs[i].MicrosoftSource||jobs[i].AutoReboot||jobs[i].Targets.Single().Host!=rows[i].Host||jobs[i].Targets.Single().ConnectionAddress!="192.0.2."+(i+1)||queue.Count(q=>q.Job.Id==jobs[i].Id)!=1)throw new Exception("Confirmed options/target changed or duplicate queued");
   var rejected=new RemoteJob{Steps=new(){new(){Kind="CMD",Code="echo rejected"}}};jobs.Add(rejected);
   if(await CompletePreparedSubmission47(rejected,new(){rows[0]},new(){new ComputerRow(ConnectionStore.Capture(rows[0].Target))},()=>Task.FromResult((false,false,false)),false)||History.Contains(rejected)||queue.Any(q=>q.Job.Id==rejected.Id))throw new Exception("Cancelled preparation still queued work");
  }finally{
   var ids=jobs.Select(j=>j.Id).ToHashSet();var pending=queue.Where(q=>!ids.Contains(q.Job.Id)).ToList();queue.Clear();foreach(var q in pending)queue.Enqueue(q);
   foreach(var j in jobs){History.Remove(j);foreach(var t in j.Targets)confirmedJobs.Remove(j.Id+t.Host);}
   foreach(var s in saved){s.Row.JobId=s.Job;s.Row.Target.Status=s.Target.Status;s.Row.Target.State=s.Target.State;s.Row.Target.Output=s.Target.Output;s.Row.Selected=s.Marked;}
   recentLimit=oldLimit;workHosts.Clear();workHosts.AddRange(work);RecentGroup.IsExpanded=expanded;recentHosts.Clear();recentHosts.AddRange(recent);Store.Write("recent-computers.json",recentHosts);RefreshRecent();
   MicrosoftBox.IsChecked=microsoft;RebootBox.IsChecked=reboot;ComputerGrid.SelectedItem=oldRow;MainTabs.SelectedItem=oldTab;historyViewId=oldHistory;
   Editor.Text=text;Editor.Select(selectionStart,selectionLength);Save();ShowCurrent();if(ticking)timer.Start();
  }
 }
}
