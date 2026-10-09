using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DcsCampaign;

/// Rückkanal DCS -> Kampagne: das Missionsskript schreibt "DCSCAMP|token|art|absZeit|..." per env.info in dcs.log,
/// hier wird mitgelesen. Kein Eingriff in MissionScripting.lua, keine Adminrechte. Während der Mission folgt die Kriegsuhr DCS.
static class Bridge
{
    static string LogFile => Path.Combine(Program.SavedGames, "Logs", "dcs.log");
    static readonly Regex Name = new(@"^([FUOCQ])(\d+)-(\d+)(?:/(\d+))?(?:-([rlx]))?$");

    /// Alle DCS-Profile in Saved Games: Spiel, Open Beta, Dedicated Server (DCS.dcs_serverrelease oder eigener Name per -w)
    public static string[] Profiles()
    {
        var sg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        return Directory.Exists(sg) ? Directory.GetDirectories(sg, "DCS*") : [];
    }

    /// Hook auch in jedes andere DCS-Profil (Dedicated Server), sobald er im Spielprofil steckt (Setup-Option „hook“). Wirkt ab dem nächsten Start des Servers.
    public static void SyncHooks()
    {
        var mine = Path.Combine(AppContext.BaseDirectory, "mod", "DcsCampaignHook.lua");
        if (!File.Exists(Path.Combine(Program.SavedGames, "Scripts", "Hooks", "DcsCampaignHook.lua")) || !File.Exists(mine)) return;
        var body = File.ReadAllBytes(mine);
        foreach (var p in Profiles().Where(p => Directory.Exists(Path.Combine(p, "Config"))))
        {
            var dst = Path.Combine(p, "Scripts", "Hooks", "DcsCampaignHook.lua");
            try { if (!File.Exists(dst) || !File.ReadAllBytes(dst).AsSpan().SequenceEqual(body)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.WriteAllBytes(dst, body); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// Läuft die Mission in einem anderen DCS-Profil (Dedicated Server, eigener Server aus dem Spiel mit anderem Profil)? Dann dessen dcs.log – das Token der Mission steht drin.
    public static string? Find(World w, string cur, IEnumerable<string>? logs = null)
    {
        var tag = Encoding.UTF8.GetBytes($"DCSCAMP|{w.Mission}|");
        foreach (var f in (logs ?? Profiles().Select(p => Path.Combine(p, "Logs", "dcs.log"))).Where(f => File.Exists(f) && !string.Equals(Path.GetFullPath(f), Path.GetFullPath(cur), StringComparison.OrdinalIgnoreCase)))
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long n = Math.Min(fs.Length, 1 << 20);
            fs.Position = fs.Length - n;
            var b = new byte[n];
            fs.ReadExactly(b);
            if (b.AsSpan().IndexOf(tag) >= 0) return f;
        }
        return null;
    }
    static DateTime scan;

    public static void Start() => new Thread(() =>
    {
        try { SyncHooks(); } catch (Exception e) { Console.WriteLine("Hook: " + e.Message); }
        while (true)
        {
            Thread.Sleep(1000);
            try { lock (Web.L) Poll(Web.W); }
            catch (Exception e) { Console.WriteLine("dcs.log: " + e.Message); }
        }
    }) { IsBackground = true }.Start();

    static void Poll(World w)
    {
        var file = w.LogFile != "" && File.Exists(w.LogFile) ? w.LogFile : LogFile;
        if (w.Mission != "" && !w.MissionLive && DateTime.Now > scan)            // neue Mission, noch kein Lebenszeichen: läuft sie woanders?
        {
            scan = DateTime.Now.AddSeconds(5);
            if (Find(w, file) is { } other) { file = w.LogFile = other; w.LogPos = 0; w.LogHead = ""; }
        }
        if (!File.Exists(file)) return;
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var head = new byte[Math.Min(200, fs.Length)];
        fs.ReadExactly(head);
        int nl = Array.IndexOf(head, (byte)'\n');
        var h = nl > 0 ? Encoding.UTF8.GetString(head, 0, nl) : "";
        if (w.LogPos < 0) { w.LogPos = w.Mission != "" ? 0 : fs.Length; w.LogHead = h; }        // erster Lauf: Altes überspringen
        if (fs.Length < w.LogPos || h != "" && w.LogHead != "" && h != w.LogHead)                 // DCS neu gestartet: neue Datei
        {
            w.LogPos = 0;
            if (w.MissionLive) End(w, "DCS restarted – crash? Regenerate the mission at the current war time");
        }
        if (h != "") w.LogHead = h;
        if (fs.Length <= w.LogPos) return;
        fs.Position = w.LogPos;
        var buf = new byte[fs.Length - w.LogPos];
        fs.ReadExactly(buf);
        int end = Array.LastIndexOf(buf, (byte)'\n');
        if (end < 0) return;
        w.LogPos += end + 1;
        Feed(w, Encoding.UTF8.GetString(buf, 0, end).Split('\n'));
        if (w.Mission == "" && Web.W == w) Web.Save();
    }

    static double N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// Logzeilen auswerten (auch für den Selbsttest).
    public static void Feed(World w, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            int i = line.IndexOf("DCSCAMP|", StringComparison.Ordinal);
            if (i < 0) continue;
            var p = line[(i + 8)..].TrimEnd('\r').Split('|');
            if (w.Mission == "" || p.Length < 3 || p[0] != w.Mission) continue;
            if (p.Length > 3) p[3] = Real(w, p[3]);                                      // nächster Einsatz: Maschine gehört jetzt zu einem neuen Flug
            if (p[1] == "KILL" && p.Length > 4) p[4] = Real(w, p[4]);
            if (!w.MissionLive) { w.MissionLive = true; w.Log(w.Player, "dcs", "DCS mission running – the war clock now follows DCS."); }
            var t = w.MissionStart.Date.AddSeconds(N(p[2]));
            if (t > w.Time) Sim.Advance(w, t);
            switch (p[1])
            {
                case "POS" when p.Length >= 7:
                    switch (Ent(w, p[3]))
                    {
                        case Flight f:
                            f.X = N(p[4]); f.Z = N(p[5]); f.Alt = N(p[6]);
                            if (f.Task == "Recon" && !f.Struck && f.Alive > 0 && f.Route.FirstOrDefault(r => r.Kind == "target") is { } tp && Geo.Dist(f.X, f.Z, tp.X, tp.Z) < 15000) Air.Photo(w, f);   // über dem Ziel: Fotos
                            break;
                        case Unit u: u.X = N(p[4]); u.Z = N(p[5]); break;
                        case Convoy c: c.X = N(p[4]); c.Z = N(p[5]); break;
                    }
                    break;
                case "WH" when p.Length >= 5:                                              // DCS-Platzlager: Kerosin kg, "wsType=n;…"
                    var wl = w.WhLast[(int)N(p[3])] = new() { ["fuel"] = N(p[4]) / 1000 };
                    foreach (var s in p.Length > 5 ? p[5].Split(';', StringSplitOptions.RemoveEmptyEntries) : []) if (s.Split('=') is [var ws, var n]) wl[ws] = N(n);
                    break;
                case "T": Miz.Sync(w); if (AllHome(w)) return; break;                      // Live-Befehle über den Hook
                case "MENU" when p.Length >= 5: Tasking.Menu(w, p[3], p[4], p.Length > 5 ? p[5] : ""); break;   // F10 → Kampagne
                case "CMD": Note(w, "hook"); break;
                case "SPAWN" when p.Length >= 4: InDcs(Ent(w, p[3]), true); break;      // Fahrplan-Gruppe in der Bubble: ab jetzt zählt DCS
                case "OUT" when p.Length >= 7:                                            // Bubble verlassen: Kampagne rechnet weiter
                    var e = Ent(w, p[3]);
                    InDcs(e, false);
                    if (e is Flight fo) { fo.X = N(p[5]); fo.Z = N(p[6]); }
                    break;
                case "DEAD" when p.Length >= 6: Lost(w, p[3], N(p[4]), N(p[5])); if (AllHome(w)) return; break;
                case "KILL" when p.Length >= 6: Kill(w, p[3], p[4], p[5]); break;
                case "EJECT" or "PILOTDEAD" or "TAKEOFF" when p.Length >= 4:
                    if (Note(w, $"{p[1].ToLowerInvariant()}:{p[3]}") && p[1] == "TAKEOFF" && w.DcsNotes.Any(s => s.StartsWith($"tgt:{p[3].Split('-')[0]}="))) Deb(w, $"{Who(w, p[3])} aircraft {p[3].Split('-')[^1]} takes off");
                    if (p[1] == "TAKEOFF") Career.Up(w, p[3]);
                    break;
                case "LAND" when p.Length >= 5:
                    if (p[4] == "" && p.Length >= 7 && Field(w, p[3], N(p[5]), N(p[6]))) break;     // Hubschrauber im Gelände: keine Landung am Platz
                    Landed(w, p[3], p[4]); if (AllHome(w)) return; break;
                case "IMPACT" when p.Length >= 7: Impact(w, p[3], N(p[4]), N(p[5]), N(p[6])); break;
                case "BIRTH" or "LEAVE" when p.Length >= 5:
                    Deb(w, $"{p[4]} {(p[1] == "BIRTH" ? "boards" : "leaves")} {Who(w, p[3])}");
                    if (p[1] == "BIRTH") { Note(w, "birth:" + p[3]); Career.Board(w, p[3], p[4]); }
                    else { if (!w.DcsNotes.Contains("takeoff:" + p[3])) w.DcsNotes.Remove("birth:" + p[3]); Career.Leave(w, p[3]); }   // am Boden ausgestiegen: zählt nicht
                    break;
                case "SAM" when p.Length >= 5:
                    if (p[4] != "clear") Deb(w, p[4] switch { "harm" => $"{Who(w, p[3] + "-1")}: radar off – HARM inbound", "jam" => $"{Who(w, p[3] + "-1")}: jammed – radar sees only inside burn-through range", _ => $"{Who(w, p[3] + "-1")}: early warning lost – autonomous, radar on" });
                    break;
                case "AIHOME" when p.Length >= 5: Deb(w, p[4] == "ok" ? $"{Who(w, p[3])}: player left – AI flies the aircraft home (slot \"…U\" to re-enter)" : $"{Who(w, p[3])}: player left, AI replacement failed ({p[4]})"); break;
                case "TESTSLOT" when p.Length >= 4: Deb(w, $"Test player slot via script: {p[3]}"); break;
                case "TRAIN" when p.Length >= 5: Deb(w, $"{p[3]}: {(p[4] == "ok" ? "runs as a real train on the track" : $"runs as a truck column ({p[4]})")}"); break;
                case "END": End(w, "mission ended in DCS"); return;
            }
        }
    }

    /// Mehrspieler: Mission vorbei, wenn jede Maschine, in der ein Mensch saß, gelandet oder verloren ist (Entscheidung 5) –
    /// mit 5 min Nachlauf, damit noch jemand über F10 → Kampagne → Nächster Einsatz weiterfliegen kann.
    static bool AllHome(World w)
    {
        var born = w.DcsNotes.Where(s => s.StartsWith("birth:")).Select(s => Real(w, s[6..])).ToList();
        var mark = w.DcsNotes.FirstOrDefault(s => s.StartsWith("home:"));
        if (!w.Mp || w.Mission == "" || born.Count == 0 || !born.All(u => w.DcsNotes.Contains("land:" + u) || w.DcsNotes.Contains("dead:" + u))) { if (mark != null) w.DcsNotes.Remove(mark); return false; }
        if (mark == null)
        {
            w.DcsNotes.Add($"home:{w.Time.Ticks}");
            Miz.Cmd("heim.lua", "trigger.action.outText('Warclock: all landed - mission ends in 5 minutes. To keep flying: F10 > Campaign > Next sortie.', 60)");
            return false;
        }
        if (w.Time < new DateTime(long.Parse(mark[5..])).AddMinutes(5)) return false;
        End(w, "all players landed");
        Miz.Cmd("ende.lua", "trigger.action.outText('Warclock: all landed - mission evaluated. The admin plans the next one, the server then loads it automatically.', 120)");
        return true;
    }

    static bool Note(World w, string s) { if (w.DcsNotes.Contains(s)) return false; w.DcsNotes.Add(s); return true; }
    /// alias:F12-1=F57-1 (Maschine) bzw. alias:F12=F57 (Gruppe, „F12 Rufname“): neuer Flug nach erneutem Start, auch mehrfach hintereinander.
    public static string Real(World w, string n)
    {
        for (int i = 0; i < 8; i++)
        {
            var k = n.Split(' ')[0];
            if (w.DcsNotes.LastOrDefault(s => s.StartsWith($"alias:{k}=")) is not { } a) break;
            n = a[(7 + k.Length)..] + n[k.Length..];
        }
        return n;
    }
    static void Deb(World w, string s) => w.Debrief.Add($"{w.Time:HH:mm} {s}");
    /// Gruppe "F12 …", "U5 …", "O7 …", "C9 …" -> Flug, Verband, Stellung, Transport.
    static object? Ent(World w, string group)
    {
        if (Name.Match(group.Split(' ')[0] + "-1") is not { Success: true } m) return null;
        int id = int.Parse(m.Groups[2].Value);
        return m.Groups[1].Value switch
        {
            "F" => w.Flights.FirstOrDefault(f => f.Id == id), "U" => w.Units.FirstOrDefault(u => u.Id == id),
            "O" => w.Objs.FirstOrDefault(o => o.Id == id), "C" => w.Convoys.FirstOrDefault(c => c.Id == id), _ => null,
        };
    }
    static void InDcs(object? e, bool on)
    {
        switch (e)
        {
            case Flight f when !f.Player: f.InDcs = on; break;
            case Unit u: u.InDcs = on; break;
            case Obj o: o.InDcs = on; break;
            case Convoy c: c.InDcs = on; break;
        }
    }
    static int Pilot(World w, string unit) => w.DcsNotes.FirstOrDefault(s => s.StartsWith($"pilot:{unit}=")) is { } s ? int.Parse(s.Split('=')[1]) : -1;

    static Side? SideOf(World w, string unit)
    {
        if (Name.Match(unit) is not { Success: true } m) return null;
        int id = int.Parse(m.Groups[2].Value);
        return m.Groups[1].Value switch
        {
            "F" => w.Flights.FirstOrDefault(f => f.Id == id)?.Side, "U" => w.Units.FirstOrDefault(u => u.Id == id)?.Side, "O" => w.Objs.FirstOrDefault(o => o.Id == id)?.Side,
            "C" => w.Convoys.FirstOrDefault(c => c.Id == id)?.Side, "Q" => w.Sqns.FirstOrDefault(q => q.Id == id)?.Side, _ => null,
        };
    }

    static string Who(World w, string name)
    {
        var m = Name.Match(name);
        if (!m.Success) return name;
        int id = int.Parse(m.Groups[2].Value);
        return m.Groups[1].Value switch
        {
            "F" => w.Flights.FirstOrDefault(f => f.Id == id) is { } f ? $"{f.Callsign} ({f.Type})" : name,
            "U" => w.Units.FirstOrDefault(u => u.Id == id)?.Name ?? name,
            "O" => w.Objs.FirstOrDefault(o => o.Id == id)?.Name ?? name,
            "C" => "Convoy",
            _ => name,
        };
    }

    static void Kill(World w, string killer, string target, string weapon)
    {
        if (!Note(w, $"kill:{target}={Who(w, killer)}, {weapon}")) return;
        if (Name.Match(killer) is { Success: true } km && km.Groups[1].Value == "F") Note(w, $"score:F{km.Groups[2].Value}:{target}");   // Abschüsse je Flug für die Nachbesprechung
        Career.Kill(w, killer, SideOf(w, target), target.StartsWith('F'));
        if (!target.StartsWith('F') || Name.Match(killer) is not { Success: true } m || m.Groups[1].Value != "F") return;
        if (w.Flights.FirstOrDefault(f => f.Id == int.Parse(m.Groups[2].Value)) is not { } f) return;
        var q = Air.SqnOf(w, f);
        int pi = Pilot(w, killer);
        if (pi >= 0 && pi < q.Pilots.Count && Career.In(w, killer) == null) Air.Credit(w, q, pi);
        f.Result += " Kill in DCS.";
        Deb(w, $"{Who(w, killer)} kills {Who(w, target)} ({weapon})");
    }

    /// Einheit/Gebäude/Maschine in DCS zerstört -> Kampagne. Unbekannte Namen = Landschaftsobjekte (Brücken).
    static void Lost(World w, string name, double x, double z)
    {
        if (!Note(w, "dead:" + name)) return;
        var m = Name.Match(name);
        if (!m.Success) { Scenery(w, x, z); return; }
        int id = int.Parse(m.Groups[2].Value), of = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
        string by = w.DcsNotes.FirstOrDefault(s => s.StartsWith($"kill:{name}="))?.Split('=', 2)[1] ?? "";
        string tail = by == "" ? "" : $" ({by})";
        switch (m.Groups[1].Value)
        {
            case "F" when w.Flights.FirstOrDefault(f => f.Id == id) is { } f:
                f.X = x; f.Z = z;
                bool? ejected = w.DcsNotes.Contains("pilotdead:" + name) ? false : w.DcsNotes.Contains("eject:" + name) ? true : null;
                var me = Career.In(w, name);
                string how = by == "" ? "lost in DCS" : $"shot down in DCS: {by}";
                Air.Loss(w, f, how, f.Side.Enemy(), Pilot(w, name), ejected, me);
                if (me != null) Career.Down(w, me, how, by != "");
                Deb(w, $"{f.Callsign} ({f.Type}) aircraft {m.Groups[3].Value} lost{tail}");
                break;
            case "U" when w.Units.FirstOrDefault(u => u.Id == id) is { } u:
                double before = u.Str;
                Sim.Hit(w, u, 0.5 / Math.Max(1, u.Veh));             // DCS-Fahrzeug = Kampfgruppe des Bataillons
                Deb(w, $"{u.Name} ({u.Side.De()}): vehicle destroyed, strength {before:P0} → {Math.Max(0, u.Str):P0}{tail}");
                break;
            case "O" when w.Objs.FirstOrDefault(o => o.Id == id) is { } o:
                double h0 = o.Health;
                string role = m.Groups[5].Value;
                string bld = "building";
                if (of > 0 && Scenario.Parts.TryGetValue(o.Kind, out var pl))   // Gebäude → Teil (Piste, Tanklager …)
                {
                    int pi = Scenario.PartAt(o.Kind, int.Parse(m.Groups[3].Value) - 1);
                    bld = "building, " + Air.HitPart(w, o, pi, 1.0 / Math.Max(1, pl[pi].Types.Length));
                }
                else if (of > 0) o.Health = Math.Max(0, o.Health - 1.0 / of);
                else
                {
                    o.Health = Math.Max(0, o.Health - (role == "r" || o.Kind == OK.Tel ? 0.5 : role == "l" ? 0.15 : 0.1));
                    if (role == "l" && Scenario.Sams.TryGetValue(o.Sys, out var sam))
                        o.Missiles = Math.Max(0, o.Missiles - sam.Missiles / Math.Max(1, sam.Parts.Where(p => Miz.Role(p.T) == "l").Sum(p => p.N)));
                    if (o.Health < 0.3) o.Missiles = 0;
                }
                w.Intel(o.Side.Enemy())[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = "DCS sortie", Str = o.Health, Claim = true };   // bestätigt erst die Aufklärung
                Deb(w, $"{o.Name}: {(of > 0 ? bld : role == "r" ? "radar" : role == "l" ? "launcher" : "vehicle")} destroyed, condition {h0:P0} → {o.Health:P0}{tail}");
                break;
            case "C" when w.Convoys.FirstOrDefault(c => c.Id == id) is { } cv:
                int k = Math.Min(cv.Trucks, Math.Max(1, (int)Math.Round((double)cv.Trucks / Math.Max(1, of))));
                cv.Tons *= 1 - (double)k / Math.Max(1, cv.Trucks); cv.Fuel *= 1 - (double)k / Math.Max(1, cv.Trucks);
                cv.Trucks -= k; cv.Missiles = cv.Missiles * cv.Trucks / (cv.Trucks + k);
                if (cv.Trucks <= 0) w.Convoys.Remove(cv);
                if (cv.Navy) w.Log(Side.None, "see", $"{cv.Name}: {k} ship(s) sunk in combat.", cv.X, cv.Z);
                Deb(w, cv.Navy ? $"{cv.Name}: {k} ship(s) sunk{tail}" : $"{Sim.Kind(cv).What} to {Sim.ObjName(w, cv.To)}: {k} {Sim.Kind(cv).Items} destroyed{tail}");
                break;
            case "Q" when w.Sqns.FirstOrDefault(q => q.Id == id) is { } q:
                if (q.Ac > 0) { q.Ac--; q.Lost++; }
                Deb(w, $"{q.Name}: {q.Type} destroyed on the ground{tail}");
                break;
        }
    }

    static void Scenery(World w, double x, double z)
    {
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Bridge && o.Health >= 0.3 && Geo.Dist(o.X, o.Z, x, z) < 600))
        {
            o.Health = 0.1;
            Deb(w, $"{o.Name} collapsed");
            w.Log(Side.None, "logistik", $"{o.Name} destroyed – supplies must be rerouted.", o.X, o.Z);
        }
    }

