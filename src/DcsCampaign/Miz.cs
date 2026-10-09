using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DcsCampaign;

/// Missionsgenerator: übernommener Flug + alles Relevante entlang seiner Route als .miz (Format des Missionseditors, version 20).
/// Namen für den Rückkanal (Bridge): Flug "F{id} Rufname", Maschine "F{id}-n", Verband "U{id}-n", Stellung "O{id}-n-r|l|x"
/// (Radar/Starter/sonst), Gebäude "O{id}-k/Anzahl", Konvoi "C{id}-k/Anzahl", abgestellte Flugzeuge "Q{staffel}-k".
static class Miz
{
    public static string Dir => Program.Dev != null ? Path.Combine(Program.Dev, "Missions") : Path.Combine(Program.SavedGames, "Missions", "Kampagne");

    // Bubble um den Spieler (Kalibrierknöpfe): Fahrplanfenster h, Luft 100 NM, Boden 40 NM, Obergrenze aktiver Einheiten (Leistungstest in DCS)
    const int Window = 8;
    const double AirR = 185200, MidR = 111120, HeloR = 55560, GroundR = 55560;   // 100 / 60 / 30 / 30 NM
    static readonly int[] MaxGround = [150, 300, 500], MaxAir = [24, 40, 64];    // gleichzeitig in DCS je Leistung (World.Perf): Fahrzeuge/Schiffe, KI-Maschinen

    // Sprit (kg, voll intern), Düppel, Fackeln
    public static readonly Dictionary<string, (double Fuel, int Chaff, int Flare)> Tanks = new()
    {
        ["FA-18C_hornet"] = (4900, 60, 60), ["F-14B"] = (7348, 140, 60), ["F-15ESE"] = (10245, 120, 60), ["F-16C_50"] = (3249, 60, 60),
        ["F-15C"] = (6103, 120, 60), ["A-10C_2"] = (5029, 240, 120), ["Su-25T"] = (3790, 128, 128), ["Su-25"] = (2835, 128, 128),
        ["Su-27"] = (9400, 96, 96), ["MiG-29S"] = (3500, 30, 30), ["Su-30"] = (9400, 96, 96), ["MiG-31"] = (15500, 0, 0),
        ["Su-24M"] = (11700, 96, 96), ["Su-34"] = (12100, 96, 96), ["Tu-22M3"] = (50000, 48, 48),
        ["A-50"] = (70000, 192, 192), ["E-3A"] = (65000, 120, 60), ["KC135MPRS"] = (90700, 120, 60), ["KC-135"] = (90700, 120, 60),
        ["C-130J-30"] = (19692, 120, 120), ["C-130"] = (20830, 120, 60), ["C-17A"] = (132405, 120, 60), ["IL-76MD"] = (80000, 96, 96), ["An-26B"] = (5500, 0, 96),
        ["AH-64D_BLK_II"] = (1140, 30, 60), ["Mi-24P"] = (1701, 0, 192), ["AH-1W"] = (1250, 30, 30), ["UH-1H"] = (631, 0, 60), ["Mi-8MT"] = (1929, 0, 128),
        ["F-14A-135-GR"] = (7348, 140, 60), ["F-14A-135-GR-Early"] = (7348, 140, 60), ["F-14A-95-GR"] = (7348, 140, 60), ["F-14BU"] = (7348, 140, 60),
        ["F-4E-45MC"] = (5510, 120, 30), ["AV8BNA"] = (3519, 120, 60), ["M-2000C"] = (3165, 234, 64), ["JF-17"] = (2325, 36, 32), ["Mirage-F1EE"] = (3246, 30, 15), ["Mirage-F1CE"] = (3246, 30, 15), ["Mirage-F1M-EE"] = (3246, 30, 15),
        ["MiG-21Bis"] = (2280, 18, 32), ["AJS37"] = (4476, 0, 0), ["MiG-29 Fulcrum"] = (3376, 30, 30), ["A-10C"] = (5029, 240, 120), ["Su-33"] = (9500, 96, 96), ["Ka-50"] = (1450, 0, 128), ["Ka-50_3"] = (1450, 0, 128),
        ["SA342M"] = (416, 0, 0), ["SA342L"] = (416, 0, 0), ["SA342Mistral"] = (416, 0, 0), ["SA342Minigun"] = (416, 0, 0), ["OH58D"] = (334, 0, 0), ["CH-47Fbl1"] = (3891, 120, 120),
        ["L-39ZA"] = (980, 0, 0), ["L-39C"] = (980, 0, 0), ["C-101CC"] = (1796, 0, 0), ["C-101EB"] = (1796, 0, 0), ["MB-339A"] = (626, 0, 0), ["MB-339APAN"] = (626, 0, 0), ["F-86F Sabre"] = (1282, 0, 0), ["MiG-15bis"] = (1172, 0, 0), ["MiG-19P"] = (1800, 0, 0), ["F-4E"] = (5510, 120, 60), ["MiG-29A"] = (3376, 30, 30), ["MiG-29G"] = (3376, 30, 30), ["A-10A"] = (5029, 240, 120), ["J-11A"] = (9400, 96, 96), ["F-5E-3"] = (2046, 30, 15), ["Mirage 2000-5"] = (3160, 112, 16),
    };
    /// DCS-Länder-Ids (Scripts\Database\db_countries.lua)
    static readonly Dictionary<string, int> Countries = new() { ["Russia"] = 0, ["USA"] = 2, ["Georgia"] = 16, ["Iran"] = 34, ["Oman"] = 73, ["United Arab Emirates"] = 74, ["Syria"] = 47, ["Israel"] = 15, ["Turkey"] = 3, ["Jordan"] = 59, ["UK"] = 4 };
    static readonly Dictionary<string, string> TplCountry = new() { ["RU"] = "Russia", ["GE"] = "Georgia", ["IR"] = "Iran", ["AE"] = "United Arab Emirates", ["SY"] = "Syria", ["IL"] = "Israel", ["TR"] = "Turkey", ["JO"] = "Jordan" };
    public static readonly string[] Skill = ["Average", "Good", "High", "Excellent"];
    static readonly string[] AwacsCs = ["Overlord", "Magic", "Wizard", "Focus", "Darkstar"], TankerCs = ["Texaco", "Arco", "Shell"];
    static readonly HashSet<string> Warehouse = [".Ammunition depot", "Warehouse", "Tank", "Tank 2", "Tank 3"];
    static readonly int[] PlaneSpots = [68, 72, 100, 104], HeloSpots = [40, 104];      // Term_Type: Shelter, Flugzeug, offen; Hubschrauber, offen
    static string Cat(string type) => Air.Helo(type) ? "helicopter" : "plane";
    static double Above(string type) => Air.Helo(type) ? 100 : 500;          // Erscheinen in der Luft: über dem Gelände

    sealed class Ctx(World w, Flight f, DateTime t0)
    {
        public readonly World W = w;
        public Flight F = f;                                                // Spielerflug, an dem gerade gebaut wird (Mehrspieler: einer nach dem anderen)
        public List<Flight> Mine = [f];                                     // alle Spielerflüge der Mission
        public readonly DateTime T0 = t0;
        public int Gid, Uid;
        public readonly List<(Side S, string Country, string Cat, Dictionary<string, object?> G)> Groups = [];
        public readonly Dictionary<string, int> Gids = [];                 // "F12", "U5", "O7", "C9" -> groupId (Ziele für KI-Aufträge)
        public List<(string Name, double X, double Z, double Alt, DateTime T)> Wps = [];            // Route von F wie in DCS (WP 0 = Start)
        public readonly List<string> Notes = [];
        public List<Flight> Ai = [];                                        // KI-Flüge in der Mission
        public readonly List<object?> Trains = [];                          // Güterzüge, die das Skript auf Gleise setzt
        public readonly List<object?> Spawn = [];                           // Fahrplan: Gruppen, die das Skript in der Bubble um den Spieler erzeugt
        public readonly List<object?> Smoke = [], Craters = [];             // Rauch (brennende Wracks, Depots, Kampf an der Front), Pistenkrater
        public string? Awacs;
        public int Cv;                                                      // unitId des Trägers (Start/Landung per linkUnit)                                               // eigenes AWACS: Lagebild per Funktext
        public readonly Dictionary<string, List<(string T, P P)>> Elems = [];   // Zielelemente je Stellung/Anlage an ihrem Platz in der Mission (Zielblatt)
        public readonly List<Dictionary<string, object?>> Seats = [];      // Menschen-Plätze von F (Kartusche)
        public double Sec(DateTime t) => Math.Round((t - T0).TotalSeconds);
        /// Fahrplan-Eintrag: erscheint, sobald seine Planposition näher als r am Spieler ist (dep = Ziel, das mit erscheint).
        public Dictionary<string, object?> Dyn(string country, string cat, Dictionary<string, object?> g, string key, double r, string? dep = null)
        {
            var e = D(("n", g["name"]), ("k", key), ("c", Countries[country]), ("cat", cat switch { "plane" => 0, "helicopter" => 1, "ship" => 3, _ => 2 }), ("r", Math.Round(r)), ("dep", dep), ("g", g));
            Spawn.Add(e);
            return e;
        }
        public void Add(Side s, string country, string cat, Dictionary<string, object?> g)
        {
            if (g.GetValueOrDefault("route") is Dictionary<string, object?> r && r.GetValueOrDefault("points") is List<object?> { Count: > 0 } p && p[0] is Dictionary<string, object?> first)
                first["ETA_locked"] = true;                                 // Missionseditor: erster Wegpunkt = Startzeit, fixiert (sonst „keine Wegpunkte mit fixierter Zeit“)
            Groups.Add((s, country, cat, g));
        }
    }

    static Dictionary<string, object?> D(params (string K, object? V)[] kv) { var d = new Dictionary<string, object?>(); foreach (var (k, v) in kv) d[k] = v; return d; }
    static List<object?> L(params object?[] items) => [.. items];

    /// Mission für den übernommenen Flug bauen; vorher bis zum Missionsstart vorspulen. Alte Kampagnenmissionen im Ordner werden gelöscht.
    public static object Fly(World w, string? dir = null)
    {
        if (w.Mission != "") throw new InvalidOperationException("A DCS mission is already running – end it first");
        var mine = w.Flights.Where(f => f.Player && !f.Done).Take(w.Mp ? 16 : 1).ToList();      // Mehrspieler: alle gewählten Flüge in einer Mission
        if (mine.Count == 0) throw new ArgumentException("Take over a flight first");
        var starts = mine.ToDictionary(f => f, f => f.Status == "geplant" ? Fit(f, f.Start, w.Time) : "Luft");   // zu knapp für Kaltstart: nächstschnellere Startart
        var late = mine.Where(m => m.Status == "geplant" && starts[m] != m.Start)
            .Select(m => $"{m.Callsign}: takeoff {m.Takeoff:HH:mm} – too late for a {En.Of(m.Start)} start, {En.Of(starts[m])} instead (move the TOT for more time).").ToList();
        DateTime T(Flight f) => StartAt(f, starts[f], w.Time);
        var t0 = mine.Min(T);                                             // Mission beginnt mit dem frühesten Flug
        if (t0 < w.Time) t0 = w.Time;
        t0 = t0.AddTicks(-(t0.Ticks % TimeSpan.TicksPerMinute));
        foreach (var m in mine) m.InDcs = true;                           // beim Vorspulen nicht abstrakt weiterfliegen
        try { Sim.Advance(w, t0); } finally { foreach (var m in mine) m.InDcs = false; }
        foreach (var m in mine)
        {
            if (m.Done || m.Status == "gestrichen") throw new ArgumentException($"{m.Callsign} has been cancelled in the meantime: " + m.Result);
            if (starts[m] == "Luft" && T(m) > t0.AddMinutes(10)) starts[m] = "kalt";   // startet deutlich später: wartet am Platz
            if (m.Status == "geplant" && !Air.Launch(w, m)) throw new ArgumentException($"{m.Callsign} cannot launch: " + m.Result);
        }
        var f = mine[0];

        // Fahrplan für 8 h: KI-Flüge, die der Spielerroute auf Luft-Bubble-Weite nahekommen, Bodentruppen/Stellungen/Konvois im Umkreis.
        // Das Missionsskript erzeugt sie erst in der Bubble um den Spieler (DCS meldet SPAWN/OUT); bis dahin rechnet die Kampagne sie abstrakt.
        var x = new Ctx(w, f, t0) { Mine = mine };
        x.Notes.AddRange(late);
        var lines = mine.Select(m => m.Route.Select(r => new P(r.X, r.Z)).ToList()).ToList();
        var end = t0.AddHours(Window);
        double Near(Flight c)
        {
            double m = double.MaxValue;
            for (var t = t0; t <= end; t = t.AddMinutes(3))
                if (t >= c.Takeoff && t <= c.Land) { var a = Air.PosAt(c, t); foreach (var o in mine) { var b = Air.PosAt(o, t); m = Math.Min(m, Geo.Dist(a.X, a.Z, b.X, b.Z)); } }
            return m;
        }
        var ai = w.Flights.Where(c => !mine.Contains(c) && !c.Done && c.Status != "gestrichen" && c.Land > t0.AddMinutes(10) && c.Takeoff < end)
            .Select(c => (c, d: Near(c))).Select(t => (t.c, t.d, p: mine.Any(m => m.Pkg == t.c.Pkg) ? 0 : t.c.Side == f.Side && t.c.Task is "AWACS" or "Tanker" ? 1
                : t.c.Side != f.Side && t.c.Task is "CAP" or "Sweep" or "Escort" && t.d < AirR ? 2 : t.d < AirR ? 3 : 9))
            .Where(t => t.p < 9).OrderBy(t => t.p).ThenBy(t => t.d).Take(80).Select(t => t.c).ToList();
        x.Ai = ai;

        var hot = ai.Concat(mine).Where(c => c.Target >= 0).Select(c => (c.TargetKind, c.Target)).ToHashSet();
        double Dist(double px, double pz) { var p = new P(px, pz); double m = double.MaxValue; foreach (var line in lines) for (int i = 1; i < line.Count; i++) m = Math.Min(m, Geo.SegDist(p, line[i - 1], line[i])); return m; }
        var units = w.Units.Where(u => Dist(u.X, u.Z) < GroundR + 25000 || hot.Contains(("unit", u.Id)) || hot.Contains(("node", u.Node))).ToList();
        var sites = w.Objs.Where(o => o.Kind is OK.Sam or OK.Ewr or OK.Tel && o.Health > 0.3 && (Dist(o.X, o.Z) < GroundR + 25000 + (o.Kind == OK.Sam ? o.Range : 0) || hot.Contains(("obj", o.Id)))).ToList();
        var objs = w.Objs.Where(o => Scenario.Statics.ContainsKey(o.Kind) && !(o.Kind == OK.Airbase && o.BaseId <= 0) && (Dist(o.X, o.Z) < 25000 || hot.Contains(("obj", o.Id)))).ToList();
        var convoys = w.Convoys.Where(c => Dist(c.X, c.Z) < GroundR + 25000 + Sim.NavyRange(c) || hot.Contains(("convoy", c.Id))).ToList();
        var parked = w.Objs.Where(o => o.Kind == OK.Airbase && o.Side != f.Side && hot.Contains(("obj", o.Id))).ToList();

