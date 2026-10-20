using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease34(){
  var beforeIndex=KindBox.SelectedIndex;try{KindBox.SelectedIndex=1;if(((ComboBoxItem)KindBox.SelectedItem).Content.ToString()!="PowerShell"||FromEditor().Kind!="PowerShell")throw new Exception("Editor shell kind depends on display label");}finally{KindBox.SelectedIndex=beforeIndex;}
  var alias=new CommandStep{Kind="PS"};NormalizeCommandKinds34(new[]{alias});if(alias.Kind!="PowerShell")throw new Exception("Legacy PS alias rejected");bool rejected=false;try{NormalizeCommandKinds34(new[]{new CommandStep{Kind="bad"}});}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Unknown kind passed validation");
  foreach(var bad in new[]{".","", "{\"State\":"}){bool invalid=false;try{RemoteService.ParseData34<PollResult>(bad,"fixture status.json");}catch(RemoteDataException ex){invalid=ex.Message.Contains("JSON");}if(!invalid)throw new Exception("Malformed JSON accepted");}
  if(RemoteService.ParseData34<PollResult>("{\"State\":{\"Status\":\"Completed\"}}","fixture").State?.Status!="Completed")throw new Exception("Recovery JSON rejected");
  const string progress="#< CLIXML\n<Objs><Obj S=\"progress\"><MS><S>Служебный прогресс</S></MS></Obj></Objs>";
  if(PowerShellBridge.PlainError(progress)!=""||!PowerShellBridge.PlainError(progress+"\nОшибка клиента").Contains("Ошибка клиента")||!PowerShellBridge.PlainError("#< CLIXML\n<Objs><S S=\"Error\">Ошибка_x000A_подробности</S></Objs>").Contains("Ошибка\nподробности"))throw new Exception("CLIXML progress filtering lost a real error");
  var unsupported=FailureCard31(new PollResult{State=new(){Status="Failed",Error="Неподдерживаемый тип команды: PS [HRESULT 0x80131500]"}});if(unsupported.Contains("расшифровка")||!unsupported.Contains("оболочку"))throw new Exception("Unsupported shell presented as unknown Windows error");
  foreach(int dpi in new[]{96,120,144,192})foreach(string kind in new[]{"settings","reboot"}){var icon=UiIcon32.Create(kind);icon.Measure(new Size(18,18));icon.Arrange(new Rect(0,0,18,18));var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(18*dpi/96,18*dpi/96,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(icon);if(icon.DesiredSize.Width!=18||icon.DesiredSize.Height!=18)throw new Exception("Vector icon DPI dimensions changed");}
  var compact=CompactEvents34(new[]{new ClientEvent31{At="2026-01-01T10:00:00Z",Kind="Warning",Text="Ошибка чтения",Details="status.json"},new ClientEvent31{At="2026-01-01T10:01:00Z",Kind="Warning",Text="Ошибка чтения",Details="status.json"}});if(compact.Count!=1||compact[0].Repeats!=2||compact[0].FirstAt!="2026-01-01T10:00:00Z")throw new Exception("Read warning deduplication lost count / first time");
  var row=Current();var state=new RemoteState{Status="Running",Stage="Скачивание · Накопительное обновление Windows",StageStarted=DateTimeOffset.UtcNow.AddMinutes(-10).AddSeconds(-58).ToString("o"),Progress=38,TransferText="Скачано 321,6 / 843,2 МБ · 507 КБ/с (последний замер)"};var priorState=row.Target.State;var priorStatus=row.Target.Status;int theme=Theme.Current;double width=Width,height=Height;bool running=timer.IsEnabled;timer.Stop();
  try{row.Target.State=state;row.Target.Status="Running";string text=StageText34(row,state,true,true);if(!text.StartsWith(state.Stage+"\nСкачано 38% за 10 мин 58 с")||text.Contains("последний замер")||text.Contains("Выполняется")||text.Contains("Скачано 321"))throw new Exception("Progress caption not compact: "+text);
   foreach(int palette in new[]{0,1,2}){Theme.Apply(palette,false);if(((SolidColorBrush)Theme.Brush("ScrollTrack")).Color.A!=0)throw new Exception("Scroll track has its own background");foreach(string surface in new[]{"ActiveSurface","SelectionSurface","SidebarSurface"})if(Contrast33(Theme.Brush("WarningText"),Theme.Brush(surface))<4.5)throw new Exception("Gold warning contrast too low");Width=1000;Height=640;MainTabs.SelectedItem=CommandsTab;UpdateWsusStage();CurrentStage.Text=text;UpdateLayout();if(SettingsButton34.Content is not Viewbox||HeaderReboot.Content is not Viewbox)throw new Exception("Header icon is font dependent");if(HeaderNext31.Parent==CurrentStage.Parent)throw new Exception("Maintenance badge overlaps current stage");Capture32("release34-"+palette+"-compact");}
  }finally{row.Target.State=priorState;row.Target.Status=priorStatus;Theme.Apply(theme,false);Width=width;Height=height;UpdateWsusStage();if(running)timer.Start();}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"poll34.ps1"),RemoteService.PollScript34);
 }
}