    /// Einschlag von Bomben/Raketen: Brücken (Lage aus Kartendaten, daher 600 m) und Pisten.
    static void Impact(World w, string weapon, double x, double z, double kg)
    {
        if (kg <= 0) kg = 40;
        foreach (var o in w.Objs.Where(o => o.Kind is OK.Bridge or OK.Airbase && o.Health > 0 && Geo.Dist(o.X, o.Z, x, z) < (o.Kind == OK.Bridge ? 600 : 2500)))
        {
            if (o.Kind == OK.Airbase)                                         // nur Treffer auf der Bahn; Lager/Tanks zählen als Gebäude (DEAD)
            {
                if (w.Th.Bases.FirstOrDefault(b => b.Id == o.BaseId) is { } rb && OnRunway(rb, x, z)) Deb(w, $"{o.Name}: runway hit by {weapon} – {Air.HitPart(w, o, 0, kg / 1200)}");
                continue;
            }
            double h0 = o.Health;
            o.Health = Math.Max(0, o.Health - kg / (o.Kind == OK.Bridge ? 700 : 2500));
            if (o.Kind == OK.Airbase) { o.Stock *= 1 - Math.Min(0.05, kg / 10000); o.Fuel *= 1 - Math.Min(0.08, kg / 6000); }
            Deb(w, $"{o.Name}: hit by {weapon}, condition {h0:P0} → {o.Health:P0}");
            if (o.Kind == OK.Bridge && o.Health < 0.3 && h0 >= 0.3) w.Log(Side.None, "logistik", $"{o.Name} destroyed – supplies must be rerouted.", o.X, o.Z);
        }
    }

