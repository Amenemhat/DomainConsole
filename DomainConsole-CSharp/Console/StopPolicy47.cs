using System;
using DomainConsole.Shared;
namespace DomainConsole;

// The selected tab and the plan's future steps do not identify the running operation.
internal readonly record struct StopPolicy47(bool Wua, bool Command, bool Immediate, bool Force) {
 public static StopPolicy47 For(RemoteJob? job,RemoteState? state){
  string kind=job!=null&&state!=null&&state.Step>0&&state.Step<=job.Steps.Count?job.Steps[state.Step-1].Kind:"";
  bool wua=kind is "Updates" or "UpdateScan",command=kind is "CMD" or "PowerShell";
  bool immediate=state?.SupportsImmediateStop==true&&(command||(wua&&state.SupportsWuaCancel));
  bool force=command&&state?.StopStatus=="ForceRequired"||wua&&state?.Stage.Contains("KB890830",StringComparison.OrdinalIgnoreCase)==true;
  return new(wua,command,immediate,force);
 }
}
