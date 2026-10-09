using System.Text.RegularExpressions;

namespace DcsCampaign;

/// Luftkrieg: ATO-Planung (Pakete, Routen um Bedrohungen, Timing), Ausführung und Kampfauflösung außerhalb von DCS.
static class Air
{
    public record Perf(double Spd, double Radius, double Ceil, bool Night = true, string Roles = "");   // Roles: was eine Staffel dieses Musters fliegt, wenn der Spieler es beim Start wählt
    const string MR = "CAP,Escort,Sweep,Strike,SEAD,CAS,BAI,Interdiction,OCA", FTR = "CAP,Escort,Sweep", FS = "CAP,Escort,Sweep,Strike", STK = "Strike,Interdiction,OCA,BAI",
        ATK = "CAS,BAI,Interdiction", CB = "CAS,BAI", FA = "CAP,Escort,Strike,SEAD,CAS,BAI,Interdiction", TR = "Transport";
    public static readonly Dictionary<string, Perf> Perfs = new()
    {
        ["FA-18C_hornet"] = new(220, 560, 12000, true, MR), ["F-14B"] = new(230, 700, 13000, true, FS), ["F-15ESE"] = new(230, 900, 13000, true, "Strike,Interdiction,OCA,BAI,CAS,Escort"),
        ["F-16C_50"] = new(225, 560, 13000, true, "SEAD,Strike,CAP,BAI,CAS"), ["F-15C"] = new(235, 750, 14000, true, FTR), ["A-10C_2"] = new(150, 450, 9000, true, ATK),
        ["Su-25T"] = new(170, 400, 8000, false, CB), ["E-3A"] = new(190, 1500, 10000, true, "AWACS"), ["KC135MPRS"] = new(200, 1500, 10000, true, "Tanker"), ["KC-135"] = new(200, 1500, 10000, true, "Tanker"),
        ["Su-27"] = new(235, 800, 14000, true, FTR), ["MiG-29S"] = new(230, 550, 13000, true, FTR), ["Su-30"] = new(235, 900, 14000, true, "Sweep,CAP,Strike,SEAD"), ["MiG-31"] = new(260, 700, 16000, true, "CAP"),
        ["Su-24M"] = new(230, 700, 11000, true, "Strike,Interdiction,OCA,SEAD"), ["Su-25"] = new(170, 400, 8000, false, CB), ["Su-34"] = new(230, 900, 13000, true, STK), ["Tu-22M3"] = new(240, 1600, 12000, true, "Strike"), ["A-50"] = new(190, 1500, 10000, true, "AWACS"),
        ["F-14A-135-GR"] = new(230, 650, 13000, true, FS), ["F-14A-135-GR-Early"] = new(230, 650, 13000, true, FS), ["F-14A-95-GR"] = new(230, 650, 13000, true, FS), ["F-14BU"] = new(230, 700, 13000, true, FS),
        ["F-4E"] = new(230, 600, 12000, true, FA), ["MiG-29A"] = new(230, 500, 13000, true, FTR), ["F-5E-3"] = new(200, 400, 11000, true, "CAP,Escort,CAS,BAI"), ["Mirage 2000-5"] = new(225, 550, 14000, true, FTR),
        ["AH-64D_BLK_II"] = new(70, 180, 3000, true, CB), ["Mi-24P"] = new(75, 200, 3000, false, CB), ["A-10A"] = new(150, 450, 9000, false, ATK), ["J-11A"] = new(235, 800, 14000, true, FTR), ["MiG-29G"] = new(230, 500, 13000, true, FTR),
        ["AH-1W"] = new(70, 160, 3000, false, CB), ["UH-1H"] = new(55, 200, 3000, true, "CSAR"), ["Mi-8MT"] = new(60, 250, 3000, false, "CSAR"),
        ["C-130J-30"] = new(150, 1800, 9000, true, TR), ["C-130"] = new(150, 1800, 9000, true, TR), ["C-17A"] = new(220, 3000, 12000, true, TR), ["IL-76MD"] = new(210, 2500, 12000, true, TR), ["An-26B"] = new(120, 1000, 7500, true, TR),
        // weitere DCS-Module (Spieler) und KI-Muster für eigene Staffeln beim Kampagnenstart
        ["F-4E-45MC"] = new(230, 600, 12000, true, FA), ["AV8BNA"] = new(200, 450, 12000, true, "CAS,BAI,Strike,Interdiction"), ["M-2000C"] = new(225, 500, 15000, true, FS),
        ["JF-17"] = new(220, 550, 13000, true, FA), ["Mirage-F1EE"] = new(220, 450, 13000, true, "CAP,Escort,Sweep,Strike,CAS,BAI"), ["Mirage-F1CE"] = new(220, 450, 13000, true, "CAP,Escort,Sweep,Strike,CAS,BAI"),
        ["Mirage-F1M-EE"] = new(220, 450, 13000, true, "CAP,Escort,Sweep,Strike,CAS,BAI"), ["MiG-21Bis"] = new(230, 350, 13000, false, "CAP,Escort,Sweep,CAS,BAI"), ["AJS37"] = new(230, 500, 11000, true, "Strike,Interdiction,BAI,CAS"),
        ["MiG-29 Fulcrum"] = new(230, 500, 13000, true, FTR), ["A-10C"] = new(150, 450, 9000, true, ATK), ["Su-33"] = new(235, 700, 14000, true, FTR),
        ["Ka-50"] = new(75, 200, 3000, false, CB), ["Ka-50_3"] = new(75, 200, 3000, true, CB), ["SA342M"] = new(65, 150, 3000, false, CB), ["SA342L"] = new(65, 150, 3000, false, CB),
        ["SA342Mistral"] = new(65, 150, 3000, false, CB), ["SA342Minigun"] = new(65, 150, 3000, false, CB), ["OH58D"] = new(55, 120, 3000, true, CB), ["CH-47Fbl1"] = new(70, 300, 3000, true, "Transport,CSAR"),
        ["L-39ZA"] = new(170, 300, 10000, false, CB), ["L-39C"] = new(170, 300, 10000, false, CB), ["C-101CC"] = new(170, 300, 10000, false, CB), ["C-101EB"] = new(170, 300, 10000, false, CB),
        ["MB-339A"] = new(170, 300, 10000, false, CB), ["MB-339APAN"] = new(170, 300, 10000, false, CB), ["F-86F Sabre"] = new(230, 350, 13000, false, "CAP,Escort,Sweep,CAS"),
        ["MiG-15bis"] = new(230, 300, 13000, false, FTR), ["MiG-19P"] = new(240, 300, 15000, false, FTR),
        ["Tornado IDS"] = new(240, 700, 11000, true, STK), ["Tornado GR4"] = new(240, 700, 11000, true, STK), ["MiG-23MLD"] = new(240, 450, 13000, true, FTR), ["MiG-25PD"] = new(280, 600, 18000, true, "CAP"),
        ["MiG-27K"] = new(230, 450, 11000, true, "Strike,BAI,CAS,Interdiction"), ["Su-17M4"] = new(230, 450, 11000, false, "Strike,BAI,CAS,Interdiction"), ["F-117A"] = new(240, 900, 12000, true, "Strike,Interdiction,OCA"),
        ["F-16A"] = new(225, 500, 13000, true, "CAP,Escort,Strike,CAS,BAI"), ["Ka-52"] = new(80, 200, 3000, true, CB), ["Mi-24V"] = new(75, 200, 3000, false, CB), ["IL-78M"] = new(210, 2000, 11000, true, "Tanker"),
    };
    /// Nutzlast je Transporter (t), 0 = kein Transporter.
    public static double Lift(string t) => t switch { "C-17A" => 70, "IL-76MD" => 40, "C-130J-30" => 20, "C-130" => 18, "An-26B" => 5, _ => 0 };
    /// Hubschrauber: fliegen im Tiefflug, ohne SEAD/Jagdschutz; Rettung (CSAR) nur mit Kabine.
    public static bool Helo(string t) => t is "AH-64D_BLK_II" or "Mi-24P" or "AH-1W" or "UH-1H" or "Mi-8MT" or "Ka-50" or "Ka-50_3" or "SA342M" or "SA342L" or "SA342Mistral" or "SA342Minigun" or "OH58D" or "CH-47Fbl1" or "Ka-52" or "Mi-24V";
    /// Muster mit Cockpit in DCS – alle anderen fliegt nur die KI
    public static bool Flyable(string t) => Fly.Contains(t);
    static readonly HashSet<string> Fly = ["FA-18C_hornet", "F-14B", "F-14BU", "F-14A-135-GR", "F-14A-135-GR-Early", "F-14A-95-GR", "F-15ESE", "F-16C_50", "F-15C", "A-10C_2", "A-10C", "A-10A", "Su-25T", "Su-25",
        "Su-27", "J-11A", "Su-33", "MiG-29A", "MiG-29S", "MiG-29G", "MiG-29 Fulcrum", "F-5E-3", "F-4E-45MC", "AV8BNA", "M-2000C", "JF-17", "Mirage-F1EE", "Mirage-F1CE", "Mirage-F1M-EE", "MiG-21Bis", "AJS37",
        "L-39ZA", "L-39C", "C-101CC", "C-101EB", "MB-339A", "MB-339APAN", "F-86F Sabre", "MiG-15bis", "MiG-19P",
        "AH-64D_BLK_II", "UH-1H", "Mi-24P", "Mi-8MT", "Ka-50", "Ka-50_3", "SA342M", "SA342L", "SA342Mistral", "SA342Minigun", "OH58D", "CH-47Fbl1", "C-130J-30"];
    public static bool Cabin(string t) => t is "UH-1H" or "Mi-8MT" or "Mi-24P" or "Mi-24V" or "CH-47Fbl1";
    /// Hubschrauber kommen nicht über Gebirge über ihrer Gipfelhöhe (Kaukasus-Hauptkamm): Direktweg vom Platz zum Ziel.
    static bool Reach(World w, Sqn q, P tgt)
    {
        if (!Helo(q.Type)) return true;
        var b = new P(BaseOf(w, q).X, BaseOf(w, q).Z);
        int k = (int)(Geo.Dist(b, tgt) / 2000) + 1;                      // lückenlos: alle 2 km, Radius 2 km
        for (int i = 0; i <= k; i++) { var p = Geo.Lerp(b, tgt, i / (double)k); if (w.Th.Peak(p.X, p.Z, 2000) + 150 > PerfOf(q.Type).Ceil) return false; }
        return true;
    }
    public static Perf PerfOf(string t) => Perfs.GetValueOrDefault(t) ?? new(220, 600, 12000);

    public static readonly string[] BlueCs = ["Enfield", "Springfield", "Uzi", "Colt", "Dodge", "Ford", "Chevy", "Pontiac"];
    public static readonly string[] Attack = ["Strike", "OCA", "DEAD", "CAS", "BAI", "Interdiction", "Antiship"];
    static readonly string[] A2A = ["CAP", "Sweep", "Escort"];

    // ---------- Bewaffnung ----------

    static readonly string[] Fighter = ["AIM-120", "R-77", "AIM-54", "R-27ER", "R-27", "AIM-7", "R-33", "R-40", "AIM-9", "R-73"];
    static readonly string[] Bombs = ["MK-8", "GBU", "AGM", "KH-", "KAB", "FAB", "CBU", "BETAB", "RBK", "B-8", "S-8", "UB-", "LAU-6", "ZUNI", "OFAB", "S-25", "S-24"];
    static readonly Dictionary<string, (int[] Tasks, string[] Want, string[] Avoid)> Rules = new()
    {
        ["CAP"] = ([11, 10, 19, 18], Fighter, Bombs),
        ["Sweep"] = ([19, 11, 10, 18], Fighter, Bombs),
        ["Escort"] = ([18, 11, 19, 10], Fighter, Bombs),
        ["Strike"] = ([33, 32, 34], ["GBU-31", "GBU-38", "GBU-32", "KAB-1500", "KAB-500", "GBU-10", "GBU-24", "GBU-12", "KH-29", "MK-84", "FAB-500", "MK-83", "FAB-250"], ["AGM-88", "KH-58", "KH-31P"]),
        ["DEAD"] = ([33, 32, 29], ["GBU-31", "GBU-38", "AGM-154", "KAB-500", "KH-29", "GBU-12", "MK-84", "FAB-500"], []),
        ["OCA"] = ([34, 32, 33], ["BETAB", "GBU-31", "MK-84", "FAB-500", "GBU-38", "MK-82", "FAB-250"], ["AGM-88", "KH-58"]),
        ["CAS"] = ([31, 32], ["AGM-65", "CBU-97", "CBU-87", "CBU-99", "VIKHR", "AGM-114", "9M114", "BGM-71", "KH-25ML", "KH-29", "GBU-12", "RBK", "MK-82", "MK-20", "B-8", "S-8"], ["AGM-88", "KH-58", "KH-31P", "AGM-84"]),
        ["BAI"] = ([31, 32], ["AGM-65", "CBU-97", "CBU-87", "AGM-114", "9M114", "BGM-71", "KH-25ML", "KH-29", "GBU-12", "RBK", "MK-82", "FAB-250"], ["AGM-88", "KH-58", "AGM-84"]),
        ["Interdiction"] = ([32, 31, 33], ["CBU-97", "CBU-87", "MK-82", "GBU-38", "GBU-12", "AGM-65", "RBK", "FAB-250", "FAB-500"], ["AGM-88", "AGM-84"]),
        ["Antiship"] = ([30, 33], ["AGM-84D", "KH-35", "KH-31A", "AGM-84A", "KH-22"], []),
        ["SEAD"] = ([29], ["AGM-88", "KH-58", "KH-31P", "KH-25MPU"], []),
        ["Jammer"] = ([29], ["ALQ-184", "ALQ-131", "ALQ-119", "SORBTSIYA", "MPS-410", "L005", "AGM-88", "KH-58"], Bombs),
        ["Recon"] = ([17, 11], [], Bombs),
    };

    static readonly string[] Cheap = ["MK-82", "FAB-250", "FAB-500", "MK-83", "MK-84", "RBK", "B-8", "S-8", "LAU-61", "LAU-68", "ZUNI"];

    /// Beladung nach Auftrag; spar = Platzlager knapp: ungelenkte Bomben/Raketen statt Präzisionswaffen.
    public static string ChooseLoad(Theater th, string type, string task, bool spar = false)
    {
        if (!th.Payloads.TryGetValue(type, out var list) || list.Count == 0 || !Rules.TryGetValue(task, out var r)) return "";
        var want = spar && Attack.Contains(task) ? Cheap : r.Want;
        Payload? best = null; double bs = double.MinValue;
        foreach (var p in list)
        {
            var n = p.Name.ToUpperInvariant();
            if (n.Contains("CARRIER") || n.Contains("CLEAN") || n.Contains("FERRY")) continue;
            double s = p.Tasks.Intersect(r.Tasks).Any() ? 50 : 0;
            for (int i = 0; i < want.Length; i++) if (n.Contains(want[i].ToUpperInvariant())) { s += 100 - i * 6; break; }
            if (r.Avoid.Any(a => n.Contains(a.ToUpperInvariant()))) s -= 120;
            if (n.Contains("FUEL")) s += 3;
            if (s > bs) { bs = s; best = p; }
        }
        return best?.Name ?? "";
    }

    static int Count(string load, string pattern)
    {
        int c = 0;
        foreach (Match m in Regex.Matches(load.ToUpperInvariant(), "(" + pattern + @")[\w-]*\s*[*X]?\s*(\d*)")) c += m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 1;
        return c;
    }
    public static int A2aCount(string load) => Count(load, "AIM-120|AIM-7|AIM-9|AIM-54|R-77|R-27|R-73|R-60|R-33|R-40|R-37");
    /// Zustand des Ziels (Objekt: Zustand, Verband: Stärke), null = kein Ziel
    public static double? TgtState(World w, Flight f) => f.TargetKind switch { "obj" => w.Objs.FirstOrDefault(o => o.Id == f.Target)?.Health, "unit" => w.Units.FirstOrDefault(u => u.Id == f.Target)?.Str,
        "convoy" => w.Convoys.FirstOrDefault(c => c.Id == f.Target) is { } c ? (double?)((double)c.Trucks / Math.Max(1, c.Trucks0)) : null, _ => null };
    public static int ArmCount(string load) => Count(load, "AGM-88|KH-58|KH-31P|KH-25MPU");

