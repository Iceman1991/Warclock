using System.Globalization;
using System.Text.RegularExpressions;

namespace DcsCampaign;

/// F10-Menü „Kampagne“ im Cockpit (wie BMS): Lage, CAS-Check-in beim JTAC, Pop-up-Ziel im Flug (Divert), nächster Einsatz nach der Landung.
/// Frage = MENU-Zeile in dcs.log (Bridge), Antwort = Hook-Befehl an DCSCAMP.cmd: Text (say; {POS} setzt die Mission als Koordinaten ein),
/// Kartenmarker, Rauch, Auswahlmenü. Jeder Funkspruch steht zusätzlich in %TEMP%\DcsCampaign-Funk.txt – den spricht DCS-ATC (ATC-SCHNITTSTELLE.md).
static class Tasking
{
    public static readonly string[] Jtacs = ["Axeman", "Darknight", "Warrior", "Pointer", "Eyeball", "Moonbeam", "Whiplash", "Finger", "Pinpoint", "Ferret",
        "Shaba", "Playboy", "Hammer", "Jaguar", "Deathstar", "Anvil", "Firefly", "Mantis", "Badger"];   // Reihenfolge = DCS-FAC-Rufzeichen (callname 1–19)
    public static string FunkFile = Program.Dev != null ? Path.Combine(Program.Dev, "DcsCampaign-Funk.txt") : Path.Combine(Path.GetTempPath(), "DcsCampaign-Funk.txt");
    static readonly Regex Unit = new(@"^F(\d+)-(\d+)$");

    public static void Menu(World w, string unit, string what, string key)
    {
        if (w.Mission == "" || Unit.Match(unit) is not { Success: true } m || w.Flights.FirstOrDefault(f => f.Id == int.Parse(m.Groups[1].Value)) is not { Player: true } f) return;
        switch (what)
        {
            case "lage": Lage(w, f, unit); break;
            case "jtac": Jtac(w, f, unit); break;
            case "ziele": Choices(w, f, unit, false); break;
            case "weiter": Choices(w, f, unit, true); break;
            case "pick": Pick(w, f, unit, key); break;
        }
    }

    // ---------- Antworten ----------

    static void Lage(World w, Flight f, string unit)
    {
        var at = new P(f.X, f.Z);
        var l = new List<string> { $"{f.Callsign}: {f.Task} {Name(w, f)}" + (f.Tot > w.Time ? $", TOT {f.Tot:HH:mm}" : "") };
        l.AddRange(w.Events.Where(e => (e.Side == f.Side || e.Side == Side.None) && e.Kind is "front" or "lage").TakeLast(2).Select(e => $"{e.T:HH:mm} {e.Text}"));
        var th = Air.Threats(w, f.Side, Math.Max(f.Alt, 3000)).Where(t => Geo.Dist(t.X, t.Z, at.X, at.Z) < t.R + 55000).OrderBy(t => Geo.Dist(t.X, t.Z, at.X, at.Z)).Take(4)
            .Select(t => $"{t.Name} {Brg(at, new P(t.X, t.Z))} {Nm(at, new P(t.X, t.Z))}").ToList();
        l.Add(th.Count == 0 ? "No known air defenses within 30 NM." : "Threats: " + string.Join(", ", th));
        var sup = w.Flights.Where(o => o.Side == f.Side && Air.Up(o) && o.Task is "AWACS" or "Tanker").Select(o => $"{o.Callsign} {o.Freq:0.0}" + (o.Tacan > 0 ? $" TACAN {o.Tacan}X" : "")).ToList();
        if (sup.Count > 0) l.Add("Support: " + string.Join(", ", sup));
        if (w.Downed.Where(d => d.Side == f.Side).MinBy(d => Geo.Dist(d.X, d.Z, at.X, at.Z)) is { } dn && Geo.Dist(dn.X, dn.Z, at.X, at.Z) < 185000)
        {
            var dp = new P(dn.X, dn.Z); var name = Air.PilotOf(w, dn).Name;
            bool near = Air.Helo(f.Type) && Geo.Dist(dp.X, dp.Z, at.X, at.Z) < 10000;   // Hubschrauber in der Nähe: der Pilot zündet Rauch
            l.Add($"Pilot down: {name} {Brg(at, dp)} {Nm(at, dp)}, location {{POS}}" + (near ? " - popping red smoke, land within 500 m to pick him up." : "."));
            Send(w, unit, "lage", "Lage", string.Join("\n", l), dp, $"Pilot {name}", near);
            return;
        }
        Send(w, unit, "lage", "Lage", string.Join("\n", l));
    }

