using System.Text.RegularExpressions;

namespace DcsCampaign;

/// Jede weitere installierte DCS-Karte (Syrien, Sinai, Kola, Afghanistan, Irak, …) ohne handgemachte Startlage:
/// Projektion aus den Funkfeuern gerechnet (Beacons.lua hat x/z und Lat/Lon), Plätze aus Funkfeuern und Funk (genaue Namen, Lage und
/// Parkplätze lernt die Kampagne aus der DCS-ATC), Städte und Höhen wie bei jeder Karte. Dazu eine erzeugte Startlage:
/// Front quer durch die Mitte der Plätze, Rot greift auf bis zu drei Achsen an, Ziele und Staffeln an den größten Orten bzw. Plätzen.
// ponytail: Startlage nach Schema (gerade Front, Russland gegen NATO); handgemachte Startlagen wie Kaukasus/Golf bei Bedarf je Karte nachziehen.
static class Auto
{
    static string Terr(string map) => Path.Combine(Theater.FindDcs(), @"Mods\terrains", map);
    static readonly Dictionary<string, MapDef?> Defs = [];
    static readonly Dictionary<string, Setup> Sets = [];

    /// Installierte Karten ohne eigene Startlage, deren Daten die Kampagne lesen kann
    public static IEnumerable<string> Maps()
    {
        var d = Path.Combine(Theater.FindDcs(), @"Mods\terrains");
        return Directory.Exists(d) ? Directory.GetDirectories(d).Select(Path.GetFileName).OfType<string>().Where(m => !Theater.Maps.ContainsKey(m) && Def(m) != null) : [];
    }
    static bool Usable(string m) => new[] { "Beacons.lua", "Radio.lua", @"Map\towns.lua", @"extra\lightmap\lightmap.lua", @"extra\lightmap\lightmap.png" }.All(f => File.Exists(Path.Combine(Terr(m), f)));

    public static MapDef? Def(string map) { lock (Defs) return Defs.TryGetValue(map, out var d) ? d : Defs[map] = Usable(map) ? MakeDef(map, Terr(map)) : null; }

    static double N(string s) => double.Parse(s);
    public record Bcn(int Ab, string Name, string Type, double X, double Y, double Z, double Dir, double Lat, double Lon, int Ch, bool Geo);

    /// Funkfeuer der Karte: Platz-Id (airfieldN_k), Art, Lage x/y/z, Richtung, Lat/Lon, TACAN-Kanal
    public static List<Bcn> Beacons(string file)
    {
        var r = new List<Bcn>();
        foreach (var b in File.ReadAllText(file).Split("display_name").Skip(1))
        {
            var p = Regex.Match(b, @"position\s*=\s*\{\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)");
            if (!p.Success) continue;
            var g = Regex.Match(b, @"latitude\s*=\s*(-?[\d.]+)\s*,\s*longitude\s*=\s*(-?[\d.]+)");
            var ab = Regex.Match(b, @"beaconId\s*=\s*'airfield(\d+)_");
            r.Add(new Bcn(ab.Success ? int.Parse(ab.Groups[1].Value) : 0, Regex.Match(b, @"^\s*=\s*_\(\s*[""']([^""']*)").Groups[1].Value,
                Regex.Match(b, @"type\s*=\s*BEACON_TYPE_(\w+)").Groups[1].Value, N(p.Groups[1].Value), N(p.Groups[2].Value), N(p.Groups[3].Value),
                Regex.Match(b, @"direction\s*=\s*(-?[\d.]+)") is { Success: true } d ? N(d.Groups[1].Value) : 0,
                g.Success ? N(g.Groups[1].Value) : 0, g.Success ? N(g.Groups[2].Value) : 0,
                Regex.Match(b, @"channel\s*=\s*(\d+)") is { Success: true } c ? int.Parse(c.Groups[1].Value) : 0, g.Success));
        }
        return r;
    }