    /// Punkt auf der Startbahn (±100 m quer, ±1700 m längs um den Platzbezugspunkt)
    static bool OnRunway(Base b, double x, double z)
    {
        double d = Geo.Dist(b.X, b.Z, x, z), a = (Geo.Brg(new P(b.X, b.Z), new P(x, z)) - b.RwyHdg) * Math.PI / 180;
        return Math.Abs(d * Math.Sin(a)) < 100 && Math.Abs(d * Math.Cos(a)) < 1700;
    }

    /// Landung eines Hubschraubers im Gelände (true): eigener abgeschossener Pilot bis 500 m und Kabine an Bord → gerettet.
    static bool Field(World w, string unit, double x, double z)
    {
        if (Name.Match(unit) is not { Success: true } m || m.Groups[1].Value != "F" || w.Flights.FirstOrDefault(f => f.Id == int.Parse(m.Groups[2].Value)) is not { } f || !Air.Helo(f.Type)) return false;
        Deb(w, $"{f.Callsign} aircraft {m.Groups[3].Value} lands off-field");
        if (!Air.Cabin(f.Type) || w.Downed.Where(d => d.Side == f.Side).MinBy(d => Geo.Dist(d.X, d.Z, x, z)) is not { } d || Geo.Dist(d.X, d.Z, x, z) > 500) return true;
        var name = Air.PilotOf(w, d).Name;
        f.Result += $" {name} picked up.";
        Air.Saved(w, d, $"CSAR: {f.Callsign} ({f.Type}) lands next to {name} and picks him up.");
        Deb(w, $"{f.Callsign}: {name} on board");
        return true;
    }