    /// CAS-Check-in: JTAC an der nächsten Kontaktlinie mit Feindberührung, Ziel = stärkster Feindverband dort, Rauch und Marker.
    static void Jtac(World w, Flight f, string unit)
    {
        var at = new P(f.X, f.Z);
        var c = w.Axes.Select((ax, i) => (i, fr: Sim.Front(w, ax))).Where(t => t.fr != null)
            .Select(t => (t.i, en: f.Side == Side.Red ? t.fr!.Value.B : t.fr!.Value.R))
            .Where(t => w.Units.Any(u => u.Side != f.Side && u.Node == t.en)).OrderBy(t => Geo.Dist(at.X, at.Z, w.Nodes[t.en].X, w.Nodes[t.en].Z)).FirstOrDefault(t => Geo.Dist(at.X, at.Z, w.Nodes[t.en].X, w.Nodes[t.en].Z) < 150000, (i: -1, en: -1));
        if (c.i < 0) { Send(w, unit, "jtac", "JTAC", "No JTAC available: no enemy contact within 80 NM."); return; }
        string jtac = Jtacs[c.i % Jtacs.Length];
        var u = w.Units.Where(u => u.Side != f.Side && u.Node == c.en).MaxBy(u => u.Str * Sim.Pow[u.Kind])!;
        var tp = new P(u.X, u.Z);
        var own = w.Units.Where(o => o.Side == f.Side).MinBy(o => Geo.Dist(o.X, o.Z, tp.X, tp.Z));
        var flak = w.Units.Where(o => o.Side != f.Side && o.Kind == UK.Shorad && Geo.Dist(o.X, o.Z, tp.X, tp.Z) < 12000).Select(o => o.Name)
            .Concat(Air.Threats(w, f.Side, 1500).Where(t => Geo.Dist(t.X, t.Z, tp.X, tp.Z) < t.R).Select(t => t.Name)).Distinct().Take(3).ToList();
        var back = own == null ? "" : Dir(Geo.Brg(tp, new P(own.X, own.Z)));
        var text = string.Join("\n", new[]
        {
            $"{f.Callsign}, {jtac}: CAS tasking.",
            $"Target: {u.Name}, strength {u.Str:P0}, dug in {u.Dig:P0}.",
            "Location: {POS}",
            $"From you: {Brg(at, tp)} {Nm(at, tp)}.",
            "Mark: red smoke.",
            own == null ? "Friendlies: none nearby." : $"Friendlies: {Geo.Dist(tp.X, tp.Z, own.X, own.Z) / 1000:0.0} km {back}.",
            flak.Count == 0 ? "Threats: none known." : "Threats: " + string.Join(", ", flak) + ".",
            back == "" ? "" : $"Egress {back}.",
        }.Where(s => s != ""));
        w.Intel(f.Side)[$"U{u.Id}"] = new Sight { X = u.X, Z = u.Z, T = w.Time, Src = $"JTAC {jtac}", Str = u.Str };
        w.Debrief.Add($"{w.Time:HH:mm} JTAC {jtac}: {f.Callsign} checks in, target {u.Name}");
        Send(w, unit, "jtac", jtac, text, tp, $"JTAC {jtac}: {u.Name}", smoke: true);
    }

