using System.Text.Json;
using System.Text.Json.Serialization;

namespace DcsCampaign;

public enum Side { None = 0, Red = 1, Blue = 2 }

public class Node { public int Id; public string Name = ""; public double X, Z, Size; public Side Owner; public int Key = -1; [JsonIgnore] public List<int> Adj = []; }
public class Edge { public int Id, A, B, Bridge = -1; public double Len, Cost; public bool Pass, Rail, Snow; public int Other(int n) => n == A ? B : A; }   // Rail: Gleis (nur Güterzüge); Snow: Pass, den Schneefall sperrt

/// Hauptachse des Bodenkriegs: Schlüsselorte von tief Rot (Index 0) nach tief Blau.
/// Op: laufende Operation von OpSide – "" Ruhe, "Bereitstellung", "Angriff", "Konsolidierung"; OpSince = Beginn der Stufe.
public class Axis { public int Id; public string Name = ""; public List<int> Keys = []; public Side Push = Side.Red, OpSide; public DateTime Pause, OpSince; public string Op = ""; public bool Warned; }

public enum OK { Airbase, Depot, Factory, Port, Bridge, Ewr, Sam, Cp, Tel }   // Tel: Raketenwerfer (Scud/MLRS), fährt
public class Obj
{
    public int Id, Node = -1, BaseId, Edge = -1, Missiles;
    public OK Kind;
    public string Name = "", Sys = "";
    public Side Side;
    public double X, Z, Value = 1, Stock, Fuel, Cap, Output, Range, MaxAlt, Flow;   // Stock = Munition/Material t, Fuel = Sprit t (je bis Cap); Flow: Brücke, t Nachschub der letzten ~24 h
    public DateTime QuietUntil;
    public bool InDcs, Decoy, Exposed;          // Attrappe (Flugabwehr-Täuschung); vom Gegner als Attrappe erkannt
    public Dictionary<string, int>? Arms;       // Waffenbestand je Klasse (Flugplätze, Air.ArmClasses), null = noch nicht angelegt (Soll)
    public double[]? Parts;                     // Zustand je Teil (Scenario.Parts: Piste, Shelter, Tanklager …), null = alle wie Health
    double h = 1;

    /// Gesamtzustand = gewichtetes Mittel der Teile. Setzen (pauschaler Schaden, Instandsetzung, Eroberung) verteilt die Änderung gleichmäßig auf die Teile.
    public double Health
    {
        get
        {
            if (Parts is not { } p || !Scenario.Parts.TryGetValue(Kind, out var l) || l.Length != p.Length) return h;
            double s = 0;
            for (int i = 0; i < p.Length; i++) s += l[i].W * p[i];
            return s;
        }
        set
        {
            h = Math.Clamp(value, 0, 1);
            if (Parts is not { } p || !Scenario.Parts.TryGetValue(Kind, out var l) || l.Length != p.Length) return;
            for (int k = 0; k < 4; k++)
            {
                double d = h - Health, wf = 0;
                if (Math.Abs(d) < 1e-6) break;
                for (int i = 0; i < p.Length; i++) if (d < 0 ? p[i] > 0 : p[i] < 1) wf += l[i].W;
                if (wf <= 0) break;
                for (int i = 0; i < p.Length; i++) if (d < 0 ? p[i] > 0 : p[i] < 1) p[i] = Math.Clamp(p[i] + d / wf, 0, 1);
            }
        }
    }
    /// Zustand eines Teils; Ziele ohne diesen Teil: Gesamtzustand
    public double P(string part) => Scenario.Parts.TryGetValue(Kind, out var l) && Array.FindIndex(l, x => x.Name == part) is var i and >= 0 ? Parts?[i] ?? h : Health;
    /// Treffer auf einen Teil (dmg = Anteil dieses Teils), ein Zehntel streut auf die übrigen
    public void Hit(int part, double dmg)
    {
        if (!Scenario.Parts.TryGetValue(Kind, out var l)) { Health -= dmg; return; }
        Parts ??= Enumerable.Repeat(h, l.Length).ToArray();
        for (int i = 0; i < Parts.Length; i++) Parts[i] = Math.Clamp(Parts[i] - (i == part ? dmg : dmg * 0.1), 0, 1);
        h = Health;
    }
    /// Instandsetzung: Teil für Teil in der Reihenfolge von Scenario.Parts (Piste zuerst); rep = Anteil am Gesamtzustand
    public void Fix(double rep)
    {
        if (!Scenario.Parts.TryGetValue(Kind, out var l)) { Health = Math.Min(1, Health + rep); return; }
        Parts ??= Enumerable.Repeat(h, l.Length).ToArray();
        int i = Array.FindIndex(Parts, p => p < 1);
        if (i >= 0) Parts[i] = Math.Min(1, Parts[i] + rep / l[i].W);
        h = Health;
    }
}