    static void Landed(World w, string unit, string place)
    {
        Career.Land(w, unit, place);
        if (!Note(w, "land:" + unit) || Name.Match(unit) is not { Success: true } m || m.Groups[1].Value != "F") return;
        if (w.Flights.FirstOrDefault(f => f.Id == int.Parse(m.Groups[2].Value)) is not { Done: false } f) return;
        Deb(w, $"{f.Callsign} aircraft {m.Groups[3].Value} landed{(place == "" ? "" : " at " + place)}");
        if (w.DcsNotes.Count(s => s.StartsWith($"land:F{f.Id}-")) < f.Alive) return;
        if (f.Route.Any(r => r.Kind == "target" && r.T <= w.Time)) f.Struck = true;
        Air.LandFlight(w, f);
    }

    /// Nachbesprechung wie BMS: Ablauf je Flug (Meldungen mit Rufname oder Ziel) und Bewertung nach Auftrag, Ziel vorher/nachher, Verlusten
    static Rating Rate(World w, Flight f, double t0)
    {
        int Fid(string s) => int.TryParse(s.Split(':')[1].Split('-')[0][1..], out var i) ? i : -1;
        string? tn = f.TargetKind == "obj" ? w.Objs.FirstOrDefault(o => o.Id == f.Target)?.Name : f.TargetKind == "unit" ? w.Units.FirstOrDefault(u => u.Id == f.Target)?.Name : f.TargetKind == "convoy" ? Air.TargetName(w, "convoy", f.Target) : null;
        double t1 = Air.TgtState(w, f) ?? (f.TargetKind == "convoy" ? 0 : -1), dmg = t0 >= 0 && t1 >= 0 ? t0 - t1 : 0;
        int lost = w.DcsNotes.Count(s => s.StartsWith($"dead:F{f.Id}-")), air = w.DcsNotes.Count(s => s.StartsWith($"score:F{f.Id}:F")), gnd = w.DcsNotes.Count(s => s.StartsWith($"score:F{f.Id}:")) - air;
        int pk = w.DcsNotes.Count(s => s.StartsWith("dead:F") && w.Flights.FirstOrDefault(x => x.Id == Fid(s))?.Pkg == f.Pkg) - lost;   // Verluste der anderen Flüge im Paket
        string Tg() => $"{tn}: {t0:P0} → {t1:P0}{(gnd > 0 ? $", {gnd} ground kill(s)" : "")}";
        var (g, why) = f.Status == "gestrichen" ? (2, f.Result == "" ? "not flown" : f.Result) : f.Task switch
        {
            "CAP" or "Escort" or "Sweep" => (pk == 0 ? 0 : 1, (pk == 0 ? "package unharmed" : $"package lost {pk} aircraft") + (air > 0 ? $", {air} air kill(s)" : "")),
            "SEAD" or "Jammer" => (pk == 0 ? 0 : 1, pk == 0 ? "package got through without losses" : $"package lost {pk} aircraft"),
            "Recon" => f.Result.StartsWith("Photos") ? (0, f.Result) : (2, "no photos of the target"),
            _ when t0 >= 0 && tn != null => t1 <= 0.3 || dmg >= 0.4 ? (0, Tg()) : dmg >= 0.1 || gnd > 0 ? (1, Tg()) : (2, $"{tn} not hit ({t1:P0})"),
            _ => (0, f.Result == "" ? "task flown" : f.Result.Trim()),
        };
        if (lost > 0 && lost >= f.N) (g, why) = (2, why + " – flight lost");
        else if (lost > 0) (g, why) = (Math.Max(g, 1), why + $" – {lost} aircraft lost");
        var cs = new System.Text.RegularExpressions.Regex($@"(?<!\w){System.Text.RegularExpressions.Regex.Escape(f.Callsign)}(?!\d)");
        return new Rating { Flight = f.Id, Grade = g, Lost = lost, Air = air, Ground = gnd, Player = f.Player, Callsign = f.Callsign, Type = f.Type, Task = f.Task, Owner = f.Owner, Why = why,
            Log = w.Debrief.Where(l => cs.IsMatch(l) || tn != null && l.Contains(tn)).ToList() };
    }