        foreach (var m in mine) x.Gids[$"F{m.Id}"] = ++x.Gid;
        Carrier(x);
        var pages = new List<Page>();
        foreach (var m in mine)                                           // je Spielerflug: Gruppe, Briefing, Kniebrett, Kartusche
        {
            x.F = m; x.Wps = []; x.Seats.Clear();
            var (hb, st) = Player(x, starts[m]);
            var (brief, task, card, data, lineup) = Brief(x, st, hb);
            string? dn = null, dtc = null;
            if (m.Type == "FA-18C_hornet" && Dtc(x) is { } dj)
            {
                dn = pages.Any(o => o.DtcName != null) ? $"Kampagne-F{m.Id}" : "Kampagne";       // ponytail: weitere Hornet-Flüge eigene Kartusche, in DCS noch nicht erprobt
                dtc = dj.Replace("\"name\": \"Kampagne\"", $"\"name\": \"{dn}\"");
                foreach (var u in x.Seats) u["DTC"] = D(("AutoLoad", true), ("Cartridges", L(D(("name", dn), ("default", true)))));
            }
            pages.Add(new(m, hb, st, brief, task, card, data, lineup, MapPage(x), dn, dtc));
        }
        x.F = f;
        Build(x, ai, units, sites, convoys);
        Facs(x);
        int li = 0;
        foreach (var p in Sim.Line(w).Where(p => Dist(p.X, p.Z) < GroundR + 25000)) Post(x, p, ++li);
        foreach (var o in objs) Buildings(x, o);
        foreach (var o in parked) Parked(x, o);