public enum UK { Armor, Mech, Motor, Art, Shorad }
public class Unit
{
    public int Id, Axis = -1, Node, Next = -1, Veh, Dp = -1;   // Dp: versorgendes Depot, -1 = keins
    public string Name = "", Role = "front", Order = "Halten", Why = "", Template = "";
    public Side Side;
    public UK Kind;
    public double Str = 1, Supply = 1, Fuel = 1, Line = 1, Morale = 1, Exp = 0.5, Dig, Prog, X, Z;   // Supply = Munition, Fuel = Sprit (Anteil am eigenen Vorrat), Line = Leistung der Versorgungslinie
    public List<int> Path = [];
    public bool InDcs;
}

public class Pilot { public string Name = ""; public int Skill = 1, Sorties, Kills; public string Status = "aktiv"; public double Fatigue; }
public class Sqn
{
    public int Id, Base, Ac, Dmg, Lost, Cs, Size;
    public string Name = "", Type = "", Country = "";
    public Side Side;
    public string[] Roles = [];
    public List<Pilot> Pilots = [];
    public List<DateTime> Repair = [];
    public bool Player => Air.Flyable(Type);  // Muster mit Cockpit in DCS: jeder Flug davon ist übernehmbar
}

public class Wp { public string Name = "", Kind = "nav"; public double X, Z, Alt, Spd; public DateTime T; }
public class Flight
{
    public int Id, Pkg, Sqn, N, Alive, Target = -1, Leg, Missiles, Arm, Tacan;
    public Side Side;
    public string Type = "", Callsign = "", Task = "", Load = "", Status = "geplant", Why = "", Result = "", TargetKind = "", Start = "kalt";
    public List<Wp> Route = [];
    public DateTime Takeoff, Land, Tot;
    public double X, Z, Alt, Freq, Cargo;       // Cargo: geladene Tonnen (Lufttransport), halb Munition, halb Sprit
    public bool Player, InDcs, Done, Struck;
    public int Passes;
    public int Part = -1;                       // Zielpunkt: Teil des Ziels (Scenario.Parts), -1 = KI wählt
    public DateTime Busy;                       // Luftkampf-Pause
    public List<int> Pilots = [];
    public Dictionary<int, string>? Pyl;        // eigene Bestückung je Station (CLSID), null = Beladung Load
    public string Crew = "";                    // je Maschine P = Mensch, K = KI ("" = Maschine 1 Mensch); wirkt im Mehrspieler (Client-Plätze)
    public string Owner = "";                   // Mehrspieler: Mitspieler, der den Flug plant ("" = Host bzw. frei)
}
/// Ausrüstung des Spielers je Muster, gilt auch für den nächsten Flug. -1 = Standard (Muster bzw. Kartuschen-Vorlage).
public class Kit
{
    public int Chaff = -1, Flare = -1, Gun = 100, Fuel = 100, Hmd = 1, AltBaro = -1, AltRdr = -1;   // Hmd (F/A-18C): 0 ohne, 1 JHMCS, 2 Nachtsichtbrille
    public bool Ripple;                                     // F/A-18C: Raketen als Salve
    public string Tacan = "";                               // "44X", "" = Heimatplatz
    public Dictionary<string, double[]> Cmds = [];          // ALE-47 "MAN_1".."MAN_6": Düppel, Wiederholungen, Intervall s, Fackeln
    public Dictionary<string, Ch> Comm = [];                // eigene Funkkanäle "Gerät-Kanal" ("1-5")
}
public class Ch { public double F; public string N = ""; }
/// Bewertung eines Flugs nach der DCS-Mission: 0 Erfolg, 1 teilweise, 2 Fehlschlag; Log = Meldungen mit Rufname oder Ziel
public class Rating { public int Flight, Grade, Lost, Air, Ground; public bool Player; public string Callsign = "", Type = "", Task = "", Owner = "", Why = ""; public List<string> Log = []; }
/// Interne Werte (Status, Befehle, Startart …) bleiben deutsch – sie stehen so in den Spielständen; angezeigt wird englisch.
public static class En
{
    public static string Of(string v) => v switch
    {
        "geplant" => "planned",
        "in der Luft" => "airborne",
        "im Einsatz" => "on task",
        "Rückflug" => "RTB",
        "Abbruch" => "aborted",
        "gelandet" => "landed",
        "verloren" => "lost",
        "gestrichen" => "cancelled",
        "kalt" => "cold",
        "heiß" => "hot",
        "Startbahn" => "runway",
        "Luft" => "air start",
        "Angriff" => "attack",
        "Halten" => "hold",
        "Verteidigen" => "defend",
        "Rückzug" => "withdraw",
        "Reserve" => "reserve",
        "Auffrischen" => "refit",
        "Verlegen" => "redeploy",
        "Ausbruch" => "breakout",
        "Bereitstellung" => "staging",
        "Konsolidierung" => "consolidation",
        "aktiv" => "active",
        "vermisst" => "missing",
        "gefangen" => "captured",
        "tot" => "KIA",
        "verwundet" => "wounded",
        "gefallen" => "KIA", "ausgeschieden" => "retired",
        _ => v,
    };
}
/// Ausrüstungsprofile je Muster (Name → Kit) – liegen neben den Spielständen und gelten für alle Kampagnen; "Default" bekommt jede neue Kampagne.
public static class Profiles
{
    static string File => Path.Combine(Program.Dir, "profiles.json");
    static readonly System.Text.Json.JsonSerializerOptions O = new() { IncludeFields = true };
    public static readonly Dictionary<string, Dictionary<string, Kit>> All = Read();
    static Dictionary<string, Dictionary<string, Kit>> Read() { try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Kit>>>(System.IO.File.ReadAllText(File), O) ?? []; } catch { return []; } }
    public static void Write() => System.IO.File.WriteAllText(File, System.Text.Json.JsonSerializer.Serialize(All, O));
    public static Kit Copy(Kit k) => System.Text.Json.JsonSerializer.Deserialize<Kit>(System.Text.Json.JsonSerializer.Serialize(k, O), O)!;
}
public class Pkg { public int Id, Target = -1; public Side Side; public string Name = "", Task = "", TargetKind = "", Why = ""; public DateTime Tot; public List<int> Flights = []; }
/// Rail: Güterzug, Trucks = Waggons; Ship: Frachter von See (From = -1), Trucks = Laderäume. Tons = Munition, Fuel = Sprit.
/// Origin: Startknoten (Einspeisung von außerhalb hat kein From-Objekt). Reinf ≥ 0: Truppentransport (Index in w.S.Reinf).
public class Convoy { public int Id, From, To, Trucks, Trucks0, Leg, Origin = -1, Reinf = -1, Missiles, Ssm; public Side Side; public List<int> Path = []; public double Prog, Tons, Fuel, X, Z; public bool InDcs, Rail, Ship, Navy; public string Name = ""; public List<string> Types = []; }   // Navy: Kriegsschiffverband (Trucks = Schiffe, To = Heimathafen, Missiles/Ssm = Seezielflugkörper an Bord/Magazin)
public class Ev { public DateTime T; public Side Side; public string Kind = "", Text = ""; public double X, Z; }
public class Sight { public double X, Z, Str; public DateTime T; public string Src = ""; public bool Claim; public double[]? Parts; }   // Claim: Pilotenmeldung nach Angriff, unbestätigt bis Aufklärung (BDA)
public class Snap { public DateTime T; public double RedG, BlueG; public int RedA, BlueA, RedS, BlueS; }   // Bodenstärke (Kampfkraft), Maschinen, SAM-Stellungen
public class Wreck { public string Type = ""; public double X, Z, Hdg; public DateTime T; public Side Side; }
public class Downed { public int Id, Sqn, Pilot; public Side Side; public double X, Z; public DateTime T; }
/// Kommandeur einer Seite: Phase 1 Luftüberlegenheit, 2 SEAD, 3 Abriegelung, 4 Bodenoffensive. Set > 0 = vom Spieler festgelegt.
/// Prio je Angriffsart: 0 aus, 1 normal (fehlt = 1), 2 hoch; Focus = Schwerpunkt-Achse (-1 keine).
public class Cmd { public int Phase = 1, Set, Focus = -1; public string Why = ""; public DateTime Since, NextOp; public Dictionary<string, int> Prio = []; }
/// Wetter am Kriegsschauplatz. Front: Ost-Koordinate (DCS z) einer Wetterfront, die mit FrontKmh nach Osten zieht. Snow: Schnee auf den Pässen.
public class Weather { public double Cloud = 0.3, Base = 1800, WindDir = 270, Wind = 4, Temp = 14, Qnh = 760, Vis = 40000, FrontZ, FrontKmh, FrontStr, FrontKm; public bool Rain, Snow; public DateTime SnowUntil; }

