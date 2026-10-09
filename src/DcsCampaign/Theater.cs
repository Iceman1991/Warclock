using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DcsCampaign;

public record Town(string Name, double X, double Z, double Light);
public record Base(int Id, string Name, double X, double Z, double Elev, double RwyHdg, string Rwy);
public record Payload(string Name, int[] Tasks, List<(int Num, string Clsid)> Pylons);
/// Karte: Projektion (Mittelmeridian, Versatz), höchster Gipfel (Höhenraster ist abgeschnitten), Flugplätze, TACAN, Ortsfilter (x0, x1, z0, z1).
/// Land: (Lat, Lon) sicher Land, auch wo das Höhenraster 0 m zeigt (flache Küsten).
public record MapDef(string Id, string Name, double Lon0, double FE, double FN, double Top, string Bases, Dictionary<string, string> Tacans, double[]? TownBox = null, Func<double, double, bool>? Land = null);

/// Alles, was aus der DCS-Installation kommt: Städte, Höhen, Flugplätze, Loadouts. Nur lesen, nichts weitergeben.
public sealed class Theater
{
    public string Dcs = "";
    public MapDef Map = Maps["Caucasus"];
    public List<Town> Towns = [];
    public List<Base> Bases = [];
    public Dictionary<int, List<(double X, double Z, int Num, int Type)>> Parking = [];
    public Dictionary<string, List<Payload>> Payloads = [];
    public Dictionary<int, double> Uhf = [];
    // Höhenraster aus extra\lightmap (512 m, Alpha = Höhe 0..HMax, darüber abgeschnitten); Werte aus lightmap.lua
    public double HX0 = 260096, HZ0 = -380928, HCell = 512, HMax = 2693.878906;
    public int HW, HH;
    public float[] H = [];
    public float[] Light = [];
    public byte[] Relief = [];          // PNG für die Weboberfläche
    public byte[] Satellite = [];       // nodesMap.png
    public (double X0, double Z0, double X1, double Z1) SatBox = (-418619.1875, 113728.15625, 26382.5, 943187.0625);

    public static string FindDcs()
    {
        foreach (var k in new[] { @"Software\Eagle Dynamics\DCS World", @"Software\Eagle Dynamics\DCS World OpenBeta" })
            if (Microsoft.Win32.Registry.CurrentUser.OpenSubKey(k)?.GetValue("Path") is string p && Directory.Exists(p)) return p;
        return @"C:\Program Files\Eagle Dynamics\DCS World";
    }

    public static readonly Dictionary<string, MapDef> Maps = new()
    {
        ["Caucasus"] = new("Caucasus", "Caucasus", 33, -99516.9999999732, -4998114.999999984, 5650, CaucasusBases,
            new() { ["Batumi"] = "16X", ["Kobuleti"] = "67X", ["Senaki-Kolkhi"] = "31X", ["Kutaisi"] = "44X", ["Vaziani"] = "22X" }, [-470000, 60000, 100000, 960000]),
        ["PersianGulf"] = new("PersianGulf", "Persian Gulf", 57, 75755.99999999645, -2894933.0000000377, 4400, GulfBases,
            new() { ["Al Minhad AFB"] = "99X", ["Bandar Abbas Intl"] = "78X", ["Havadarya"] = "47X", ["Kish Intl"] = "112X", ["Kerman"] = "97X", ["Shiraz Intl"] = "94X", ["Bandar-e-Jask"] = "110X", ["Tunb Kochak"] = "89X" }, Land: Gulf.Land),
    };
    static readonly Dictionary<string, Theater> Cache = [];
    /// Karte laden (einmal je Prozess) und ihre Projektion einschalten.
    public static Theater Get(string map) { Theater t; lock (Cache) t = Cache.TryGetValue(map, out var c) ? c : Load(null, map); Geo.Use(t.Map); return t; }
    public static bool Supercarrier => Directory.Exists(Path.Combine(FindDcs(), @"Mods\tech\Supercarrier"));
    public static bool Installed(string map) => Directory.Exists(Path.Combine(FindDcs(), @"Mods\terrains", map));
    /// Handgepflegte Karte oder aus der Installation gerechnet (Auto)
    public static MapDef Def(string map) => Maps.TryGetValue(map, out var m) ? m : Auto.Def(map) ?? throw new ArgumentException($"Map {map} is not installed or not readable");