    // ---------- Waffenbestand je Flugplatz (wie BMS): Start verbraucht, Landung bringt Ungenutztes zurück, Nachschub aus dem Material des Platzes ----------
    public static readonly (string C, string Pat)[] ArmClasses =
    [
        ("AAM-R", "AIM-120|AIM-7|AIM-54|R-77|R-27R|R-27ER|R-33|R-40R|R-37"), ("AAM-IR", "AIM-9|R-73|R-60|R-27T|R-27ET|R-40T|MAGIC"),
        ("ARM", "AGM-88|KH-58|KH-31P|KH-25MPU"), ("PGM", "GBU|AGM-65|AGM-154|AGM-84E|AGM-84H|KH-29|KH-25ML|KH-59|KAB|S-25L"), ("AShM", "AGM-84D|AGM-84A|KH-31A|KH-35|KH-22"),
        ("ATGM", "AGM-114|VIKHR|9M127|9M120|9M114|ATAKA|SHTURM|TOW"), ("Bombs", "MK-8|MK-20|CBU|FAB|OFAB|BETAB|RBK|ZAB|SAB|KMGU"), ("Rockets", "HYDRA|LAU-|ZUNI|S-8|S-13|S-24|S-5|B-8|B-13|UB-"),
    ];
    /// Waffen je Maschine nach Klasse
    public static Dictionary<string, int> Need(string load) => ArmClasses.Select(a => (a.C, N: Count(load, a.Pat))).Where(t => t.N > 0).ToDictionary(t => t.C, t => t.N);
    /// Soll eines Platzes: drei Einsätze je Maschine der dort liegenden Staffeln, nach ihren Rollen
    public static Dictionary<string, int> Full(World w, Obj b)
    {
        var r = new Dictionary<string, int>();
        void Add(string c, int n) => r[c] = r.GetValueOrDefault(c) + n;
        foreach (var q in w.Sqns.Where(q => q.Base == b.Id))
        {
            int n = Math.Max(q.Size, q.Ac) * 3;
            bool Has(params string[] k) => k.Any(q.Roles.Contains);
            if (Has("CAP", "Escort", "Sweep")) { Add("AAM-R", 4 * n); Add("AAM-IR", 2 * n); }
            if (Has("SEAD")) Add("ARM", 2 * n);
            if (Has("Strike", "BAI", "Interdiction", "OCA")) { Add("PGM", 2 * n); Add("Bombs", 4 * n); Add("AShM", n / 2); }
            if (Has("CAS")) { Add(Helo(q.Type) ? "ATGM" : "PGM", 4 * n); Add("Rockets", 2 * n); Add("Bombs", 2 * n); }
            Add("AAM-IR", n);                                               // Selbstschutz
        }
        return r;
    }
    public static Dictionary<string, int> Stores(World w, Obj b) => b.Arms ??= Full(w, b);
    static bool Fits(World w, Obj b, string load, int n) => Need(load).All(kv => Stores(w, b).GetValueOrDefault(kv.Key) >= kv.Value * n);
    /// Was fehlt: „PGM 4/8, ARM 0/4“
    public static string Short(World w, Obj b, string load, int n) =>
        string.Join(", ", Need(load).Where(kv => Stores(w, b).GetValueOrDefault(kv.Key) < kv.Value * n).Select(kv => $"{kv.Key} {Stores(w, b).GetValueOrDefault(kv.Key)}/{kv.Value * n}"));
    /// Beladung, für die der Platz die Waffen hat: Wunschbeladung, sonst die sparsame (Freifallbomben), sonst die Wunschbeladung (Start scheitert dann)
    static string PickLoad(World w, Sqn q, string task, int n, string? type = null)
    {
        var b = BaseOf(w, q);
        var l = ChooseLoad(w.Th, type ?? q.Type, task, b.Stock < 60);
        if (Fits(w, b, l, n)) return l;
        var c = ChooseLoad(w.Th, type ?? q.Type, task, true);
        return Fits(w, b, c, n) ? c : l;
    }

    public static (double Struct, double Veh) Punch(string load)
    {
        var l = load.ToUpperInvariant();
        bool Any(params string[] k) => k.Any(l.Contains);
        double s = Any("GBU-31", "GBU-38", "GBU-32", "KAB", "GBU-10", "GBU-12", "GBU-24", "KH-29", "AGM-154") ? 0.3 : Any("MK-84", "FAB-500", "MK-83", "BETAB", "FAB-250", "MK-82", "FAB-100") ? 0.16 : 0.05;
        double v = Any("AGM-65", "KH-25ML", "KH-29", "VIKHR", "AGM-114", "9M114", "BGM-71") ? 2.0 : Any("CBU-97", "CBU-87", "RBK", "CBU-99", "MK-20") ? 2.5 : Any("MK-82", "FAB-250", "GBU-12", "GBU-38", "FAB-100") ? 1.2 : Any("B-8", "S-8", "LAU-61", "LAU-68", "ZUNI", "S-25") ? 0.8 : 0.3;
        return (s, v);
    }
    static bool Laser(string load) => new[] { "GBU-10", "GBU-12", "GBU-16", "GBU-24", "KAB-500L", "KAB-1500L", "KH-29L", "KH-25ML" }.Any(load.ToUpperInvariant().Contains);

    // ---------- Bedrohungskarte und Routen ----------

    const double G0X = -480000, G0Z = 180000, GC = 8000;
    const int GH = 70, GW = 102;
    static P Cell(int i) => new(G0X + (i / GW + 0.5) * GC, G0Z + (i % GW + 0.5) * GC);
    static int Idx(P p) => Math.Clamp((int)((p.X - G0X) / GC), 0, GH - 1) * GW + Math.Clamp((int)((p.Z - G0Z) / GC), 0, GW - 1);
    static double Leth(string sys) => sys switch { "SA-10" or "Patriot" => 8, "SA-11" => 6, "Hawk" => 5, "SA-6" or "NASAMS" => 4, _ => 2 };

    public record Threat(string Name, double X, double Z, double R, double Leth);

    /// Wo eine Seite ein Objekt vermutet: eigenes = wahr, feindliches = letzte Sichtung (SAMs ziehen um).
    public static P Seen(World w, Side s, Obj o) => o.Side != s && w.Intel(s).TryGetValue($"O{o.Id}", out var k) ? new(k.X, k.Z) : new(o.X, o.Z);

    /// Bekannte gegnerische Flugabwehr, die eine Seite auf dieser Höhe fürchten muss.
    public static List<Threat> Threats(World w, Side s, double alt)
    {
        var know = w.Intel(s);
        var l = new List<Threat>();
        foreach (var o in w.Objs.Where(o => o.Side == s.Enemy() && o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles > 0 && know.ContainsKey($"O{o.Id}")))
            if (alt < o.MaxAlt) l.Add(new(o.Name, know[$"O{o.Id}"].X, know[$"O{o.Id}"].Z, o.Range * (alt < 1500 ? 0.45 : 1), Leth(o.Sys)));   // bekannte, nicht wahre Stellung
        foreach (var u in w.Units.Where(u => u.Side == s.Enemy() && u.Kind == UK.Shorad))
            if (alt < 3800 && know.TryGetValue($"U{u.Id}", out var k)) l.Add(new($"Air defense {u.Name}", k.X, k.Z, 9000, 1.5));
        foreach (var c in w.Convoys.Where(c => c.Navy && c.Side == s.Enemy() && know.ContainsKey($"C{c.Id}")))   // Schiffsflugabwehr
            l.Add(new(c.Name, know[$"C{c.Id}"].X, know[$"C{c.Id}"].Z, Sim.NavyRange(c), 2));
        return l;
    }

    /// Bekannte feindliche Jäger: Patrouillen (Sichtung < 30 min) und Fliegerhorste mit Jagdstaffeln – zum Umfliegen und für Sweep/Begleitschutz.
    public static List<Threat> AirThreats(World w, Side s)
    {
        var know = w.Intel(s);
        var l = w.Flights.Where(f => f.Side != s && A2A.Contains(f.Task) && Up(f) && know.TryGetValue($"F{f.Id}", out var k) && k.T > w.Time.AddMinutes(-30))
            .Select(f => new Threat($"{f.Alive}× fighters", know[$"F{f.Id}"].X, know[$"F{f.Id}"].Z, 40000, 3)).ToList();
        l.AddRange(w.Objs.Where(o => o.Kind == OK.Airbase && o.Side != s && o.Health > 0.35 && w.Sqns.Any(q => q.Base == o.Id && q.Roles.Contains("CAP") && q.Ac > 0))
            .Select(o => new Threat($"Fighter base {o.Name}", o.X, o.Z, 25000, 2)));
        return l;
    }

    public static double[] Grid(List<Threat> th)
    {
        var g = new double[GW * GH];
        for (int i = 0; i < g.Length; i++)
        {
            var c = Cell(i);
            double cost = 1;
            foreach (var t in th)
            {
                double d = Geo.Dist(c.X, c.Z, t.X, t.Z);
                if (d < t.R + GC) cost += t.Leth * 3 * (1.2 - Math.Min(1, d / (t.R + GC)) * 0.6);
            }
            g[i] = cost;
        }
        return g;
    }

