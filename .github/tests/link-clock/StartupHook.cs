using System.Reflection;
using System.Text.Json;

internal class StartupHook
{
    public static void Initialize()
    {
        string? marker = Environment.GetEnvironmentVariable("DC_TEST_LINK_CLOCK_RESULT");
        if (marker is null) return;
        try
        {
            var assembly = Assembly.GetEntryAssembly()!;
            if (assembly.GetName().Name != "DomainConsole") throw new Exception("Unexpected packaged executable");
            var window = assembly.GetType("DomainConsole.MainWindow", true)!;
            var interrupted = window.GetMethod("InterruptedTransfer", BindingFlags.NonPublic | BindingFlags.Static)!;
            var expire = window.GetMethod("ExpireUnavailableProof", BindingFlags.NonPublic | BindingFlags.Static)!;
            var linkType = interrupted.GetParameters()[0].ParameterType;
            var stateType = interrupted.GetParameters()[1].ParameterType;
            var rebootType = expire.GetParameters()[0].ParameterType;
            var now = DateTimeOffset.UtcNow;
            var results = new List<string>();
            void Check(bool success, string label)
            {
                if (!success) throw new Exception(label);
                results.Add(label);
            }
            static void Set(object target, string name, object? value) => target.GetType().GetProperty(name)!.SetValue(target, value);
            static object? Get(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target);
            object NewLink() => Activator.CreateInstance(linkType)!;
            bool Observe(object target, bool yes, string problem, DateTimeOffset time) =>
                (bool)linkType.GetMethod("Observe38")!.Invoke(target, [yes, problem, time, time])!;
            bool Due(object target, DateTimeOffset time) => (bool)linkType.GetMethod("RecoveryDue")!.Invoke(target, [time])!;
            foreach (var stage in new[] { "Скачивание", "Установка" })
            {
                object state = Activator.CreateInstance(stateType)!;
                Set(state, "Stage", stage + " · test package");
                Set(state, "Progress", 24);
                Set(state, "Updated", now.AddHours(-2).ToString("o"));
                object link = NewLink();
                DateTimeOffset loss = now.AddMinutes(1);
                object? View(DateTimeOffset time) => interrupted.Invoke(null, [link, state, time, now.ToString("o")]);
                for (int minute = -65; minute <= 0; minute++)
                    Observe(link, true, "", now.AddMinutes(minute));
                Check(View(now) is null && !Due(link, now), stage + ": 65 minutes reachable with unchanged 24 percent");
                Observe(link, false, "WinRM timeout", now);
                Check(View(now) is null, stage + ": a single timeout cannot start an incident");
                Observe(link, false, "WinRM timeout", now.AddSeconds(30));
                Observe(link, false, "WinRM timeout", loss);
                Check(!Due(link, loss.AddSeconds(29)), stage + ": no early accelerated read");
                foreach (var (at, count) in new[] { (30,1), (90,2), (210,3) })
                {
                    var time = loss.AddSeconds(at);
                    Check(Due(link, time), stage + ": read due at +" + at);
                    Set(link, "RecoveryChecks", count);
                    Observe(link, false, "WinRM timeout", time);
                    link = JsonSerializer.Deserialize(JsonSerializer.Serialize(link, linkType), linkType)!;
                    Check(!Due(link, time) && Equals(Get(link, "LossConfirmedUtc"), loss), stage + ": restart preserves retry " + count + " and incident start");
                }
                object live = View(loss.AddMinutes(15).AddTicks(-1))!;
                Check(Equals(Get(live, "Busy"), true) && Equals(Get(live, "Percent"), 24), stage + ": frozen percentage at 14:59.999");
                object history = View(loss.AddMinutes(15))!;
                Check(Equals(Get(history, "Busy"), false) && Get(history, "Percent") is null && Get(history,"Text")!.ToString()!.Contains("24%"), stage + ": historical stage and original percent at exactly 15:00");
                var nonTransfer = Activator.CreateInstance(stateType)!;
                Set(nonTransfer,"Stage","Обслуживание");
                Check(interrupted.Invoke(null,[link,nonTransfer,loss.AddMinutes(1),now.ToString("o")]) is null,"Maintenance has independent deadlines");
                Observe(link, true, "", loss.AddMinutes(18));
                Check(View(loss.AddMinutes(18)) is null && !Due(link,loss.AddMinutes(18)) && Equals(Get(link,"LastRestoredUtc"),loss.AddMinutes(18)), stage + ": transport return immediately stops the clock");
                Check(!Observe(link,false,"old timeout",loss.AddMinutes(17)),stage + ": late pre-recovery response rejected");
                Check(Equals(Get(state,"Progress"),24) && Equals(Get(state,"Updated"),now.AddHours(-2).ToString("o")), stage + ": operation progress and its own timestamp never rewritten");

            }
            foreach (var needed in new[]{true,false})
            {
                var proof = Activator.CreateInstance(rebootType, needed, true, now.ToString("o"), new[]{"WUA","CBS"}, "")!;
                var valid = expire.Invoke(null,[proof,now.AddHours(24).AddTicks(-1)])!;
                var old = expire.Invoke(null,[proof,now.AddHours(24)])!;
                Check(Equals(Get(valid,"Required"),needed) && Get(old,"Required") is null && Equals(Get(old,"Stamp"),now.ToString("o")), "24h source expiry retains original stamp, required="+needed);
            }
            IdentityCases.Run(assembly,Check);
            File.WriteAllText(marker,JsonSerializer.Serialize(results));
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(marker, error.ToString());
            Environment.Exit(1);
        }
    }
}