    /// Transversal-Mercator wie DCS (WGS84, k0 0,9996): Mittelmeridian (halbe Grad) mit dem kleinsten Rest, Versatz = Mittel der Abweichungen
    public static (double Lon0, double FE, double FN, double Err) Fit(IList<(double Lat, double Lon, double X, double Z)> pts)
    {
        var best = (Lon0: 0.0, FE: 0.0, FN: 0.0, Err: double.MaxValue);
        double c = Math.Round(pts.Average(p => p.Lon));
        for (double l0 = c - 15; l0 <= c + 15; l0 += 0.5)
        {
            var off = pts.Select(p => Geo.Raw(p.Lat, p.Lon, l0) is var r ? (N: p.X - r.X, E: p.Z - r.Z) : default).ToList();
            double fn = off.Average(o => o.N), fe = off.Average(o => o.E);
            double err = Math.Sqrt(off.Average(o => (o.N - fn) * (o.N - fn) + (o.E - fe) * (o.E - fe)));
            if (err < best.Err) best = (l0, fe, fn, err);
        }
        return best;
    }

    /// Plätze aus den Rollwegnetzen (AirfieldsTaxiways\Name.rn4: Ketten von Double-Tripeln x, Höhe, z), Id über den Kommentar in Radio.lua.
    /// Mitte = Median der Punkte, Piste = längste gerade Kette (ab 500 m), Span = 95 % der Punkte liegen so nah an der Mitte (Hubschrauberplätze klein).
    public static Dictionary<int, (double X, double Y, double Z, double Hdg, double Span)> Taxiways(string dir, string radio)
    {
        var r = new Dictionary<int, (double, double, double, double, double)>();
        var td = Path.Combine(dir, "AirfieldsTaxiways");
        if (!Directory.Exists(td)) return r;
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(radio, @"--[ \t]*([^\r\n]+?)[ \t]*\r?\n\s*radioId\s*=\s*'airfield(\d+)_0'")) ids.TryAdd(m.Groups[1].Value, int.Parse(m.Groups[2].Value));
        foreach (var f in Directory.GetFiles(td, "*.rn4"))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            if (!ids.TryGetValue(n, out int id)) continue;
            var b = File.ReadAllBytes(f);
            (double X, double Y, double Z)? Pt(int o)
            {
                double x = BitConverter.ToDouble(b, o), y = BitConverter.ToDouble(b, o + 8), z = BitConverter.ToDouble(b, o + 16);
                return Math.Abs(x) is > 100 and < 1.5e6 && Math.Abs(z) is > 100 and < 1.5e6 && y is > -500 and < 5000 ? (x, y, z) : null;
            }
            var chains = new List<List<(double X, double Y, double Z)>>(); var cur = new List<(double X, double Y, double Z)>();
            for (int o = 0; o + 24 <= b.Length;)
            {
                var p = Pt(o);
                if (p is not { } q || cur.Count > 0 && Geo.Dist(q.X, q.Z, cur[^1].X, cur[^1].Z) > 1500) { if (cur.Count > 1) chains.Add(cur); cur = []; }
                if (p is { } v) { cur.Add(v); o += 24; } else o += 4;
            }
            if (cur.Count > 1) chains.Add(cur);
            var pts = chains.SelectMany(c => c).ToList();
            if (pts.Count < 10) continue;
            static double Q(IEnumerable<double> v, double q) { var s = v.Order().ToList(); return s[(int)(q * (s.Count - 1))]; }
            double mx = Q(pts.Select(p => p.X), 0.5), mz = Q(pts.Select(p => p.Z), 0.5);
            var rwy = chains.Select(c => (c, D: Geo.Dist(c[0].X, c[0].Z, c[^1].X, c[^1].Z))).Where(x => x.D > 500 && x.D > 0.97 * x.c.Zip(x.c.Skip(1), (a, e) => Geo.Dist(a.X, a.Z, e.X, e.Z)).Sum())
                .OrderByDescending(x => x.D).Select(x => x.c).FirstOrDefault();
            double h = rwy == null ? 0 : (Math.Atan2(rwy[^1].Z - rwy[0].Z, rwy[^1].X - rwy[0].X) * 180 / Math.PI + 360) % 180;
            r[id] = (mx, Q(pts.Select(p => p.Y), 0.5), mz, h, Q(pts.Select(p => Geo.Dist(p.X, p.Z, mx, mz)), 0.95));
        }
        return r;
    }

    /// Kartendefinition aus der Installation (dir = Mods\terrains\Karte)
    public static MapDef? MakeDef(string map, string dir)
    {
        var bs = Beacons(Path.Combine(dir, "Beacons.lua"));
        var geo = bs.Where(b => b.Geo).Select(b => (b.Lat, b.Lon, b.X, b.Z)).ToList();
        if (geo.Count < 3) return null;
        var (lon0, fe, fn, err) = Fit(geo);
        if (err > 500) return null;                                         // keine TM-Projektion wie erwartet
        var radio = File.ReadAllText(Path.Combine(dir, "Radio.lua"));
        var comment = new Dictionary<int, string>(); var call = new Dictionary<int, string>();
        foreach (Match m in Regex.Matches(radio, @"radioId\s*=\s*'airfield(\d+)_0'[\s\S]*?callsign\s*=\s*\{\{\[""\w+""\]\s*=\s*\{_\(""([^""]+)""\)")) call[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
        foreach (Match m in Regex.Matches(radio, @"--[ \t]*([^\r\n]+?)[ \t]*\r?\n\s*radioId\s*=\s*'airfield(\d+)_0'")) comment[int.Parse(m.Groups[2].Value)] = m.Groups[1].Value;
        string Name(int id, string? beacon)                                 // Kommentar in Radio.lua, Funkfeuer, Rufname – der erste, der kein Kürzel in Großbuchstaben ist (OIBA, ADANA)
        {
            var c = new[] { comment.GetValueOrDefault(id), beacon == null ? null : Regex.Replace(beacon, "(?<=[a-z])(?=[A-Z])", " "), call.GetValueOrDefault(id) }
                .OfType<string>().Select(n => n.Replace('_', ' ').Trim()).Where(n => n.Length > 0).ToList();
            return c.FirstOrDefault(n => n != n.ToUpperInvariant()) ?? c.FirstOrDefault() ?? $"Airfield {id}";
        }
        var lines = new List<string>();
        var tac = new Dictionary<string, string>();
        var taxi = Taxiways(dir, radio);
        string Line(int id, string name, double x, double z, double elev, double h)
        {
            int r = (int)Math.Round(h / 10) % 36, o = (r + 18) % 36;
            return FormattableString.Invariant($"{id};{name.Replace(";", " ").Trim()};{x:0.0};{z:0.0};{Math.Max(0, elev):0};{h:0};{(r == 0 ? 36 : r):00}/{(o == 0 ? 36 : o):00}");
        }
        foreach (var g in bs.Where(b => b.Ab > 0).GroupBy(b => b.Ab).OrderBy(g => g.Key))
        {
            var on = g.Where(b => !b.Type.Contains("HOMER")).ToList();       // auf dem Platz: ILS, TACAN, VOR, RSBN; Funkfeuer der Anflüge liegen km draußen
            if (on.Count == 0) on = [.. g];
            var loc = g.FirstOrDefault(b => b.Type.Contains("LOCALIZER")) ?? g.FirstOrDefault(b => b.Type.Contains("GLIDESLOPE"));
            double h = loc != null ? (loc.Dir % 360 + 360) % 360 : taxi.TryGetValue(g.Key, out var tx) ? tx.Hdg : 0;
            var bn = g.Select(b => b.Name).Where(n => n.Length > 0).GroupBy(n => n).MaxBy(n => n.Count())?.Key;
            var name = Name(g.Key, bn);
            lines.Add(Line(g.Key, name, on.Average(b => b.X), on.Average(b => b.Z), on.Min(b => b.Y), h));
            if (g.FirstOrDefault(b => b.Type is "TACAN" or "VORTAC" && b.Ch > 0) is { } t) tac.TryAdd(name, $"{t.Ch}X");
        }
        foreach (var (id, t) in taxi.Where(t => t.Value.Span > 450 && !bs.Any(b => b.Ab == t.Key)).OrderBy(t => t.Key))   // Plätze ohne Funkfeuer (Hubschrauberplätze sind klein)
            lines.Add(Line(id, Name(id, null), t.X, t.Z, t.Y, t.Hdg));
        if (lines.Count < 2) return null;
        var entry = Path.Combine(dir, "entry.lua");
        var title = File.Exists(entry) && Regex.Match(File.ReadAllText(entry), @"\['localizedName'\]\s*=\s*""([^""]+)""") is { Success: true } ln ? ln.Groups[1].Value : map;
        return new MapDef(map, title, lon0, fe, fn, 0, string.Join('\n', lines), tac);
    }

    // ---------- erzeugte Startlage ----------

    const string Multi = "CAP,Escort,Sweep,Strike,SEAD,CAS,BAI,Interdiction,OCA";
    static readonly (Side S, string Type, string Name, int N, string Roles)[] Air =
    [
        (Side.Blue, "FA-18C_hornet", "VFA-87 Golden Warriors", 12, Multi), (Side.Blue, "F-16C_50", "480th FS Warhawks", 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "F-15C", "493rd FS Grim Reapers", 10, "CAP,Sweep,Escort"), (Side.Blue, "F-15ESE", "336th FS Rocketeers", 12, "Strike,Interdiction,OCA,BAI,CAS,Escort"),
        (Side.Blue, "F-14B", "VF-103 Jolly Rogers", 10, "CAP,Escort,Sweep,Strike"), (Side.Blue, "A-10C_2", "74th FS Flying Tigers", 10, "CAS,BAI,Interdiction"),
        (Side.Blue, "E-3A", "961st AACS", 2, "AWACS"), (Side.Blue, "KC135MPRS", "909th ARS (Basket)", 3, "Tanker"), (Side.Blue, "KC-135", "909th ARS (Boom)", 3, "Tanker"),
        (Side.Blue, "AH-64D_BLK_II", "1-227th ARB Apache", 8, "CAS,BAI"), (Side.Blue, "UH-1H", "Dustoff Company (CSAR)", 6, "CSAR"), (Side.Blue, "C-130J-30", "37th Airlift Squadron", 6, "Transport"),
        (Side.Red, "Su-27", "3rd Fighter Aviation Regiment", 12, "CAP,Sweep,Escort"), (Side.Red, "MiG-29S", "31st Fighter Aviation Regiment", 12, "CAP,Escort,Sweep"),
        (Side.Red, "Su-30", "3624th Air Base", 12, "Sweep,CAP,Strike,SEAD"), (Side.Red, "Su-24M", "559th Bomber Regiment", 10, "Strike,Interdiction,OCA,SEAD"),
        (Side.Red, "Su-25", "368th Assault Aviation Regiment", 12, "CAS,BAI"), (Side.Red, "Su-34", "47th Bomber Regiment", 12, "Strike,Interdiction,OCA,BAI"),
        (Side.Red, "A-50", "A-50 Squadron", 2, "AWACS"), (Side.Red, "Mi-24P", "55th Helicopter Regiment", 8, "CAS,BAI"), (Side.Red, "Mi-8MT", "Rescue Squadron (CSAR)", 4, "CSAR"),
        (Side.Red, "IL-76MD", "235th Transport Aviation Regiment", 4, "Transport"),
    ];
    static Setup Light(string map) => new()
    {
        Id = "auto-" + map, Map = map, Name = $"{Def(map)?.Name ?? map}: Generated War", BlueText = "Blue – NATO", RedText = "Red – Russia",
        Text = "Generated from the DCS map: front line through the middle of the airfields, Red attacks on up to three axes. Airfields, towns and targets come from the map itself.",
        Sqns = [.. Air.OrderBy(a => a.S != Side.Red).Select(a => (a.S, a.S == Side.Red ? "Russia" : "USA", a.Type, a.Name, 0, a.N, a.Roles))],
    };

    /// Für die Auswahl (ohne die Karte zu laden)
    public static IEnumerable<Setup> List() => Maps().Select(Light);

    /// Vollständige Startlage zu "auto-Karte" (lädt die Karte), sonst null
    public static Setup? Get(string id) => id.StartsWith("auto-") ? For(Theater.Get(id[5..])) : null;

    /// Startlage für eine geladene Karte erzeugen (einmal je Karte, immer gleich: hängt nur an Plätzen aus der Installation, Städten und Höhen)
    public static Setup For(Theater th)
    {
        lock (Sets) if (Sets.TryGetValue(th.Map.Id, out var have)) return have;
        Geo.Use(th.Map);
        var defs = th.Map.Bases.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(';')).Select(f => (X: N(f[2]), Z: N(f[3]))).ToList();
        var unique = th.Towns.GroupBy(t => t.Name).Where(g => g.Count() == 1).Select(g => g.Key).ToHashSet();   // doppelte Ortsnamen taugen nicht als Ort
        var land = th.Towns.Where(t => unique.Contains(t.Name) && !th.Water(t.X, t.Z) && !th.Clipped(t.X, t.Z)).ToList();
        if (defs.Count < 2 || land.Count < 10) throw new ArgumentException($"{th.Map.Name}: not enough airfields or towns for a generated war");

        // Front: gerade Linie zwischen den Plätzen (jede Seite 35–65 % davon), Richtung und Lage so, dass sie möglichst viele Orte an Land schneidet
        // (Inseln wie Zypern oder ein Meer dazwischen ziehen sie sonst ins Wasser). Rot im Norden (bzw. Osten).
        double cx = defs.Average(p => p.X), cz = defs.Average(p => p.Z);
        var (score, ux, uz, split) = (-1, 1.0, 0.0, 0.0);
        for (int a = 0; a < 180; a += 15)
        {
            double ax = Math.Cos(a * Math.PI / 180), az = Math.Sin(a * Math.PI / 180);
            var ts = defs.Select(p => (p.X - cx) * ax + (p.Z - cz) * az).Order().ToList();
            double lm = defs.Average(p => -(p.X - cx) * az + (p.Z - cz) * ax);
            foreach (var q in new[] { 0.35, 0.42, 0.5, 0.58, 0.65 })
            {
                int k = (int)Math.Round(q * (ts.Count - 1));
                double sp = (ts[k] + ts[Math.Min(k + 1, ts.Count - 1)]) / 2;
                int s = land.Count(t => Math.Abs((t.X - cx) * ax + (t.Z - cz) * az - sp) < 15000 && Math.Abs(-(t.X - cx) * az + (t.Z - cz) * ax - lm) < 220000);
                if (s > score) (score, ux, uz, split) = (s, ax, az, sp);
            }
        }
        if (ux < -0.3 || Math.Abs(ux) <= 0.3 && uz < 0) (ux, uz, split) = (-ux, -uz, -split);
        double T(double x, double z) => (x - cx) * ux + (z - cz) * uz;     // Tiefe: + Rot, - Blau
        double L(double x, double z) => -(x - cx) * uz + (z - cz) * ux;    // quer zur Front
        double D(double x, double z) => T(x, z) - split;
        Side SideAt(double x, double z) => D(x, z) > 0 ? Side.Red : Side.Blue;
        P At(double d, double l) => new(cx + ux * (split + d) - uz * l, cz + uz * (split + d) + ux * l);
        var band = land.Where(t => Math.Abs(D(t.X, t.Z)) < 140000).Select(t => L(t.X, t.Z)).Order().ToList();
        double lc = defs.Average(p => L(p.X, p.Z));
        double l0 = Math.Max(band.Count > 0 ? band[band.Count / 20] : lc - 100000, lc - 220000), l1 = Math.Min(band.Count > 0 ? band[band.Count * 19 / 20] : lc + 100000, lc + 220000);
        bool InWar(double x, double z) => Math.Abs(D(x, z)) < 140000 && L(x, z) > l0 - 20000 && L(x, z) < l1 + 20000;
        Town? Near(P p, double r, HashSet<string> used) => land.Where(t => !used.Contains(t.Name) && InWar(t.X, t.Z) && Geo.Dist(t.X, t.Z, p.X, p.Z) < r)
            .MinBy(t => Geo.Dist(t.X, t.Z, p.X, p.Z) - t.Light * 20);

        // Achsen: Orte im Abstand ~20 km von hinten Rot nach hinten Blau
        var used = new HashSet<string>();
        var cands = new List<(double La, List<Town> Keys)>();
        for (double la = l0; la <= l1; la += 20000)                         // Kandidaten quer zur Front, die längsten Ketten gewinnen
        {
            var seen = new HashSet<string>();
            var keys = new List<Town>();
            for (double d = 90000; d >= -90000; d -= 20000)
                if (Near(At(d, la), 18000, seen) is { } t) { keys.Add(t); seen.Add(t.Name); }
            keys = [.. keys.OrderByDescending(k => D(k.X, k.Z))];
            if (keys.Count(k => SideAt(k.X, k.Z) == Side.Red) >= 2 && keys.Count(k => SideAt(k.X, k.Z) == Side.Blue) >= 2) cands.Add((la, keys));
        }
        var pick = new List<(double La, List<Town> Keys)>();
        foreach (var c in cands.OrderByDescending(c => c.Keys.Count).ThenBy(c => Math.Abs(c.La - lc)))
            if (pick.Count < 3 && pick.All(p => Math.Abs(p.La - c.La) >= 70000) && c.Keys.All(k => !used.Contains(k.Name))) { pick.Add(c); foreach (var k in c.Keys) used.Add(k.Name); }
        var axes = new List<(string Name, Side Push, string[] Keys)>();
        foreach (var (_, keys) in pick.OrderBy(p => p.La))
            axes.Add(($"Axis {axes.Count + 1} ({keys.First(k => SideAt(k.X, k.Z) == Side.Blue).Name})", Side.Red, [.. keys.Select(k => k.Name)]));

        // Verbände: je Achse vorn Mech/Panzer/Flugabwehr, dahinter Artillerie und Reserve
        var oob = new List<(Side S, UK K, int Ax, string Role, string At, string Tpl, string Name)>();
        var redRes = new List<string>(); var blueRes = new List<string>();
        string Ord(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
        for (int i = 0; i < axes.Count; i++)
        {
            var k = axes[i].Keys;
            var sides = k.Select(n => land.First(t => t.Name == n)).Select(t => SideAt(t.X, t.Z)).ToList();
            int rf = sides.LastIndexOf(Side.Red), bf = sides.IndexOf(Side.Blue);
            string rb = k[Math.Max(0, rf - 1)], rr = k[Math.Max(0, rf - 2)], bb = k[Math.Min(k.Length - 1, bf + 1)], br = k[Math.Min(k.Length - 1, bf + 2)];
            redRes.Add(rr); blueRes.Add(br);
            int div = 19 + i * 23;
            oob.AddRange([
                (Side.Red, UK.Mech, i, "front", k[rf], "RU-Mech", $"{Ord(div)} Motor Rifle Div {Ord(div * 10 + 3)} Rgt"),
                (Side.Red, UK.Armor, i, "front", k[rf], "RU-Armor", $"{Ord(div)} Motor Rifle Div Tank Bn"),
                (Side.Red, UK.Shorad, i, "front", k[rf], "RU-Shorad", $"{Ord(div)} Div Air Defense Rgt"),
                (Side.Red, UK.Art, i, "front", rb, "RU-Art", $"{Ord(div)} Div Artillery Rgt"),
                (Side.Red, UK.Mech, i, "reserve", rr, "RU-Mech", $"{Ord(div)} Div {Ord(div * 10 + 4)} Rgt"),
                (Side.Blue, UK.Mech, i, "front", k[bf], "US-Mech", $"{Ord(i + 1)} Bde Combat Team 1st Bn"),
                (Side.Blue, UK.Armor, i, "front", k[bf], "US-Armor", $"{Ord(i + 1)}/{Ord(64 + i)} Armor (US) Tank Bn"),
                (Side.Blue, UK.Shorad, i, "front", k[bf], "US-Shorad", $"{Ord(i + 1)} Bde Air Defense"),
                (Side.Blue, UK.Art, i, "front", bb, "US-Art", $"{Ord(i + 1)}/{Ord(41 + i)} FA (US) Artillery Bn"),
                (Side.Blue, UK.Motor, i, "reserve", br, "US-Motor", $"{Ord(i + 1)}/{Ord(2 + i)} SCR (US) Stryker Bn"),
            ]);
        }

        // Ziele je Seite: Depots, Werke, Stäbe, Radar, Häfen, Flugabwehr an den größten Orten
        var objs = new List<(OK K, Side S, string At, string Name, double Value, string Sys)>();
        var taken = new HashSet<string>();
        void Obj(OK k, Side s, string at, string name, double v, string sys = "") { if (taken.Add(name)) objs.Add((k, s, at, name, v, sys)); }
        bool Coast(Town t) => new[] { (3000.0, 0.0), (-3000.0, 0.0), (0.0, 3000.0), (0.0, -3000.0) }.Any(o => th.Water(t.X + o.Item1, t.Z + o.Item2));
        var decoys = new List<(Side S, string At, string Name, string Sys)>();
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            bool red = s == Side.Red;
            var mine = land.Where(t => SideAt(t.X, t.Z) == s).OrderByDescending(t => t.Light).ToList();
            double Depth(Town t) => Math.Abs(D(t.X, t.Z));
            var res = red ? redRes : blueRes;
            var army = mine.FirstOrDefault(t => InWar(t.X, t.Z) && Depth(t) is > 50000 and < 135000) ?? mine.First();
            var hq = mine.FirstOrDefault(t => Depth(t) > 80000 && t != army) ?? army;
            var plants = mine.Where(t => Depth(t) > 60000 && t != army && t != hq).Take(2).ToList();
            var radars = mine.Where(t => Depth(t) is > 35000 and < 110000 && t != army).Take(2).ToList();
            var port = mine.FirstOrDefault(Coast);
            Obj(OK.Depot, s, army.Name, $"Army Depot {army.Name}", 4);
            foreach (var r in res.Distinct()) Obj(OK.Depot, s, r, $"Forward Depot {r}", 3);
            for (int i = 0; i < plants.Count; i++) Obj(OK.Factory, s, plants[i].Name, i == 0 ? $"Arms Plant {plants[i].Name}" : $"Refinery {plants[i].Name}", i == 0 ? 4 : 3);
            Obj(OK.Cp, s, hq.Name, red ? $"HQ Southern Front {hq.Name}" : $"General Staff {hq.Name}", 5);
            Obj(OK.Cp, s, army.Name, $"Command Post {army.Name}", 4);
            if (port != null) Obj(OK.Port, s, port.Name, $"Port {port.Name}", 4);
            foreach (var r in radars) Obj(OK.Ewr, s, r.Name, $"Radar {r.Name}", 3, red ? "EWR-RU" : "EWR-BL");
            Obj(OK.Sam, s, army.Name, red ? $"SA-10 {army.Name}" : $"Patriot {army.Name}", 5, red ? "SA-10" : "Patriot");
            Obj(OK.Sam, s, hq.Name, red ? $"SA-15 {hq.Name}" : $"Patriot {hq.Name}", red ? 2 : 5, red ? "SA-15" : "Patriot");
            foreach (var r in res.Distinct().Take(2)) Obj(OK.Sam, s, r, red ? $"SA-11 {r}" : $"Hawk {r}", red ? 4 : 3, red ? "SA-11" : "Hawk");
            foreach (var r in res.Distinct().Skip(2).Take(1).Concat(radars.Take(1).Select(t => t.Name))) Obj(OK.Sam, s, r, red ? $"SA-6 {r}" : $"NASAMS {r}", 3, red ? "SA-6" : "NASAMS");
            if (axes.Count > 0)
            {
                var k = axes[0].Keys; var sides = k.Select(n => SideAt(land.First(t => t.Name == n).X, land.First(t => t.Name == n).Z)).ToList();
                var at = red ? k[Math.Max(0, sides.LastIndexOf(Side.Red) - 1)] : k[Math.Min(k.Length - 1, sides.IndexOf(Side.Blue) + 1)];
                if (!taken.Contains(red ? $"SA-6 {at}" : $"Hawk {at}")) decoys.Add((s, at, red ? $"SA-6 {at}" : $"Hawk {at}", red ? "SA-6" : "Hawk"));
            }
        }

        // Nachschub: Straße von hinten (Anfang/Ende der mittleren Achse), Frachter von See zu den Häfen
        var gates = new List<(Side S, string At, double Mun, double Pol)>();
        var reinf = new List<(int H, Side S, UK K, string At, string Tpl, string Name)>();
        if (axes.Count > 0)
        {
            var mid = axes[axes.Count / 2].Keys;
            gates.Add((Side.Red, mid[0], 250, 350)); gates.Add((Side.Blue, mid[^1], 300, 450));
            reinf.AddRange([(24, Side.Red, UK.Armor, mid[0], "RU-Armor", "1st Guards Tank Army 1st Bn"), (48, Side.Red, UK.Mech, mid[0], "RU-Mech", "76th Airborne Div 104th Rgt"),
                (72, Side.Red, UK.Art, mid[0], "RU-Art", "1st Guards Tank Army Artillery Bn"), (30, Side.Blue, UK.Mech, mid[^1], "US-Mech", "2/7 Inf (US) Bradley Bn"),
                (54, Side.Blue, UK.Armor, mid[^1], "US-Armor", "3/69 Armor (US) Tank Bn"), (96, Side.Blue, UK.Art, mid[^1], "US-Art", "1/41 FA (US) Artillery Bn")]);
        }
        var sea = new List<(Side S, double X, double Z, string From)>();
        foreach (var (k, s, at, _, _, _) in objs.Where(o => o.K == OK.Port))
        {
            var t = land.First(x => x.Name == at);
            var p = Enumerable.Range(0, 24).SelectMany(i => new[] { 60000.0, 100000, 150000 }.Select(d => Geo.Move(new P(t.X, t.Z), i * 15, d)))
                .Where(p => SideAt(p.X, p.Z) == s && th.Water(p.X, p.Z) && Scenario.Lane(th, p.X, p.Z, t.X, t.Z)).MaxBy(p => Math.Abs(D(p.X, p.Z)));
            if (p != null) sea.Add((s, Math.Round(p.X), Math.Round(p.Z), ""));
        }

        // Plätze: Seite nach Lage; Staffeln vorn (Erdkampf, Hubschrauber), hinten (AWACS, Tanker, Transport), sonst reihum
        var bases = th.Bases.Where(b => b.Id > 0).ToList();
        var sqns = new List<(Side S, string Country, string Type, string Name, int Base, int N, string Roles)>();
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            var mine = bases.Where(b => SideAt(b.X, b.Z) == s).OrderBy(b => Math.Abs(D(b.X, b.Z))).ToList();
            var back = mine.Where(b => Math.Abs(D(b.X, b.Z)) > 25000).ToList() is { Count: > 0 } far ? far : mine;
            int n = 0;
            foreach (var a in Air.Where(a => a.S == s))
            {
                var b = a.Roles is "AWACS" or "Tanker" or "Transport" ? back[^1] : global::DcsCampaign.Air.Helo(a.Type) || a.Roles.StartsWith("CAS") ? back[0] : back[n++ % back.Count];
                sqns.Add((s, s == Side.Red ? "Russia" : "USA", a.Type, a.Name, b.Id, a.N, a.Roles));
            }
        }
        Base Bull(Side s) { var m = bases.Where(b => SideAt(b.X, b.Z) == s).ToList(); double mx = m.Average(b => b.X), mz = m.Average(b => b.Z); return m.MinBy(b => Geo.Dist(b.X, b.Z, mx, mz))!; }
        P Pt(double d, double l) => At(d, l);
        var (la0, _) = Geo.ToLL(cx, cz);
        var set = Light(th.Map.Id) with
        {
            Start = $"War on {th.Map.Name}: Russian forces attack across the front. NATO defends.",
            RedC = "Russia", BlueC = "USA", BlueSite = "USA", RedIds = [0], BlueIds = [2],
            BullBlue = new(Bull(Side.Blue).X, Bull(Side.Blue).Z), BullRed = new(Bull(Side.Red).X, Bull(Side.Red).Z),
            Owner = (lat, lon) => Geo.ToXZ(lat, lon) is var (x, z) ? SideAt(x, z) : Side.None, InWar = InWar,
            BlueBases = [.. bases.Where(b => SideAt(b.X, b.Z) == Side.Blue).Select(b => b.Id)],
            AirReinf = [(36, Side.Blue, "USA", "F-15C", "44th FS Vampire Bats", sqns.First(q => q.S == Side.Blue).Base, 10, "CAP,Sweep,Escort"),
                        (48, Side.Red, "Russia", "Su-34", "277th Bomber Regiment", sqns.First(q => q.S == Side.Red).Base, 12, "Strike,Interdiction,OCA,BAI")],
            Axes = [.. axes], Oob = [.. oob], Objs = [.. objs], Decoys = [.. decoys], Gates = [.. gates], Reinf = [.. reinf], Sea = [.. sea], Sqns = [.. sqns],
            Support = [(Side.Blue, "Magic", Pt(-120000, lc).X, Pt(-120000, lc).Z, 9000, "AWACS"), (Side.Blue, "Texaco", Pt(-90000, lc - 80000).X, Pt(-90000, lc - 80000).Z, 7000, "Tanker"),
                       (Side.Blue, "Shell", Pt(-90000, lc + 80000).X, Pt(-90000, lc + 80000).Z, 7600, "Tanker"), (Side.Red, "A-50", Pt(130000, lc).X, Pt(130000, lc).Z, 9000, "AWACS")],
            Caps = axes.Count >= 2 ? [("Front A", 0, true, Side.None, 0, 0), ("Front B", axes.Count - 1, false, Side.None, 0, 0)]
                 : axes.Count == 1 ? [("Front", 0, true, Side.None, 0, 0)]
                 : [("Front", -1, true, Side.Blue, Pt(-50000, lc).X, Pt(-50000, lc).Z), ("Front", -1, true, Side.Red, Pt(50000, lc).X, Pt(50000, lc).Z)],
            RedNames = Scenario.RedNames, BlueNames = Scenario.UsNames, BlueFirst = Scenario.UsFirst, HostNames = Scenario.UsNames,
            Temp = Math.Round(Math.Clamp(40 - 0.6 * la0, -10, 32)),
        };
        lock (Sets) Sets[th.Map.Id] = set;
        return set;
    }
}