    public static Theater Load(string? dcs = null, string map = "Caucasus")
    {
        var m = Def(map);
        Geo.Use(m);
        var t = new Theater { Dcs = dcs ?? FindDcs(), Map = m };
        var terr = Path.Combine(t.Dcs, @"Mods\terrains", map);
        var box = Nums(Regex.Match(File.ReadAllText(Path.Combine(terr, @"extra\lightmap\lightmap.lua")), @"box\s*=\s*\{\s*min\s*=\s*\{([^}]*)\}\s*,\s*max\s*=\s*\{([^}]*)\}").Value);
        (t.HX0, t.HZ0, t.HMax) = (box[3], box[2], box[4]);                  // Zeile 0 = Nordrand (max x), Spalte 0 = Westrand (min z)
        if (m.Top <= 0) t.Map = m = m with { Top = Math.Max(3000, t.HMax * 1.6) };   // Auto-Karte: höchster Gipfel unbekannt
        t.LoadHeights(Path.Combine(terr, @"extra\lightmap\lightmap.png"));
        var tb = m.TownBox ?? [box[0], box[3], box[2], box[5]];
        var towns = Lua.Get(Lua.Parse(File.ReadAllText(Path.Combine(terr, @"Map\towns.lua"))), "towns") as Dictionary<string, object?> ?? [];
        foreach (var (name, v) in towns)
        {
            var (x, z) = Geo.ToXZ(Lua.Num(v, "latitude"), Lua.Num(v, "longitude"));
            if (x < tb[0] || x > tb[1] || z < tb[2] || z > tb[3]) continue;
            t.Towns.Add(new Town(Nice(Lua.Str(v, "display_name") is { Length: > 0 } dn ? dn : name), x, z, t.LightAt(x, z)));
        }
        foreach (var l in m.Bases.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = l.Split(';');
            t.Bases.Add(new Base(int.Parse(f[0]), f[1], D(f[2]), D(f[3]), D(f[4]), D(f[5]), f[6].Trim()));
        }
        // Parkplätze (und genaue Platzpositionen): selbst aus einer Kampagnenmission mitgeschrieben, sonst aus der DCS-ATC, falls dieselbe Karte
        _ = t.ReadAirfields(AirfieldFile(map)) || t.LearnAirfields();
        foreach (var ty in Air.Perfs.Keys) t.Payloads[ty] = t.LoadPayloads(ty);
        var radio = Path.Combine(terr, "Radio.lua");   // Platzfrequenzen wie in der DCS-ATC: UHF, sonst VHF (Golf)
        if (File.Exists(radio))
            foreach (var block in File.ReadAllText(radio).Split("radioId").Skip(1))
                if (Regex.Match(block, @"^\s*=\s*'airfield(\d+)_0'") is { Success: true } rid
                    && new[] { "UHF", "VHF_HI" }.Select(b => Regex.Match(block, $@"\[{b}\]\s*=\s*\{{\s*MODULATIONTYPE_AM,\s*([\d.]+)")).FirstOrDefault(x => x.Success) is { } mf)
                    t.Uhf[int.Parse(rid.Groups[1].Value)] = Math.Round(D(mf.Groups[1].Value) / 1e6, 3);
        var sat = Path.Combine(terr, @"MissionGenerator\nodesMap.png");
        if (File.Exists(sat)) t.Satellite = File.ReadAllBytes(sat);
        var sb = File.Exists(Path.ChangeExtension(sat, ".lua")) ? Nums(File.ReadAllText(Path.ChangeExtension(sat, ".lua"))) : [];
        if (sb.Length >= 4) t.SatBox = (sb[0], sb[1], sb[2], sb[3]);         // minX, minZ, maxX, maxZ
        t.Relief = t.MakeRelief();
        lock (Cache) Cache[map] = t;
        return t;
    }

