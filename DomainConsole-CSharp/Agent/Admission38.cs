using System;
using System.IO;
using System.Text;
using System.Linq;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static void Admission38(){
   try{ReserveControl37();WriteJson("admission38.json",new{Allowed=true,ReasonCode="accepted",Message="",ActiveJobId="",PlanId=Job.PlanId37});}
   catch(Exception ex){
    string code=ex is TimeoutException?"control_busy":"control_unknown";string message="Допуск нового запуска не подтверждён. Текущее задание продолжает наблюдаться.";string active="",plan="";
    try{var record=ReadControl37();plan=record.PlanId;active=record.JobIds.LastOrDefault(id=>!JobFinished37(id))??record.LastJobId;
     if(Job.AutomaticChild31&&Job.PlanId37.Length>0&&Job.PlanId37!=Job.Id&&record.PlanId!=Job.PlanId37){code="stale_plan";message="Прежний план больше не владеет клиентом; его продолжение не запускалось.";}
     else if(record.OwnerId.Length>0&&record.OwnerId!=Job.ConsoleId37){code="other_console";message="Новый запуск не выполнен: управление принадлежит другой консоли («"+record.OwnerLabel+"»).";}
     else if(!NoActiveJobs37(record)){code="active_plan";message="Новый запуск не выполнен: уже идёт обслуживание либо завершение прежнего задания ещё не подтверждено.";}
    }catch{}
    Log("Admission declined: "+code+"; "+ex);
    WriteJson("admission38.json",new{Allowed=false,ReasonCode=code,Message=message,ActiveJobId=active,PlanId=plan});
   }
  }
 }
}