public class World
{
    public int Version = 9, NextId = 1, Seed = 1;                // 2: Sprit/Munition getrennt, Einspeisung statt Gratis-Nachschub; 3: Attrappen; 4: SAM-Stellungen aus Vorkriegsaufklärung; 5: Hubschrauber; 6: Transporter; 7: Englisch; 8: Seekrieg; 9: Kriegsphasen, Raketenwerfer
    public DateTime Time = new(2026, 10, 12, 10, 0, 0), Start = new(2026, 10, 12, 10, 0, 0);   // Kriegsbeginn 10 Uhr morgens
    public double Speed = 0;
    public Side Player = Side.Blue;
    public string Name = "Caucasus";
    public string Map = "Caucasus", Scen = "kaukasus";                  // Karte (DCS-Theater) und Startlage
    public string Over = "";                    // Kriegsende (Text), "" = Krieg läuft
    public int Esc;                             // Eskalation (ROE): 0 nur Militär, 1 + Werke/Häfen, 2 + Regierung/Generalstab
    public int JoinRed = -1, JoinBlue = -1;     // Stunde, in der ein Verbündeter eintritt (-1 = noch nicht)
    public string Truce = "";                   // Waffenstillstandsangebot des Gegners, "" = keins
    public int Truces;
    public DateTime TruceAt;
    public bool Fog = true;
    public bool Mp;                             // Mission als Mehrspieler-Mission (eigener Server aus dem Spiel): Menschen auf Client-Plätzen
    public int Perf = 1;                        // Leistung des PCs: 0 schwach, 1 normal, 2 stark – wie viel gleichzeitig in DCS lebt
    public int AirPct = 100, GroundPct = 100, SamPct = 100, SupplyPct = 100, SkillAdj;   // Schwierigkeit wie in BMS (Gegner): Luft, Boden, Flugabwehr, Nachschub in %, Können seiner Piloten ±
    public List<Snap> History = [];             // Kräfteverlauf: Start und täglich 06 Uhr
    public Dictionary<int, Dictionary<string, double>> WhStart = [], WhLast = [], WhUsed = [];   // DCS-Platzlager je Platz-ID: Bestand ohne Startbedarf ("fuel" t, "Klasse@wsType"), letzte Meldung, schon gebuchte Starts
    public List<Node> Nodes = [];
    public List<Edge> Edges = [];
    public List<Axis> Axes = [];
    public List<Obj> Objs = [];
    public List<Unit> Units = [];
    public List<Sqn> Sqns = [];
    public List<Flight> Flights = [];
    public List<Pkg> Pkgs = [];
    public List<Convoy> Convoys = [];
    public List<Ev> Events = [];
    public SortedDictionary<string, Sight> IntelRed = new(), IntelBlue = new();   // sortiert: gleiche Reihenfolge nach Laden
    public Weather Wx = new();
    public Cmd CmdRed = new(), CmdBlue = new();
    public Cmd CmdOf(Side s) => s == Side.Red ? CmdRed : CmdBlue;
    public DateTime LastAto, LastHour, LastAuto;
    public string Mission = "";                 // aktive DCS-Mission (Token), "" = keine
    public DateTime MissionStart;
    public bool MissionLive;                    // DCS hat START gemeldet
    public string MissionFile = "";
    public long LogPos = -1;                    // gelesene Bytes in dcs.log
    public string LogHead = "";                 // erste Zeile von dcs.log: neue Datei = DCS neu gestartet
    public string LogFile = "";                 // dcs.log, in dem die Mission läuft (Dedicated Server: eigenes Profil); leer = Spielprofil
    public List<string> DcsNotes = [], Debrief = [];   // laufende DCS-Mission: Merker (tot, gelandet, Pilot je Maschine), Nachbesprechung
    public List<Rating> Sorties = [];                   // Nachbesprechung je Flug der Spielerpakete (wie BMS)
    public Dictionary<string, int> MissionGids = [];   // Fahrplan der laufenden Mission: "F12" -> groupId (Live-Befehle über den Hook)
    public int MissionGid, MissionUid, MissionSeq;
    public DateTime MissionMsg;                 // bis hierhin Lagemeldungen als Funktext an DCS gegeben
    public List<Wreck> Wrecks = [];             // Wracks zerstörter Fahrzeuge (bleiben in Folgemissionen liegen)
    public double WreckAcc;
    public List<Downed> Downed = [];            // abgeschossene Piloten über Feindgebiet (CSAR); Pilot < 0 = ~Career.Id (Mensch)
    public List<Career> Logbook = [];           // Pilotenkarrieren der Menschen (DCS-Spielername)
    public string Me = "";                      // Einzelspieler: dein DCS-Name (Pilotenfenster)
    public List<string> Report = [];            // Lagebericht 06:00 (Spielerseite)
    public List<string> News = [];              // Zeitung: "Zeit|Schlagzeile|Unterzeile", neueste zuletzt
    public Dictionary<string, Kit> Kits = [];   // Ausrüstung des Spielers je Muster
    public Kit KitOf(string type) => Kits.GetValueOrDefault(type) ?? new Kit();
    public long RngState = 0x2545F4914F6CDD1D;

