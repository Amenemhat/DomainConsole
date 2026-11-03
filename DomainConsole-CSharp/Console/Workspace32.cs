using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static bool QueueOnly32(IEnumerable<string>? sources)=>sources!=null&&sources.Any()&&sources.All(s=>s.StartsWith("PendingFileRenameOperations",StringComparison.OrdinalIgnoreCase));
 static string RebootDescription32(RebootEvidence reboot){bool only=QueueOnly32(reboot.Sources);return "Флаги перезагрузки: "+(only||!reboot.Required.HasValue?"не подтверждены":reboot.Required==true?"требуется · "+string.Join("; ",reboot.Sources.Select(ShortRebootSource)):"WUA / Windows Update / CBS отсутствуют")+" · "+LocalDateConverter.Format(reboot.CollectedUtc)+(reboot.Error.Length>0?" · "+reboot.Error:"");}

 void Background32_Changed(object sender,RoutedEventArgs e){if(IsLoaded&&e.OriginalSource==BackgroundGroup)Store.Write("background-expanded32.json",BackgroundGroup.IsExpanded);}
 void Palette32_Changed(){HeaderRecent31.ItemsSource=null;EventList32.ItemsSource=null;headerEventsKey32="";historyEventsKey32="";historyReset35=true;ApplyEditorMode();foreach(var r in Computers){r.Refresh();r.SetConnection(r.LinkKind,r.LinkHint);r.SetActivity(r.Working,r.WorkBusy?null:r.WorkProgress,r.RecentHint,ComputerGrid.SelectedItem==r);}RefreshOutcomes();RefreshBranches();if(ComputerGrid.SelectedItem is ComputerRow row){RefreshHeader31(row);UpdateWsusStage();}}
}
