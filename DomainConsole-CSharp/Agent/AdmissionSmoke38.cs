using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static void AdmissionSmoke38(string root){
   var first=FixtureJob37("A",Guid.NewGuid().ToString());first.ConsoleLabel37="Рабочая консоль";FixtureFolder37(root,first);ReserveControl37();string before=File.ReadAllText(ControlFile37);
   var next=FixtureJob37("A",Guid.NewGuid().ToString());FixtureFolder37(root,next);Admission38();var result=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(FilePath("admission38.json"),Encoding.UTF8));if(Convert.ToBoolean(result["Allowed"])||(string)result["ReasonCode"]!="active_plan"||!((string)result["Message"]).Contains("Новый запуск"))throw new Exception("Same owner admission reason/UTF8 failed");if(File.ReadAllText(ControlFile37)!=before||File.Exists(FilePath("status.json")))throw new Exception("Rejected admission changed operation state");
   var other=FixtureJob37("B");FixtureFolder37(root,other);Admission38();result=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(FilePath("admission38.json"),Encoding.UTF8));if((string)result["ReasonCode"]!="other_console"||!((string)result["Message"]).Contains("Рабочая консоль"))throw new Exception("Foreign owner UTF8 message lost");
   File.WriteAllText(Path.Combine(root,first.Id,"status.json"),Json.Serialize(new DomainConsole.Shared.RemoteState{Status="Completed",Ended=DateTime.UtcNow.ToString("o")}));FixtureFolder37(root,next);Admission38();result=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(FilePath("admission38.json"),Encoding.UTF8));if(!Convert.ToBoolean(result["Allowed"]))throw new Exception("Finished same-console operation blocks new root plan");
   Console.WriteLine("Structured UTF8 admission, same-owner active plan, foreign owner and preservation of running state verified.");
  }
 }
}