    /// Auswahl: Pop-up-Ziele im Umkreis (Divert) oder Aufträge vom Platz, auf dem der Spieler steht (nächster Einsatz).
    static void Choices(World w, Flight f, string unit, bool next)
    {
        var q = Air.SqnOf(w, f);
        P from;
        if (next)
        {
            if (!w.DcsNotes.Contains("land:" + unit) || Base(w, f) is not { } b) { Send(w, unit, "lage", "Kampagne", "Next sortie only after landing at a friendly, operational airfield."); return; }
            from = new P(b.X, b.Z);
        }
        else from = new P(f.X, f.Z);
        var l = Cands(w, f, from, next ? Air.PerfOf(q.Type).Radius * 1000 : 150000).Take(4).ToList();
        if (l.Count == 0) { Send(w, unit, "lage", "Kampagne", "No suitable targets in range."); return; }
        string title = next ? "Choose next sortie" : "Choose new target";
        var items = l.Select(x => (object?)D(("l", $"{x.Task} {Air.TargetName(w, x.Kind, x.Id)} · {Nm(from, x.Pos)} {Brg(from, x.Pos)}"), ("k", $"{(next ? "N" : "D")}:{x.Task}:{x.Kind}:{x.Id}"))).ToList();
        Send(w, unit, "lage", "Kampagne", $"{l.Count} targets - F10 > Campaign > {title}.", menu: D(("u", unit), ("t", title), ("items", items)));
    }

    static IEnumerable<Air.Cand> Cands(World w, Flight f, P from, double max)
    {
        var q = Air.SqnOf(w, f);
        return Air.Targets(w, f.Side).Where(x => q.Roles.Contains(x.Task == "DEAD" ? "Strike" : x.Task) && Geo.Dist(from.X, from.Z, x.Pos.X, x.Pos.Z) < max)
            .OrderByDescending(x => x.Score * Air.Weight(w, f.Side, x.Task, x.Pos) / (1 + Geo.Dist(from.X, from.Z, x.Pos.X, x.Pos.Z) / 80000));
    }

    static void Pick(World w, Flight f, string unit, string key)
    {
        var k = key.Split(':');
        if (k.Length != 4 || !int.TryParse(k[3], out int id)) return;
        var x = Air.Targets(w, f.Side).FirstOrDefault(x => x.Task == k[1] && x.Kind == k[2] && x.Id == id);
        if (x == null) { Send(w, unit, "lage", "Kampagne", "Target no longer valid - choose again."); return; }
        if (k[0] == "D") Divert(w, f, unit, x); else Turnaround(w, f, unit, x);
    }

    /// Pop-up-Ziel: der Spielerflug bekommt ein eigenes Paket und einen neuen Zielpunkt (Route ab jetzt direkt dorthin).
    static void Divert(World w, Flight f, string unit, Air.Cand x)
    {
        var at = new P(f.X, f.Z); double v = Air.PerfOf(f.Type).Spd;
        var p = Pkg(w, f, x, "Pop-up target in flight");
        var home = f.Route[^1];
        var tt = w.Time.AddSeconds(Geo.Dist(at.X, at.Z, x.Pos.X, x.Pos.Z) / v);
        f.Route = [.. f.Route.Where(r => r.T <= w.Time && r.Kind != "land"), new Wp { Name = "Divert", Kind = "nav", X = at.X, Z = at.Z, Alt = f.Alt, Spd = v, T = w.Time },
            new Wp { Name = "Target", Kind = "target", X = x.Pos.X, Z = x.Pos.Z, Alt = f.Alt, Spd = v, T = tt },
            new Wp { Name = home.Name, Kind = "land", X = home.X, Z = home.Z, Alt = home.Alt, Spd = v, T = tt.AddSeconds(Geo.Dist(x.Pos.X, x.Pos.Z, home.X, home.Z) / v + 300) }];
        (f.Task, f.Target, f.TargetKind, f.Tot, f.Land, f.Struck, f.Pkg, p.Tot) = (x.Task, x.Id, x.Kind, tt, f.Route[^1].T, false, p.Id, tt);
        w.Log(f.Side, "ato", $"{f.Callsign} retasked: {x.Task} {Air.TargetName(w, x.Kind, x.Id)} – {x.Why}.");
        Send(w, unit, "awacs", Awacs(w, f), Brief(w, f, at, x, "new tasking"), x.Pos, $"{f.Callsign}: {x.Task} {Air.TargetName(w, x.Kind, x.Id)}");
    }

