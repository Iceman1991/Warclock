namespace DcsCampaign;

/// Startlage: Karte, Gebiete, Achsen, Kräfte, Logistik – alles, was eine Kampagne von der anderen unterscheidet. Positionen in DCS x/z der Karte.
public sealed record Setup
{
    public string Id = "", Map = "Caucasus", Name = "", Text = "", BlueText = "", RedText = "", Start = "";
    public string RedC = "Russia", BlueC = "Georgia", BlueSite = "USA";      // DCS-Länder: Rot; Blau für Gebäude/Wracks; Blau für Flugabwehr/Transporte
    public int[] RedIds = [], BlueIds = [];                                  // Koalitionen (DCS-Länder-Ids)
    public P BullRed, BullBlue;
    public Func<double, double, Side> Owner = (_, _) => Side.None;           // (Lat, Lon) -> Seite bei Kriegsbeginn, None = neutral
    public Func<double, double, bool> InWar = (_, _) => true;                // (x, z) im Kampfgebiet (Straßennetz)
    public int[] BlueBases = [];                                            // alle anderen Plätze: Rot
    public int[] Neutral = [];                                              // Plätze ohne Seite (kein Objekt, in DCS neutral)
    public Dictionary<string, string[]> Names = [];                         // Pilotennamen je Land (sonst Rot/Gastland/Blau)
    public (string Name, Side Push, string[] Keys)[] Axes = [];
    public (string A, string B, string Name)[] Passes = [];                 // Pässe, die das 512-m-Raster nicht hergibt
    public (string A, string B)[] Snow = [];                                // Pässe, die Schneefall sperrt
    public string SnowText = "";
    public string[][] Rail = [];                                            // Bahnhöfe in Fahrtrichtung, Abzweige als eigene Linie
    public (Side S, double X, double Z, string From)[] Sea = [];            // Frachter kommen hierher von See; From: Herkunftshafen auf der Karte (zerstört = keine Frachter)
    public (Side S, string At, double Mun, double Pol)[] Gates = [];        // Einspeisung von außerhalb der Karte, alle 12 h
    public Func<Obj, (double Mun, double Pol)> Cargo = _ => (400, 400);
    public (Side S, UK K, int Ax, string Role, string At, string Tpl, string Name)[] Oob = [];
    public (OK K, Side S, string At, string Name, double Value, string Sys)[] Objs = [];   // At: Ort im Straßennetz oder (Hinterland) irgendein Ort der Karte
    public (string Name, double Lat, double Lon)[] Bridges = [];
    public (Side S, string Country, string Type, string Name, int Base, int N, string Roles)[] Sqns = [];
    public (Side S, string At, string Name, string Sys)[] Decoys = [];
    public (int H, Side S, UK K, string At, string Tpl, string Name)[] Reinf = [];   // Stunde nach Kriegsbeginn
    public (int H, Side S, string Country, string Type, string Name, int Base, int N, string Roles)[] AirReinf = [];   // neue Staffeln im Krieg (Stunde nach Kriegsbeginn)
    public (double Lat, double Lon) Cv;                                      // Trägerposition auf offener See (0 = kein Träger möglich)
    public (Side S, string From, int PerDay)[] Rockets = [];                // Raketen/Drohnen aus einer Startstellung (Objekt; zerstört = keine), PerDay teilt 24
    public (string At, Side S)[] Held = [];                                 // Orte, die bei Kriegsbeginn schon die Seite gewechselt haben (Schlüsselort samt Umland)
    public (Side S, string At, int N, string Sys)[] Tels = [];              // mobile Raketenwerfer (Scud/MLRS) im Raum At
    public (Side S, string Text)[] Join = [];                               // Verbündeter tritt im Krieg ein: Reinf/AirReinf mit H < 0 kommen -H Stunden danach
    public (Side S, string Name, double X, double Z, double Alt, string Task)[] Support = [];   // AWACS-/Tanker-Stationen
    public (string Name, int Ax, bool Night, Side S, double X, double Z)[] Caps = [];        // CAP an der Front einer Achse (Ax ≥ 0) oder fest für Seite S
    public string[] RedNames = [], BlueNames = [], BlueFirst = [], HostNames = [];
    public double Temp = 12;                                                // Tagesmittel °C
}

static class Scenario
{
    static List<Setup> Hand => [Caucasus, CaucasusCounter, CaucasusOffensive, Gulf.Landing, Gulf.Dubai, Gulf.Tanker, .. Syria.Sets];
    /// Zur Auswahl: handgemachte Startlagen + eine erzeugte je weiterer installierter Karte ohne eigene
    public static List<Setup> All => Hand is var h ? [.. h, .. Auto.List().Where(a => h.All(x => x.Map != a.Map))] : [];
    public static Setup Get(string id) => Auto.Get(id) ?? Hand.FirstOrDefault(x => x.Id == id) ?? Caucasus;

    // ---------- Gebiete (Lon, Lat) ----------
    static readonly (double Lon, double Lat)[] RuBorder = [(40.00, 43.38), (40.25, 43.58), (40.70, 43.57), (41.05, 43.52), (41.40, 43.39), (41.85, 43.23), (42.30, 43.17), (42.60, 43.20), (43.10, 42.98), (43.55, 42.83), (43.90, 42.67), (44.30, 42.72), (44.63, 42.73), (45.10, 42.62), (45.65, 42.28), (46.20, 41.90)];
    static readonly (double Lon, double Lat)[] South = [(41.30, 41.52), (42.00, 41.58), (42.60, 41.50), (43.30, 41.35), (43.60, 41.15), (44.20, 41.13), (44.80, 41.20), (45.20, 41.25), (45.60, 41.45), (46.20, 41.60)];
    static readonly (double Lon, double Lat)[] Abkhazia = [(40.00, 43.38), (40.25, 43.58), (40.70, 43.57), (41.05, 43.52), (41.40, 43.39), (41.85, 43.23), (42.15, 43.00), (42.12, 42.75), (41.95, 42.62), (41.80, 42.50), (41.62, 42.43), (41.55, 42.39), (41.00, 42.50), (39.90, 43.30)];
    static readonly (double Lon, double Lat)[] Ossetia = [(43.40, 42.55), (43.55, 42.80), (43.90, 42.67), (44.30, 42.72), (44.45, 42.55), (44.40, 42.35), (44.15, 42.20), (44.02, 42.17), (43.93, 42.15), (43.80, 42.22), (43.62, 42.30), (43.45, 42.40)];

