using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 public static string ShellKind34(int index)=>index switch {0=>"CMD",1=>"PowerShell",_=>throw new InvalidOperationException("Выберите CMD или PowerShell.")};
 public static void NormalizeLegacyShells34(IEnumerable<CommandStep> steps){foreach(var step in steps)if(string.Equals(step.Kind,"PS",StringComparison.OrdinalIgnoreCase))step.Kind="PowerShell";}
 public static void NormalizeCommandKinds34(IEnumerable<CommandStep> steps){foreach(var step in steps){if(string.Equals(step.Kind,"PS",StringComparison.OrdinalIgnoreCase))step.Kind="PowerShell";if(!new[]{"CMD","PowerShell","Updates","UpdateScan","Diagnostics","CacheCleanup","CacheResidueCleanup","ReportRecovery","ClientDiagnostics","DeliveryDiagnostics"}.Contains(step.Kind))throw new InvalidOperationException("Неподдерживаемый тип команды: «"+step.Kind+"». Выберите CMD или PowerShell; сценарий не отправлен.");}}
 public static string StageText34(ComputerRow row,RemoteState? state,bool active,bool fresh){
  if(!active)return TerminalText35(row,state);
  if(row.Target.Status=="Sending")return "Передача задания на клиент";
  if(row.Target.Status=="SubmissionUnknown")return "Передача не подтверждена; проверяется результат без повторного запуска";
  if(state==null)return "Задание передано; ожидается результат";
  string stage=row.Stage;string duration=LogPresentation.Duration(state,DateTimeOffset.UtcNow).Replace(" сек"," с");string percent=state.Progress.HasValue?(state.ProgressValue.Length>0?state.ProgressValue:state.Progress.ToString())+"%":"";
  string detail="";bool download=stage.StartsWith("Скачивание",StringComparison.OrdinalIgnoreCase),install=stage.StartsWith("Установка",StringComparison.OrdinalIgnoreCase);
  if(download||install){detail=(download?"Скачано":"Установлено")+(percent.Length>0?" "+percent:"")+(duration.Length>0?" за "+duration:"");if(state.TransferText.Length>0)detail+=" · "+Regex.Replace(state.TransferText,@"^Скачано\s*","").Replace(" (последний замер)","");}
  else detail=(percent.Length>0?percent+" · ":"")+duration;
  if(state.UpdateTotal38>0&&(download||install))detail="Обновление "+state.UpdateIndex38+" из "+state.UpdateTotal38+" · установлено: "+state.InstalledCount38+" · "+detail;
  if(!fresh)detail="Последний снимок · "+detail+" · ожидается обновление статуса";
  if(state.ActivityMessage.Length>0)detail+="\n"+state.ActivityMessage;if(state.StopMessage.Length>0)detail+="\n"+state.StopMessage;
  return stage+(detail.Length>0?"\n"+detail:"");
 }
 void IdentityHeader34_SizeChanged(object sender,SizeChangedEventArgs e)=>ArrangeIdentity34();
 void ArrangeIdentity34(){
  if(IdentityHeader34==null||IdentityHeader34.ActualWidth<=0)return;
  EventsRow38.MinHeight=EventHistoryGroup32.IsExpanded&&EventList32.Items.Count>0?65:0;EventsSplitter38.Visibility=EventHistoryGroup32.IsExpanded&&EventList32.Items.Count>0?Visibility.Visible:Visibility.Collapsed;
  EventsRow38.Height=EventHistoryGroup32.IsExpanded&&EventList32.Items.Count>0?new GridLength(Math.Min(ActualHeight<750?85:300,eventsHeight38)):GridLength.Auto;
  CurrentComputer.MaxWidth=Math.Max(120,Math.Min(260,IdentityHeader34.ActualWidth*0.3));

 }
 void RefreshServiceBadge34(ComputerRow row){
  string text="",details="";if(maintenancePlans.TryGetValue(row.Host,out var p)&&!p.Done){details=p.Phase+(p.Detail.Length>0?" · "+p.Detail:"");text=p.Phase.Contains("перезагруз",StringComparison.OrdinalIgnoreCase)?"Ждёт перезагрузки":p.Detail.Contains("Связь",StringComparison.OrdinalIgnoreCase)||p.Phase.Contains("связ",StringComparison.OrdinalIgnoreCase)?"Ждёт связи":p.Phase.Contains("готовност",StringComparison.OrdinalIgnoreCase)?"Ждёт готовности":"Обслуживание";}
  else if(p!=null&&p.Phase=="Требуется действие"){text="Требуется действие";details=p.Detail;}
  else {details=PostRebootText(row.Host).Split('\n')[0];if(details.Length>0)text="Ожидает возврата";}
  HeaderNext31.Text=text;HeaderNext31.ToolTip=details;HeaderNext31.Visibility=text.Length>0?Visibility.Visible:Visibility.Collapsed;
 }
 static List<ClientEvent31> CompactEvents34(IEnumerable<ClientEvent31> source){
  var raw=source.SelectMany(e=>e.Occurrences36.Count>0?e.Occurrences36:new List<ClientEvent31>{e}).DistinctBy(e=>e.At+"|"+e.Kind+"|"+e.Text+"|"+e.Details+"|"+e.Operation36);
  return raw.GroupBy(e=>e.Kind+"|"+e.Operation36+"|"+e.Text+"|"+e.Details).Select(g=>{var events=g.OrderByDescending(e=>e.At).ToList();var last=events[0];return new ClientEvent31{At=last.At,FirstAt=events.Select(e=>e.FirstAt.Length>0?e.FirstAt:e.At).Min()??last.At,Kind=last.Kind,Operation36=last.Operation36,Text=last.Text,Details=last.Details,Repeats=events.Sum(e=>e.Repeats),Occurrences36=events};}).OrderByDescending(e=>e.At).ToList();
 }
}