    /// Mission vorbei (DCS meldet Ende oder der Spieler beendet sie): alles zurück in die Kampagne.
    public static void End(World w, string why)
    {
        if (w.Mission == "") return;
        Career.End(w);
        if (w.Th.Parking.Count == 0) w.Th.LearnAirfields();                   // erste Mission auf einer neuen Karte: ab jetzt Start vom Abstellplatz
        bool live = w.MissionLive;
        foreach (var f in w.Flights.Where(f => f.InDcs))
        {
            f.InDcs = false;
            if (f.Done) continue;
            int up = !live ? 0 : w.DcsNotes.Count(s => s.StartsWith($"takeoff:F{f.Id}-") && !w.DcsNotes.Contains("dead:" + s[8..]));
            if (f.Player && up == 0)
            {
                var q = Air.SqnOf(w, f);
                q.Ac += f.Alive;                                 // nie abgehoben: Maschinen zurück in die Staffel
                if (!w.Mp && f.Takeoff > w.Time) { foreach (var i in f.Pilots) q.Pilots[i].Sorties--; f.Pilots.Clear(); f.Status = "geplant"; continue; }   // vor der Startzeit abgebrochen: bleibt geplant, gleich nochmal fliegen
                f.Status = "gestrichen"; f.Done = true; f.Result = live ? "Player did not take off" : "Mission not flown";
                continue;
            }
            if (f.Player && up < f.Alive) { Air.SqnOf(w, f).Ac += f.Alive - up; f.Alive = up; }   // Mehrspieler: freie Plätze nicht geflogen, zurück in die Staffel
            if (f.Player && w.DcsNotes.Count(s => s.StartsWith($"land:F{f.Id}-")) >= f.Alive) { Air.LandFlight(w, f); continue; }
            if (!live) continue;
            if (f.Route.Any(r => r.Kind == "target" && r.T <= w.Time)) f.Struck = true;   // was in DCS geschah, nicht noch einmal abstrakt auswerten
            if (f.Task == "CAS") f.Passes = 2;
            if (f.Struck && f.Status == "in der Luft") f.Status = "Rückflug";
        }
        w.Sorties = w.DcsNotes.Where(s => s.StartsWith("tgt:F")).Select(s => (Id: int.Parse(s[5..s.IndexOf('=')]), T0: double.Parse(s[(s.IndexOf('=') + 1)..], System.Globalization.CultureInfo.InvariantCulture)))
            .Select(x => (F: w.Flights.FirstOrDefault(f => f.Id == x.Id), x.T0)).Where(x => x.F is { Status: not "geplant" }).Select(x => Rate(w, x.F!, x.T0)).OrderByDescending(r => r.Player).ToList();
        if (w.Sorties.Where(r => r.Player).MinBy(r => r.Grade) is { } best)            // Zeitung: der Einsatz der Spieler
            Sim.News(w, best.Grade switch { 0 => $"{best.Task} mission a success: {best.Callsign} returns", 1 => $"Mixed results for {best.Callsign}", _ => $"{best.Callsign}'s {best.Task} mission fails" }, best.Why);
        foreach (var u in w.Units) u.InDcs = false;
        foreach (var o in w.Objs) o.InDcs = false;
        foreach (var c in w.Convoys) c.InDcs = false;
        Stocktake(w);
        w.Log(w.Player, "dcs", $"DCS mission ended ({why}): {(w.Debrief.Count == 0 ? "no reports" : $"{w.Debrief.Count} reports in the debrief")}.");
        w.Mission = ""; w.MissionLive = false;
        Miz.Cmds(true);
    }

