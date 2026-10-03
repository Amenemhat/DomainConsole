using System;
using System.Collections.Generic;
namespace DomainConsole.Shared {
 public class CommandStep { public string Name {get;set;}="Новая команда"; public string Kind {get;set;}="CMD"; public string Code {get;set;}=""; }
 public class RemoteJob {
  public string Id {get;set;}=Guid.NewGuid().ToString();public string Created {get;set;}=DateTime.UtcNow.ToString("o");
  public string Name {get;set;}="Задание";public string OutputChannel {get;set;}="";public bool MicrosoftSource {get;set;}public bool AutoReboot {get;set;}public bool ContinueOnError {get;set;}
  public List<CommandStep> Steps {get;set;}=new List<CommandStep>();public List<TargetRecord> Targets {get;set;}=new List<TargetRecord>();
 }
 public class TargetRecord {public string Name {get;set;}="";public string Host {get;set;}="";public string OU {get;set;}="";public string OS {get;set;}="";public string Status {get;set;}="Queued";public RemoteState State {get;set;}public string Output {get;set;}="";}
 public class StepResult {public string Name {get;set;}="";public int ExitCode {get;set;}public string Started {get;set;}="";public string Ended {get;set;}="";public string Error {get;set;}="";}
 public class RemoteState {
  public string StageStarted {get;set;}="";public string ReportRequestStatus {get;set;}="Не запрошен";
  public string Id {get;set;}="";public string Status {get;set;}="Queued";public string Stage {get;set;}="";public string Started {get;set;}="";public string Updated {get;set;}="";public string Ended {get;set;}="";
  public int Step {get;set;}public int Total {get;set;}public int? Progress {get;set;}public string Error {get;set;}="";public bool RebootRequired {get;set;}public string RestoreStatus {get;set;}="NotRequested";public int UpdatesRemaining {get;set;}public List<StepResult> Results {get;set;}=new List<StepResult>();
 }
 public class LibraryEntry {public string Name {get;set;}="";public string Category {get;set;}="Мои команды";public string Description {get;set;}="";public bool Favorite {get;set;}public bool MicrosoftSource {get;set;}public List<CommandStep> Steps {get;set;}=new List<CommandStep>();}
}
