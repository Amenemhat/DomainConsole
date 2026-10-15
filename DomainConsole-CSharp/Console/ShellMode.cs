using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 public static bool IsIndependentControl(List<CommandStep> steps)=>steps.Count==1&&(steps[0].Kind is "CMD" or "PowerShell")&&Regex.IsMatch(steps[0].Code,@"(?i)^\s*shutdown(?:\.exe)?\s+[-/][rsa]\b")&&!Regex.IsMatch(steps[0].Code,@"[\r\n;&|]");
 public static string? LikelyShell(string code){
  if(Regex.IsMatch(code,@"(?im)^\s*(?:powershell(?:\.exe)?|pwsh(?:\.exe)?|cmd(?:\.exe)?)\b"))return null;
  if(Regex.IsMatch(code,@"(?im)^\s*(?:[a-z]:\\[^\r\n]*?>\s*)?(?:\$[\w{]|(?:Get|Set|New|Remove|Select|Where|ForEach|Start|Stop|Restart|Repair|Import|Export|Test|Write|Invoke|Add|Clear|Enable|Disable)-[A-Za-z]+\b|(?:try|foreach|function|param)\s*[({])"))return "PowerShell";
  if(Regex.IsMatch(code,@"(?im)^\s*(?:@?echo\s+off\b|set\s+\w+=|for\s+%|if\s+(?:not\s+)?(?:exist|errorlevel)\b|rem\s)|%[A-Za-z_][A-Za-z0-9_]*%"))return "CMD";
  return null;
 }
 bool ConfirmShells(List<CommandStep> steps){foreach(var step in steps.Where(s=>s.Kind is "CMD" or "PowerShell")){var likely=LikelyShell(step.Code);if(likely==null||likely==step.Kind)continue;var w=new Window{Owner=this,Title="Выбор оболочки",Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(16)};panel.Children.Add(new TextBlock{Text="Команда похожа на "+likely+", выбран "+step.Kind+". Распознавание приблизительное. Как выполнить?",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,8)});panel.Children.Add(new TextBox{Text=step.Code,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var actions=new WrapPanel();foreach(var mode in new[]{likely,likely=="CMD"?"PowerShell":"CMD"}){var button=new Button{Content="В "+mode,Margin=new(0,8,6,0)};button.Click+=(s,e)=>{step.Kind=mode;if(steps.Count==1&&step.Code==Editor.Text)KindBox.SelectedIndex=mode=="PowerShell"?1:0;w.DialogResult=true;};actions.Children.Add(button);}var cancel=new Button{Content="Отмена",Margin=new(0,8,0,0)};cancel.Click+=(s,e)=>w.DialogResult=false;actions.Children.Add(cancel);panel.Children.Add(actions);w.Content=panel;if(w.ShowDialog()!=true)return false;}return true;}
 void ShellMode_Changed(object sender,SelectionChangedEventArgs e){if(Editor==null||RunEditorButton==null)return;ApplyEditorMode();if(IsLoaded)Store.Write("shell-mode.json",KindBox.SelectedIndex);}
 void ApplyEditorMode(){bool ps=KindBox.SelectedIndex==1;Editor.Background=new SolidColorBrush(ps?Color.FromRgb(16,34,57):Color.FromRgb(20,20,20));Editor.Foreground=Brushes.WhiteSmoke;Editor.CaretBrush=Brushes.White;Editor.Resources["ScrollTrack"]=Editor.Background;Editor.Resources["ScrollThumb"]=Brushes.SlateGray;RunEditorButton.Content="Выполнить в "+(ps?"PowerShell":"CMD");}
 void ApplyOutputMode(ComputerRow row){var job=ChannelJob(row,false);var kinds=job?.Steps.Select(s=>s.Kind).Distinct().ToArray()??Array.Empty<string>();var mode=kinds.Length==1?kinds[0]:kinds.Length>1?"Смешанный набор":"";OutputBox.ShellMode=mode;OutputCaption.Text="Результат · "+row.Name+(mode.Length>0?" · "+mode:"");}
}