    /// Lagerabgleich: was DCS über die gebuchten Starts hinaus aus dem Platzlager genommen hat (Nachtanken, Aufmunitionieren, Lagertreffer), fehlt auch der Kampagne.
    /// ponytail: nur Abzug, nie Gutschrift; KI-Starts außerhalb der Bubble zählen mit als gebucht → eher zu wenig als doppelt abgezogen.
    static void Stocktake(World w)
    {
        foreach (var (id, s) in w.WhStart)
        {
            if (!w.WhLast.TryGetValue(id, out var l) || w.Base(id) is not { } o) continue;
            var u = w.WhUsed.GetValueOrDefault(id) ?? [];
            var gone = new Dictionary<string, double>();
            foreach (var (k, v) in s) if (l.TryGetValue(k.Split('@')[^1], out var n)) gone[k.Split('@')[0]] = gone.GetValueOrDefault(k.Split('@')[0]) + Math.Max(0, v - n);
            double fuel = Math.Max(0, gone.GetValueOrDefault("fuel") - u.GetValueOrDefault("fuel"));
            int arms = 0;
            foreach (var (c, g) in gone) if (c != "fuel" && (int)Math.Round(g - u.GetValueOrDefault(c)) is > 0 and var x) { var st = Air.Stores(w, o); st[c] = Math.Max(0, st.GetValueOrDefault(c) - x); arms += x; }
            o.Fuel = Math.Max(0, o.Fuel - fuel);
            if (fuel >= 0.1 || arms > 0) w.Log(o.Side, "dcs", $"{o.Name}: DCS used more than planned – {fuel:0.#} t fuel, {arms} weapons taken from stock.", o.X, o.Z);
        }
        w.WhStart = []; w.WhLast = []; w.WhUsed = [];
    }
}
