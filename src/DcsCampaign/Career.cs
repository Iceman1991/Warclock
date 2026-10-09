using System.Globalization;

namespace DcsCampaign;

/// Pilotenkarriere je Mensch (DCS-Spielername): Logbuch, Rang, Abschüsse, Orden. Wird ein Mensch abgeschossen, trägt sein Logbuch
/// das Schicksal (vermisst → CSAR, gefangen, gefallen), nicht der Staffelpilot des Platzes. Gefallen/gefangen: nächster Einsatz = neuer Pilot.
public class Career : Pilot
{
    public int Id, Air, Ground;
    public Side Side;
    public double Hours;
    public List<string> Medals = [];
    public List<Sortie> Book = [];                       // neueste zuerst
    public Sortie? Cur;                                  // laufender Einsatz (ab Einsteigen, zählt ab Start)
    static readonly string[] Blue = ["2nd Lt", "1st Lt", "Capt", "Maj", "Lt Col", "Col"], Red = ["Lt", "Sr Lt", "Capt", "Maj", "Lt Col", "Col"];
    static readonly int[] Need = [0, 10, 25, 45, 70, 100];
    public int Score => Sorties + 2 * Air + Ground / 2;
    public string Rank => (Side == Side.Red ? Red : Blue)[Need.Count(n => Score >= n) - 1];

    public static Career? Active(World w, string name) => w.Logbook.LastOrDefault(c => c.Name == name && c.Status is "aktiv" or "vermisst");
    /// Mensch in dieser Maschine (Merker who:F12-1=Name ab dem Einsteigen; nach „Nächster Einsatz“ heißt die Maschine F57-1)
    public static Career? In(World w, string unit) => w.DcsNotes.LastOrDefault(s => s.StartsWith("who:") && Bridge.Real(w, s[4..s.IndexOf('=')]) == unit) is { } s ? Active(w, s[(s.IndexOf('=') + 1)..]) : null;
    static Flight? FlightOf(World w, string unit) => w.Flights.FirstOrDefault(f => unit.StartsWith($"F{f.Id}-"));
    static Sortie New(World w, string unit, Flight? f) => new() { T = w.Time, Unit = unit, Callsign = f?.Callsign ?? "", Type = f?.Type ?? "", Task = f?.Task ?? "", Target = f == null ? "" : DcsCampaign.Air.TargetName(w, f.TargetKind, f.Target) };

    public static void Board(World w, string unit, string name)
    {
        if (name == "" || FlightOf(w, unit) is not { } f) return;
        var c = Active(w, name);
        if (c == null) { w.Logbook.Add(c = new Career { Id = w.NewId(), Name = name, Side = f.Side }); w.Log(f.Side, "spieler", $"{c.Rank} {name} joins the campaign – logbook opened."); }
        if (!w.Mp && w.Me == "") w.Me = name;
        if (c.Status == "vermisst") { w.Debrief.Add($"{w.Time:HH:mm} {name} is still MIA – flies as a stand-in, sortie not logged"); return; }
        w.DcsNotes.RemoveAll(s => s.StartsWith("who:") && s.EndsWith("=" + name));
        w.DcsNotes.Add($"who:{unit}={name}");
        if (c.Cur?.Unit == unit) return;
        if (c.Cur != null) Close(w, c, "changed aircraft");
        c.Cur = New(w, unit, f);
    }

    /// Ausgestiegen: am Boden zählt der Einsatz nicht; in der Luft fliegt die KI weiter, das Logbuch schließt bei Landung/Missionsende.
    public static void Leave(World w, string unit)
    {
        if (In(w, unit) is not { Cur: { Up: var up } } c || up != default) return;
        c.Cur = null; w.DcsNotes.RemoveAll(s => s.StartsWith($"who:{unit}="));
    }

    public static void Up(World w, string unit)
    {
        if (In(w, unit) is not { } c) return;
        c.Cur ??= New(w, unit, FlightOf(w, unit));        // nächster Einsatz nach der Landung in derselben Mission
        if (c.Cur.Up == default) c.Cur.Up = w.Time;
    }

    public static void Kill(World w, string unit, Side? target, bool air)
    {
        if (In(w, unit) is not { Cur: { } s } c || target is not { } t || t == c.Side) return;
        if (air) s.Air++; else s.Ground++;
    }

    public static void Land(World w, string unit, string place) { if (In(w, unit) is { Cur.Up: var up } c && up != default) Close(w, c, place == "" ? "landed" : $"landed at {place}"); }

    /// Abgeschossen (Schicksal hat Air.Loss schon gesetzt): Verwundetenabzeichen, Logbuch schließen.
    public static void Down(World w, Career c, string how, bool hostile)
    {
        string fate = c.Status switch { "aktiv" => "ejected, recovered", "vermisst" => "ejected over enemy territory – MIA", _ => "KIA" };
        if (hostile && c.Status != "gefallen") Medal(w, c, "Purple Heart");
        if (c.Cur != null) Close(w, c, $"{how} – {fate}", true);
        if (c.Status == "gefallen") w.Log(c.Side, "spieler", $"{c.Rank} {c.Name} KIA after {c.Sorties} sorties, {c.Air} air / {c.Ground} ground kills. Next sortie: a new pilot.");
        else if (c.Status == "vermisst") w.Log(c.Side, "spieler", $"{c.Rank} {c.Name} is MIA behind enemy lines – no flying until CSAR brings you home.");
    }

    public static void Captured(World w, Career c)
    {
        Medal(w, c, "Prisoner of War Medal");
        w.Log(c.Side, "spieler", $"{c.Rank} {c.Name} is a prisoner of war – logbook closed after {c.Sorties} sorties. Next sortie: a new pilot.");
    }

    /// Missionsende: offene Einsätze schließen (in der Luft = zählt, nie gestartet = verworfen).
    public static void End(World w) { foreach (var c in w.Logbook.Where(c => c.Cur != null).ToList()) Close(w, c, "mission ended airborne"); }

    static void Close(World w, Career c, string result, bool force = false)
    {
        var s = c.Cur!; c.Cur = null;
        if (s.Up == default && !force) return;
        if (s.Up == default) s.Up = w.Time;
        string r0 = c.Rank;
        s.Hours = Math.Round((w.Time - s.Up).TotalHours, 1); s.Result = result;
        c.Hours += s.Hours; c.Sorties++; c.Air += s.Air; c.Kills = c.Air; c.Ground += s.Ground;
        c.Book.Insert(0, s);
        if (c.Sorties % 10 == 0) Medal(w, c, "Air Medal");
        if (s.Air >= 3) Medal(w, c, "Silver Star");
        else if (s.Air >= 2 || s.Ground >= 4) Medal(w, c, "Distinguished Flying Cross");
        if (c.Air >= 5 && c.Air - s.Air < 5) Medal(w, c, "Ace");
        if (c.Rank != r0) w.Log(c.Side, "spieler", $"{c.Name} promoted to {c.Rank}.");
    }

    public static void Medal(World w, Career c, string m)
    {
        c.Medals.Add($"{m} ({w.Time.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)})");
        w.Log(c.Side, "spieler", $"{c.Rank} {c.Name}: {m}.");
    }
}

public class Sortie { public DateTime T, Up; public string Unit = "", Callsign = "", Type = "", Task = "", Target = "", Result = ""; public double Hours; public int Air, Ground; }
