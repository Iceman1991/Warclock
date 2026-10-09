using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DcsCampaign;

/// Engine-Host: Uhr, Speichern, lokaler Webserver (nur localhost, keine Adminrechte) mit Fog of War.
static class Web
{
    public static World W = null!;
    public static readonly object L = new();
    static DateTime lastSave = DateTime.UtcNow;
    static double acc;
    static DateTime skip;                       // „+1 h“: mit 1024× hinspulen statt springen (Karte bleibt flüssig)
    public static string SaveFile => Path.Combine(Program.Dir, "saves", "kampagne.json");
    static readonly string WebDir = Path.Combine(AppContext.BaseDirectory, "web");
    public static readonly int[] Speeds = [0, 1, 2, 4, 8, 16, 32, 64, 256, 1024];
    static int Port, Lan;                       // Lan: Port für Mitspieler im Netz (0 = nicht eingeladen)
    static readonly string Code = Random.Shared.Next(100000, 1000000).ToString();
    static readonly Dictionary<string, DateTime> Online = [];
    static string[] Ips = [];

    public static int Serve(int port)
    {
        var http = new HttpListener();
        http.Prefixes.Add($"http://localhost:{port}/");
        Port = port;
        try { http.Start(); }
        catch (HttpListenerException)
        {
            try       // läuft schon? Dann nur Oberfläche öffnen. Sonst gehört der Port einem fremden Programm.
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                if (hc.GetStringAsync($"http://localhost:{port}/api/static").Result.Contains("\"Speeds\"")) { Open(port); return 0; }
            }
            catch { }
            Console.WriteLine($"Port {port} is in use by another program – Warclock cannot start. Press Enter to close.");
            Console.ReadLine();
            return 1;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(SaveFile)!);
        W = Load() ?? Scenario.Create(Theater.Get("Caucasus"), Environment.TickCount & 0xffff);
        W.Speed = 0;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { lock (L) Save(); };
        new Thread(Clock) { IsBackground = true }.Start();
        Bridge.Start();
        Atc.Ensure();                                                    // DCS-ATC fehlt: im Hintergrund laden und installieren
        Console.Title = "Warclock";
        Console.WriteLine($"Warclock is running: http://localhost:{port}/  (close this window to quit, progress is saved)");
        Open(port);
        while (true)
        {
            var ctx = http.GetContext();
            ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
        }
    }

    static void Open(int port) { if (Program.Dev == null) Process.Start(new ProcessStartInfo($"http://localhost:{port}/") { UseShellExecute = true }); }

    /// Spielstand laden; ist er kaputt (z. B. Absturz beim Schreiben), die Sicherung nehmen und den kaputten beiseitelegen.
    public static World? Load(string? file = null)
    {
        file ??= SaveFile;
        foreach (var f in new[] { file, file + ".bak" })
            try { if (File.Exists(f)) { var w = World.FromJson(File.ReadAllText(f)); Scenario.Upgrade(w); if (f != file && File.Exists(file)) File.Move(file, file + ".kaputt", true); return w; } }
            catch (Exception e) { Console.WriteLine($"Save {Path.GetFileName(f)} is unreadable ({e.Message})."); }
        return null;
    }

    public static void Save()
    {
        var tmp = SaveFile + ".tmp";
        File.WriteAllText(tmp, W.ToJson());
        if (File.Exists(SaveFile)) File.Copy(SaveFile, SaveFile + ".bak", true);
        File.Move(tmp, SaveFile, true);
        lastSave = DateTime.UtcNow;
    }

    /// Laufender Krieg bleibt im Archiv (vor „Neu“ und „Laden“)
    static void Archive() { if (File.Exists(SaveFile)) File.Copy(SaveFile, Path.Combine(Path.GetDirectoryName(SaveFile)!, $"kampagne-{DateTime.Now:yyyy-MM-dd-HHmmss}.json"), true); }

    /// Uhrtempo für Uhr und Karte: DCS-Mission 0 (Bridge treibt, Karte rechnet mit 1×), Vorspulen 1024×, sonst gewählt
    static double Rate(World w) => w.Mission != "" ? 0 : skip > w.Time ? 1024 : w.Speed;

    static void Clock()
    {
        var sw = Stopwatch.StartNew();
        double last = 0;
        while (true)
        {
            Thread.Sleep(100);
            double now = sw.Elapsed.TotalSeconds, dt = now - last;
            last = now;
            lock (L)
            {
                double speed = Rate(W);                                  // während einer DCS-Mission treibt Bridge die Uhr (DCS-Zeit)
                acc += dt * speed;
                for (int n = 0; acc >= 60 && n < 300; n++) { Sim.Advance(W, W.Time.AddMinutes(1)); acc -= 60; }
                if (acc >= 60) acc = 0;
                if ((DateTime.UtcNow - lastSave).TotalSeconds > 60) Save();
            }
        }
    }

    // ---------- HTTP ----------

    static void Handle(HttpListenerContext c)
    {
        var res = c.Response;
        try
        {
            var path = c.Request.Url!.AbsolutePath;
            if (!c.Request.IsLocal) { res.StatusCode = 403; res.Close(); return; }        // Mitspieler kommen nur über den Lan-Proxy
            string? you = null;
            if (c.Request.Headers["X-Dcscamp-Remote"] != null && path.StartsWith("/api/"))   // Mitspieler: Name + Code des Hosts
            {
                you = Regex.Replace(Uri.UnescapeDataString(c.Request.Headers["X-Who"] ?? ""), @"[^\p{L}\p{N} _.-]", "").Trim();
                if (you.Length > 16) you = you[..16];
                if (you == "" || c.Request.Headers["X-Code"] != Code) { res.StatusCode = 403; Send(res, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Join with your name and the host's code")); return; }
            }
            if (path.StartsWith("/api/"))
            {
                string body = c.Request.HasEntityBody ? new StreamReader(c.Request.InputStream, Encoding.UTF8).ReadToEnd() : "{}";
                var j = JsonNode.Parse(body.Length == 0 ? "{}" : body)!;
                string json;                                            // im Lock serialisieren: View() ist lazy, die Uhr ändert die Listen
                lock (L) json = World.J(Api(path[5..], j, c.Request.QueryString, you) ?? new { ok = true });
                Send(res, "application/json", Encoding.UTF8.GetBytes(json));
            }
            else if (path == "/bg/relief.png") Send(res, "image/png", W.Th.Relief);
            else if (path == "/bg/sat.png") Send(res, "image/png", W.Th.Satellite);
            else
            {
                var f = Path.GetFullPath(Path.Combine(WebDir, path == "/" ? "index.html" : path.TrimStart('/')));
                if (!f.StartsWith(WebDir) || !File.Exists(f)) { res.StatusCode = 404; res.Close(); return; }
                var type = Path.GetExtension(f) switch { ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", ".png" => "image/png", ".svg" => "image/svg+xml", _ => "application/octet-stream" };
                Send(res, type, File.ReadAllBytes(f));
            }
        }
        catch (Exception e)
        {
            try { res.StatusCode = e is ArgumentException or InvalidOperationException ? 400 : 500; Send(res, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(e.Message)); } catch { }
        }
    }

    static void Send(HttpListenerResponse r, string type, byte[] data)
    {
        r.ContentType = type;
        r.Headers["Cache-Control"] = "no-store";
        r.ContentLength64 = data.Length;
        r.OutputStream.Write(data);
        r.Close();
    }

    /// Einzelspieler: dein Pilot ist hinter den Linien vermisst – erst CSAR abwarten oder neuen Piloten anfangen
    static void Mia() { if (!W.Mp && Career.Active(W, W.Me) is { Status: "vermisst" } c) throw new ArgumentException($"{c.Rank} {c.Name} is MIA behind enemy lines – wait for CSAR or start a new pilot (Pilot window)"); }
    static void Take(Flight f, string? you, bool log = true)
    {
        foreach (var o in W.Flights.Where(o => !W.Mp || o.Done)) { o.Player = false; o.Owner = ""; }   // Mehrspieler: Flüge sammeln, alle in einer Mission
        f.Player = true; f.Owner = you ?? "";
        if (log) W.Log(W.Player, "spieler", $"{(you == null ? "You take" : you + " takes")} over {f.Callsign} ({f.Type}, {f.Task}), {(f.Status == "geplant" ? $"takeoff {f.Takeoff:HH:mm}" : "airborne")}, TOT {f.Tot:HH:mm}.");
    }
    static Flight Fl(JsonNode j) => W.Flights.FirstOrDefault(f => f.Id == (int)j["Flight"]!) ?? throw new ArgumentException("Flight not found");

    internal static object? Api(string p, JsonNode j, System.Collections.Specialized.NameValueCollection q, string? you = null)
    {
        if (you != null)                                                // Mitspieler: nur eigene Flüge nehmen und planen, alles andere macht der Host
        {
            Online[you] = DateTime.UtcNow;
            if (p is not ("static" or "state" or "assess" or "take" or "offer" or "flight" or "drop")) throw new ArgumentException("Only the host can do that");
            if (p is "take" or "offer" && !W.Mp) throw new ArgumentException("The host has not switched to multiplayer yet");
        }
        switch (p)
        {
            case "static": return Static();
            case "state": return View(you);
            case "speed": W.Speed = Speeds.Contains((int)j["Speed"]!) ? (int)j["Speed"]! : 0; return null;
            case "advance":
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                skip = W.Time.AddMinutes(Math.Clamp((int)j["Minutes"]!, 1, 24 * 60)); return null;
            case "save": Save(); return null;
            case "saves":
                return Directory.GetFiles(Path.GetDirectoryName(SaveFile)!, "kampagne*.json").OrderByDescending(File.GetLastWriteTime)
                    .Select(f => new { File = Path.GetFileName(f), T = File.GetLastWriteTime(f), Current = f == SaveFile });
            case "load":
            {
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                var f = Path.Combine(Path.GetDirectoryName(SaveFile)!, Path.GetFileName((string)j["File"]!));
                if (f == SaveFile) return null;
                if (!Path.GetFileName(f).StartsWith("kampagne") || !f.EndsWith(".json")) throw new ArgumentException("Not a save file");
                var w = Load(f) ?? throw new ArgumentException("Save file is unreadable");
                Save(); Archive();
                W = w; W.Speed = 0; Save(); return null;
            }
            case "new":
            {
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                var set = Scenario.Get((string?)j["Setup"] ?? "kaukasus");
                if (!Theater.Installed(set.Map)) throw new ArgumentException($"Map {set.Map} is not installed in DCS");
                var th = Theater.Get(set.Map);
                Archive();
                if (j["Sqns"] is JsonArray qs) set = set with { Sqns = [.. qs.Select(x => Scenario.Sqn(set, x!))] };   // Luftwaffen aus dem Startdialog
                TimeSpan? at = TimeSpan.TryParse((string?)j["Start"], out var tt) && tt >= TimeSpan.Zero && tt < TimeSpan.FromDays(1) ? tt : null;
                W = Scenario.Create(th, (int?)j["Seed"] ?? Environment.TickCount & 0xffff, set.Id, (string?)j["Carrier"] ?? "", set, at);
                if (j["Side"] is { } sd) W.Player = (string)sd! == "Red" ? Side.Red : Side.Blue;
                W.Fog = (bool?)j["Fog"] ?? true;
                Scenario.Difficulty(W, (int?)j["Air"] ?? 100, (int?)j["Ground"] ?? 100, (int?)j["Sam"] ?? 100, (int?)j["Skill"] ?? 0, (int?)j["Supply"] ?? 100);
                foreach (var (t, ps) in Profiles.All) if (ps.TryGetValue("Default", out var dk)) W.Kits[t] = Profiles.Copy(dk);   // Profil "Default" gilt ab Start
                Save(); return null;
            }
            case "take":
            {
                var f = Fl(j);
                if (f.Side != W.Player || !Air.SqnOf(W, f).Player) throw new ArgumentException("This aircraft type is not playable");
                if (f.Status is not ("geplant" or "in der Luft" or "im Einsatz")) throw new ArgumentException($"Flight is {En.Of(f.Status)} – can no longer be taken over");
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                if (you != null && f.Player && f.Owner != "" && f.Owner != you) throw new ArgumentException($"{f.Owner} already has {f.Callsign}");
                Mia();
                Take(f, you);
                return null;
            }
            case "offer":                                               // neuer Auftrag aus der Liste: Paket anlegen und übernehmen
            {
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                Mia();
                var f = Air.TakeOffer(W, (string)j["Task"]!, (string)j["Kind"]!, (int)j["Target"]!, (int)j["Sqn"]!);
                W.Log(W.Player, "ato", $"{W.Pkgs.First(p => p.Id == f.Pkg).Name} created (TOT {f.Tot:HH:mm}).");
                Take(f, you);
                return new { f.Id };
            }
            case "flight":
            {
                var f = Fl(j);
                if (f.Side != W.Player || f.Status != "geplant" || you != null && (!f.Player || f.Owner != you)) throw new ArgumentException("Only your own planned flights can be changed");
                if (j["Sqn"] is { } sq) Air.Swap(W, f, (int)sq!);
                if (j["Type"] is { } ty) Air.Retype(W, f, (string)ty!);
                if (j["Start"] is { } st) f.Start = (string)st!;
                if (j["Part"] is { } pt) f.Part = (int)pt!;
                if (j["N"] is { } nn)                                   // Rotte kleiner/größer: leere Plätze bleiben in der Staffel
                {
                    f.N = f.Alive = Math.Clamp((int)nn!, 1, Math.Min(4, f.N + Air.Free(W, Air.SqnOf(W, f))));
                    var c = (f.Crew == "" ? "P" : f.Crew).PadRight(f.N, 'K')[..f.N];
                    f.Crew = c.Contains('P') ? c : "P" + c[1..];
                }
                if (j["Crew"] is { } cr)
                {
                    var c = ((string)cr!).ToUpperInvariant();
                    if (c.Length != f.N || c.Any(ch => ch is not ('P' or 'K')) || !c.Contains('P')) throw new ArgumentException("Crew: player or AI per aircraft, at least one player");
                    f.Crew = c;
                }
                if (j["Load"] is { } ld) { f.Load = (string)ld!; f.Pyl = null; }
                if (j["Pyl"] is JsonObject py)                          // eine Station ändern: Rest wie bisher
                {
                    var pyl = Miz.Pylons(W, f).ToDictionary(x => x.Num, x => x.Clsid);
                    var sts = W.Th.Stations(f.Type);
                    foreach (var (k, v) in py)
                    {
                        int num = int.Parse(k); var id = (string?)v ?? "";
                        if (id != "" && !(sts.GetValueOrDefault(num)?.Contains(id) ?? false)) throw new ArgumentException($"Station {num}: {Theater.Wpn(id).Name} does not fit there");
                        pyl[num] = id;
                    }
                    f.Pyl = pyl;
                    f.Load = Theater.LoadName(pyl.OrderBy(x => x.Key).Select(x => x.Value).Where(x => x != ""));
                }
                if (j["Load"] != null || j["Pyl"] != null || j["N"] != null) { f.Missiles = Air.A2aCount(f.Load) * f.N; f.Arm = Air.ArmCount(f.Load) * f.N; }
                if (j["Tot"] is { } tt) { f.Tot = DateTime.Parse((string)tt!); Air.Retime(f); }
                if (j["Wps"] is JsonArray wps)
                    Air.SetRoute(f, wps.Select(x => new Wp { Name = (string?)x!["Name"] ?? "", Kind = (string?)x["Kind"] ?? "nav", X = (double)x["X"]!, Z = (double)x["Z"]!, Alt = (double)x["Alt"]!, Spd = (double)x["Spd"]! }).ToList());
                return Air.Assess(W, f);
            }
            case "assess": return Air.Assess(W, W.Flights.First(f => f.Id == int.Parse(q["id"]!)));
            case "kit":                                                 // Ausrüstung des Spielers je Muster
            {
                var type = (string)j["Type"]!;
                var (_, chaff, flare) = Miz.Tanks.GetValueOrDefault(type, (5000, 60, 60));
                if (j["Reset"] != null) { W.Kits.Remove(type); return null; }
                if (j["Save"] is { } sv)                                // Profil: aktuelle Ausrüstung unter einem Namen, für alle Kampagnen
                {
                    var n = ((string)sv!).Trim();
                    if (n == "" || n.Length > 24) throw new ArgumentException("Profile name: 1–24 characters");
                    (Profiles.All.TryGetValue(type, out var ps) ? ps : Profiles.All[type] = [])[n] = Profiles.Copy(W.KitOf(type));
                    Profiles.Write(); return null;
                }
                if (j["Load"] is { } lp) { W.Kits[type] = Profiles.Copy(Profiles.All.GetValueOrDefault(type)?.GetValueOrDefault((string)lp!) ?? throw new ArgumentException("Profile not found")); return null; }
                if (j["Drop"] is { } dp) { Profiles.All.GetValueOrDefault(type)?.Remove((string)dp!); Profiles.Write(); return null; }
                var k = W.Kits.TryGetValue(type, out var o) ? o : W.Kits[type] = new Kit();
                int I(string n, int lo, int hi) => Math.Clamp((int)j[n]!, lo, hi);
                if (j["Chaff"] != null) k.Chaff = I("Chaff", 0, chaff + flare);
                if (j["Flare"] != null) k.Flare = I("Flare", 0, chaff + flare);
                if (j["Chaff"] != null || j["Flare"] != null)          // ein Werfer: zusammen nicht mehr als die Grundausstattung
                {
                    if (k.Chaff < 0) k.Chaff = chaff; if (k.Flare < 0) k.Flare = flare;
                    if (k.Chaff + k.Flare > chaff + flare) { if (j["Chaff"] != null) k.Flare = chaff + flare - k.Chaff; else k.Chaff = chaff + flare - k.Flare; }
                }
                if (j["Gun"] != null) k.Gun = I("Gun", 0, 100);
                if (j["Fuel"] != null) k.Fuel = I("Fuel", 10, 100);
                if (j["Hmd"] != null) k.Hmd = I("Hmd", 0, 2);
                if (j["Ripple"] != null) k.Ripple = (bool)j["Ripple"]!;
                if (j["AltBaro"] != null) k.AltBaro = I("AltBaro", 0, 50000);
                if (j["AltRdr"] != null) k.AltRdr = I("AltRdr", 0, 5000);
                if (j["Tacan"] != null)
                {
                    var t = ((string)j["Tacan"]!).Trim().ToUpperInvariant();
                    if (t != "" && !System.Text.RegularExpressions.Regex.IsMatch(t, @"^([1-9]|[1-9]\d|1[01]\d|12[0-6])[XY]$")) throw new ArgumentException("TACAN: channel 1–126 and X or Y, e.g. 44X");
                    k.Tacan = t;
                }
                if (j["Cmds"] is JsonObject cm)
                    foreach (var (prog, v) in cm)
                    {
                        if (!Miz.CmdsProgs.Contains(prog) || v is not JsonArray a || a.Count != 4) throw new ArgumentException("Unknown ALE-47 program");
                        double D(int i, double lo, double hi) => Math.Clamp((double)a[i]!, lo, hi);
                        k.Cmds[prog] = [D(0, 0, 6), D(1, 1, 24), Math.Round(D(2, 0.25, 5) * 4) / 4, D(3, 0, 6)];
                    }
                if (j["Comm"] is JsonObject co)
                    foreach (var (key, v) in co)
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(key, @"^[12]-([1-9]|1\d|20)$")) throw new ArgumentException("Unknown channel");
                        if (v == null) { k.Comm.Remove(key); continue; }
                        var fq = (double)v["F"]!;
                        if (fq is < 30 or > 400) throw new ArgumentException("Frequency 30–400 MHz");
                        k.Comm[key] = new Ch { F = Math.Round(fq, 3), N = ((string?)v["N"] ?? "").Trim() is var n && n.Length > 14 ? n[..14] : n };
                    }
                return null;
            }
            case "package":
            {
                var kind = (string)j["Kind"]!; int id = (int)j["Target"]!; var task = (string)j["Task"]!;
                if (!Air.Attack.Contains(task)) throw new ArgumentException("Unknown mission type");
                var pos = kind switch
                {
                    "obj" => W.Objs.Where(o => o.Id == id).Select(o => Air.Seen(W, W.Player, o)).First(),
                    "unit" => W.Intel(W.Player).TryGetValue($"U{id}", out var s) ? new P(s.X, s.Z) : throw new ArgumentException("Unit not spotted"),
                    _ => throw new ArgumentException("Unknown target type"),
                };
                var pk = Air.Package(W, W.Player, task, kind, id, pos, "Commander's order", (int?)j["Sqn"]);
                if (pk == null) throw new ArgumentException("No suitable squadron available (range, readiness, night)");
                W.Log(W.Player, "ato", $"{pk.Name} created (TOT {pk.Tot:HH:mm}).");
                return new { pk.Id };
            }
            case "mp":
                W.Mp = (bool)j["Mp"]!;
                if (!W.Mp && j["Flight"] is { } keep) foreach (var o in W.Flights) o.Player = o.Id == (int)keep!;   // Einzelspieler: nur ein Flug
                return null;
            case "me": W.Me = ((string?)j["Name"] ?? "").Trim(); return null;
            case "newpilot":                                            // Einzelspieler: Pilot in den Ruhestand, nächster Einsatz = neuer Pilot
                if (Career.Active(W, W.Me) is { } rc) { rc.Status = "ausgeschieden"; rc.Cur = null; W.Downed.RemoveAll(d => d.Pilot == ~rc.Id); W.Log(rc.Side, "spieler", $"{rc.Rank} {rc.Name} retires after {rc.Sorties} sorties. Next sortie: a new pilot."); }
                return null;
            case "perf": W.Perf = Math.Clamp((int)j["Perf"]!, 0, 2); return null;
            case "drop":                                                // Mehrspieler: Flug aus der Mission nehmen, die KI fliegt ihn
            {
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                var f = Fl(j);
                if (you != null && f.Owner != you) throw new ArgumentException("Not your flight");
                if (you == null) f.Player = false;                      // Mitspieler gibt den Platz nur frei, ein anderer kann ihn nehmen
                f.Owner = "";
                return null;
            }
            case "takepkg":                                             // Mehrspieler: ganzes Paket als Client-Plätze freigeben, Mitspieler nehmen sich die Flüge
            {
                if (W.Mission != "") throw new InvalidOperationException("A DCS mission is running");
                var fs = W.Flights.Where(f => f.Pkg == (int)j["Pkg"]! && f.Side == W.Player && f.Status is "geplant" or "in der Luft" or "im Einsatz"
                    && W.Sqns.FirstOrDefault(q => q.Id == f.Sqn)?.Player == true).ToList();
                if (fs.Count == 0) throw new ArgumentException("No playable flights in this package");
                W.Mp = true;
                foreach (var f in fs) { Take(f, null, false); f.Crew = new string('P', f.N); }
                W.Log(W.Player, "spieler", $"{W.Pkgs.First(p => p.Id == fs[0].Pkg).Name}: {string.Join(", ", fs.Select(f => f.Callsign))} open for players ({fs.Sum(f => f.N)} client slots).");
                return null;
            }
            case "invite": Invite(); return null;
            case "tacview":                                             // jüngste Tacview-Aufnahme seit der Mission öffnen
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tacview");
                var since = File.Exists(W.MissionFile) ? File.GetLastWriteTime(W.MissionFile) : DateTime.MinValue;
                var f = Directory.Exists(dir) ? new DirectoryInfo(dir).GetFiles("*.acmi").Where(x => x.LastWriteTime > since).MaxBy(x => x.LastWriteTime) : null;
                if (f == null) throw new ArgumentException("No Tacview recording of this mission in Documents\\Tacview – is Tacview installed and recording?");
                Process.Start(new ProcessStartInfo(f.FullName) { UseShellExecute = true });
                return null;
            }
            case "phase": W.CmdOf(W.Player).Set = Math.Clamp((int)j["Phase"]!, 0, 4); Air.Commander(W, W.Player); return null;
            case "truce":                                               // Waffenstillstandsangebot der KI annehmen = Kriegsende entlang der Front
                if (W.Truce == "") return null;
                if ((bool)j["Accept"]!) { (W.Over, W.Speed) = ($"Ceasefire – the war ends along the current front line. {W.Truce}", 0); W.Log(Side.None, "kriegsende", W.Over); }
                else W.Log(W.Player, "ato", "Ceasefire offer rejected – the war goes on.");
                W.Truce = ""; return null;
            case "prio":
            {
                var c = W.CmdOf(W.Player);
                if (j["Task"] is { } tk) { var t = (string)tk!; if (!Air.Attack.Contains(t)) throw new ArgumentException("Unknown mission type"); c.Prio[t] = Math.Clamp((int)j["V"]!, 0, 2); }
                if (j["Focus"] is { } fc) c.Focus = Math.Clamp((int)fc!, -1, W.Axes.Count - 1);
                W.Log(W.Player, "ato", "Commander: priorities " + string.Join(", ", Air.Attack.Select(a => $"{a} {new[] { "off", "normal", "high" }[c.Prio.GetValueOrDefault(a, 1)]}")) + (c.Focus >= 0 ? $", main effort {W.Axes[c.Focus].Name}" : "") + ".");
                return null;
            }
            case "fly": { var r = Miz.Fly(W); Atc.Launch(); return r; }
            case "atc": Atc.Ensure(); return null;
            case "end": Bridge.End(W, "Mission ended by you"); Save(); return null;
        }
        throw new ArgumentException("unknown: " + p);
    }

    // ---------- Daten für die Oberfläche ----------

    static object Static() => new
    {
        W.Map, MapName = W.Th.Map.Name, W.Scen, Setup = W.S.Name,
        Setups = Scenario.All.Select(x => new { x.Id, x.Map, MapName = Theater.Def(x.Map).Name, x.Name, x.Text, x.BlueText, x.RedText, Installed = Theater.Installed(x.Map),
            Play = x.Sqns.Select(q => q.Type).Where(Air.Flyable).Distinct(), Cv = x.Cv.Lat != 0, Sqns = x.Sqns.Select(q => new { Side = q.S.ToString(), q.Type, q.Name, q.N }) }),
        Types = Air.Perfs.Where(kv => W.Th.Payloads.GetValueOrDefault(kv.Key)?.Count > 0 || kv.Value.Roles is "AWACS" or "Tanker" or "Transport" || Air.Cabin(kv.Key))   // wählbar: mit Beladungen in DCS (oder ohne Waffen)
            .Select(kv => new { Type = kv.Key, Fly = Air.Flyable(kv.Key) }),
        Supercarrier = Theater.Supercarrier,
        Nodes = W.Nodes.Select(n => new { n.Id, n.Name, n.X, n.Z, n.Size, n.Key }),
        Edges = W.Edges.Select(e => new { e.A, e.B, e.Pass, e.Bridge, e.Rail, e.Snow }),
        Axes = W.Axes.Select(a => new { a.Name, a.Keys }),
        Bases = W.Th.Bases.Select(b => new { b.Id, b.Name, b.X, b.Z, b.RwyHdg, b.Rwy }),
        Relief = new { X0 = W.Th.HX0 - W.Th.HH * W.Th.HCell, Z0 = W.Th.HZ0, X1 = W.Th.HX0, Z1 = W.Th.HZ0 + W.Th.HW * W.Th.HCell },
        Sat = new { W.Th.SatBox.X0, W.Th.SatBox.Z0, W.Th.SatBox.X1, W.Th.SatBox.Z1 },
        Speeds, Air.Phases,
        Loads = Playable.ToDictionary(t => t, t => W.Th.Payloads.GetValueOrDefault(t, []).Select(p => new { p.Name, Pyl = p.Pylons.ToDictionary(y => y.Num, y => y.Clsid) })),
        Stations = Playable.ToDictionary(t => t, W.Th.Stations),
        Wpn = Playable.SelectMany(t => W.Th.Stations(t).Values.SelectMany(v => v)).Concat(Playable.SelectMany(t => W.Th.Payloads.GetValueOrDefault(t, [])).SelectMany(p => p.Pylons.Select(y => y.Clsid)))
            .Distinct().ToDictionary(id => id, id => new object[] { Theater.Wpn(id).Name, Theater.Wpn(id).N }),
        Tanks = Playable.ToDictionary(t => t, t => { var k = Miz.Tanks.GetValueOrDefault(t, (5000, 60, 60)); return new { k.Fuel, k.Chaff, k.Flare }; }),
        Cmds = Miz.CmdsProgs,
        Parts = Scenario.Parts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Select(p => p.Name)),
        Variants = Playable.ToDictionary(t => t, t => Air.Variants(W.Th, t)),
    };
    static IEnumerable<string> Playable => W.Sqns.Select(q => q.Type).Where(Air.Flyable).SelectMany(t => Air.Variants(W.Th, t)).Distinct();   // alle Muster mit Cockpit in dieser Kampagne (Neu/Laden lädt die Seite neu)

    /// Lagebild der Spielerseite: eigene Kräfte vollständig, Gegner nur soweit aufgeklärt.
    /// Mitspieler einladen: Weiterleitung aus dem Netz auf den lokalen Server (HttpListener im Netz bräuchte Adminrechte)
    static void Invite()
    {
        if (Lan > 0) return;
        var l = new TcpListener(Program.Dev != null ? IPAddress.Loopback : IPAddress.Any, Port + 2);   // Dev-Test: kein Firewall-Dialog
        try { l.Start(); } catch (SocketException) { throw new ArgumentException($"Port {Port + 2} is in use – cannot invite players"); }
        Lan = Port + 2;
        Ips = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()       // ohne Hyper-V/WSL-Switches und Adressen ohne DHCP (169.254)
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && !n.Name.StartsWith("vEthernet"))
            .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(u => u.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254.")).Select(a => a.ToString()).ToArray();
        new Thread(() => { while (true) { var c = l.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(_ => Relay(c)); } }) { IsBackground = true }.Start();
    }

    /// Eine Anfrage je Verbindung: Host-Kopf auf localhost, Kennung „Mitspieler“ (vom Client gesendete wird verworfen), Antwort zurück
    static void Relay(TcpClient c)
    {
        try
        {
            using (c) using (var up = new TcpClient("127.0.0.1", Port))
            {
                NetworkStream a = c.GetStream(), b = up.GetStream();
                if (ReadHead(a) is not { } rq) return;
                b.Write(Rewrite(rq.Head, $"Host: localhost:{Port}", "X-Dcscamp-Remote: 1", "Connection: close")); b.Write(rq.Body);
                _ = a.CopyToAsync(b);                                   // Rest des Körpers
                if (ReadHead(b) is not { } rs) return;
                a.Write(Rewrite(rs.Head, "Connection: close")); a.Write(rs.Body);
                b.CopyTo(a);
            }
        }
        catch { }
    }
    static (string Head, byte[] Body)? ReadHead(NetworkStream s)
    {
        var buf = new byte[32768]; int n = 0;
        while (n < buf.Length && s.Read(buf, n, buf.Length - n) is var r && r > 0)
        {
            n += r;
            int i = buf.AsSpan(0, n).IndexOf("\r\n\r\n"u8);
            if (i >= 0) return (Encoding.Latin1.GetString(buf, 0, i), buf[(i + 4)..n]);
        }
        return null;
    }
    static byte[] Rewrite(string head, params string[] set) => Encoding.Latin1.GetBytes(string.Join("\r\n",
        head.Split("\r\n").Where((l, i) => i == 0 || !Regex.IsMatch(l, @"^(host|connection|keep-alive|x-dcscamp-[\w-]*)\s*:", RegexOptions.IgnoreCase)).Concat(set)) + "\r\n\r\n");

    static object View(string? you = null)
    {
        var w = W; var me = w.Player; var know = w.Intel(me);
        bool Mine(Side s) => s == me || !w.Fog;
        Sight? K(string k) => know.GetValueOrDefault(k);
        return new
        {
            w.Time, w.Start, w.Speed, Rate = w.MissionLive ? 1 : Rate(w), Acc = Math.Round(acc, 1), Player = me, w.Fog, w.Wx, w.Mission, w.MissionLive, w.MissionFile, w.MissionStart, w.Debrief, w.Sorties, w.Name, w.Over, w.Esc, w.Truce, w.Mp, w.Perf, w.History, You = you ?? "",
            Online = Online.Where(kv => kv.Value > DateTime.UtcNow.AddSeconds(-10)).Select(kv => kv.Key), Invite = you == null && Lan > 0 ? new { Port = Lan, Code, Ips } : null, Cmd = w.CmdOf(me), Atc = Atc.State,
            Owners = w.Nodes.Select(n => (int)n.Owner),
            Fronts = w.Axes.Select(a => Sim.Front(w, a) is var (r, b) ? new[] { r, b } : null),
            Line = Sim.Line(w).Select(p => new { p.Side, p.X, p.Z }),
            Units = w.Units.Select(u =>
            {
                var s = Mine(u.Side) ? null : K($"U{u.Id}");
                if (!Mine(u.Side) && s == null) return null;
                return new
                {
                    u.Id, u.Name, u.Side, u.Kind, u.Template, Veh = u.Veh,
                    X = s?.X ?? u.X, Z = s?.Z ?? u.Z, Str = s?.Str ?? u.Str,
                    Supply = s == null ? u.Supply : -1, Fuel = s == null ? u.Fuel : -1, Line = s == null ? u.Line : -1, Morale = s == null ? u.Morale : -1, Dig = s == null ? u.Dig : -1,
                    Order = s == null ? u.Order : "", Why = s == null ? u.Why : $"{s.Src}", Seen = s?.T, Axis = s == null ? u.Axis : -1, Dp = s == null ? u.Dp : -1,
                    Path = s == null ? (u.Next >= 0 ? new[] { u.Next } : []).Concat(u.Path) : [],
                };
            }).Where(x => x != null),
            Objs = w.Objs.Select(o =>
            {
                var s = Mine(o.Side) ? null : K($"O{o.Id}");
                if (!Mine(o.Side) && s == null) return null;
                return new
                {
                    o.Id, o.Name, o.Kind, o.Side, X = s?.X ?? o.X, Z = s?.Z ?? o.Z, o.Sys, o.BaseId, o.Value,
                    Health = s?.Str ?? o.Health, Stock = s == null ? o.Stock : -1, Fuel = s == null ? o.Fuel : -1, Flow = s == null ? o.Flow : -1, o.Cap, Range = o.Range, o.MaxAlt,
                    Missiles = s == null ? o.Missiles : -1, Seen = s?.T, Src = s?.Src ?? "", Claim = s?.Claim ?? false, Parts = s == null ? o.Parts : s.Parts,
                    Arms = s == null && o.Kind == OK.Airbase ? Air.Stores(w, o) : null, ArmsFull = s == null && o.Kind == OK.Airbase ? Air.Full(w, o) : null, Quiet = s == null && o.QuietUntil > w.Time, Decoy = s == null && o.Decoy,
                };
            }).Where(x => x != null),
            Sqns = w.Sqns.Select(q => Mine(q.Side)
                ? new { q.Id, q.Name, q.Type, q.Side, q.Country, q.Base, Ac = q.Ac, q.Size, q.Lost, Repair = q.Repair.Count, q.Roles, q.Player, Pilots = q.Pilots }
                : new { q.Id, q.Name, q.Type, q.Side, q.Country, q.Base, Ac = -1, q.Size, Lost = -1, Repair = -1, q.Roles, Player = false, Pilots = new List<Pilot>() }),
            Flights = w.Flights.Where(f => f.Status != "gestrichen").Select(f =>
            {
                var s = Mine(f.Side) ? null : K($"F{f.Id}");
                if (!Mine(f.Side) && (s == null || s.T < w.Time.AddMinutes(-3) || !Air.Up(f))) return null;
                return s == null
                    ? new { f.Id, f.Pkg, f.Side, f.Type, f.Callsign, f.Task, f.N, f.Alive, f.Status, f.Load, f.Pyl, f.Crew, f.Start, f.Why, f.Result, f.Takeoff, f.Land, f.Tot, f.X, f.Z, f.Alt, f.Freq, f.Tacan, f.Missiles, f.Arm, f.Player, f.InDcs, f.Sqn, f.Target, f.TargetKind, f.Part, f.Owner, Route = f.Route, Src = "" }
                    : new { f.Id, Pkg = -1, f.Side, Type = "?", Callsign = "Contact", Task = "?", N = (int)s.Str, Alive = (int)s.Str, Status = "", Load = "", Pyl = (Dictionary<int, string>?)null, Crew = "", Start = "", Why = "", Result = "", Takeoff = s.T, Land = s.T, Tot = s.T, s.X, s.Z, f.Alt, Freq = 0.0, Tacan = 0, Missiles = -1, Arm = -1, Player = false, InDcs = false, Sqn = -1, Target = -1, TargetKind = "", Part = -1, Owner = "", Route = new List<Wp>(), s.Src };
            }).Where(x => x != null),
            Pkgs = w.Pkgs.Where(p => p.Side == me),
            Convoys = w.Convoys.Select(c =>
            {
                var s = Mine(c.Side) ? null : K($"C{c.Id}");
                if (!Mine(c.Side) && s == null) return null;
                return new { c.Id, c.Side, c.Rail, c.Ship, c.Navy, Range = Sim.NavyRange(c), Types = c.Types.Take(s == null ? c.Trucks : (int)s.Str), Missiles = s == null && c.Navy ? c.Missiles : -1, Sim.Kind(c).What, Troops = c.Reinf >= 0 && s == null ? w.S.Reinf[c.Reinf].Name : "", X = s?.X ?? c.X, Z = s?.Z ?? c.Z, Trucks = s == null ? c.Trucks : (int)s.Str,
                    Tons = s == null ? c.Tons : -1, Fuel = s == null ? c.Fuel : -1, To = s == null ? Sim.ObjName(w, c.To) : "?", Path = s == null ? c.Path.Skip(c.Leg) : [] };
            }).Where(x => x != null),
            Ops = w.Axes.Select(a => new { a.Id, a.Name, Op = a.Op != "" && (a.OpSide == me || !w.Fog || a.Warned || a.Op != "Bereitstellung") ? a.Op : "", a.OpSide, a.OpSince }),
            Gates = w.S.Gates.Where(g => Mine(g.S)).Select(g => new { g.At, Open = Scenario.Find(w, g.At)?.Owner == g.S, Tons = g.Mun + g.Pol }),
            Events = w.Events.Where(e => e.Side == me || e.Side == Side.None || !w.Fog).TakeLast(400),
            Threats = Air.Threats(w, me, 0).Select(t => t.Name),
            w.Report, Downed = w.Downed.Count(d => d.Side == me), w.Logbook, w.Me,
            w.Kits, Profiles = Profiles.All.ToDictionary(p => p.Key, p => p.Value.Keys.Order().ToList()), Offers = w.Over == "" ? Air.Offers(w) : [], Plans = w.Flights.Where(f => f.Player && !f.Done && f.Side == me).ToDictionary(pf => pf.Id.ToString(), pf =>
                new { Comm = new[] { Miz.Comm(w, pf, 1), Miz.Comm(w, pf, 2) }, Cmds = Miz.Cmds(w), Alt = new[] { Miz.AltWarn(w).Baro, Miz.AltWarn(w).Rdr }, Tacan = Miz.TacanOf(w, pf), Dtc = Miz.DtcName(), Home = Miz.Home(w, pf).Name }),
        };
    }
}
