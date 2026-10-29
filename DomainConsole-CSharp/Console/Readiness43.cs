using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ReadyProof43 {public string Key{get;set;}="";public string Job{get;set;}="";public string Boot{get;set;}="";public string At{get;set;}="";public string Report{get;set;}="";public string Scan{get;set;}="";public string Reboot{get;set;}="";}
public partial class MainWindow {
 readonly Dictionary<string,ReadyProof43> readyProofs43=Store.Read("ready-proofs43.json",new Dictionary<string,ReadyProof43>());
 readonly SemaphoreSlim pollQueue43=new(3);
 readonly HashSet<string> scheduledPoll46=new(StringComparer.OrdinalIgnoreCase);
 Task PollRows43(){
  var generation=PollingControl43.Token;
  foreach(var row in ActivePollRows().Where(JobPollDue42).OrderByDescending(IsCurrent).ToList()){
   string key=row.JobId+"|"+row.Host;if(!scheduledPoll46.Add(key))continue;
   _=PollOne46(row,key,generation);
  }
  return Task.CompletedTask;
 }
 async Task PollOne46(ComputerRow row,string key,CancellationToken generation){
  bool entered=false;try{await pollQueue43.WaitAsync(generation);entered=true;
   if(TransportBudget41.Paused)return;await Task.Delay(Random.Shared.Next(30,650),generation);
   if(!TransportBudget41.Paused)await Poll(row);
  }catch(OperationCanceledException) when(generation.IsCancellationRequested){}
  catch(Exception ex){ReadFailed34(row.Host,"результат задания",ex,row.JobId);}
  finally{if(entered)pollQueue43.Release();scheduledPoll46.Remove(key);}
 }

 void PollActivity43(string key,string label){if(closing)return;var generation48=PollingControl43.Token;Dispatcher.BeginInvoke(()=>{if(closing||generation48.IsCancellationRequested)return;foreach(var row in Computers)if(TransportBudget41.Key(ConnectionStore.Capture(row.Target))==key)row.SetService43(TransportBudget41.Paused?"":label);});}
 ClientAssessment41 PreserveReady43(ComputerRow row,ClientAssessment41 assessment,bool active,RemoteState? state,CachedWsusReport? cache,RebootView40 reboot,string scan){
  string key=Identity40.EndpointKey(ConnectionStore.Capture(row.Target))+"|"+OverviewServer+"|ssl="+(WsusSsl.IsChecked==true)+"|"+row.Target.DirectoryId;
  string job=HostJobs39(row.Host).FirstOrDefault(x=>IsWsusJob(x.Job)&&CurrentIdentity(x.Target)).Job?.Id??"";
  string boot=rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc??"";
  if(assessment.Kind=="Success"){
   var proof=new ReadyProof43{Key=key,Job=job,Boot=boot,At=DateTimeOffset.UtcNow.ToString("o"),Report=cache?.Report.LastReportedUtc??"",Scan=scan,Reboot=reboot.Stamp};
   if(!readyProofs43.TryGetValue(row.Host,out var previous)||previous.Key!=key||previous.Job!=job||previous.Report!=proof.Report||previous.Scan!=scan||previous.Reboot!=proof.Reboot){readyProofs43[row.Host]=proof;Store.Write("ready-proofs43.json",readyProofs43);}
   return assessment;
  }
  if(!readyProofs43.TryGetValue(row.Host,out var saved))return assessment;
  bool contradiction=active||row.Target.AdDisabled||saved.Key!=key||saved.Job!=job||(boot.Length>0&&saved.Boot!=boot)||assessment.Kind is "Error" or "SyncError" or "Reboot" or "Needed" or "NoWsus" or "Working"||reboot.Required==true||cache?.Found==false||cache?.Report.NeededCount>0||cache?.Report.FailedCount>0||cache?.Report.ApprovedUnknownCount>0||cache?.Report.LastSyncResult is "Failed" or "FailedWithErrors";
  if(contradiction){readyProofs43.Remove(row.Host);Store.Write("ready-proofs43.json",readyProofs43);return assessment;}
  return new("Success","Проверено · необходимых обновлений нет","Последняя полная проверка: "+Display36.Date(saved.At)+"\nПерезагрузка не требовалась · отчёт WSUS подтверждён: "+Display36.Date(saved.Report)+"\nСледующая проверка — по расписанию или вручную");
 }
}