    public static double LineLat((double Lon, double Lat)[] l, double lon)
    {
        if (lon <= l[0].Lon) return l[0].Lat;
        for (int i = 1; i < l.Length; i++)
            if (lon <= l[i].Lon) return l[i - 1].Lat + (l[i].Lat - l[i - 1].Lat) * (lon - l[i - 1].Lon) / (l[i].Lon - l[i - 1].Lon);
        return l[^1].Lat;
    }
    public static bool In((double Lon, double Lat)[] poly, double lon, double lat)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Lat > lat) != (poly[j].Lat > lat) && lon < (poly[j].Lon - poly[i].Lon) * (lat - poly[i].Lat) / (poly[j].Lat - poly[i].Lat) + poly[i].Lon) c = !c;
        return c;
    }
    /// Seite eines Punkts bei Kriegsbeginn; None = neutral.
    public static Side Owner(World w, double x, double z) { var (lat, lon) = Geo.ToLL(x, z); return w.S.Owner(lat, lon); }
    static Side CaucasusOwner(double lat, double lon) =>
        lat < LineLat(South, lon) ? Side.None                                // Türkei/Armenien
        : lon < 40.0 || lat > LineLat(RuBorder, lon) || In(Abkhazia, lon, lat) || In(Ossetia, lon, lat) ? Side.Red : Side.Blue;

    // ---------- Kaukasus: Achsen (Rot -> Blau) ----------
    static readonly (string Name, Side Push, string[] Keys)[] AxesDef =
    [
        ("West (Inguri)", Side.Red, ["Sochi", "Adler", "Gagra", "Gudauta", "Sukhumi", "Ochamchira", "Gali", "Zugdidi", "Senaki", "Samtredia", "Kutaisi", "Zestafoni"]),
        ("Coast (Poti)", Side.Red, ["Gali", "Anaklia", "Zeni", "Poti", "Lanchhuti", "Ozurgeti", "Kobuleti", "Batumi"]),
        ("East (Gori)", Side.Red, ["Alagir", "Java", "Tskhinvali", "Gori", "Kaspi", "Mtskheta", "Tbilisi"]),
        ("Center (Khashuri)", Side.Red, ["Tskhinvali", "Kareli", "Khashuri", "Zestafoni", "Kutaisi"]),
        ("North (Darial)", Side.Red, ["Vladikavkaz", "Kazbegi", "Pasanauri", "Dusheti", "Mtskheta"]),
    ];
    // Pässe, die das 512-m-Raster nicht hergibt
    static readonly (string A, string B, string Name)[] Passes = [("Alagir", "Java", "Roki Tunnel"), ("Vladikavkaz", "Kazbegi", "Darial"), ("Kazbegi", "Pasanauri", "Jvari Pass")];

    // Bahnstrecken (Bahnhöfe in Fahrtrichtung, Abzweige als eigene Linie): Küstenbahn Tuapse–Sochi–Sukhumi–Zugdidi,
    // Transkaukasische Bahn Senaki–Kutaisi–Tbilisi, Batumi-/Poti-Linien, Nordkaukasus Mineralnye Vody–Prokhladny–Vladikavkaz.
    static readonly string[][] RailLines =
    [
        ["Golovinka", "Loo", "Dagomys", "Sochi", "Hosta", "Adler", "Veseloe", "Gagra", "Gudauta", "Psyrtsha", "Sukhumi", "Ochamchira", "Gali", "Zugdidi",
         "Senaki", "Abasha", "Samtredia", "Kutaisi", "Zestafoni", "Shrosha", "Khashuri", "Gori", "Kaspi", "Mtskheta", "Tbilisi", "Lilo", "Sagarejo"],
        ["Ochamchira", "Tkvarcheli"], ["Senaki", "Poti"], ["Samtredia", "Lanchhuti", "Zemo-natanebi", "Kobuleti", "Batumi"], ["Zemo-natanebi", "Ozurgeti"],
        ["Kutaisi", "Tkibuli"], ["Zestafoni", "Chiatura", "Sachhere"], ["Gori", "Tskhinvali"], ["Tbilisi", "Rustavi"], ["Tbilisi", "Marneuli"],
        ["Vladikavkaz", "Beslan", "Elhotovo", "Prohladniy", "Novopavlovsk", "Georgievsk", "Mineral'nye Vody", "Pyatigorsk", "Essentuki", "Kislovodsk"],
        ["Prohladniy", "Nartkala", "Nal'chik"], ["Prohladniy", "Ekaterinogradskaya", "Mozdok"], ["Beslan", "Nazran", "Karabulak"], ["Beslan", "Alagir"],
    ];

    /// Woher ein Frachter für diesen Hafen kommt (nächster Seeweg der Seite) und mit welchem Anteil der Ladung – null: Herkunftshafen zerstört.
    public static (double X, double Z, double K)? SeaGate(World w, Obj port)
    {
        var g = w.S.Sea.Where(g => g.S == port.Side).OrderBy(g => Lane(w.Th, g.X, g.Z, port.X, port.Z) ? 0 : 1).ThenBy(g => Geo.Dist(g.X, g.Z, port.X, port.Z)).FirstOrDefault();
        if (g.From == null) return null;
        if (g.From == "") return (g.X, g.Z, 1);
        var src = w.Objs.FirstOrDefault(o => o.Name == g.From);
        return src == null || src.Side != port.Side || src.Health < 0.3 ? null : (g.X, g.Z, src.Health);
    }

    /// Gerader Seeweg vom Einlauf zum Hafen liegt auf dem Wasser – bis auf die letzten km (Reede, Inseln vor dem Hafen).
    public static bool Lane(Theater th, double gx, double gz, double px, double pz, double near = 40000)
    {
        double d = Geo.Dist(gx, gz, px, pz);
        return Enumerable.Range(0, (int)(d / 2000)).Select(i => i * 2000 / d).Where(f => d * (1 - f) > near).All(f => th.Water(gx + (px - gx) * f, gz + (pz - gz) * f));
    }

    /// Seestation vor einem Hafen: auf dem Einlaufweg seiner Seite (Richtung nächster Seeweg) im freien Wasser; null = keins gefunden.
    public static P? Offshore(World w, Obj port, double min = 25000)
    {
        var gs = w.S.Sea.Where(g => g.S == port.Side).ToList();
        if (gs.Count == 0) return null;
        var g = gs.MinBy(g => Geo.Dist(g.X, g.Z, port.X, port.Z));
        var a = new P(port.X, port.Z); double b = Geo.Brg(a, new P(g.X, g.Z));
        foreach (var d in new[] { min, min + 10000, min + 20000, min + 35000 })
            foreach (var o in new[] { 0, -30, 30, -60, 60 })
                if (Geo.Move(a, b + o, d) is var p && w.Th.Water(p.X, p.Z)) return p;
        return null;
    }

    /// Seekrieg: je Seite ein Überwasserverband vor dem wichtigsten eigenen Hafen (Kaukasus: Moskwa-Gruppe gegen US-Kreuzer, Golf: iranische FK-Schnellboote).
    static void AddNavy(World w)
    {
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            var home = w.Objs.Where(o => o.Kind == OK.Port && o.Side == s).MaxBy(o => o.Value);
            if (home == null || Offshore(w, home) is not { } p) continue;
            var (name, types, ssm) = s == Side.Blue ? ("Blue Surface Group", new[] { "TICONDEROG", "PERRY", "PERRY" }, 16)
                : w.Map == "PersianGulf" ? ("IRIN Missile Boat Group", new[] { "MOLNIYA", "MOLNIYA", "MOLNIYA" }, 8) : (w.Map == "Caucasus" ? "Black Sea Fleet SAG" : w.Map == "Syria" ? "Russian Mediterranean Squadron" : "Red Surface Group", new[] { "MOSCOW", "REZKY", "ALBATROS" }, 16);
            var c = new Convoy { Id = w.NewId(), Side = s, From = -1, To = home.Id, Ship = true, Navy = true, Name = name, Types = [.. types], Trucks = types.Length, Trucks0 = types.Length, Missiles = ssm, Ssm = ssm, X = p.X, Z = p.Z };
            w.Convoys.Add(c);
            w.Intel(s.Enemy())[$"C{c.Id}"] = new Sight { X = p.X, Z = p.Z, T = w.Time, Src = "Pre-war intel", Str = c.Trucks };   // Liegeplatz bekannt
        }
    }

    public const double FactoryOutput = 12;                             // t Munition je Stunde bei vollem Zustand

    /// Version 7: deutsche Namen alter Spielstände (Startlage, Achsen, Objekte, Verbände, Staffeln, Aufklärungsquelle) → englisch. Meldungstexte bleiben.
    static readonly Dictionary<string, string> Renamed = new()
    {
        ["Kaukasus"] = "Caucasus", ["Golf: Landung in den Nordemiraten"] = "Gulf: Landing in the Northern Emirates", ["Golf: Iran vor Dubai"] = "Gulf: Iran Outside Dubai", ["Golf: Tankerkrieg"] = "Gulf: Tanker War",
        ["Küste (Poti)"] = "Coast (Poti)", ["Ost (Gori)"] = "East (Gori)", ["Mitte (Khashuri)"] = "Center (Khashuri)", ["Nord (Darial)"] = "North (Darial)",
        ["Küste (Sharjah)"] = "Coast (Sharjah)", ["Mitte (Al Dhaid)"] = "Center (Al Dhaid)", ["Ostküste (Fujairah)"] = "East Coast (Fujairah)", ["Küste (Dubai)"] = "Coast (Dubai)",
        ["Mitte (Al Ain)"] = "Center (Al Ain)", ["Ostküste (Oman)"] = "East Coast (Oman)", ["7. Gard.-Brigade (Abchasien) 1. Btl"] = "7th Guards Brigade (Abkhazia) 1st Bn", ["131. Mot.-Schützen-Brig. Panzer-Btl"] = "131st Motor Rifle Bde Tank Bn",
        ["131. Brig. Artillerie-Abt."] = "131st Bde Artillery Bn", ["131. Brig. Flugabwehr"] = "131st Bde Air Defense", ["131. Brig. 2. Mot.-Schützen-Btl"] = "131st Bde 2nd Motor Rifle Bn", ["Abchasisches Mot.-Btl Gali"] = "Abkhazian Motorized Bn Gali",
        ["Abchasische Reserve Sukhumi"] = "Abkhazian Reserve Sukhumi", ["19. Mot.-Schützen-Div. Panzer-Btl"] = "19th Motor Rifle Div Tank Bn", ["19. Div. 503. Mot.-Schützen-Rgt"] = "19th Div 503rd Motor Rifle Rgt", ["19. Div. 693. Mot.-Schützen-Rgt"] = "19th Div 693rd Motor Rifle Rgt",
        ["19. Div. Artillerie-Rgt"] = "19th Div Artillery Rgt", ["19. Div. Flugabwehr-Rgt"] = "19th Div Air Defense Rgt", ["Südossetisches Btl"] = "South Ossetian Bn", ["58. Armee Panzer-Reserve"] = "58th Army Tank Reserve",
        ["42. Mot.-Schützen-Div. 70. Rgt"] = "42nd Motor Rifle Div 70th Rgt", ["42. Div. 71. Rgt"] = "42nd Div 71st Rgt", ["58. Armee Aufklärung"] = "58th Army Recon", ["58. Armee Flugabwehr"] = "58th Army Air Defense",
        ["2. Inf.-Brigade 21. Btl"] = "2nd Inf Brigade 21st Bn", ["2. Inf.-Brigade 22. Btl"] = "2nd Inf Brigade 22nd Bn", ["Artillerie-Brigade West"] = "Artillery Brigade West", ["NATO Flugabwehr West"] = "NATO Air Defense West",
        ["2/7 Cav (US) Panzer-Btl"] = "2/7 Cav (US) Tank Bn", ["Küstenschutz-Btl Anaklia"] = "Coastal Defense Bn Anaklia", ["4. Inf.-Brigade 41. Btl"] = "4th Inf Brigade 41st Bn", ["1. Panzer-Btl Gori"] = "1st Tank Bn Gori",
        ["Artillerie-Brigade Ost"] = "Artillery Brigade East", ["Flugabwehr-Btl Ost"] = "Air Defense Bn East", ["3. Inf.-Brigade 31. Btl"] = "3rd Inf Brigade 31st Bn", ["1/8 Inf (US) Bradley-Btl"] = "1/8 Inf (US) Bradley Bn",
        ["1/64 Armor (US) Panzer-Btl"] = "1/64 Armor (US) Tank Bn", ["Gebirgsjäger-Btl Kazbegi"] = "Mountain Infantry Bn Kazbegi", ["1. Inf.-Brigade 11. Btl"] = "1st Inf Brigade 11th Bn", ["3. Inf.-Brigade 32. Btl"] = "3rd Inf Brigade 32nd Bn",
        ["2/2 SCR (US) Stryker-Btl"] = "2/2 SCR (US) Stryker Bn", ["20. Gard.-Mot.-Schützen-Div. Panzer-Btl"] = "20th Guards Motor Rifle Div Tank Bn", ["19. Div. 429. Mot.-Schützen-Rgt"] = "19th Div 429th Motor Rifle Rgt", ["7. Luftsturm-Div. 108. Rgt"] = "7th Air Assault Div 108th Rgt",
        ["58. Armee 2. Panzer-Btl"] = "58th Army 2nd Tank Bn", ["20. Div. Artillerie-Abt."] = "20th Div Artillery Bn", ["76. Luftlande-Div. 104. Rgt"] = "76th Airborne Div 104th Rgt", ["5. Inf.-Brigade 51. Btl"] = "5th Inf Brigade 51st Bn",
        ["3/69 Armor (US) Panzer-Btl"] = "3/69 Armor (US) Tank Bn", ["2/7 Inf (US) Bradley-Btl"] = "2/7 Inf (US) Bradley Bn", ["1/41 FA (US) Artillerie-Abt."] = "1/41 FA (US) Artillery Bn", ["1/2 SCR (US) Stryker-Btl"] = "1/2 SCR (US) Stryker Bn",
        ["Frontdepot Ochamchira"] = "Forward Depot Ochamchira", ["Frontdepot Java"] = "Forward Depot Java", ["Armeedepot Wladikawkas"] = "Army Depot Vladikavkaz", ["Armeedepot Sotschi"] = "Army Depot Sochi",
        ["Rüstungswerk Wladikawkas"] = "Arms Plant Vladikavkaz", ["Werk Naltschik"] = "Plant Nalchik", ["Hafen Sotschi"] = "Port Sochi", ["Stab 58. Armee"] = "HQ 58th Army",
        ["Stab Gruppe West"] = "HQ Group West", ["Gefechtsstand Zchinwali"] = "Command Post Tskhinvali", ["Radar Sotschi"] = "Radar Sochi", ["Radar Mosdok"] = "Radar Mozdok",
        ["SA-11 Sotschi"] = "SA-11 Sochi", ["SA-6 Dschawa"] = "SA-6 Java", ["SA-15 Gudauta Platz"] = "SA-15 Gudauta Airfield", ["SA-15 Wladikawkas"] = "SA-15 Vladikavkaz",
        ["Frontdepot Senaki"] = "Forward Depot Senaki", ["Frontdepot Kaspi"] = "Forward Depot Kaspi", ["Depot Duscheti"] = "Depot Dusheti", ["Armeedepot Kutaissi"] = "Army Depot Kutaisi",
        ["Armeedepot Tiflis"] = "Army Depot Tbilisi", ["Stahlwerk Rustawi"] = "Steel Works Rustavi", ["Werk Sestaponi"] = "Plant Zestafoni", ["Flugzeugwerk Tiflis"] = "Aircraft Plant Tbilisi",
        ["Hafen Poti"] = "Port Poti", ["Hafen Batumi"] = "Port Batumi", ["Generalstab Tiflis"] = "General Staff Tbilisi", ["Stab Kommando West"] = "HQ Command West",
        ["Radar Kutaissi"] = "Radar Kutaisi", ["Radar Tiflis"] = "Radar Tbilisi", ["Patriot Tiflis"] = "Patriot Tbilisi", ["Patriot Kutaissi"] = "Patriot Kutaisi",
        ["SA-11 Suchumi"] = "SA-11 Sukhumi", ["Hawk Kutaissi"] = "Hawk Kutaisi", ["NASAMS Chaschuri"] = "NASAMS Khashuri", ["Inguri-Brücke Rukhi"] = "Inguri Bridge Rukhi",
        ["Kodori-Brücke"] = "Kodori Bridge", ["Gumista-Brücke Sukhumi"] = "Gumista Bridge Sukhumi", ["Psou-Brücke"] = "Psou Bridge", ["Rioni-Brücke Poti"] = "Rioni Bridge Poti",
        ["Rioni-Brücke Samtredia"] = "Rioni Bridge Samtredia", ["Kura-Brücke Gori"] = "Kura Bridge Gori", ["Aragvi-Brücke Mzcheta"] = "Aragvi Bridge Mtskheta", ["Liachwi-Brücke"] = "Liakhvi Bridge",
        ["Roki-Tunnel"] = "Roki Tunnel", ["Darial-Brücke"] = "Darial Bridge", ["Tskhenis-Brücke"] = "Tskhenis Bridge", ["Georgische Luftwaffe 1. Staffel"] = "Georgian Air Force 1st Squadron",
        ["909th ARS (Korb)"] = "909th ARS (Basket)", ["3. Jagdfliegerregiment"] = "3rd Fighter Aviation Regiment", ["31. Jagdfliegerregiment"] = "31st Fighter Aviation Regiment", ["3624. Fliegerbasis"] = "3624th Air Base",
        ["Abfangstaffel Mosdok"] = "Interceptor Squadron Mozdok", ["559. Bomberregiment"] = "559th Bomber Regiment", ["368. Schlachtfliegerregiment"] = "368th Assault Aviation Regiment", ["Schlachtflieger Sukhumi"] = "Assault Squadron Sukhumi",
        ["47. Bomberregiment"] = "47th Bomber Regiment", ["52. Bomberregiment"] = "52nd Bomber Regiment", ["A-50 Staffel"] = "A-50 Squadron", ["1-227th ARB Apache (B-Kompanie)"] = "1-227th ARB Apache (B Company)",
        ["Dustoff Kutaissi (CSAR)"] = "Dustoff Kutaisi (CSAR)", ["55. Hubschrauberregiment"] = "55th Helicopter Regiment", ["Hubschrauberstaffel Gudauta"] = "Helicopter Squadron Gudauta", ["Rettungsstaffel Gudauta (CSAR)"] = "Rescue Squadron Gudauta (CSAR)",
        ["235. Transportfliegerregiment"] = "235th Transport Aviation Regiment", ["Transportstaffel Maikop"] = "Transport Squadron Maykop", ["Hafen Bandar Abbas"] = "Port Bandar Abbas", ["Raffinerie Bandar Abbas"] = "Refinery Bandar Abbas",
        ["Werft Qeshm"] = "Shipyard Qeshm", ["Industriegebiet Bandar Abbas"] = "Industrial Area Bandar Abbas", ["Hafen Bandar Lengeh"] = "Port Bandar Lengeh", ["Ölterminal Sirri"] = "Oil Terminal Sirri",
        ["Stab IRGC-Marine Qeshm"] = "HQ IRGC Navy Qeshm", ["Raketenbrigade Bastak"] = "Missile Brigade Bastak", ["Drohnenbasis Bandar Khamir"] = "Drone Base Bandar Khamir", ["Hauptquartier Marine Bandar Abbas"] = "Navy HQ Bandar Abbas",
        ["Raffinerie Lar"] = "Refinery Lar", ["Kraftwerk Minab"] = "Power Plant Minab", ["Marinestützpunkt Jask"] = "Naval Base Jask", ["Stab Südkommando Hajiabad"] = "HQ Southern Command Hajiabad",
        ["SA-15 Raffinerie"] = "SA-15 Refinery", ["SA-2 Hormus"] = "SA-2 Hormuz", ["SA-15 Große Tunb"] = "SA-15 Greater Tunb", ["Armeedepot Abu Dhabi"] = "Army Depot Abu Dhabi",
        ["Aluminiumwerk EMAL"] = "Aluminium Plant EMAL", ["Industriegebiet Musaffah"] = "Industrial Area Musaffah", ["Hafen Jebel Ali"] = "Port Jebel Ali", ["Hafen Mina Zayed"] = "Port Mina Zayed",
        ["Generalstab Abu Dhabi"] = "General Staff Abu Dhabi", ["Al-Maktoum-Brücke (Dubai Creek)"] = "Al Maktoum Bridge (Dubai Creek)", ["Al-Garhoud-Brücke"] = "Al Garhoud Bridge", ["Al-Maqta-Brücke (Abu Dhabi)"] = "Al Maqta Bridge (Abu Dhabi)",
        ["Hafen Ras al-Khaimah"] = "Port Ras al-Khaimah", ["Hafen Khasab"] = "Port Khasab", ["Armeedepot Khasab"] = "Army Depot Khasab", ["Frontdepot Ras al-Khaimah"] = "Forward Depot Ras al-Khaimah",
        ["Frontdepot Dibba"] = "Forward Depot Dibba", ["Gefechtsstand IRGC-Marine"] = "Command Post IRGC Marines", ["Frontdepot Ajman"] = "Forward Depot Ajman", ["Frontdepot Al Dhaid"] = "Forward Depot Al Dhaid",
        ["Frontdepot Fujairah"] = "Forward Depot Fujairah", ["Armeedepot Dubai"] = "Army Depot Dubai", ["Hafen Fujairah"] = "Port Fujairah", ["Hafen Khor Fakkan"] = "Port Khor Fakkan",
        ["Stab Nordkommando"] = "HQ Northern Command", ["Hafen Sharjah"] = "Port Sharjah", ["Armeedepot Ras al-Khaimah"] = "Army Depot Ras al-Khaimah", ["Frontdepot Khor Fakkan"] = "Forward Depot Khor Fakkan",
        ["Gefechtsstand IRGC Sharjah"] = "Command Post IRGC Sharjah", ["SA-11 Umm al-Qaiwain"] = "SA-11 Umm Al Quwain", ["Armeedepot Jebel Ali"] = "Army Depot Jebel Ali", ["Frontdepot Al Madam"] = "Forward Depot Al Madam",
        ["Stab Dubai"] = "HQ Dubai", ["1. Shaheen-Staffel (VAE)"] = "1st Shaheen Squadron (UAE)", ["71. Staffel (VAE)"] = "71st Squadron (UAE)", ["81. Jagdstaffel (Tomcat)"] = "81st Fighter Squadron (Tomcat)",
        ["23. Jagdstaffel (Fulcrum)"] = "23rd Fighter Squadron (Fulcrum)", ["91. Jagdbomberstaffel (Phantom)"] = "91st Fighter-Bomber Squadron (Phantom)", ["Tiger-Staffel Bandar Abbas"] = "Tiger Squadron Bandar Abbas", ["72. Bomberstaffel (Fencer)"] = "72nd Bomber Squadron (Fencer)",
        ["82. Jagdstaffel (Tomcat)"] = "82nd Fighter Squadron (Tomcat)", ["11. Jagdstaffel (Fulcrum)"] = "11th Fighter Squadron (Fulcrum)", ["61. Jagdbomberstaffel (Phantom)"] = "61st Fighter-Bomber Squadron (Phantom)", ["IRGC-Schlachtflieger"] = "IRGC Ground Attack Squadron",
        ["Apache-Staffel Al Minhad (VAE)"] = "Apache Squadron Al Minhad (UAE)", ["Rettungsstaffel Al Minhad (CSAR)"] = "Rescue Squadron Al Minhad (CSAR)", ["IRGC-Heeresflieger Havadarya"] = "IRGC Army Aviation Havadarya", ["IRGC-Rettungsstaffel (CSAR)"] = "IRGC Rescue Squadron (CSAR)",
        ["Transportstaffel Al Dhafra (VAE)"] = "Transport Squadron Al Dhafra (UAE)", ["Transportstaffel Shiraz"] = "Transport Squadron Shiraz", ["Transportstaffel Kerman"] = "Transport Squadron Kerman", ["IRGC-Marine 1. Brigade 1. Btl"] = "IRGC Marines 1st Brigade 1st Bn",
        ["Artesh 37. Panzerbrigade 1. Btl"] = "Artesh 37th Armored Brigade 1st Bn", ["IRGC Artillerie-Btl Ras al-Khaimah"] = "IRGC Artillery Bn Ras al-Khaimah", ["IRGC Flugabwehr Nord"] = "IRGC Air Defense North", ["IRGC-Marine 1. Brigade 2. Btl"] = "IRGC Marines 1st Brigade 2nd Bn",
        ["IRGC Saberin-Btl"] = "IRGC Saberin Bn", ["IRGC-Marine Btl Dibba"] = "IRGC Marines Bn Dibba", ["IRGC-Marine 1. Brigade 3. Btl"] = "IRGC Marines 1st Brigade 3rd Bn", ["Artesh 37. Panzerbrigade 2. Btl"] = "Artesh 37th Armored Brigade 2nd Bn",
        ["IRGC-Marine Reserve Khasab"] = "IRGC Marines Reserve Khasab", ["VAE 1. Mech.-Brigade 11. Btl"] = "UAE 1st Mech Brigade 11th Bn", ["VAE 3. Panzerbrigade 31. Btl"] = "UAE 3rd Armored Brigade 31st Bn", ["VAE Artillerie-Brigade Nord"] = "UAE Artillery Brigade North",
        ["VAE Flugabwehr Nord"] = "UAE Air Defense North", ["VAE 1. Mech.-Brigade 12. Btl"] = "UAE 1st Mech Brigade 12th Bn", ["Präsidialgarde Aufklärungs-Btl"] = "Presidential Guard Recon Bn", ["VAE Küstenschutz-Btl Fujairah"] = "UAE Coastal Defense Bn Fujairah",
        ["VAE 2. Mech.-Brigade 21. Btl"] = "UAE 2nd Mech Brigade 21st Bn", ["Präsidialgarde Panzer-Btl"] = "Presidential Guard Tank Bn", ["VAE 3. Panzerbrigade 32. Btl"] = "UAE 3rd Armored Brigade 32nd Bn", ["IRGC-Marine 2. Brigade 1. Btl"] = "IRGC Marines 2nd Brigade 1st Bn",
        ["Artesh 37. Panzerbrigade 3. Btl"] = "Artesh 37th Armored Brigade 3rd Bn", ["Artesh Artillerie-Gruppe 33"] = "Artesh Artillery Group 33", ["IRGC-Marine 2. Brigade 2. Btl"] = "IRGC Marines 2nd Brigade 2nd Bn", ["VAE 2. Mech.-Brigade 22. Btl"] = "UAE 2nd Mech Brigade 22nd Bn",
        ["IRGC Artillerie-Btl Sharjah"] = "IRGC Artillery Bn Sharjah", ["IRGC Flugabwehr Sharjah"] = "IRGC Air Defense Sharjah", ["IRGC-Marine Btl Khor Fakkan"] = "IRGC Marines Bn Khor Fakkan", ["Artesh 55. Luftlandebrigade 1. Btl"] = "Artesh 55th Airborne Brigade 1st Bn",
        ["IRGC-Marine 3. Brigade 1. Btl"] = "IRGC Marines 3rd Brigade 1st Bn", ["Vorkriegsaufklärung"] = "Pre-war intel",
    };
    static string Ren(string s) => Renamed.GetValueOrDefault(s, s);

    /// Spielstände älterer Versionen nachrüsten (idempotent): Häfen, Sprit, Schneepässe, Gleise.
    public static void Upgrade(World w)
    {
        if (w.Version < 7)                                                  // englische Namen – vor dem Staffel-Nachrüsten (Abgleich über den Namen)
        {
            w.Name = Ren(w.Name);
            foreach (var n in w.Nodes) n.Name = Ren(n.Name);
            foreach (var a in w.Axes) a.Name = Ren(a.Name);
            foreach (var o in w.Objs) o.Name = Ren(o.Name);
            foreach (var u in w.Units) u.Name = Ren(u.Name);
            foreach (var q in w.Sqns) q.Name = Ren(q.Name);
            foreach (var s in w.IntelRed.Values.Concat(w.IntelBlue.Values)) s.Src = Ren(s.Src);
        }
        foreach (var p in w.Objs.Where(o => o.Kind == OK.Port && o.Cap == 0)) { p.Cap = 1500; p.Output = 0; }   // Häfen leben von Frachtern
        if (w.Version < 2)
        {
            foreach (var o in w.Objs.Where(o => o.Kind is OK.Depot or OK.Airbase or OK.Port)) o.Fuel = Math.Min(o.Cap, o.Stock);
            foreach (var o in w.Objs.Where(o => o.Kind == OK.Factory)) o.Output = FactoryOutput;
            w.Version = 2;
        }
        if (w.Version < 3) { AddDecoys(w); w.Version = 3; }
        if (w.Version < 4) { PrewarIntel(w); w.Version = 4; }
        if (w.Version < 6)                                                  // neue Staffeln (Hubschrauber, Transporter) nachrüsten, wenn ihr Platz noch in eigener Hand ist; Piloten am Boden bekommen Ids
        {
            foreach (var d in w.S.Sqns.Where(d => w.Sqns.All(q => q.Name != d.Name) && w.Base(d.Base) is { } b && b.Side == d.S)) AddSqn(w, d);
            foreach (var d in w.Downed.Where(d => d.Id == 0)) d.Id = w.NewId();
            w.Version = 6;
        }
        if (w.Version < 7) w.Version = 7;
        if (w.Version < 8) { AddNavy(w); w.Version = 8; }
        if (w.Version < 9) { w.Esc = 2; AddTels(w); w.Version = 9; }       // laufende Kriege: keine Beschränkung mehr nachträglich
        foreach (var e in w.Edges.Where(e => e.Pass))
            e.Snow = new[] { w.Nodes[e.A].Name, w.Nodes[e.B].Name } is var n && w.S.Snow.Any(p => n.Contains(p.A) && n.Contains(p.B));
        if (w.Edges.Any(e => e.Rail) || w.S.Rail.Length == 0) return;
        var seen = new HashSet<(int, int)>();
        foreach (var line in w.S.Rail)
            for (int i = 1; i < line.Length; i++)
            {
                Node a = Town(w, line[i - 1]), b = Town(w, line[i]);
                if (!seen.Add((Math.Min(a.Id, b.Id), Math.Max(a.Id, b.Id)))) continue;
                double d = Geo.Dist(a.X, a.Z, b.X, b.Z) * 1.15;                // Gleis windet sich
                var e = new Edge { Id = w.Edges.Count, A = a.Id, B = b.Id, Len = d, Cost = d, Rail = true };
                var br = w.Objs.Where(o => o.Kind == OK.Bridge).Select(o => (o, d: Geo.SegDist(new P(o.X, o.Z), new P(a.X, a.Z), new P(b.X, b.Z)))).Where(t => t.d < 2500).OrderBy(t => t.d).FirstOrDefault();
                if (br.o != null) e.Bridge = br.o.Id;                          // Fluss quert Straße und Gleis: eine Brücke sperrt beide
                w.Edges.Add(e);
            }
        w.Link();
    }

    // ---------- Bodentruppen (alle Karten) ----------
    public static readonly Dictionary<string, string[]> Templates = new()
    {
        ["RU-Armor"] = ["T-72B3", "T-72B3", "T-72B3", "T-72B3", "T-72B3", "BMP-3", "BMP-3", "Ural-375 PBU"],
        ["RU-Mech"] = ["BMP-2", "BMP-2", "BMP-2", "BMP-2", "BMP-2", "T-72B", "T-72B", "Ural-375"],
        ["RU-Motor"] = ["BTR-80", "BTR-80", "BTR-80", "BTR-80", "BRDM-2", "Ural-375", "SA-18 Igla manpad"],
        ["RU-Art"] = ["SAU Msta", "SAU Msta", "SAU Msta", "Grad-URAL", "Grad-URAL", "KAMAZ Truck"],
        ["RU-Shorad"] = ["2S6 Tunguska", "2S6 Tunguska", "Strela-10M3", "Osa 9A33 ln", "ZSU-23-4 Shilka"],
        ["GE-Armor"] = ["T-72B", "T-72B", "T-72B", "T-72B", "T-72B", "BMP-2", "BMP-2", "Ural-375 PBU"],
        ["GE-Mech"] = ["BMP-2", "BMP-2", "BMP-2", "BMP-2", "BMP-2", "T-72B", "T-72B", "Ural-375"],
        ["GE-Motor"] = ["BTR-80", "BTR-80", "BTR-80", "BTR-80", "Hummer", "Ural-375", "SA-18 Igla manpad"],
        ["GE-Art"] = ["SAU Gvozdika", "SAU Gvozdika", "SAU Gvozdika", "Grad-URAL", "Grad-URAL", "Ural-375"],
        ["GE-Shorad"] = ["Strela-10M3", "Strela-10M3", "ZSU-23-4 Shilka", "Osa 9A33 ln"],
        ["US-Armor"] = ["M-1 Abrams", "M-1 Abrams", "M-1 Abrams", "M-1 Abrams", "M-1 Abrams", "M-2 Bradley", "M-2 Bradley", "M 818"],
        ["US-Mech"] = ["M-2 Bradley", "M-2 Bradley", "M-2 Bradley", "M-2 Bradley", "M-2 Bradley", "M-1 Abrams", "M-1 Abrams", "M 818"],
        ["US-Motor"] = ["M1126 Stryker ICV", "M1126 Stryker ICV", "M1126 Stryker ICV", "M1126 Stryker ICV", "M1128 Stryker MGS", "Hummer"],
        ["US-Art"] = ["M-109", "M-109", "M-109", "MLRS", "MLRS", "M 818"],
        ["US-Shorad"] = ["M1097 Avenger", "M1097 Avenger", "Gepard", "M6 Linebacker"],
        ["IR-Armor"] = ["T-72B", "T-72B", "T-72B", "Chieftain_mk3", "Chieftain_mk3", "BMP-2", "BMP-2", "Ural-375"],
        ["IR-Mech"] = ["BMP-2", "BMP-2", "BMP-1", "BMP-1", "BMP-1", "T-55", "T-55", "Ural-375"],
        ["IR-Motor"] = ["BTR-80", "BTR-80", "M-113", "M-113", "BMP-1", "Ural-375", "SA-18 Igla manpad"],
        ["IR-Art"] = ["M-109", "M-109", "SAU Gvozdika", "Grad-URAL", "Grad-URAL", "Ural-375"],
        ["IR-Shorad"] = ["Tor 9A331", "ZSU-23-4 Shilka", "ZSU-23-4 Shilka", "Strela-10M3"],
        ["AE-Armor"] = ["Leclerc", "Leclerc", "Leclerc", "Leclerc", "Leclerc", "BMP-3", "BMP-3", "M 818"],
        ["AE-Mech"] = ["BMP-3", "BMP-3", "BMP-3", "BMP-3", "BMP-3", "Leclerc", "Leclerc", "M 818"],
        ["AE-Motor"] = ["TPZ", "TPZ", "TPZ", "TPZ", "Hummer", "M 818"],
        ["AE-Art"] = ["M-109", "M-109", "M-109", "MLRS", "MLRS", "M 818"],
        ["AE-Shorad"] = ["M1097 Avenger", "M1097 Avenger", "Gepard", "Strela-10M3"],
        ["SY-Armor"] = ["T-72B", "T-72B", "T-72B", "T-55", "T-55", "BMP-1", "BMP-1", "Ural-375"],
        ["SY-Mech"] = ["BMP-1", "BMP-1", "BMP-2", "BMP-2", "BMP-1", "T-72B", "T-55", "Ural-375"],
        ["SY-Motor"] = ["BTR-80", "BRDM-2", "BTR-80", "BRDM-2", "Ural-375", "SA-18 Igla manpad"],
        ["SY-Art"] = ["SAU Gvozdika", "SAU Gvozdika", "SAU Gvozdika", "Grad-URAL", "Grad-URAL", "Ural-375"],
        ["SY-Shorad"] = ["ZSU-23-4 Shilka", "ZSU-23-4 Shilka", "Strela-10M3", "Osa 9A33 ln"],
        ["IL-Armor"] = ["Merkava_Mk4", "Merkava_Mk4", "Merkava_Mk4", "Merkava_Mk4", "Merkava_Mk4", "M-113", "M-113", "M 818"],
        ["IL-Mech"] = ["M-113", "M-113", "M-113", "M-113", "M-113", "Merkava_Mk4", "Merkava_Mk4", "M 818"],
        ["IL-Art"] = ["M-109", "M-109", "M-109", "MLRS", "MLRS", "M 818"],
        ["IL-Shorad"] = ["Vulcan", "Vulcan", "M48 Chaparral", "M1097 Avenger"],
        ["TR-Armor"] = ["leopard-2A4", "leopard-2A4", "leopard-2A4", "leopard-2A4", "leopard-2A4", "M-113", "M-113", "M 818"],
        ["TR-Mech"] = ["M-113", "M-113", "M-113", "M-113", "M-113", "M-60", "M-60", "M 818"],
        ["TR-Art"] = ["T155_Firtina", "T155_Firtina", "T155_Firtina", "MLRS", "MLRS", "M 818"],
        ["TR-Shorad"] = ["Vulcan", "Vulcan", "M48 Chaparral", "M1097 Avenger"],
        ["JO-Armor"] = ["Challenger2", "Challenger2", "Challenger2", "Challenger2", "Challenger2", "M-113", "M-113", "M 818"],
        ["JO-Mech"] = ["M-113", "M-113", "M-113", "M-113", "M-113", "M-60", "M-60", "M 818"],
        ["JO-Art"] = ["M-109", "M-109", "M-109", "MLRS", "MLRS", "M 818"],
        ["JO-Shorad"] = ["Gepard", "Gepard", "M1097 Avenger", "Vulcan"],
        ["Convoy-RU"] = ["Ural-375", "Ural-375", "KAMAZ Truck", "KAMAZ Truck", "Ural-375", "GAZ-66"],
        ["Convoy-BL"] = ["M 818", "M 818", "M978 HEMTT Tanker", "M 818", "Hummer"],
    };

    /// SAM-Systeme: Name, Reichweite km, max. Höhe m, Raketen, Teile (Typ, Anzahl)
    public static readonly Dictionary<string, (double Km, double Alt, int Missiles, (string T, int N)[] Parts)> Sams = new()
    {
        ["SA-10"] = (75, 27000, 16, [("S-300PS 64H6E sr", 1), ("S-300PS 40B6MD sr", 1), ("S-300PS 40B6M tr", 1), ("S-300PS 54K6 cp", 1), ("S-300PS 5P85C ln", 2), ("S-300PS 5P85D ln", 2)]),
        ["SA-11"] = (35, 22000, 12, [("SA-11 Buk SR 9S18M1", 1), ("SA-11 Buk CC 9S470M1", 1), ("SA-11 Buk LN 9A310M1", 3)]),
        ["SA-6"] = (24, 12000, 9, [("Kub 1S91 str", 1), ("Kub 2P25 ln", 3)]),
        ["SA-2"] = (40, 25000, 6, [("p-19 s-125 sr", 1), ("SNR_75V", 1), ("S_75M_Volhov", 6)]),
        ["SA-15"] = (12, 6000, 16, [("Tor 9A331", 2)]),
        ["Patriot"] = (100, 24000, 16, [("Patriot str", 1), ("Patriot ECS", 1), ("Patriot cp", 1), ("Patriot EPP", 1), ("Patriot AMG", 1), ("Patriot ln", 4)]),
        ["Hawk"] = (40, 14000, 9, [("Hawk sr", 1), ("Hawk tr", 1), ("Hawk pcp", 1), ("Hawk cwar", 1), ("Hawk ln", 3)]),
        ["NASAMS"] = (25, 15000, 12, [("NASAMS_Radar_MPQ64F1", 1), ("NASAMS_Command_Post", 1), ("NASAMS_LN_C", 2)]),
        ["EWR-RU"] = (0, 0, 0, [("1L13 EWR", 1)]),
        ["EWR-RU2"] = (0, 0, 0, [("55G6 EWR", 1)]),
        ["EWR-BL"] = (0, 0, 0, [("FPS-117", 1), ("FPS-117 ECS", 1)]),
        ["Scud"] = (0, 0, 0, [("Scud_B", 2)]), ["MLRS"] = (0, 0, 0, [("MLRS", 2)]),   // Raketenwerfer (OK.Tel)
    };

    /// Teile der Ziele (wie die Features in BMS): Name, Gewicht am Gesamtzustand, Gebäude in DCS. Reihenfolge = Reihenfolge der Instandsetzung.
    /// Piste: Bombentreffer auf der Bahn (IMPACT), Shelters: Maschinen am Boden; Lager brennen, Hallen/Kraftwerk bremsen die Produktion.
    public static readonly Dictionary<OK, (string Name, double W, string[] Types)[]> Parts = new()
    {
        [OK.Airbase] = [("Runway", 0.4, []), ("Shelters", 0.25, []), ("Ammo dump", 0.2, [".Ammunition depot", "Small werehouse 1"]), ("Fuel farm", 0.15, ["Fuel tank", "Tank", "Tank"])],
        [OK.Depot] = [("Warehouses", 0.6, [".Ammunition depot", "Warehouse", "Small werehouse 1"]), ("Fuel tanks", 0.4, ["Tank", "Tank", "Fuel tank"])],
        [OK.Factory] = [("Production halls", 0.7, ["Workshop A", "Tech combine", "Workshop A", "Repair workshop"]), ("Power plant", 0.3, ["Chemical tank A"])],
        [OK.Port] = [("Cargo handling", 0.6, ["Warehouse", "Warehouse", "Small werehouse 1"]), ("Fuel terminal", 0.4, ["Tank 2", "Tank"])],
        [OK.Cp] = [("Command bunker", 0.6, [".Command Center", "Military staff"]), ("Comms", 0.4, ["Comms tower M", "Garage A"])],
    };
    public static readonly Dictionary<OK, string[]> Statics = Parts.ToDictionary(kv => kv.Key, kv => kv.Value.SelectMany(p => p.Types).ToArray());
    /// Gebäude Nummer i (ab 0, wie in Statics) → Teil
    public static int PartAt(OK kind, int i)
    {
        var l = Parts[kind];
        for (int k = 0; k < l.Length; k++) { if (i < l[k].Types.Length) return k; i -= l[k].Types.Length; }
        return l.Length - 1;
    }

    // Seite, Art, Achse, Rolle, Ort, Vorlage, Name
    static readonly (Side S, UK K, int Ax, string Role, string At, string Tpl, string Name)[] Oob =
    [
        (Side.Red, UK.Mech, 0, "front", "Gali", "RU-Mech", "7th Guards Brigade (Abkhazia) 1st Bn"),
        (Side.Red, UK.Armor, 0, "front", "Gali", "RU-Armor", "131st Motor Rifle Bde Tank Bn"),
        (Side.Red, UK.Art, 0, "front", "Ochamchira", "RU-Art", "131st Bde Artillery Bn"),
        (Side.Red, UK.Shorad, 0, "front", "Gali", "RU-Shorad", "131st Bde Air Defense"),
        (Side.Red, UK.Mech, 0, "reserve", "Ochamchira", "RU-Mech", "131st Bde 2nd Motor Rifle Bn"),
        (Side.Red, UK.Motor, 1, "front", "Gali", "RU-Motor", "Abkhazian Motorized Bn Gali"),
        (Side.Red, UK.Motor, 0, "reserve", "Sukhumi", "RU-Motor", "Abkhazian Reserve Sukhumi"),
        (Side.Red, UK.Armor, 2, "front", "Tskhinvali", "RU-Armor", "19th Motor Rifle Div Tank Bn"),
        (Side.Red, UK.Mech, 2, "front", "Tskhinvali", "RU-Mech", "19th Div 503rd Motor Rifle Rgt"),
        (Side.Red, UK.Mech, 3, "front", "Tskhinvali", "RU-Mech", "19th Div 693rd Motor Rifle Rgt"),
        (Side.Red, UK.Art, 2, "front", "Java", "RU-Art", "19th Div Artillery Rgt"),
        (Side.Red, UK.Shorad, 2, "front", "Tskhinvali", "RU-Shorad", "19th Div Air Defense Rgt"),
        (Side.Red, UK.Motor, 3, "front", "Tskhinvali", "RU-Motor", "South Ossetian Bn"),
        (Side.Red, UK.Armor, 2, "reserve", "Java", "RU-Armor", "58th Army Tank Reserve"),
        (Side.Red, UK.Mech, 2, "reserve", "Alagir", "RU-Mech", "42nd Motor Rifle Div 70th Rgt"),
        (Side.Red, UK.Mech, 4, "front", "Vladikavkaz", "RU-Mech", "42nd Div 71st Rgt"),
        (Side.Red, UK.Motor, 4, "front", "Vladikavkaz", "RU-Motor", "58th Army Recon"),
        (Side.Red, UK.Shorad, 4, "reserve", "Vladikavkaz", "RU-Shorad", "58th Army Air Defense"),

        (Side.Blue, UK.Mech, 0, "front", "Zugdidi", "GE-Mech", "2nd Inf Brigade 21st Bn"),
        (Side.Blue, UK.Mech, 0, "front", "Zugdidi", "GE-Mech", "2nd Inf Brigade 22nd Bn"),
        (Side.Blue, UK.Art, 0, "front", "Senaki", "GE-Art", "Artillery Brigade West"),
        (Side.Blue, UK.Shorad, 0, "front", "Zugdidi", "US-Shorad", "NATO Air Defense West"),
        (Side.Blue, UK.Armor, 0, "reserve", "Senaki", "US-Armor", "2/7 Cav (US) Tank Bn"),
        (Side.Blue, UK.Motor, 1, "front", "Anaklia", "GE-Motor", "Coastal Defense Bn Anaklia"),
        (Side.Blue, UK.Mech, 2, "front", "Gori", "GE-Mech", "4th Inf Brigade 41st Bn"),
        (Side.Blue, UK.Armor, 2, "front", "Gori", "GE-Armor", "1st Tank Bn Gori"),
        (Side.Blue, UK.Art, 2, "front", "Kaspi", "GE-Art", "Artillery Brigade East"),
        (Side.Blue, UK.Shorad, 2, "front", "Gori", "GE-Shorad", "Air Defense Bn East"),
        (Side.Blue, UK.Motor, 3, "front", "Kareli", "GE-Motor", "3rd Inf Brigade 31st Bn"),
        (Side.Blue, UK.Mech, 2, "reserve", "Mtskheta", "US-Mech", "1/8 Inf (US) Bradley Bn"),
        (Side.Blue, UK.Armor, 2, "reserve", "Tbilisi", "US-Armor", "1/64 Armor (US) Tank Bn"),
        (Side.Blue, UK.Motor, 4, "front", "Kazbegi", "GE-Motor", "Mountain Infantry Bn Kazbegi"),
        (Side.Blue, UK.Mech, 4, "front", "Pasanauri", "GE-Mech", "1st Inf Brigade 11th Bn"),           // Tiefe: Nord und Mitte fielen sonst am 2. Tag bis Mzcheta/Kutaissi
        (Side.Blue, UK.Mech, 3, "front", "Khashuri", "GE-Mech", "3rd Inf Brigade 32nd Bn"),
        (Side.Blue, UK.Motor, 3, "reserve", "Khashuri", "US-Motor", "2/2 SCR (US) Stryker Bn"),
    ];

    // Objekte: Art, Seite, Ort (Stadt), Name, Wert, System
    static readonly (OK K, Side S, string At, string Name, double Value, string Sys)[] ObjDef =
    [
        (OK.Depot, Side.Red, "Ochamchira", "Forward Depot Ochamchira", 3, ""),
        (OK.Depot, Side.Red, "Java", "Forward Depot Java", 3, ""),
        (OK.Depot, Side.Red, "Vladikavkaz", "Army Depot Vladikavkaz", 4, ""),
        (OK.Depot, Side.Red, "Sukhumi", "Depot Sukhumi", 3, ""),
        (OK.Depot, Side.Red, "Sochi", "Army Depot Sochi", 4, ""),
        (OK.Factory, Side.Red, "Vladikavkaz", "Arms Plant Vladikavkaz", 4, ""),
        (OK.Factory, Side.Red, "Nal'chik", "Plant Nalchik", 3, ""),
        (OK.Port, Side.Red, "Sochi", "Port Sochi", 3, ""),
        (OK.Cp, Side.Red, "Vladikavkaz", "HQ 58th Army", 5, ""),
        (OK.Cp, Side.Red, "Sukhumi", "HQ Group West", 4, ""),
        (OK.Cp, Side.Red, "Tskhinvali", "Command Post Tskhinvali", 3, ""),
        (OK.Ewr, Side.Red, "Gudauta", "Radar Gudauta", 3, "EWR-RU2"),
        (OK.Ewr, Side.Red, "Java", "Radar Java", 3, "EWR-RU"),
        (OK.Ewr, Side.Red, "Sochi", "Radar Sochi", 2, "EWR-RU2"),
        (OK.Ewr, Side.Red, "Mozdok", "Radar Mozdok", 2, "EWR-RU"),
        (OK.Sam, Side.Red, "Gudauta", "SA-10 Gudauta", 5, "SA-10"),
        (OK.Sam, Side.Red, "Alagir", "SA-10 Alagir", 5, "SA-10"),
        (OK.Sam, Side.Red, "Ochamchira", "SA-11 Ochamchira", 4, "SA-11"),
        (OK.Sam, Side.Red, "Java", "SA-11 Java", 4, "SA-11"),
        (OK.Sam, Side.Red, "Sochi", "SA-11 Sochi", 3, "SA-11"),
        (OK.Sam, Side.Red, "Sukhumi", "SA-6 Sukhumi", 3, "SA-6"),
        (OK.Sam, Side.Red, "Java", "SA-6 Java", 3, "SA-6"),
        (OK.Sam, Side.Red, "Gudauta", "SA-15 Gudauta Airfield", 2, "SA-15"),
        (OK.Sam, Side.Red, "Vladikavkaz", "SA-15 Vladikavkaz", 2, "SA-15"),

        (OK.Depot, Side.Blue, "Senaki", "Forward Depot Senaki", 3, ""),
        (OK.Depot, Side.Blue, "Kaspi", "Forward Depot Kaspi", 3, ""),
        (OK.Depot, Side.Blue, "Dusheti", "Depot Dusheti", 2, ""),
        (OK.Depot, Side.Blue, "Kutaisi", "Army Depot Kutaisi", 4, ""),
        (OK.Depot, Side.Blue, "Tbilisi", "Army Depot Tbilisi", 4, ""),
        (OK.Factory, Side.Blue, "Rustavi", "Steel Works Rustavi", 4, ""),
        (OK.Factory, Side.Blue, "Zestafoni", "Plant Zestafoni", 3, ""),
        (OK.Factory, Side.Blue, "Tbilisi", "Aircraft Plant Tbilisi", 3, ""),
        (OK.Port, Side.Blue, "Poti", "Port Poti", 4, ""),
        (OK.Port, Side.Blue, "Batumi", "Port Batumi", 4, ""),
        (OK.Cp, Side.Blue, "Tbilisi", "General Staff Tbilisi", 5, ""),
        (OK.Cp, Side.Blue, "Kutaisi", "HQ Command West", 4, ""),
        (OK.Ewr, Side.Blue, "Kutaisi", "Radar Kutaisi", 3, "EWR-BL"),
        (OK.Ewr, Side.Blue, "Tbilisi", "Radar Tbilisi", 3, "EWR-BL"),
        (OK.Ewr, Side.Blue, "Senaki", "Radar Senaki", 2, "EWR-BL"),
        (OK.Sam, Side.Blue, "Tbilisi", "Patriot Tbilisi", 5, "Patriot"),
        (OK.Sam, Side.Blue, "Kutaisi", "Patriot Kutaisi", 5, "Patriot"),
        (OK.Sam, Side.Blue, "Samtredia", "Hawk Samtredia", 3, "Hawk"),
        (OK.Sam, Side.Blue, "Batumi", "Hawk Batumi", 3, "Hawk"),
        (OK.Sam, Side.Blue, "Kaspi", "NASAMS Kaspi", 3, "NASAMS"),
    ];

    // Brücken (Lat, Lon): hängen an der nächsten Kante des Straßennetzes
    static readonly (string Name, double Lat, double Lon)[] Bridges =
    [
        ("Inguri Bridge Rukhi", 42.552, 41.806), ("Kodori Bridge", 42.832, 41.180), ("Gumista Bridge Sukhumi", 43.030, 40.960),
        ("Psou Bridge", 43.385, 40.005), ("Rioni Bridge Poti", 42.150, 41.690), ("Rioni Bridge Samtredia", 42.165, 42.350),
        ("Kura Bridge Gori", 41.980, 44.100), ("Aragvi Bridge Mtskheta", 41.845, 44.715), ("Liakhvi Bridge", 42.150, 43.990),
        ("Roki Tunnel", 42.640, 44.000), ("Darial Bridge", 42.760, 44.630), ("Tskhenis Bridge", 42.230, 42.540),
    ];

    // Staffeln: Seite, Land, Typ, Name, Platz-Id, Anzahl, Rollen, Spielbar
    static readonly (Side S, string Country, string Type, string Name, int Base, int N, string Roles)[] Squadrons =
    [
        (Side.Blue, "USA", "FA-18C_hornet", "VFA-87 Golden Warriors", 25, 12, "CAP,Escort,Sweep,Strike,SEAD,CAS,BAI,Interdiction,OCA"),
        (Side.Blue, "USA", "F-14B", "VF-103 Jolly Rogers", 22, 10, "CAP,Escort,Sweep,Strike"),
        (Side.Blue, "USA", "F-15ESE", "336th FS Rocketeers", 31, 12, "Strike,Interdiction,OCA,BAI,CAS,Escort"),
        (Side.Blue, "USA", "F-16C_50", "480th FS Warhawks", 23, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "USA", "F-15C", "493rd FS Grim Reapers", 29, 12, "CAP,Sweep,Escort"),
        (Side.Blue, "USA", "A-10C_2", "74th FS Flying Tigers", 24, 10, "CAS,BAI,Interdiction"),
        (Side.Blue, "Georgia", "Su-25T", "Georgian Air Force 1st Squadron", 23, 8, "CAS,BAI"),
        (Side.Blue, "USA", "E-3A", "961st AACS", 29, 2, "AWACS"),
        (Side.Blue, "USA", "KC135MPRS", "909th ARS (Basket)", 30, 3, "Tanker"),
        (Side.Blue, "USA", "KC-135", "909th ARS (Boom)", 30, 3, "Tanker"),

        (Side.Red, "Russia", "Su-27", "3rd Fighter Aviation Regiment", 21, 12, "CAP,Sweep,Escort"),
        (Side.Red, "Russia", "MiG-29S", "31st Fighter Aviation Regiment", 18, 12, "CAP,Escort,Sweep"),
        (Side.Red, "Russia", "Su-30", "3624th Air Base", 15, 12, "Sweep,CAP,Strike,SEAD"),
        (Side.Red, "Russia", "MiG-31", "Interceptor Squadron Mozdok", 28, 8, "CAP"),
        (Side.Red, "Russia", "Su-24M", "559th Bomber Regiment", 16, 10, "Strike,Interdiction,OCA,SEAD"),
        (Side.Red, "Russia", "Su-25", "368th Assault Aviation Regiment", 27, 12, "CAS,BAI"),
        (Side.Red, "Russia", "Su-25", "Assault Squadron Sukhumi", 20, 8, "CAS,BAI"),
        (Side.Red, "Russia", "Su-34", "47th Bomber Regiment", 26, 12, "Strike,Interdiction,OCA,BAI"),
        (Side.Red, "Russia", "Tu-22M3", "52nd Bomber Regiment", 28, 6, "Strike"),
        (Side.Red, "Russia", "A-50", "A-50 Squadron", 26, 2, "AWACS"),

        (Side.Blue, "USA", "AH-64D_BLK_II", "1-227th ARB Apache", 23, 8, "CAS,BAI"),
        (Side.Blue, "USA", "AH-64D_BLK_II", "1-227th ARB Apache (B Company)", 30, 6, "CAS,BAI"),
        (Side.Blue, "USA", "UH-1H", "Dustoff Kutaisi (CSAR)", 25, 6, "CSAR"),
        (Side.Red, "Russia", "Mi-24P", "55th Helicopter Regiment", 27, 8, "CAS,BAI"),
        (Side.Red, "Russia", "Mi-24P", "Helicopter Squadron Gudauta", 21, 6, "CAS,BAI"),
        (Side.Red, "Russia", "Mi-8MT", "Rescue Squadron Gudauta (CSAR)", 21, 4, "CSAR"),

        (Side.Blue, "USA", "C-130J-30", "37th Airlift Squadron", 22, 6, "Transport"),
        (Side.Blue, "USA", "C-17A", "62nd Airlift Wing", 29, 3, "Transport"),
        (Side.Red, "Russia", "IL-76MD", "235th Transport Aviation Regiment", 26, 4, "Transport"),
        (Side.Red, "Russia", "An-26B", "Transport Squadron Maykop", 16, 4, "Transport"),
    ];

    public static readonly string[] UsNames = ["Miller", "Brooks", "Hayes", "Carter", "Walsh", "Reed", "Foster", "Bennett", "Hughes", "Price", "Coleman", "Ward", "Morgan", "Bell", "Cooper", "Russell", "Griffin", "Diaz", "Kelly", "Sanders", "Murphy", "Ross", "Perry", "Long", "Powell", "Jenkins", "Barnes", "Fisher", "Henderson", "Graham"];
    public static readonly string[] UsFirst = ["J.", "M.", "D.", "S.", "R.", "K.", "T.", "A.", "C.", "B."];
    static readonly string[] GeoNames = ["Beridze", "Kapanadze", "Gelashvili", "Maisuradze", "Giorgadze", "Lomidze", "Tsiklauri", "Bolkvadze", "Nikoladze", "Khutsishvili"];
    public static readonly string[] RedNames = ["Ivanov", "Smirnov", "Kuznetsov", "Popov", "Vasilyev", "Petrov", "Sokolov", "Mikhailov", "Novikov", "Fedorov", "Morozov", "Volkov", "Alekseev", "Lebedev", "Semenov", "Egorov", "Pavlov", "Kozlov", "Stepanov", "Nikolaev"];

    /// Wählbare Träger (DCS-Typ): Supercarrier-Modul bzw. frei
    public static readonly Dictionary<string, (string Name, int Tacan, string Cs)> Carriers = new()
    {
        ["CVN_75"] = ("CVN-75 Harry S. Truman", 75, "TRU"), ["Stennis"] = ("CVN-74 John C. Stennis", 74, "STN"),
    };

    public static World Create(Theater th, int seed = 1, string id = "kaukasus", string carrier = "", Setup? custom = null, TimeSpan? at = null)
    {
        var set = custom ?? Get(id);
        Geo.Use(th.Map);
        var w = new World { Th = th, S = set, Scen = set.Id, Map = set.Map, Name = set.Name, Seed = seed, RngState = unchecked(0x2545F4914F6CDD1DL ^ (seed * (long)0x9E3779B97F4A7C15UL)) };
        if (at is { } t) w.Time = w.Start = w.Start.Date + t;                // Uhrzeit des Kriegsbeginns aus dem Startdialog
        BuildGraph(w);
        BuildAxes(w);
        BuildObjects(w);
        AddDecoys(w);
        Hold(w);
        PrewarIntel(w);
        AddNavy(w);
        AddTels(w);
        w.Esc = set.Axes.Length == 0 ? 1 : 0;                           // reiner Luft-/Seekrieg: Industrie gleich frei
        Upgrade(w);
        BuildUnits(w);
        if (Carriers.ContainsKey(carrier) && set.Cv.Lat != 0)            // Träger: Platz auf See, kein Ziel (Wert 0), Versorgung über Versorgungsschiffe
        {
            var (cx, cz) = Geo.ToXZ(set.Cv.Lat, set.Cv.Lon);
            w.Objs.Add(new Obj { Id = w.NewId(), Kind = OK.Airbase, Name = Carriers[carrier].Name, Side = Side.Blue, X = cx, Z = cz, BaseId = -1, Sys = carrier, Stock = 2000, Fuel = 2000, Cap = 2000 });
        }
        BuildAir(w);
        Sim.Snapshot(w);
        w.Log(Side.None, "start", set.Start);
        return w;
    }

    static void BuildGraph(World w)
    {
        var th = w.Th;
        var keys = w.S.Axes.SelectMany(a => a.Keys).Concat(w.S.Objs.Select(o => o.At)).Concat(w.S.Passes.SelectMany(p => new[] { p.A, p.B }))
            .Concat(w.S.Oob.Select(o => o.At)).Concat(w.S.Reinf.Select(r => r.At)).Concat(w.S.Decoys.Select(d => d.At)).ToHashSet();
        var cand = th.Towns.Where(t => w.S.InWar(t.X, t.Z) && (keys.Contains(t.Name) || !th.Water(t.X, t.Z)) && Owner(w, t.X, t.Z) != Side.None)
            .OrderByDescending(t => keys.Contains(t.Name) ? 1e9 : t.Light).ToList();
        foreach (var t in cand)
        {
            if (w.Nodes.Any(n => n.Name == t.Name && keys.Contains(t.Name))) continue;
            if (!keys.Contains(t.Name) && w.Nodes.Any(n => Geo.Dist(n.X, n.Z, t.X, t.Z) < 6500)) continue;
            w.Nodes.Add(new Node { Id = w.Nodes.Count, Name = t.Name, X = t.X, Z = t.Z, Size = t.Light, Owner = Owner(w, t.X, t.Z) });
        }
        var seen = new HashSet<(int, int)>();
        foreach (var n in w.Nodes)
            foreach (var m in w.Nodes.Where(m => m != n).OrderBy(m => Geo.Dist(n.X, n.Z, m.X, m.Z)).Take(5))
            {
                double d = Geo.Dist(n.X, n.Z, m.X, m.Z);
                if (d > 32000 || !seen.Add((Math.Min(n.Id, m.Id), Math.Max(n.Id, m.Id)))) continue;
                if (Terrain(th, n, m) is { } cost) w.Edges.Add(new Edge { Id = w.Edges.Count, A = n.Id, B = m.Id, Len = d, Cost = cost });
            }
        foreach (var (a, b, name) in w.S.Passes)
        {
            var na = w.Nodes.First(n => n.Name == a); var nb = w.Nodes.First(n => n.Name == b);
            double d = Geo.Dist(na.X, na.Z, nb.X, nb.Z);
            w.Edges.Add(new Edge { Id = w.Edges.Count, A = na.Id, B = nb.Id, Len = d * 1.3, Cost = d * 3, Pass = true });
        }
        w.Link();
        // Zusammenhang: getrennte Teile an der kürzesten passierbaren Stelle verbinden
        while (true)
        {
            var comp = Components(w);
            if (comp.Distinct().Count() <= 1) break;
            var main = comp.GroupBy(c => c).MaxBy(g => g.Count())!.Key;
            var best = (d: double.MaxValue, a: -1, b: -1);
            foreach (var n in w.Nodes.Where(n => comp[n.Id] != main))
                foreach (var m in w.Nodes.Where(m => comp[m.Id] == main))
                {
                    double d = Geo.Dist(n.X, n.Z, m.X, m.Z);
                    if (d < best.d) best = (d, n.Id, m.Id);
                }
            var (na2, nb2) = (w.Nodes[best.a], w.Nodes[best.b]);
            w.Edges.Add(new Edge { Id = w.Edges.Count, A = na2.Id, B = nb2.Id, Len = best.d, Cost = (Terrain(th, na2, nb2) ?? best.d * 4) });
            w.Link();
        }
    }

    /// Bewegungskosten einer Kante (m, gewichtet nach Steigung) oder null, wenn unpassierbar (Gipfel/Meer).
    static double? Terrain(Theater th, Node a, Node b)
    {
        double d = Geo.Dist(a.X, a.Z, b.X, b.Z), climb = 0, prev = th.Height(a.X, a.Z);
        int n = Math.Max(2, (int)(d / 700)), water = 0;
        for (int i = 1; i <= n; i++)
        {
            double f = (double)i / n, x = a.X + (b.X - a.X) * f, z = a.Z + (b.Z - a.Z) * f;
            if (th.Clipped(x, z)) return null;
            double h = th.Height(x, z);
            if (h < 1 && th.Sea(x, z)) water++;
            climb += Math.Abs(h - prev);
            prev = h;
        }
        if (water > n * 0.25) return null;
        return d * (1 + climb / d * 12);
    }

    static int[] Components(World w)
    {
        var c = Enumerable.Repeat(-1, w.Nodes.Count).ToArray();
        int k = 0;
        foreach (var s in w.Nodes)
        {
            if (c[s.Id] >= 0) continue;
            var st = new Stack<int>([s.Id]);
            c[s.Id] = k;
            while (st.Count > 0)
            {
                int v = st.Pop();
                foreach (var e in w.Nodes[v].Adj)
                {
                    int o = w.Edges[e].Other(v);
                    if (c[o] < 0) { c[o] = k; st.Push(o); }
                }
            }
            k++;
        }
        return c;
    }

    public static Node Town(World w, string name) => Find(w, name) ?? throw new Exception("Town missing from the road network: " + name);
    public static Node? Find(World w, string name) => w.Nodes.FirstOrDefault(n => n.Name == name);   // Spielstand einer älteren Auto-Lage: Orte der neu erzeugten Lage fehlen evtl.

    static void BuildAxes(World w)
    {
        foreach (var (name, push, keys) in w.S.Axes)
            w.Axes.Add(new Axis { Id = w.Axes.Count, Name = name, Push = push, Keys = keys.Select(k => Town(w, k).Id).ToList() });
        var all = w.Axes.SelectMany(a => a.Keys).Distinct().ToList();
        foreach (var n in all.Count == 0 ? [] : w.Nodes)
        {
            var k = all.MinBy(id => Geo.Dist(n.X, n.Z, w.Nodes[id].X, w.Nodes[id].Z));
            if (Geo.Dist(n.X, n.Z, w.Nodes[k].X, w.Nodes[k].Z) < 28000) n.Key = k;
        }
        foreach (var id in all) w.Nodes[id].Key = id;
    }

    public static (double X, double Z) Near(World w, double x, double z, double dist)
    {
        for (int i = 0; i < 20; i++)
        {
            double a = w.Rnd() * Math.PI * 2, d = dist * (0.6 + 0.4 * w.Rnd());
            double nx = x + Math.Cos(a) * d, nz = z + Math.Sin(a) * d;
            if (!w.Th.Water(nx, nz) && !w.Th.Clipped(nx, nz) && w.Objs.All(o => Geo.Dist(o.X, o.Z, nx, nz) > 1500)) return (nx, nz);
        }
        return (x + dist, z);
    }

    // Attrappen: für den Gegner Flugabwehr wie jede andere (Radarwarner, Kartusche, Ziel), schießen aber nie. Aufklärungsflüge entlarven sie.
    static readonly (Side S, string At, string Name, string Sys)[] Decoys =
        [(Side.Red, "Sukhumi", "SA-11 Sukhumi", "SA-11"), (Side.Red, "Gagra", "SA-6 Gagra", "SA-6"), (Side.Blue, "Kutaisi", "Hawk Kutaisi", "Hawk"), (Side.Blue, "Khashuri", "NASAMS Khashuri", "NASAMS")];
    static void AddDecoys(World w)
    {
        foreach (var (s, at, name, sys) in w.S.Decoys)
        {
            var t = Town(w, at);
            var (x, z) = Near(w, t.X, t.Z, 6000);
            var sam = Sams[sys];
            w.Objs.Add(new Obj { Id = w.NewId(), Kind = OK.Sam, Name = name, Side = s, X = x, Z = z, Node = t.Id, Value = 1, Sys = sys, Decoy = true, Range = sam.Km * 1000, MaxAlt = sam.Alt, Missiles = sam.Missiles });
        }
    }

    /// Vorkriegsaufklärung: die Friedensstellungen der Flugabwehr (auch Attrappen) kennt der Gegner. Wer sie kennt, zwingt sie zum Stellungswechsel (Sim) –
    /// ab dann zeigt die Sichtung ins Leere, bis Radarwarner, Aufklärung oder BDA sie erneuern.
    static void PrewarIntel(World w)
    {
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Sam && o.Health > 0))
            w.Intel(o.Side.Enemy()).TryAdd($"O{o.Id}", new Sight { X = o.X, Z = o.Z, T = w.Time, Src = "Pre-war intel", Str = o.Health });
    }

    /// Startlage nach ersten Kämpfen: gehaltene Orte wechseln samt Umland und Zielen die Seite; die Flugabwehr des Verlierers dort ist weg.
    static void Hold(World w)
    {
        var ids = new HashSet<int>();
        foreach (var (at, s) in w.S.Held)
        {
            var t = Town(w, at);
            foreach (var n in w.Nodes.Where(n => n.Id == t.Id || n.Key == t.Id)) { n.Owner = s; ids.Add(n.Id); }
        }
        w.Objs.RemoveAll(o => ids.Contains(o.Node) && o.Kind is OK.Sam or OK.Ewr && o.Side != w.Nodes[o.Node].Owner);
        foreach (var o in w.Objs)
            if (o.Kind == OK.Bridge) { var e = w.Edges[o.Edge]; if (w.Nodes[e.A].Owner == w.Nodes[e.B].Owner) o.Side = w.Nodes[e.A].Owner; }
            else if (ids.Contains(o.Node)) o.Side = w.Nodes[o.Node].Owner;
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Sam && o.Range <= 40000))   // Ring reicht über die neue Front: Batterie hat sich nach hinten abgesetzt
        {
            double Gap(double x, double z) => w.Nodes.Where(n => n.Owner == o.Side.Enemy()).Min(n => Geo.Dist(n.X, n.Z, x, z));
            if (ids.Count == 0 || Gap(o.X, o.Z) >= o.Range) continue;
            if (w.Nodes.Where(n => n.Owner == o.Side && Gap(n.X, n.Z) > o.Range + 3000).MinBy(n => Geo.Dist(n.X, n.Z, o.X, o.Z)) is { } to)
                ((o.X, o.Z), o.Node) = (Near(w, to.X, to.Z, 2000), to.Id);
        }
    }

    /// Raketenwerfer (Scud/MLRS): stehen verteilt im Raum, schießen bevorzugt nachts, wechseln nach jedem Schuss die Stellung (Sim.Launchers).
    static void AddTels(World w)
    {
        foreach (var (s, at, n, sys) in w.S.Tels)
        {
            var t = Find(w, at);
            var tw = t == null ? w.Th.Towns.FirstOrDefault(x => x.Name == at) : null;
            if (t == null && tw == null || t != null && t.Owner != s) continue;
            for (int i = 0; i < n; i++)
            {
                var (x, z) = Near(w, t?.X ?? tw!.X, t?.Z ?? tw!.Z, 10000);
                w.Objs.Add(new Obj { Id = w.NewId(), Kind = OK.Tel, Name = $"{sys} launcher {at}-{i + 1}", Side = s, X = x, Z = z, Node = t?.Id ?? -1, Value = 4, Sys = sys, Missiles = 6 });
            }
        }
    }

    static void BuildObjects(World w)
    {
        foreach (var b in w.Th.Bases.Where(b => !w.S.Neutral.Contains(b.Id)))
        {
            var side = w.S.BlueBases.Contains(b.Id) ? Side.Blue : Side.Red;
            var node = w.S.InWar(b.X, b.Z) ? w.Nodes.MinBy(n => Geo.Dist(n.X, n.Z, b.X, b.Z)) : null;
            w.Objs.Add(new Obj { Id = w.NewId(), Kind = OK.Airbase, Name = b.Name, Side = side, X = b.X, Z = b.Z, BaseId = b.Id, Node = node?.Id ?? -1, Value = 4, Stock = 600, Fuel = 1200, Cap = 2000 });
        }
        foreach (var (k, s, at, name, value, sys) in w.S.Objs)
        {
            var t = w.Nodes.FirstOrDefault(n => n.Name == at);                // Hinterland (nicht im Straßennetz): Ziel ohne Bodenanschluss
            var tw = t == null ? w.Th.Towns.FirstOrDefault(n => n.Name == at) ?? throw new Exception("Town missing on the map: " + at) : null;
            var (x, z) = Near(w, t?.X ?? tw!.X, t?.Z ?? tw!.Z, t == null ? 1500 : k == OK.Sam ? 6000 : k == OK.Ewr ? 4000 : 2500);
            if (t == null && w.Th.Water(x, z)) (x, z) = (tw!.X, tw.Z);       // kleine Inseln
            var o = new Obj { Id = w.NewId(), Kind = k, Name = name, Side = s, X = x, Z = z, Node = t?.Id ?? -1, Value = value, Sys = sys };
            if (k == OK.Depot) { o.Cap = name.StartsWith("Army") ? 4000 : 2000; o.Stock = o.Fuel = o.Cap * 0.6; }
            if (k == OK.Factory) o.Output = FactoryOutput;
            if (Sams.TryGetValue(sys, out var sam)) { o.Range = k == OK.Ewr ? 250000 : sam.Km * 1000; o.MaxAlt = k == OK.Ewr ? 30000 : sam.Alt; o.Missiles = sam.Missiles; }
            w.Objs.Add(o);
        }
        foreach (var (name, lat, lon) in w.S.Bridges)
        {
            var (x, z) = Geo.ToXZ(lat, lon);
            var e = w.Edges.Where(e => e.Bridge < 0).MinBy(e => Geo.SegDist(new P(x, z), new P(w.Nodes[e.A].X, w.Nodes[e.A].Z), new P(w.Nodes[e.B].X, w.Nodes[e.B].Z)))!;
            var side = w.Nodes[e.A].Owner == w.Nodes[e.B].Owner ? w.Nodes[e.A].Owner : Owner(w, x, z);
            var o = new Obj { Id = w.NewId(), Kind = OK.Bridge, Name = name, Side = side, X = x, Z = z, Edge = e.Id, Node = e.A, Value = 3 };
            e.Bridge = o.Id;
            w.Objs.Add(o);
        }
    }

    // Verstärkungen: Stunde nach Kriegsbeginn, Seite, Art, Ort, Vorlage, Name
    static readonly (int H, Side S, UK K, string At, string Tpl, string Name)[] Reinforcements =
    [
        (18, Side.Red, UK.Armor, "Sochi", "RU-Armor", "20th Guards Motor Rifle Div Tank Bn"),
        (24, Side.Red, UK.Mech, "Vladikavkaz", "RU-Mech", "19th Div 429th Motor Rifle Rgt"),
        (36, Side.Red, UK.Mech, "Sochi", "RU-Mech", "7th Air Assault Div 108th Rgt"),
        (48, Side.Red, UK.Armor, "Vladikavkaz", "RU-Armor", "58th Army 2nd Tank Bn"),
        (60, Side.Red, UK.Art, "Sochi", "RU-Art", "20th Div Artillery Bn"),
        (72, Side.Red, UK.Mech, "Vladikavkaz", "RU-Mech", "76th Airborne Div 104th Rgt"),
        (30, Side.Blue, UK.Mech, "Tbilisi", "GE-Mech", "5th Inf Brigade 51st Bn"),
        (54, Side.Blue, UK.Armor, "Batumi", "US-Armor", "3/69 Armor (US) Tank Bn"),
        (78, Side.Blue, UK.Mech, "Batumi", "US-Mech", "2/7 Inf (US) Bradley Bn"),
        (96, Side.Blue, UK.Art, "Kutaisi", "US-Art", "1/41 FA (US) Artillery Bn"),
        (120, Side.Blue, UK.Motor, "Tbilisi", "US-Motor", "1/2 SCR (US) Stryker Bn"),
        (-6, Side.Blue, UK.Armor, "Batumi", "TR-Armor", "Turkish 1st Armored Bde Tank Bn"),         // H < 0: nach dem Kriegseintritt (Setup.Join)
        (-18, Side.Blue, UK.Mech, "Batumi", "TR-Mech", "Turkish 14th Mech Bde 1st Bn"),
        (-30, Side.Blue, UK.Art, "Batumi", "TR-Art", "Turkish 14th Mech Bde Artillery Bn"),
        (-8, Side.Red, UK.Armor, "Vladikavkaz", "RU-Armor", "4th Guards Tank Div Tank Bn"),
        (-20, Side.Red, UK.Mech, "Sochi", "RU-Mech", "2nd Guards Motor Rifle Div 1st Bn"),
    ];

    public static Unit NewUnit(World w, Side s, UK k, int ax, string role, Node n, string tpl, string name)
    {
        var u = new Unit { Id = w.NewId(), Side = s, Kind = k, Axis = ax, Role = role, Node = n.Id, Template = tpl, Name = name, Veh = Templates[tpl].Length, Exp = s == Side.Red ? 0.55 : 0.5, Dig = role == "front" ? 0.3 : 0, X = n.X, Z = n.Z };
        u.Order = role == "reserve" ? "Reserve" : "Halten";
        w.Units.Add(u);
        return u;
    }

    static void BuildUnits(World w)
    {
        foreach (var (s, k, ax, role, at, tpl, name) in w.S.Oob)
        {
            var n = Town(w, at);
            var f = w.S.Held.Length > 0 && role == "front" && ax >= 0 ? Sim.Front(w, w.Axes[ax]) : null;
            if (n.Owner != s)                                               // Ort schon verloren (Startlage): Front-Verbände an die neue Front, sonst nächster eigener Ort
                n = f is { } fb ? w.Nodes[s == Side.Red ? fb.R : fb.B] : w.Nodes.Where(m => m.Owner == s).MinBy(m => Geo.Dist(m.X, m.Z, n.X, n.Z))!;
            else if (f is { } fr && s == Side.Red && k != UK.Art) n = w.Nodes[fr.R];   // Angreifer ist schon vorgerückt
            NewUnit(w, s, k, ax, role, n, tpl, name);
        }
    }

    static void BuildAir(World w) { foreach (var d in w.S.Sqns) AddSqn(w, d); }

    /// Schwierigkeit wie die Kampagnenoptionen in BMS, bezogen auf den Gegner des Spielers: Staffelstärke, Bodenstärke (über 100 %: die eigenen
    /// Verbände sind schwächer, mehr als voll geht nicht), Flugabwehr (unter 100 %: weniger Stellungen, darüber: mehr Flugkörper), Pilotenkönnen, Nachschub.
    public static void Difficulty(World w, int air, int ground, int sam, int skill, int supply)
    {
        var e = w.Player.Enemy();
        (w.AirPct, w.GroundPct, w.SamPct, w.SkillAdj, w.SupplyPct) = (air, ground, sam, skill, supply);
        foreach (var q in w.Sqns.Where(q => q.Side == e && q.Roles.Any(r => r is not ("AWACS" or "Tanker" or "Transport" or "CSAR"))))
        {
            q.Ac = q.Size = Math.Max(2, (int)Math.Round(q.Size * air / 100.0));
            while (q.Pilots.Count < q.Ac + 2) q.Pilots.Add(new Pilot { Name = PilotName(w, e, q.Country), Skill = 1 + (int)(w.Rnd() * 2.2) });
        }
        foreach (var p in w.Sqns.Where(q => q.Side == e).SelectMany(q => q.Pilots)) p.Skill = Math.Clamp(p.Skill + skill, 0, 3);
        foreach (var u in w.Units) u.Str = Math.Clamp(u.Side == e ? u.Str * ground / 100.0 : ground > 100 ? u.Str * 100.0 / ground : u.Str, 0.2, 1);
        var sams = w.Objs.Where(o => o.Side == e && o.Kind == OK.Sam && !o.Decoy).OrderBy(o => o.Value).ThenBy(o => o.Id).ToList();
        if (sam < 100)
            foreach (var o in sams.Take((int)Math.Round(sams.Count * (100 - sam) / 100.0))) { w.Objs.Remove(o); w.Intel(w.Player).Remove($"O{o.Id}"); }
        else foreach (var o in sams) o.Missiles = (int)Math.Round(o.Missiles * sam / 100.0);
        w.History.Clear(); Sim.Snapshot(w);
    }

    public static string PilotName(World w, Side s, string country)
    {
        var last = w.S.Names.TryGetValue(country, out var l) ? w.Pick(l) : s == Side.Red ? w.Pick(w.S.RedNames) : country == w.S.BlueC ? w.Pick(w.S.HostNames) : w.Pick(w.S.BlueNames);
        return (s == Side.Red ? "" : w.Pick(w.S.BlueFirst) + " ") + last;
    }

    static void AddSqn(World w, (Side S, string Country, string Type, string Name, int Base, int N, string Roles) d)
    {
        var (s, country, type, name, baseId, n, roles) = d;
        var b = s == Side.Blue && (type == "FA-18C_hornet" || type.StartsWith("F-14")) && w.Objs.FirstOrDefault(o => o.Kind == OK.Airbase && o.BaseId < 0) is { } cv ? cv   // Marine auf den Träger
            : w.Base(baseId) ?? Home(w, s, type, roles);
        if (b.Side != s) b = w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == s && o.BaseId > 0).MinBy(o => Geo.Dist(o.X, o.Z, b.X, b.Z)) ?? b;   // Platz schon verloren (Startlage)
        var q = new Sqn { Id = w.NewId(), Side = s, Country = country, Type = type, Name = name, Base = b.Id, Ac = n, Size = n, Roles = roles.Split(','), Cs = s == Side.Blue ? 1 + w.Sqns.Count(x => x.Side == Side.Blue) : 0 };
        for (int i = 0; i < n + 2; i++)
        {
            q.Pilots.Add(new Pilot { Name = PilotName(w, s, country), Skill = Math.Clamp(1 + (int)(w.Rnd() * 2.2) + (s != w.Player ? w.SkillAdj : 0), 0, 3) });
        }
        w.Sqns.Add(q);
    }

    /// Platz für eine Staffel ohne Vorgabe (beim Start hinzugefügt): bei einer eigenen Staffel gleicher Art mit gemeinsamer Rolle, sonst der Platz mit den wenigsten Staffeln.
    static Obj Home(World w, Side s, string type, string roles) =>
        w.Sqns.Where(q => q.Side == s && Air.Helo(q.Type) == Air.Helo(type) && q.Roles.Intersect(roles.Split(',')).Any())
            .Select(q => w.Objs.FirstOrDefault(o => o.Id == q.Base)).FirstOrDefault(o => o is { BaseId: > 0 } && o.Side == s)
        ?? w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == s && o.BaseId > 0).MinBy(o => w.Sqns.Count(q => q.Base == o.Id))!;

    /// Staffel aus dem Startdialog: bekannte (I) behalten Platz und Land, ein neues Muster fliegt seine üblichen Rollen, neue Staffeln suchen sich einen Platz.
    public static (Side, string, string, string, int, int, string) Sqn(Setup set, System.Text.Json.Nodes.JsonNode x)
    {
        var s = (string?)x["Side"] == "Red" ? Side.Red : Side.Blue;
        var type = (string?)x["Type"] ?? "";
        if (!Air.Perfs.TryGetValue(type, out var pf)) throw new ArgumentException($"Unknown aircraft {type}");
        int i = (int?)x["I"] ?? -1;
        (Side S, string Country, string Type, string Name, int Base, int N, string Roles) o = i >= 0 && i < set.Sqns.Length && set.Sqns[i].S == s ? set.Sqns[i]
            : (s, set.Sqns.Where(q => q.S == s).GroupBy(q => q.Country).MaxBy(g => g.Count())?.Key ?? (s == Side.Red ? "Russia" : "USA"), type, "", 0, 0, pf.Roles);
        var name = ((string?)x["Name"])?.Trim() is { Length: > 0 } nm ? nm : $"{type} Squadron";
        return (s, o.Country, type, name, o.Base, Math.Clamp((int?)x["N"] ?? 8, 1, 24), o.Type == type || pf.Roles == "" ? o.Roles : pf.Roles);
    }

    /// Neue Staffel trifft ein: auf ihrem Platz, wenn er noch eigen und benutzbar ist, sonst auf dem eigenen Platz mit den meisten Staffeln
    public static void Arrive(World w, (int H, Side S, string Country, string Type, string Name, int Base, int N, string Roles) a)
    {
        var b = w.Base(a.Base) is { } b0 && b0.Side == a.S && b0.Health > 0.3 ? b0
            : w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == a.S && o.BaseId > 0 && o.Health > 0.3).MaxBy(o => w.Sqns.Count(q => q.Base == o.Id));
        if (b == null) return;
        int n = a.S != w.Player ? Math.Max(2, (int)Math.Round(a.N * w.AirPct / 100.0)) : a.N;
        AddSqn(w, (a.S, a.Country, a.Type, a.Name, b.BaseId, n, a.Roles));
        w.Log(a.S, "luft", $"Reinforcements: {a.Name} ({n} {a.Type}) arrives at {w.Base(w.Sqns[^1].Base)?.Name ?? b.Name} and joins the air war.", b.X, b.Z);
    }

    // Startlage Kaukasus (am Ende: braucht die Felder oben)
    public static readonly Setup Caucasus = new()
    {
        Id = "kaukasus", Map = "Caucasus", Name = "Caucasus", Cv = (41.85, 40.95),           // Träger: Schwarzes Meer westlich Batumi
        Text = "Red (Russia, Abkhazia, South Ossetia) attacks Georgia, NATO supports Blue. Five axes, supply by rail, road and sea.",
        BlueText = "Blue – Georgia + NATO", RedText = "Red – Russia",
        Start = "War in the Caucasus: Russian forces attack from Abkhazia and South Ossetia. NATO supports Georgia from the air.",
        RedC = "Russia", BlueC = "Georgia", BlueSite = "USA", RedIds = [0, 18, 19], BlueIds = [2, 16, 3],
        BullBlue = new(-284583, 685030), BullRed = new(-165163, 460902),      // Kutaissi bzw. Sotschi
        Owner = CaucasusOwner, InWar = (x, z) => x > -400000 && x < 20000 && z > 420000 && z < 965000,
        BlueBases = [22, 23, 24, 25, 29, 30, 31],
        Axes = AxesDef, Passes = Passes, Snow = [("Kazbegi", "Pasanauri"), ("Alagir", "Java")], SnowText = "Jvari Pass and Roki Tunnel", Rail = RailLines,
        Sea = [(Side.Blue, -300000, 470000, ""), (Side.Red, -170000, 360000, "")],    // Blau über das offene Schwarze Meer, Rot von Noworossijsk/Krim
        Gates = [(Side.Red, "Mineral'nye Vody", 250, 350), (Side.Red, "Golovinka", 150, 250), (Side.Blue, "Rustavi", 300, 450)],   // Bahn: Rot aus Rostow und Tuapse, Blau aus Aserbaidschan
        Cargo = port => port.Name.Contains("Batumi") ? (250, 550) : (400, 400),   // Batumi hat das Ölterminal
        Oob = Oob, Objs = ObjDef, Bridges = Bridges, Sqns = Squadrons, Decoys = Decoys, Reinf = Reinforcements,
        AirReinf = [(36, Side.Blue, "USA", "F-15C", "44th FS Vampire Bats", 29, 10, "CAP,Sweep,Escort"), (48, Side.Red, "Russia", "Su-34", "277th Bomber Regiment", 26, 12, "Strike,Interdiction,OCA,BAI"),
                    (60, Side.Blue, "USA", "F-16C_50", "555th FS Triple Nickel", 31, 12, "SEAD,Strike,CAP,BAI,CAS"), (72, Side.Red, "Russia", "Su-27", "790th Fighter Aviation Regiment", 15, 12, "CAP,Sweep,Escort"),
                    (-12, Side.Blue, "Turkey", "F-16C_50", "191st Filo Kobra", 22, 12, "CAP,Escort,Strike,SEAD,BAI,CAS"), (-12, Side.Red, "Russia", "Su-30", "120th Fighter Aviation Regiment", 15, 12, "Sweep,CAP,Strike,SEAD")],
        Tels = [(Side.Red, "Gudauta", 2, "Scud"), (Side.Red, "Alagir", 2, "Scud"), (Side.Blue, "Khashuri", 2, "MLRS")],
        Join = [(Side.Blue, "Turkey enters the war on the side of Georgia: armour lands at Batumi, F-16s join the air war."), (Side.Red, "Moscow commits its strategic reserves: fresh tank and motor rifle divisions roll south.")],
        Names = new() { ["Turkey"] = Syria.TurkishNames },
        Support = [(Side.Blue, "Magic", -352000, 760000, 9000, "AWACS"), (Side.Blue, "Texaco", -330000, 690000, 7000, "Tanker"), (Side.Blue, "Shell", -345000, 845000, 7600, "Tanker"), (Side.Red, "A-50", -105000, 640000, 9000, "AWACS")],
        Caps = [("West", 0, true, Side.None, 0, 0), ("East", 2, false, Side.None, 0, 0)],
        RedNames = RedNames, BlueNames = UsNames, BlueFirst = UsFirst, HostNames = GeoNames, Temp = 12,
    };

    // Weitere Startlagen wie in BMS: Gegenangriff nach den ersten Verlusten, eigene Offensive
    static readonly Setup CaucasusCounter = Caucasus with
    {
        Id = "kaukasus-counter", Name = "Caucasus: Counterstrike",
        Text = "Day three: Russia holds Zugdidi, Anaklia, Senaki and Gori. Georgia and NATO strike back before the line hardens.",
        Start = "Counterstrike in the Caucasus: Russian forces stand at Senaki and Gori. NATO air power must stop them and open the way back.",
        Held = [("Zugdidi", Side.Red), ("Anaklia", Side.Red), ("Senaki", Side.Red), ("Gori", Side.Red)],
        Axes = [.. AxesDef.Select(a => a.Name.StartsWith("West") || a.Name.StartsWith("East") ? a with { Push = Side.Blue } : a)],
        Reinf = [.. Reinforcements.Select(r => r.S == Side.Blue && r.H > 0 ? r with { H = r.H / 2 } : r)],
    };
    static readonly Setup CaucasusOffensive = Caucasus with
    {
        Id = "kaukasus-offensive", Name = "Caucasus: Liberation",
        Text = "Blue attacks: Georgia and NATO move to retake Abkhazia and South Ossetia. Russian reserves are days away.",
        Start = "Operation Liberation: Georgian and NATO forces cross into Abkhazia and South Ossetia.",
        Axes = [.. AxesDef.Select(a => a with { Push = Side.Blue })],
        Reinf = [.. Reinforcements.Select(r => r.H < 0 ? r : r.S == Side.Blue ? r with { H = r.H / 2 } : r with { H = r.H + 24 })],
    };
}
