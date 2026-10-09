using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;

namespace DcsCampaign;

/// DCS-ATC (Tower, AWACS, Funk – eigenes Projekt) gehört fest dazu: ist es installiert, startet die Kampagne es mit der Mission;
/// fehlt es oder gibt es ein neueres Release, lädt sie beim Start das öffentliche Release (Zip mit Setup) und installiert es still. Das Setup der DCS-ATC fragt dabei
/// einmal nach Adminrechten (Eintrag in MissionScripting.lua) – das bleibt dessen Sache.
static class Atc
{
    const string Rel = "https://github.com/Iceman1991/DCS-ATC-Release/releases/latest";
    public const string Url = Rel + "/download/DCS-ATC.zip";
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B7E2C1A-4D2F-4E8B-9A11-DC5A7C0A7C01}_is1";   // AppId aus DcsAtc.iss
    public static string State = "";                                     // Oberfläche: leer = alles gut, sonst Fortschritt oder Fehler
    static int busy;

    public static string? Exe => !Program.Opt("Atc") ? null : new[] {   // im Setup abgewählt: wie nicht installiert (Kampagne spricht AWACS selbst) Registry.CurrentUser.OpenSubKey(Key)?.GetValue("InstallLocation") as string,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DCS-ATC") }
        .Where(d => !string.IsNullOrEmpty(d)).Select(d => Path.Combine(d!, "DcsAtc.exe")).FirstOrDefault(File.Exists);

    /// Mit der Mission: DCS-ATC starten, falls installiert und noch nicht offen (wie DCS-mit-ATC.cmd).
    public static void Launch()
    {
        if (Program.Dev != null || Exe is not { } exe || Process.GetProcessesByName("DcsAtc").Length > 0) return;
        try { Process.Start(new ProcessStartInfo(exe, "--auto") { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized }); }
        catch (Exception e) { State = "DCS-ATC could not start: " + e.Message; }
    }

    /// Beim Start der App (und auf „Erneut versuchen“): fehlt DCS-ATC oder ist ein neueres Release draußen (und ATC läuft nicht), im Hintergrund laden und installieren.
    public static void Ensure()
    {
        if (Program.Dev != null || !Program.Opt("Atc") || Interlocked.Exchange(ref busy, 1) == 1) return;
        new Thread(() => { try { if (Exe == null || Process.GetProcessesByName("DcsAtc").Length == 0 && Newer(Latest(), Registry.CurrentUser.OpenSubKey(Key)?.GetValue("DisplayVersion") as string)) Install(); } finally { busy = 0; } }) { IsBackground = true }.Start();
    }

    /// Neuestes Release: GitHub leitet …/releases/latest auf …/releases/tag/v0.12.0-alpha um. Offline: null, die installierte bleibt.
    static string? Latest()
    {
        try
        {
            using var hc = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
            using var r = hc.Send(new HttpRequestMessage(HttpMethod.Head, Rel));
            return r.Headers.Location?.ToString().Split('/')[^1];
        }
        catch { return null; }
    }

    /// Release-Tag neuer als die installierte Version ("v0.13.0-alpha" gegen DisplayVersion "0.12.0-alpha")? Unbekannt = nein.
    public static bool Newer(string? tag, string? installed) => V(tag) is { } t && V(installed) is { } i && t > i;
    static Version? V(string? s) => Version.TryParse(s?.TrimStart('v').Split('-')[0], out var v) ? v : null;

    static void Install()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-Atc");
        var zip = Path.Combine(dir, "DCS-ATC.zip");
        try
        {
            Directory.CreateDirectory(dir);
            State = "Downloading DCS-ATC …";
            using (var hc = new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
            using (var r = hc.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).Result.EnsureSuccessStatusCode())
            using (var src = r.Content.ReadAsStream())
            using (var dst = File.Create(zip))
            {
                long total = r.Content.Headers.ContentLength ?? 0, done = 0;
                var buf = new byte[1 << 20];
                for (int n; (n = src.Read(buf)) > 0;)
                {
                    dst.Write(buf, 0, n); done += n;
                    State = total > 0 ? $"Downloading DCS-ATC: {done * 100 / total} % of {total >> 20} MB" : $"Downloading DCS-ATC: {done >> 20} MB";
                }
            }
            State = "Installing DCS-ATC …";
            var setup = Unpack(zip, dir);
            using var p = Process.Start(new ProcessStartInfo(setup, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true })!;
            p.WaitForExit();
            State = Exe != null ? "" : $"DCS-ATC installation aborted (code {p.ExitCode})";
        }
        catch (Exception e) { State = "DCS-ATC not installed: " + (e.InnerException ?? e).Message; }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// Setup aus dem Release-Zip holen (DCS-ATC-Setup.exe liegt dort neben den Lies-mich-Dateien).
    public static string Unpack(string zip, string dir)
    {
        using var z = ZipFile.OpenRead(zip);
        var e = z.Entries.FirstOrDefault(e => e.Name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase)) ?? throw new IOException("no setup in the package");
        var setup = Path.Combine(dir, e.Name);
        e.ExtractToFile(setup, true);
        return setup;
    }
}