        // Spuren des Krieges: Wracks (bleiben liegen), Rauch, Pistenkrater auf beschädigten Plätzen (nicht am eigenen Startplatz)
        int wi = 0;
        foreach (var k in w.Wrecks.Where(k => Dist(k.X, k.Z) < GroundR && !k.Type.Contains("manpad")).TakeLast(50))
            x.Add(k.Side, k.Side == Side.Red ? w.S.RedC : w.S.BlueC, "static", Static(x, $"W{++wi}", k.Type, StaticCat(k.Type), new P(k.X, k.Z), k.Hdg * Math.PI / 180, true, false));
        foreach (var k in w.Wrecks.Where(k => k.T > t0.AddHours(-3) && Dist(k.X, k.Z) < GroundR).TakeLast(8)) x.Smoke.Add(D(("x", Math.Round(k.X)), ("z", Math.Round(k.Z)), ("p", 1)));
        foreach (var o in w.Objs.Where(o => o.Kind is OK.Depot or OK.Factory or OK.Port && o.Health < 0.5 && Dist(o.X, o.Z) < GroundR).Take(4))
            x.Smoke.Add(D(("x", Math.Round(o.X)), ("z", Math.Round(o.Z)), ("p", o.Health < 0.25 ? 3 : 6)));
        foreach (var a in w.Axes.Where(a => a.Op == "Angriff"))
            if (Sim.Front(w, a) is var (rf, bf))
                foreach (var n in new[] { w.Nodes[rf], w.Nodes[bf] }.Where(n => Dist(n.X, n.Z) < GroundR)) x.Smoke.Add(D(("x", Math.Round(n.X + 800)), ("z", Math.Round(n.Z)), ("p", 7)));
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Airbase && o.BaseId > 0 && o.P("Runway") < 0.7 && !pages.Any(pg => pg.Home.Id == o.BaseId) && Dist(o.X, o.Z) < GroundR + 50000))
        {
            var b = w.Th.Bases.First(b => b.Id == o.BaseId);
            int n = Math.Min(5, (int)Math.Ceiling((0.7 - o.P("Runway")) * 8));
            for (int i = 0; i < n; i++) { var p = Geo.Move(new P(b.X, b.Z), b.RwyHdg, (i - (n - 1) / 2.0) * 300); x.Craters.Add(D(("x", Math.Round(p.X)), ("z", Math.Round(p.Z)))); }
        }
        x.Awacs = Atc.Exe != null ? null                                   // mit DCS-ATC spricht die das AWACS, kein Textbild der Kampagne
            : w.Flights.FirstOrDefault(c => c.Side == f.Side && c.Task == "AWACS" && !c.Done && c.Status != "gestrichen" && c.Land > t0)?.Callsign;

        var token = Guid.NewGuid().ToString("N")[..10];
        dir ??= Dir;
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "*.miz")) File.Delete(old);      // nur die aktuelle Kampagnenmission behalten
        var path = Path.Combine(dir, $"Kampagne {t0:yyyy-MM-dd HHmm} {f.Callsign}{(mine.Count > 1 ? $" +{mine.Count - 1}" : "")}.miz");
        WriteMiz(x, token, path, pages);

        foreach (var m in mine) m.InDcs = true;                           // Fahrplan-Gruppen erst mit SPAWN aus DCS
        foreach (var o in objs) o.InDcs = true;
        (w.MissionGids, w.MissionGid, w.MissionUid, w.MissionSeq, w.MissionMsg) = (x.Gids, x.Gid, x.Uid, 0, t0);
        Cmds(true);
        w.Mission = token; w.MissionStart = t0; w.MissionLive = false; w.MissionFile = path; w.Speed = 0;
        w.DcsNotes.Clear(); w.Debrief.Clear(); w.Sorties.Clear();
        foreach (var pf in w.Flights.Where(x => mine.Any(m => m.Pkg == x.Pkg)))   // Nachbesprechung: Zustand der Ziele der Spielerpakete vorher
            w.DcsNotes.Add($"tgt:F{pf.Id}={(Air.TgtState(w, pf) ?? -1).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
        foreach (var c in mine.Concat(ai)) for (int n = 1; n <= c.Pilots.Count; n++) w.DcsNotes.Add($"pilot:F{c.Id}-{n}={c.Pilots[n - 1]}");
        foreach (var pg in pages.Where(pg => pg.Start == "Luft")) for (int n = 1; n <= pg.F.Alive; n++) w.DcsNotes.Add($"takeoff:F{pg.F.Id}-{n}");
        if (w.Mp) Cmd("load.miz", path);                                  // Mehrspieler: der Hook lädt die Mission auf dem laufenden Server
        w.Log(w.Player, "dcs", $"DCS mission created: {Path.GetFileName(path)} – start {t0:HH:mm}{(mine.Count > 1 ? $", {mine.Count} player flights" : "")}, schedule {Window} h: {ai.Count} flights, {units.Count} units, {sites.Count} sites, {convoys.Count} transports. Load it in DCS and fly.");
        return new { File = path, Start = t0, x.Notes, Flights = ai.Count, Units = units.Count, Sites = sites.Count };
    }

    /// Missionsbeginn für einen Flug je Startart
    static DateTime StartAt(Flight f, string start, DateTime now) => start switch
    {
        "kalt" => f.Takeoff.AddMinutes(-25), "heiß" => f.Takeoff.AddMinutes(-10), "Startbahn" => f.Takeoff.AddMinutes(-2),
        _ => f.Status == "geplant" ? f.Takeoff.AddMinutes(8) : now,
    };

    /// Startart, für die die Zeit bis zum Start noch reicht (kalt 25 min, heiß 10, Startbahn 2), sonst die nächstschnellere
    public static string Fit(Flight f, string start, DateTime now) =>
        new[] { "kalt", "heiß", "Startbahn", "Luft" }.SkipWhile(s => s != start).FirstOrDefault(s => s == "Luft" || StartAt(f, s, now) >= now) ?? "Luft";

    record Page(Flight F, Base Home, string Start, string Brief, string Task, List<string> Card, List<string> Data, List<string> Lineup, byte[] Map, string? DtcName, string? Dtc);

    /// Fahrplan-Gruppen bauen. Gruppen-Ids vorab, denn Begleitschutz und Angriff verweisen darauf.
    static void Build(Ctx x, List<Flight> ai, List<Unit> units, List<Obj> sites, List<Convoy> convoys)
    {
        foreach (var k in ai.Select(c => $"F{c.Id}").Concat(units.Select(u => $"U{u.Id}")).Concat(sites.Select(o => $"O{o.Id}")).Concat(convoys.Select(c => $"C{c.Id}")))
            if (!x.Gids.ContainsKey(k)) x.Gids[k] = ++x.Gid;
        foreach (var c in ai) AiFlight(x, c);
        foreach (var u in units) Ground(x, u);
        foreach (var o in sites) Site(x, o);
        foreach (var c in convoys) Convoy(x, c);
    }

    // ---------- Live-Befehle (Hook) ----------
    // Die App legt Lua-Befehle in %TEMP%\DcsCampaign-Cmd ab, der Hook Saved Games\DCS\Scripts\Hooks\DcsCampaignHook.lua führt sie im Missionsskript aus
    // (DCSCAMP.cmd). Ohne Hook bleibt es beim Fahrplan aus der .miz; nach 5 min ohne Quittung (CMD) legt die App nichts mehr ab.

    public static string CmdDir = Program.Dev != null ? Path.Combine(Program.Dev, "Cmd") : Path.Combine(Path.GetTempPath(), "DcsCampaign-Cmd");
    public static void Cmds(bool clear)
    {
        Directory.CreateDirectory(CmdDir);
        if (clear) foreach (var f in Directory.GetFiles(CmdDir)) File.Delete(f);
    }
    /// Befehl an den Hook: name.lua läuft im Missionsskript, load.miz enthält den Pfad der Mission, die der Server laden soll.
    public static void Cmd(string name, string code)
    {
        Cmds(false);
        var file = Path.Combine(CmdDir, name);
        File.WriteAllText(file + ".tmp", code);
        File.Move(file + ".tmp", file, true);
    }

    /// Jede Minute (T aus DCS): neue Flüge, Verbände, Stellungen und Transporte nahe dem Spieler nachschieben, erledigte entfernen.
    public static void Sync(World w)
    {
        var mine = w.Flights.Where(f => f.Player && f.InDcs).ToList();
        if (w.Mission == "" || !w.MissionLive || mine.Count == 0) return;
        var pf = mine[0];
        if (w.MissionSeq > 0 && w.Time > w.MissionStart.AddMinutes(5) && !w.DcsNotes.Contains("hook")) return;
        var x = new Ctx(w, pf, w.MissionStart) { Mine = mine, Gid = w.MissionGid, Uid = w.MissionUid };
        foreach (var (k, v) in w.MissionGids) x.Gids[k] = v;
        bool New(string k) => !x.Gids.ContainsKey(k);
        double Me(double px, double pz) => mine.Min(m => Geo.Dist(px, pz, m.X, m.Z));
        var ai = w.Flights.Where(c => !c.Done && c.Status != "gestrichen" && New($"F{c.Id}") && c.Takeoff < w.Time.AddHours(1) && c.Land > w.Time.AddMinutes(10)
            && c.Route.Any(r => Me(r.X, r.Z) < AirR + 50000)).ToList();
        var units = w.Units.Where(u => New($"U{u.Id}") && Me(u.X, u.Z) < GroundR + 25000).ToList();
        var sites = w.Objs.Where(o => o.Kind is OK.Sam or OK.Ewr or OK.Tel && o.Health > 0.3 && New($"O{o.Id}") && Me(o.X, o.Z) < GroundR + 25000 + (o.Kind == OK.Sam ? o.Range : 0)).ToList();
        var convoys = w.Convoys.Where(c => New($"C{c.Id}") && Me(c.X, c.Z) < GroundR + 25000 + Sim.NavyRange(c)).ToList();
        Build(x, ai, units, sites, convoys);
        Facs(x);
        foreach (var t in x.Trains.Cast<Dictionary<string, object?>>())
            if (x.Spawn.Cast<Dictionary<string, object?>>().FirstOrDefault(e => Equals(e["n"], t["g"])) is { } e) e["train"] = t;
        bool Gone(string k) => int.Parse(k[1..]) is var id && k[0] switch
        {
            'F' => w.Flights.FirstOrDefault(f => f.Id == id) is not { Done: false } f || f.Status == "gestrichen",
            'U' => !w.Units.Any(u => u.Id == id),
            'L' => false,                                               // Stützpunkt: bleibt bis Missionsende
            'O' => w.Objs.FirstOrDefault(o => o.Id == id) is not { Health: >= 0.3 },
            _ => !w.Convoys.Any(c => c.Id == id),
        };
        var del = w.MissionGids.Keys.Where(k => !mine.Any(m => k == $"F{m.Id}") && !w.DcsNotes.Contains("del:" + k) && Gone(k)).ToList();
        var msg = w.Events.Where(e => e.T > w.MissionMsg && (e.Side == pf.Side || e.Side == Side.None) && e.Kind is "front" or "logistik" or "lage")   // Lage im Funk
            .TakeLast(3).Select(e => (object?)$"SITREP {e.T:HH:mm}: {e.Text}").ToList();
        if (w.MissionSeq > 0 && x.Spawn.Count == 0 && del.Count == 0 && msg.Count == 0) return;
        (w.MissionGids, w.MissionGid, w.MissionUid, w.MissionMsg) = (x.Gids, x.Gid, x.Uid, w.Time);
        w.DcsNotes.AddRange(del.Select(k => "del:" + k));
        foreach (var c in ai) for (int n = 1; n <= c.Pilots.Count; n++) w.DcsNotes.Add($"pilot:F{c.Id}-{n}={c.Pilots[n - 1]}");
        var code = Lua.Write("DCSCAMP_CMD", D(("seq", ++w.MissionSeq), ("add", x.Spawn), ("del", del.Cast<object?>().ToList()), ("msg", msg)))
            + $"if DCSCAMP and DCSCAMP.token == {Lua.Quote(w.Mission)} and DCSCAMP.cmd then DCSCAMP.cmd(DCSCAMP_CMD) end\n";
        Cmd($"{w.MissionSeq:00000}.lua", code);
    }

    /// Flugzeugträger mit Geleit: läuft gegen den Wind hin und her (Wind über Deck), TACAN/ICLS, Supercarrier dazu Link 4 und ACLS, Funk 127.5.
    static void Carrier(Ctx x)
    {
        var w = x.W;
        if (w.Objs.FirstOrDefault(o => o.Kind == OK.Airbase && o.BaseId < 0) is not { } cv) return;
        var (name, ch, cs) = Scenario.Carriers[cv.Sys];
        var c = new P(cv.X, cv.Z);
        bool Open(double h) => Enumerable.Range(1, 20).All(i => Geo.Move(c, h, i * 2000) is var p && w.Th.Water(p.X, p.Z));
        double hdg = new double[] { 0, 30, -30, 60, -60, 90, -90, 180 }.Select(d => (w.Wx.WindDir + d + 360) % 360).FirstOrDefault(Open, w.Wx.WindDir);
        var up = Geo.Move(c, hdg, 40000);
        x.Cv = ++x.Uid;
        bool sc = cv.Sys.StartsWith("CVN_");
        var tasks = new List<Dictionary<string, object?>>
        {
            Wrapped("ActivateBeacon", D(("type", 4), ("system", 3), ("AA", false), ("unitId", x.Cv), ("modeChannel", "X"), ("channel", ch), ("callsign", cs), ("bearing", true), ("frequency", (1087.0 + ch) * 1e6))),
            Wrapped("ActivateICLS", D(("type", 131584), ("channel", 11), ("unitId", x.Cv))),
        };
        if (sc) { tasks.Add(Wrapped("ActivateLink4", D(("unitId", x.Cv), ("frequency", 336000000)))); tasks.Add(Wrapped("ActivateACLS", D(("unitId", x.Cv)))); }
        for (int i = 0; i < tasks.Count; i++) tasks[i]["number"] = i + 1;
        var pts = Enumerable.Range(0, 10).Select(i => (object?)D(("x", (i % 2 == 0 ? c : up).X), ("y", (i % 2 == 0 ? c : up).Z), ("alt", 0), ("alt_type", "BARO"), ("type", "Turning Point"), ("action", "Turning Point"),
            ("speed", 12.9), ("speed_locked", true), ("ETA", 0), ("ETA_locked", i == 0), ("name", ""), ("formation_template", ""),
            ("task", D(("id", "ComboTask"), ("params", D(("tasks", i == 0 ? tasks.Cast<object?>().ToList() : L()))))))).ToList();
        double rad = hdg * Math.PI / 180;
        object? Ship(string type, string n, int id, P p) => D(("type", type), ("unitId", id), ("name", n), ("skill", "Excellent"), ("x", p.X), ("y", p.Z), ("heading", rad),
            ("frequency", 127500000), ("modulation", 0), ("transportable", D(("randomTransportable", false))));
        var units = L(Ship(cv.Sys, name, x.Cv, c), Ship("TICONDEROG", $"{cs} Escort CG", ++x.Uid, Geo.Move(c, hdg + 120, 2000)), Ship("USS_Arleigh_Burke_IIa", $"{cs} Escort DDG", ++x.Uid, Geo.Move(c, hdg - 120, 2000)));
        x.Add(Side.Blue, "USA", "ship", D(("groupId", ++x.Gid), ("name", "CV " + name), ("hidden", false), ("visible", false), ("start_time", 0), ("tasks", D()),
            ("hiddenOnMFD", false), ("hiddenOnPlanner", false), ("lateActivation", false), ("uncontrolled", false), ("route", D(("points", pts))), ("units", units), ("x", c.X), ("y", c.Z)));
        if (x.Mine.Any(f => Home(w, f).Id < 0))
            x.Notes.Add($"Carrier {name}: TACAN {ch}X {cs}, ICLS 11{(sc ? ", Link 4 336.0, ACLS" : "")}, radio 127.5 AM, heading {hdg:000}° into the wind.");
    }

    // ---------- Flüge ----------

    static Base BaseAt(World w, double px, double pz) => w.Objs.FirstOrDefault(o => o.Kind == OK.Airbase && o.BaseId < 0 && Geo.Dist(o.X, o.Z, px, pz) < 5000) is { } cv
        ? new Base(-1, cv.Name, cv.X, cv.Z, 20, w.Wx.WindDir, "") : w.Th.Bases.MinBy(b => Geo.Dist(b.X, b.Z, px, pz))!;   // Id -1 = Träger
    static string DcsTask(string t) => t switch
    {
        "CAP" => "CAP", "Sweep" => "Fighter Sweep", "Escort" => "Escort", "CAS" => "CAS", "SEAD" or "Jammer" => "SEAD", "Recon" => "Reconnaissance", "AWACS" => "AWACS", "Tanker" => "Refueling", "OCA" => "Runway Attack", "Antiship" => "Antiship Strike", "CSAR" or "Transport" => "Transport", _ => "Ground Attack",
    };
    static string WpName(Wp r) => r.Kind switch { "push" => "PUSH", "ip" => "IP", "target" => r.Name switch { "Pilot" => "PILOT", "Anflug" => "APPROACH", _ => "TARGET" }, "egress" => "EGRESS", "orbit" => "STATION", "orbit2" => "STATION END", "land" => r.Name, _ => "" };

    static (Base, string) Player(Ctx x, string start)
    {
        var (w, f) = (x.W, x.F);
        var b = BaseAt(w, f.Route[0].X, f.Route[0].Z);
        var spots = new List<(double X, double Z, int Num, int Type)>();
        if (start is "kalt" or "heiß" && b.Id > 0)                         // Träger: DCS stellt aufs Deck
        {
            var all = w.Th.Parking.GetValueOrDefault(b.Id)?.Where(p => (Air.Helo(f.Type) ? HeloSpots : PlaneSpots).Contains(p.Type)).ToList() ?? [];
            if (all.Count >= f.Alive) { var s0 = all.MinBy(p => p.Num); spots = all.OrderBy(p => Geo.Dist(p.X, p.Z, s0.X, s0.Z)).Take(f.Alive).ToList(); }
            else { start = "Startbahn"; x.Notes.Add($"Parking spots at {b.Name} unknown (learned via DCS-ATC with the first mission on this map) – runway start."); }
        }
        double spd = Air.PerfOf(f.Type).Spd, hdg, alt;
        P lead;
        List<Wp> rest;
        var pts = new List<object?>();
        var ts = start != "Luft" && StartAt(f, start, x.T0) > x.T0.AddMinutes(10) ? StartAt(f, start, x.T0) : x.T0;   // Mehrspieler: später startender Flug erscheint erst zu seiner Zeit
        if (start == "Luft")
        {
            var pos = Air.PosAt(f, x.T0);
            rest = f.Route.Where(r => r.T > x.T0 && r.Kind != "start").ToList();
            if (rest.Count == 0) throw new ArgumentException("Flight is almost back already");
            lead = new P(pos.X, pos.Z);
            hdg = Geo.Brg(lead, new P(rest[0].X, rest[0].Z));
            alt = Math.Max(pos.Alt, w.Th.Peak(pos.X, pos.Z, 2000) + Above(f.Type));
            pts.Add(Pt(x, lead.X, lead.Z, alt, spd, "Turning Point", "Turning Point", "START", x.T0, []));
        }
        else
        {
            rest = f.Route.Skip(1).ToList();
            var (type, action) = start switch { "kalt" => ("TakeOffParking", "From Parking Area"), "heiß" => ("TakeOffParkingHot", "From Parking Area Hot"), _ => ("TakeOff", "From Runway") };
            lead = spots.Count > 0 ? new P(spots[0].X, spots[0].Z) : new P(b.X, b.Z);
            hdg = b.RwyHdg; alt = b.Elev; spd = 0;
            pts.Add(Pt(x, lead.X, lead.Z, b.Elev, 0, type, action, b.Name, ts, [], b.Id));
        }
        x.Wps.Add((start == "Luft" ? "Start (air)" : b.Name, lead.X, lead.Z, alt, start == "Luft" ? x.T0 : f.Takeoff));
        foreach (var r in rest) { pts.Add(Leg(x, r, WpName(r), [])); x.Wps.Add((WpName(r) is { Length: > 0 } n ? n : "", r.X, r.Z, r.Alt, r.T)); }
        var units = new List<object?>();
        for (int n = 1; n <= f.Alive; n++)
        {
            var p = start == "Luft" ? Wing(lead, hdg, n) : spots.Count >= n ? new P(spots[n - 1].X, spots[n - 1].Z) : lead;
            bool human = w.Mp ? (f.Crew == "" ? "P" : f.Crew).ElementAtOrDefault(n - 1) == 'P' : n == 1;   // Einzelspieler: nur Maschine 1
            var u = AirUnit(x, f, n, p, alt, hdg, spd, human ? (w.Mp ? "Client" : "Player") : PilotSkill(x, f, n));
            if (spots.Count >= n) u["parking"] = spots[n - 1].Num;
            else if (b.Id < 0 && start != "Luft") u["parking"] = u["parking_id"] = $"{n}";   // Träger: ohne Deckplatz setzt DCS die Maschine nicht hin (freie Kamera)
            u["Radio"] = Radios(w, f);
            if (human) x.Seats.Add(u);
            units.Add(u);
        }
        x.Add(f.Side, Air.SqnOf(w, f).Country, Cat(f.Type), Group(x, f, pts, units, x.Sec(ts)));
        return (b, start);
    }

    static P Wing(P lead, double hdg, int n) => n == 1 ? lead : Geo.Move(lead, hdg + 180 + (n % 2 == 0 ? -35 : 35), 250.0 * (n - 1));

    static void AiFlight(Ctx x, Flight c)
    {
        var w = x.W;
        var sp = c.Takeoff.AddMinutes(6) > x.T0 ? c.Takeoff.AddMinutes(6) : x.T0;   // später startende Flüge erscheinen kurz nach dem Start in der Luft
        var rest = c.Route.Where(r => r.T > sp && r.Kind != "start").ToList();
        if (rest.Count == 0) return;
        var pos = Air.PosAt(c, sp);
        var at = new P(pos.X, pos.Z);
        double alt = Math.Max(pos.Alt, w.Th.Peak(pos.X, pos.Z, 2000) + Above(c.Type)), spd = Air.PerfOf(c.Type).Spd, hdg = Geo.Brg(at, new P(rest[0].X, rest[0].Z));
        var units = new List<object?>();
        for (int n = 1; n <= c.Alive; n++) units.Add(AirUnit(x, c, n, Wing(at, hdg, n), alt, hdg, spd, PilotSkill(x, c, n)));
        var first = EnRoute(x, c, (int)((Dictionary<string, object?>)units[0]!)["unitId"]!);
        first.AddRange(Leash(c));
        var o1 = c.Route.FirstOrDefault(r => r.Kind == "orbit"); var o2 = c.Route.FirstOrDefault(r => r.Kind == "orbit2");
        if (o1 != null && o2 != null && sp >= o1.T && sp < o2.T) first.Add(Orbit(o1.Alt, spd, (o2.T - sp).TotalSeconds));   // schon auf Station
        var attack = Attack(x, c);
        if (attack != null && !rest.Any(r => r.Kind == "ip") && rest.Any(r => r.Kind == "target")) first.Add(attack);   // IP schon vorbei
        var pts = new List<object?> { Pt(x, at.X, at.Z, alt, spd, "Turning Point", "Turning Point", "", sp, first) };
        foreach (var r in rest)
        {
            var tasks = new List<Dictionary<string, object?>>();
            if (r.Kind == "orbit" && o2 != null) tasks.Add(Orbit(r.Alt, spd, (o2.T - r.T).TotalSeconds));
            if (r.Kind == "ip" && attack != null) tasks.Add(attack);
            pts.Add(Leg(x, r, "", tasks));
        }
        string? dep = c.TargetKind switch { "obj" => "O", "unit" => "U", "convoy" => "C", _ => null } is { } k && x.Gids.ContainsKey(k + c.Target) ? k + c.Target : null;
        x.Dyn(Air.SqnOf(w, c).Country, Cat(c.Type), Group(x, c, pts, units, 0), $"F{c.Id}", Bubble(x, c), dep);
    }

    /// Ab welcher Entfernung zum Spieler ein KI-Flug in DCS erscheint: feindliche Jäger (BVR, Radarbild), eigenes Paket und
    /// AWACS/Tanker früh, Hubschrauber spät (tief, nur nah zu sehen), alle anderen mittel.
    static double Bubble(Ctx x, Flight c) =>
        x.Mine.Any(m => m.Pkg == c.Pkg) || c.Side == x.F.Side && c.Task is "AWACS" or "Tanker" || c.Side != x.F.Side && c.Task is "CAP" or "Sweep" or "Escort" ? AirR
        : Air.Helo(c.Type) ? HeloR : MidR;

    static List<Dictionary<string, object?>> EnRoute(Ctx x, Flight c, int lead)
    {
        switch (c.Task)
        {
            case "CAP": return [Engage("CAP", 75000, "Air")];                 // nicht weit von der Station weg (SAM-Schirm)
            case "Sweep": return [Engage("CAP", 110000, "Air")];
            case "SEAD": return [Engage("SEAD", 90000, "Air Defence")];
            case "CAS":                                                        // wartet hinter der Linie: Angriffsradius bis über die Kontaktlinie
                var hold = c.Route.FirstOrDefault(r => r.Kind == "orbit");
                double reach = hold != null && c.TargetKind == "node" && c.Target >= 0 && c.Target < x.W.Nodes.Count ? Geo.Dist(hold.X, hold.Z, x.W.Nodes[c.Target].X, x.W.Nodes[c.Target].Z) + 10000 : 0;
                return [Engage("CAS", Math.Max(25000, reach), "Helicopters", "Ground Units", "Light armed ships")];
            case "AWACS": return [Tk("AWACS", D()), Opt(0, 4)];
            case "Tanker": return [Tk("Tanker", D()), Beacon(c, lead), Opt(0, 4)];
            case "Escort":
                var core = x.W.Flights.FirstOrDefault(o => o.Pkg == c.Pkg && Air.Attack.Contains(o.Task) && x.Gids.ContainsKey($"F{o.Id}"));
                return core != null ? [Escort(x.Gids[$"F{core.Id}"])] : [Engage("CAP", 60000, "Air")];
            default: return [];
        }
    }

    /// DCS-KI an die Leine: ausweichen statt abbrechen, Bingo = heim; Angreifer nur aufs Auftragsziel, ohne Luftkampf und Notabwurf; Jäger ohne Bodenziele.
    static IEnumerable<Dictionary<string, object?>> Leash(Flight c)
    {
        yield return Opt(1, 2);
        yield return Opt(6, true);
        if (Air.Attack.Contains(c.Task) || c.Task == "SEAD") { yield return Opt(0, 2); yield return Opt(14, true); yield return Opt(15, true); }
        if (c.Task is "CAP" or "Sweep" or "Escort") yield return Opt(17, true);
        if (c.Task == "Jammer") { yield return Opt(13, 3); yield return Opt(0, 3); }   // Störer: ECM immer an, nur Selbstschutz
    }
    static string Gn(Flight f) => $"F{f.Id} {f.Callsign}" + (f.Owner != "" ? $" ({f.Owner})" : "");   // Gruppenname (Mehrspieler: mit Mitspieler)

    /// Angriffsauftrag ab IP: Gruppe in der Mission direkt, Flugplatz = Piste, sonst Bombenwurf auf den Zielpunkt.
    static Dictionary<string, object?>? Attack(Ctx x, Flight c)
    {
        if (!Air.Attack.Contains(c.Task) || c.Task == "CAS" || c.Route.FirstOrDefault(r => r.Kind == "target") is not { } tgt) return null;
        if (c.TargetKind == "obj" && x.W.Objs.FirstOrDefault(o => o.Id == c.Target) is { } po && Scenario.Parts.ContainsKey(po.Kind) && !(po.Kind == OK.Airbase && po.BaseId <= 0))
        {
            int pi = c.Part >= 0 ? c.Part : Air.AutoPart(x.W, po);                 // Zielpunkt: Piste oder die Gebäude des Teils
            if (po.Kind == OK.Airbase && pi == 0) return Tk("BombingRunway", Atk(("runwayId", po.BaseId)));
            var at = Spots(x, po).Where((_, i) => Scenario.PartAt(po.Kind, i) == pi).ToList();
            if (at.Count > 0) return Tk("Bombing", Atk(("x", at.Average(p => p.X)), ("y", at.Average(p => p.Z))));
        }
        var key = c.TargetKind switch { "obj" => "O", "unit" => "U", "convoy" => "C", _ => "?" } + c.Target;
        bool decoy = c.TargetKind == "obj" && x.W.Objs.FirstOrDefault(o => o.Id == c.Target) is { Decoy: true };   // Attrappe = Statics: auf den Punkt bomben
        return !decoy && x.Gids.TryGetValue(key, out var g) ? Tk("AttackGroup", Atk(("groupId", g))) : Tk("Bombing", Atk(("x", tgt.X), ("y", tgt.Z)));
    }

    static Dictionary<string, object?> Group(Ctx x, Flight f, List<object?> pts, List<object?> units, double startSec)
    {
        var lead = (Dictionary<string, object?>)units[0]!;
        return D(("groupId", x.Gids[$"F{f.Id}"]), ("name", Gn(f)), ("task", DcsTask(f.Task)), ("taskSelected", true), ("tasks", D()),
            ("uncontrolled", false), ("lateActivation", false), ("hidden", f.Side != x.W.Player), ("hiddenOnMFD", false), ("hiddenOnPlanner", false),
            ("communication", true), ("frequency", f.Freq), ("modulation", 0), ("radioSet", false), ("start_time", startSec),
            ("route", D(("points", pts))), ("units", units), ("x", lead["x"]), ("y", lead["y"]));
    }

    static Dictionary<string, object?> AirUnit(Ctx x, Flight f, int n, P p, double alt, double hdg, double spd, string skill)
    {
        var (fuel, chaff, flare) = Tanks.GetValueOrDefault(f.Type, (5000, 60, 60));
        var pylons = new Dictionary<int, object?>();
        foreach (var (num, clsid) in Pylons(x.W, f)) pylons[num] = D(("CLSID", clsid));
        double rad = hdg * Math.PI / 180;
        var k = f.Player ? x.W.KitOf(f.Type) : new Kit();                // Ausrüstung des Spielers gilt für seine ganze Rotte
        var u = D(("unitId", ++x.Uid), ("name", $"F{f.Id}-{n}"), ("type", f.Type), ("skill", skill), ("x", p.X), ("y", p.Z), ("alt", Math.Round(alt)), ("alt_type", "BARO"),
            ("speed", spd), ("heading", rad), ("psi", -rad), ("onboard_num", $"{(f.Id * 10 + n) % 1000:000}"), ("hardpoint_racks", true), ("callsign", Callsign(f, n)),
            ("payload", D(("pylons", pylons), ("fuel", Math.Round(fuel * k.Fuel / 100)), ("chaff", k.Chaff < 0 ? chaff : k.Chaff), ("flare", k.Flare < 0 ? flare : k.Flare), ("gun", k.Gun))));
        if (f.Player && f.Type == "FA-18C_hornet") u["AddPropAircraft"] = D(("OuterBoard", k.Ripple ? 1 : 0), ("InnerBoard", k.Ripple ? 1 : 0), ("HelmetMountedDevice", k.Hmd));
        return u;
    }

    /// Bestückung je Station: eigene (Planung) oder die der Beladung.
    public static IEnumerable<(int Num, string Clsid)> Pylons(World w, Flight f) => f.Pyl != null
        ? f.Pyl.Where(p => p.Value != "").Select(p => (p.Key, p.Value))
        : w.Th.Payloads.GetValueOrDefault(f.Type)?.FirstOrDefault(l => l.Name == f.Load)?.Pylons ?? [];

    /// Düppel, Fackeln, Kanone für Kniebrett und Oberfläche
    public static string Ammo(World w, Flight f)
    {
        var (_, chaff, flare) = Tanks.GetValueOrDefault(f.Type, (5000, 60, 60)); var k = w.KitOf(f.Type);
        return $"{(k.Chaff < 0 ? chaff : k.Chaff)} / {(k.Flare < 0 ? flare : k.Flare)} / {k.Gun} %, internal fuel {k.Fuel} %";
    }

    static string PilotSkill(Ctx x, Flight f, int n) => n <= f.Pilots.Count ? Skill[Math.Clamp(Air.SqnOf(x.W, f).Pilots[f.Pilots[n - 1]].Skill, 0, 3)] : "Average";

    static object Callsign(Flight f, int n)
    {
        var p = f.Callsign.Split(' ');
        if (f.Side == Side.Red || p.Length < 2 || !int.TryParse(p[1], out var fl)) return (int.TryParse(p[0], out var c) ? c : 100) * 10 + n;
        int i = f.Task switch { "AWACS" => Array.IndexOf(AwacsCs, p[0]), "Tanker" => Array.IndexOf(TankerCs, p[0]), _ => Array.IndexOf(Air.BlueCs, p[0]) } + 1;
        return new Dictionary<object, object?> { [1] = Math.Max(1, i), [2] = fl, [3] = n, ["name"] = $"{p[0]}{fl}{n}" };
    }

    public record Chan(double F, string N, bool Own);
    public static Base Home(World w, Flight f) => BaseAt(w, f.Route[0].X, f.Route[0].Z);

    /// Funkplan eines Geräts (1, 2), Kanal 1–20: Flug, Paket, AWACS, Texaco, Shell, eigene Plätze (Heimat zuerst), Rest frei, 20 = Guard.
    /// Eigene Kanäle des Spielers (Kit.Comm) ersetzen die Vorgabe.
    public static List<Chan> Comm(World w, Flight f, int radio)
    {
        var home = Home(w, f);
        var core = w.Flights.FirstOrDefault(o => o.Pkg == f.Pkg && Air.Attack.Contains(o.Task)) ?? f;
        var ch = new List<Chan> { new(f.Freq, "Flight", false), new(core.Freq, "Package", false), new(251.0, "AWACS", false), new(270.0, "Texaco", false), new(271.0, "Shell", false) };
        if (JtacOf(w, f) is { } jt) ch.Add(new(JtacF, "JTAC " + jt.Cs, false));
        ch.AddRange(w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == f.Side).OrderBy(o => o.BaseId == home.Id ? -1 : Geo.Dist(o.X, o.Z, home.X, home.Z))
            .Select(o => o.BaseId < 0 ? new Chan(127.5, o.Name, false) : new Chan(w.Th.Uhf.GetValueOrDefault(o.BaseId), w.Th.Bases.First(b => b.Id == o.BaseId).Name, false)).Where(c => c.F > 0));   // Träger: 127.5 AM
        for (int i = ch.Count; i < 19; i++) ch.Add(new(305.0 + i, "spare", false));
        ch = [.. ch.Take(19), new(243.0, "Guard", false)];
        var own = w.KitOf(f.Type).Comm;
        for (int i = 0; i < 20; i++) if (own.TryGetValue($"{radio}-{i + 1}", out var c)) ch[i] = new(c.F, c.N == "" ? "custom" : c.N, true);
        return ch;
    }

    static object? Radios(World w, Flight f)
    {
        if (f.Type is not ("FA-18C_hornet" or "F-14B" or "F-14BU" or "F-14A-135-GR" or "F-14A-135-GR-Early" or "F-14A-95-GR" or "F-15ESE")) return null;
        var r = new Dictionary<int, object?>();
        for (int k = 1; k <= 2; k++)
        {
            var ch = Comm(w, f, k);
            var d = new Dictionary<int, object?>();
            for (int i = 0; i < 20; i++) d[i + 1] = ch[i].F;
            r[k] = D(("channels", d));
        }
        return r;
    }

    /// TACAN der eigenen Plätze (Kartusche: Heimatplatz, falls nichts eigenes eingestellt)
    public static string TacanOf(World w, Flight f) => w.KitOf(f.Type).Tacan is { Length: > 0 } t ? t
        : Scenario.Carriers.Values.FirstOrDefault(c => c.Name == Home(w, f).Name) is { Name: not null } cv ? $"{cv.Tacan}X {cv.Cs}" : w.Th.Map.Tacans.GetValueOrDefault(Home(w, f).Name, "");

    // ---------- F/A-18C-Kartusche (DTC): Funk, Bedrohungsringe (MEZ), Frontlinie (FLOT) ----------

    static readonly Dictionary<string, (string Type, string Text)> MezTypes = new()     // Namen wie CoreMods\aircraft\FA-18C\DTC\SA\MEZ_THRTS_defs.lua
    {
        ["SA-2"] = ("SAM SA-2 'Guideline'", "2"), ["SA-3"] = ("SAM SA-3 'Goa'", "3"), ["SA-5"] = ("SAM SA-5 'Gammon'", "5"), ["SA-6"] = ("SAM SA-6 'Gainful'", "6"),
        ["SA-8"] = ("SAM SA-8 'Gecko'", "8"), ["SA-10"] = ("SAM SA-10 'Grumble'", "10"), ["SA-11"] = ("SAM SA-11 'Gadfly'", "11"), ["SA-13"] = ("SAM SA-13 'Gopher'", "13"),
        ["SA-15"] = ("SAM SA-15 'Gauntlet'", "15"), ["SA-19"] = ("SAM SA-19 'Grison'", "19"), ["Hawk"] = ("SAM Hawk", "HK"), ["Patriot"] = ("SAM Patriot", "P"), ["NASAMS"] = ("SAM NASAMS", "NS"),
    };

    /// Vorlage ist eine vom DCS-Missionseditor gespeicherte Hornet-Kartusche des Spielers (Saved Games\DCS\DTC): eigene CMDS-, RWR- und HARM-Einstellungen
    /// bleiben, Funk/MEZ/FLOT kommen aus der Kampagne, kartenbezogene Punkte werden geleert. Ohne Vorlage keine Kartusche.
    static (string? File, string Json)? dtcTpl;                                 // einmal lesen (neue Vorlage: Kampagne neu starten)
    public static JsonNode? DtcTemplate()
    {
        if (dtcTpl == null)
        {
            dtcTpl = (null, "");
            var dir = Path.Combine(Program.SavedGames, "DTC");
            if (Directory.Exists(dir))
                foreach (var file in Directory.EnumerateFiles(dir, "*.dtc"))
                    try { var j = JsonNode.Parse(File.ReadAllText(file)); if ((string?)j?["type"] == "FA-18C_hornet" && j["data"] is JsonObject) { dtcTpl = (Path.GetFileNameWithoutExtension(file), j.ToJsonString()); break; } } catch { }
        }
        return dtcTpl.Value.File == null ? null : JsonNode.Parse(dtcTpl.Value.Json);
    }
    public static string? DtcName() { DtcTemplate(); return dtcTpl!.Value.File; }

    public static readonly string[] CmdsProgs = ["MAN_1", "MAN_2", "MAN_3", "MAN_4", "MAN_5", "MAN_6"];
    /// ALE-47-Programme (F/A-18C): eigene, sonst aus der Kartuschen-Vorlage. Je Programm Düppel, Wiederholungen, Intervall s, Fackeln.
    public static Dictionary<string, double[]> Cmds(World w)
    {
        var tpl = DtcTemplate()?["data"]?["ALR67"]?["CMDS"]?["CMDSProgramSettings"];
        var own = w.KitOf("FA-18C_hornet").Cmds;
        return CmdsProgs.ToDictionary(p => p, p => own.TryGetValue(p, out var v) ? v : tpl?[p] is { } t
            ? [(double?)t["Chaff"]?["Quantity"] ?? 0, (double?)t["Chaff"]?["Repeat"] ?? 1, (double?)t["Chaff"]?["Interval"] ?? 1, (double?)t["Flare"]?["Quantity"] ?? 0] : [1.0, 1, 1, 1]);
    }
    /// Höhenwarnung Baro/Radar (ft): eigene oder aus der Vorlage
    public static (int Baro, int Rdr) AltWarn(World w)
    {
        var k = w.KitOf("FA-18C_hornet"); var a = DtcTemplate()?["data"]?["WYPT"]?["NAV_SETTINGS"]?["Altitude_Warning"];
        return (k.AltBaro >= 0 ? k.AltBaro : (int?)a?["Warn_Alt_Baro"] ?? 2000, k.AltRdr >= 0 ? k.AltRdr : (int?)a?["Warn_Alt_Rdr"] ?? 500);
    }

    static string? Dtc(Ctx x)
    {
        var root = DtcTemplate();
        if (root == null) { x.Notes.Add("No F/A-18C cartridge template in Saved Games\\DCS\\DTC – radios and waypoints come from the mission, no MEZ/FLOT."); return null; }
        try
        {
            var (w, f) = (x.W, x.F);
            var d = root["data"]!;
            root["name"] = "Kampagne"; d["name"] = "Kampagne"; d["terrain"] = w.Th.Map.Id;
            for (int k = 1; k <= 2; k++)
            {
                var ch = Comm(w, f, k);
                for (int i = 1; i <= 20; i++)
                    if (d["COMM"]?[$"COMM{k}"]?[$"Channel_{i}"] is JsonObject c) { c["frequency"] = ch[i - 1].F; c["modulation"] = 0; c["name"] = ch[i - 1].N.Length > 8 ? ch[i - 1].N[..8] : ch[i - 1].N; }
            }
            if (d["ALR67"]?["CMDS"]?["CMDSProgramSettings"] is JsonObject cp)
                foreach (var (prog, v) in Cmds(w))
                    if (cp[prog] is JsonObject pr) { pr["Chaff"] = new JsonObject { ["Quantity"] = v[0], ["Repeat"] = v[1], ["Interval"] = v[2] }; if (pr["Flare"] is JsonObject fl) fl["Quantity"] = v[3]; }
            if (d["WYPT"]?["NAV_SETTINGS"] is JsonObject ns)
            {
                var (baro, rdr) = AltWarn(w);
                ns["Altitude_Warning"] = new JsonObject { ["Warn_Alt_Baro"] = baro, ["Warn_Alt_Rdr"] = rdr };
                if (System.Text.RegularExpressions.Regex.Match(TacanOf(w, f), @"^(\d+)([XY])$") is { Success: true } tc && ns["TACAN"] is JsonObject tn)
                { tn["Channel"] = int.Parse(tc.Groups[1].Value); tn["ChannelMode"] = tc.Groups[2].Value == "X" ? 1 : 2; tn["OnOff"] = true; }
            }
            if (d["WYPT"] is JsonObject wy) { wy["NAV_PTS"] = new JsonArray(); wy["NAV_ROUTE"] = new JsonArray(new JsonArray(), new JsonArray(), new JsonArray()); wy["terrain"] = w.Th.Map.Id; }   // Wegpunkte kommen aus der Mission
            d["GPS_WYPT"] = new JsonArray();
            var sa = d["SA"]!.AsObject();
            sa["CAP_PTS"] = new JsonArray(); sa["CORRIDORS"] = new JsonArray();
            var know = w.Intel(f.Side);
            var mez = new JsonArray();
            foreach (var o in w.Objs.Where(o => o.Side == f.Side.Enemy() && o.Kind == OK.Sam && o.Health > 0.3 && know.ContainsKey($"O{o.Id}")).OrderBy(o => RouteDist(x, know[$"O{o.Id}"].X, know[$"O{o.Id}"].Z)).Take(40))
            {
                var (type, text) = MezTypes.GetValueOrDefault(o.Sys, ("Custom", o.Sys.Replace("SA-", "")));
                int n = mez.Count + 1;
                mez.Add(new JsonObject { ["id"] = $"MEZ_THRTS_{n}", ["num"] = n, ["x"] = Math.Round(know[$"O{o.Id}"].X), ["y"] = Math.Round(know[$"O{o.Id}"].Z), ["text"] = text, ["threat_type"] = type, ["threat_ring_radius"] = Math.Round(o.Range / Geo.NM, 1), ["threat_level"] = 1 });
            }
            sa["MEZ_THRTS"] = mez;
            var flot = new JsonArray();
            var line = Flot(w);
            for (int k = 0; k < 3 && k * 6 < line.Count - 1; k++)                // max. 3 Linien à 7 Punkte, aneinander anschließend
                flot.Add(new JsonObject
                {
                    ["id"] = $"FLOT_{k + 1}", ["num"] = k + 1, ["note"] = "",
                    ["points"] = new JsonArray(line.Skip(k * 6).Take(7).Select((p, i) => (JsonNode)new JsonObject { ["id"] = $"FLOT_{k + 1}_PT_{i + 1}", ["x"] = Math.Round(p.X), ["y"] = Math.Round(p.Z) }).ToArray()),
                });
            sa["FAOR_FLOT"] = new JsonObject { ["FAOR"] = new JsonArray(), ["FLOT"] = flot };
            x.Notes.Add($"F/A-18C cartridge: radio 1–20, ALE-47 programs, altitude warning, TACAN, {mez.Count} threat rings, FLOT – loaded at start.");
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception e) { x.Notes.Add("Cartridge not created: " + e.Message); return null; }
    }

    static double RouteDist(Ctx x, double px, double pz) =>
        x.Wps.Zip(x.Wps.Skip(1)).Select(s => Geo.SegDist(new P(px, pz), new P(s.First.X, s.First.Z), new P(s.Second.X, s.Second.Z))).DefaultIfEmpty(0).Min();

    /// Frontlinie West→Ost: Mitten der Wege zwischen roten und blauen Orten, in 19 Streifen zusammengefasst (Median der Nord-Lage).
    public static List<P> Flot(World w)
    {
        var m = w.Edges.Select(e => (A: w.Nodes[e.A], B: w.Nodes[e.B])).Where(e => e.A.Owner != Side.None && e.B.Owner != Side.None && e.A.Owner != e.B.Owner)
            .Select(e => new P((e.A.X + e.B.X) / 2, (e.A.Z + e.B.Z) / 2)).ToList();
        if (m.Count < 2) return [];
        double z0 = m.Min(p => p.Z), step = (m.Max(p => p.Z) - z0) / 19 + 1;
        return m.GroupBy(p => (int)((p.Z - z0) / step)).OrderBy(g => g.Key)
            .Select(g => { var xs = g.Select(p => p.X).Order().ToList(); return new P(xs[xs.Count / 2], g.Average(p => p.Z)); }).ToList();
    }

    // ---------- Wegpunkte und Aufträge ----------

    static Dictionary<string, object?> Pt(Ctx x, double px, double pz, double alt, double spd, string type, string action, string name, DateTime t, List<Dictionary<string, object?>> tasks, int airdrome = 0)
    {
        for (int i = 0; i < tasks.Count; i++) tasks[i]["number"] = i + 1;
        var d = D(("x", px), ("y", pz), ("alt", Math.Round(alt)), ("alt_type", "BARO"), ("type", type), ("action", action), ("speed", spd), ("speed_locked", true),
            ("ETA", Math.Max(0, x.Sec(t))), ("ETA_locked", false), ("name", name), ("formation_template", ""),
            ("task", D(("id", "ComboTask"), ("params", D(("tasks", tasks.Cast<object?>().ToList()))))));
        if (airdrome > 0) d["airdromeId"] = airdrome;
        else if (airdrome < 0) { d["linkUnit"] = x.Cv; d["helipadId"] = x.Cv; }   // Träger
        return d;
    }

    static Dictionary<string, object?> Leg(Ctx x, Wp r, string name, List<Dictionary<string, object?>> tasks) => r.Kind == "land"
        ? Pt(x, r.X, r.Z, r.Alt, r.Spd, "Land", "Landing", name, r.T, [], BaseAt(x.W, r.X, r.Z).Id)
        : Pt(x, r.X, r.Z, r.Alt, r.Spd, "Turning Point", "Turning Point", name, r.T, tasks);

    static Dictionary<string, object?> Tk(string id, Dictionary<string, object?> prm) => D(("enabled", true), ("auto", false), ("id", id), ("params", prm));
    static Dictionary<string, object?> Engage(string key, double maxDist, params string[] types)
    {
        var t = Tk("EngageTargets", D(("targetTypes", types.Cast<object?>().ToList()), ("priority", 0), ("maxDistEnabled", true), ("maxDist", maxDist)));
        t["key"] = key;
        return t;
    }
    static Dictionary<string, object?> Wrapped(string id, Dictionary<string, object?> prm) => Tk("WrappedAction", D(("action", D(("id", id), ("params", prm)))));
    static Dictionary<string, object?> Opt(int name, object value) => Wrapped("Option", D(("name", name), ("value", value)));
    static Dictionary<string, object?> Orbit(double alt, double spd, double sec) => Tk("ControlledTask", D(
        ("task", D(("id", "Orbit"), ("params", D(("altitude", Math.Round(alt)), ("pattern", "Race-Track"), ("speed", spd))))),
        ("stopCondition", D(("duration", Math.Max(60, Math.Round(sec)))))));
    static Dictionary<string, object?> Escort(int gid) => Tk("Escort", D(("groupId", gid), ("engagementDistMax", 60000), ("lastWptIndexFlag", false), ("lastWptIndexFlagChangeManually", true),
        ("targetTypes", L("Planes")), ("noTargetTypes", L("Helicopters")), ("pos", D(("x", -500), ("y", 0), ("z", 200)))));
    // TACAN X, Kanal 1–63, Bodenstation-Modus (wie der Missionseditor für Tanker): 961 + Kanal MHz
    static Dictionary<string, object?> Beacon(Flight c, int unitId) => Wrapped("ActivateBeacon", D(("type", 4), ("system", 4), ("AA", false), ("unitId", unitId),
        ("modeChannel", "X"), ("channel", c.Tacan), ("callsign", c.Callsign[..3].ToUpperInvariant()), ("bearing", true), ("frequency", (961.0 + c.Tacan) * 1e6)));
    static Dictionary<string, object?> Atk(params (string K, object? V)[] kv)
    {
        var d = D(("attackQtyLimit", false), ("attackQty", 1), ("expend", "Auto"), ("groupAttack", true), ("altitudeEnabled", false), ("altitude", 2000), ("directionEnabled", false), ("direction", 0));
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }

    // ---------- Bodentruppen, Stellungen, Gebäude ----------

    /// Nächster Punkt an Land (Fahrzeuge nicht ins Meer stellen).
    static P Land(Theater th, double px, double pz)
    {
        for (double r = 0; r <= 8000; r += 500)
            for (int a = 0; a < (r == 0 ? 1 : 12); a++)
            {
                var p = Geo.Move(new P(px, pz), a * 30, r);
                if (th.Height(p.X, p.Z) > 3 && !th.Clipped(p.X, p.Z)) return p;
            }
        return new P(px, pz);
    }

    static List<P> Spread(P c, int n, double hdg, double gap) =>
        Enumerable.Range(0, n).Select(i => Geo.Move(Geo.Move(c, hdg + 90, (i % 4 - 1.5) * gap), hdg + 180, i / 4 * gap)).ToList();

    public static string Role(string type)
    {
        var t = type.ToLowerInvariant();
        return t.Contains(" ln") || t.Contains("_ln") || t.Contains("volhov") ? "l"
            : t.Contains(" sr") || t.Contains(" tr") || t.Contains(" str") || t.Contains("radar") || t.Contains("ewr") || t is "fps-117" or "hawk cwar" or "tor 9a331" or "snr_75v" ? "r" : "x";
    }

    // ---------- JTAC und FAC(A) für CAS: DCS-ATC erkennt sie an Name („JTAC …“, „FACA …“) und FAC-Aufgabe (Rufzeichen, Frequenz, Lasercode) ----------
    public const double JtacF = 133.0;
    public const int Laser = 1688;
    public record Fac(string Cs, int Idx, string Faca, int FacaIdx, P Pos, P Tgt);

    /// JTAC eines CAS-Flugs: 3–6 km vor dem Feind an seiner Kontaktlinie, Richtung eigene Linien, mit Sicht aufs Ziel (Höhenmodell). Rufzeichen je Achse wie im F10-Menü.
    public static Fac? JtacOf(World w, Flight f)
    {
        if (f.Task != "CAS" || f.TargetKind != "node" || f.Target < 0 || f.Target >= w.Nodes.Count) return null;
        var tp = new P(w.Nodes[f.Target].X, w.Nodes[f.Target].Z);
        int ax = w.Axes.FindIndex(a => Sim.Front(w, a) is var (r, b) && (r == f.Target || b == f.Target));
        var own = w.Nodes.Where(n => n.Owner == f.Side).MinBy(n => Geo.Dist(n.X, n.Z, tp.X, tp.Z));
        double brg = own == null ? 0 : Geo.Brg(tp, new P(own.X, own.Z));
        var spots = (from d in new[] { 4000.0, 3000, 5000, 6000 } from o in new[] { 0.0, -25, 25, -50, 50 } select Land(w.Th, Geo.Move(tp, brg + o, d).X, Geo.Move(tp, brg + o, d).Z)).ToList();
        int i = Math.Max(0, ax) % Tasking.Jtacs.Length, k = (i + 9) % Tasking.Jtacs.Length;
        return new(Tasking.Jtacs[i], i + 1, Tasking.Jtacs[k], k + 1, spots.FirstOrDefault(p => Los(w.Th, p, tp), spots[0]), tp);
    }

    /// Sicht Boden zu Boden (je 2 m darüber), Höhenmodell alle 100 m.
    public static bool Los(Theater th, P a, P b)
    {
        double ha = th.Height(a.X, a.Z) + 2, hb = th.Height(b.X, b.Z) + 2;
        int n = (int)(Geo.Dist(a.X, a.Z, b.X, b.Z) / 100);
        for (int i = 1; i < n; i++) { double t = (double)i / n; if (th.Height(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t) > ha + (hb - ha) * t) return false; }
        return true;
    }

    static Dictionary<string, object?> FacTask(int idx) => Tk("FAC", D(("callname", idx), ("number", 1), ("designation", "Auto"), ("datalink", true), ("frequency", JtacF * 1e6), ("modulation", 0), ("laserCode", Laser)));

    /// CAS der Spieler: JTAC (Hummer/UAZ) an der Kontaktlinie, Blau dazu eine MQ-9 als FAC(A) über dem Ziel. Unsichtbar, unverwundbar, schießen nicht;
    /// ohne DCS-ATC spricht der DCS-eigene JTAC (F-Menü). Die Kampagne rechnet sie nicht (Namen ohne Kampagnen-Schlüssel).
    static void Facs(Ctx x)
    {
        var done = new HashSet<string>();
        foreach (var m in x.Mine)
        {
            if (JtacOf(x.W, m) is not { } j || !done.Add(j.Cs)) continue;
            bool blue = m.Side == Side.Blue, hidden = m.Side != x.W.Player;
            List<object?> quiet() => [Wrapped("SetInvisible", D(("value", true))), Wrapped("SetImmortal", D(("value", true))), Opt(0, 4)];
            x.Add(m.Side, blue ? x.W.S.BlueSite : x.W.S.RedC, "vehicle", Vehicles(x, ++x.Gid, $"JTAC {j.Cs}", [(blue ? "Hummer" : "UAZ-469", $"JTAC {j.Cs}-1", j.Pos, Geo.Brg(j.Pos, j.Tgt))], [],
                "Excellent", hidden, extra: [FacTask(j.Idx), .. quiet()]));
            if (!blue) continue;                                            // ponytail: FAC(A) nur Blau (MQ-9), Rot ohne Drohne – bei Bedarf WingLoong-I
            var p = Geo.Move(j.Tgt, Geo.Brg(j.Tgt, j.Pos), 3000);
            double alt = Math.Max(4600, x.W.Th.Height(j.Tgt.X, j.Tgt.Z) + 3000), v = 70;
            var pt = Pt(x, p.X, p.Z, alt, v, "Turning Point", "Turning Point", "FAC(A)", x.T0,
                [FacTask(j.FacaIdx), .. quiet().Cast<Dictionary<string, object?>>(), Tk("Orbit", D(("altitude", Math.Round(alt)), ("pattern", "Circle"), ("speed", v)))]);
            var u = D(("unitId", ++x.Uid), ("name", $"FACA {j.Faca}-1"), ("type", "MQ-9 Reaper"), ("skill", "Excellent"), ("x", p.X), ("y", p.Z), ("alt", Math.Round(alt)), ("alt_type", "BARO"),
                ("speed", v), ("heading", 0.0), ("psi", 0.0), ("onboard_num", "901"), ("callsign", new Dictionary<object, object?> { [1] = 8, [2] = 9, [3] = 1, ["name"] = "Pontiac91" }),
                ("payload", D(("pylons", D()), ("fuel", 1300), ("chaff", 0), ("flare", 0), ("gun", 100))));
            x.Add(Side.Blue, x.W.S.BlueSite, "plane", D(("groupId", ++x.Gid), ("name", $"FACA {j.Faca}"), ("task", "AFAC"), ("taskSelected", true), ("tasks", D()),
                ("uncontrolled", false), ("lateActivation", false), ("hidden", hidden), ("hiddenOnMFD", false), ("hiddenOnPlanner", false),
                ("communication", true), ("frequency", JtacF), ("modulation", 0), ("radioSet", true), ("start_time", 0),
                ("route", D(("points", L(pt)))), ("units", L(u)), ("x", p.X), ("y", p.Z)));
        }
    }

    static void Ground(Ctx x, Unit u)
    {
        var w = x.W;
        var tpl = Scenario.Templates[u.Template];
        int n = Math.Clamp((int)Math.Round(tpl.Length * u.Str), 1, tpl.Length);
        var c = Land(w.Th, u.X, u.Z);
        var en = w.Nodes.Where(m => m.Owner != u.Side).MinBy(m => Geo.Dist(m.X, m.Z, c.X, c.Z));
        double hdg = en == null ? 0 : Geo.Brg(c, new P(en.X, en.Z));
        var veh = Spread(c, n, hdg, 60).Select((p, i) => (tpl[i], $"U{u.Id}-{i + 1}", p, hdg)).ToList();
        var path = (u.Next >= 0 ? new[] { u.Next } : []).Concat(u.Path).Take(4).Select(id => new P(w.Nodes[id].X, w.Nodes[id].Z)).ToList();
        string country = TplCountry.GetValueOrDefault(u.Template.Split('-')[0], "USA");
        var e = x.Dyn(country, "vehicle", Vehicles(x, x.Gids[$"U{u.Id}"], $"U{u.Id} {u.Name}", veh, path, Skill[Math.Clamp((int)(u.Exp * 4), 0, 3)], u.Side != w.Player), $"U{u.Id}", GroundR);
        // Artillerie in Reichweite der feindlichen Front schießt in DCS echt (FireAtPoint, das Skript wiederholt alle paar Minuten)
        if (u.Kind == UK.Art && w.Nodes.Where(m => m.Owner == u.Side.Enemy() && Geo.Dist(m.X, m.Z, c.X, c.Z) < 20000).MinBy(m => Geo.Dist(m.X, m.Z, c.X, c.Z)) is { } tgt)
            e["fire"] = D(("x", Math.Round(tgt.X)), ("z", Math.Round(tgt.Z)));
    }

    /// Stützpunkt der Sicherungslinie: 3 Fahrzeuge des Eigentümer-Verbands, Front zum Feind. Gruppe "L…" (keine Kampagnen-Einheit),
    /// Fahrzeuge "U{id}-1xx": Verluste treffen den Verband wie seine eigenen Fahrzeuge.
    static void Post(Ctx x, Sim.Post p, int k)
    {
        var u = x.W.Units.First(u => u.Id == p.Unit);
        var tpl = Scenario.Templates[u.Template];
        var c = Land(x.W.Th, p.X, p.Z);
        var t = x.W.Nodes[p.Toward];
        double hdg = Geo.Brg(c, new P(t.X, t.Z));
        var veh = Spread(c, Math.Min(3, tpl.Length), hdg, 60).Select((q, i) => (tpl[i], $"U{u.Id}-{100 + 3 * k + i}", q, hdg)).ToList();
        x.Gids[$"L{k}"] = ++x.Gid;
        x.Dyn(TplCountry.GetValueOrDefault(u.Template.Split('-')[0], "USA"), "vehicle",
            Vehicles(x, x.Gids[$"L{k}"], $"L{k} Outpost {u.Name}", veh, [], Skill[Math.Clamp((int)(u.Exp * 4), 0, 3)], u.Side != x.W.Player), $"L{k}", GroundR);
    }

    static void Site(Ctx x, Obj o)
    {
        var parts = Scenario.Sams[o.Sys].Parts;
        int nl = parts.Where(p => Role(p.T) == "l").Sum(p => p.N), li = 0, k = 0;
        double ring = o.Sys is "SA-10" or "Patriot" ? 220 : 110;
        var c = Land(x.W.Th, o.X, o.Z);
        var veh = new List<(string T, string N, P P, double H)>();
        foreach (var (t, n) in parts)
            for (int i = 0; i < n; i++)
            {
                string role = Role(t);
                var p = role == "l" ? Geo.Move(c, 360.0 * li++ / Math.Max(1, nl), ring) : Geo.Move(c, 72.0 * k, k == 0 ? 0 : 50);
                k++;
                veh.Add((t, $"O{o.Id}-{k}-{role}", p, role == "l" ? Geo.Brg(c, p) : 0));
            }
        x.Elems[$"O{o.Id}"] = veh.Select(v => (v.T, v.P)).ToList();   // Attrappen sehen auf dem Zielblatt echt aus
        string country = o.Side == Side.Red ? x.W.S.RedC : x.W.S.BlueSite;
        if (o.Decoy)                                                        // Attrappe: Fahrzeuge als Statics – sehen echt aus, strahlen und schießen nicht
            foreach (var (t, n, p, h) in veh) x.Add(o.Side, country, "static", Static(x, n, t, "Air Defence", p, h, false, o.Side != x.W.Player));
        else
        {
            var e = x.Dyn(country, "vehicle", Vehicles(x, x.Gids[$"O{o.Id}"], $"O{o.Id} {o.Name}", veh, [], "Excellent", o.Side != x.W.Player, sam: o.Kind == OK.Sam), $"O{o.Id}", GroundR + o.Range);   // Ring schneidet die Bubble
            if (o.Kind != OK.Sam) return;
            e["iads"] = Math.Round(o.Range);                               // IADS im Skript: dunkel bis Ziel in Reichweite, Netz = deckende Frühwarnradare/AWACS
            e["net"] = x.W.Objs.Where(r => r.Side == o.Side && r.Kind == OK.Ewr && r.Health > 0.3 && Geo.Dist(r.X, r.Z, o.X, o.Z) < r.Range * 0.8).Select(r => (object?)$"O{r.Id}")
                .Concat(x.W.Flights.Where(f => f.Side == o.Side && f.Task == "AWACS" && !f.Done && (Air.Up(f) || f.Status == "geplant" && f.Takeoff < x.T0.AddHours(2)) && f.Route.Any(p => Geo.Dist(p.X, p.Z, o.X, o.Z) < 320000)).Select(f => (object?)$"F{f.Id}")).ToList();
        }
    }

    /// Lage der Gebäude eines Ziels (Reihenfolge wie Scenario.Statics); Flugplatz: 900 m neben der Piste
    static List<P> Spots(Ctx x, Obj o)
    {
        int n = Scenario.Statics[o.Kind].Length;
        var c = new P(o.X, o.Z);
        if (o.Kind == OK.Airbase && x.W.Th.Bases.FirstOrDefault(b => b.Id == o.BaseId) is { } ab) c = Geo.Move(c, ab.RwyHdg + 90, 900);
        c = Land(x.W.Th, c.X, c.Z);
        return Enumerable.Range(0, n).Select(i => Geo.Move(c, 360.0 * i / n + 15, i == 0 ? 0 : 120)).ToList();
    }

    /// Gebäude eines Ziels als Statics, je Teil so viele zerstört, wie sein Zustand sagt. Name O{id}-{i}/{n}: DEAD → Teil (Scenario.PartAt).
    static void Buildings(Ctx x, Obj o)
    {
        var types = Scenario.Statics[o.Kind];
        var parts = Scenario.Parts[o.Kind];
        int n = types.Length;
        if (n == 0) return;
        var at = Spots(x, o);
        x.Elems[$"O{o.Id}"] = types.Select((t, i) => ($"{parts[Scenario.PartAt(o.Kind, i)].Name}: {t.TrimStart('.')}", at[i])).ToList();
        for (int k = 0, i = 0; k < parts.Length; k++)
        {
            int m = parts[k].Types.Length, dead = (int)Math.Round(m * (1 - o.P(parts[k].Name)));
            for (int j = 0; j < m; j++, i++)
                x.Add(o.Side, o.Side == Side.Red ? x.W.S.RedC : x.W.S.BlueC, "static",
                    Static(x, $"O{o.Id}-{i + 1}/{n}", types[i], Warehouse.Contains(types[i]) ? "Warehouses" : "Fortifications", at[i], 0, j < dead, o.Side != x.W.Player));
        }
    }

    static void Convoy(Ctx x, Convoy cv)
    {
        var w = x.W;
        string country = cv.Side == Side.Red ? w.S.RedC : w.S.BlueSite;
        if (cv.Ship) { x.Dyn(country, "ship", Ship(x, cv), $"C{cv.Id}", GroundR + Sim.NavyRange(cv)); return; }
        var tpl = Scenario.Templates[cv.Side == Side.Red ? "Convoy-RU" : "Convoy-BL"];
        int n = Math.Clamp(cv.Trucks, 1, 10);
        var path = cv.Path.Skip(cv.Leg).Take(5).Select(id => new P(w.Nodes[id].X, w.Nodes[id].Z)).ToList();
        var c = Land(w.Th, cv.X, cv.Z);
        double hdg = path.Count > 0 ? Geo.Brg(c, path[0]) : 0;
        var veh = Enumerable.Range(0, n).Select(i => (tpl[i % tpl.Length], $"C{cv.Id}-{i + 1}/{n}", Geo.Move(c, hdg + 180, i * 40.0), hdg)).ToList();
        x.Dyn(country, "vehicle", Vehicles(x, x.Gids[$"C{cv.Id}"], $"C{cv.Id} {(cv.Rail ? "Train" : "Convoy")}", veh, path, "Average", cv.Side != w.Player, 12), $"C{cv.Id}", GroundR);
        // Güterzug: das Skript setzt beim Erscheinen einen echten Zug aufs nächste Gleis und entfernt dann die Lkw (klappt das nicht, bleiben die Lkw)
        if (cv.Rail && path.Count > 0)
            x.Trains.Add(D(("g", $"C{cv.Id} Train"), ("id", cv.Id), ("n", Math.Clamp(cv.Trucks, 2, 12)), ("c", Countries[country]),
                ("tx", Math.Round(path[^1].X)), ("tz", Math.Round(path[^1].Z))));
    }

    /// Frachter: von der aktuellen Position bis 4 km vor den Hafen (DCS-Schiffe laufen nicht an Land). Kriegsschiffe: Kiellinie, halten die Position.
    static Dictionary<string, object?> Ship(Ctx x, Convoy cv)
    {
        var p = x.W.Objs.First(o => o.Id == cv.To);
        var c = new P(cv.X, cv.Z);
        double brg = Geo.Brg(c, new P(p.X, p.Z));
        var berth = Geo.Move(new P(p.X, p.Z), brg + 180, 4000);
        Dictionary<string, object?> Pt(P q, double v, double eta) => D(("x", q.X), ("y", q.Z), ("alt", 0), ("alt_type", "BARO"), ("type", "Turning Point"), ("action", "Turning Point"),
            ("speed", v), ("speed_locked", true), ("ETA", Math.Round(eta)), ("ETA_locked", false), ("name", ""), ("formation_template", ""), ("task", D(("id", "ComboTask"), ("params", D(("tasks", D()))))));
        List<string> ships = cv.Navy ? [.. cv.Types.Take(Math.Max(1, cv.Trucks))] : [cv.Side == Side.Red ? "Dry-cargo ship-2" : "Dry-cargo ship-1"];
        return D(("groupId", x.Gids[$"C{cv.Id}"]), ("name", cv.Navy ? $"C{cv.Id} {cv.Name}" : $"C{cv.Id} Freighter"), ("tasks", D()), ("start_time", 0), ("visible", false), ("hidden", cv.Side != x.W.Player),
            ("hiddenOnMFD", false), ("hiddenOnPlanner", false), ("lateActivation", false), ("uncontrolled", false),
            ("route", D(("points", cv.Navy ? L(Pt(c, 0, 0)) : L(Pt(c, 0, 0), Pt(berth, 6.9, Geo.Dist(c.X, c.Z, berth.X, berth.Z) / 6.9))))),
            ("units", L([.. ships.Select((t, i) => (object?)D(("unitId", ++x.Uid), ("name", $"C{cv.Id}-{i + 1}/{ships.Count}"), ("type", t), ("skill", cv.Navy ? "Excellent" : "Average"),
                ("x", Geo.Move(c, brg + 180, i * 800.0).X), ("y", Geo.Move(c, brg + 180, i * 800.0).Z), ("heading", brg * Math.PI / 180), ("transportable", D(("randomTransportable", false))), ("frequency", 127500000), ("modulation", 0)))])),
            ("x", c.X), ("y", c.Z));
    }

    /// Ziel-Flugplatz eines Angriffs: Maschinen der dortigen Staffeln stehen auf den Abstellplätzen.
    static void Parked(Ctx x, Obj ab)
    {
        var w = x.W;
        var spots = (w.Th.Parking.GetValueOrDefault(ab.BaseId) ?? []).Where(p => PlaneSpots.Contains(p.Type)).OrderBy(p => p.Num).ToList();
        int k = 0;
        foreach (var q in w.Sqns.Where(q => q.Base == ab.Id && q.Ac > 0))
            for (int i = 0; i < Math.Min(q.Ac, 4) && k < spots.Count; i++, k++)
                x.Add(q.Side, q.Country, "static", Static(x, $"Q{q.Id}-{i + 1}", q.Type, Air.Helo(q.Type) ? "Helicopters" : "Planes", new P(spots[k].X, spots[k].Z), 0, false, q.Side != w.Player));
    }

    static Dictionary<string, object?> Vehicles(Ctx x, int gid, string name, List<(string T, string N, P P, double H)> veh, List<P> path, string skill, bool hidden, double spd = 6, bool sam = false, List<object?>? extra = null)
    {
        double eta = 0; var last = veh[0].P;                       // Planzeit je Punkt: das Skript setzt später erscheinende Gruppen an die Planposition
        Dictionary<string, object?> V(P p, string action, double v)
        {
            eta += v > 0 ? Geo.Dist(last.X, last.Z, p.X, p.Z) / v : 0; last = p;
            return D(("x", p.X), ("y", p.Z), ("alt", 0), ("alt_type", "BARO"), ("type", "Turning Point"), ("action", action),
                ("speed", v), ("speed_locked", true), ("ETA", Math.Round(eta)), ("ETA_locked", false), ("name", ""), ("formation_template", ""), ("task", D(("id", "ComboTask"), ("params", D(("tasks", D()))))));
        }
        var p0 = V(veh[0].P, "Off Road", 0);
        var opts = sam ? new List<object?> { Opt(8, 0), Opt(9, 2) } : [Opt(8, 0)];
        if (extra != null) opts.AddRange(extra);
        for (int i = 0; i < opts.Count; i++) ((Dictionary<string, object?>)opts[i]!)["number"] = i + 1;
        p0["task"] = D(("id", "ComboTask"), ("params", D(("tasks", opts))));
        var pts = new List<object?> { p0 };
        pts.AddRange(path.Select(p => (object?)V(p, "On Road", spd)));
        return D(("groupId", gid), ("name", name), ("task", "Ground Nothing"), ("taskSelected", true), ("tasks", D()), ("start_time", 0),
            ("visible", false), ("hidden", hidden), ("hiddenOnMFD", false), ("hiddenOnPlanner", false), ("lateActivation", false), ("manualHeading", false), ("uncontrolled", false),
            ("route", D(("points", pts), ("spans", D()))),
            ("units", veh.Select(v => (object?)D(("unitId", ++x.Uid), ("name", v.N), ("type", v.T), ("skill", skill), ("x", v.P.X), ("y", v.P.Z), ("heading", v.H * Math.PI / 180), ("playerCanDrive", false))).ToList()),
            ("x", veh[0].P.X), ("y", veh[0].P.Z));
    }

    /// Kategorie eines Fahrzeugs als Static (Wrack).
    static string StaticCat(string t) =>
        t.Contains("Ural") || t.Contains("KAMAZ") || t.Contains("GAZ") || t.Contains("M 818") || t.Contains("HEMTT") ? "Unarmed"
        : t.Contains("SAU") || t.Contains("Grad") || t is "M-109" or "MLRS" ? "Artillery"
        : t.Contains("Tunguska") || t.Contains("Strela") || t.Contains("Osa") || t.Contains("ZSU") || t.Contains("Avenger") || t.Contains("Tor ") || t is "Gepard" or "M6 Linebacker" ? "Air Defence"
        : "Armor";

    static Dictionary<string, object?> Static(Ctx x, string name, string type, string cat, P p, double hdg, bool dead, bool hidden) =>
        D(("groupId", ++x.Gid), ("name", name), ("dead", dead), ("heading", hdg), ("hidden", hidden), ("linkOffset", false), ("x", p.X), ("y", p.Z),
          ("route", D(("points", L(D(("action", ""), ("alt", 0), ("formation_template", ""), ("name", ""), ("speed", 0), ("type", ""), ("x", p.X), ("y", p.Z)))))),
          ("units", L(D(("unitId", ++x.Uid), ("name", name), ("type", type), ("category", cat), ("canCargo", false), ("heading", hdg), ("x", p.X), ("y", p.Z)))));

    // ---------- Mission zusammensetzen ----------

    /// Splash Damage (Stevey666, Forum/User Files): liegt beim Spieler unter <Datenordner>\scripts, wird in jede Mission eingebaut. Nicht im Paket – fremdes Skript.
    public static string? Splash => Program.Opt("Splash") && Directory.Exists(Path.Combine(Program.Dir, "scripts"))
        ? Directory.GetFiles(Path.Combine(Program.Dir, "scripts"), "Splash_Damage*.lua").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;

    static Dictionary<string, object?> Mission(Ctx x, Base home)
    {
        var t0 = x.T0;
        Dictionary<string, object?> Coal(Side s)
        {
            var countries = x.Groups.Where(g => g.S == s).GroupBy(g => g.Country).Select(cg =>
            {
                var d = D(("id", Countries[cg.Key]), ("name", cg.Key));
                foreach (var cat in cg.GroupBy(g => g.Cat)) d[cat.Key] = D(("group", cat.Select(g => (object?)g.G).ToList()));
                return (object?)d;
            }).ToList();
            var bull = s == Side.Blue ? x.W.S.BullBlue : x.W.S.BullRed;
            return D(("name", s == Side.Blue ? "blue" : "red"), ("bullseye", D(("x", bull.X), ("y", bull.Z))), ("nav_points", D()), ("country", countries));
        }
        var res = D(("actions", D()), ("conditions", D()), ("func", D()));
        return D(
            ("coalition", D(("blue", Coal(Side.Blue)), ("red", Coal(Side.Red)))),
            ("coalitions", D(("blue", x.W.S.BlueIds.Select(i => (object?)i).ToList()), ("neutrals", L()), ("red", x.W.S.RedIds.Select(i => (object?)i).ToList()))),
            ("currentKey", 0),
            ("date", D(("Day", t0.Day), ("Month", t0.Month), ("Year", t0.Year))),
            ("descriptionText", "DictKey_descriptionText_1"), ("descriptionBlueTask", "DictKey_descriptionBlueTask_2"),
            ("descriptionRedTask", "DictKey_descriptionRedTask_3"), ("sortie", "DictKey_sortie_4"), ("maxDictId", 4),
            ("failures", D()), ("forcedOptions", D()), ("goals", D()),
            ("map", D(("centerX", home.X), ("centerY", home.Z), ("zoom", 1000000))),
            ("pictureFileNameB", D()), ("pictureFileNameN", D()), ("pictureFileNameR", D()), ("requiredModules", D()),
            ("result", D(("offline", res), ("blue", res), ("red", res), ("total", 0))),
            ("start_time", (int)(t0 - t0.Date).TotalSeconds), ("theatre", x.W.Th.Map.Id),
            ("trig", D(("actions", L(string.Concat(Keys.Select(k => $"a_do_script_file(getValueResourceByKey(\"{k}\"));")))),
                ("conditions", L("return(true)")), ("customStartup", D()), ("events", D()), ("flag", L(true)), ("func", D()),
                ("funcStartup", L("if mission.trig.conditions[1]() then mission.trig.actions[1]() end")))),
            ("triggers", D(("zones", D()))),
            ("trigrules", L(D(("actions", L([.. Keys.Select(k => (object?)D(("file", k), ("predicate", "a_do_script_file")))])),
                ("colorItem", "0xffffffff"), ("comment", "Warclock: data + feedback + Splash Damage"), ("eventlist", ""), ("predicate", "triggerStart"), ("rules", D())))),
            ("usedModules", D()), ("version", 20), ("weather", Weather(x.W.Wx)));
    }

    static Dictionary<string, object?> Weather(Weather x)
    {
        int dens = (int)Math.Round(Math.Clamp(x.Cloud, 0, 1) * 10);
        if (x.Rain) dens = Math.Max(dens, 9);
        double to = (x.WindDir + 180) % 360;                      // DCS speichert die Richtung, in die der Wind weht
        return D(("atmosphere_type", 0), ("type_weather", 0), ("name", "Campaign"), ("qnh", Math.Round(x.Qnh)), ("season", D(("temperature", Math.Round(x.Temp)))),
            ("clouds", D(("base", Math.Round(x.Base)), ("density", dens), ("thickness", Math.Round(200 + x.Cloud * 1800)), ("iprecptns", x.Rain ? 1 : 0))),
            ("visibility", D(("distance", Math.Min(80000, x.Vis)))), ("fog", D(("thickness", 0), ("visibility", 0))), ("enable_fog", false),
            ("enable_dust", false), ("dust_density", 0), ("groundTurbulence", 0), ("halo", D(("preset", "auto"))), ("cyclones", D()),
            ("wind", D(("atGround", D(("dir", Math.Round(to)), ("speed", Math.Round(x.Wind, 1)))),
                       ("at2000", D(("dir", Math.Round((to + 15) % 360)), ("speed", Math.Round(x.Wind * 1.8, 1)))),
                       ("at8000", D(("dir", Math.Round((to + 30) % 360)), ("speed", Math.Round(x.Wind * 3, 1)))))));
    }

    /// CLSID -> (wsType der Waffe "a,b,c,d", Waffen je Station). Der Hook schreibt die Liste einmal je DCS-Start aus der DCS-Datenbank, hier aufgehoben. null = noch nie gelaufen.
    public static Dictionary<string, (string Ws, int N)>? WsMap()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "DcsCampaign-Weapons.txt"), keep = Path.Combine(Program.Dir, "Weapons-wsType.txt");
        try { if (File.Exists(tmp) && (!File.Exists(keep) || File.GetLastWriteTimeUtc(tmp) > File.GetLastWriteTimeUtc(keep))) { Directory.CreateDirectory(Program.Dir); File.Copy(tmp, keep, true); } }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!File.Exists(keep)) return null;
        var m = new Dictionary<string, (string, int)>();
        foreach (var p in File.ReadLines(keep).Select(l => l.Split(';')).Where(p => p.Length == 6)) m[p[0]] = (string.Join(",", p[1..5]), int.TryParse(p[5], out var n) ? n : 1);
        return m.Count > 0 ? m : null;
    }

    /// Was am Boden startende Flüge der Mission je Platz mitnehmen: Stationen (CLSID -> Anzahl) und Kerosin (kg)
    static Dictionary<int, (Dictionary<string, int> Pyl, double Fuel)> Demand(Ctx x)
    {
        var d = new Dictionary<int, (Dictionary<string, int>, double)>();
        foreach (var (_, _, cat, g) in x.Groups.Where(t => t.Cat is "plane" or "helicopter"))
        {
            if (g["route"] is not Dictionary<string, object?> r || r["points"] is not List<object?> { Count: > 0 } p || p[0] is not Dictionary<string, object?> p0 || p0.GetValueOrDefault("airdromeId") is not int id) continue;
            var (pyl, fuel) = d.TryGetValue(id, out var e) ? e : ([], 0.0);
            foreach (var u in ((List<object?>)g["units"]!).Cast<Dictionary<string, object?>>())
            {
                var pay = (Dictionary<string, object?>)u["payload"]!;
                foreach (var s in ((Dictionary<int, object?>)pay["pylons"]!).Values.Cast<Dictionary<string, object?>>()) { var c = (string)s["CLSID"]!; pyl[c] = pyl.GetValueOrDefault(c) + 1; }
                fuel += (double)pay["fuel"]!;
            }
            d[id] = (pyl, fuel);
        }
        return d;
    }

    /// Platzlager wie die Kampagne: Kerosin = Bestand, Waffen = Klassenbestand je Waffe, die die Staffeln dort laden können (+ was startende Flüge mitnehmen).
    /// Ohne Waffenliste (Hook noch nie gelaufen) oder mit unbekannter Station bleibt die Munition unbegrenzt – sonst stünden Maschinen ohne Waffen da.
    public static Dictionary<string, object?> Warehouses(World w, Dictionary<int, (Dictionary<string, int> Pyl, double Fuel)> demand, Dictionary<string, (string Ws, int N)>? map)
    {
        var ap = new Dictionary<int, object?>();
        w.WhStart = []; w.WhLast = []; w.WhUsed = [];
        foreach (var b in w.Th.Bases)
        {
            var o = w.Base(b.Id);
            var (pyl, fuel) = demand.TryGetValue(b.Id, out var e) ? e : ([], 0.0);
            var amt = new Dictionary<string, int>();
            var start = new Dictionary<string, double> { ["fuel"] = Math.Max(0, o?.Fuel ?? 0) };
            bool known = map != null && o != null;
            if (known)
            {
                foreach (var (c, n) in pyl) if (map!.TryGetValue(c, out var m)) amt[m.Ws] = amt.GetValueOrDefault(m.Ws) + n * m.N; else known = false;
                var stock = Air.Stores(w, o!);
                int ac = w.Sqns.Where(q => q.Base == o!.Id).Sum(q => q.Ac);
                var seen = new HashSet<string>();
                foreach (var q in w.Sqns.Where(q => q.Base == o!.Id))
                    foreach (var (_, c) in w.Th.Payloads.GetValueOrDefault(q.Type)?.SelectMany(p => p.Pylons) ?? [])
                        if (map!.TryGetValue(c, out var m) && seen.Add(m.Ws))      // ponytail: Klassenbestand gilt für jede Waffe der Klasse (AIM-120 und AIM-7 je voll), Rückmeldung kommt über Start/Landung
                        {
                            if (Air.Need(Theater.Wpn(c).Name).Keys.FirstOrDefault() is { } k) amt[m.Ws] = amt.GetValueOrDefault(m.Ws) + (int)(start[$"{k}@{m.Ws}"] = Math.Max(0, stock.GetValueOrDefault(k)));
                            else amt[m.Ws] = amt.GetValueOrDefault(m.Ws) + 2 * ac;      // Tanks/Behälter: 2 je Maschine
                        }
            }
            if (o != null) w.WhStart[b.Id] = known ? start : new() { ["fuel"] = start["fuel"] };
            var weapons = known ? L([.. amt.Where(kv => kv.Value > 0).Select(kv => (object?)D(("wsType", L([.. kv.Key.Split(',').Select(s => (object?)int.Parse(s))])), ("initialAmount", kv.Value)))]) : L();
            ap[b.Id] = D(("coalition", o?.Side switch { Side.Blue => "BLUE", Side.Red => "RED", _ => "NEUTRAL" }),
                ("unlimitedAircrafts", true), ("unlimitedFuel", o == null), ("unlimitedMunitions", !known),
                ("OperatingLevel_Air", 10), ("OperatingLevel_Eqp", 10), ("OperatingLevel_Fuel", 10), ("aircrafts", D()), ("weapons", weapons),
                ("allowHotStart", false), ("dynamicCargo", false), ("dynamicSpawn", false),
                ("diesel", D(("InitFuel", 100))), ("gasoline", D(("InitFuel", 100))), ("jet_fuel", D(("InitFuel", o == null ? 100 : Math.Round(Math.Max(0, o.Fuel) + fuel / 1000, 1)))), ("methanol_mixture", D(("InitFuel", 100))),
                ("periodicity", 30), ("size", 100), ("speed", 16.666666), ("suppliers", D()));
        }
        return D(("airports", ap), ("warehouses", D()));
    }

    static string[] Keys => Splash == null ? ["ResKey_1", "ResKey_2"] : ["ResKey_1", "ResKey_2", "ResKey_3"];

    static void WriteMiz(Ctx x, string token, string path, List<Page> pages)
    {
        var splash = Splash;
        if (splash == null && Program.Opt("Splash")) x.W.Log(x.W.Player, "ato", $"Splash Damage script missing ({Path.Combine(Program.Dir, "scripts")}\\Splash_Damage_*.lua) – mission built without it.");
        var mine = pages.Select(pg => pg.F).ToList();
        var script = Path.Combine(AppContext.BaseDirectory, "mod", "DcsCampaign.lua");
        if (!File.Exists(script)) throw new InvalidOperationException("Mission script missing: " + script);
        var tmp = path + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            void Put(string name, byte[] data) { using var s = zip.CreateEntry(name).Open(); s.Write(data); }
            void Text(string name, string s) => Put(name, new UTF8Encoding(false).GetBytes(s));
            Text("mission", Lua.Write("mission", Mission(x, pages[0].Home)));
            Text("warehouses", Lua.Write("warehouses", Warehouses(x.W, Demand(x), WsMap())));
            Text("l10n/DEFAULT/dictionary", Lua.Write("dictionary", D(("DictKey_descriptionText_1", pages[0].Brief), ("DictKey_descriptionBlueTask_2", string.Join("\n\n", pages.Select(pg => pg.Task))),
                ("DictKey_descriptionRedTask_3", ""), ("DictKey_sortie_4", string.Join(", ", mine.Select(m => $"{m.Callsign} – {m.Task}"))))));
            var res = D(("ResKey_1", "DcsCampaignData.lua"), ("ResKey_2", "DcsCampaign.lua"));
            if (splash != null) { res["ResKey_3"] = Path.GetFileName(splash); Put("l10n/DEFAULT/" + Path.GetFileName(splash), File.ReadAllBytes(splash)); }
            Text("l10n/DEFAULT/mapResource", Lua.Write("mapResource", res));
            var gone = x.W.Objs.Where(o => o.Kind == OK.Bridge && o.Health < 0.3).Select(o => (object?)D(("x", Math.Round(o.X)), ("z", Math.Round(o.Z)))).ToArray();
            Text("l10n/DEFAULT/DcsCampaignData.lua", Lua.Write("DCSCAMP", D(("token", token), ("bridges", L(gone)), ("trains", x.Trains),   // zerstörte Brücken sprengen, Züge aufs Gleis
                    ("player", $"F{mine[0].Id}-1"), ("max", MaxGround[Math.Clamp(x.W.Perf, 0, 2)]), ("maxAir", MaxAir[Math.Clamp(x.W.Perf, 0, 2)]), ("spawn", x.Spawn),                                              // Fahrplan + Bubble
                    ("side", mine[0].Side == Side.Red ? 1 : 2), ("mp", x.W.Mp),
                    ("jam", L([.. x.W.Flights.Where(f => f.Task == "Jammer" && x.Gids.ContainsKey($"F{f.Id}")).Select(f => (object?)D(("n", Gn(f)), ("s", f.Side == Side.Red ? 1 : 2)))])),   // Störer: IADS sieht nur innerhalb Burn-through
                    ("homes", pages.ToDictionary(pg => $"F{pg.F.Id}", pg => (object?)D(("ad", pg.Home.Id), ("x", Math.Round(pg.Home.X)), ("z", Math.Round(pg.Home.Z)), ("c", Countries[Air.SqnOf(x.W, pg.F).Country])))),   // Mehrspieler: KI fliegt verlassene Maschinen heim
                    ("awacs", x.Awacs), ("smoke", x.Smoke), ("craters", x.Craters),
                    ("wh", x.W.WhStart.ToDictionary(kv => kv.Key, kv => (object?)L([.. kv.Value.Keys.Where(k => k.Contains('@')).Select(k => (object?)L([.. k.Split('@')[1].Split(',').Select(s => (object?)int.Parse(s))]))])))))   // Platzlager zum Rückmelden
                + Lua.Write("DCSATC_OPTIONS", D(("traffic", 0), ("tanker", false), ("awacs", false), ("fog", false))));   // DCS-ATC: kein eigener Verkehr, Kampagne stellt Tanker/AWACS
            Put("l10n/DEFAULT/DcsCampaign.lua", File.ReadAllBytes(script));
            if (x.W.Report.Count > 0) Put("KNEEBOARD/IMAGES/Kampagne-0-Lage.png", TextPage(x.W.Report));
            for (int i = 0; i < pages.Count; i++)                                // mehrere Flüge: Seiten mit Rufnamen, alle sehen alle
            {
                var pre = pages.Count == 1 ? "Kampagne" : $"Kampagne-{i + 1} {pages[i].F.Callsign}";
                Put($"KNEEBOARD/IMAGES/{pre}-1-Auftrag.png", TextPage(pages[i].Card));
                Put($"KNEEBOARD/IMAGES/{pre}-2-Karte.png", pages[i].Map);
                Put($"KNEEBOARD/IMAGES/{pre}-3-Daten.png", TextPage(pages[i].Data));
                Put($"KNEEBOARD/IMAGES/{pre}-4-Lineup.png", TextPage(pages[i].Lineup));
                if (TargetPage(x, pages[i].F) is { } tp) Put($"KNEEBOARD/IMAGES/{pre}-5-Ziel.png", tp);
                if (pages[i].Dtc != null) Text($"DTC/{pages[i].DtcName}.dtc", pages[i].Dtc!);   // Kartusche wie vom Missionseditor (unit.DTC verweist darauf)
            }
        }
        File.Move(tmp, path, true);
    }

    // ---------- Briefing und Kniebrett ----------

    static (string Brief, string Task, List<string> Card, List<string> Data, List<string> Lineup) Brief(Ctx x, string start, Base home)
    {
        var (w, f) = (x.W, x.F);
        var pkg = w.Pkgs.FirstOrDefault(p => p.Id == f.Pkg);
        var mates = w.Flights.Where(o => o.Pkg == f.Pkg && o != f).ToList();
        var core = mates.Append(f).FirstOrDefault(o => Air.Attack.Contains(o.Task)) ?? f;
        var b = new List<string> { $"{w.Name} campaign, {x.T0:dd.MM.yyyy HH:mm}", "", "SITUATION" };
        foreach (var ax in w.Axes)
            b.Add(Sim.Front(w, ax) is var (r, bl) ? $"{ax.Name}: Front {w.Nodes[r].Name} (Red) – {w.Nodes[bl].Name} (Blue)" : $"{ax.Name}: broken through");
        b.AddRange(w.Events.Where(e => (e.Side == f.Side || e.Side == Side.None) && e.Kind is "front" or "logistik" && e.T > x.T0.AddHours(-12)).TakeLast(6).Select(e => $"{e.T:HH:mm} {e.Text}"));
        b.Add("");
        b.Add("TASKING");
        foreach (var m in x.Mine) { b.Add((x.Mine.Count > 1 ? m.Callsign + ": " : "") + (w.Pkgs.FirstOrDefault(p => p.Id == m.Pkg)?.Name ?? m.Task)); b.Add(m.Why); }

        var wx = w.Wx;
        var threats = Air.Threats(w, f.Side, 3000).Where(t => x.Wps.Zip(x.Wps.Skip(1)).Any(s => Geo.SegDist(new P(t.X, t.Z), new P(s.First.X, s.First.Z), new P(s.Second.X, s.Second.Z)) < t.R + 10000)).Select(t => t.Name).Distinct().ToList();
        var awacs = x.Ai.Where(o => o.Side == f.Side && o.Task == "AWACS").Select(o => $"{o.Callsign} {o.Freq:0.0}");
        var tankers = x.Ai.Where(o => o.Side == f.Side && o.Task == "Tanker").Select(o => $"{o.Callsign} {o.Freq:0.0} TACAN {o.Tacan}X").Distinct();
        var c = new List<string>
        {
            $"{f.Callsign} – {f.Task} – {f.Alive}× {f.Type}",
            $"Package: {pkg?.Name ?? "-"}",
            $"Target:  {(pkg?.Name.Split(' ', 5).Last() ?? "-")}",
            $"Takeoff {f.Takeoff:HH:mm} ({En.Of(start)}) {home.Name}   TOT {f.Tot:HH:mm}   Land {f.Land:HH:mm}",
            $"Load:    {(f.Load == "" ? "-" : f.Load)}",
            $"Chaff / flares / gun: {Ammo(w, f)}{(TacanOf(w, f) is { Length: > 0 } tcn ? "   TACAN " + tcn : "")}",
            $"AWACS: {(awacs.Any() ? string.Join(", ", awacs) : "none scheduled")}",
            $"Tanker: {(tankers.Any() ? string.Join(", ", tankers) : "none scheduled")}",
            "",
            "RADIO  Ch  Freq    Name (radio 2 only where different)",
        };
        if (JtacOf(w, f) is { } jt)
            c.Insert(8, $"JTAC:   {jt.Cs} {JtacF:0.0} AM, laser {Laser}, {Geo.Mgrs(jt.Pos.X, jt.Pos.Z)}" + (f.Side == Side.Blue ? $" · FAC(A) {jt.Faca} (MQ-9) over the target" : ""));
        var c1 = Comm(w, f, 1); var c2 = Comm(w, f, 2);
        for (int i = 0; i < 20; i += 2)
            c.Add(string.Join("  ", new[] { i, i + 1 }.Select(k => $"{k + 1,2} {c1[k].F,7:0.000} {c1[k].N,-14}{(c2[k] != c1[k] ? $"| {c2[k].F:0.000} {c2[k].N} " : "")}")));
        c.Add("");
        c.Add("Data card, lineup card and target sheet: next kneeboard pages.");
        c.Add("");
        c.Add(threats.Count > 0 ? "⚠ Known air defenses near route: " + string.Join(", ", threats) : "No known air defenses near route.");
        c.Add($"Weather: wind {wx.WindDir:000}°/{wx.Wind / Geo.Kt:0} kt, clouds {wx.Cloud:P0} base {wx.Base / Geo.Ft:0} ft{(wx.Rain ? ", rain" : "")}, visibility {wx.Vis / 1000:0} km");
        c.Add($"        QNH {wx.Qnh * 1.33322:0} hPa / {wx.Qnh / 25.4:0.00} inHg, {wx.Temp:0} °C");
        c.Add("");
        c.Add("Results are reported back to the campaign automatically.");

        // Datenkarte (BMS): Steuerpunkte mit Kurs, Distanz, Höhe, Grundgeschwindigkeit, Zeit, Koordinaten
        var d = new List<string> { $"DATA CARD – {f.Callsign} ({f.Alive}× {f.Type})", "", "STPT Name          HDG  DIST  ALT ft  GS kt  TIME   Coordinates" };
        double total = 0;
        for (int i = 0; i < x.Wps.Count; i++)
        {
            var p = x.Wps[i];
            string leg = "   –     –";
            string gs = "     –";
            if (i > 0)
            {
                var q = x.Wps[i - 1]; double dm = Geo.Dist(q.X, q.Z, p.X, p.Z), sec = (p.T - q.T).TotalSeconds;
                total += dm;
                leg = $"{Geo.Brg(new P(q.X, q.Z), new P(p.X, p.Z)),4:000}  {dm / Geo.NM,4:0}";
                gs = sec > 0 ? $"{dm / sec / Geo.Kt,6:0}" : gs;
            }
            d.Add($"{i,3}  {Cut(p.Name, 12),-12} {leg} {p.Alt / Geo.Ft,7:0} {gs}  {p.T:HH:mm}  {Geo.LLText(p.X, p.Z)}");
        }
        d.Add($"Total {total / Geo.NM:0} NM, {(x.Wps[^1].T - x.Wps[0].T):h\\:mm} h · headings true (grid)");
        if (f.Route.FirstOrDefault(r => r.Kind == "target") is { } tw)
        {
            d.Add("");
            d.Add($"TARGET {Air.TargetName(w, f.TargetKind, f.Target)}{Air.AimText(w, f)}");
            d.Add($"  {Geo.LLText(tw.X, tw.Z)} · MGRS {Geo.Mgrs(tw.X, tw.Z)} · elev {w.Th.Height(tw.X, tw.Z) / Geo.Ft:0} ft");
        }
        d.Add("");
        d.Add($"Takeoff {f.Takeoff:HH:mm} · TOT {f.Tot:HH:mm} · land {f.Land:HH:mm} at {home.Name}");
        d.Add($"Weather: wind {wx.WindDir:000}°/{wx.Wind / Geo.Kt:0} kt · QNH {wx.Qnh / 25.4:0.00} inHg / {wx.Qnh * 1.33322:0} hPa");

        // Lineup (BMS): alle Flüge des Pakets und die Unterstützung
        var l = new List<string> { $"LINEUP – {pkg?.Name ?? f.Callsign}", $"TOT {f.Tot:HH:mm}", "", "Callsign          A/C              Task      TOT    Freq    Load" };
        foreach (var o in mates.Append(f).OrderBy(o => o.Tot).ThenBy(o => o.Task))
            l.Add($"{Cut(o.Callsign + (o == f ? " ◄" : ""), 16),-16}  {o.Alive}× {Cut(o.Type, 13),-13}  {o.Task,-8}  {o.Tot:HH:mm}  {o.Freq,6:0.0}  {Cut(o.Load == "" ? "-" : o.Load, 18)}");
        var sup = x.Ai.Where(o => o.Side == f.Side && o.Task is "AWACS" or "Tanker").ToList();
        if (sup.Count > 0)
        {
            l.Add(""); l.Add("SUPPORT           A/C              Task      Station        Freq    TACAN");
            foreach (var o in sup)
                l.Add($"{Cut(o.Callsign, 16),-16}  {o.Alive}× {Cut(o.Type, 13),-13}  {o.Task,-8}  {o.Route.FirstOrDefault(r => r.Kind == "orbit")?.T ?? o.Tot:HH:mm}–{OrbitEndOf(o):HH:mm}  {o.Freq,6:0.0}  {(o.Tacan > 0 ? $"{o.Tacan}X" : "-")}");
        }
        if (f.Route.FirstOrDefault(r => r.Kind == "push") is { } push) { l.Add(""); l.Add($"Push {push.T:HH:mm} at PUSH ({Geo.LLText(push.X, push.Z)})"); }
        return (string.Join("\n", b), string.Join("\n", c), c, d, l);
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
    static DateTime OrbitEndOf(Flight f) => f.Route.LastOrDefault(r => r.Kind == "orbit2")?.T ?? f.Land;

    /// Zielblatt (wie die BMS-Zielfotos): Skizze der Zielelemente an ihren Plätzen in der Mission, Anflug vom IP, DMPI-Liste mit Koordinaten.
    static byte[]? TargetPage(Ctx x, Flight f)
    {
        var w = x.W;
        if (f.TargetKind is not ("obj" or "unit" or "convoy") || f.Route.FirstOrDefault(r => r.Kind == "target") is not { } tw) return null;
        var els = (f.TargetKind == "obj" ? x.Elems.GetValueOrDefault($"O{f.Target}") : null) ?? [("Target", new P(tw.X, tw.Z))];
        double cx = els.Average(e => e.P.X), cz = els.Average(e => e.P.Z);
        double half = Math.Max(600, els.Max(e => Math.Max(Math.Abs(e.P.X - cx), Math.Abs(e.P.Z - cz))) + 300);
        const int top = 84, side = 640;
        double s = side / (2 * half);
        float ox = (768 - side) / 2f;
        PointF S(P p) => new((float)(ox + (p.Z - cz + half) * s), (float)(top + (cx + half - p.X) * s));
        using var bmp = new Bitmap(768, 1024);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(252, 250, 242));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var big = new Font("Segoe UI", 17, FontStyle.Bold);
        using var small = new Font("Segoe UI", 9);
        using var mono = new Font("Consolas", 11);
        string name = Air.TargetName(w, f.TargetKind, f.Target);
        g.DrawString($"TARGET  {name}{Air.AimText(w, f)}", big, Brushes.Black, 10, 8);
        var k = w.Intel(f.Side).GetValueOrDefault($"{(f.TargetKind == "obj" ? "O" : f.TargetKind == "unit" ? "U" : "C")}{f.Target}");
        g.DrawString($"{f.Callsign} · {f.Task} · TOT {f.Tot:HH:mm}" + (k == null ? "" : $" · intel: {k.Src}, {(int)(x.T0 - k.T).TotalHours} h old{(f.TargetKind == "obj" ? $", condition {k.Str:P0}{(k.Claim ? " (claimed)" : "")}" : "")}"), small, Brushes.DimGray, 12, 44);
        g.FillRectangle(Brushes.White, ox, top, side, side);
        double step = half > 1500 ? 500 : half > 700 ? 200 : 100;
        using var grid = new Pen(Color.FromArgb(225, 225, 225));
        for (double v = -Math.Floor(half / step) * step; v <= half; v += step)
        {
            var a = S(new P(cx + half, cz + v)); var bb = S(new P(cx - half, cz + v));
            g.DrawLine(grid, a, bb);
            var c1 = S(new P(cx + v, cz - half)); var c2 = S(new P(cx + v, cz + half));
            g.DrawLine(grid, c1, c2);
        }
        g.DrawRectangle(Pens.Black, ox, top, side, side);
        var tp = new P(tw.X, tw.Z);
        int ti = f.Route.IndexOf(tw);
        var ipw = f.Route.Take(ti).LastOrDefault(r => r.Kind == "ip") ?? (ti > 0 ? f.Route[ti - 1] : null);
        if (ipw != null)
        {
            double brg = Geo.Brg(new P(ipw.X, ipw.Z), tp);
            var from = S(Geo.Move(new P(cx, cz), brg + 180, half * 0.95)); var to = S(Geo.Move(new P(cx, cz), brg + 180, half * 0.35));
            using var arrow = new Pen(Color.FromArgb(200, 0, 90, 200), 4) { CustomEndCap = new AdjustableArrowCap(5, 6) };
            g.DrawLine(arrow, from, to);
            g.DrawString($"attack heading {brg:000}° from {(ipw.Kind == "ip" ? "IP" : "last STPT")}", small, Brushes.SteelBlue, Math.Clamp(from.X - 60, ox + 4, ox + side - 200), Math.Clamp(from.Y + 6, top + 4, top + side - 20));
        }
        var list = els.Take(12).ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var p = S(list[i].P); string role = Role(list[i].T);
            var col = role == "r" ? Brushes.Firebrick : role == "l" ? Brushes.DarkOrange : Brushes.DimGray;
            if (role == "r") g.FillPolygon(col, new PointF[] { new(p.X, p.Y - 9), new(p.X + 9, p.Y), new(p.X, p.Y + 9), new(p.X - 9, p.Y) });
            else if (role == "l") g.FillPolygon(col, new PointF[] { new(p.X, p.Y - 8), new(p.X + 8, p.Y + 7), new(p.X - 8, p.Y + 7) });
            else g.FillRectangle(col, p.X - 7, p.Y - 7, 14, 14);
            g.DrawString((i + 1).ToString(), small, Brushes.Black, p.X + 9, p.Y - 16);
        }
        g.DrawString("N ↑", big, Brushes.Black, ox + side - 54, top + 6);
        float sb = (float)(step * s);
        g.FillRectangle(Brushes.Black, ox + 10, top + side - 18, sb, 5);
        g.DrawString($"{step:0} m", small, Brushes.Black, ox + 14 + sb, top + side - 24);
        float y = top + side + 14;
        g.DrawString(" #  Element             Coordinates                 MGRS                Elev ft", mono, Brushes.Black, 10, y);
        y += 20;
        for (int i = 0; i < list.Count && y < 1010; i++, y += 18)
        {
            var p = list[i].P;
            g.DrawString($"{i + 1,2}  {Cut(list[i].T, 18),-18}  {Geo.LLText(p.X, p.Z),-26}  {Geo.Mgrs(p.X, p.Z),-18}  {w.Th.Height(p.X, p.Z) / Geo.Ft,6:0}", mono, Brushes.Black, 10, y);
        }
        return Png(bmp);
    }

    static byte[] Png(Bitmap bmp) { using var ms = new MemoryStream(); bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }

    static IEnumerable<string> Wrap(string s, int n)
    {
        while (s.Length > n) { int k = s.LastIndexOf(' ', n); if (k < n / 2) k = n; yield return s[..k]; s = "      " + s[k..].TrimStart(); }
        yield return s;
    }

    static byte[] TextPage(List<string> lines)
    {
        using var bmp = new Bitmap(768, 1024);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(252, 250, 242));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var big = new Font("Segoe UI", 18, FontStyle.Bold);
        using var mono = new Font("Consolas", 12);
        float y = 12;
        g.DrawString(lines[0], big, Brushes.Black, 12, y);
        y += 40;
        foreach (var l in lines.Skip(1).SelectMany(l => Wrap(l, 82)))
        {
            if (y > 1000) break;
            g.DrawString(l, mono, l.StartsWith('⚠') ? Brushes.DarkRed : Brushes.Black, 12, y);
            y += l.Length == 0 ? 8 : 18.5f;
        }
        return Png(bmp);
    }

    /// Lagekarte (Norden oben): Relief, Orte nach Besitzer, bekannte Flugabwehr, Route mit Wegpunktnummern.
    static byte[] MapPage(Ctx x)
    {
        var th = x.W.Th;
        double cx = (x.Wps.Min(p => p.X) + x.Wps.Max(p => p.X)) / 2, cz = (x.Wps.Min(p => p.Z) + x.Wps.Max(p => p.Z)) / 2;
        double s = Math.Min(728 / (x.Wps.Max(p => p.Z) - x.Wps.Min(p => p.Z) + 60000), 984 / (x.Wps.Max(p => p.X) - x.Wps.Min(p => p.X) + 60000));   // Pixel je Meter
        double x1 = cx + 512 / s, z0 = cz - 384 / s;
        PointF S(double px, double pz) => new((float)((pz - z0) * s), (float)((x1 - px) * s));
        using var bmp = new Bitmap(768, 1024);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.Bilinear;
        using (var relief = new Bitmap(new MemoryStream(th.Relief)))
        {
            double k = s * th.HCell, sx = (z0 - th.HZ0) / th.HCell, sy = (th.HX0 - x1) / th.HCell;   // Kartenpixel je Reliefpixel, Ausschnitt im Relief
            double ax = Math.Max(0, sx), ay = Math.Max(0, sy), bx = Math.Min(th.HW, sx + 768 / k), by = Math.Min(th.HH, sy + 1024 / k);   // nur was das Relief hat
            if (bx > ax && by > ay)
                g.DrawImage(relief, new RectangleF((float)((ax - sx) * k), (float)((ay - sy) * k), (float)((bx - ax) * k), (float)((by - ay) * k)),
                    new RectangleF((float)ax, (float)ay, (float)(bx - ax), (float)(by - ay)), GraphicsUnit.Pixel);
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var small = new Font("Segoe UI", 9);
        using var bold = new Font("Segoe UI", 11, FontStyle.Bold);
        foreach (var n in x.W.Nodes)
        {
            var p = S(n.X, n.Z);
            if (p.X < 0 || p.Y < 0 || p.X > 768 || p.Y > 1024) continue;
            using var br = new SolidBrush(n.Owner == Side.Red ? Color.FromArgb(200, 200, 30, 30) : Color.FromArgb(200, 30, 60, 200));
            g.FillEllipse(br, p.X - 3, p.Y - 3, 6, 6);
            if (n.Key == n.Id) g.DrawString(n.Name, small, Brushes.Black, p.X + 4, p.Y - 7);
        }
        using var red = new Pen(Color.FromArgb(220, 190, 0, 0), 2) { DashStyle = DashStyle.Dash };
        foreach (var t in Air.Threats(x.W, x.F.Side, 3000))
        {
            var c = S(t.X, t.Z); float r = (float)(t.R * s);
            g.DrawEllipse(red, c.X - r, c.Y - r, 2 * r, 2 * r);
            g.DrawString(t.Name, small, Brushes.DarkRed, c.X + 3, c.Y + 3);
        }
        var pts = x.Wps.Select(p => S(p.X, p.Z)).ToArray();
        using var route = new Pen(Color.FromArgb(240, 0, 0, 0), 3);
        g.DrawLines(route, pts);
        for (int i = 0; i < pts.Length; i++)
        {
            g.FillEllipse(Brushes.White, pts[i].X - 9, pts[i].Y - 9, 18, 18);
            g.DrawEllipse(Pens.Black, pts[i].X - 9, pts[i].Y - 9, 18, 18);
            g.DrawString(i.ToString(), small, Brushes.Black, pts[i].X - (i > 9 ? 7 : 4), pts[i].Y - 7);
            if (x.Wps[i].Name.Length > 0) g.DrawString(x.Wps[i].Name, bold, Brushes.Black, pts[i].X + 10, pts[i].Y + 4);
        }
        g.FillRectangle(Brushes.White, 0, 0, 768, 26);
        g.DrawString($"THREAT MAP  {x.F.Callsign} – {x.F.Task}   50 km = {50000 * s:0} px   North up", bold, Brushes.Black, 6, 4);
        return Png(bmp);
    }
}