    public static string AirfieldFile(string map) => Path.Combine(Program.Dir, $"Airfields-{map}.txt");

    /// Plätze aus der DCS-ATC übernehmen (sie schreibt sie in jeder Mission für die laufende Karte) und für später aufheben.
    public bool LearnAirfields()
    {
        var atc = Path.Combine(Path.GetTempPath(), "DcsAtc-Airfields.txt");
        if (!ReadAirfields(atc)) return false;
        try { Directory.CreateDirectory(Program.Dir); File.Copy(atc, AirfieldFile(Map.Id), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return true;
    }
    static double[] Nums(string s) => Regex.Matches(s, @"-?\d+\.\d+").Select(x => D(x.Value)).ToArray();

    /// Flugplätze im Format der DCS-ATC (M;Karte / A;Name;x;z;Höhe;Id;Seite / P;x;z;Nummer;Typ). Nur, wenn die Karte passt (alte Dateien ohne M: Kaukasus).
    public bool ReadAirfields(string file)
    {
        if (!File.Exists(file)) return false;
        var lines = File.ReadAllLines(file);
        if ((lines.FirstOrDefault(l => l.StartsWith("M;"))?[2..].Trim() ?? "Caucasus") != Map.Id) return false;
        Parking.Clear();
        int id = 0;
        foreach (var l in lines)
        {
            var f = l.Split(';');
            if (f[0] == "A" && f.Length > 5)
            {
                id = (int)D(f[5]);
                int i = Bases.FindIndex(b => b.Id == id);
                if (i >= 0) Bases[i] = Bases[i] with { X = D(f[2]), Z = D(f[3]), Elev = D(f[4]), Name = Maps.ContainsKey(Map.Id) ? Bases[i].Name : f[1] };
                else if (!Maps.ContainsKey(Map.Id)) Bases.Add(new Base(id, f[1], D(f[2]), D(f[3]), D(f[4]), 0, ""));   // Auto-Karte: auch Plätze ohne Funkfeuer, echte Namen
            }
            else if (f[0] == "R" && f.Length >= 4 && id > 0 && Bases.FindIndex(b => b.Id == id) is var k and >= 0 && Bases[k].RwyHdg == 0)   // Pistenrichtung (rad) für Plätze ohne ILS
            {
                double h = Math.Round((D(f[2]) * 180 / Math.PI % 360 + 360) % 360);
                int r = (int)Math.Round(h / 10) % 36, o = (r + 18) % 36;
                Bases[k] = Bases[k] with { RwyHdg = h, Rwy = $"{(r == 0 ? 36 : r):00}/{(o == 0 ? 36 : o):00}" };
            }
            else if (f[0] == "P" && f.Length >= 5 && id > 0)
                (Parking.TryGetValue(id, out var ps) ? ps : Parking[id] = []).Add((D(f[1]), D(f[2]), (int)D(f[3]), (int)D(f[4])));
        }
        return true;
    }

    static double D(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    static string Nice(string s) => string.Join(' ', s.Split(' ', '_').Where(w => w.Length > 0).Select(w => w.Length < 2 ? w : char.ToUpper(w[0]) + w[1..].ToLowerInvariant()));

    /// Beladungen aus DCS, dazu eigene aus dem Missionseditor (Saved Games\DCS\MissionEditor\UnitPayloads).
    List<Payload> LoadPayloads(string type)
    {
        var file = new[] { Path.Combine(Dcs, @"MissionEditor\data\scripts\UnitPayloads", type + ".lua") }
            .Concat(new[] { "CoreMods", "Mods" }.Select(m => Path.Combine(Dcs, m, "aircraft")).Where(Directory.Exists).SelectMany(Directory.EnumerateDirectories).Select(d => Path.Combine(d, "UnitPayloads", type + ".lua")))
            .FirstOrDefault(File.Exists);
        var list = new List<Payload>();
        foreach (var f in new[] { file, Path.Combine(Program.SavedGames, @"MissionEditor\UnitPayloads", type + ".lua") })
        {
            if (f == null || !File.Exists(f)) continue;
            var root = Lua.Parse(File.ReadAllText(f));
            foreach (var p in Lua.Arr<Dictionary<string, object?>>(root.Values.Select(v => Lua.Get(v, "payloads")).FirstOrDefault(v => v != null)))
                if (list.All(o => o.Name != Lua.Str(p, "name")))
                    list.Add(new Payload(Lua.Str(p, "name"),
                        Lua.Arr<object>(Lua.Get(p, "tasks")).OfType<double>().Select(d => (int)d).ToArray(),
                        Lua.Arr<Dictionary<string, object?>>(Lua.Get(p, "pylons")).Select(y => ((int)Lua.Num(y, "num"), Lua.Str(y, "CLSID"))).ToList()));
        }
        return list;
    }

    /// Was je Station geht: alles, was eine Beladung des Musters dort trägt.
    // ponytail: Stationsauswahl nur aus den Beladungen (die Musterdateien sind teils verschlüsselt); eigene Beladungen im DCS-Missionseditor erweitern sie.
    public SortedDictionary<int, List<string>> Stations(string type)
    {
        var r = new SortedDictionary<int, List<string>>();
        foreach (var (num, id) in Payloads.GetValueOrDefault(type)?.SelectMany(p => p.Pylons) ?? [])
            if (num > 0 && Wpn(id).N > 0 && !(r.TryGetValue(num, out var l) ? l : r[num] = []).Any(x => Wpn(x) == Wpn(id))) r[num].Add(id);   // gleicher Name = gleiche Waffe (Mk-83 mit/ohne BRU-32)
        foreach (var l in r.Values) l.Sort((a, b) => string.CompareOrdinal(Wpn(a).Name, Wpn(b).Name));
        return r;
    }

    static readonly Dictionary<string, (string Name, int N)> Weapons = ReadWeapons();
    static Dictionary<string, (string, int)> ReadWeapons()
    {
        using var s = new StreamReader(typeof(Theater).Assembly.GetManifestResourceStream("weapons.txt")!);
        return s.ReadToEnd().Split('\n').Where(l => l.Length > 0 && l[0] != '#').Select(l => l.TrimEnd('\r').Split(';'))
            .ToDictionary(f => f[0], f => (f[1], int.Parse(f[2])));
    }
    /// Name und Anzahl einer Waffe (CLSID); Unbekanntes lesbar gemacht.
    public static (string Name, int N) Wpn(string id) => Weapons.TryGetValue(id, out var w) ? w : (id.Trim('{', '}').Replace('_', ' '), 1);
    /// Beladungsname aus der Bestückung, wie die DCS-Namen ("AIM-9X*2, GBU-31(V)4/B*2, ATFLIR") – daraus rechnet die Kampagne Raketen und Wirkung.
    public static string LoadName(IEnumerable<string> ids) => string.Join(", ", ids.Select(Wpn).Where(w => w.N > 0).GroupBy(w => w.Name)
        .Select(g => (g.Key, N: g.Sum(w => w.N))).OrderByDescending(g => g.Key.StartsWith("AIM") || g.Key.StartsWith("R-")).Select(g => g.N > 1 ? $"{g.Key}*{g.N}" : g.Key));

    void LoadHeights(string png)
    {
        using var bmp = new Bitmap(png);
        HW = bmp.Width; HH = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, HW, HH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var raw = new byte[data.Stride * HH];
        Marshal.Copy(data.Scan0, raw, 0, raw.Length);
        bmp.UnlockBits(data);
        H = new float[HW * HH];
        Light = new float[HW * HH];
        for (int r = 0; r < HH; r++)
            for (int c = 0; c < HW; c++)
            {
                int o = r * data.Stride + c * 4;
                H[r * HW + c] = (float)(raw[o + 3] / 255.0 * HMax);
                Light[r * HW + c] = raw[o] + raw[o + 1] + raw[o + 2];
            }
    }

    (double R, double C) Cell(double x, double z) => ((HX0 - x) / HCell, (z - HZ0) / HCell);

    /// Geländehöhe in m (bilinear). Gipfel über 2694 m sind abgeschnitten -> Clipped().
    public double Height(double x, double z)
    {
        var (r, c) = Cell(x, z);
        int r0 = (int)Math.Floor(r), c0 = (int)Math.Floor(c);
        if (r0 < 0 || c0 < 0 || r0 >= HH - 1 || c0 >= HW - 1) return 0;
        double fr = r - r0, fc = c - c0;
        double h(int rr, int cc) => H[rr * HW + cc];
        return (h(r0, c0) * (1 - fc) + h(r0, c0 + 1) * fc) * (1 - fr) + (h(r0 + 1, c0) * (1 - fc) + h(r0 + 1, c0 + 1) * fc) * fr;
    }
    public bool Clipped(double x, double z) => Height(x, z) > HMax - 30;
    /// Sichere Höhe (m) für Gelände um den Punkt; abgeschnittene Gipfel = höchster Gipfel der Karte (Kaukasus: Elbrus).
    public double Peak(double x, double z, double radius)
    {
        double m = 0;
        for (double dx = -radius; dx <= radius; dx += HCell)
            for (double dz = -radius; dz <= radius; dz += HCell)
                m = Math.Max(m, Clipped(x + dx, z + dz) ? Map.Top : Height(x + dx, z + dz));
        return m;
    }
    bool LandAt(double x, double z) => Map.Land is { } l && Geo.ToLL(x, z) is var (la, lo) && l(la, lo);
    public bool Sea(double x, double z) => Height(x, z) < 1 && !LandAt(x, z);
    public bool Water(double x, double z) => Sea(x, z) && Height(x + 2000, z) < 1 && Height(x - 2000, z) < 1 && Height(x, z + 2000) < 1 && Height(x, z - 2000) < 1;

    double LightAt(double x, double z)
    {
        var (r, c) = Cell(x, z);
        double s = 0;
        for (int dr = -2; dr <= 2; dr++)
            for (int dc = -2; dc <= 2; dc++)
            {
                int rr = (int)r + dr, cc = (int)c + dc;
                if (rr >= 0 && cc >= 0 && rr < HH && cc < HW) s += Light[rr * HW + cc];
            }
        return s;
    }

    /// Schummerung + Höhenfarben + Meer als PNG (Hintergrund der Karte). Pixel = 512 m, Zeile 0 = Norden.
    byte[] MakeRelief()
    {
        using var bmp = new Bitmap(HW, HH, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, HW, HH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var px = new byte[data.Stride * HH];
        for (int r = 0; r < HH; r++)
            for (int c = 0; c < HW; c++)
            {
                double h = H[r * HW + c];
                double hw = c > 0 ? H[r * HW + c - 1] : h, hn = r > 0 ? H[(r - 1) * HW + c] : h;
                double shade = Math.Clamp(1 + ((h - hw) + (h - hn)) / 140.0, 0.45, 1.35);
                bool sea = h < 0.5 && !LandAt(HX0 - (r + 0.5) * HCell, HZ0 + (c + 0.5) * HCell);
                (double R, double G, double B) col = sea ? (38, 72, 104)
                    : h < 200 ? (112, 140, 92) : h < 600 ? (134, 150, 98) : h < 1200 ? (150, 140, 104) : h < 2000 ? (140, 122, 100) : h < 2600 ? (160, 150, 142) : (226, 228, 232);
                if (!sea) col = (col.R * shade, col.G * shade, col.B * shade);
                int o = r * data.Stride + c * 4;
                px[o] = (byte)Math.Clamp(col.B, 0, 255); px[o + 1] = (byte)Math.Clamp(col.G, 0, 255); px[o + 2] = (byte)Math.Clamp(col.R, 0, 255); px[o + 3] = 255;
            }
        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    // Id;Name;x;z;Höhe m;Pistenkurs (Grad, rechtweisend);Piste — Positionen wie von DCS (world.getAirbases) gemeldet
    const string CaucasusBases = """
12;Anapa-Vityazevo;-4447.8;244022.0;43;222;04/22
13;Krasnodar-Center;11652.9;366766.3;30;87;09/27
14;Novorossiysk;-40299.0;279853.5;40;221;04/22
15;Krymsk;-7349.0;293712.0;20;40;04/22
16;Maykop-Khanskaya;-27625.9;457047.7;180;40;04/22
17;Gelendzhik;-50995.6;297848.8;22;40;01/19
18;Sochi-Adler;-165163.3;460901.5;30;62;06/24
19;Krasnodar-Pashkovsky;8706.8;388985.7;34;227;05/23
20;Sukhumi-Babushara;-221381.7;565908.8;13;297;12/30
21;Gudauta;-195650.5;515898.8;21;151;15/33
22;Batumi;-356437.2;618210.9;10;306;13/31
23;Senaki-Kolkhi;-281903.1;648379.1;13;275;09/27
24;Kobuleti;-317604.9;636704.1;18;250;07/25
25;Kutaisi;-284582.8;685029.6;45;254;07/25
26;Mineralnye Vody;-52089.5;707418.1;320;295;12/30
27;Nalchik;-125487.6;759560.1;430;55;06/24
28;Mozdok;-83329.8;835634.7;155;263;08/26
29;Tbilisi-Lochini;-314926.2;895724.0;480;128;13/31
30;Soganlug;-318657.0;896274.9;449;132;14/32
31;Vaziani;-318191.7;902331.8;465;135;13/31
32;Beslan;-148472.4;842251.8;524;94;10/28
""";

    // Golf: Positionen aus Funkfeuern/ILS genähert, Pistenkurse grob; genaue Positionen und Parkplätze schreibt die erste Kampagnenmission mit (Airfields-PersianGulf.txt)
    const string GulfBases = """
1;Abu Musa Island;-31252;-122339;5;80;08/26
2;Bandar Abbas Intl;112100;12300;6;28;03/21
3;Bandar Lengeh;41945;-138343;20;80;08/26
4;Al Dhafra AFB;-212000;-173300;16;128;13/31
5;Dubai Intl;-101300;-88000;5;122;12/30
6;Al Maktoum Intl;-140100;-110000;38;122;12/30
7;Fujairah Intl;-117500;7500;20;113;11/29
8;Tunb Island AFB;11207;-93508;10;90;09/27
9;Havadarya;109566;-5500;15;80;08/26
10;Khasab;-400;-200;20;14;01/19
11;Lar;168933;-179095;793;90;09/27
12;Al Minhad AFB;-126000;-88900;55;90;09/27
13;Qeshm Island;65700;-32836;6;50;05/23
14;Sharjah Intl;-92500;-73300;30;122;12/30
15;Sirri Island;-26409;-171201;9;120;12/30
16;Tunb Kochak;8919;-109438;4;80;08/26
17;Sir Abu Nuayr;-101620;-204319;5;100;10/28
18;Kerman;453173;71709;1751;160;16/34
19;Shiraz Intl;381030;-351866;1487;110;11/29
20;Sas Al Nakheel;-188595;-176233;3;160;16/34
21;Bandar-e-Jask;-57383;155651;6;60;06/24
22;Abu Dhabi Intl;-191000;-161000;20;130;13/31
23;Al-Bateen;-189741;-183148;4;130;13/31
24;Kish Intl;42152;-222115;22;90;09/27
25;Al Ain Intl;-211309;-65722;244;10;01/19
26;Lavan Island;75891;-286531;18;110;11/29
27;Jiroft;283559;141391;808;130;13/31
28;Ras Al Khaimah Intl;-64393;-30032;30;160;16/34
29;Liwa AFB;-273863;-250289;110;130;13/31
""";
}
