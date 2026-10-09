using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace DomainConsole.Shared {
 public class CommandStep { public string Name {get;set;}="Новая команда"; public string Kind {get;set;}="CMD"; public string Code {get;set;}=""; }
 public class RemoteJob : System.ComponentModel.INotifyPropertyChanged {
  public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;public void RefreshSummary(){if(PropertyChanged!=null)PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("Summary"));}
  public string CommandPreview {get {var text=string.Join("; ",Steps.Select(s=>string.IsNullOrWhiteSpace(s.Code)?s.Name:Regex.Replace(s.Code,@"\s+"," ").Trim()));return text.Length>180?text.Substring(0,177)+"…":text;}}
  public string Summary {get {int running=Targets.Count(t=>t.Status=="Queued"||t.Status=="Sending"||t.Status=="Submitted"||t.Status=="Running");int errors=Targets.Count(t=>t.Status=="Failed"||t.Status=="CompletedWithErrors"||t.Status=="InterventionRequired"||t.Status=="ConnectionFailed"||t.Status=="AccessDenied");return running>0?"Активно: "+running:errors>0?"Ошибок: "+errors:Targets.Count==0?"Нет целей":"Завершено: "+Targets.Count;}}

  public bool QueueAfterWsus {get;set;}public int StallMinutes {get;set;}=10;public bool ExclusiveMaintenance {get;set;}
  public string Id {get;set;}=Guid.NewGuid().ToString();public string Created {get;set;}=DateTime.UtcNow.ToString("o");
  public string Name {get;set;}="Задание";public string OutputChannel {get;set;}="";public bool MicrosoftSource {get;set;}public bool AutoReboot {get;set;}public bool ContinueOnError {get;set;}public int DiagnosticLookbackMinutes {get;set;}=15;
  public List<CommandStep> Steps {get;set;}=new List<CommandStep>();public List<TargetRecord> Targets {get;set;}=new List<TargetRecord>();
 }
 public class TargetRecord {public string Name {get;set;}="";public string Host {get;set;}="";public string OU {get;set;}="";public string OS {get;set;}="";public string Status {get;set;}="Queued";public RemoteState State {get;set;}public string Output {get;set;}="";}
 public class StepResult {public int? NativeExitCode {get;set;}public string Name {get;set;}="";public int ExitCode {get;set;}public string Started {get;set;}="";public string Ended {get;set;}="";public string Error {get;set;}="";}
 public class RemoteState {
  public string ActivityMessage {get;set;}="";public string Recommendation {get;set;}="";public string RecommendationAction {get;set;}="";public string LastMovementUtc {get;set;}="";public string TransferText {get;set;}="";public int ForceCandidatePid {get;set;}public string ForceCandidateStarted {get;set;}="";
  public bool SupportsWuaCancel {get;set;}public bool SupportsImmediateStop {get;set;}public string StopStatus {get;set;}="";public string StopMessage {get;set;}="";
  public string LastOutputUtc {get;set;}="";public string ProgressValue {get;set;}="";public string LastProgressUtc {get;set;}="";public int ProcessId {get;set;}public string ProcessName {get;set;}="";
  public string StageEnded {get;set;}="";public string DiagnosticStatus {get;set;}="NotRequested";public string DiagnosticStarted {get;set;}="";public string DiagnosticEnded {get;set;}="";public string DiagnosticError {get;set;}="";public int DiagnosticProcessId {get;set;}
  public string StageStarted {get;set;}="";public string ReportRequestStatus {get;set;}="Не запрошен";
  public string Id {get;set;}="";public string Status {get;set;}="Queued";public string Stage {get;set;}="";public string Started {get;set;}="";public string Updated {get;set;}="";public string Ended {get;set;}="";
  public int Step {get;set;}public int Total {get;set;}public int? Progress {get;set;}public string Error {get;set;}="";public bool RebootRequired {get;set;}public string RestoreStatus {get;set;}="NotRequested";public int UpdatesRemaining {get;set;}public List<StepResult> Results {get;set;}=new List<StepResult>();
 }
 public class LibraryEntry {public string Name {get;set;}="";public string Category {get;set;}="Мои команды";public string Description {get;set;}="";public bool Favorite {get;set;}public bool MicrosoftSource {get;set;}public List<CommandStep> Steps {get;set;}=new List<CommandStep>();}
}