    /// Nächster Einsatz nach der Landung: die Maschine des Spielers wird ein neuer Flug (gleiches Rufzeichen), die Rotte bleibt im alten.
    /// Ab jetzt meldet DCS die Maschine unter dem alten Namen – Bridge übersetzt per alias:F12-1=F57-1.
    static void Turnaround(World w, Flight f, string unit, Air.Cand x)
    {
        var q = Air.SqnOf(w, f);
        var b = Base(w, f);
        if (b == null) return;
        int n = int.Parse(Unit.Match(unit).Groups[2].Value);
        int pilot = w.DcsNotes.FirstOrDefault(s => s.StartsWith($"pilot:{unit}=")) is { } ps ? int.Parse(ps.Split('=')[1]) : -1;
        if (f.Done) { if (q.Repair.Count > 0) q.Repair.RemoveAt(q.Repair.IndexOf(q.Repair.Max())); }   // Rotte schon gelandet: die Maschine des Spielers geht nicht in die Wartung
        else { f.Alive--; f.N = Math.Max(f.Alive, 1); f.Pilots.Remove(pilot); w.DcsNotes.Remove("land:" + unit); }   // Rotte landet ohne den Spieler
        var p = Pkg(w, f, x, "Next sortie after landing");
        double alt = x.Task is "CAS" or "BAI" or "Interdiction" ? 5000 : 7000;
        var tot = w.Time.AddMinutes(20).AddSeconds(Geo.Dist(b.X, b.Z, x.Pos.X, x.Pos.Z) * 1.15 / Air.PerfOf(q.Type).Spd);   // 20 min nachtanken, aufmunitionieren, rollen
        var nf = Air.NewFlight(w, p, q, x.Task, 1, x.Pos, tot, alt, x.Task == "CAS" ? 30 : 0);
        (nf.Callsign, nf.Freq, nf.Player, nf.InDcs, nf.Status, nf.Crew, nf.Why, p.Tot) = (f.Callsign, f.Freq, true, true, "in der Luft", "P", p.Why, nf.Tot);
        if (pilot >= 0) { nf.Pilots = [pilot]; w.DcsNotes.Add($"pilot:F{nf.Id}-1={pilot}"); }
        b.Fuel = Math.Max(0, b.Fuel - Air.PolOf(q.Type)); b.Stock = Math.Max(0, b.Stock - Air.MunOf(x.Task));
        w.DcsNotes.Add($"alias:{unit}=F{nf.Id}-1");
        w.DcsNotes.Add($"alias:F{f.Id}=F{nf.Id}");                        // Position der Gruppe (Spieler führt sie)
        w.MissionGids[$"F{nf.Id}"] = w.MissionGids.GetValueOrDefault($"F{f.Id}");   // nicht noch einmal als KI-Gruppe einspielen
        w.Log(f.Side, "ato", $"{f.Callsign} (aircraft {n}) launches again from {b.Name}: {x.Task} {Air.TargetName(w, x.Kind, x.Id)}, TOT {nf.Tot:HH:mm}.");
        Send(w, unit, "lage", b.Name, Brief(w, nf, new P(b.X, b.Z), x, $"next sortie, TOT {nf.Tot:HH:mm}"), x.Pos, $"{f.Callsign}: {x.Task} {Air.TargetName(w, x.Kind, x.Id)}");
    }

    // ---------- Hilfen ----------

    static Pkg Pkg(World w, Flight f, Air.Cand x, string why)
    {
        w.Pkgs.FirstOrDefault(p => p.Id == f.Pkg)?.Flights.Remove(f.Id);
        var p = new Pkg { Id = w.NewId(), Side = f.Side, Task = x.Task, TargetKind = x.Kind, Target = x.Id, Why = $"{why}: {x.Why}" };
        p.Name = $"Package {p.Id} – {x.Task} {Air.TargetName(w, x.Kind, x.Id)}";
        p.Flights.Add(f.Id);
        w.Pkgs.Add(p);
        return p;
    }