    [JsonIgnore] public Theater Th = null!;
    [JsonIgnore] public Setup S = null!;

    public int NewId() => NextId++;
    public SortedDictionary<string, Sight> Intel(Side s) => s == Side.Red ? IntelRed : IntelBlue;

    /// Deterministischer Zufall (xorshift), Zustand wird mitgespeichert.
    public double Rnd()
    {
        RngState ^= RngState << 13; RngState ^= RngState >> 7; RngState ^= RngState << 17;
        return (RngState >>> 11) * (1.0 / (1L << 53));
    }
    public bool Chance(double p) => Rnd() < p;
    public T Pick<T>(IReadOnlyList<T> l) => l[(int)(Rnd() * l.Count) % l.Count];

    public void Log(Side s, string kind, string text, double x = 0, double z = 0)
    {
        Events.Add(new Ev { T = Time, Side = s, Kind = kind, Text = text, X = x, Z = z });
        if (Events.Count > 3000) Events.RemoveRange(0, 500);
    }

    public void Link()
    {
        foreach (var n in Nodes) n.Adj.Clear();
        foreach (var e in Edges) { Nodes[e.A].Adj.Add(e.Id); Nodes[e.B].Adj.Add(e.Id); }
    }

    public Obj? Base(int baseId) => Objs.FirstOrDefault(o => o.Kind == OK.Airbase && o.BaseId == baseId);
    public bool Daylight => Time.Hour >= 7 && Time.Hour < 19;

    static readonly JsonSerializerOptions Js = new() { IncludeFields = true, WriteIndented = false, Converters = { new JsonStringEnumConverter() } };
    public string ToJson() => JsonSerializer.Serialize(this, Js);
    /// Spielstand lesen; Karte und Startlage aus dem Stand (alte Stände: Kaukasus).
    public static World FromJson(string s, Theater? th = null)
    {
        var w = JsonSerializer.Deserialize<World>(s, Js)!;
        w.S = Scenario.Get(w.Scen);
        w.Th = th ?? Theater.Get(w.Map);
        Geo.Use(w.Th.Map);
        w.Link();
        return w;
    }
    public static string J(object o) => JsonSerializer.Serialize(o, Js);
}
