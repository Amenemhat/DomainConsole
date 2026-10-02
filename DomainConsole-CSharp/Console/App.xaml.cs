using System.Windows;
namespace DomainConsole;
public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);DispatcherUnhandledException+=(s,a)=>{if(Environment.GetEnvironmentVariable("DOMAINCONSOLE_UI_SMOKE")=="1"){System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"smoke-error.txt"),a.Exception.ToString());Shutdown(1);a.Handled=true;return;}MessageBox.Show(a.Exception.Message,"Domain Console");a.Handled=true;};}
}