    static string Brief(World w, Flight f, P from, Air.Cand x, string what)
    {
        var th = Air.Threats(w, f.Side, 3000).Where(t => Geo.Dist(t.X, t.Z, x.Pos.X, x.Pos.Z) < t.R + 10000).Select(t => t.Name).Take(3).ToList();
        return string.Join("\n", $"{f.Callsign}, {what}: {x.Task} {Air.TargetName(w, x.Kind, x.Id)}.", x.Why + ".", "Target: {POS}", $"From you: {Brg(from, x.Pos)} {Nm(from, x.Pos)}.",
            th.Count == 0 ? "No known air defenses at target." : "Threats at target: " + string.Join(", ", th) + " - go low or bring SEAD.");
    }

    static Obj? Base(World w, Flight f) => w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == f.Side && Air.Open(o) && Geo.Dist(o.X, o.Z, f.X, f.Z) < 6000).MinBy(o => Geo.Dist(o.X, o.Z, f.X, f.Z));
    static string Awacs(World w, Flight f) => w.Flights.FirstOrDefault(o => o.Side == f.Side && Air.Up(o) && o.Task == "AWACS")?.Callsign ?? "Kommandostand";
    static string Name(World w, Flight f) => Air.TargetName(w, f.TargetKind, f.Target) is { Length: > 0 } n ? n : w.Pkgs.FirstOrDefault(p => p.Id == f.Pkg)?.Name ?? "";
    static string Brg(P a, P b) => $"{Math.Round(Geo.Brg(a, b)) % 360:000}°";
    static string Nm(P a, P b) => $"{Geo.Dist(a.X, a.Z, b.X, b.Z) / 1852:0} NM";
    static string Dir(double brg) => new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" }[(int)Math.Round(((brg % 360) + 360) % 360 / 45) % 8];
    static Dictionary<string, object?> D(params (string K, object? V)[] kv) { var d = new Dictionary<string, object?>(); foreach (var (k, v) in kv) d[k] = v; return d; }

    /// Antwort an die Gruppe des Spielers (über den Hook) und als Funkspruch für DCS-ATC.
    static void Send(World w, string unit, string role, string from, string text, P? at = null, string? mark = null, bool smoke = false, Dictionary<string, object?>? menu = null)
    {
        var c = D(("seq", ++w.MissionSeq), ("say", new List<object?> { D(("u", unit), ("t", text), ("s", 45), ("x", at?.X), ("z", at?.Z)) }));
        if (at is { } p && mark != null) c["mark"] = new List<object?> { D(("u", unit), ("t", mark), ("x", Math.Round(p.X)), ("z", Math.Round(p.Z))) };
        if (at is { } s && smoke) c["smoke"] = new List<object?> { D(("x", Math.Round(s.X)), ("z", Math.Round(s.Z)), ("c", 1)) };
        if (menu != null) c["menu"] = new List<object?> { menu };
        Miz.Cmd($"{w.MissionSeq:00000}.lua", Lua.Write("DCSCAMP_CMD", c) + $"if DCSCAMP and DCSCAMP.token == {Lua.Quote(w.Mission)} and DCSCAMP.cmd then DCSCAMP.cmd(DCSCAMP_CMD) end\n");
        try   // Zeile je Funkspruch: Zeit;Einheit;Rolle;Absender;Text (Zeilenumbrüche als " / ")
        {
            File.AppendAllText(FunkFile, string.Join(";", w.Time.ToString("s", CultureInfo.InvariantCulture), unit, role, from, text.Replace("{POS}", at is { } q ? $"x={q.X:0} z={q.Z:0}" : "").Replace("\n", " / ").Replace(";", ",")) + "\n");
        }
        catch (IOException) { }
    }
}
