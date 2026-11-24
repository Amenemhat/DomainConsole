using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace DomainConsole.Agent {
 public sealed class ExistingUpdateTransfer {
  public string Id { get; set; }
  public string[] Urls { get; set; }
  public long Bytes { get; set; }
  public long Total { get; set; }
  public int Files { get; set; }
 }

 internal static partial class Program {
  const int OperationInProgress = unchecked((int)0x80240009);
  const string InspectUpdateTransfers = @"
Import-Module BitsTransfer -ErrorAction Stop
$jobs = @(Get-BitsTransfer -AllUsers -ErrorAction Stop | Where-Object {
    $_.DisplayName -eq 'WU Client Download' -and
    $_.JobState -in @('Queued','Connecting','Transferring','TransientError','Transferred')
} | ForEach-Object {
    $files = @($_.FileList)
    $total = if ([decimal]$_.BytesTotal -le [long]::MaxValue) { [long]$_.BytesTotal } else { 0 }
    @{ Id = [string]$_.JobId; Urls = @($files | ForEach-Object { [string]$_.RemoteName })
       Bytes = [long]$_.BytesTransferred; Total = $total; Files = $files.Count }
})
ConvertTo-Json -InputObject $jobs -Depth 4 -Compress
";

  sealed class UpdateObservationTimeout : TimeoutException {
   public UpdateObservationTimeout(string message) : base(message) {}
  }

  static ExistingUpdateTransfer[] ReadUpdateTransfers(string prefix) {
   if (fixtureCom54 != null)
    return fixtureTransfers54?.Invoke() ?? Array.Empty<ExistingUpdateTransfer>();
   var probe = prefix + "-bits-observation";
   if (PowerShell(InspectUpdateTransfers, probe, 12000) != 0) return null;
   try { return Json.Deserialize<ExistingUpdateTransfer[]>(File.ReadAllText(FilePath(probe + ".out"))); }
   catch (Exception ex) { Log("Не удалось прочитать снимок BITS: " + ex.Message); return null; }
  }

  static void AddDownloadUrls(dynamic update, HashSet<string> urls, int depth) {
   if (depth >= 8) return;
   try {
    dynamic contents = update.DownloadContents;
    for (int i = 0; i < Math.Min(256, (int)contents.Count); i++) {
     string url = (string)contents.Item(i).DownloadUrl;
     Uri parsed;
     if (Uri.TryCreate(url, UriKind.Absolute, out parsed) && (parsed.Scheme == "https" || parsed.Scheme == "http"))
      urls.Add(parsed.AbsoluteUri);
    }
    dynamic bundled = update.BundledUpdates;
    for (int i = 0; i < Math.Min(64, (int)bundled.Count); i++) AddDownloadUrls(bundled.Item(i), urls, depth + 1);
   } catch (Exception ex) { Log("Список URL обновления прочитан частично: " + ex.Message); }
  }

  static bool HasObservedUrl(ExistingUpdateTransfer transfer, HashSet<string> urls) {
   return (transfer.Urls ?? Array.Empty<string>()).Any(url => Uri.TryCreate(url, UriKind.Absolute, out var absolute) && urls.Contains(absolute.AbsoluteUri));
  }

  static object RefreshObservedUpdate(dynamic session, int source, Guid id, int revision, Stopwatch wait, TimeSpan limit) {
   dynamic search = session.CreateUpdateSearcher();
   search.ServerSelection = source;
   search.Online = false;
   var callback = new UpdateCallback();
   string key = "UpdateID='" + id.ToString("D") + "' and RevisionNumber=" + revision;
   dynamic operation = search.BeginSearch("(" + key + " and IsInstalled=0 and IsHidden=0) or (" + key + " and IsInstalled=1)", callback, null);
   try {
    var probe = Stopwatch.StartNew();
    while (!(bool)operation.IsCompleted) {
     if (Cancelled() || wait.Elapsed >= limit || probe.Elapsed >= TimeSpan.FromSeconds(25)) {
      try { operation.RequestAbort(); }
      catch (Exception ex) { Log("WUA не подтвердил запрос отмены проверки: " + ex.Message); }
      if (Cancelled()) return null;
      throw new UpdateObservationTimeout("Результат штатного обновления не подтверждён: проверка кэша Windows Update не завершилась.");
     }
     Thread.Sleep(250);
    }
    dynamic result = search.EndSearch(operation);
    if ((int)result.ResultCode != 2 || (int)result.Updates.Count != 1)
     throw new UpdateObservationTimeout("Результат штатного обновления не подтверждён: выбранная ревизия больше не видна однозначно. Повторите проверку обновлений.");
    return result.Updates.Item(0);
   } finally {
    if ((bool)operation.IsCompleted) operation.CleanUp();
    GC.KeepAlive(callback);
   }
  }

  static object ObserveExistingUpdate(dynamic session, object candidate, int source, string prefix,
      Stopwatch wait, bool installerWasBusy, bool inspectDownloads) {
   dynamic update = candidate;
   string title = (string)update.Title;
   Guid id = Guid.Parse((string)update.Identity.UpdateID);
   int revision = (int)update.Identity.RevisionNumber;
   var limit = TimeSpan.FromMinutes(Math.Max(1, Math.Min(120, Job.SearchTimeoutMinutes37)));
   var urls = new HashSet<string>(StringComparer.Ordinal);
   if (inspectDownloads && !(bool)update.IsDownloaded) AddDownloadUrls(update, urls, 0);
   ExistingUpdateTransfer previous = null;
   DateTime sample = default(DateTime);
   bool refresh = installerWasBusy;
   bool sawBackgroundTransfer = false;
   while (!Cancelled()) {
    if (wait.Elapsed >= limit) throw new UpdateObservationTimeout("Истекло время ожидания Windows Update. Результат не подтверждён; проверьте обновления повторно.");
    dynamic installer = session.CreateUpdateInstaller();
    string stage = "Ожидание установщика Windows Update";
    bool busy = (bool)installer.IsBusy;
    if (!busy && refresh) {
     update = RefreshObservedUpdate(session, source, id, revision, wait, limit);
     if (Cancelled()) return null;
     if ((bool)update.IsInstalled || (bool)update.IsDownloaded) return update;
    }
    if (!busy && !inspectDownloads) return update;
    ExistingUpdateTransfer match = null;
    if (!busy) {
     if ((bool)update.IsDownloaded) return update;
     var transfers = ReadUpdateTransfers(prefix);
     if (Cancelled()) return null;
     var matches = transfers?.Where(t => HasObservedUrl(t, urls)).Take(2).ToArray();
     if (transfers != null && transfers.Length == 0 && !sawBackgroundTransfer) return update;
     sawBackgroundTransfer |= transfers != null && transfers.Length > 0;
     if (matches != null && matches.Length == 1) {
      stage = "Скачивает Windows Update · " + title;
      if (matches[0].Files == 1) match = matches[0];
     } else {
      stage = transfers == null || transfers.Length == 0
       ? "Ожидание готовности обновления Windows Update"
       : "Ожидание загрузок Windows Update";
     }
    }
    if (State.Stage != stage) Stage(stage);
    var now = DateTime.UtcNow;
    if (match != null && match.Total > 0 && match.Bytes >= 0 && match.Bytes <= match.Total) {
     string speed = previous?.Id == match.Id && match.Bytes >= previous.Bytes
      ? " · " + ((match.Bytes - previous.Bytes) / Math.Max(1, (now - sample).TotalSeconds) / 1024).ToString("F1") + " КиБ/с"
      : "";
     State.ActivityMessage = string.Format("BITS · файл: {0:F1} / {1:F1} МиБ ({2:F1} %){3} · снимок {4:HH:mm}", match.Bytes / 1048576.0, match.Total / 1048576.0, match.Bytes * 100.0 / match.Total, speed, now.ToLocalTime());
    } else {
     State.ActivityMessage = busy ? "Windows не предоставила сведения о пакете и проценте текущей установки."
      : "Прогресс этого пакета не подтверждён. Проверяется готовность обновления в Windows Update.";
    }
    previous = match;
    sample = now;
    refresh = true;
    Save();
    var next = wait.Elapsed + (fixtureCom54 == null ? TimeSpan.FromMinutes(1) : TimeSpan.Zero);
    while (wait.Elapsed < next && wait.Elapsed < limit && !Cancelled()) Thread.Sleep(500);
   }
   return null;
  }
 }
}