    /// A* über das Bedrohungsraster; Rückgabe: Zwischenpunkte (ohne a und b), vereinfacht.
    public static List<P> Route(double[] g, P a, P b)
    {
        int s = Idx(a), t = Idx(b);
        var dist = new Dictionary<int, double> { [s] = 0 };
        var prev = new Dictionary<int, int>();
        var pq = new PriorityQueue<int, double>();
        pq.Enqueue(s, 0);
        double H(int i) { var c = Cell(i); return Geo.Dist(c.X, c.Z, b.X, b.Z) / GC; }
        while (pq.TryDequeue(out var n, out _))
        {
            if (n == t) break;
            int r = n / GW, c = n % GW;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int rr = r + dr, cc = c + dc;
                    if (rr < 0 || cc < 0 || rr >= GH || cc >= GW) continue;
                    int m = rr * GW + cc;
                    double nd = dist[n] + (dr != 0 && dc != 0 ? 1.4142 : 1) * (g[n] + g[m]) / 2;
                    if (dist.TryGetValue(m, out var od) && od <= nd) continue;
                    dist[m] = nd; prev[m] = n;
                    pq.Enqueue(m, nd + H(m));
                }
        }
        var cells = new List<P>();
        if (prev.ContainsKey(t)) for (int v = t; v != s; v = prev[v]) cells.Add(Cell(v));
        cells.Reverse();
        var pts = new List<P> { a };
        pts.AddRange(cells.Take(Math.Max(0, cells.Count - 1)));
        pts.Add(b);
        var keep = Simplify(pts, 7000);
        return keep.Skip(1).Take(keep.Count - 2).ToList();
    }

    static List<P> Simplify(List<P> p, double tol)
    {
        if (p.Count < 3) return p;
        double md = 0; int mi = 0;
        for (int i = 1; i < p.Count - 1; i++) { double d = Geo.SegDist(p[i], p[0], p[^1]); if (d > md) { md = d; mi = i; } }
        if (md < tol) return [p[0], p[^1]];
        var l = Simplify(p.Take(mi + 1).ToList(), tol);
        var r = Simplify(p.Skip(mi).ToList(), tol);
        return l.Take(l.Count - 1).Concat(r).ToList();
    }

    public static double Exposure(List<Threat> th, P a, P b)
    {
        double e = 0, len = Geo.Dist(a, b);
        int n = Math.Max(1, (int)(len / 4000));
        for (int i = 0; i <= n; i++)
        {
            var p = Geo.Lerp(a, b, (double)i / n);
            foreach (var t in th) if (Geo.Dist(p.X, p.Z, t.X, t.Z) < t.R) e += t.Leth * len / n / 1000;
        }
        return e;
    }

    // ---------- Planung ----------

    record Station(string Name, P Pos, double Alt, string Task, bool Night = false);

    /// CAP: über der eigenen Frontstellung einer Achse (30 km zurück) oder fest (Startlage); Night = auch nachts besetzt. Nie im Schirm bekannter SAMs.
    static IEnumerable<Station> CapStations(World w, Side s) => RawCaps(w, s).Select(st => st with { Pos = Clear(w, s, st.Pos, st.Alt) });
    static IEnumerable<Station> RawCaps(World w, Side s)
    {
        foreach (var (name, ax, night, side, x, z) in w.S.Caps)
        {
            if (ax < 0) { if (side == s) yield return new Station($"CAP {name}", new(x, z), 8000, "CAP", night); continue; }
            if (ax >= w.Axes.Count || Sim.Front(w, w.Axes[ax]) is not var (rf, bf)) continue;
            var own = w.Nodes[s == Side.Red ? rf : bf]; var en = w.Nodes[s == Side.Red ? bf : rf];
            double brg = Geo.Brg(new P(en.X, en.Z), new P(own.X, own.Z));
            yield return new Station($"CAP {name}", Geo.Move(new P(own.X, own.Z), brg, 30000), 8000, "CAP", night);
        }
    }

    static IEnumerable<Station> SupportStations(World w, Side s) => w.S.Support.Where(t => t.S == s).Select(t => new Station(t.Name, Clear(w, s, new(t.X, t.Z), t.Alt), t.Alt, t.Task));

    /// Station aus den Ringen bekannter SAM-Stellungen schieben (5 km Abstand): Jäger, AWACS und Tanker kreisen nicht im Schirm des Gegners.
    static P Clear(World w, Side s, P p, double alt)
    {
        var th = Threats(w, s, alt);
        for (int i = 0; i < 8 && th.FirstOrDefault(t => Geo.Dist(t.X, t.Z, p.X, p.Z) < t.R + 5000) is { } t; i++)
            p = Geo.Move(new P(t.X, t.Z), Geo.Brg(new P(t.X, t.Z), p), t.R + 5000);
        return p;
    }

    public static int Free(World w, Sqn q) => q.Ac - w.Flights.Where(f => f.Sqn == q.Id && f.Status == "geplant").Sum(f => f.N);   // Ac = Maschinen am Boden, gestartete sind schon abgezogen
    static Obj BaseOf(World w, Sqn q) => w.Objs.First(o => o.Id == q.Base);
    /// ai: die KI lässt je spielbarer Staffel zwei Maschinen für die Auftragsliste des Spielers stehen.
    static bool Usable(World w, Sqn q, int n, bool ai = true) { var b = BaseOf(w, q); return Open(b) && b.Side == q.Side && b.Fuel > PolOf(q.Type) * n && b.Stock > 0.4 * n && Free(w, q) >= n + (ai && q.Player ? 2 : 0); }

    /// Platzlager je Maschine und Einsatz: Sprit (t) nach Größe, Munition (t) nach Auftrag.
    public static double PolOf(string type) => Helo(type) ? 1 : Lift(type) >= 40 ? 20 : Lift(type) > 0 ? 6 : type is "E-3A" or "A-50" or "KC135MPRS" or "KC-135" or "Tu-22M3" ? 25 : type is "Su-24M" or "Su-34" or "F-15ESE" or "Su-30" or "F-14B" or "MiG-31" or "F-14A-135-GR" or "F-4E" ? 6 : 4;
    public static double MunOf(string task) => Attack.Contains(task) ? 1.5 : task == "SEAD" ? 0.8 : A2A.Contains(task) ? 0.3 : 0;

    public static void Plan(World w)
    {
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            Commander(w, s);
            foreach (var st in SupportStations(w, s)) Cover(w, s, st, st.Task == "AWACS" ? 300 : 240, st.Task == "AWACS" ? (4, 24) : (6, 22));
            foreach (var st in CapStations(w, s)) Cover(w, s, st, 90, w.Daylight || st.Night ? (0, 24) : (-1, -1));
            Offensive(w, s);
            Airlift(w, s);
            Assault(w, s);
        }
    }

    public static readonly string[] Phases = ["", "Air superiority", "SEAD corridors", "Interdiction", "Ground offensive"];
    static readonly Dictionary<string, double>[] PhaseW =
    [
        [],
        new() { ["OCA"] = 1.6, ["DEAD"] = 1.0, ["Strike"] = 0.8, ["CAS"] = 1.0, ["BAI"] = 0.8, ["Interdiction"] = 0.7, ["Recon"] = 0.8, ["Antiship"] = 1.0 },
        new() { ["OCA"] = 0.8, ["DEAD"] = 1.8, ["Strike"] = 1.1, ["CAS"] = 1.0, ["BAI"] = 0.8, ["Interdiction"] = 0.8, ["Recon"] = 1.0, ["Antiship"] = 1.0 },
        new() { ["OCA"] = 0.7, ["DEAD"] = 1.0, ["Strike"] = 1.3, ["CAS"] = 1.0, ["BAI"] = 1.3, ["Interdiction"] = 1.7, ["Recon"] = 1.0, ["Antiship"] = 1.0 },
        new() { ["OCA"] = 0.6, ["DEAD"] = 0.9, ["Strike"] = 0.8, ["CAS"] = 1.6, ["BAI"] = 1.4, ["Interdiction"] = 1.0, ["Recon"] = 0.8, ["Antiship"] = 1.0 },
    ];

    /// Kommandeur: Phase aus dem eigenen Lagebild (Jäger laut Gefechtsordnung, bekannte SAMs, aufgeklärte Bodenkräfte), mit Begründung.
    public static void Commander(World w, Side s)
    {
        var c = w.CmdOf(s);
        var know = w.Intel(s);
        int Fighters(Side x) => w.Sqns.Where(q => q.Side == x && q.Roles.Contains("CAP")).Sum(q => q.Ac + q.Repair.Count + w.Flights.Where(f => f.Sqn == q.Id && Up(f)).Sum(f => f.Alive));   // vorhandene Maschinen inkl. in der Luft
        int ef = Fighters(s.Enemy()), of = Fighters(s);
        int sams = w.Objs.Count(o => o.Side == s.Enemy() && o.Kind == OK.Sam && o.Health > 0.3 && know.ContainsKey($"O{o.Id}"));
        double og = w.Units.Where(u => u.Side == s).Sum(u => u.Str * Sim.Pow[u.Kind]);
        double eg = w.Units.Where(u => u.Side != s && know.ContainsKey($"U{u.Id}")).Sum(u => know[$"U{u.Id}"].Str * Sim.Pow[u.Kind]);
        var (p, why) = w.Time < w.Start.AddHours(24) ? (1, "Opening: win control of the air first")
            : ef > of * 1.2 ? (1, $"Enemy has approx. {ef} fighters against our {of}")
            : sams >= 3 ? (2, $"{sams} known SAM sites")
            : og < eg * 1.2 ? (3, $"Ground forces {og:0.0} vs. {eg:0.0} detected: cut enemy supply first")
            : (4, $"Air superiority secured, ground forces {og:0.0} vs. {eg:0.0} detected");
        if (p >= 3 && w.Axes.FirstOrDefault(a => a.OpSide == s && a.Op == "Angriff") is { } oa) (p, why) = (4, $"own offensive on axis {oa.Name}");
        else if (p >= 3 && w.Axes.FirstOrDefault(a => a.OpSide == s && a.Op == "Bereitstellung") is { } ob) (p, why) = (3, $"Staging on axis {ob.Name}: cut enemy supply first");
        if (c.Set > 0) (p, why) = (c.Set, "set by the commander");
        else if (p != c.Phase && w.Time < c.Since.AddHours(12)) (p, why) = (c.Phase, c.Why.Split(" · ")[0]);   // eine Phase hält mindestens 12 h
        if (p != c.Phase) { w.Log(s, "ato", $"Commander {s.De()}: phase {p} {Phases[p]} – {why}."); c.Since = w.Time; }
        c.Phase = p; c.Why = why;
    }

    static void Cover(World w, Side s, Station st, double minutes, (int From, int To) hours)
    {
        if (hours.From < 0) return;
        var key = "station:" + st.Name;
        var have = w.Flights.Where(f => f.Side == s && f.TargetKind == key && f.Status != "gestrichen" && f.Status != "verloren").ToList();
        var start = have.Count == 0 ? w.Time.AddMinutes(40) : have.Max(f => OrbitEnd(f));
        if (start < w.Time.AddMinutes(40)) start = w.Time.AddMinutes(40);
        if (start > w.Time.AddHours(3)) return;
        if (start.Hour < hours.From || start.Hour >= hours.To) return;
        var q = w.Sqns.Where(q => q.Side == s && q.Roles.Contains(st.Task) && Usable(w, q, st.Task == "CAP" ? 2 : 1))
            .OrderBy(q => Geo.Dist(BaseOf(w, q).X, BaseOf(w, q).Z, st.Pos.X, st.Pos.Z)).FirstOrDefault(q => Geo.Dist(BaseOf(w, q).X, BaseOf(w, q).Z, st.Pos.X, st.Pos.Z) < PerfOf(q.Type).Radius * 1000);
        if (q == null) return;
        var p = NewPkg(w, s, st.Task, key, -1, st.Name, st.Task == "CAP" ? $"{st.Name}: protect the airspace over the front" : $"{st.Name}: continuous support");
        var f = NewFlight(w, p, q, st.Task, st.Task == "CAP" ? 2 : 1, st.Pos, start, st.Alt, minutes);
        f.TargetKind = key;
    }

    static DateTime OrbitEnd(Flight f) => f.Route.LastOrDefault(r => r.Kind == "orbit2")?.T ?? f.Tot;

    public record Cand(string Task, string Kind, int Id, P Pos, double Score, string Why);

    static void Offensive(World w, Side s)
    {
        int busy = w.Pkgs.Count(p => p.Side == s && Attack.Contains(p.Task) && p.Flights.Any(id => w.Flights.FirstOrDefault(f => f.Id == id) is { Done: false } f && f.Status != "gestrichen"));
        int max = w.Daylight ? 6 : 2;
        if (busy >= max) return;
        if (w.Wx.Cloud > 0.85 && w.Wx.Base < 900 || w.Wx.Vis < 5000) { w.CmdOf(s).Why += " · Weather: no new strikes"; return; }
        var targeted = Targeted(w, s);
        // Ausgewogener Zielmix wie im BMS-ATO: jede schon laufende Angriffsart derselben Zielklasse halbiert, drittelt … den Wert der nächsten
        var act = w.Pkgs.Where(p => p.Side == s && Attack.Contains(p.Task) && p.Flights.Any(id => w.Flights.FirstOrDefault(f => f.Id == id) is { Done: false })).GroupBy(p => Class(w, p.Task, p.TargetKind, p.Target)).ToDictionary(g => g.Key, g => g.Count());
        var ranked = Targets(w, s).Where(x => !targeted.Contains((x.Kind, x.Id))).Select(x => x with { Score = x.Score * Weight(w, s, x.Task, x.Pos) * (0.85 + 0.3 * w.Rnd()) }).Where(x => x.Score > 0).ToList();
        for (int made = 0; made < 2 && busy + made < max && ranked.Count > 0;)
        {
            var x = ranked.MaxBy(x => x.Score / (1 + act.GetValueOrDefault(Class(w, x.Task, x.Kind, x.Id))))!;
            ranked.Remove(x);
            if (Package(w, s, x.Task, x.Kind, x.Id, x.Pos, x.Why) == null) continue;
            made++; act[Class(w, x.Task, x.Kind, x.Id)] = act.GetValueOrDefault(Class(w, x.Task, x.Kind, x.Id)) + 1;
        }
    }

    /// Lufttransport: Transportstaffeln fliegen Munition und Sprit vom Heimatplatz zum knappsten eigenen Platz
    /// (mind. 50 km weg, deutlich knapper als der Heimatplatz, Plätze mit Staffeln zuerst) und landen dort.
    static void Airlift(World w, Side s)
    {
        static double Have(Obj o) => Math.Min(o.Stock, o.Fuel) / Math.Max(1, o.Cap);
        foreach (var q in w.Sqns.Where(q => q.Side == s && Lift(q.Type) > 0 && Usable(w, q, 1)))
        {
            var b = BaseOf(w, q);
            if (Math.Min(b.Stock, b.Fuel) < 200 || w.Flights.Count(f => f.Sqn == q.Id && !f.Done) >= 2) continue;   // Heimatplatz selbst knapp / zwei Umläufe unterwegs
            double Dist(Obj o) => Geo.Dist(b.X, b.Z, o.X, o.Z);
            var d = w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == s && o.BaseId > 0 && o.Health > 0.5 && Have(o) < 0.4 && Have(o) < Have(b) - 0.1
                        && Dist(o) > 50000 && Dist(o) < PerfOf(q.Type).Radius * 1000 && !w.Flights.Any(f => f.TargetKind == "lift" && f.Target == o.Id && !f.Done))
                .MaxBy(o => (1 - Have(o)) * (1 + w.Sqns.Count(x => x.Base == o.Id && x.Ac > 0)));
            if (d == null) continue;
            var p = NewPkg(w, s, "Transport", "lift", d.Id, d.Name, $"Airlift: ammo and fuel to {d.Name} ({Math.Min(d.Stock, d.Fuel):0} t on hand)");
            p.Tot = Round5(w.Time.AddMinutes(45).AddSeconds(Dist(d) * 1.1 / PerfOf(q.Type).Spd));
            NewFlight(w, p, q, "Transport", 1, new(d.X, d.Z), p.Tot, 6000).Why = p.Why;
        }
    }

    /// Luftlandung (BMS Air Assault): bei eigener Offensive tagsüber Truppen hinter die feindliche Front – unverteidigter Ort, gern an einer Brücke, mit Geleitschutz.
    internal static void Assault(World w, Side s)
    {
        if (!w.Daylight || w.Flights.Any(f => f.Side == s && f.TargetKind == "drop" && !f.Done) || !w.Chance(0.04)) return;
        foreach (var ax in w.Axes.Where(a => a.Op == "Angriff" && a.OpSide == s))
        {
            if (Sim.Front(w, ax) is not { } fr) continue;
            var (own, foe) = s == Side.Red ? (w.Nodes[fr.R], w.Nodes[fr.B]) : (w.Nodes[fr.B], w.Nodes[fr.R]);
            double Far(Node m) => Geo.Dist(own.X, own.Z, m.X, m.Z);
            var n = w.Nodes.Where(m => m.Owner == s.Enemy() && m.Key != m.Id && Far(m) > Far(foe) + 5000 && Far(m) < 45000 && !w.Units.Any(u => u.Side != s && u.Node == m.Id))
                .MaxBy(m => (w.Objs.Any(o => o.Kind == OK.Bridge && Geo.Dist(o.X, o.Z, m.X, m.Z) < 5000) ? 2 : 1) * (0.5 + w.Rnd()));
            if (n == null) continue;
            double To(Sqn x) => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, n.X, n.Z);
            var q = w.Sqns.Where(x => x.Side == s && (Lift(x.Type) > 0 || Helo(x.Type) && x.Roles.Any(r => r is "Transport" or "CSAR")) && Usable(w, x, 2) && To(x) < PerfOf(x.Type).Radius * 1000 && Reach(w, x, new(n.X, n.Z))).MinBy(To);
            if (q == null) continue;
            bool helo = Helo(q.Type);
            var p = NewPkg(w, s, "Transport", "drop", n.Id, n.Name, $"Air assault: seize {n.Name} behind the enemy front ({ax.Name} offensive)");
            p.Tot = Round5(w.Time.AddMinutes(45).AddSeconds(To(q) * 1.1 / PerfOf(q.Type).Spd));
            var tgt = new P(n.X, n.Z);
            NewFlight(w, p, q, "Transport", 2, tgt, p.Tot, helo ? 100 : 3000).Why = p.Why;
            void Guard(string task, bool rotor, double alt)
            {
                var e = w.Sqns.Where(x => x.Side == s && x.Id != q.Id && x.Roles.Contains(task) && Helo(x.Type) == rotor && Usable(w, x, 2) && To(x) < PerfOf(x.Type).Radius * 1000 && Reach(w, x, new(n.X, n.Z))).MinBy(To);
                if (e != null) NewFlight(w, p, e, task, 2, tgt, p.Tot, alt).Why = $"Escort for the air assault on {n.Name}";
            }
            Guard("Escort", false, 4500);
            if (helo) Guard("CAS", true, 100);
            return;
        }
    }

    static string Class(World w, string task, string kind, int id) => kind == "obj" && w.Objs.FirstOrDefault(o => o.Id == id) is { } o ? o.Kind.ToString() : task;
    static HashSet<(string, int)> Targeted(World w, Side s) => w.Pkgs.Where(p => p.Side == s && p.Flights.Any(id => w.Flights.FirstOrDefault(f => f.Id == id) is { Done: false })).Select(p => (p.TargetKind, p.Target)).ToHashSet();
    static readonly Dictionary<string, double> RedW = new() { ["CAS"] = 1.5, ["OCA"] = 0.9, ["Strike"] = 0.8, ["DEAD"] = 1.0, ["BAI"] = 1.0, ["Interdiction"] = 0.8, ["Recon"] = 0.8, ["Antiship"] = 1.6 },
        BlueW = new() { ["CAS"] = 1.6, ["OCA"] = 0.7, ["Strike"] = 0.9, ["DEAD"] = 1.3, ["BAI"] = 1.2, ["Interdiction"] = 1.2, ["Recon"] = 1.0, ["Antiship"] = 1.6 };   // Kriegsschiffe: wenige, aber wertvolle Ziele
    /// Staffelrolle für einen Auftrag: DEAD fliegen Strike-Staffeln, Fotoaufklärung (Pod) ebenso
    public static string RoleOf(string task) => task is "DEAD" or "Recon" or "Antiship" ? "Strike" : task == "Jammer" ? "SEAD" : task;   // Störer: SEAD-Staffeln mit ECM-Behälter
    static readonly double[] PrioW = [0, 1, 1.8];
    /// Gewicht: Doktrin der Seite × Phase × Priorität des Kommandeurs × Schwerpunkt (Ziele bis 60 km um die Front der Schwerpunkt-Achse ×2).
    public static double Weight(World w, Side s, string task, P? at = null)
    {
        var c = w.CmdOf(s);
        double v = (s == Side.Red ? RedW : BlueW)[task] * PhaseW[c.Phase][task] * PrioW[Math.Clamp(c.Prio.GetValueOrDefault(task, 1), 0, 2)];
        if (at is { } p && c.Focus >= 0 && c.Focus < w.Axes.Count && Sim.Front(w, w.Axes[c.Focus]) is var (rf, bf)
            && Math.Min(Geo.Dist(w.Nodes[rf].X, w.Nodes[rf].Z, p.X, p.Z), Geo.Dist(w.Nodes[bf].X, w.Nodes[bf].Z, p.X, p.Z)) < 60000) v *= 2;
        return v;
    }

    /// Ziele aus dem eigenen Lagebild mit Grundwert und Begründung (KI-Planung und Auftragsliste des Spielers).
    public static List<Cand> Targets(World w, Side s)
    {
        var know = w.Intel(s);
        var c = new List<Cand>();
        bool early = (w.Time - w.Start).TotalHours < 24;
        // Luftnahunterstützung an aktiven Fronten
        foreach (var ax in w.Axes)
        {
            if (Sim.Front(w, ax) is not var (rf, bf)) continue;
            int en = s == Side.Red ? bf : rf, own = s == Side.Red ? rf : bf;
            var enemyAtt = w.Units.Any(u => u.Side != s && u.Order == "Angriff" && u.Node == en);
            var ownAtt = w.Units.Any(u => u.Side == s && u.Order == "Angriff" && u.Node == own);
            var n = w.Nodes[en];
            if (!enemyAtt && !ownAtt)
            {
                bool prep = ax.Op == "Bereitstellung" && (ax.OpSide == s || ax.Warned);   // eigene oder erkannte feindliche Bereitstellung
                if (w.Units.Any(u => u.Side != s && u.Node == en))                       // Kontaktlinie: CAS auf Abruf über der Front
                    c.Add(new("CAS", "node", en, new(n.X, n.Z), !prep ? 3.5 : ax.OpSide == s ? 5 : 6,
                        !prep ? $"Line of contact at {n.Name}: CAS on call"
                        : ax.OpSide == s ? $"Own staging on axis {ax.Name}: soften up the enemy at {n.Name}"
                        : $"Enemy staging at {n.Name} – CAS on call"));
                continue;
            }
            c.Add(new("CAS", "node", en, new(n.X, n.Z), enemyAtt ? 9 : 7, enemyAtt ? $"Enemy attacking at {w.Nodes[own].Name} – close air support" : $"Own attack on {n.Name} needs air support"));
        }
        foreach (var u in w.Units.Where(u => u.Side != s && know.TryGetValue($"U{u.Id}", out var k) && k.T > w.Time.AddHours(-2)))
        {
            if (w.Axes.Any(ax => Sim.Front(w, ax) is var (rf, bf) && u.Node == (s == Side.Red ? bf : rf))) continue;
            var k = know[$"U{u.Id}"];
            c.Add(new("BAI", "unit", u.Id, new(k.X, k.Z), 3 + 2 * u.Str + (u.Kind == UK.Armor ? 1 : 0), $"{u.Name} spotted behind the front ({k.Src}, {(w.Time - k.T).TotalMinutes:0} min old)"));
        }
        foreach (var cv in w.Convoys.Where(cv => cv.Side != s && know.TryGetValue($"C{cv.Id}", out var k) && k.T > w.Time.AddHours(-1)))
            c.Add(cv.Navy ? new Cand("Antiship", "convoy", cv.Id, new(cv.X, cv.Z), 4 + 2 * cv.Trucks + (w.Objs.Any(o => o.Kind == OK.Port && o.Side == s && Geo.Dist(o.X, o.Z, cv.X, cv.Z) < 80000) ? 4 : 0),
                    $"{cv.Name} ({cv.Trucks} warships) at sea – anti-ship missiles from outside its air defence") : new Cand("Interdiction", "convoy", cv.Id, new(cv.X, cv.Z), 4 + (cv.Tons + cv.Fuel) / 80 + (cv.Reinf >= 0 ? 8 : 0),
                cv.Reinf >= 0 ? $"{Sim.Kind(cv).What} with {w.S.Reinf[cv.Reinf].Name} en route" : $"{Sim.Kind(cv).What} ({cv.Tons + cv.Fuel:0} t) en route to {Sim.ObjName(w, cv.To)}"));
        // Roll-back: SAMs von außen nach innen – frontnah zuerst, eine Stellung im Schirm einer vorgelagerten erst, wenn die gefallen ist
        var mine = w.Nodes.Where(n => n.Owner == s).ToList();
        double Depth(P p) => mine.Count == 0 ? 0 : mine.Min(n => Geo.Dist(n.X, n.Z, p.X, p.Z)) / 1000;
        var sams = w.Objs.Where(o => o.Side == s.Enemy() && o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles > 0 && know.ContainsKey($"O{o.Id}")).Select(o => (o, P: Seen(w, s, o))).ToList();
        var depth = sams.ToDictionary(x => x.o.Id, x => Depth(x.P));
        (double, string) Dead(Obj o)
        {
            var at = Seen(w, s, o); double dp = depth.GetValueOrDefault(o.Id, Depth(at));
            var shield = sams.Where(x => x.o != o && depth[x.o.Id] < dp - 10 && Geo.Dist(x.P.X, x.P.Z, at.X, at.Z) < x.o.Range).Select(x => x.o.Name).ToList();
            double v = Math.Max(1, o.Value + (early ? 1 : 0) + 3 - Math.Min(5, dp / 12) - 2 * Math.Min(2, shield.Count));
            return (v, $"{o.Name} ({o.Sys}) threatens our missions" + (shield.Count > 0 ? $" · covered by {shield[0]} – take that one first" : dp < 50 ? " · near the front: roll back from the outside in" : ""));
        }
        foreach (var o in w.Objs.Where(o => o.Side != s && o.Side != Side.None && know.TryGetValue($"O{o.Id}", out var k) && k.Str > 0.35 && Sim.Roe(w, o)))   // geglaubter Zustand (Meldungen), nicht der wahre
        {
            (string task, double v, string why) = o.Kind switch
            {
                OK.Sam => Dead(o) is var (dv, dw) ? ("DEAD", dv, dw) : default,
                OK.Ewr => ("Strike", o.Value + 0.5 + (early ? 2 : 0), early ? $"{o.Name}: blind them first – take out enemy early warning" : $"{o.Name}: take out enemy early warning"),
                OK.Cp => ("Strike", o.Value * 1.2, $"{o.Name}: disrupt enemy command and control"),
                OK.Depot => ("Strike", o.Value * (0.8 + (o.Stock + o.Fuel) / Math.Max(1, 2 * o.Cap)), $"{o.Name}: approx. {o.Stock:0} t ammo, {o.Fuel:0} t fuel for the front"),
                OK.Factory => ("Strike", o.Value * 1.2, $"{o.Name}: strategic strike – cut the enemy's arms production"),
                OK.Port => ("Strike", o.Value * 1.1, $"{o.Name}: strategic strike – close the port to supply ships"),
                OK.Bridge => ("Interdiction", o.Value * (early ? 0.7 : 0.8 + Math.Min(1.5, o.Flow / 400)),
                    o.Flow > 50 ? $"{o.Name}: chokepoint, approx. {o.Flow:0} t of enemy supply daily" : $"{o.Name}: enemy supply route"),
                OK.Tel => know[$"O{o.Id}"].T > w.Time.AddHours(-3) ? ("BAI", o.Value * 1.5, $"{o.Name}: missile launch detected – hunt the launcher before it moves") : default,
                OK.Airbase => ("OCA", w.Sqns.Any(q => q.Base == o.Id) ? o.Value * (early && s == Side.Red ? 1.3 : 0.8) : 0, $"{o.Name}: hit squadrons on the ground, close the runway"),
                _ => ("", 0.0, ""),
            };
            if (v > 0) c.Add(new(task, "obj", o.Id, Seen(w, s, o), v, why));
        }
        foreach (var o in w.Objs.Where(o => o.Side != s && o.Side != Side.None && know.TryGetValue($"O{o.Id}", out var k) && k.Claim && k.Str <= 0.5 && k.T < w.Time.AddMinutes(-30)))   // gemeldet zerstört: vor dem Abhaken Fotos
        {
            var k = know[$"O{o.Id}"];
            c.Add(new("Recon", "obj", o.Id, new(k.X, k.Z), 2 + o.Value, $"{o.Name}: strike results unconfirmed ({k.Src} claims {k.Str:P0}) – photo recon"));
        }
        return c;
    }

    public record Offer(string Task, string Kind, int Id, int Sqn, string Type, string Name, string Why, int Nm, DateTime Tot, double Score, double X, double Z);

    /// Aufträge, die der Spieler mit einer spielbaren Staffel neu anlegen kann: Ziele des Lagebilds (schon beplante weiter unten) und die CAP-Stationen.
    public static List<Offer> Offers(World w)
    {
        var s = w.Player;
        var targeted = Targeted(w, s);
        var cap = CapStations(w, s).ToList();
        var c = Targets(w, s).Select(x => targeted.Contains((x.Kind, x.Id)) ? x with { Score = x.Score * Weight(w, s, x.Task, x.Pos) * 0.5, Why = x.Why + " – package already planned, second strike" } : x with { Score = x.Score * Weight(w, s, x.Task, x.Pos) })
            .Concat(cap.Select((st, i) => new Cand("CAP", "station", i, st.Pos, 6, $"{st.Name}: protect the airspace over the front"))).ToList();
        var res = new List<Offer>();
        List<Threat> low = Threats(w, s, 5000), high = Threats(w, s, 7000), jets = AirThreats(w, s).Where(t => t.Leth >= 3).ToList();
        foreach (var q in w.Sqns.Where(q => q.Side == s && q.Player && Usable(w, q, 2, false) && (w.Daylight || PerfOf(q.Type).Night)))
        {
            var b = BaseOf(w, q); var perf = PerfOf(q.Type);
            foreach (var x in c.Where(x => q.Roles.Contains(RoleOf(x.Task)) && CanDo(w, q, x.Task)))
            {
                double d = Geo.Dist(b.X, b.Z, x.Pos.X, x.Pos.Z);
                if (d > perf.Radius * 1000 || !Reach(w, q, x.Pos)) continue;
                var th = x.Task is "CAS" or "BAI" or "Interdiction" ? low : high;
                var near = th.Where(t => t.Name != TargetName(w, x.Kind, x.Id) && Geo.Dist(t.X, t.Z, x.Pos.X, x.Pos.Z) < t.R).Select(t => t.Name).Take(2).ToList();   // Ziel liegt im Schirm
                var why = near.Count > 0 ? $"{x.Why} · ⚠ inside coverage of {string.Join(", ", near)} – SEAD required" : x.Why;
                int js = x.Kind == "station" || x.Task == "CAS" ? 0 : jets.Count(t => Exposure([t], new(b.X, b.Z), x.Pos) > 0);
                if (js > 0) why += $" · ⚠ fighters on the route ({js} flight{(js > 1 ? "s" : "")}) – fighter sweep goes first";
                res.Add(new(x.Task, x.Kind, x.Id, q.Id, q.Type, x.Kind == "station" ? cap[x.Id].Name : TargetName(w, x.Kind, x.Id), why, (int)(d / 1852), Round5(w.Time.AddMinutes(45).AddSeconds(d * 1.15 / perf.Spd)), Math.Round(x.Score, 2), Math.Round(x.Pos.X), Math.Round(x.Pos.Z)));
            }
        }
        return res.GroupBy(o => o.Type).SelectMany(g => g.OrderByDescending(o => o.Score).ThenBy(o => o.Nm).Take(25)).OrderByDescending(o => o.Score).ThenBy(o => o.Nm).ToList();
    }

    /// Auftrag aus der Liste anlegen (Paket wie die KI, mit SEAD/Begleitschutz) und den Flug der gewählten Staffel zurückgeben.
    public static Flight TakeOffer(World w, string task, string kind, int id, int sqn)
    {
        var s = w.Player;
        var q = w.Sqns.FirstOrDefault(q => q.Id == sqn && q.Side == s && q.Player) ?? throw new ArgumentException("Squadron not flyable");
        if (kind == "station")
        {
            var st = CapStations(w, s).ElementAtOrDefault(id) ?? throw new ArgumentException("CAP station no longer exists");
            var b = BaseOf(w, q); double d = Geo.Dist(b.X, b.Z, st.Pos.X, st.Pos.Z);
            if (!Usable(w, q, 2, false) || d > PerfOf(q.Type).Radius * 1000) throw new ArgumentException($"{q.Name}: 2 aircraft not available or station out of range");
            var p = NewPkg(w, s, "CAP", "station:" + st.Name, -1, st.Name, $"{st.Name}: protect the airspace over the front");
            var f = NewFlight(w, p, q, "CAP", 2, st.Pos, Round5(w.Time.AddMinutes(45).AddSeconds(d * 1.15 / PerfOf(q.Type).Spd)), st.Alt, 90);
            f.TargetKind = "station:" + st.Name; f.Why = p.Why; p.Tot = f.Tot;
            return Lead(w, f);
        }
        var x = Targets(w, s).FirstOrDefault(x => x.Task == task && x.Kind == kind && x.Id == id) ?? throw new ArgumentException("Target no longer in the intel picture");
        var pk = Package(w, s, task, kind, id, x.Pos, x.Why, sqn) ?? throw new ArgumentException("Not feasible: no aircraft available, out of range, night, or no SEAD available");
        return Lead(w, w.Flights.First(f => f.Pkg == pk.Id && f.Sqn == sqn));
    }

    /// Neuer Auftrag für den Spieler: mindestens 30 min bis zum Start (Kaltstart 25 min + Mission laden), sonst ganzes Paket später.
    static Flight Lead(World w, Flight f)
    {
        double late = (w.Time.AddMinutes(30) - f.Takeoff).TotalMinutes;
        if (late <= 0) return f;
        var d = TimeSpan.FromMinutes(Math.Ceiling(late / 5) * 5);
        foreach (var o in w.Flights.Where(o => o.Pkg == f.Pkg && o.Status == "geplant")) { o.Tot += d; Retime(o); }
        w.Pkgs.First(p => p.Id == f.Pkg).Tot += d;
        return f;
    }

    static Pkg NewPkg(World w, Side s, string task, string kind, int target, string name, string why)
    {
        var p = new Pkg { Id = w.NewId(), Side = s, Task = task, TargetKind = kind, Target = target, Why = why };
        p.Name = $"Package {p.Id} – {task} {name}";
        w.Pkgs.Add(p);
        return p;
    }

    /// Paket bauen: Kern + ggf. SEAD und Begleitschutz, gemeinsame TOT. Wird auch von der Oberfläche benutzt.
    public static Pkg? Package(World w, Side s, string task, string kind, int id, P tgt, string why, int? sqnId = null, DateTime? tot = null)
    {
        string role = RoleOf(task);
        var cands = w.Sqns.Where(q => q.Side == s && (sqnId == null ? q.Roles.Contains(role) : q.Id == sqnId) && Usable(w, q, 2, sqnId == null)
                                      && Geo.Dist(BaseOf(w, q).X, BaseOf(w, q).Z, tgt.X, tgt.Z) < PerfOf(q.Type).Radius * 1000 && Reach(w, q, tgt)
                                      && (w.Daylight || PerfOf(q.Type).Night) && CanDo(w, q, task)).ToList();
        if (cands.Count == 0) return null;
        var q = cands.OrderBy(q => Geo.Dist(BaseOf(w, q).X, BaseOf(w, q).Z, tgt.X, tgt.Z) * (Helo(q.Type) ? 0.6 : 1) * (0.8 + 0.4 * w.Rnd())).First();   // Hubschrauber springen über Feldplätze (FARP) nach vorn: zuerst
        bool jet = !Helo(q.Type);
        double alt = AltOf(q.Type, task);
        var threats = Threats(w, s, alt);
        var air = AirThreats(w, s);
        var b = BaseOf(w, q);
        if (task == "Antiship" && w.Convoys.FirstOrDefault(c => c.Id == id) is { Navy: true } nv)   // Abschuss außerhalb der Schiffsflugabwehr
            tgt = Geo.Move(tgt, Geo.Brg(tgt, new P(b.X, b.Z)), Math.Clamp(Sim.NavyRange(nv) + 10000, 40000, 90000));
        var grid = Grid([.. threats, .. air]);                             // um SAM-Schirme und bekannte Jäger herum
        var path = Route(grid, new(b.X, b.Z), tgt);
        double transit = (Geo.Dist(b.X, b.Z, tgt.X, tgt.Z) * 1.15) / PerfOf(q.Type).Spd;
        var t0 = tot ?? Round5(w.Time.AddMinutes(45).AddSeconds(transit));
        double risk = 0, arisk = 0; var prevP = new P(b.X, b.Z);
        foreach (var pt in path.Append(tgt)) { risk += Exposure(threats, prevP, pt); arisk += Exposure(air, prevP, pt); prevP = pt; }
        string name = TargetName(w, kind, id);
        var p = NewPkg(w, s, task, kind, id, name, why);
        p.Tot = t0;
        int n = task is "Strike" or "OCA" or "DEAD" or "Antiship" && Free(w, q) >= (q.Player ? 6 : 4) && sqnId == null ? 4 : 2;
        var core = NewFlight(w, p, q, task, n, tgt, t0, alt, task == "CAS" ? 30 : 0, grid);
        if ((risk > 15 || task == "DEAD") && jet)
        {
            var sq = w.Sqns.Where(x => x.Side == s && x.Roles.Contains("SEAD") && Usable(w, x, 2) && Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z) < PerfOf(x.Type).Radius * 1000)
                .OrderBy(x => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z)).FirstOrDefault();
            if (sq != null) { var f = NewFlight(w, p, sq, "SEAD", 2, tgt, t0.AddMinutes(-2), 7500, task == "CAS" ? 32 : 0, grid); f.Why = task == "CAS" ? $"SEAD for {p.Name}: suppresses air defenses for the whole station" : $"SEAD for {p.Name}: threat along the route {risk:0}"; }
            else if ((task == "DEAD" || risk > 40) && sqnId == null) { Cancel(w, p, "no SEAD available, risk too high"); return null; }   // befohlen: fliegt auch ohne SEAD
            else why += $" · ⚠ air defenses on the route (risk {risk:0}), no SEAD available";
            var jq = risk > 25 ? w.Sqns.Where(x => x.Side == s && x.Roles.Contains("SEAD") && Free(w, x) >= 2 && Usable(w, x, 2) && Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z) < PerfOf(x.Type).Radius * 1000)
                .OrderBy(x => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z)).FirstOrDefault() : null;
            if (jq != null)                                                     // Störer: Stand-off außerhalb der Schirme, deckt das Paket von TOT −8 bis +12 min
            {
                var jb = BaseOf(w, jq); double hb = Geo.Brg(tgt, new P(jb.X, jb.Z));
                var sp = new[] { 40000, 55000, 70000, 85000 }.SelectMany(d => new[] { 0, -35, 35 }.Select(o => (d, p: Geo.Move(tgt, (hb + o + 360) % 360, d)))).MinBy(c => grid[Idx(c.p)] * 1e6 + c.d).p;
                var f = NewFlight(w, p, jq, "Jammer", 2, sp, t0.AddMinutes(-8), 7500, 20, grid);
                f.Why = $"Stand-off jamming for {p.Name}: enemy radars see the package only inside burn-through range";
            }
        }
        bool fighters = w.Flights.Any(f => f.Side != s && A2A.Contains(f.Task) && !f.Done && Geo.Dist(f.X, f.Z, tgt.X, tgt.Z) < 150000)
                        || w.Sqns.Any(x => x.Side != s && x.Roles.Contains("CAP") && x.Ac > 0 && Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z) < 160000);
        if (fighters && task is not "CAS" && jet)
        {
            var eq = w.Sqns.Where(x => x.Side == s && x.Roles.Contains("Escort") && x.Id != q.Id && Usable(w, x, 2) && Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z) < PerfOf(x.Type).Radius * 1000)
                .OrderBy(x => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z)).FirstOrDefault();
            if (eq != null) { var f = NewFlight(w, p, eq, "Escort", 2, tgt, t0, alt + 1500, 0, grid); f.Why = $"Escort for {p.Name}: enemy fighters expected"; }
        }
        if (arisk > 10 && task is not "CAS" && jet)                                // Jäger auf der Route: Jagdvorstoß räumt 8 min vor dem Paket
        {
            var used = w.Flights.Where(f => f.Pkg == p.Id).Select(f => f.Sqn).ToHashSet();
            var sq = w.Sqns.Where(x => x.Side == s && x.Roles.Contains("Sweep") && !used.Contains(x.Id) && Usable(w, x, 2) && Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z) < PerfOf(x.Type).Radius * 1000)
                .OrderBy(x => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, tgt.X, tgt.Z)).FirstOrDefault();
            if (sq != null) { var f = NewFlight(w, p, sq, "Sweep", 2, tgt, t0.AddMinutes(-8), alt + 2500, 0, Grid(threats)); f.Why = $"Fighter sweep ahead of {p.Name}: enemy fighters on the route"; }
            else why += " · ⚠ fighters on the route, no fighter sweep available";
        }
        core.Why = why;
        return p;
    }

    static double AltOf(string type, string task) => Helo(type) ? 100 : task is "CAS" or "BAI" or "Interdiction" ? 5000 : task == "OCA" ? 6000 : 7000;

    /// Muster wechseln: eine andere spielbare Staffel fliegt den Flug – der Spieler entscheidet frei (Reichweite/Sprit zeigt die Prüfung).
    /// Fehlen Maschinen, geben spätere KI-Flüge der Staffel ihre ab und werden umgeplant.
    public static void Swap(World w, Flight f, int sqn)
    {
        var q = w.Sqns.FirstOrDefault(q => q.Id == sqn && q.Side == f.Side && q.Player) ?? throw new ArgumentException("Squadron not flyable");
        if (q.Id == f.Sqn) return;
        var b = BaseOf(w, q);
        if (!Open(b) || b.Side != q.Side) throw new ArgumentException($"{q.Name}: airfield closed");
        int mine = w.Flights.Where(x => x.Sqn == q.Id && x.Status == "geplant" && x.Player).Sum(x => x.N);
        if (q.Ac - mine < f.N) throw new ArgumentException($"{q.Name}: only {Math.Max(0, q.Ac - mine)} aircraft on the ground");
        foreach (var x in w.Flights.Where(x => x.Sqn == q.Id && x.Status == "geplant" && !x.Player).OrderByDescending(x => x.Takeoff).ToList())
        {
            if (Free(w, q) >= f.N) break;
            Replan(w, x, q, $"aircraft taken for {f.Callsign}");
        }
        Move(w, f, q);
    }

    /// KI-Flug verliert seine Maschinen: nächste andere Staffel mit der Rolle übernimmt, sonst entfällt er (Hauptflug: ganzes Paket – die KI plant das Ziel neu).
    static void Replan(World w, Flight x, Sqn from, string why)
    {
        var tw = x.TargetKind == "lift" ? x.Route[^1] : x.Route.First(r => r.Kind is "target" or "orbit");
        double To(Sqn y) => Geo.Dist(BaseOf(w, y).X, BaseOf(w, y).Z, tw.X, tw.Z);
        var y = w.Sqns.Where(y => y.Side == x.Side && y.Id != from.Id && y.Roles.Contains(RoleOf(x.Task)) && Usable(w, y, x.N) && To(y) < PerfOf(y.Type).Radius * 1000
            && Reach(w, y, new(tw.X, tw.Z)) && (w.Daylight || PerfOf(y.Type).Night)).MinBy(To);
        if (y != null) { Move(w, x, y); w.Log(x.Side, "ato", $"{x.Callsign} ({x.Task}) re-planned with {y.Name} – {why}."); return; }
        var p = w.Pkgs.FirstOrDefault(p => p.Id == x.Pkg);
        if (p != null && p.Task == x.Task && !w.Flights.Any(z => z.Pkg == p.Id && z.Player)) Cancel(w, p, why);
        else { w.Flights.Remove(x); if (p != null && !w.Flights.Any(z => z.Pkg == p.Id)) w.Pkgs.Remove(p); }
        w.Log(x.Side, "ato", $"{x.Callsign} ({x.Task}) stood down – {why}; the AI will re-plan.");
    }

    /// Flug auf eine andere Staffel umsetzen: gleicher Auftrag, Paket und TOT, Route und Waffen neu.
    static void Move(World w, Flight f, Sqn q)
    {
        var tw = f.TargetKind == "lift" ? f.Route[^1] : f.Route.First(r => r.Kind is "target" or "orbit");
        P tgt = new(tw.X, tw.Z); var b = BaseOf(w, q);
        int o2 = f.Route.FindIndex(r => r.Kind == "orbit2");
        double station = o2 > 0 ? (f.Route[o2].T - f.Route[o2 - 1].T).TotalMinutes : 0;
        double alt = Helo(q.Type) || Helo(f.Type) ? AltOf(q.Type, f.Task) : f.Route.Max(r => r.Alt);
        f.Sqn = q.Id; f.Type = q.Type; f.Pyl = null;
        f.Load = PickLoad(w, q, f.Task, f.N);
        f.Missiles = A2aCount(f.Load) * f.N; f.Arm = ArmCount(f.Load) * f.N;
        f.Route = BuildRoute(w, f, b, tgt, f.Tot, alt, station, Grid([.. Threats(w, f.Side, alt), .. AirThreats(w, f.Side)]));
        f.Takeoff = f.Route[0].T; f.Land = f.Route[^1].T; f.X = f.Route[0].X; f.Z = f.Route[0].Z;
    }

    /// Varianten eines Musters (gleiche Staffel, anderes Cockpit) – nur mit Beladungen aus der DCS-Installation
    static readonly string[][] Kin = [["F-14B", "F-14BU", "F-14A-135-GR", "F-14A-135-GR-Early", "F-14A-95-GR"], ["A-10C_2", "A-10C", "A-10A"], ["Su-25T", "Su-25"], ["MiG-29S", "MiG-29A", "MiG-29G", "MiG-29 Fulcrum"], ["Su-27", "J-11A", "Su-33"],
        ["Ka-50_3", "Ka-50"], ["SA342M", "SA342L", "SA342Mistral", "SA342Minigun"], ["Mirage-F1EE", "Mirage-F1CE", "Mirage-F1M-EE"], ["L-39ZA", "L-39C"], ["C-101CC", "C-101EB"], ["MB-339A", "MB-339APAN"]];
    public static string[] Variants(Theater th, string t) => [.. (Kin.FirstOrDefault(k => k.Contains(t)) ?? [t]).Where(v => v == t || th.Payloads.GetValueOrDefault(v)?.Count > 0)];

    /// Variante wechseln (F-14A ↔ F-14B …): gleiche Staffel und Route, Waffen neu.
    public static void Retype(World w, Flight f, string type)
    {
        if (type == f.Type) return;
        if (!Variants(w.Th, f.Type).Contains(type)) throw new ArgumentException($"{type} is not a variant of {f.Type}");
        (f.Type, f.Pyl) = (type, null);
        f.Load = PickLoad(w, SqnOf(w, f), f.Task, f.N, type);
        f.Missiles = A2aCount(f.Load) * f.N; f.Arm = ArmCount(f.Load) * f.N;
    }

    /// Seezielangriff nur mit Seezielflugkörpern (Harpoon, Kh-31A, Kh-35, Kh-22) in der Beladung des Musters
    static bool CanDo(World w, Sqn q, string task) => task != "Antiship" || Need(ChooseLoad(w.Th, q.Type, task)).ContainsKey("AShM");
    public static string TargetName(World w, string kind, int id) => kind switch { "obj" or "lift" => Sim.ObjName(w, id), "unit" => w.Units.FirstOrDefault(u => u.Id == id)?.Name ?? "Unit", "node" or "drop" => w.Nodes[id].Name, "convoy" => w.Convoys.FirstOrDefault(c => c.Id == id) is { Navy: true } c ? c.Name : "Convoy", _ => "" };

    static void Cancel(World w, Pkg p, string why)
    {
        foreach (var f in w.Flights.Where(f => f.Pkg == p.Id)) { f.Status = "gestrichen"; f.Done = true; f.Result = why; }
        w.Flights.RemoveAll(f => f.Pkg == p.Id);
        w.Pkgs.Remove(p);
    }

    static DateTime Round5(DateTime t) => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 5 * 5, 0).AddMinutes(5);

    public static Flight NewFlight(World w, Pkg p, Sqn q, string task, int n, P tgt, DateTime tot, double alt, double station = 0, double[]? grid = null)
    {
        var f = new Flight { Id = w.NewId(), Pkg = p.Id, Side = q.Side, Sqn = q.Id, Type = q.Type, N = n, Alive = n, Task = task, Tot = tot, Target = p.Target, TargetKind = p.TargetKind };
        int k = f.Id;
        bool shell = p.Name == "Shell";                                     // Tanker nach Station (Funkplan: Texaco 270/51X, Shell 271/52X), Ablösung zählt hoch
        string sc = task == "AWACS" ? "Magic" : shell ? "Shell" : "Texaco";
        if (task is "AWACS" or "Tanker" && q.Side == Side.Blue)
            f.Callsign = $"{sc} {1 + w.Flights.Count(o => !o.Done && o.Side == Side.Blue && o.Task == task && o.Callsign.StartsWith(sc)) % 9}";
        else f.Callsign = q.Side == Side.Blue ? $"{BlueCs[k % 8]} {k / 8 % 9 + 1}" : $"{200 + k % 700}";
        f.Freq = task switch { "AWACS" => 251.0, "Tanker" => shell ? 271.0 : 270.0, _ => 300 + (f.Id % 79) * 1.25 };
        f.Tacan = task == "Tanker" ? (shell ? 52 : 51) : 0;
        f.Load = task is "AWACS" or "Tanker" ? "" : PickLoad(w, q, task, n);
        f.Missiles = A2aCount(f.Load) * n;
        f.Arm = ArmCount(f.Load) * n;
        f.Route = BuildRoute(w, f, BaseOf(w, q), tgt, tot, alt, station, grid ?? Grid(Threats(w, q.Side, alt)));
        f.Takeoff = f.Route[0].T; f.Land = f.Route[^1].T;
        f.X = f.Route[0].X; f.Z = f.Route[0].Z;
        p.Flights.Add(f.Id);
        w.Flights.Add(f);
        return f;
    }

    /// Route: Start – Navigation (um Bedrohungen) – Push – IP – Ziel – Abflug – Navigation – Landung. Zeiten rückwärts von der TOT.
    public static List<Wp> BuildRoute(World w, Flight f, Obj b, P tgt, DateTime tot, double alt, double station, double[] grid)
    {
        var perf = PerfOf(f.Type);
        double v = perf.Spd;
        var home = new P(b.X, b.Z);
        double elev = w.Th.Height(b.X, b.Z);
        var r = new List<Wp>();
        var land = f.TargetKind == "lift" && w.Objs.FirstOrDefault(o => o.Id == f.Target) is { } dst ? dst : b;
        var lp = new P(land.X, land.Z);
        bool orbit = station > 0 || f.Task is "CAP" or "AWACS" or "Tanker";
        P? front = orbit && f.TargetKind == "node" && f.Target >= 0 && f.Target < w.Nodes.Count ? new P(w.Nodes[f.Target].X, w.Nodes[f.Target].Z) : null;
        if (front is { } fp) tgt = Hold(w, f.Side, fp);                    // CAS (und ihre SEAD) wartet diesseits der Kontaktlinie, auf Abruf über die Front
        List<P> Way(P a, P z)                                               // Hubschrauber: Umweg übers Gebirge → direkt (Reach prüft den Direktweg)
        {
            var r = Route(grid, a, z);
            if (!Helo(f.Type)) return r;
            var q = r.Prepend(a).Append(z).ToList();
            return Enumerable.Range(1, q.Count - 1).Any(i => Math.Max(w.Th.Peak(q[i].X, q[i].Z, 3000), w.Th.Peak((q[i - 1].X + q[i].X) / 2, (q[i - 1].Z + q[i].Z) / 2, 3000)) + 150 > perf.Ceil) ? [] : r;
        }
        var inb = Way(home, tgt);
        var pts = new List<(P P, string Kind, string Name)>();
        foreach (var p in inb) pts.Add((p, "nav", ""));
        if (orbit)
        {
            var brgIn = Geo.Brg(pts.Count > 0 ? pts[^1].P : home, tgt);
            pts.Add((tgt, "orbit", f.Task == "CAS" ? "CAS station" : "Station"));
            double ob = (brgIn + 90) % 360;
            if (front is { } fp2) { double fb = Geo.Brg(tgt, fp2); ob = new[] { (fb + 90) % 360, (fb + 270) % 360 }.MinBy(c => grid[Idx(Geo.Move(tgt, c, 25000))] + (Helo(f.Type) ? w.Th.Peak(Geo.Move(tgt, c, 10000).X, Geo.Move(tgt, c, 10000).Z, 3000) : 0)); }   // Schenkel parallel zur Front, zur ruhigeren (Hubschrauber: niedrigeren) Seite
            pts.Add((Geo.Move(tgt, ob, f.Task is "Tanker" ? 40000 : Helo(f.Type) ? 10000 : 25000), "orbit2", "Station end"));
        }
        else if (f.Task == "CSAR") pts.Add((tgt, "target", "Pilot"));     // Rettung: direkt hin, landen, zurück
        else if (land != b) pts.Add((Geo.Move(tgt, Geo.Brg(tgt, pts.Count > 0 ? pts[^1].P : home), 15000), "target", "Anflug"));
        else
        {
            var last = pts.Count > 0 ? pts[^1].P : home;
            double dl = Geo.Dist(last, tgt);
            var ip = dl > 30000 ? Geo.Move(tgt, Geo.Brg(tgt, last), 25000) : Geo.Move(tgt, (Geo.Brg(tgt, home) + 30) % 360, 25000);
            if (pts.Count > 0 && dl < 30000) pts.RemoveAt(pts.Count - 1);
            if (pts.Count == 0) pts.Add((Geo.Lerp(home, ip, 0.4), "nav", ""));
            pts[^1] = (pts[^1].P, "push", "Push");
            pts.Add((ip, "ip", "IP"));
            pts.Add((tgt, "target", "Target"));
            var eg = Geo.Move(tgt, (Geo.Brg(tgt, home) + 50) % 360, 30000);
            pts.Add((eg, "egress", "Egress"));
        }
        var outb = Way(pts[^1].P, lp);
        foreach (var p in outb) pts.Add((p, "nav", ""));
        // Zeiten: Ankunft am Ziel/Station = TOT, davor rückwärts
        int ti = pts.FindIndex(x => x.Kind is "target" or "orbit");
        var times = new DateTime[pts.Count];
        times[ti] = tot;
        for (int i = ti - 1; i >= 0; i--) times[i] = times[i + 1].AddSeconds(-Geo.Dist(pts[i].P, pts[i + 1].P) / v);
        for (int i = ti + 1; i < pts.Count; i++)
        {
            double sec = Geo.Dist(pts[i - 1].P, pts[i].P) / v;
            if (pts[i].Kind == "orbit2") sec = Math.Max(sec, station > 0 ? station * 60 : 60 * 60);
            times[i] = times[i - 1].AddSeconds(sec);
        }
        if (orbit && station > 0)
        {
            int oi = pts.FindIndex(x => x.Kind == "orbit2");
            times[oi] = tot.AddMinutes(station);
            for (int i = oi + 1; i < pts.Count; i++) times[i] = times[i - 1].AddSeconds(Geo.Dist(pts[i - 1].P, pts[i].P) / v);
        }
        var t0 = (pts.Count > 0 ? times[0] : tot).AddSeconds(-Geo.Dist(home, pts[0].P) / v - 360);
        r.Add(new Wp { Name = b.Name, Kind = "start", X = b.X, Z = b.Z, Alt = elev, Spd = 0, T = t0 });
        double safe = 0;
        var prevP = home;
        foreach (var x in pts) { safe = Math.Max(safe, Math.Max(w.Th.Peak(x.P.X, x.P.Z, 3000), w.Th.Peak((prevP.X + x.P.X) / 2, (prevP.Z + x.P.Z) / 2, 3000))); prevP = x.P; }
        double cruise = Math.Max(alt, safe + 700);
        bool lowOk = Attack.Contains(f.Task) || f.Task == "SEAD", helo = Helo(f.Type);
        prevP = home;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i].P;                                                 // Angreifer: in Bedrohungszellen Tiefflug 150 m über dem höchsten Gelände des Abschnitts
            bool low = helo || lowOk && pts[i].Kind is not ("orbit" or "orbit2") && grid[Idx(p)] > 1.5;
            double a = low ? Math.Max(w.Th.Peak(p.X, p.Z, 3000), w.Th.Peak((prevP.X + p.X) / 2, (prevP.Z + p.Z) / 2, 3000)) + 150 : cruise;
            r.Add(new Wp { Name = pts[i].Name, Kind = pts[i].Kind, X = p.X, Z = p.Z, Alt = Math.Min(a, cruise), Spd = v, T = times[i] });
            prevP = p;
        }
        var lastT = times[^1].AddSeconds(Geo.Dist(pts[^1].P, lp) / v + 300);
        r.Add(new Wp { Name = land.Name, Kind = "land", X = land.X, Z = land.Z, Alt = w.Th.Height(land.X, land.Z), Spd = v * 0.6, T = lastT });
        return r;
    }

    /// CAS-Wartepunkt: von der feindlichen Frontstellung Richtung eigener Linie, 6 km hinter der Grenze (Miz: Angriffsradius reicht bis zum Ziel).
    static P Hold(World w, Side s, P en)
    {
        var own = w.Nodes.Where(n => n.Owner == s).MinBy(n => Geo.Dist(n.X, n.Z, en.X, en.Z));
        if (own == null) return en;
        double brg = Geo.Brg(en, new P(own.X, own.Z));
        for (double d = 2000; d <= 40000; d += 2000)
            if (OwnerAt(w, Geo.Move(en, brg, d).X, Geo.Move(en, brg, d).Z) == s) return Geo.Move(en, brg, d + 6000);
        return Geo.Move(en, brg, 20000);
    }

    /// Neue Zwischenpunkte (Start und Landung bleiben). Die Punkte kommen ohne Zeiten – Stationsdauer vorher merken.
    public static void SetRoute(Flight f, List<Wp> mid)
    {
        if (!mid.Any(r => r.Kind is "target" or "orbit")) throw new ArgumentException("Target/station waypoint missing");
        int o0 = f.Route.FindIndex(x => x.Kind == "orbit2");
        var station = o0 > 0 ? f.Route[o0].T - f.Route[o0 - 1].T : TimeSpan.Zero;
        f.Route = [f.Route[0], .. mid, f.Route[^1]];
        int oi = f.Route.FindIndex(x => x.Kind == "orbit2");
        if (oi > 0) f.Route[oi].T = f.Route[oi - 1].T + station;
        Retime(f);
    }

    /// Zeiten nach Routenänderung neu rechnen: TOT bleibt fest, Stationszeit bleibt erhalten.
    public static void Retime(Flight f)
    {
        var r = f.Route;
        int ti = Math.Max(1, r.FindIndex(x => x.Kind is "target" or "orbit"));
        int oi = r.FindIndex(x => x.Kind == "orbit2");
        double station = oi > 0 ? (r[oi].T - r[oi - 1].T).TotalSeconds : 0;
        double Leg(int i) => Geo.Dist(r[i - 1].X, r[i - 1].Z, r[i].X, r[i].Z) / Math.Max(60, r[i].Kind == "land" ? r[i - 1].Spd : r[i].Spd);
        r[ti].T = f.Tot;
        for (int i = ti - 1; i >= 0; i--) r[i].T = r[i + 1].T.AddSeconds(-Leg(i + 1) - (i == 0 ? 360 : 0));
        for (int i = ti + 1; i < r.Count; i++) r[i].T = r[i - 1].T.AddSeconds(i == oi ? Math.Max(Leg(i), station) : Leg(i) + (r[i].Kind == "land" ? 300 : 0));
        f.Takeoff = r[0].T; f.Land = r[^1].T;
    }

    /// Live-Prüfung eines Flugs: Sprit, Bedrohungen, Timing.
    public static List<string> Assess(World w, Flight f)
    {
        var l = new List<string>();
        var perf = PerfOf(f.Type);
        double len = 0;
        for (int i = 1; i < f.Route.Count; i++) len += Geo.Dist(f.Route[i - 1].X, f.Route[i - 1].Z, f.Route[i].X, f.Route[i].Z);
        var oi = f.Route.FindIndex(x => x.Kind == "orbit2");
        double orbitKm = oi > 0 ? (f.Route[oi].T - f.Route[oi - 1].T).TotalSeconds * perf.Spd * 0.8 / 1000 : 0;
        double reach = perf.Radius * 2.3;
        double need = len / 1000 + orbitKm;
        var tw = f.Route.FirstOrDefault(r => r.Kind is "target" or "orbit") ?? f.Route[^1];
        var tk = w.Flights.Where(o => o.Side == f.Side && o.Task == "Tanker" && !o.Done && o.Route.Any(r => r.Kind == "orbit") && OrbitEnd(o) > tw.T.AddMinutes(-60) && o.Takeoff < tw.T)
            .MinBy(o => Geo.Dist(o.Route.First(r => r.Kind == "orbit").X, o.Route.First(r => r.Kind == "orbit").Z, tw.X, tw.Z));
        l.Add(need <= reach ? $"✓ Fuel: {need:0} km of approx. {reach:0} km"
            : $"⚠ Fuel: {need:0} km route, range approx. {reach:0} km – " + (tk == null ? "no friendly tanker at TOT" : $"Tanker {tk.Callsign} {tk.Freq:0.0} MHz, TACAN {tk.Tacan}X, on station until {OrbitEnd(tk):HH:mm}"));
        var clash = new Dictionary<string, DateTime>();
        bool Near(Flight x, double px, double pz) => Geo.Dist(px, pz, x.Route[0].X, x.Route[0].Z) < 10000 || Geo.Dist(px, pz, x.Route[^1].X, x.Route[^1].Z) < 10000;
        var others = w.Flights.Where(o => o.Side == f.Side && o != f && o.Pkg != f.Pkg && !o.Done && o.Route.Count > 1 && !(o.Task == f.Task && o.TargetKind == f.TargetKind && f.TargetKind.StartsWith("station"))).ToList();
        for (var t = f.Route[0].T.AddMinutes(2); t < f.Route[^1].T; t = t.AddMinutes(2))   // ponytail: Stichprobe alle 2 min, schnelle Kreuzungen können durchrutschen
        {
            var (x, z, a) = PosAt(f, t);
            if (Near(f, x, z)) continue;
            foreach (var o in others.Where(o => o.Route[0].T < t && o.Route[^1].T > t && !clash.ContainsKey(o.Callsign)))
            {
                var (ox, oz, oa) = PosAt(o, t);
                if (Math.Abs(oa - a) < 300 && Geo.Dist(x, z, ox, oz) < 3000 && !Near(o, ox, oz)) clash[o.Callsign] = t;
            }
        }
        if (f.Status == "geplant" && w.Sqns.FirstOrDefault(q => q.Id == f.Sqn) is { } fq && BaseOf(w, fq) is var fb && Short(w, fb, f.Load, f.N) is { Length: > 0 } sh) l.Add($"⚠ Not enough weapons at {fb.Name}: {sh} – choose another loadout");
        l.Add(clash.Count == 0 ? "✓ no conflicts with other friendly flights" : "⚠ Conflict (< 3 km, < 300 m): " + string.Join(", ", clash.Select(c => $"{c.Key} {c.Value:HH:mm}")));
        var hits = new Dictionary<string, double>();
        for (int i = 1; i < f.Route.Count; i++)
        {
            var a = f.Route[i - 1]; var b = f.Route[i];
            foreach (var t in Threats(w, f.Side, Math.Min(a.Alt, b.Alt)))
                if (Geo.SegDist(new P(t.X, t.Z), new P(a.X, a.Z), new P(b.X, b.Z)) < t.R) hits[t.Name] = Math.Max(hits.GetValueOrDefault(t.Name), t.Leth);
        }
        l.Add(hits.Count == 0 ? "✓ no known air defenses on the route" : "⚠ Route through known air defenses: " + string.Join(", ", hits.OrderByDescending(h => h.Value).Select(h => h.Key)));
        for (int i = 1; i < f.Route.Count; i++)
        {
            var a = f.Route[i - 1]; var b = f.Route[i];
            double sec = (b.T - a.T).TotalSeconds, d = Geo.Dist(a.X, a.Z, b.X, b.Z);
            if (b.Kind != "orbit2" && sec > 0 && d / sec > perf.Spd * 1.35) l.Add($"⚠ Timing: {a.Name}→{b.Name} requires {d / sec * 1.944:0} kt");
        }
        if (f.Status == "geplant" && f.Takeoff < w.Time.AddMinutes(10)) l.Add("⚠ Takeoff in less than 10 minutes");
        foreach (var r in f.Route.Where(r => r.Kind is not ("start" or "land")))
            if (r.Alt < w.Th.Peak(r.X, r.Z, 2000) + 150) { l.Add($"⚠ Terrain: {(r.Name == "" ? r.Kind : r.Name)} below terrain elevation"); break; }
        return l;
    }

    // ---------- Ausführung ----------

    public static (double X, double Z, double Alt) PosAt(Flight f, DateTime t)
    {
        var r = f.Route;
        if (t <= r[0].T) return (r[0].X, r[0].Z, r[0].Alt);
        for (int i = 1; i < r.Count; i++)
            if (t <= r[i].T)
            {
                if (r[i].Kind == "orbit2") return (r[i - 1].X, r[i - 1].Z, r[i - 1].Alt);
                double fr = (t - r[i - 1].T).TotalSeconds / Math.Max(1, (r[i].T - r[i - 1].T).TotalSeconds);
                double alt = r[i - 1].Kind == "start" ? r[i - 1].Alt + (r[i].Alt - r[i - 1].Alt) * Math.Min(1, fr * 3) : r[i].Kind == "land" ? r[i - 1].Alt + (r[i].Alt - r[i - 1].Alt) * fr : r[i].Alt;
                return (r[i - 1].X + (r[i].X - r[i - 1].X) * fr, r[i - 1].Z + (r[i].Z - r[i - 1].Z) * fr, alt);
            }
        return (r[^1].X, r[^1].Z, r[^1].Alt);
    }

    public static Sqn SqnOf(World w, Flight f) => w.Sqns.First(q => q.Id == f.Sqn);

    public static void Tick(World w)
    {
        foreach (var f in w.Flights.Where(f => !f.Done && !f.InDcs).ToList()) Step(w, f);
        Engage(w);
        Qra(w);
    }

    /// Alarmrotte: Feind im eigenen Luftraum, vom Radar erfasst, unter 40 km vor einem Platz mit Jägern → 2 Jäger starten gegen den Kontakt.
    static void Qra(World w)
    {
        foreach (var e in w.Flights.Where(f => Airborne(f) && f.Alive > 0).ToList())
        {
            var s = e.Side.Enemy();
            foreach (var q in w.Sqns.Where(q => q.Side == s && q.Roles.Contains("CAP")))
            {
                var b = BaseOf(w, q);
                double d = Geo.Dist(b.X, b.Z, e.X, e.Z);
                if (d > 40000 || w.Flights.Any(f => f.TargetKind == $"qra:{b.Id}" && !f.Done) || !Usable(w, q, 2)
                    || OwnerAt(w, e.X, e.Z) != s || Sim.Radar(w, s, e.X, e.Z, e.Alt) == null) continue;
                var p = NewPkg(w, s, "CAP", $"qra:{b.Id}", -1, $"QRA {b.Name}", $"Scramble: contact {d / 1000:0} km from {b.Name}");
                var f = NewFlight(w, p, q, "CAP", 2, new(e.X, e.Z), w.Time.AddMinutes(10).AddSeconds(1.3 * d / PerfOf(q.Type).Spd), 6000, 20);
                f.Why = p.Why;
                w.Log(s, "luft", $"QRA {b.Name}: {f.Callsign} ({q.Type}) scrambles against a contact {d / 1000:0} km from the field.", b.X, b.Z);
                break;
            }
        }
    }

    static void Step(World w, Flight f)
    {
        if (f.Status == "gestrichen") { f.Done = true; return; }
        var q = SqnOf(w, f);
        if (f.Status == "geplant")
        {
            if (!f.Player && Moot(w, f) is { } why) { f.Status = "gestrichen"; f.Done = true; f.Result = why; w.Log(f.Side, "ato", $"{f.Callsign} ({f.Task}) cancelled: {why}."); return; }
            if (w.Time < f.Takeoff || !Launch(w, f)) return;
        }
        (f.X, f.Z, f.Alt) = PosAt(f, w.Time);
        var o1 = f.Route.FirstOrDefault(r => r.Kind == "orbit"); var o2 = f.Route.FirstOrDefault(r => r.Kind == "orbit2");
        if (o1 != null && o2 != null && w.Time >= o1.T && w.Time < o2.T && f.Status != "Abbruch") f.Status = "im Einsatz";
        else if (f.Status == "im Einsatz") f.Status = "Rückflug";
        if (f.Task == "CAP" && f.Missiles == 0 && f.Status == "im Einsatz") Abort(w, f, "missiles expended");
        var tw = f.Route.FirstOrDefault(r => r.Kind == "target");
        if (tw != null && !f.Struck && w.Time >= tw.T && f.Status != "Abbruch") { f.Struck = true; Strike(w, f); }
        if (f.Struck && f.Status == "in der Luft") f.Status = "Rückflug";
        if (f.Task == "CAS" && f.Status == "im Einsatz" && w.Time.Minute % 10 == 0 && f.Passes < 2) { f.Passes++; Cas(w, f); }
        if (f.Task == "SEAD" && (tw ?? o1) is { } sw && w.Time >= sw.T.AddMinutes(-6) && w.Time <= (o2?.T ?? sw.T.AddMinutes(8)) && (tw != null || w.Time.Minute % 10 == 0)) Harm(w, f, null);   // auf Station: alle 10 min
        if (w.Time >= f.Land) LandFlight(w, f);
    }

    /// Lohnt der geplante Angriff noch? Grund zum Streichen oder null.
    static string? Moot(World w, Flight f) => f.TargetKind switch
    {
        "obj" => w.Objs.FirstOrDefault(o => o.Id == f.Target) is not { } o || o.Health <= 0.05 && f.Task != "Recon" ? "target already destroyed" : o.Side == f.Side ? "target now in friendly hands" : null,
        "unit" => w.Units.Any(u => u.Id == f.Target) ? null : "target unit wiped out",
        "convoy" => w.Convoys.Any(c => c.Id == f.Target) ? null : "convoy no longer en route",
        "pilot" => w.Downed.Any(d => d.Id == f.Target) ? null : "pilot already rescued or captured",
        "lift" => w.Objs.FirstOrDefault(o => o.Id == f.Target) is { } o && o.Side == f.Side && o.Health > 0.3 ? null : "destination airfield lost",
        "drop" => w.Nodes[f.Target].Owner == f.Side ? "objective already in friendly hands" : null,
        _ => null,
    };

    /// Start: Maschinen, Piloten, Sprit/Munition vom Platz. false = gestrichen.
    public static bool Launch(World w, Flight f)
    {
        var q = SqnOf(w, f);
        var b = BaseOf(w, q);
        if (!Open(b, 0.35) || b.Side != f.Side) { f.Status = "gestrichen"; f.Done = true; f.Result = $"{b.Name} closed"; return false; }
        if (q.Ac < f.N) { if (q.Ac <= 0) { f.Status = "gestrichen"; f.Done = true; f.Result = "no aircraft ready"; return false; } f.N = q.Ac; }
        double pol = PolOf(f.Type) * f.N * (f.Task == "Tanker" ? 1.5 : 1), mun = MunOf(f.Task) * f.N;    // Tanker geben Sprit ab
        if (b.Fuel < pol) { f.Status = "gestrichen"; f.Done = true; f.Result = $"no fuel at {b.Name}"; w.Log(f.Side, "ato", $"{f.Callsign} ({f.Task}) cancelled: no fuel at {b.Name} ({b.Fuel:0} t).", b.X, b.Z); return false; }
        if (!Fits(w, b, f.Load, f.N))
        {
            if (!f.Player && f.Pyl == null && ChooseLoad(w.Th, f.Type, f.Task, true) is var cheap && Fits(w, b, cheap, f.N)) f.Load = cheap;   // KI: sparsame Beladung
            else
            {
                f.Result = $"not enough weapons at {b.Name}: {Short(w, b, f.Load, f.N)}";
                if (f.Player) return false;                                   // Spieler: Beladung ändern, Flug bleibt geplant
                f.Status = "gestrichen"; f.Done = true;
                w.Log(f.Side, "ato", $"{f.Callsign} ({f.Task}) cancelled: {f.Result}.", b.X, b.Z);
                return false;
            }
        }
        foreach (var (c, k) in Need(f.Load)) Stores(w, b)[c] -= k * f.N;
        if (w.Mission != "" && w.WhStart.ContainsKey(b.BaseId))                       // DCS läuft: Start schon gebucht, beim Lagerabgleich nicht doppelt abziehen
        {
            var u = w.WhUsed.TryGetValue(b.BaseId, out var d) ? d : w.WhUsed[b.BaseId] = [];
            u["fuel"] = u.GetValueOrDefault("fuel") + pol;
            foreach (var (c, k) in Need(f.Load)) u[c] = u.GetValueOrDefault(c) + k * f.N;
        }
        q.Ac -= f.N; f.Alive = f.N;
        f.Missiles = A2aCount(f.Load) * f.N; f.Arm = ArmCount(f.Load) * f.N;
        Sim.Fl(b.Side, "- Flugbetrieb", pol + Math.Min(b.Stock, mun)); b.Fuel -= pol; b.Stock = Math.Max(0, b.Stock - mun);
        if (Lift(f.Type) > 0 && f.TargetKind == "lift") { f.Cargo = Math.Max(0, Math.Min(Lift(f.Type) * f.N, Math.Min(b.Stock, b.Fuel))); b.Stock -= f.Cargo / 2; b.Fuel -= f.Cargo / 2; }
        f.Pilots = q.Pilots.Select((p, i) => (p, i)).Where(t => t.p.Status == "aktiv").OrderBy(t => t.p.Fatigue).Take(f.N).Select(t => t.i).ToList();
        foreach (var i in f.Pilots) { q.Pilots[i].Sorties++; q.Pilots[i].Fatigue += 0.3; }
        f.Status = "in der Luft";
        return true;
    }

    public static void LandFlight(World w, Flight f)
    {
        var q = SqnOf(w, f);
        f.Status = "gelandet"; f.Done = true;
        var hb = BaseOf(w, q);
        double back = Geo.Dist(f.X, f.Z, hb.X, hb.Z) / PerfOf(f.Type).Spd / 60;   // woanders gelandet (Transporter): leer zurück zum Heimatplatz, abstrakt
        for (int i = 0; i < f.Alive; i++) q.Repair.Add(w.Time.AddMinutes(90 + back));
        var need = Need(f.Load); int a2a = need.Where(kv => kv.Key.StartsWith("AAM")).Sum(kv => kv.Value);
        var st = Stores(w, hb);                                             // Ungenutztes zurück ins Lager
        foreach (var (c, k) in need)
            st[c] = st.GetValueOrDefault(c) + Math.Min(k * f.Alive, c switch
            {
                "AAM-R" or "AAM-IR" => a2a > 0 ? (int)Math.Round((double)f.Missiles * k / a2a) : 0,
                "ARM" => f.Arm,
                _ => f.Struck || f.Task == "CAS" ? 0 : k * f.Alive,
            });
        if (f.Cargo > 0) Unload(w, f);
        if (string.IsNullOrEmpty(f.Result)) f.Result = "uneventful";
        foreach (var i in f.Pilots) Learn(w, q, q.Pilots[i]);
    }

    /// Abschuss gutschreiben: ab dem fünften ist der Pilot ein Ass (Meldung, Zeitung)
    public static void Credit(World w, Sqn q, int i)
    {
        var p = q.Pilots[i];
        if (++p.Kills == 5) w.Log(q.Side, "luft", $"{p.Name} ({q.Name}) is now an ace – 5 air victories.");
        Learn(w, q, p);
    }

    /// Piloten lernen: je 8 Einsätze oder 2 Abschüsse eine Stufe, bis Excellent
    static void Learn(World w, Sqn q, Pilot p)
    {
        if (p.Skill >= 3 || p.Status != "aktiv" || p.Skill >= 1 + p.Sorties / 8 + p.Kills / 2) return;
        p.Skill++;
        w.Log(q.Side, "luft", $"{p.Name} ({q.Name}) gains experience – now {Miz.Skill[p.Skill]}.");
    }

    /// Fracht an den eigenen Platz, auf dem der Transporter gelandet ist; mit abgeschossenen Maschinen ging ihr Anteil verloren.
    static void Unload(World w, Flight f)
    {
        double c = f.Cargo * f.Alive / Math.Max(1, f.N);
        f.Cargo = 0;
        var d = w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == f.Side && o.BaseId > 0).MinBy(o => Geo.Dist(o.X, o.Z, f.X, f.Z));
        if (d == null || Geo.Dist(d.X, d.Z, f.X, f.Z) > 10000 || c <= 0) return;
        d.Stock = Math.Min(d.Cap, d.Stock + c / 2); d.Fuel = Math.Min(d.Cap, d.Fuel + c / 2);
        f.Result += $" {c:0} t delivered to {d.Name}.";
        w.Log(f.Side, "logistik", $"Airlift: {f.Callsign} ({f.Type}) delivers {c:0} t of ammo and fuel to {d.Name}.", d.X, d.Z);
    }

    public static void Abort(World w, Flight f, string why)
    {
        var b = BaseOf(w, SqnOf(w, f));
        f.Status = "Abbruch"; f.Result = why;
        var keep = f.Route.Where(r => r.T <= w.Time).ToList();
        double v = PerfOf(f.Type).Spd;
        keep.Add(new Wp { Name = "Abort", Kind = "nav", X = f.X, Z = f.Z, Alt = f.Alt, Spd = v, T = w.Time });
        keep.Add(new Wp { Name = b.Name, Kind = "land", X = b.X, Z = b.Z, Alt = w.Th.Height(b.X, b.Z), Spd = v, T = w.Time.AddSeconds(Geo.Dist(f.X, f.Z, b.X, b.Z) / v + 300) });
        f.Route = keep; f.Land = keep[^1].T;
        f.Struck = true;
    }

    // ---------- Wirkung am Ziel ----------

    /// Fotoaufklärung am Ziel: wahrer Zustand des Ziels und von allem im Umkreis von 15 km (bestätigte BDA); in DCS über die Position (Bridge, POS).
    public static void Photo(World w, Flight f)
    {
        if (!f.InDcs) PointDefense(w, f);
        if (f.Alive <= 0) return;
        var know = w.Intel(f.Side);
        var tp = f.Route.FirstOrDefault(r => r.Kind == "target") is { } t ? new P(t.X, t.Z) : new P(f.X, f.Z);
        foreach (var o in w.Objs.Where(o => o.Side != f.Side && o.Side != Side.None && !(o.Decoy && o.Exposed) && Geo.Dist(o.X, o.Z, tp.X, tp.Z) < 15000))
        {
            var old = know.GetValueOrDefault($"O{o.Id}");
            know[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = $"Recon {f.Callsign}", Str = o.Health, Parts = o.Parts?.ToArray() };
            if (f.TargetKind == "obj" && o.Id == f.Target)
            {
                f.Result = $"Photos of {o.Name}: condition {o.Health:P0}{(old is { Claim: true } ? $" (claimed {old.Str:P0})" : "")}.";
                w.Log(f.Side, "luft", $"{f.Callsign} ({f.Type}) recon: {f.Result}", o.X, o.Z);
            }
        }
        foreach (var u in w.Units.Where(u => u.Side != f.Side && Geo.Dist(u.X, u.Z, tp.X, tp.Z) < 15000))
            know[$"U{u.Id}"] = new Sight { X = u.X, Z = u.Z, T = w.Time, Src = $"Recon {f.Callsign}", Str = u.Str };
        if (f.TargetKind == "obj" && w.Objs.FirstOrDefault(o => o.Id == f.Target) is { } g && Geo.Dist(g.X, g.Z, tp.X, tp.Z) >= 15000)
        {
            know.Remove($"O{g.Id}");                                       // Stellung leer (verlegt): Sichtung verworfen
            f.Result = $"{g.Name}: site empty – relocated.";
            w.Log(f.Side, "luft", $"{f.Callsign} recon: {g.Name} no longer at the reported position.", tp.X, tp.Z);
        }
        f.Struck = true;
    }

    /// Flugplatz offen: Piste mindestens so heil (andere Ziele: Gesamtzustand)
    public static bool Open(Obj b, double min = 0.4) => b.P("Runway") >= min;

    /// Zielpunkt der KI: Plätze erst die Piste sperren, dann Maschinen in den Shelters; Lager das Vollere; Werke die Hallen.
    public static int AutoPart(World w, Obj o) => o.Kind switch
    {
        OK.Airbase => Open(o) ? 0 : w.Sqns.Any(q => q.Base == o.Id && q.Ac > 0) ? 1 : 2,
        OK.Depot => o.Fuel > o.Stock ? 1 : 0,
        OK.Factory => o.P("Production halls") > 0.3 ? 0 : 1,
        _ => 0,
    };

    /// Zielpunkt für Kniebrett/Liste: " – aim: Runway" bzw. ""
    public static string AimText(World w, Flight f) =>
        f.TargetKind == "obj" && w.Objs.FirstOrDefault(o => o.Id == f.Target) is { } o && Scenario.Parts.TryGetValue(o.Kind, out var l) && f.Task is not ("Recon" or "DEAD" or "SEAD")
            ? $" – aim: {l[f.Part >= 0 && f.Part < l.Length ? f.Part : AutoPart(w, o)].Name}" : "";

    /// Treffer auf einen Teil mit Folgen: Lager brennen, Maschinen in den Shelters, Piste zu. Liefert „Runway 100 % → 30 %“ (+ Folgen).
    public static string HitPart(World w, Obj o, int part, double dmg)
    {
        var name = Scenario.Parts[o.Kind][part].Name;
        bool open = Open(o);
        double before = o.P(name);
        o.Hit(part, dmg);
        double d = before - o.P(name);
        string extra = "";
        switch (name)
        {
            case "Fuel farm" or "Fuel tanks" or "Fuel terminal":
                double lp = Math.Min(0.6, d * 0.8); Sim.Fl(o.Side, "- Bomben auf Lager", o.Fuel * lp); o.Fuel *= 1 - lp; break;
            case "Ammo dump" or "Warehouses" or "Cargo handling":
                double lm = Math.Min(0.5, d * 0.6); Sim.Fl(o.Side, "- Bomben auf Lager", o.Stock * lm); o.Stock *= 1 - lm; break;
            case "Shelters":
                foreach (var q in w.Sqns.Where(q => q.Base == o.Id && q.Ac > 0))
                {
                    int kill = Math.Min(q.Ac, (int)Math.Round(q.Ac * d * 0.5 * (0.5 + w.Rnd())));
                    if (kill > 0) { q.Ac -= kill; q.Lost += kill; extra += $" {kill} {q.Type} destroyed in their shelters."; }
                }
                break;
        }
        if (o.Kind == OK.Airbase && open && !Open(o)) w.Log(o.Side, "logistik", $"{o.Name}: runway cut – airfield closed until repaired.", o.X, o.Z);
        return $"{name} {before:P0} → {o.P(name):P0}{extra}";
    }

    static double Wx(World w, string load) => Laser(load) && w.Wx.Cloud > 0.75 && w.Wx.Base < 3500 ? 0.35 : w.Wx.Rain ? 0.7 : 1;

    internal static void Strike(World w, Flight f)
    {
        if (f.Task == "CSAR") { Rescue(w, f); return; }
        if (f.Task == "Recon") { Photo(w, f); return; }
        if (f.TargetKind == "drop" && f.Task == "Transport") { Sim.Drop(w, f); return; }
        if (f.Task is "CAS" or "SEAD" or "Escort" or "CAP" or "Sweep" or "AWACS" or "Tanker" or "Transport") return;
        var (ps, pv) = Punch(f.Load);
        double wf = Wx(w, f.Load) * (0.7 + 0.6 * w.Rnd());
        var know = w.Intel(f.Side);
        PointDefense(w, f);
        if (f.Alive <= 0) return;
        if (f.TargetKind == "obj" && w.Objs.FirstOrDefault(o => o.Id == f.Target) is { } o)
        {
            if (o.Health <= 0.05) { f.Result = "Target already destroyed"; return; }
            if (o.Kind is OK.Sam or OK.Tel && f.Route.FirstOrDefault(r => r.Kind == "target") is { } tp && Geo.Dist(o.X, o.Z, tp.X, tp.Z) > 3000
                && !(o.Kind == OK.Tel && Geo.Dist(o.X, o.Z, tp.X, tp.Z) < 15000 && w.Chance(0.4)))   // Werfer: in der Nähe noch 40 % Chance, ihn beim Absuchen zu finden
            {
                know.Remove($"O{o.Id}");                                   // Stellung leer: Sichtung verworfen, bis sie neu aufgeklärt ist
                f.Result = o.Kind == OK.Tel ? $"{o.Name}: launcher gone – it moved." : $"{o.Name}: site empty – relocated.";
                w.Log(f.Side, "luft", $"{f.Callsign}: {o.Name} no longer at the reported position – relocated.", tp.X, tp.Z);
                return;
            }
            double dmg = ps * f.Alive * wf * (o.Kind == OK.Bridge ? 0.8 : o.Kind is OK.Airbase or OK.Depot ? 0.5 : 1);
            double before = o.Health;
            string aim = "";
            if (Scenario.Parts.TryGetValue(o.Kind, out var pl))               // gebündelt auf einen Teil (Zielpunkt)
            {
                int pi = f.Part >= 0 && f.Part < pl.Length ? f.Part : AutoPart(w, o);
                aim = " – " + HitPart(w, o, pi, Math.Min(1, dmg * 0.7 / pl[pi].W));
            }
            else o.Health = Math.Max(0, o.Health - dmg);
            if (o.Kind == OK.Sam && o.Health < 0.3) o.Missiles = 0;
            double claim = 0.25 * w.Rnd() - 0.05;                           // Piloten überschätzen gern
            know[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = $"Pilot report {f.Callsign}", Str = Math.Clamp(o.Health - claim, 0, 1), Claim = true,
                Parts = o.Parts?.Select(p => Math.Clamp(p - claim * 2, 0, 1)).ToArray() };
            f.Result = $"{o.Name} hit{aim}: condition {before:P0} → {o.Health:P0}.{f.Result}";
            w.Log(f.Side, "luft", $"{f.Callsign} ({f.Type}) attacks {o.Name}{aim}: condition {before:P0} → {o.Health:P0}.", o.X, o.Z);
            if (o.Decoy) w.Log(o.Side, "flugabwehr", $"{o.Name} (decoy) attacked by {f.Callsign} – the enemy wasted a sortie.", o.X, o.Z);
            if (o.Kind == OK.Bridge && o.Health < 0.3 && before >= 0.3) w.Log(Side.None, "logistik", $"{o.Name} destroyed – supply must be rerouted.", o.X, o.Z);
        }
        else if (f.TargetKind == "convoy" && w.Convoys.FirstOrDefault(c => c.Id == f.Target) is { Navy: true } nv && Geo.Dist(nv.X, nv.Z, f.X, f.Z) < 160000)
        {
            int n = Need(f.Load).GetValueOrDefault("AShM") * f.Alive;     // Seezielflugkörper aus der Abstandsposition
            bool big = Sim.NavyRange(nv) > 40000;
            if (n == 0)                                                     // nur Bomben: über die Schiffe hinweg, mitten in die Flugabwehr
            {
                for (int i = f.Alive; i > 0; i--) if (w.Chance(big ? 0.3 : 0.15)) Loss(w, f, $"ship air defense ({nv.Name})", nv.Side);
                if (f.Alive <= 0) return;
            }
            string name = nv.Name, ms = n > 0 ? $"{n} anti-ship missiles" : "bombs";
            int hits = Enumerable.Range(0, n > 0 ? n : f.Alive).Count(_ => w.Chance(n > 0 ? (big ? 0.15 : 0.3) : 0.12 * wf));
            int sunk = Sim.Sink(w, nv, hits);
            if (w.Convoys.Contains(nv)) know[$"C{nv.Id}"] = new Sight { X = nv.X, Z = nv.Z, T = w.Time, Src = $"Pilot report {f.Callsign}", Str = nv.Trucks };
            f.Result = $"{name}: {sunk} ship(s) sunk ({ms}).";
            w.Log(f.Side, "luft", $"{f.Callsign} ({f.Type}) attacks {name} with {ms}: {sunk} ship(s) sunk.", nv.X, nv.Z);
            if (sunk > 0) w.Log(Side.None, "see", $"{name}: {sunk} ship(s) sunk by an air strike.", nv.X, nv.Z);
        }
        else if (f.TargetKind == "convoy" && w.Convoys.FirstOrDefault(c => c.Id == f.Target) is { } cv && Geo.Dist(cv.X, cv.Z, f.X, f.Z) < 60000)
        {
            int kill = Math.Min(cv.Trucks, (int)Math.Round(pv * f.Alive * wf));
            Sim.Fl(cv.Side, "- Transport zerstört", (cv.Tons + cv.Fuel) * kill / Math.Max(1, cv.Trucks));
            double keep = 1 - (double)kill / Math.Max(1, cv.Trucks);
            cv.Tons *= keep; cv.Fuel *= keep; cv.Trucks -= kill;
            var (what, items) = Sim.Kind(cv);
            f.Result = $"{what} hit: {kill} {items} destroyed.";
            w.Log(f.Side, "luft", $"{f.Callsign} hits {what.ToLowerInvariant()} bound for {Sim.ObjName(w, cv.To)}: {kill} {items} destroyed.", cv.X, cv.Z);
            if (cv.Trucks <= 0) w.Convoys.Remove(cv);
        }
        else if (f.TargetKind is "unit" or "node") Cas(w, f);
        else f.Result = "Target not found";
    }

    static void Cas(World w, Flight f)
    {
        var (_, pv) = Punch(f.Load);
        var tw = f.Route.FirstOrDefault(r => r.Kind is "target" or "orbit");
        if (tw == null) return;
        var tgt = f.TargetKind == "node" && f.Target >= 0 && f.Target < w.Nodes.Count ? new P(w.Nodes[f.Target].X, w.Nodes[f.Target].Z) : new P(tw.X, tw.Z);   // CAS kreist hinten, wirkt an der Front
        bool Mine(Unit u) => f.TargetKind == "unit" && u.Id == f.Target;
        var foes = w.Units.Where(u => u.Side != f.Side && !u.InDcs && Geo.Dist(u.X, u.Z, tgt.X, tgt.Z) < (Mine(u) ? 30000 : 15000))
            .OrderBy(u => Mine(u) ? 0 : u.Kind == UK.Shorad ? 2 : 1).ToList();
        if (foes.Count == 0) { if (f.Task != "CAS") f.Result = "no targets found"; return; }
        PointDefense(w, f);
        if (f.Alive <= 0) return;
        var u = foes[0];
        double kills = pv * f.Alive * Wx(w, f.Load) * (0.3 + 0.5 * w.Rnd()) * (f.Task == "CAS" ? 0.6 : 0.8);
        double before = u.Str;
        Sim.Hit(w, u, kills / 40);                                         // Bataillon ≈ 40 Fahrzeuge
        u.Morale = Math.Max(0, u.Morale - 0.08);
        f.Result = $"{u.Name}: {kills:0.#} vehicles destroyed.";
        w.Log(f.Side, "luft", $"{f.Callsign} ({f.Type}) attacks {u.Name}: ~{kills:0.#} vehicles destroyed ({before:P0} → {Math.Max(0, u.Str):P0}).", u.X, u.Z);
    }

    static void PointDefense(World w, Flight f)
    {
        if (f.Alt > 4500) return;
        int shorad = w.Units.Count(u => u.Side != f.Side && u.Kind == UK.Shorad && Geo.Dist(u.X, u.Z, f.X, f.Z) < 12000);
        double p = 0.01 + 0.015 * Math.Min(3, shorad);
        for (int i = f.Alive; i > 0; i--) if (w.Chance(p)) Loss(w, f, "air defense at the target", f.Side.Enemy());
    }

    // ---------- Gefechte in der Luft und Flugabwehr ----------

    public static bool Up(Flight f) => !f.Done && f.Alive > 0 && f.Status is "in der Luft" or "im Einsatz" or "Rückflug" or "Abbruch";
    static bool Airborne(Flight f) => Up(f) && !f.InDcs;

    internal static void Engage(World w)
    {
        var air = w.Flights.Where(Airborne).ToList();
        if (air.Count == 0) return;
        // Flugabwehr
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles > 0 && o.QuietUntil <= w.Time && !o.InDcs))
            foreach (var f in air.Where(f => f.Side != o.Side && f.Alive > 0))
            {
                double d = Geo.Dist(o.X, o.Z, f.X, f.Z);
                if (d > o.Range || f.Alt > o.MaxAlt) continue;
                if (f.Alt < w.Th.Peak(f.X, f.Z, 3000) + 400 && d > 15000) continue;     // Tiefflug (wie in BuildRoute geplant) unterfliegt das Radar
                bool cue = Sim.Radar(w, o.Side, f.X, f.Z, f.Alt) != null;
                if (!cue && d > o.Range * 0.5) continue;                       // EMCON: ohne Zielzuweisung (Frühwarnradar/AWACS) schaltet die Batterie erst spät auf
                if (air.Any(s => s.Side == f.Side && s.Task == "SEAD" && s.Arm > 0 && s.Alive > 0 && Geo.Dist(s.X, s.Z, o.X, o.Z) < 80000) && w.Chance(0.5))
                {
                    if (o.QuietUntil < w.Time.AddMinutes(-30)) w.Log(o.Side, "flugabwehr", $"{o.Name} goes dark: enemy SEAD with anti-radiation missiles nearby.", o.X, o.Z);
                    o.QuietUntil = w.Time.AddMinutes(10);                       // dunkel = kein Ziel für HARM, schießt aber auch nicht
                    break;
                }
                bool jam = air.Any(s => s.Side == f.Side && s.Task == "Jammer" && s.Alive > 0 && Geo.Dist(s.X, s.Z, o.X, o.Z) < 120000);
                if (jam && d > o.Range * 0.4) continue;                         // Störer im Paket: Radar sieht erst innerhalb der Burn-through-Entfernung
                if (o.Decoy && o.Exposed) break;                                // entlarvte Attrappe: niemand fällt mehr darauf herein
                if (!w.Chance(cue ? 0.35 : 0.2)) continue;
                w.Intel(f.Side)[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = $"RWR {f.Callsign}", Str = o.Health };
                if (w.Chance(0.7) && Harm(w, null, o)) break;
                if (o.Decoy) { o.QuietUntil = w.Time.AddMinutes(10); break; }   // Attrappe strahlt nur, schießt nie
                bool big = o.Sys is "SA-10" or "Patriot";
                int shots = Math.Min(big ? 2 : 1, o.Missiles);
                o.Missiles -= shots;
                o.QuietUntil = w.Time.AddMinutes(5);                       // neu zielen / nachladen
                double pk = (big ? 0.15 : o.Sys is "SA-11" or "Hawk" ? 0.12 : 0.1) * (f.Task == "SEAD" ? 0.5 : 0.7) * (jam ? 0.6 : 1);   // Täuschkörper, Ausweichen, Störung: die meisten Schüsse gehen vorbei
                for (int i = 0; i < shots && f.Alive > 0; i++) if (w.Chance(pk)) Loss(w, f, $"{o.Sys} ({o.Name})", o.Side);
                if (f.Alive > 0 && Attack.Contains(f.Task) && !f.Struck && w.Chance(0.25)) Abort(w, f, $"aborted after {o.Sys} fire");
                break;
            }
        foreach (var u in w.Units.Where(u => u.Kind == UK.Shorad && !u.InDcs && u.Str > 0.2))
            foreach (var f in air.Where(f => f.Side != u.Side && f.Alive > 0 && f.Alt < 3500 && Geo.Dist(u.X, u.Z, f.X, f.Z) < 8000))
                if (w.Chance(0.2)) { if (w.Chance(0.15)) Loss(w, f, $"air defense {u.Name}", u.Side); w.Intel(f.Side)[$"U{u.Id}"] = new Sight { X = u.X, Z = u.Z, T = w.Time, Src = $"Ground fire {f.Callsign}", Str = u.Str }; }
        foreach (var c in w.Convoys.Where(c => c.Navy && !c.InDcs && c.Trucks > 0))       // Schiffsflugabwehr: ein Ziel je Verband
        {
            double r = Sim.NavyRange(c);
            if (w.Time.Minute % 5 != 0 || air.FirstOrDefault(f => f.Side != c.Side && f.Alive > 0 && f.Alt < 15000 && Geo.Dist(c.X, c.Z, f.X, f.Z) < r) is not { } f || !w.Chance(0.5)) continue;   // alle 5 min: zielen, nachladen
            w.Intel(f.Side)[$"C{c.Id}"] = new Sight { X = c.X, Z = c.Z, T = w.Time, Src = $"RWR {f.Callsign}", Str = c.Trucks };
            if (w.Chance((r > 40000 ? 0.12 : 0.08) * (f.Task == "SEAD" ? 0.5 : 0.7))) Loss(w, f, $"ship air defense ({c.Name})", c.Side);
        }
        // Luft-Luft: Jäger schützen ihre Station bzw. ihr Paket; Begleitschutz fängt Angreifer ab
        foreach (var a in air.Where(a => a.Alive > 0 && a.Missiles > 0 && A2A.Contains(a.Task) && a.Busy <= w.Time))
        {
            var station = a.Route.FirstOrDefault(r => r.Kind is "orbit" or "target");
            Flight? best = null; double bd = double.MaxValue;
            foreach (var b in air.Where(b => b.Side != a.Side && b.Alive > 0 && b.Busy <= w.Time))
            {
                double d = Geo.Dist(a.X, a.Z, b.X, b.Z);
                bool commit = a.Task switch
                {
                    "CAP" => station != null && Geo.Dist(b.X, b.Z, station.X, station.Z) < 50000 && d < 70000,
                    "Sweep" => d < 90000,
                    _ => d < 30000 && A2A.Contains(b.Task),
                };
                if (!commit || (Sim.Radar(w, a.Side, b.X, b.Z, b.Alt) == null && d > 80000)) continue;
                if (d < bd) { bd = d; best = b; }
            }
            if (best == null) continue;
            // Begleitschutz des Ziels stellt sich dazwischen
            var esc = air.FirstOrDefault(e => e.Pkg == best.Pkg && e.Task == "Escort" && e.Alive > 0 && e.Missiles > 0 && e.Busy <= w.Time && Geo.Dist(e.X, e.Z, best.X, best.Z) < 40000);
            if (esc != null && esc != best) best = esc;
            a.Busy = best.Busy = w.Time.AddMinutes(20);
            Dogfight(w, a, best);
        }
    }

    static double Skill(World w, Flight f)
    {
        var q = SqnOf(w, f);
        return f.Pilots.Count == 0 ? 1.5 : f.Pilots.Average(i => q.Pilots[i].Skill);
    }

    static void Dogfight(World w, Flight a, Flight b)
    {
        double ka = Skill(w, a), kb = Skill(w, b);
        bool bFighter = A2A.Contains(b.Task) || b.Missiles > 0;
        int shots = Math.Min(a.Alive, a.Missiles);
        a.Missiles -= shots;
        int hitsA = 0, hitsB = 0;
        for (int i = 0; i < shots && b.Alive > 0; i++)
            if (w.Chance(0.035 * ka / kb * (b.Task is "AWACS" or "Tanker" or "Transport" ? 3 : bFighter ? 1 : 1.5))) { Loss(w, b, $"air combat with {a.Callsign} ({a.Type})", a.Side); hitsA++; }
        if (b.Alive > 0 && b.Missiles > 0 && bFighter)
        {
            int ret = Math.Min(b.Alive, b.Missiles);
            b.Missiles -= ret;
            for (int i = 0; i < ret && a.Alive > 0; i++)
                if (w.Chance((A2A.Contains(b.Task) ? 0.035 : 0.02) * kb / ka)) { Loss(w, a, $"air combat with {b.Callsign} ({b.Type})", b.Side); hitsB++; }
        }
        if (hitsA > 0) a.Result += $" {hitsA} kill(s).";
        if (hitsB > 0) b.Result += $" {hitsB} kill(s).";
        if (a.Alive > 0 && hitsA > 0) foreach (var i in a.Pilots.Take(hitsA)) Credit(w, SqnOf(w, a), i);
        if (b.Alive > 0 && hitsB > 0) foreach (var i in b.Pilots.Take(hitsB)) Credit(w, SqnOf(w, b), i);
        w.Log(Side.None, "luft", $"Air combat: {a.Callsign} ({a.Side.De()}, {a.Type}) vs. {b.Callsign} ({b.Side.De()}, {b.Type}) – losses {b.Callsign}: {hitsA}, {a.Callsign}: {hitsB}.", b.X, b.Z);
        if (b.Alive > 0 && Attack.Contains(b.Task) && !b.Struck && w.Chance(0.45)) Abort(w, b, $"aborted after intercept by {a.Callsign}");
    }

    /// HARM gegen strahlende Stellung. Mit Flug = SEAD sucht selbst, ohne Flug = Reaktion auf Abstrahlung von o.
    static bool Harm(World w, Flight? sead, Obj? site)
    {
        var shooters = sead != null ? [sead] : w.Flights.Where(f => Airborne(f) && f.Arm > 0 && site != null && f.Side != site.Side && Geo.Dist(f.X, f.Z, site.X, site.Z) < 90000).ToList();
        foreach (var f in shooters)
        {
            if (f.Arm <= 0 || f.Alive <= 0) continue;
            var o = site ?? w.Objs.Where(o => o.Kind == OK.Sam && o.Side != f.Side && o.Health > 0.3 && o.QuietUntil <= w.Time && Geo.Dist(f.X, f.Z, o.X, o.Z) < 90000).MinBy(o => Geo.Dist(f.X, f.Z, o.X, o.Z));
            if (o == null || o.InDcs) return false;
            f.Arm--;
            // HARM trifft nur das Radar: Starter bleiben, Ersatzradar kommt (Sim.Hourly); endgültig zerstört nur DEAD
            if (w.Chance(0.4)) { o.Health = Math.Max(0.1, o.Health - 0.6); w.Log(f.Side, "luft", $"{f.Callsign} hits the radar of {o.Name} with a HARM.", o.X, o.Z); f.Result += $" HARM hit on {o.Name}."; }
            else { o.QuietUntil = w.Time.AddMinutes(10); f.Result += $" {o.Name} suppressed."; }
            return true;
        }
        return false;
    }

    public static Pilot PilotOf(World w, Downed d) => d.Pilot < 0 ? w.Logbook.First(c => c.Id == ~d.Pilot) : w.Sqns.First(q => q.Id == d.Sqn).Pilots[d.Pilot];

    public static void Loss(World w, Flight f, string how, Side killer, int pilot = -1, bool? ejected = null, Career? human = null)
    {
        if (f.Alive <= 0) return;
        f.Alive--;
        var q = SqnOf(w, f);
        q.Lost++;
        string who = "";
        Pilot? p = null;
        if (f.Pilots.Count > 0)
        {
            int k = pilot >= 0 ? f.Pilots.IndexOf(pilot) : -1;   // DCS meldet, welche Maschine (Pilot) es war
            if (k < 0) k = f.Pilots.Count - 1;
            p = q.Pilots[f.Pilots[k]];
            f.Pilots.RemoveAt(k);
        }
        p = human ?? p;                                         // Mensch im Cockpit: sein Logbuch trägt das Schicksal, der Staffelpilot des Platzes bleibt heil
        if (p != null)
        {
            bool home = OwnerAt(w, f.X, f.Z) == f.Side;
            p.Status = (ejected ?? w.Chance(0.75)) ? (home ? "aktiv" : "vermisst") : "gefallen";
            if (p.Status == "aktiv") p.Fatigue = 2;
            if (p.Status == "vermisst") w.Downed.Add(new Downed { Id = w.NewId(), Side = f.Side, Sqn = q.Id, Pilot = p is Career c ? ~c.Id : q.Pilots.IndexOf(p), X = f.X, Z = f.Z, T = w.Time });
            who = $" Pilot {p.Name}: {(p.Status == "aktiv" ? "recovered" : p.Status == "vermisst" ? "ejected over enemy territory – CSAR under way" : "KIA")}.";
        }
        w.Log(f.Side, "verlust", $"{f.Callsign} ({f.Side.De()}, {f.Type}) loses an aircraft: {how}.{who}", f.X, f.Z);
        f.Result += $" Loss: {how}.";
        if (f.Alive <= 0) { f.Status = "verloren"; f.Done = true; }
    }

    /// CSAR (stündlich): Piloten über Feindgebiet. Mit Rettungsstaffel fliegt ein Einsatz hin (Rettung am Ziel, Flugabwehr kann ihn holen);
    /// ohne Rettungsstaffel holt sie ein Hubschrauber nahe der Front und ohne bekannte Flugabwehr eher heraus (Würfel). Nach 4 h ohne Rettung unterwegs: gefangen.
    public static void Csar(World w)
    {
        foreach (var d in w.Downed.ToList())
        {
            var q = w.Sqns.First(q => q.Id == d.Sqn);
            var p = PilotOf(w, d);
            bool going = w.Flights.Any(f => f.TargetKind == "pilot" && f.Target == d.Id && !f.Done && f.Status != "gestrichen");
            if (w.Sqns.Any(x => x.Side == d.Side && x.Roles.Contains("CSAR")))
            {
                if (!going) going = Rescuers(w, d, p);
                if (!going && w.Time - d.T >= TimeSpan.FromHours(4)) Captured(w, d, p, q);
                continue;
            }
            double front = w.Nodes.Where(n => n.Owner == d.Side).Select(n => new P(n.X, n.Z)).Concat(w.Objs.Where(o => o.Side == d.Side && o.Kind == OK.Airbase).Select(o => new P(o.X, o.Z)))
                .Select(n => Geo.Dist(n.X, n.Z, d.X, d.Z)).DefaultIfEmpty(1e9).Min();   // eigenes Gebiet: Orte oder Flugplätze (ohne Landfront)
            bool sam = Threats(w, d.Side, 300).Any(t => Geo.Dist(t.X, t.Z, d.X, d.Z) < t.R);
            double ch = front < 25000 ? (sam ? 0.15 : 0.4) : front < 60000 ? (sam ? 0.05 : 0.2) : 0.02;
            if (w.Chance(ch)) Saved(w, d, $"CSAR: helicopter extracts {p.Name} ({q.Name}) {front / 1000:0} km beyond the front line.");
            else if (w.Time - d.T >= TimeSpan.FromHours(4)) Captured(w, d, p, q);
        }
    }

    static void Captured(World w, Downed d, Pilot p, Sqn q)
    {
        p.Status = "gefangen"; w.Downed.Remove(d);
        if (p is Career c) Career.Captured(w, c);
        w.Log(d.Side, "verlust", $"{p.Name} ({q.Name}) taken prisoner – rescue did not get through.", d.X, d.Z);
    }

    /// Rettungseinsatz: nächste Rettungsstaffel in Reichweite, 2 Hubschrauber, Start in etwa 20 min. false = keine frei.
    static bool Rescuers(World w, Downed d, Pilot p)
    {
        double Dist(Sqn x) => Geo.Dist(BaseOf(w, x).X, BaseOf(w, x).Z, d.X, d.Z);
        var q = w.Sqns.Where(x => x.Side == d.Side && x.Roles.Contains("CSAR") && Usable(w, x, 2) && (w.Daylight || PerfOf(x.Type).Night) && Dist(x) < PerfOf(x.Type).Radius * 1000 && Reach(w, x, new(d.X, d.Z))).MinBy(Dist);
        if (q == null) return false;
        var pk = NewPkg(w, d.Side, "CSAR", "pilot", d.Id, p.Name, $"CSAR: {p.Name} ejected beyond the front line – extract");
        pk.Tot = Round5(w.Time.AddMinutes(20).AddSeconds(Dist(q) * 1.15 / PerfOf(q.Type).Spd));
        var f = NewFlight(w, pk, q, "CSAR", 2, new(d.X, d.Z), pk.Tot, 100);
        f.Why = pk.Why;
        w.Log(d.Side, "ato", $"CSAR: {f.Callsign} ({q.Type}) en route to {p.Name}, arrival {pk.Tot:HH:mm}.", d.X, d.Z);
        return true;
    }

    /// Pilot zurück in der Staffel (Rettungseinsatz, Würfel, Landung eines Spielers bei ihm).
    public static void Saved(World w, Downed d, string text)
    {
        var p = PilotOf(w, d);
        p.Status = "aktiv"; p.Fatigue = 2; w.Downed.Remove(d);
        w.Log(d.Side, "verlust", text, d.X, d.Z);
    }

    static void Rescue(World w, Flight f)
    {
        PointDefense(w, f);                                                 // Flugabwehr am Landeplatz
        if (f.Alive <= 0) return;
        if (w.Downed.FirstOrDefault(d => d.Id == f.Target) is not { } d) { f.Result = "pilot no longer there"; return; }
        var q = w.Sqns.First(q => q.Id == d.Sqn);
        var name = PilotOf(w, d).Name;
        f.Result = $"{name} rescued.";
        Saved(w, d, $"CSAR: {f.Callsign} ({f.Type}) extracts {name} ({q.Name}).");
    }

    public static Side OwnerAt(World w, double x, double z)
    {
        var n = w.Nodes.MinBy(n => Geo.Dist(n.X, n.Z, x, z));
        return n == null || Geo.Dist(n.X, n.Z, x, z) > 40000 ? Scenario.Owner(w, x, z) : n.Owner;
    }
}
