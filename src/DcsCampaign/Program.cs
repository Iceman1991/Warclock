using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DcsCampaign;

static class Program
{
    public static readonly string? Dev = Environment.GetEnvironmentVariable("DCSCAMP_DEV");   // Entwickler-Lauf: eigener Datenordner, kein Browser
    /// Optionale Teile laut Setup ({app}\options.ini, z. B. "Atc=0"); ohne Datei (Entwickler, Tests) alles an.
    public static bool Opt(string key) => !(File.Exists(Path.Combine(AppContext.BaseDirectory, "options.ini"))
        && File.ReadLines(Path.Combine(AppContext.BaseDirectory, "options.ini")).Any(l => l.Trim().Equals(key + "=0", StringComparison.OrdinalIgnoreCase)));
    public static string Dir => Dev ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCS-Kampagne");
    public static string SavedGames => new[] { "DCS", "DCS.openbeta" }.Select(n => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", n))
        .FirstOrDefault(Directory.Exists) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "DCS");

    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Contains("--test")) return Test(Theater.Get("Caucasus"), args.Contains("gulf"));
        if (args.Contains("--balance")) { var set = Scenario.All.FirstOrDefault(s => args.Contains(s.Id)) ?? Scenario.Caucasus; Balance(Theater.Get(set.Map), args.Select(a => int.TryParse(a, out var n) ? n : 0).FirstOrDefault(n => n > 0, 7), [1, 2, 3, 4], set.Id); return 0; }
        return Web.Serve(Dev == null ? 8642 : 8643);     // 8787 belegt hier der Foundry-Companion; Entwickler-Lauf neben der installierten Version
    }

    /// Entwicklerwerkzeug: mehrere Startwerte je n Tage rechnen, Mittelwerte je Tag und Seite ausgeben.
    static void Balance(Theater th, int days, int[] seeds, string id = "kaukasus")
    {
        var rows = new Dictionary<(int D, Side S), List<double[]>>();
        var over = new List<string>();
        foreach (var seed in seeds)
        {
            var w = Scenario.Create(th, seed, id);
            var prev = new Dictionary<Side, (double Sorties, double Lost)>();
            for (int d = 1; d <= days; d++)
            {
                var t0 = w.Time;
                Sim.Advance(w, w.Time.AddDays(1));
                var ev = w.Events.Where(e => e.T > t0).ToList();
                foreach (var s in new[] { Side.Red, Side.Blue })
                {
                    var qs = w.Sqns.Where(q => q.Side == s).ToList();
                    double sorties = qs.Sum(q => q.Pilots.Sum(p => p.Sorties)), lost = qs.Sum(q => q.Lost);
                    var (ps, pl) = prev.GetValueOrDefault(s);
                    prev[s] = (sorties, lost);
                    var l = ev.Where(e => e.Side == s && e.Kind == "verlust").ToList();
                    var us = w.Units.Where(u => u.Side == s).ToList();
                    (rows.TryGetValue((d, s), out var r) ? r : rows[(d, s)] = []).Add([
                        qs.Sum(q => q.Ac + q.Repair.Count), sorties - ps, lost - pl,
                        l.Count(e => e.Text.Contains("air combat")), l.Count(e => e.Text.Contains("(SA-") || e.Text.Contains("Patriot") || e.Text.Contains("Hawk") || e.Text.Contains("NASAMS")), l.Count(e => e.Text.Contains("air defense")),
                        w.Objs.Count(o => o.Side == s && o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles > 0),
                        us.Count, us.Count == 0 ? 0 : us.Average(u => u.Str), us.Count == 0 ? 0 : us.Average(u => u.Supply), us.Count == 0 ? 0 : us.Average(u => u.Fuel),
                        ev.Count(e => e.Kind == "front" && e.Text.Contains($"captured by {s.De()}")),
                        w.Nodes.Count(n => n.Owner == s && n.Key == n.Id),
                        w.Objs.Where(o => o.Side == s && o.Kind == OK.Depot).Sum(o => o.Stock), w.Objs.Where(o => o.Side == s && o.Kind == OK.Depot).Sum(o => o.Fuel),
                        w.Objs.Where(o => o.Side == s && o.Kind == OK.Airbase && o.Node >= 0).Sum(o => o.Fuel),
                        ev.Count(e => e.Text.StartsWith($"{s.De()} attacks on axis") || e.Text.StartsWith($"Local attack by {s.De()}"))]);
                }
                if (w.Over != "") { over.Add($"Startwert {seed}, Tag {d}: {w.Over}"); break; }
            }
            double Econ(Side s) { var l = w.Objs.Where(o => o.Side == s && o.Kind is OK.Port or OK.Factory or OK.Depot or OK.Cp).ToList(); return l.Sum(o => o.Value * o.Health) / Math.Max(0.01, l.Sum(o => o.Value)); }
            Console.WriteLine($"Startwert {seed}: Wirtschaft Rot {Econ(Side.Red):P0} Blau {Econ(Side.Blue):P0}, Raketen {w.Events.Count(e => e.Text.StartsWith("Missile impact"))} Treffer, {w.Events.Count(e => e.Text.StartsWith("Missile attack"))} abgefangen");
            Console.WriteLine($"Startwert {seed} – Operationen und Front:");
            foreach (var e in w.Events.Where(e => e.Kind == "front" && (e.Text.Contains("axis") || e.Text.Contains("captured") || e.Text.Contains("surrenders"))))
                Console.WriteLine($"  {(e.T - w.Start).TotalHours,5:0} h  {e.Text}");
            if (seed != seeds[0]) continue;
            Console.WriteLine($"Startwert {seed} am Ende – Lager und schlecht versorgte Verbände:");
            foreach (var o in w.Objs.Where(o => o.Kind is OK.Depot or OK.Factory or OK.Port).OrderBy(o => o.Side))
                Console.WriteLine($"  {o.Side.De(),-5} {o.Name,-28} Zustand {o.Health,4:P0}  Mun {o.Stock,5:0} t  Sprit {o.Fuel,5:0} t{(w.Convoys.Any(c => c.From == o.Id) ? "  (Transport unterwegs)" : "")}");
            foreach (var u in w.Units.Where(u => u.Supply < 0.6 || u.Fuel < 0.5)) Console.WriteLine($"  {u.Side.De(),-5} {u.Name,-40} Mun {u.Supply,4:P0} Sprit {u.Fuel,4:P0} {u.Why}");
            foreach (var o in w.Objs.Where(o => o.Kind == OK.Bridge).OrderByDescending(o => o.Flow).Take(5)) Console.WriteLine($"  Brücke {o.Name,-28} {o.Flow,6:0} t/Tag  Zustand {o.Health:P0}");
        }
        Console.WriteLine("Tag Seite  Flz  Einsätze Verl. (Luft/SAM/Flak)  SAM  Verb. Stärke  Mun Sprit  eingen. Schlüssel  Depot-Mun Sprit Platz-Sprit Angriffe");
        foreach (var ((d, s), l) in rows.OrderBy(k => k.Key.D).ThenBy(k => k.Key.S))
        {
            double M(int i) => l.Average(r => r[i]);
            Console.WriteLine($"{d,3} {s.De(),-5} {M(0),4:0} {M(1),8:0} {M(2),5:0.0} ({M(3):0.0}/{M(4):0.0}/{M(5):0.0}) {M(6),9:0.0} {M(7),5:0.0} {M(8),6:P0} {M(9),4:P0} {M(10),5:P0} {M(11),8:0.0} {M(12),9:0.0} {M(13),9:0} {M(14),5:0} {M(15),11:0} {M(16),8:0.0}  (n={l.Count})");
        }
        foreach (var o in over) Console.WriteLine(o);
        Console.WriteLine("Materialfluss t/Tag (Mittel):");
        foreach (var (k, v) in Sim.Flow.OrderBy(k => k.Key)) Console.WriteLine($"  {k,-32} {v / days / seeds.Length,7:0}");
    }

    // ---------- Selbsttest: Aufbau, Speichern/Laden, Determinismus, 72 h Krieg, Brücken, Tempo ----------

    static int Test(Theater th, bool gulfOnly = false)
    {
        int fail = 0;
        void Check(bool ok, string what) { Console.WriteLine((ok ? "OK    " : "FEHLER ") + what); if (!ok) fail++; }
        if (gulfOnly) { SamTest(Check); GulfTest(Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("logistik")) { Logistik(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("plan")) { PlanTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("bda")) { BdaTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("heli")) { HeloTest(th, Check); LiftTest(th, Check); SwapTest(th, Check); OobTest(th, Check); JtacTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("traeger")) { CarrierTest(Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("mp")) { MpTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("ew")) { EwTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("navy")) { NavyTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("auto")) { AutoTest(Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("mix")) { MixTest(th, Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("syria")) { SyriaTest(Check); return fail; }
        if (Environment.GetCommandLineArgs().Contains("war")) { WarTest(th, Check); return fail; }

        Check(th.Payloads.GetValueOrDefault("F-14B")?.Count > 0, $"Loadouts F-14B: {th.Payloads.GetValueOrDefault("F-14B")?.Count ?? 0}");
        var hp = th.Payloads["FA-18C_hornet"];
        int same = hp.Count(p => Air.A2aCount(Theater.LoadName(p.Pylons.Select(y => y.Clsid))) == Air.A2aCount(p.Name) && Air.ArmCount(Theater.LoadName(p.Pylons.Select(y => y.Clsid))) == Air.ArmCount(p.Name));
        Check(th.Stations("FA-18C_hornet").Count >= 9 && same >= hp.Count * 0.8, $"Bestückung je Station: F/A-18C {th.Stations("FA-18C_hornet").Count} Stationen, {same}/{hp.Count} Beladungen nachgerechnet gleich (Luft-Luft, HARM)");
        foreach (var (type, task) in new[] { ("FA-18C_hornet", "CAP"), ("FA-18C_hornet", "Strike"), ("FA-18C_hornet", "SEAD"), ("FA-18C_hornet", "CAS"), ("F-14B", "CAP"), ("F-15ESE", "Strike"), ("A-10C_2", "CAS"), ("Su-25T", "CAS"), ("Su-27", "CAP"), ("Su-24M", "Strike"), ("Su-34", "OCA"), ("Su-30", "SEAD") })
        {
            var l = Air.ChooseLoad(th, type, task);
            Console.WriteLine($"       {type,-14} {task,-7} {l}  [A2A {Air.A2aCount(l)}, ARM {Air.ArmCount(l)}, Wirkung {Air.Punch(l)}]");
        }

        var sw = Stopwatch.StartNew();
        var w = Scenario.Create(th, 1);
        Console.WriteLine($"       Aufbau {sw.ElapsedMilliseconds} ms: {w.Nodes.Count} Orte, {w.Edges.Count(e => !e.Rail)} Wege, {w.Edges.Count(e => e.Rail)} Gleisstrecken, {w.Objs.Count} Objekte, {w.Units.Count} Verbände, {w.Sqns.Count} Staffeln");
        Check(w.Axes.All(a => a.Keys.Count >= 4 && Sim.Front(w, a) != null), "Achsen mit Front");
        var cut = w.Objs.Where(o => o.Node >= 0 && o.Kind is OK.Depot or OK.Airbase or OK.Factory or OK.Port)
            .Where(o => !w.Objs.Any(d => d.Side == o.Side && d.Name.StartsWith("Army") && Sim.Path(w, d.Node, o.Node, o.Side, true) != null)).Select(o => o.Name).ToList();
        Check(cut.Count == 0, "Nachschubnetz zusammenhängend " + string.Join(", ", cut));
        var json = w.ToJson();
        Check(World.FromJson(json, th).ToJson() == json, $"Speichern/Laden verlustfrei ({json.Length / 1024} KB)");
        var sf = Path.Combine(Path.GetTempPath(), "DcsCampaign-Load.json");
        File.WriteAllText(sf, "{kaputt"); File.WriteAllText(sf + ".bak", json);
        Check(Web.Load(sf)?.ToJson() == json && File.Exists(sf + ".kaputt"), "Spielstand kaputt → Sicherung geladen");

        sw.Restart();
        for (int d = 1; d <= 3; d++) { Sim.Advance(w, w.Time.AddDays(1)); Report(w, d); }
        double perDay = sw.Elapsed.TotalSeconds / 3;
        Check(perDay < 10, $"Tempo {perDay:0.00} s Rechenzeit pro Kriegstag");
        Check(w.Flights.All(f => double.IsFinite(f.X) && double.IsFinite(f.Z) && double.IsFinite(f.Alt)), "Flugpositionen gültig");
        Check(w.Units.All(u => u.Str is >= 0 and <= 1 && u.Supply is >= 0 and <= 1.0001 && u.Fuel is >= 0 and <= 1.0001), "Verbände im Wertebereich");
        Check(w.Objs.Where(o => o.Kind is OK.Depot or OK.Airbase).All(o => o.Stock >= -0.01 && o.Stock <= o.Cap + 1 && o.Fuel >= -0.01 && o.Fuel <= o.Cap + 1), "Depots im Wertebereich");
        Check(Ev("staging") > 0 && Ev("attacks on axis") + Ev("Local attack") + Ev("calls off the attack") > 0, $"Operationen: {Ev("– staging")} Bereitstellungen, {Ev("attacks on axis")} Angriffe im Schwerpunkt, {Ev("Local attack")} örtliche, {Ev("calls off the attack")} abgesagt, {Ev("stalls")} kommen zum Stehen");
        Check(w.Events.Any(e => e.Text.StartsWith("Troop train") || e.Text.StartsWith("Troopship")) && w.Events.Any(e => e.Text.StartsWith("Reinforcements:")),
            $"Verstärkungen reisen per Bahn/Schiff an ({w.Events.Count(e => e.Text.StartsWith("Troop train") || e.Text.StartsWith("Troopship"))} Transporte, {w.Events.Count(e => e.Text.StartsWith("Reinforcements:"))} eingetroffen)");
        Logistik(th, Check);
        Check(w.Report.Count > 8 && w.Report[0].StartsWith("SITREP"), $"Lagebericht 06:00 ({w.Report.Count} Zeilen): {w.Report.LastOrDefault()}");
        var days = w.News.GroupBy(n => n[..10]).ToList();
        Check(days.Count >= 3 && days.All(d => d.Count() is >= 1 and <= 4) && w.News.All(n => n.Split('|').Length == 3), $"Zeitung: {days.Count} Ausgaben, zuletzt „{string.Join(" / ", days[^1].Select(n => n.Split('|')[1]))}“");
        var wk = World.FromJson(json, th); var wu = wk.Units.First(u => u.Kind == UK.Armor); Sim.Hit(wk, wu, 0.5);
        Check(wk.Wrecks.Count == (int)(0.5 * wu.Veh) && wk.Wrecks.Count > 0 && wk.Wrecks.All(r => Geo.Dist(r.X, r.Z, wu.X, wu.Z) < 1500), $"Wracks bleiben liegen ({wk.Wrecks.Count} von {wu.Veh} Fahrzeugen nach 50 % Verlust, {w.Wrecks.Count} im Testkrieg)");
        Check(w.Events.Any(e => e.Text.StartsWith("CSAR:") || e.Text.Contains("taken prisoner")),
            $"CSAR: {w.Events.Count(e => e.Text.StartsWith("CSAR:"))} gerettet, {w.Events.Count(e => e.Text.Contains("taken prisoner"))} gefangen, {w.Downed.Count} noch vermisst");
        var up = Scenario.Create(th, 3);
        up.Objs.RemoveAll(o => o.Decoy); up.Version = 2; Scenario.Upgrade(up);
        Check(w.Objs.Count(o => o.Decoy) == 4 && up.Objs.Count(o => o.Decoy) == 4 && up.Version == new World().Version,
            $"Attrappen (auch in alten Spielständen): {w.Events.Count(e => e.Text.Contains("decoy"))} Meldungen, {w.Objs.Count(o => o.Exposed)} entlarvt");
        Check(w.Events.Count(e => e.Kind == "luft") > 20, $"Luftkrieg findet statt ({w.Events.Count(e => e.Kind == "luft")} Luftereignisse)");
        Check(w.Events.Any(e => e.Text.StartsWith("QRA ")), $"Alarmrotten starten ({w.Events.Count(e => e.Text.StartsWith("QRA "))} QRA){(w.Over == "" ? "" : " – " + w.Over)}");
        var hb = w.Objs.First(o => o.Kind == OK.Airbase && o.Side == Side.Blue); var ht = w.Objs.First(o => o.Kind == OK.Airbase && o.Side == Side.Red);
        var lr = Air.BuildRoute(w, new Flight { Type = "FA-18C_hornet", Task = "Strike" }, hb, new(ht.X, ht.Z), w.Time.AddHours(2), 7000, 0, Air.Grid([new("Test-SAM", ht.X, ht.Z, 30000, 6)]));
        for (int h = 0; h < 12 && !w.Flights.Any(f => !f.Done && f.Side == Side.Blue && Air.Attack.Contains(f.Task)); h++) Sim.Advance(w, w.Time.AddHours(1));   // nachts ggf. kein Angriff geplant
        var asx = w.Flights.Where(f => !f.Done).Select(f => Air.Assess(w, f)).ToList();
        var k0 = w.Flights.First(f => !f.Done && f.Side == Side.Blue && Air.Attack.Contains(f.Task));
        Air.Perfs["Kurzstrecke"] = new(220, 5, 9000);
        var kl = new Flight { Id = -1, Pkg = -1, Side = Side.Blue, Type = "Kurzstrecke", Task = k0.Task, Callsign = "Klon 1", Route = k0.Route };
        w.Flights.Add(kl); var ka = Air.Assess(w, kl); w.Flights.Remove(kl);
        Check(ka.Any(s => s.StartsWith("⚠ Conflict") && s.Contains(k0.Callsign)) && ka[0].StartsWith("⚠ Fuel"), $"Routenprüfung: {ka[0]} · {ka[1]}");
        Check(lr.First(r => r.Kind == "target").Alt < 3000 && lr.Max(r => r.Alt) >= 7000 && asx.All(a => a.Any(s => s.Contains("onflict"))),
            $"Routen: Ziel im SAM-Schirm im Tiefflug ({lr.First(r => r.Kind == "target").Alt:0} m), {asx.Count(a => a.Any(s => s.StartsWith("⚠ Conflict")))} von {asx.Count} Flügen mit Konflikthinweis, {asx.Count(a => a.Any(s => s.Contains("TACAN")))} mit Tankerhinweis");
        int Ev(string s) => w.Events.Count(e => e.Text.Contains(s));
        Check(Ev("Weather front") > 0, $"Wetterfronten ziehen durch ({Ev("Weather front")}×, jetzt {w.Wx.FrontKm:0} km westlich, {w.Wx.FrontKmh:0} km/h)");
        Check(Ev("Commander") > 0 && Ev("relocates") > 0 && Ev("goes dark") + Ev("reported position") > 0, $"Lage reagiert: {Ev("Commander")} Phasenwechsel, {Ev("relocates")} SAM-Stellungswechsel, {Ev("reported position")} Angriffe ins Leere, {Ev("goes dark")}× SAM dunkel vor SEAD");
        var r6 = Scenario.Create(th, 6, at: Dawn); int trains = 0, ships = 0; bool onRail = true; var fromPort = new HashSet<int>();
        bool RailPath(World x, int a, List<int> p) => p.All(n => { var e = Sim.EdgeOf(x, a, n, true); a = n; return e != null; });
        Check(Sim.Path(r6, Scenario.Town(r6, "Batumi").Id, Scenario.Town(r6, "Tbilisi").Id, Side.Blue, true, rail: true) != null
            && Sim.Path(r6, Scenario.Town(r6, "Nal'chik").Id, Scenario.Town(r6, "Vladikavkaz").Id, Side.Red, true, rail: true) != null,
            $"Gleisnetz: {r6.Edges.Count(e => e.Rail)} Strecken, {r6.Edges.Count(e => e.Rail && e.Bridge >= 0)} über Brücken, Batumi–Tiflis und Naltschik–Wladikawkas befahrbar");
        for (int h = 0; h < 12; h++)
        {
            Sim.Advance(r6, r6.Time.AddHours(1));
            trains = Math.Max(trains, r6.Convoys.Count(c => c.Rail)); ships = Math.Max(ships, r6.Convoys.Count(c => c.Ship));
            onRail &= r6.Convoys.Where(c => c.Rail).All(c => RailPath(r6, Sim.StartNode(r6, c), c.Path));
            fromPort.UnionWith(r6.Convoys.Where(c => c.From >= 0 && r6.Objs.First(o => o.Id == c.From).Kind == OK.Port).Select(c => c.From));
        }
        Check(trains > 0 && onRail, $"Werke und Häfen schicken Güterzüge nur über Gleise (bis {trains} gleichzeitig unterwegs)");
        Check(ships >= 3 && !r6.Convoys.Any(c => c.Ship && !c.Navy) && fromPort.Count > 0 && r6.Objs.Where(o => o.Kind == OK.Port && o.Name != "Port Sochi" && !r6.Events.Any(e => e.Text.Contains("bound for " + o.Name))).All(o => fromPort.Contains(o.Id)),   // versenkter Frachter: nichts weiterzuschicken
            $"Frachter laufen in Poti, Batumi und Sotschi ein ({ships} unterwegs, {r6.Convoys.Count(c => c.Ship && !c.Navy)} noch auf See; per Zug weiter: {string.Join(", ", r6.Objs.Where(o => fromPort.Contains(o.Id)).Select(o => $"{o.Name} {o.Health:0.00}"))}; ohne: {string.Join(", ", r6.Objs.Where(o => o.Kind == OK.Port && !fromPort.Contains(o.Id)).Select(o => $"{o.Name} {o.Health:0.00} {o.Stock:0}/{o.Fuel:0}"))})");
        var r10 = Scenario.Create(th, 6); var r14 = Scenario.Create(th, 6, at: new(14, 30, 0)); Sim.Advance(r10, r10.Time.AddHours(1));
        int s10 = r10.Convoys.Count(c => c.Ship && !c.Navy);
        Check(r10.Start.Hour == 10 && r14.Start.TimeOfDay == new TimeSpan(14, 30, 0) && r14.Time == r14.Start && s10 >= 3,
            $"Kriegsbeginn {r10.Start:HH:mm} (einstellbar, z. B. {r14.Start:HH:mm}): {s10} Frachter schon unterwegs (sonst erst um 06 Uhr)");
        bool sea = r6.Objs.Where(o => o.Kind == OK.Port).All(p =>
        {
            return Scenario.SeaGate(r6, p) is not var (gx, gz, _) || Scenario.Lane(th, gx, gz, p.X, p.Z, 8000);
        });
        Check(sea, "Seewege der Frachter liegen auf dem Wasser");
        var cutR = Scenario.Create(th, 6); var bz = cutR.Objs.First(o => o.Kind == OK.Bridge); bz.Health = 0;
        foreach (var e in cutR.Edges.Where(e => e.Rail)) e.Bridge = bz.Id;                     // ganzes Gleisnetz gesperrt
        for (int h = 0; h < 20; h++) { bz.Health = 0; Sim.Advance(cutR, cutR.Time.AddHours(1)); }   // Werk hat nach ~17 h 200 t
        Check(cutR.Convoys.Any(c => c.From >= 0 && cutR.Objs.First(o => o.Id == c.From).Kind == OK.Factory && !c.Rail) && !cutR.Convoys.Any(c => c.Rail && c.Path.Count > 0),
            "Gleis gesperrt: Werke schicken Lkw-Kolonnen über die Straße");
        var g = Scenario.Create(th, 4);
        Sim.Advance(g, g.Time.AddHours(3));
        var mf = g.Flights.FirstOrDefault(f => f.Status == "geplant" && !f.Player && f.TargetKind == "obj" && f.Takeoff > g.Time.AddMinutes(2));
        if (mf != null) { g.Objs.First(o => o.Id == mf.Target).Health = 0; Sim.Advance(g, g.Time.AddMinutes(1)); }
        Check(mf?.Status == "gestrichen", $"ATO streicht Angriff auf zerstörtes Ziel ({mf?.Callsign}: {mf?.Result})");

        var v = Scenario.Create(th, 2);
        foreach (var k in v.Axes.SelectMany(x => x.Keys)) v.Nodes[k].Owner = Side.Blue;
        Sim.Advance(v, v.Time.AddHours(1));
        Check(v.Over.Contains("Blue") && v.Events.Any(e => e.Kind == "kriegsende"), $"Siegbedingung: {v.Over}");

        var a = Scenario.Create(th, 7); var b = Scenario.Create(th, 7);
        Sim.Advance(a, a.Time.AddHours(14)); Sim.Advance(b, b.Time.AddHours(14));
        Check(a.ToJson() == b.ToJson(), "deterministisch (gleicher Startwert = gleicher Verlauf)");
        var c = World.FromJson(a.ToJson(), th);
        Sim.Advance(a, a.Time.AddHours(8)); Sim.Advance(c, c.Time.AddHours(8));
        Check(a.ToJson() == c.ToJson(), "Weiterrechnen nach Laden identisch");

        var t = Scenario.Create(th, 3);
        foreach (var o in t.Objs.Where(o => o.Kind == OK.Bridge)) o.Health = 0;
        foreach (var o in t.Objs.Where(o => o.Kind == OK.Depot && !o.Name.StartsWith("Army"))) o.Stock = 100;
        Sim.Advance(t, t.Time.AddHours(2));
        bool ok = true;
        foreach (var cv in t.Convoys.Where(c => !c.Ship))
        {
            int prev = Sim.StartNode(t, cv);
            foreach (var n in cv.Path)
            {
                var e = Sim.EdgeOf(t, prev, n, cv.Rail);
                if (e != null && Sim.Broken(e, t)) ok = false;
                prev = n;
            }
        }
        Check(ok && t.Convoys.Count + t.Events.Count(e => e.Text.Contains("no supply route")) > 0, $"Konvoys meiden zerstörte Brücken ({t.Convoys.Count} unterwegs, {t.Events.Count(e => e.Text.Contains("no supply route"))} Meldungen ohne Weg)");
        Sim.Advance(t, t.Time.AddHours(12));
        Check(t.Events.Any(e => e.Text.Contains("by engineers")), $"Pioniere reparieren Brücken ({t.Events.Count(e => e.Text.Contains("by engineers"))} von {t.Objs.Count(o => o.Kind == OK.Bridge)} nach 14 h wieder befahrbar)");

        var m = Scenario.Create(th, 5, at: Dawn);
        Sim.Advance(m, m.Time.AddHours(3));
        var pf = m.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && Air.SqnOf(m, f).Player && f.Route.Any(r => r.Kind == "target"))
            .OrderBy(f => f.Type == "FA-18C_hornet" ? 0 : 1).ThenBy(f => f.Takeoff).FirstOrDefault();      // Hornet bevorzugt: Kartusche mittesten
        Check(pf != null, "spielbarer Angriffsflug in der ATO");
        var cap = m.Flights.First(f => f.Status == "geplant" && f.Route.Any(r => r.Kind == "orbit2"));
        TimeSpan Station(Flight f) { int i = f.Route.FindIndex(r => r.Kind == "orbit2"); return f.Route[i].T - f.Route[i - 1].T; }
        var st0 = Station(cap);
        Air.SetRoute(cap, cap.Route.Skip(1).SkipLast(1).Select(r => new Wp { Name = r.Name, Kind = r.Kind, X = r.X + 3000, Z = r.Z, Alt = r.Alt, Spd = r.Spd }).ToList());   // wie aus der Oberfläche: ohne Zeiten
        Check(Station(cap) == st0 && cap.Tot == cap.Route.First(r => r.Kind == "orbit").T, $"Route ändern: Stationszeit bleibt ({st0.TotalMinutes:0} min)");
        if (pf != null) MizTest(m, pf, Check);
        SamTest(Check);
        PlanTest(th, Check);
        MixTest(th, Check);
        HeloTest(th, Check);
        LiftTest(th, Check);
        WarTest(th, Check);
        SwapTest(th, Check);
        OobTest(th, Check);
        JtacTest(th, Check);
        MpTest(th, Check);
        EwTest(th, Check);
        NavyTest(th, Check);
        BdaTest(th, Check);
        CarrierTest(Check);
        AtcTest(Check);
        LuaTest(Check);
        GulfTest(Check);
        SyriaTest(Check);
        AutoTest(Check);

        Console.WriteLine(fail == 0 ? "ALLES OK" : $"{fail} FEHLER");
        return fail == 0 ? 0 : 1;
    }

    /// Zielmix wie in BMS: über drei Tage greifen beide Seiten auch Fabriken, Brücken, Depots, Häfen, Gefechtsstände und Plätze an, nicht nur SAMs und Truppen.
    /// Mechanik-Tests mit Zeitfenstern ab 05 Uhr (Frachter 06, Züge 05/17, Tagesbilanz 06) – die Kampagne selbst beginnt standardmäßig um 10.
    static readonly TimeSpan Dawn = TimeSpan.FromHours(5);

    static void MixTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 7, at: Dawn);
        var seen = new Dictionary<int, string>();
        for (int h = 0; h < 72; h++)
        {
            Sim.Advance(w, w.Time.AddHours(1));
            foreach (var p in w.Pkgs.Where(p => !seen.ContainsKey(p.Id) && Air.Attack.Contains(p.Task)))
                seen[p.Id] = p.TargetKind == "obj" && w.Objs.FirstOrDefault(o => o.Id == p.Target) is { } o ? o.Kind.ToString() : p.Task;
        }
        var mix = seen.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        int strat = new[] { "Factory", "Bridge", "Depot", "Port", "Cp", "Ewr", "Airbase" }.Sum(k => mix.GetValueOrDefault(k));
        check(new[] { "Factory", "Bridge", "Depot", "Cp" }.All(mix.ContainsKey) && strat >= seen.Count * 0.3,
            $"Zielmix 3 Tage: {seen.Count} Angriffe, {strat} strategisch – {string.Join(", ", mix.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}"))}");
        // Kräfteverlauf, neue Staffeln, Piloten lernen, Asse
        int Ev(string s) => w.Events.Count(e => e.Text.Contains(s));
        var q = w.Sqns.First(x => x.Side == Side.Red && x.Roles.Contains("CAP"));
        for (int i = 0; i < 5; i++) Air.Credit(w, q, 0);
        check(w.History.Count == 3 && w.History[^1].RedS < w.History[0].RedS && w.Sqns.Any(x => x.Name == "44th FS Vampire Bats") && w.Sqns.Any(x => x.Name == "277th Bomber Regiment")
              && Ev("gains experience") > 0 && Ev("is now an ace") > 0,
            $"Verlauf {string.Join(" → ", w.History.Select(h => $"{h.BlueG:0}:{h.RedG:0} Boden / {h.BlueA}:{h.RedA} Flz / {h.BlueS}:{h.RedS} SAM"))}; neue Staffeln {Ev("joins the air war")}, " +
            $"Beförderungen {Ev("gains experience")}, Asse {Ev("is now an ace")} ({w.Events.Last(e => e.Text.Contains("is now an ace")).Text})");
        // Schwierigkeit: Gegner (Rot) halbe Luftwaffe und Flugabwehr, Boden 150 % (eigene schwächer), Neulinge, halber Nachschub
        var n = Scenario.Create(th, 7); var d = Scenario.Create(th, 7);
        Scenario.Difficulty(d, 50, 150, 50, -1, 50);
        int Ac(World x, Side s) => x.Sqns.Where(s2 => s2.Side == s && s2.Roles.Contains("CAP")).Sum(s2 => s2.Ac);
        double G(World x, Side s) => x.Units.Where(u => u.Side == s).Sum(u => u.Str);
        double Sk(World x) => x.Sqns.Where(s2 => s2.Side == Side.Red).SelectMany(s2 => s2.Pilots).Average(p => p.Skill);
        int Sams(World x) => x.Objs.Count(o => o.Side == Side.Red && o.Kind == OK.Sam && !o.Decoy);
        check(Ac(d, Side.Red) < Ac(n, Side.Red) * 0.6 && G(d, Side.Blue) < G(n, Side.Blue) * 0.75 && Sams(d) < Sams(n) * 0.6 && Sk(d) < Sk(n) && Ac(d, Side.Blue) == Ac(n, Side.Blue) && d.History.Count == 1,
            $"Schwierigkeit: rote Jäger {Ac(n, Side.Red)} → {Ac(d, Side.Red)}, blaue Bodenstärke {G(n, Side.Blue):0.0} → {G(d, Side.Blue):0.0}, rote SAMs {Sams(n)} → {Sams(d)}, Können {Sk(n):0.00} → {Sk(d):0.00}");
    }

    /// Syrien: Plätze aus den Rollwegnetzen, Seiten nach Grenzen (Libanon neutral), vier Achsen, zwei Tage Krieg, Mission mit Syrien/Israel/Türkei.
    static void SyriaTest(Action<bool, string> check)
    {
        if (!Theater.Installed("Syria")) { Console.WriteLine("       (Karte Syrien nicht installiert – übersprungen)"); return; }
        var set = Scenario.All.First(s => s.Map == "Syria");
        var w = Scenario.Create(Theater.Get("Syria"), 1, set.Id);
        string Side0(int id) => w.Base(id)?.Side.De() ?? "neutral";
        check(Side0(7) == "Red" && Side0(21) == "Red" && Side0(30) == "Blue" && Side0(16) == "Blue" && Side0(6) == "neutral" && w.Units.All(u => w.Nodes[u.Node].Owner == u.Side) && w.Axes.All(a => Sim.Front(w, a) != null)
              && !Scenario.All.Any(s => s.Id == "auto-Syria"),
            $"{set.Name}: {w.Th.Bases.Count} Plätze (Damaskus {Side0(7)}, Hmeimim {Side0(21)}, Ramat David {Side0(30)}, Incirlik {Side0(16)}, Beirut {Side0(6)}), {w.Nodes.Count} Orte, {w.Objs.Count} Ziele, {w.Units.Count} Verbände, {w.Sqns.Count} Staffeln, Achsen {string.Join(", ", w.Axes.Select(a => $"{a.Name} {(Sim.Front(w, a) is var (r, b) ? w.Nodes[r].Name + "/" + w.Nodes[b].Name : "-")}"))}");
        var sw = Stopwatch.StartNew();
        Sim.Advance(w, w.Time.AddDays(2));
        check(w.Over == "" && w.Events.Count(e => e.Kind == "front") > 10 && w.Sqns.Where(q => q.Country == "Turkey").Any(q => q.Pilots[0].Name.Split(' ')[^1] is "Yilmaz" or "Kaya" or "Demir" or "Sahin" or "Celik" or "Yildiz" or "Aydin" or "Ozturk" or "Arslan" or "Dogan" or "Kilic" or "Aslan" or "Cetin" or "Kurt" or "Koc" or "Ozdemir"),
            $"{set.Name}: zwei Tage in {sw.Elapsed.TotalSeconds:0.0} s, {w.Events.Count(e => e.Kind == "luft")} Luftereignisse, {w.Events.Count(e => e.Kind == "front")} Frontmeldungen, z. B. {w.Events.LastOrDefault(e => e.Kind == "front")?.Text}{(w.Over == "" ? "" : " – " + w.Over)}");
        var pf = w.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && Air.SqnOf(w, f).Player && f.Task is not ("Antiship" or "CAS") && Air.Attack.Contains(f.Task) && f.Route.Any(r => r.Kind == "target")).MinBy(f => f.Takeoff)
            ?? (Air.Offers(w).FirstOrDefault(o => o.Task is not ("CAS" or "CAP" or "Antiship")) is { } of ? Air.TakeOffer(w, of.Task, of.Kind, of.Id, of.Sqn) : null);
        if (pf == null) { check(false, $"{set.Name}: kein spielbarer Angriffsflug"); return; }
        pf.Player = true; pf.Start = "kalt";
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestSyria");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Miz.Fly(w, dir);
        using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
        string Entry(string n) { using var rd = new StreamReader(z.GetEntry(n)!.Open()); return rd.ReadToEnd(); }
        var (ms, data) = (Entry("mission"), Entry("l10n/DEFAULT/DcsCampaignData.lua"));
        var types = new[] { "Merkava_Mk4", "leopard-2A4", "Challenger2", "T155_Firtina", "BMP-1", "T-55" }.Where(data.Contains).ToList();
        check(ms.Contains("\"Syria\"") && ms.Contains("[2] = 47") && ms.Contains("[3] = 3") && types.Count > 0,
            $"{set.Name}: Mission gebaut ({pf.Callsign} {pf.Type} {pf.Task} ab {w.Objs.First(o => o.Id == Air.SqnOf(w, pf).Base).Name}, Bodenverbände {string.Join(", ", types)})");
        Theater.Get("Caucasus");
    }

    /// Persischer Golf: jede Startlage aufbauen, zwei Tage rechnen, eine Mission bauen. Danach wieder Kaukasus (Projektion ist global).
    /// Kriegsbeginn: Kurz- und Mittelstrecken-SAMs (auch Attrappen) stehen nicht an der Grenze – ihr Ring reicht höchstens 10 km über die Front.
    /// Roll-back: die ersten DEAD-Pakete gehen auf frontnahe SAMs, nicht auf tiefe im Schirm; Jäger auf der Route = Jagdvorstoß.
    static void PlanTest(Theater th, Action<bool, string> check)
    {
        var dead = new List<string>(); int deep = 0, sweeps = 0, cas = 0, front = 0, seadOn = 0, seadShort = 0;
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var w = Scenario.Create(th, seed); var seen = new HashSet<int>();
            for (int h = 0; h < 16; h++) { Sim.Advance(w, w.Time.AddMinutes(30)); front += Over(w, seen, f => f.Task == "CAS"); }
            var blue = w.Nodes.Where(n => n.Owner == Side.Blue).ToList();
            foreach (var p in w.Pkgs.Where(p => p.Side == Side.Blue && p.Task == "DEAD").OrderBy(p => p.Id).Take(2))
            {
                var o = w.Objs.First(o => o.Id == p.Target);
                double d = blue.Min(n => Geo.Dist(n.X, n.Z, o.X, o.Z)) / 1000;
                dead.Add($"{o.Name} {d:0} km"); if (d > 70) deep++;
            }
            sweeps += w.Flights.Count(f => f.Task == "Sweep" && f.Pkg >= 0 && w.Pkgs.Any(p => p.Id == f.Pkg && p.Task != "Sweep"));
            cas += w.Flights.Count(f => f.Side == Side.Blue && f.Task == "CAS");
            foreach (var f in w.Flights.Where(f => f.Task == "CAS" && f.TargetKind == "node" && f.Route.Any(r => r.Kind == "orbit2")))   // abgebrochene haben keine Station mehr
            {
                var sd = w.Flights.FirstOrDefault(x => x.Pkg == f.Pkg && x.Task == "SEAD");
                if (sd == null) continue;
                seadOn++;
                if (sd.Route.FirstOrDefault(r => r.Kind == "orbit2") is not { } e || e.T < f.Route.First(r => r.Kind == "orbit2").T) seadShort++;
            }
        }
        check(dead.Count > 0 && deep == 0, $"Roll-back: erste DEAD-Ziele frontnah ({string.Join(", ", dead)}); Jagdvorstöße vor Paketen {sweeps}, CAS-Flüge Blau {cas}");
        // Kommandeur-Prioritäten: DEAD aus, CAS hoch, Schwerpunkt Achse 0 → keine neuen DEAD-Pakete, Angriffe näher an der Schwerpunkt-Front
        var pw = Scenario.Create(th, 2); var bc = pw.CmdOf(Side.Blue);
        var basew = Scenario.Create(th, 2);
        bc.Prio["DEAD"] = 0; bc.Prio["CAS"] = 2; bc.Focus = 0;
        int ids0 = pw.Pkgs.Count == 0 ? 0 : pw.Pkgs.Max(x => x.Id);
        Sim.Advance(pw, pw.Time.AddHours(8)); Sim.Advance(basew, basew.Time.AddHours(8));
        double Near0(World x) { var l = x.Pkgs.Where(k => k.Side == Side.Blue && Air.Attack.Contains(k.Task) && k.Id > ids0 && k.Name.Contains("Package")).Select(k => x.Flights.Where(f => f.Pkg == k.Id).Select(f => f.Route.FirstOrDefault(r => r.Kind is "target" or "orbit")).FirstOrDefault(r => r != null)).Where(r => r != null).ToList();
            return Sim.Front(x, x.Axes[0]) is var (rf, bf) && l.Count > 0 ? l.Average(r => Geo.Dist(x.Nodes[bf].X, x.Nodes[bf].Z, r!.X, r.Z) < 60000 ? 1.0 : 0) : 0; }   // Anteil im Schwerpunkt-Umkreis (Weight: 60 km)
        double Share(World x, string t) => (double)x.Pkgs.Count(k => k.Side == Side.Blue && k.Task == t) / Math.Max(1, x.Pkgs.Count(k => k.Side == Side.Blue && Air.Attack.Contains(k.Task)));
        int deadN = pw.Pkgs.Count(k => k.Side == Side.Blue && k.Task == "DEAD" && k.Id > ids0);
        double casN = Share(pw, "CAS"), casB = Share(basew, "CAS");
        check(deadN == 0 && casN >= casB && Near0(pw) >= Near0(basew), $"Prioritäten: DEAD aus → {deadN} DEAD-Pakete, CAS hoch → {casN:P0} der Angriffe CAS (ohne {casB:P0}), Schwerpunkt {pw.Axes[0].Name}: {Near0(pw):P0} der Ziele bis 60 km an der Front (ohne {Near0(basew):P0})");
        check(cas > 0 && front == 0 && seadShort == 0, $"CAS-Station diesseits der Front ({front} drüben), SEAD bei {seadOn} CAS-Paketen über die ganze Station ({seadShort} zu kurz)");
    }

    /// Hubschrauber: Staffeln (alte Spielstände nachgerüstet), CAS tief auf der eigenen Seite ohne SEAD, Mission mit Gruppe „helicopter“;
    /// abgeschossener Pilot → CSAR-Einsatz der Rettungsstaffel → gerettet (oder die Rettung wird selbst abgeschossen).
    /// Neue CAS-Flüge mit Wartepunkt jenseits der Front – geprüft kurz nach der Planung (danach kann die Front wandern).
    static int Over(World w, HashSet<int> seen, Func<Flight, bool> which) =>
        w.Flights.Count(f => which(f) && f.TargetKind == "node" && f.Route.FirstOrDefault(r => r.Kind == "orbit") is { } o && seen.Add(f.Id) && Air.OwnerAt(w, o.X, o.Z) != f.Side);

    /// Startlage Gegenangriff, Kriegsphasen (ROE), Luftlandung, Verbündeter, Scud-Jagd.
    static void WarTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 1, "kaukasus-counter");
        string Own(string t) => Scenario.Town(w, t).Owner.De();
        check(Own("Zugdidi") == "Red" && Own("Gori") == "Red" && w.Units.All(u => w.Nodes[u.Node].Owner == u.Side) && w.Axes.All(a => Sim.Front(w, a) != null) && w.Objs.Count(o => o.Kind == OK.Tel) >= 4,
            $"Startlage {w.S.Name}: Zugdidi {Own("Zugdidi")}, Gori {Own("Gori")}, {w.Objs.Count(o => o.Kind == OK.Tel)} Raketenwerfer");
        var fac = w.Objs.First(o => o.Kind == OK.Factory);
        bool before = Sim.Roe(w, fac);
        Sim.Escalate(w, 1, "Test");
        check(!before && Sim.Roe(w, fac) && w.Events.Any(e => e.Text.StartsWith("Escalation")), "Kriegsphasen: Werke erst ab Phase 2 Ziel");
        var ax = w.Axes[0];
        (ax.Op, ax.OpSide, w.Time) = ("Angriff", Side.Blue, w.Time.Date.AddHours(10));
        for (int i = 0; i < 300 && !w.Flights.Any(f => f.TargetKind == "drop"); i++) Air.Assault(w, Side.Blue);
        var lf = w.Flights.FirstOrDefault(f => f.TargetKind == "drop" && f.Task == "Transport");
        if (lf != null) Sim.Drop(w, lf);
        check(lf != null && w.Nodes[lf.Target].Owner == Side.Blue && w.Units.Any(u => u.Name.StartsWith("Air assault")),
            $"Luftlandung: {lf?.Callsign} ({lf?.Type}) auf {(lf != null ? w.Nodes[lf.Target].Name : "-")}, {w.Flights.Count(f => f.TargetKind == "drop")} Flüge im Paket");
        Sim.Escalate(w, 2, "Test");
        Sim.Advance(w, w.Time.AddHours(24));
        var launch = w.Events.FirstOrDefault(e => e.Text.Contains("launcher") && e.Text.StartsWith("Missile"));
        check(w.JoinBlue >= 0 && w.Events.Any(e => e.Text.Contains("Turkish 1st Armored")) && launch != null,
            $"Verbündeter ab Stunde {w.JoinBlue}, Türkei rückt an; Werfer: {launch?.Text ?? "kein Start"}");
        check(w.Pkgs.Any(p => p.Side == Side.Blue && p.TargetKind == "obj" && w.Objs.Any(o => o.Id == p.Target && o.Kind == OK.Tel)),
            $"Scud-Jagd: {w.Pkgs.Count(p => p.Side == Side.Blue && p.TargetKind == "obj" && w.Objs.Any(o => o.Id == p.Target && o.Kind == OK.Tel))} Pakete gegen Werfer");
    }

    /// Lufttransport: knapper Frontplatz → Transporter fliegt vom Heimatplatz hin, landet dort, Fracht kommt an; spielbare C-130J landet in der Mission am Zielplatz.
    static void LiftTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 3);
        var dst = w.Objs.First(o => o.Kind == OK.Airbase && o.BaseId == 23);   // Senaki: Frontplatz mit Staffeln
        dst.Stock = dst.Fuel = 40;
        Sim.Advance(w, w.Time.AddHours(1));
        var lf = w.Flights.FirstOrDefault(f => f.Task == "Transport" && f.Target == dst.Id);
        var land = lf?.Route[^1];
        Sim.Advance(w, w.Time.AddHours(4));
        var ev = w.Events.FirstOrDefault(e => e.Text.StartsWith("Airlift") && e.Text.Contains(dst.Name));
        check(lf != null && land != null && Geo.Dist(land.X, land.Z, dst.X, dst.Z) < 1 && lf.Route.Any(x => x.Kind == "target") && ev != null,
            $"Lufttransport: {lf?.Callsign} ({lf?.Type}) von {lf?.Route[0].Name} nach {dst.Name}, landet dort – {ev?.Text ?? lf?.Result}");
        var c = Scenario.Create(th, 3);
        var cd = c.Objs.First(o => o.Kind == OK.Airbase && o.BaseId == 23); cd.Stock = cd.Fuel = 40;
        Sim.Advance(c, c.Time.AddMinutes(30));
        if (c.Flights.FirstOrDefault(f => f.Task == "Transport" && f.Type == "C-130J-30" && !f.Done) is not { } pf) { check(false, "Lufttransport: kein C-130J-Flug geplant"); return; }
        pf.Player = true;
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-Lift");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Miz.Fly(c, dir);
        using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
        using var rd = new StreamReader(z.GetEntry("mission")!.Open()); var ms = rd.ReadToEnd();
        var dest = c.Objs.First(o => o.Id == pf.Target);
        check(ms.Contains("C-130J-30") && Regex.IsMatch(ms, $"\"airdromeId\"\\]\\s*=\\s*{dest.BaseId}\\b"), $"Mission mit spielbarer C-130J {pf.Callsign}: Landung in {dest.Name} (Platz {dest.BaseId})");
    }

    /// Muster wechseln: ein geplanter Flug geht an die F-16C – gleicher Auftrag und TOT, Route ab ihrem Platz, Mission mit F-16C als Spieler.
    /// BDA: Angriffe melden unbestätigt (Piloten überschätzen), die KI plant danach Fotoaufklärung, die den wahren Zustand bestätigt.
    static void BdaTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 4);
        Sim.Advance(w, w.Time.AddHours(24));
        var claims = w.Intel(Side.Blue).Count(k => k.Value.Claim);
        var recon = w.Flights.Where(f => f.Task == "Recon").ToList();
        var photos = w.Events.Where(e => e.Text.Contains(" recon: Photos of ")).ToList();
        var ok = w.Intel(Side.Blue).Where(k => k.Value.Src.StartsWith("Recon ") && k.Key[0] == 'O').All(k => !k.Value.Claim);   // Foto = bestätigt (danach kann instand gesetzt werden)
        check(claims + photos.Count > 0 && recon.Count > 0 && photos.Count > 0 && ok,
            $"BDA: {claims} unbestätigte Meldungen offen, {recon.Count} Aufklärungsflüge, {photos.Count} bestätigt – z. B. {photos.FirstOrDefault()?.Text}");

        // Teile (Features): Piste sperrt den Platz, Shelter-Treffer kosten Maschinen, Instandsetzung Piste zuerst, DCS-Gebäude → Teil
        var p = Scenario.Create(th, 5);
        var ab = p.Objs.First(o => o.Kind == OK.Airbase && o.BaseId > 0 && p.Sqns.Any(q => q.Base == o.Id && q.Ac > 4));
        int ac = p.Sqns.Where(q => q.Base == ab.Id).Sum(q => q.Ac);
        var rwy = Air.HitPart(p, ab, 0, 0.8);
        bool shut = !Air.Open(ab) && ab.Health > 0.6 && Air.AutoPart(p, ab) == 1;
        var shl = Air.HitPart(p, ab, 1, 0.9);
        int lost = ac - p.Sqns.Where(q => q.Base == ab.Id).Sum(q => q.Ac);
        double fuel0 = ab.Fuel;
        Air.HitPart(p, ab, 3, 1);
        for (int i = 0; i < 3; i++) ab.Fix(0.06);
        bool first = Air.Open(ab, 0.35) && ab.P("Shelters") < 0.2;
        ab.Health = ab.Health;                                                    // Setter verteilt nichts, wenn gleich
        var dp = p.Objs.First(o => o.Kind == OK.Depot);
        double s0 = dp.Stock;
        p.Mission = "tok"; p.MissionStart = p.Time.Date;
        Bridge.Feed(p, [$"DCSCAMP|tok|DEAD|{(p.Time - p.Time.Date).TotalSeconds:0}|O{dp.Id}-1/{Scenario.Statics[OK.Depot].Length}|{dp.X:0}|{dp.Z:0}"]);
        p.Mission = "";
        check(shut && lost > 0 && fuel0 > 0 && ab.Fuel < fuel0 * 0.5 && first && dp.P("Warehouses") < 0.8 && dp.P("Fuel tanks") > 0.9 && dp.Stock < s0,
            $"Teile: {ab.Name} {rwy} → geschlossen, {shl}; Tanklager brennt, Instandsetzung Piste zuerst (offen nach 3 h, Shelters {ab.P("Shelters"):P0}); DCS-Gebäude 1 → Warehouses {dp.P("Warehouses"):P0}");

        // Waffenbestand: ohne Lenkwaffen fliegt die KI mit Freifallbomben, der Spieler muss umrüsten; Landung bringt Raketen zurück, Nachschub stündlich
        var a = Scenario.Create(th, 6, at: Dawn);
        string Ag(Flight f) => Air.Need(f.Load).Keys.FirstOrDefault(k => !k.StartsWith("AAM")) ?? "";   // Luft-Boden-Klasse der Beladung
        List<Flight> Cands() => a.Flights.Where(f => f.Status == "geplant" && Ag(f) != "" && f.Takeoff > a.Time.AddMinutes(20)).ToList();
        Sim.Advance(a, a.Time.AddHours(4));
        for (int i = 0; i < 16 && Cands().Count < 2; i++) Sim.Advance(a, a.Time.AddMinutes(30));
        var sf = Cands();
        check(sf.Count >= 2, $"Waffen: {sf.Count} geplante Flüge mit Luft-Boden-Waffen");
        if (sf.Count < 2) return;
        var (ai, pl) = (sf[0], sf[1]);
        string c1 = Ag(ai), c2 = Ag(pl);
        var ib = a.Objs.First(o => o.Id == Air.SqnOf(a, ai).Base);
        Air.Stores(a, ib)[c1] = 0; Air.Stores(a, a.Objs.First(o => o.Id == Air.SqnOf(a, pl).Base))[c2] = 0;
        pl.Player = true;
        string l0 = ai.Load;
        bool aiOk = Air.Launch(a, ai), plOk = Air.Launch(a, pl);
        var cap = a.Flights.First(f => f.Status == "geplant" && f.Task == "CAP" && Air.A2aCount(f.Load) > 0);   // ungeschossene Raketen kommen zurück
        var cb = a.Objs.First(o => o.Id == Air.SqnOf(a, cap).Base);
        int aam = Air.Stores(a, cb).Where(kv => kv.Key.StartsWith("AAM")).Sum(kv => kv.Value);
        Air.Launch(a, cap); int out_ = aam - Air.Stores(a, cb).Where(kv => kv.Key.StartsWith("AAM")).Sum(kv => kv.Value);
        cap.Missiles -= 2; Air.LandFlight(a, cap);
        int back = Air.Stores(a, cb).Where(kv => kv.Key.StartsWith("AAM")).Sum(kv => kv.Value) - (aam - out_);
        bool kept = !plOk && pl.Status == "geplant" && pl.Result.Contains($"{c2} 0/");
        string pr = pl.Result;
        Sim.Advance(a, a.Time.AddMinutes(61));
        check((aiOk ? !Air.Need(ai.Load).ContainsKey(c1) : ai.Status == "gestrichen" && ai.Result.Contains(c1)) && kept
              && out_ > 2 && back == out_ - 2 && Air.Stores(a, ib)[c1] > 0,
            $"Waffen: KI ohne {c1}: {(aiOk ? $"{l0} → {ai.Load}" : ai.Result)}; Spieler ohne {c2}: „{pr}“, Flug bleibt geplant; CAP startet mit {out_} Luft-Luft, 2 verschossen, {back} zurück; Nachschub {c1} {Air.Stores(a, ib)[c1]}");
    }

    static void SwapTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 3);
        Sim.Advance(w, w.Time.AddMinutes(30));
        var q = w.Sqns.First(q => q.Type == "F-16C_50" && q.Side == Side.Blue);
        var fly = w.Sqns.Where(x => x.Side == Side.Blue && x.Player).Select(x => x.Type).Distinct().ToList();
        foreach (var f in w.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && f.Sqn != q.Id && q.Roles.Contains(f.Task == "DEAD" ? "Strike" : f.Task)).ToList())
        {
            var (old, tot, load) = (f.Type, f.Tot, f.Load);
            try { Air.Swap(w, f, q.Id); } catch (ArgumentException) { continue; }
            var b = w.Objs.First(o => o.Id == q.Base);
            f.Player = true;
            var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-Swap");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Miz.Fly(w, dir);
            using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
            using var rd = new StreamReader(z.GetEntry("mission")!.Open()); var ms = rd.ReadToEnd();
            check(f.Type == "F-16C_50" && f.Tot == tot && Geo.Dist(f.Route[0].X, f.Route[0].Z, b.X, b.Z) < 1 && f.Load != "" && ms.Contains("F-16C_50") && fly.Contains("F-16C_50") && fly.Contains("A-10C_2"),
                $"Muster wechseln: {f.Callsign} {f.Task} {old} → F-16C ab {b.Name}, TOT {tot:HH:mm} bleibt, {load} → {f.Load}; spielbar: {fly.Count} Muster");
            var t = w.Flights.First(x => x.Type == "F-14B" && x.Status == "geplant");
            var (tr, tl) = (t.Route, t.Load);
            Air.Retype(w, t, "F-14A-135-GR");
            bool bad = false; try { Air.Retype(w, t, "F-16C_50"); } catch (ArgumentException) { bad = true; }
            check(t.Type == "F-14A-135-GR" && t.Route == tr && t.Load != "" && bad && Air.Variants(th, "Su-25T").Contains("Su-25") && Air.Variants(th, "F-14B").Length == 5,
                $"Variante: {t.Callsign} F-14B → F-14A, Route bleibt, {tl} → {t.Load}; F-16C abgelehnt; Varianten: {string.Join(", ", new[] { "F-14B", "A-10C_2", "Su-25T", "MiG-29S", "Su-27" }.Select(x => string.Join("/", Air.Variants(th, x))))}");
            var (ai, g) = (w.Flights.First(x => x.Side == Side.Blue && x.Status == "geplant" && !x.Player && x.Sqn != q.Id && x != t), w.Flights.Last(x => x.Side == Side.Blue && x.Status == "geplant" && !x.Player && x.Sqn != q.Id && x != t));
            Air.Swap(w, ai, q.Id);
            q.Ac = w.Flights.Where(x => x.Sqn == q.Id && x.Status == "geplant").Sum(x => x.N);   // Staffel ausgebucht
            var before = w.Flights.Where(x => x.Sqn == q.Id && x.Status == "geplant" && !x.Player).ToList();
            g.Player = true;
            Air.Swap(w, g, q.Id);
            var moved = before.FirstOrDefault(x => !w.Flights.Contains(x) || x.Sqn != q.Id);
            check(ai != g && g.Sqn == q.Id && moved != null && Air.Free(w, q) >= 0,
                $"Frei umplanen: {g.Callsign} {g.Task} nimmt die ausgebuchte {q.Name}, {moved?.Callsign} {moved?.Task} → {(moved == null ? "-" : w.Flights.Contains(moved) ? Air.SqnOf(w, moved).Name : "gestrichen")}");
            return;
        }
        check(false, "Muster wechseln: kein Flug für die F-16C gefunden");
    }

    /// Mehrspieler: Host gibt ein Paket frei, Mitspieler nehmen sich je einen Flug und dürfen nur den eigenen ändern
    static void MpTest(Theater th, Action<bool, string> check)
    {
        var w = Web.W = Scenario.Create(th, 5);
        Sim.Advance(w, w.Time.AddMinutes(30));
        var pk = w.Flights.Where(f => f.Side == w.Player && f.Status == "geplant" && w.Sqns.First(q => q.Id == f.Sqn).Player).GroupBy(f => f.Pkg).MaxBy(g => g.Count());
        if (pk == null || pk.Count() < 2) { check(false, "Mehrspieler: kein Paket mit zwei spielbaren Flügen"); return; }
        var q0 = new System.Collections.Specialized.NameValueCollection();
        string Try(string p, string json, string? you) { try { Web.Api(p, System.Text.Json.Nodes.JsonNode.Parse(json)!, q0, you); return "ok"; } catch (ArgumentException e) { return e.Message; } }
        string early = Try("take", $"{{\"Flight\":{pk.First().Id}}}", "Alice");
        Try("takepkg", $"{{\"Pkg\":{pk.Key}}}", null);
        var (a, b) = (pk.First(), pk.Skip(1).First());
        string ta = Try("take", $"{{\"Flight\":{a.Id}}}", "Alice"), tb = Try("take", $"{{\"Flight\":{a.Id}}}", "Bob"), tb2 = Try("take", $"{{\"Flight\":{b.Id}}}", "Bob");
        string ea = Try("flight", $"{{\"Flight\":{a.Id},\"Start\":\"heiß\"}}", "Alice"), eb = Try("flight", $"{{\"Flight\":{b.Id},\"Start\":\"heiß\"}}", "Alice"), sp = Try("speed", "{\"Speed\":4}", "Alice");
        string da = Try("drop", $"{{\"Flight\":{a.Id}}}", "Alice");
        check(early != "ok" && w.Mp && pk.All(f => f.Player && f.Crew == new string('P', f.N)) && ta == "ok" && tb.Contains("Alice") && tb2 == "ok" && b.Owner == "Bob"
              && ea == "ok" && a.Start == "heiß" && eb != "ok" && b.Start != "heiß" && sp != "ok" && w.Speed == 0 && da == "ok" && a.Player && a.Owner == "",
            $"Mehrspieler: Paket {pk.Key} frei ({pk.Count()} Flüge, alle Client); vorher „{early}“; Bob auf Alices Flug „{tb}“; Alice an Bobs Flug „{eb}“; Tempo „{sp}“; Alice gibt frei → offen");
        // Dedicated Server: Mission läuft in einem anderen Profil – dessen dcs.log hat das Token
        var dir = Directory.CreateTempSubdirectory("DcsCampaign-Logs").FullName;
        string[] logs = [Path.Combine(dir, "client.log"), Path.Combine(dir, "server.log"), Path.Combine(dir, "old.log")];
        w.Mission = "tok42";
        File.WriteAllText(logs[0], "INFO EDCORE\n"); File.WriteAllText(logs[1], new string('x', 2 << 20) + "\nSCRIPTING: DCSCAMP|tok42|START|21600\n"); File.WriteAllText(logs[2], "DCSCAMP|tok41|START|1\n");
        var found = Bridge.Find(w, logs[0], logs);
        w.Mission = "tok43";
        check(found == logs[1] && Bridge.Find(w, logs[0], logs) == null && Bridge.Find(w, logs[1], logs) == null, $"Dedicated Server: dcs.log mit dem Token gefunden ({Path.GetFileName(found ?? "–")}), fremde/alte Missionen nicht");
        Directory.Delete(dir, true);
    }

    /// Störer: SAM schießt auf 45 % seiner Reichweite – mit Störer im Paket nicht (Burn-through 40 %); Pakete bekommen Störer
    static void EwTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 7); Sim.Advance(w, w.Time.AddMinutes(30));
        var o = w.Objs.Where(o => o.Kind == OK.Sam && o.Side == Side.Red && o.Range > 30000).MaxBy(o => o.Range);
        var bl = w.Flights.Where(f => f.Side == Side.Blue && f.Sqn >= 0).Take(2).ToList();
        if (o == null || bl.Count < 2) { check(false, "EW: keine SAM oder Flüge"); return; }
        foreach (var f in w.Flights) f.Done = true;
        int Shots(bool jam)
        {
            int n = 0;
            for (int i = 0; i < 80; i++)
            {
                o.Health = 1; o.Missiles = 8; o.QuietUntil = DateTime.MinValue; o.InDcs = false; o.Decoy = false;
                foreach (var (f, task, d) in new[] { (bl[0], "Escort", o.Range * 0.45), (bl[1], jam ? "Jammer" : "Recon", o.Range * 1.3) })
                {
                    var p = Geo.Move(new P(o.X, o.Z), 90, d);
                    f.Done = false; f.Status = "in der Luft"; f.Task = task; f.Alive = f.N = 2; f.Missiles = 0; f.Arm = 0; f.InDcs = false; f.Busy = DateTime.MinValue; f.X = p.X; f.Z = p.Z; f.Alt = 7000;
                }
                Air.Engage(w); n += 8 - o.Missiles;
            }
            return n;
        }
        int free = Shots(false), jammed = Shots(true);
        var w2 = Scenario.Create(th, 7);
        Sim.Advance(w2, w2.Time.AddHours(8));
        var js = w2.Flights.Where(f => f.Task == "Jammer").ToList();
        check(free > 0 && jammed == 0 && js.Count > 0 && js.All(f => f.Load != "" && f.Route.Any(r => r.Kind == "orbit")),
            $"EW: {o.Sys} auf {o.Range * 0.45 / 1000:0} km – ohne Störer {free} Schüsse, mit Störer {jammed}; 8 h Krieg: {js.Count} Störerflüge, z. B. {js.FirstOrDefault()?.Type} „{js.FirstOrDefault()?.Load}“");
    }

    /// Seekrieg: je Seite ein Verband auf See; Blockade versenkt Frachter; Seezielpaket feuert außerhalb der Schiffsflugabwehr und versenkt Schiffe; Schiffsflugabwehr schießt
    static void NavyTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 7);
        var fl = w.Convoys.Where(c => c.Navy).ToList();
        var red = fl.FirstOrDefault(c => c.Side == Side.Red);
        check(fl.Count == 2 && red != null && fl.All(c => th.Water(c.X, c.Z)), $"See: {string.Join(", ", fl.Select(c => $"{c.Name} ({string.Join("/", c.Types)}, Flugabwehr {Sim.NavyRange(c) / 1000:0} km, {c.Missiles} FK)"))}");
        if (red == null) return;
        var fr = new Convoy { Id = w.NewId(), Side = Side.Blue, From = -1, To = w.Objs.First(o => o.Kind == OK.Port && o.Side == Side.Blue).Id, Tons = 300, Fuel = 300, Trucks = 8, Trucks0 = 8, Ship = true };
        w.Convoys.Add(fr);
        int h = 0;
        for (; h < 12 && w.Convoys.Contains(fr); h++)
        {
            fr.X = red.X + 20000; fr.Z = red.Z;
            w.Intel(Side.Red)[$"C{fr.Id}"] = new Sight { X = fr.X, Z = fr.Z, T = w.Time, Src = "test", Str = 8 };
            Sim.Patrol(w, red, 1);
        }
        check(!w.Convoys.Contains(fr) && w.Events.Any(e => e.Kind == "see" && e.Text.Contains("sunk by " + red.Name)), $"Blockade: Frachter nach {h} h versenkt, {red.Name} noch {red.Missiles} FK");
        var hq = w.Sqns.First(q => q.Side == Side.Blue && q.Type == "FA-18C_hornet");
        var pk = Air.Package(w, Side.Blue, "Antiship", "convoy", red.Id, new P(red.X, red.Z), "test", hq.Id);
        var core = pk == null ? null : w.Flights.FirstOrDefault(f => f.Pkg == pk.Id && f.Task == "Antiship");
        var tp = core?.Route.FirstOrDefault(r => r.Kind == "target");
        double sd = tp == null ? 0 : Geo.Dist(tp.X, tp.Z, red.X, red.Z);
        check(core != null && tp != null && Air.Need(core.Load).ContainsKey("AShM") && sd > Sim.NavyRange(red), $"Seezielpaket: {core?.N}× {core?.Type} „{core?.Load}“, Abschuss {sd / 1000:0} km vor {red.Name}");
        if (core == null || tp == null) return;
        int n0 = red.Trucks, sunk = 0;
        for (int i = 0; i < 20; i++)
        {
            red.Trucks = n0; if (!w.Convoys.Contains(red)) w.Convoys.Add(red);
            core.Alive = core.N; core.X = tp.X; core.Z = tp.Z; core.Status = "im Einsatz";
            Air.Strike(w, core); sunk += n0 - red.Trucks;
        }
        check(sunk > 0 && sunk < 20 * n0, $"Seezielangriff: 20 Angriffe → {sunk} Schiffe versenkt");
        red.Trucks = n0; if (!w.Convoys.Contains(red)) w.Convoys.Add(red);
        foreach (var f in w.Flights) f.Done = true;
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Sam)) o.Missiles = 0;
        var bl = w.Flights.First(f => f.Side == Side.Blue && f.Sqn >= 0 && f != core);
        int lost = 0;
        for (int i = 0; i < 200; i++)
        {
            var p = Geo.Move(new P(red.X, red.Z), 90, Sim.NavyRange(red) * 0.5);
            bl.Done = false; bl.Status = "in der Luft"; bl.Task = "Escort"; bl.Alive = bl.N = 2; bl.InDcs = false; bl.Busy = DateTime.MinValue; bl.X = p.X; bl.Z = p.Z; bl.Alt = 6000;
            Air.Engage(w); lost += 2 - bl.Alive;
        }
        check(lost > 0, $"Schiffsflugabwehr {red.Name}: {lost} Abschüsse in 200 Minuten");
        var w2 = Scenario.Create(th, 7);
        Sim.Advance(w2, w2.Time.AddDays(2));
        var see = w2.Events.Where(e => e.Kind == "see").ToList();
        int ash = w2.Events.Count(e => e.Kind == "luft" && e.Text.Contains(" attacks ") && e.Text.Contains(" anti-ship missiles"));
        check(ash > 0 && see.Count > 0, $"2 Tage Krieg: {ash} Seezielangriffe, {see.Count} Seeereignisse, z. B. „{see.LastOrDefault()?.Text}“; Flotten: {string.Join(", ", w2.Convoys.Where(c => c.Navy).Select(c => $"{c.Name} {c.Trucks} Schiffe/{c.Missiles} FK"))}");
    }

    /// Luftwaffen aus dem Startdialog: Muster tauschen, Staffel dazu und weg – Rollen, Platz, Planung.
    static void OobTest(Theater th, Action<bool, string> check)
    {
        var set = Scenario.Get("kaukasus");
        int sw = Array.FindIndex(set.Sqns, q => q.S == Side.Blue && Air.Flyable(q.Type) && !Air.Helo(q.Type) && q.Roles.Contains("CAP")), rm = Array.FindIndex(set.Sqns, q => q.S == Side.Red && q.Roles.Contains("CAP"));
        var js = new System.Text.Json.Nodes.JsonArray();
        for (int i = 0; i < set.Sqns.Length; i++)
            if (i != rm) js.Add(new System.Text.Json.Nodes.JsonObject { ["Side"] = set.Sqns[i].S.ToString(), ["Type"] = i == sw ? "AV8BNA" : set.Sqns[i].Type, ["Name"] = set.Sqns[i].Name, ["N"] = set.Sqns[i].N, ["I"] = i });
        js.Add(new System.Text.Json.Nodes.JsonObject { ["Side"] = "Blue", ["Type"] = "Ka-50_3", ["Name"] = "", ["N"] = 30, ["I"] = -1 });
        var w = Scenario.Create(th, 1, set.Id, "", set with { Sqns = [.. js.Select(x => Scenario.Sqn(set, x!))] });
        var (av, ka) = (w.Sqns.First(q => q.Type == "AV8BNA"), w.Sqns.First(q => q.Type == "Ka-50_3"));
        var kb = w.Objs.First(o => o.Id == ka.Base);
        Sim.Advance(w, w.Time.AddHours(6));
        var f = w.Flights.FirstOrDefault(f => f.Sqn == av.Id);
        bool bad; try { Scenario.Sqn(set, new System.Text.Json.Nodes.JsonObject { ["Type"] = "X-99" }); bad = false; } catch (ArgumentException) { bad = true; }
        check(av.Player && !av.Roles.Contains("CAP") && av.Roles.Contains("CAS") && ka.Size == 24 && ka.Name == "Ka-50_3 Squadron" && kb.Side == Side.Blue && kb.BaseId > 0
              && w.Sqns.Count(q => q.Side == Side.Red) == set.Sqns.Count(q => q.S == Side.Red) - 1 && f != null && th.Payloads["AV8BNA"].Count > 0 && bad,
            $"Startdialog: {set.Sqns[sw].Type} → AV-8B ({string.Join(",", av.Roles)}), Ka-50 III auf {kb.Name} ({ka.Size}), Rot −1 Staffel, AV-8B-Flug {f?.Task} {f?.Load}, {th.Payloads.Count(kv => kv.Value.Count > 0)} Muster mit Beladungen");
    }

    /// CAS-Spielerflug: JTAC an der Kontaktlinie mit Sicht aufs Ziel, Blau dazu MQ-9 als FAC(A), Funkkanal – DCS-ATC erkennt beide an Name und FAC-Aufgabe.
    static void JtacTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 1, "kaukasus");
        if (Air.Offers(w).FirstOrDefault(o => o.Task == "CAS") is not { } of) { check(false, "JTAC: kein CAS-Angebot"); return; }
        var pf = Air.TakeOffer(w, of.Task, of.Kind, of.Id, of.Sqn);
        pf.Player = true; pf.Start = "kalt";
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestJtac");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Miz.Fly(w, dir);
        var j = Miz.JtacOf(w, pf);
        using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
        using var rd = new StreamReader(z.GetEntry("mission")!.Open());
        var ms = rd.ReadToEnd();
        check(j != null && Miz.Los(th, j.Pos, j.Tgt) && ms.Contains($"\"JTAC {j.Cs}\"") && ms.Contains($"\"FACA {j.Faca}\"") && ms.Contains("\"AFAC\"") && ms.Contains("\"FAC\"") && ms.Contains("\"MQ-9 Reaper\"")
              && Miz.Comm(w, pf, 1).Any(c => c.N == "JTAC " + j.Cs && c.F == Miz.JtacF),
            $"CAS {pf.Callsign}: JTAC {j?.Cs} {(j == null ? 0 : Geo.Dist(j.Pos.X, j.Pos.Z, j.Tgt.X, j.Tgt.Z) / 1000):0.0} km vor {w.Nodes[pf.Target].Name} mit Sicht, FAC(A) {j?.Faca} MQ-9, {Miz.JtacF} AM Kanal {Miz.Comm(w, pf, 1).FindIndex(c => c.N.StartsWith("JTAC")) + 1}");
    }

    static void HeloTest(Theater th, Action<bool, string> check)
    {
        var w = Scenario.Create(th, 1);
        var hs = w.Sqns.Where(q => Air.Helo(q.Type)).ToList();
        var old = World.FromJson(w.ToJson(), th); old.Sqns.RemoveAll(q => Air.Helo(q.Type)); old.Version = 4; Scenario.Upgrade(old);
        check(hs.Count >= 5 && old.Sqns.Count(q => Air.Helo(q.Type)) == hs.Count && th.Payloads["AH-64D_BLK_II"].Count > 0,
            $"Hubschrauberstaffeln: {string.Join(", ", hs.Select(q => q.Type).Distinct())}; alter Spielstand nachgerüstet");
        List<Flight> hc = []; int over = 0; var seen = new HashSet<int>();
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var x = seed == 1 ? w : Scenario.Create(th, seed);
            for (int h = 0; h < 16; h++) { Sim.Advance(x, x.Time.AddMinutes(30)); over += Over(x, seen, f => Air.Helo(f.Type) && f.Task == "CAS"); }
            hc = x.Flights.Where(f => Air.Helo(f.Type) && f.Task == "CAS" && f.Route.Any(r => r.Kind == "orbit")).ToList();
            w = x;
            if (hc.Count > 0) break;
        }
        int high = hc.Count(f => f.Route.Max(r => r.Alt) > 4000);                                   // nicht über den Hauptkamm
        int sead = w.Flights.Count(x => x.Task == "SEAD" && hc.Any(f => f.Pkg == x.Pkg));
        check(hc.Count > 0 && over + high + sead == 0, $"Hubschrauber-CAS: {hc.Count} Flüge ({string.Join(", ", hc.Select(f => f.Type).Distinct())}) tief ({high} über 4000 m) auf der eigenen Seite ({over} drüben), ohne SEAD ({sead})");
        if (hc.Where(f => !f.Done).OrderBy(f => Air.SqnOf(w, f).Player ? 0 : 1).FirstOrDefault() is { } pf)
        {
            pf.Player = true;
            var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-Heli");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Miz.Fly(w, dir);
            using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
            using var rd = new StreamReader(z.GetEntry("mission")!.Open()); var ms = rd.ReadToEnd();
            check(ms.Contains("[\"helicopter\"]") && ms.Contains(pf.Type), $"Mission mit spielbarem Hubschrauber {pf.Callsign} ({pf.Type}) als Gruppe „helicopter“");
        }
        var c = Scenario.Create(th, 2);
        var en = c.Nodes[Sim.Front(c, c.Axes[0])!.Value.R];
        var dq = c.Sqns.First(q => q.Side == Side.Blue && q.Type == "F-16C_50");
        var dn = new Downed { Id = c.NewId(), Side = Side.Blue, Sqn = dq.Id, Pilot = 0, X = en.X, Z = en.Z, T = c.Time };
        dq.Pilots[0].Status = "vermisst"; c.Downed.Add(dn);
        Sim.Advance(c, c.Time.AddHours(1));
        var cf = c.Flights.FirstOrDefault(f => f.Task == "CSAR" && f.Target == dn.Id);
        Sim.Advance(c, c.Time.AddHours(3));
        var pn = dq.Pilots[0];
        check(cf != null && Air.Helo(cf.Type) && (pn.Status == "aktiv" && cf.Result.Contains("rescued") || cf.Alive < cf.N),
            $"CSAR-Einsatz: {cf?.Callsign} ({cf?.Type}) zu {pn.Name} bei {en.Name} – {pn.Status}, {cf?.Result}");
    }

    /// Träger: auf offener See, außer Reichweite roter SAMs, Marine an Bord; Mission: Start vom Deck (linkUnit), TACAN/ICLS, Supercarrier mit Link 4.
    static void CarrierTest(Action<bool, string> check)
    {
        foreach (var set in Scenario.All.Where(s => s.Cv.Lat != 0 && Theater.Installed(s.Map)).GroupBy(s => s.Map).Select(g => g.First()))
            foreach (var type in new[] { "CVN_75", "Stennis" })
            {
                var th = Theater.Get(set.Map);
                var w = Scenario.Create(th, 3, set.Id, type);
                var cv = w.Objs.First(o => o.Kind == OK.Airbase && o.BaseId < 0);
                double sam = w.Objs.Where(o => o.Side == Side.Red && o.Kind == OK.Sam).Min(o => Geo.Dist(o.X, o.Z, cv.X, cv.Z) - o.Range) / 1000;
                var navy = w.Sqns.Where(q => q.Base == cv.Id).Select(q => q.Type).ToList();
                Sim.Advance(w, w.Time.AddHours(3));
                var of = Air.Offers(w).FirstOrDefault(o => o.Kind != "station" && w.Sqns.First(q => q.Id == o.Sqn).Base == cv.Id);   // wie der Spieler: Auftrag aus der Liste
                var pf = of == null ? null : Air.TakeOffer(w, of.Task, of.Kind, of.Id, of.Sqn);
                string ms = "";
                if (pf != null)
                {
                    pf.Player = true; pf.Start = "kalt";
                    var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestCv");
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    Miz.Fly(w, dir);
                    using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
                    using var r = new StreamReader(z.GetEntry("mission")!.Open()); ms = r.ReadToEnd();
                    w.Mission = "";
                }
                check(th.Water(cv.X, cv.Z) && sam > 20 && navy.Contains("FA-18C_hornet") && navy.Contains("F-14B") && pf != null
                      && ms.Contains($"\"{type}\"") && ms.Contains("\"linkUnit\"") && ms.Contains("\"parking_id\"") && ms.Contains("\"TakeOffParking\"") && ms.Contains("ActivateICLS") && ms.Contains("ActivateLink4") == (type != "Stennis"),
                    $"Träger {set.Map} {type}: auf See, {sam:0} km außerhalb roter SAM-Ringe, an Bord {string.Join(", ", navy.Distinct())}, Spielerflug {pf?.Callsign} {pf?.Type} vom Deck");
            }
    }

    /// DCS-ATC: Setup aus einem Release-Zip holen (Aufbau wie DCS-ATC.zip), Installation erkennen.
    static void AtcTest(Action<bool, string> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestAtc");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "DCS-ATC.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var n in new[] { "LIESMICH.txt", "DCS-ATC-Setup.exe" }) using (var sw = new StreamWriter(z.CreateEntry(n).Open())) sw.Write(n);
        }
        var setup = Atc.Unpack(zip, dir);
        check(Path.GetFileName(setup) == "DCS-ATC-Setup.exe" && File.ReadAllText(setup) == "DCS-ATC-Setup.exe" && Atc.Url.StartsWith("https://github.com/Iceman1991/DCS-ATC-Release/")
              && Atc.Newer("v0.13.0-alpha", "0.12.0-alpha") && Atc.Newer("v0.12.1", "0.12.0-alpha") && !Atc.Newer("v0.12.0-alpha", "0.12.0-alpha") && !Atc.Newer("v0.9.9-alpha", "0.12.0-alpha") && !Atc.Newer(null, "0.12.0-alpha") && !Atc.Newer("v1.0", null),
            $"DCS-ATC: Setup aus dem Release-Zip, hier {(Atc.Exe is { } e ? "installiert: " + e : "nicht installiert – die App lädt es beim Start")}");
        Directory.Delete(dir, true);
    }

    /// Missionsskript und Hook: Lua-Syntax mit dem Lua der DCS-Installation (binluae.exe) prüfen; ohne DCS übersprungen.
    static void LuaTest(Action<bool, string> check)
    {
        var luae = Path.Combine(Theater.FindDcs(), "bin", "luae.exe");
        if (!File.Exists(luae)) { check(true, "Lua-Syntax: luae.exe der DCS-Installation fehlt – nicht geprüft"); return; }
        var chk = Path.Combine(Path.GetTempPath(), "DcsCampaign-chk.lua");
        File.WriteAllText(chk, "for i = 1, #arg do local f, e = loadfile(arg[i]) print(f and 'OK' or ('FEHLER ' .. tostring(e))) end");
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "mod"), "*.lua");
        using var p = Process.Start(new ProcessStartInfo(luae, string.Join(" ", files.Prepend(chk).Select(f => $"\"{f}\""))) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(20000);
        check(o.Split('\n').Count(l => l.StartsWith("OK")) == files.Length, $"Lua-Syntax ({files.Length} Dateien, Lua aus DCS): " + o.Replace("\r", "").Replace("\n", " ").Trim());
        var bub = Path.Combine(Path.GetTempPath(), "DcsCampaign-bubble.lua");
        File.WriteAllText(bub, BubbleLua);
        using var q = Process.Start(new ProcessStartInfo(luae, $"\"{bub}\" \"{Path.Combine(AppContext.BaseDirectory, "mod", "DcsCampaign.lua")}\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var bo = q.StandardOutput.ReadToEnd().Replace("\r", "").Trim(); q.WaitForExit(20000);
        var last = bo.Split('\n')[^1].Split('|');
        check(last[0] == "OK" && !bo.Contains("FEHLER Zeitplan"), last.Length > 1 ? last[1] + (bo.Contains("FEHLER Zeitplan") ? " · " + bo : "") : "Bubble-Test: " + bo);
        var ct = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "campaigntest.lua"));   // Repo: Skript mit DCS-Attrappen
        if (!File.Exists(ct)) return;
        using var r = Process.Start(new ProcessStartInfo(luae, $"\"{ct}\" \"{Path.Combine(AppContext.BaseDirectory, "mod", "DcsCampaign.lua")}\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var ro = r.StandardOutput.ReadToEnd().Replace("\r", "").Trim(); r.WaitForExit(20000);
        check(ro.EndsWith("ALLES OK"), "Missionsskript mit DCS-Attrappen (Bubble, Züge, Hook, Mehrspieler, IADS): " + string.Join(" · ", ro.Split('\n').Where(l => !l.StartsWith("DCSCAMP"))));
        using var h = Process.Start(new ProcessStartInfo(luae, $"\"{Path.Combine(Path.GetDirectoryName(ct)!, "hooktest.lua")}\" \"{Path.Combine(AppContext.BaseDirectory, "mod", "DcsCampaignHook.lua")}\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var ho = h.StandardOutput.ReadToEnd().Replace("\r", "").Trim(); h.WaitForExit(20000);
        check(ho.EndsWith("ALLES OK"), "Hook mit DCS-Attrappen (Befehle, Mehrspieler-Client/Server, Waffenliste): " + ho);
    }

    /// Bubble-Logik des Missionsskripts mit DCS-Attrappen (Aufruf: luae bubble.lua DcsCampaign.lua).
    const string BubbleLua = """
-- Bubble-Logik von mod/DcsCampaign.lua mit DCS-Attrappen: Spieler bei 0/0, Bodengruppen 5–50 km, Luftgruppen 20–180 km.
-- Erwartet: das Nächste zuerst, Budget Boden 300 Fahrzeuge / Luft 40 Maschinen. Aufruf: luae bubble.lua ../mod/DcsCampaign.lua
local T, sched, groups, spawned = 0, {}, {}, {}
local function auto() return setmetatable({}, { __index = function() return auto() end, __call = function() return nil end }) end
setmetatable(_G, { __index = function(_, k) return auto() end })
timer = { getTime = function() return T end, getAbsTime = function() return T end,
  scheduleFunction = function(f, a, t) sched[#sched + 1] = { f = f, a = a, t = t } return #sched end, removeFunction = function() end }
env = { info = function(s) local n = s:match("|SPAWN|%d+|([^|]+)|") if n then spawned[#spawned + 1] = n end end, error = print, warning = print }
local player = { isExist = function() return true end, getPoint = function() return { x = 0, y = 3000, z = 0 } end,
  getGroup = function() return { getID = function() return 1 end } end, getName = function() return "F1-1" end }
coalition = { side = { NEUTRAL = 0, RED = 1, BLUE = 2 }, getGroups = function() return {} end, getPlayers = function() return { player } end,
  addGroup = function(c, cat, g)
    local us = {}
    for _, u in ipairs(g.units) do us[#us + 1] = { isExist = function() return true end, getLife = function() return 1 end,
      getPoint = function() return { x = u.x, y = 0, z = u.y } end, getName = function() return u.name end } end
    groups[g.name] = { isExist = function() return true end, getUnits = function() return us end, getSize = function() return #us end,
      getName = function() return g.name end, destroy = function() groups[g.name] = nil end }
  end }
Group = { getByName = function(n) return groups[n] end, Category = { AIRPLANE = 0, HELICOPTER = 1, GROUND = 2, SHIP = 3, TRAIN = 4 } }
Unit = { getByName = function() return nil end }
land = { getHeight = function() return 0 end }

local S = {}
local function entry(k, cat, d, n, r)
  local units = {}
  for i = 1, n do units[i] = { name = k .. "-" .. i, x = d + i, y = 0 } end
  S[#S + 1] = { n = k .. " Test", k = k, c = 2, cat = cat, r = r,
    g = { name = k .. " Test", route = { points = { { x = d, y = 0, alt = 3000, ETA = 0, task = { params = { tasks = {} } } }, { x = d + 1000, y = 0, alt = 3000, ETA = 7200, task = { params = { tasks = {} } } } } }, units = units } }
end
for i = 10, 1, -1 do entry("U" .. i, 2, i * 5000, 40, 55560) end           -- fern zuerst in der Liste: die Reihenfolge darf nicht zählen
for i = 12, 1, -1 do entry("F" .. i, 0, i * 15000, 4, 185200) end
DCSCAMP = { token = "t", side = 2, player = "F1-1", max = 300, maxAir = 40, spawn = S }

dofile(arg[1])
T = 5
for _, s in ipairs(sched) do if s.t <= T then local ok, err = pcall(s.f, s.a, T) if not ok then print("FEHLER Zeitplan: " .. tostring(err)) end end end
local have = {}
for _, n in ipairs(spawned) do have[n:match("^(%a%d+)")] = true end
local ok = true
for i = 1, 8 do ok = ok and have["U" .. i] == true end                       -- 7×40 = 280 < 300 → die 8. kommt noch, dann voll
for i = 9, 10 do ok = ok and not have["U" .. i] end
for i = 1, 10 do ok = ok and have["F" .. i] == true end                      -- 10×4 = 40
for i = 11, 12 do ok = ok and not have["F" .. i] end
print((ok and "OK" or "FEHLER") .. "|Bubble im Missionsskript (DCS-Attrappen): " .. #spawned .. " Gruppen erzeugt, Boden U1–U8, Luft F1–F10 – das Nächste zuerst, Budget 300 Fahrzeuge / 40 Maschinen")
""";

    static void SamTest(Action<bool, string> check)
    {
        foreach (var set in Scenario.All.Where(s => Theater.Installed(s.Map)))
        {
            var w = Scenario.Create(Theater.Get(set.Map), 1, set.Id);
            var near = w.Objs.Where(o => o.Kind == OK.Sam && o.Range <= 40000)
                .Select(o => (o.Name, Over: o.Range - w.Nodes.Where(n => n.Owner == o.Side.Enemy()).Select(n => Geo.Dist(n.X, n.Z, o.X, o.Z)).DefaultIfEmpty(1e9).Min()))
                .Where(t => t.Over > 0).Select(t => $"{t.Name} +{t.Over / 1000:0} km").ToList();
            check(near.Count == 0, $"{set.Name}: SAMs nicht an der Grenze {string.Join(", ", near)}");
        }
    }

    static void GulfTest(Action<bool, string> check)
    {
        if (!Theater.Installed("PersianGulf")) { Console.WriteLine("       (Karte Persischer Golf nicht installiert – übersprungen)"); return; }
        var th = Theater.Get("PersianGulf");
        foreach (var set in Scenario.All.Where(s => s.Map == "PersianGulf"))
        {
            var w = Scenario.Create(th, 1, set.Id);
            string Side0(string n) => w.Objs.First(o => o.Name == n).Side.De();
            check(Side0("Bandar Abbas Intl") == "Red" && Side0("Al Dhafra AFB") == "Blue" && w.Units.All(u => w.Nodes[u.Node].Owner == u.Side) && w.Axes.All(a => Sim.Front(w, a) != null),
                $"{set.Name}: {w.Nodes.Count} Orte, {w.Objs.Count} Objekte ({w.Objs.Count(o => o.Node < 0)} im Hinterland), {w.Units.Count} Verbände, {w.Sqns.Count} Staffeln, {w.Axes.Count} Achsen mit Front");
            var dry = w.Objs.Where(p => p.Kind == OK.Port && !set.Sea.Any(g => g.From == p.Name) && Scenario.SeaGate(w, p) is var (gx, gz, _) && !Scenario.Lane(th, gx, gz, p.X, p.Z)).Select(p => p.Name)
                .Concat(set.Sea.Where(g => !th.Water(g.X, g.Z)).Select(g => $"Einlauf {g.X:0}/{g.Z:0}")).ToList();
            int nv0 = w.Convoys.Count(c => c.Navy);
            var sw = Stopwatch.StartNew();
            Sim.Advance(w, w.Time.AddDays(2));
            check(dry.Count == 0 && w.Over == "" && w.Flights.All(f => double.IsFinite(f.X) && double.IsFinite(f.Z)) && w.Events.Count(e => e.Kind == "luft") > 10 && w.Events.Any(e => e.Text.Contains("Freighter") || e.Kind == "logistik"),
                $"{set.Name}: zwei Tage in {sw.Elapsed.TotalSeconds:0.0} s, {w.Events.Count(e => e.Kind == "luft")} Luftereignisse, {w.Events.Count(e => e.Kind == "front")} Frontmeldungen{(w.Over == "" ? "" : " – " + w.Over)}{(dry.Count == 0 ? "" : ", Seeweg über Land: " + string.Join(", ", dry))}");
            check(nv0 == 2 && w.Convoys.Where(c => c.Navy).All(c => th.Water(c.X, c.Z)),
                $"{set.Name}: {nv0} Flottenverbände, nach 2 Tagen: {string.Join(", ", w.Convoys.Where(c => c.Navy).Select(c => $"{c.Name} {c.Trucks} Schiffe/{c.Missiles} FK"))}, {w.Events.Count(e => e.Kind == "see")} Seeereignisse");
            if (set != Gulf.Landing) continue;
            var m = Scenario.Create(th, 5, set.Id); Sim.Advance(m, m.Time.AddHours(3));
            var pf = m.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && Air.SqnOf(m, f).Player && f.Route.Any(r => r.Kind == "target")).MinBy(f => f.Takeoff)
                ?? (Air.Offers(m).FirstOrDefault(o => o.Task is not ("CAS" or "CAP" or "Antiship")) is { } of ? Air.TakeOffer(m, of.Task, of.Kind, of.Id, of.Sqn) : null);   // KI hat zufällig keinen geplant: aus der Auftragsliste
            if (pf == null) { check(false, "Golf: spielbarer Angriffsflug"); continue; }
            pf.Player = true; pf.Start = "kalt";
            var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestGolf");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Miz.Fly(m, dir);
            using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
            using var r = new StreamReader(z.GetEntry("mission")!.Open()); var ms = r.ReadToEnd();
            var sp = Miz.Splash;
            check(ms.Contains("\"PersianGulf\"") && ms.Contains("\"Iran\"") && ms.Contains("\"United Arab Emirates\"") && !ms.Contains("\"Russia\"")
                  && ms.Contains("ResKey_3") == (sp != null) && (sp == null || z.GetEntry("l10n/DEFAULT/" + Path.GetFileName(sp)) != null),
                $"Golf: Mission gebaut ({pf.Callsign} {pf.Type} {pf.Task}, Theater PersianGulf, Iran gegen VAE/USA, {z.Entries.Count} Dateien, Splash Damage {(sp == null ? "fehlt" : Path.GetFileName(sp))})");
        }
        Theater.Get("Caucasus");
    }

    /// Weitere Karten ohne eigene Startlage: Projektion und Plätze aus der Installation gerechnet – an Kaukasus und Golf gegen die gepflegten Werte geprüft.
    /// Erzeugte Startlage auf beiden Karten: aufbauen, zwei Tage rechnen, eine Mission bauen.
    static void AutoTest(Action<bool, string> check)
    {
        static Dictionary<int, (double X, double Z, string Name)> Bs(MapDef m) => m.Bases.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(';'))
            .ToDictionary(f => int.Parse(f[0]), f => (double.Parse(f[2]), double.Parse(f[3]), f[1]));
        foreach (var map in new[] { "Caucasus", "PersianGulf" }.Where(Theater.Installed))
        {
            var hand = Theater.Maps[map];
            var auto = Auto.MakeDef(map, Path.Combine(Theater.FindDcs(), @"Mods\terrains", map));
            if (auto == null) { check(false, $"{map}: Kartendaten aus der Installation nicht lesbar"); continue; }
            var (hb, ab) = (Bs(hand), Bs(auto));
            Geo.Use(hand);
            var off = ab.Where(kv => hb.ContainsKey(kv.Key)).Select(kv => Geo.Dist(kv.Value.X, kv.Value.Z, hb[kv.Key].X, hb[kv.Key].Z)).ToList();
            check(auto.Lon0 == hand.Lon0 && Math.Abs(auto.FE - hand.FE) < 30 && Math.Abs(auto.FN - hand.FN) < 30 && off.Count >= hb.Count * 0.7 && off.Count(d => d < 4000) >= off.Count * 0.9,
                $"{map} aus der Installation: Meridian {auto.Lon0}, Versatz {auto.FE - hand.FE:0}/{auto.FN - hand.FN:0} m, {ab.Count} Plätze ({off.Count} bekannte, {off.Count(d => d < 4000)} unter 4 km, max {off.DefaultIfEmpty().Max() / 1000:0.0} km), z. B. {string.Join(", ", ab.Values.Take(3).Select(b => b.Name))}");
        }
        foreach (var map in new[] { "Caucasus", "PersianGulf" }.Where(Theater.Installed).Concat(Auto.Maps()))
        {
            var th = Theater.Get(map);
            var set = Auto.For(th);
            var w = Scenario.Create(th, 3, set.Id);
            int sides = w.Objs.Where(o => o.Kind == OK.Airbase).Select(o => o.Side).Distinct().Count();
            check(sides == 2 && w.Sqns.Select(q => q.Side).Distinct().Count() == 2 && w.Units.All(u => w.Nodes[u.Node].Owner == u.Side) && w.Axes.All(a => Sim.Front(w, a) != null) && w.Objs.Count(o => o.Kind == OK.Sam) >= 6,
                $"{set.Name}: {w.Nodes.Count} Orte, {w.Axes.Count} Achsen ({string.Join(", ", w.Axes.Select(a => a.Name))}), {w.Units.Count} Verbände, {w.Objs.Count} Ziele, {w.Sqns.Count} Staffeln auf {w.Sqns.Select(q => q.Base).Distinct().Count()} Plätzen, Häfen {w.Objs.Count(o => o.Kind == OK.Port)}, Seewege {set.Sea.Length}");
            var sw = Stopwatch.StartNew();
            Sim.Advance(w, w.Time.AddDays(2));
            check(w.Over == "" && w.Flights.All(f => double.IsFinite(f.X) && double.IsFinite(f.Z)) && w.Events.Count(e => e.Kind == "luft") > 10,
                $"{set.Name}: zwei Tage in {sw.Elapsed.TotalSeconds:0.0} s, {w.Events.Count(e => e.Kind == "luft")} Luftereignisse, {w.Events.Count(e => e.Kind == "front")} Frontmeldungen{(w.Over == "" ? "" : " – " + w.Over)}");
            var pf = w.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && Air.SqnOf(w, f).Player && f.Task is not ("Antiship" or "CAS") && Air.Attack.Contains(f.Task) && f.Route.Any(r => r.Kind == "target")).MinBy(f => f.Takeoff)
                ?? (Air.Offers(w).FirstOrDefault(o => o.Task is not ("CAS" or "CAP" or "Antiship")) is { } of ? Air.TakeOffer(w, of.Task, of.Kind, of.Id, of.Sqn) : null);
            if (pf == null) { check(false, $"{set.Name}: kein spielbarer Angriffsflug"); continue; }
            pf.Player = true; pf.Start = "kalt";
            var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-TestAuto");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Miz.Fly(w, dir);
            using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
            using var r = new StreamReader(z.GetEntry("mission")!.Open()); var ms = r.ReadToEnd();
            check(ms.Contains($"\"{map}\"") && ms.Contains("\"Russia\"") && ms.Contains("\"USA\""), $"{set.Name}: Mission gebaut ({pf.Callsign} {pf.Type} {pf.Task} ab {w.Objs.First(o => o.Id == Air.SqnOf(w, pf).Base).Name})");
        }
        Theater.Get("Caucasus");
    }

    /// Logistik wirkt: Abriegelung, Kessel ohne Sprit, Einspeisung vom Kartenrand, Schnee auf den Pässen.
    static void Logistik(Theater th, Action<bool, string> check)
    {
        // Abriegelung West: Rot bei Gali hat sich verschossen (20 %). Ohne Abriegelung füllt die Versorgungslinie in 24 h auf;
        // mit zerstörter Gumista- und Kodori-Brücke und fast leeren Depots Ochamchira/Sukhumi kommt nur noch der langsame Bergumweg durch. Front eingefroren.
        double Run(bool cut)
        {
            var w = Scenario.Create(th, 8);
            foreach (var a in w.Axes) a.Pause = w.Time.AddDays(9);
            w.CmdRed.NextOp = w.CmdBlue.NextOp = w.Time.AddDays(9);
            var br = w.Objs.Where(o => o.Name.StartsWith("Gumista") || o.Name.StartsWith("Kodori")).ToList();
            if (cut) foreach (var o in w.Objs.Where(o => o.Side == Side.Red && o.Kind == OK.Depot && (o.Name.Contains("Ochamchira") || o.Name.Contains("Sukhumi")))) { o.Stock = 10; o.Fuel = 10; }
            int gali = Scenario.Town(w, "Gali").Id;
            var f = w.Units.Where(u => u.Side == Side.Red && u.Node == gali).ToList();
            foreach (var u in f) u.Supply = 0.2;
            for (int h = 0; h < 24; h++) { if (cut) foreach (var b in br) b.Health = 0; Sim.Advance(w, w.Time.AddHours(1)); }
            return f.Average(u => u.Supply);
        }
        double cm = Run(true), nm = Run(false);
        check(cm < nm - 0.3 && nm > 0.8, $"Abriegelung West: Rot bei Gali nach 24 h Munition {cm:P0} (Brücken zerstört, nur Bergumweg), ohne Abriegelung {nm:P0}");

        var k = Scenario.Create(th, 9);
        var u0 = k.Units.First(u => u.Side == Side.Red && u.Role == "reserve");
        var dest = k.Nodes.Where(n => n.Owner == Side.Red && Geo.Dist(n.X, n.Z, u0.X, u0.Z) > 30000).MinBy(n => Geo.Dist(n.X, n.Z, u0.X, u0.Z))!;
        u0.Fuel = 0; var (x0, z0) = (u0.X, u0.Z);
        Sim.Order(k, u0, dest.Id, "Verlegen", "Test");
        for (int h = 0; h < 24; h++) { foreach (var o in k.Objs.Where(o => o.Side == Side.Red && o.Kind == OK.Depot)) o.Health = 0.1; Sim.Advance(k, k.Time.AddHours(1)); }
        var red = k.Units.Where(u => u.Side == Side.Red).ToList();
        check(red.All(u => u.Line == 0) && red.Average(u => u.Supply) < 0.95 && Geo.Dist(u0.X, u0.Z, x0, z0) < 3000 && u0.Fuel == 0 && u0.Why.Contains("fuel 0 %"),
            $"Ohne Depot: Rot abgeschnitten (Munition Ø {red.Average(u => u.Supply):P0}), Verband ohne Sprit bleibt liegen: {u0.Why}");

        var g = Scenario.Create(th, 10);
        Scenario.Town(g, "Golovinka").Owner = Side.Blue;
        Sim.Advance(g, g.Time.Date.AddHours(17).AddMinutes(10));             // Züge fahren um 17 Uhr ab
        bool mv = g.Convoys.Any(c => c.Origin == Scenario.Town(g, "Mineral'nye Vody").Id), ru = g.Convoys.Any(c => c.Rail && c.Origin == Scenario.Town(g, "Rustavi").Id && c.From < 0),
             gv = g.Events.Any(e => e.Text.Contains("Supply feed via Golovinka cut"));
        check(mv && ru && gv, $"Einspeisung vom Kartenrand (Zug, Lkw bei zerstörtem Gleis), unterbrochen wenn der Ort fällt (Mineralnyje Wody {mv} {string.Join("", g.Convoys.Where(c => c.Origin == Scenario.Town(g, "Mineral'nye Vody").Id).Select(c => c.Rail ? "Zug" : "Lkw"))}, Rustawi {ru}, Golovinka {gv})");

        g.Wx.SnowUntil = g.Time.AddHours(5); g.Wx.Snow = true;
        bool closed = Sim.Path(g, Scenario.Town(g, "Kazbegi").Id, Scenario.Town(g, "Pasanauri").Id, Side.Blue, true) is not { } sp || !sp.Contains(Scenario.Town(g, "Pasanauri").Id) || sp.Count > 1;
        g.Wx.Snow = false;
        check(closed && Sim.Path(g, Scenario.Town(g, "Kazbegi").Id, Scenario.Town(g, "Pasanauri").Id, Side.Blue, true) is [_], "Schnee sperrt den Kreuzpass, danach wieder frei");

        var ow = Scenario.Create(th, 11); Sim.Advance(ow, ow.Time.AddHours(3));
        var offers = Air.Offers(ow);
        var of = offers.FirstOrDefault(o => o.Kind != "station");
        var tf = of == null ? null : Air.TakeOffer(ow, of.Task, of.Kind, of.Id, of.Sqn);
        var cs = Air.Offers(ow).FirstOrDefault(o => o.Kind == "station");
        var cf = cs == null ? null : Air.TakeOffer(ow, "CAP", "station", cs.Id, cs.Sqn);
        check(offers.Select(o => o.Type).Distinct().Count() >= 3 && tf?.Sqn == of!.Sqn && tf.Task == of.Task && tf.Status == "geplant" && cf?.Task == "CAP" && cf.Takeoff > ow.Time
              && Air.Offers(ow).Where(o => o.Kind == of.Kind && o.Id == of.Id).All(o => o.Why.Contains("second strike")),
            $"Auftragsliste: {offers.Count} Aufträge ({string.Join(", ", offers.Select(o => o.Type).Distinct())}), {of?.Task} {of?.Name} angelegt als {tf?.Callsign}, CAP {cf?.Callsign}");
    }

    /// Missionsgenerator + Rückkanal ohne DCS: .miz bauen und prüfen, dann DCS-Meldungen einspielen.
    static void MizTest(World m, Flight pf, Action<bool, string> check)
    {
        pf.Player = true; pf.Start = "kalt";
        var dir = Path.Combine(Path.GetTempPath(), "DcsCampaign-Test");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "alt.miz"), "alt");
        var gone = m.Objs.Last(o => o.Kind == OK.Bridge);
        gone.Health = 0;
        var r1 = pf.Route[1]; var fz = m.Objs.First(o => o.Name == "Plant Zestafoni"); var tf = m.Objs.First(o => o.Name == "Army Depot Tbilisi");
        m.Convoys.Add(new Convoy { Id = m.NewId(), Side = Side.Blue, From = -1, To = m.Objs.First(o => o.Name == "Port Poti").Id, Tons = 600, Trucks = 8, X = r1.X, Z = r1.Z, Ship = true, InDcs = true });
        m.Convoys.Add(new Convoy { Id = m.NewId(), Side = Side.Blue, From = fz.Id, To = tf.Id, Path = Sim.Path(m, fz.Node, tf.Node, Side.Blue, true, rail: true)!, Tons = 300, Trucks = 12, X = r1.X, Z = r1.Z, Rail = true, InDcs = true });
        m.Wrecks.Add(new Wreck { Type = "T-72B", X = r1.X, Z = r1.Z, T = m.Time, Side = Side.Red });
        Sim.Report(m);
        var one = m.Th.Stations(pf.Type).First(s => s.Value.Count > 1);          // eigene Bestückung + Ausrüstung wie aus der Planung
        pf.Pyl = Miz.Pylons(m, pf).ToDictionary(p => p.Num, p => p.Clsid); pf.Pyl[one.Key] = one.Value[^1];
        pf.Load = Theater.LoadName(pf.Pyl.Values);
        m.Kits[pf.Type] = new Kit { Chaff = 33, Flare = 27, Gun = 50, Comm = { ["1-7"] = new Ch { F = 255.5, N = "TEST" } }, Cmds = { ["MAN_1"] = [2, 3, 0.5, 1] } };
        var snap = m.ToJson();                                                   // für den Mehrspieler-Durchlauf unten
        var sw = Stopwatch.StartNew();
        Miz.Fly(m, dir);
        var miz = Directory.GetFiles(dir, "*.miz");
        check(miz.Length == 1 && Path.GetFileName(miz[0]) != "alt.miz", $"Mission geschrieben in {sw.ElapsedMilliseconds} ms, alte gelöscht: {Path.GetFileName(miz.FirstOrDefault())}");
        var x = Path.Combine(dir, "x");
        ZipFile.ExtractToDirectory(miz[0], x);
        var mission = File.ReadAllText(Path.Combine(x, "mission"));
        int groups = Regex.Matches(mission, @"\[""groupId""\]").Count;
        check(Lua.Parse(mission).ContainsKey("mission") && mission.Contains("\"Player\"") && groups > 5, $"Mission lesbar: Spielerflug + {groups - 1} weitere Gruppen, {mission.Length / 1024} KB");
        var gl = new List<object?>();
        void Walk(object? o) { if (o is not Dictionary<string, object?> d) return; if (d.ContainsKey("groupId")) gl.Add(d); else foreach (var v in d.Values) Walk(v); }
        Walk(Lua.Parse(mission)["mission"]);
        int loose = gl.Count(g => Lua.Get(Lua.Get(Lua.Get(g, "route"), "points"), "1") is { } p && Lua.Get(p, "ETA_locked") is not true);
        check(gl.Count > 5 && loose == 0, $"Missionseditor: erster Wegpunkt mit fixierter Zeit ({gl.Count} Gruppen, {loose} ohne)");
        var data = Lua.Parse(File.ReadAllText(Path.Combine(x, "l10n", "DEFAULT", "DcsCampaignData.lua")));
        check(Lua.Arr<Dictionary<string, object?>>(Lua.Get(data.GetValueOrDefault("DCSCAMP"), "bridges")).Any(b => Math.Abs(Lua.Num(b, "x") - gone.X) < 1), $"Zerstörte Brücke {gone.Name} wird in DCS gesprengt");
        var spawn = Lua.Arr<Dictionary<string, object?>>(Lua.Get(data.GetValueOrDefault("DCSCAMP"), "spawn"));
        string Key(Dictionary<string, object?> e) => e["k"] as string ?? "";
        var ship = m.Convoys.First(c => c.Ship && c.InDcs); var rail = m.Convoys.First(c => c.Rail && c.InDcs);
        check(spawn.Any(e => Key(e) == $"C{ship.Id}" && Lua.Num(e, "cat") == 3) && spawn.Any(e => Key(e) == $"C{rail.Id}") && Lua.Arr<Dictionary<string, object?>>(Lua.Get(data.GetValueOrDefault("DCSCAMP"), "trains")).Any(),
            "Fahrplan: Frachter als Schiffsgruppe, Güterzug für das Gleis-Skript");
        var mates = m.Flights.Where(o => o.Pkg == pf.Pkg && o != pf && !o.Done).ToList();
        double late = spawn.Where(e => Key(e).StartsWith('F')).Select(e => Lua.Num(Lua.Get(Lua.Get(Lua.Get(e, "g"), "route"), "points") is var p ? Lua.Get(p, "1") : null, "ETA")).DefaultIfEmpty().Max();
        check(mates.All(o => spawn.Any(e => Key(e) == $"F{o.Id}")) && late > (pf.Land - m.MissionStart).TotalSeconds && !gl.Any(g => Lua.Get(g, "name") is string n && Regex.IsMatch(n, @"^[FUOC]\d+ ") && n != $"F{pf.Id} {pf.Callsign}") && spawn.Any(e => Key(e).StartsWith('U')),
            $"Fahrplan: {spawn.Count(e => Key(e).StartsWith('F'))} Flüge (letzter erscheint nach {late / 3600:0.0} h, über die eigene Landung hinaus, Paket vollständig), {spawn.Count(e => Key(e).StartsWith('U'))} Verbände, {spawn.Count(e => Key(e).StartsWith('O'))} Stellungen, {spawn.Count(e => Key(e).StartsWith('C'))} Transporte – erst in der Bubble");
        var sams = spawn.Where(e => Key(e).StartsWith('O') && m.Objs.First(o => $"O{o.Id}" == Key(e)).Kind == OK.Sam).ToList();
        check(sams.Count > 0 && sams.All(e => Lua.Num(e, "iads") > 0) && sams.Any(e => Lua.Arr<object>(Lua.Get(e, "net")).Any()),
            $"IADS: {sams.Count} SAM-Stellungen mit Reichweite, Netz z. B. {string.Join(",", sams.Select(e => Lua.Arr<object>(Lua.Get(e, "net"))).FirstOrDefault(l => l.Any()) ?? [])}");
        var t5 = new Flight { Takeoff = DateTime.Today.AddMinutes(5), Status = "geplant" };
        check(Miz.Fit(t5, "kalt", DateTime.Today) == "Startbahn" && Miz.Fit(t5, "heiß", DateTime.Today.AddMinutes(-6)) == "heiß", "Startart: Start in 5 min → Startbahn statt kalt, mit 11 min bleibt heiß");
        var line = Sim.Line(m);
        check(line.Count(p => p.Side == Side.Red) > 5 && line.Count(p => p.Side == Side.Blue) > 5 && spawn.Any(e => Key(e).StartsWith('L')),
            $"Sicherungslinie: {line.Count} Stützpunkte an der Front, {spawn.Count(e => Key(e).StartsWith('L'))} davon in der Mission");
        bool tsheet = pf.TargetKind is "obj" or "unit" or "convoy";
        check(new[] { "0-Lage", "1-Auftrag", "2-Karte", "3-Daten", "4-Lineup" }.Concat(tsheet ? ["5-Ziel"] : []).All(p => File.Exists(Path.Combine(x, "KNEEBOARD", "IMAGES", $"Kampagne-{p}.png"))),
            $"Kniebrett: Lagebericht, Auftrag, Bedrohungskarte, Datenkarte, Lineup{(tsheet ? ", Zielblatt" : "")} ({pf.Task} {pf.TargetKind})");
        check(Geo.MgrsLL(48.8583, 2.2945).StartsWith("31U DQ 482") && Geo.MgrsLL(48.8583, 2.2945).Contains(" 119"), $"MGRS: Eiffelturm {Geo.MgrsLL(48.8583, 2.2945)} (soll 31U DQ 48251 11932)");
        check(gl.Any(g => Lua.Get(g, "name") as string == "W1" && Lua.Get(g, "dead") is true), "Wrack an der Route liegt als zerstörtes Fahrzeug in der Mission");
        var me = Lua.Get(Lua.Get(gl.First(g => Lua.Get(g, "name") as string == $"F{pf.Id} {pf.Callsign}"), "units"), "1");
        var pay = Lua.Get(me, "payload");
        check(Lua.Num(pay, "chaff") == 33 && Lua.Num(pay, "flare") == 27 && Lua.Num(pay, "gun") == 50 && Lua.Tabs(Lua.Get(pay, "pylons")).Any(y => Lua.Str(y, "CLSID") == one.Value[^1])
              && (pf.Type is "Su-25T" || Lua.Num(Lua.Get(Lua.Get(Lua.Get(me, "Radio"), "1"), "channels"), "7") == 255.5),
            $"Planung in der Mission: Station {one.Key} = {Theater.Wpn(one.Value[^1]).Name}, Düppel/Fackeln/Kanone, eigener Funkkanal 7");
        // Platzlager: Kerosin = Kampagnenbestand + was die startende Rotte mitnimmt; Waffen nur mit Waffenliste des Hooks begrenzt, sonst unbegrenzt
        var pg = gl.First(g => Lua.Get(g, "name") as string == $"F{pf.Id} {pf.Callsign}");
        int hid = (int)Lua.Num(Lua.Get(Lua.Get(Lua.Get(pg, "route"), "points"), "1"), "airdromeId");
        var hw = Lua.Get(Lua.Get(Lua.Parse(File.ReadAllText(Path.Combine(x, "warehouses")))["warehouses"], "airports"), hid.ToString());
        var ho = m.Base(hid)!;
        double want = Math.Round(ho.Fuel + Lua.Tabs(Lua.Get(pg, "units")).Sum(u => Lua.Num(Lua.Get(u, "payload"), "fuel")) / 1000, 1);
        var fake = m.Th.Payloads.Values.SelectMany(l => l).SelectMany(p => p.Pylons).Select(p => p.Clsid).Distinct().Select((c, i) => (c, i)).ToDictionary(t => t.c, t => ($"4,4,7,{t.i}", 1));
        var pyl = Miz.Pylons(m, pf).GroupBy(p => p.Clsid).ToDictionary(g => g.Key, g => g.Count() * pf.Alive);
        var dem = new Dictionary<int, (Dictionary<string, int> Pyl, double Fuel)> { [hid] = (pyl, 12000) };
        Dictionary<string, object?> Ap(Dictionary<string, (string Ws, int N)>? map) => (Dictionary<string, object?>)((Dictionary<int, object?>)Miz.Warehouses(m, dem, map)["airports"]!)[hid]!;
        var lim = Ap(fake);
        int Amt(string c) => ((List<object?>)lim["weapons"]!).Cast<Dictionary<string, object?>>().Where(e => string.Join(",", (List<object?>)e["wsType"]!) == fake[c].Item1).Sum(e => (int)e["initialAmount"]!);
        var cc = pyl.Keys.FirstOrDefault(c => Air.Need(Theater.Wpn(c).Name).Count > 0);
        var cls = cc == null ? null : Air.Need(Theater.Wpn(cc).Name).Keys.First();
        var miss = new Dictionary<string, (string Ws, int N)>(fake); miss.Remove(pyl.Keys.First());
        check(Lua.Num(Lua.Get(hw, "jet_fuel"), "InitFuel") == want && Lua.Get(hw, "unlimitedFuel") is false && lim["unlimitedMunitions"] is false && cc != null
              && Amt(cc) == Math.Max(0, Air.Stores(m, ho).GetValueOrDefault(cls!)) + pyl[cc] && Ap(miss)["unlimitedMunitions"] is true && Ap(null)["unlimitedMunitions"] is true
              && (double)((Dictionary<string, object?>)lim["jet_fuel"]!)["InitFuel"]! == Math.Round(ho.Fuel + 12, 1),
            $"Platzlager {ho.Name}: Kerosin {want} t (Bestand + Rotte), {Theater.Wpn(cc ?? "").Name} {(cc == null ? 0 : Amt(cc))} = {cls}-Bestand + {(cc == null ? 0 : pyl[cc])} an Bord; unbekannte Station/ohne Waffenliste → unbegrenzt");
        if (pf.Type == "FA-18C_hornet" && Miz.DtcTemplate() != null)
        {
            var dtc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(x, "DTC", "Kampagne.dtc")))!;
            int mez = dtc["data"]!["SA"]!["MEZ_THRTS"]!.AsArray().Count, flot = dtc["data"]!["SA"]!["FAOR_FLOT"]!["FLOT"]!.AsArray().Count;
            check(mission.Contains("[\"DTC\"]") && (double)dtc["data"]!["COMM"]!["COMM1"]!["Channel_1"]!["frequency"]! == pf.Freq && flot > 0
                  && (string?)dtc["data"]!["COMM"]!["COMM1"]!["Channel_7"]!["name"] == "TEST" && (double)dtc["data"]!["ALR67"]!["CMDS"]!["CMDSProgramSettings"]!["MAN_1"]!["Chaff"]!["Quantity"]! == 2,
                $"F/A-18C-Kartusche in der Mission: Funk mit Namen, ALE-47-Programme, {mez} Bedrohungsringe, {flot} FLOT-Linien");
        }
        else Console.WriteLine("       (keine Hornet-Kartusche als Vorlage – DTC-Test übersprungen)");
        var luae = Path.Combine(m.Th.Dcs, "bin", "luae.exe");
        if (File.Exists(luae))
        {
            File.WriteAllText(Path.Combine(dir, "check.lua"), "for i = 1, #arg do assert(loadfile(arg[i])) end print('LUA OK')");
            var psi = new ProcessStartInfo(luae) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(Path.Combine(dir, "check.lua"));
            foreach (var f in new[] { "mission", "warehouses", "l10n/DEFAULT/dictionary", "l10n/DEFAULT/mapResource", "l10n/DEFAULT/DcsCampaignData.lua", "l10n/DEFAULT/DcsCampaign.lua" })
                psi.ArgumentList.Add(Path.Combine(x, f));
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            check(o.Contains("LUA OK"), "DCS-Lua (luae.exe) liest alle Missionsdateien" + (o.Contains("LUA OK") ? "" : ": " + o.Trim()));
        }

        int t0 = (int)(m.MissionStart - m.MissionStart.Date).TotalSeconds;
        string Ln(int dt, string kind, string rest = "") => $"2026-10-12 06:00:00.000 INFO    SCRIPTING (Main): DCSCAMP|{m.Mission}|{kind}|{t0 + dt}{(rest == "" ? "" : "|" + rest)}";
        var site = spawn.Select(e => Key(e)).Where(k => k.StartsWith('O')).Select(k => m.Objs.First(o => o.Id == int.Parse(k[1..]))).FirstOrDefault(o => o.Kind == OK.Sam);
        var unit = spawn.Select(e => Key(e)).Where(k => k.StartsWith('U')).Select(k => m.Units.First(u => u.Id == int.Parse(k[1..]))).FirstOrDefault();
        var bridge = m.Objs.First(o => o.Kind == OK.Bridge);
        double hs = site?.Health ?? 0, hu = unit?.Str ?? 0, hb = bridge.Health;
        var lines = new List<string> { Ln(0, "START"), "DCSCAMP|anderes|END|1", Ln(1400, "BIRTH", $"F{pf.Id}-1|Testpilot"), Ln(1500, "TAKEOFF", $"F{pf.Id}-1"), Ln(2000, "POS", $"F{pf.Id} {pf.Callsign}|{pf.Route[2].X:0}|{pf.Route[2].Z:0}|6000") };
        if (site != null) lines.Add(Ln(2100, "SPAWN", $"O{site.Id} {site.Name}|6"));
        if (unit != null) lines.Add(Ln(2100, "SPAWN", $"U{unit.Id} {unit.Name}|4"));
        Bridge.Feed(m, lines);
        bool spawned = (site?.InDcs ?? true) && (unit?.InDcs ?? true);
        lines.Clear();
        if (site != null) lines.Add(Ln(2400, "DEAD", $"O{site.Id}-1-r|{site.X:0}|{site.Z:0}"));
        if (unit != null) lines.AddRange([Ln(2490, "KILL", $"F{pf.Id}-1|U{unit.Id}-1|Mk_82"), Ln(2500, "DEAD", $"U{unit.Id}-1|{unit.X:0}|{unit.Z:0}"), Ln(2600, "OUT", $"U{unit.Id} {unit.Name}|3|{unit.X:0}|{unit.Z:0}")]);
        var foe = m.Flights.First(f => f.Side != pf.Side);
        lines.AddRange([Ln(2550, "KILL", $"F{pf.Id}-1|F{foe.Id}-1|AIM_120C"), Ln(2560, "KILL", $"F{pf.Id}-1|F{foe.Id}-2|AIM_9X")]);
        lines.Add(Ln(2600, "IMPACT", $"Mk_84|{bridge.X:0}|{bridge.Z:0}|429.0"));
        Bridge.Feed(m, lines);
        check(spawned && (unit == null || !unit.InDcs) && (site?.InDcs ?? true), "Bubble: SPAWN übergibt an DCS, OUT zurück an die Kampagne");
        // Live-Befehle: erster Takt legt einen Befehl für den Hook ab (Ping + Neues), Quittung CMD = Hook vorhanden
        Miz.CmdDir = Path.Combine(dir, "cmd");
        var gone2 = m.Convoys.First(c => c.Ship && c.InDcs);
        m.Convoys.Remove(gone2);                                                 // Frachter angekommen -> aus DCS entfernen
        Bridge.Feed(m, [Ln(2700, "T")]);
        var cmd = Directory.Exists(Miz.CmdDir) ? Directory.GetFiles(Miz.CmdDir, "*.lua") : [];
        var ctext = cmd.Length == 1 ? File.ReadAllText(cmd[0]) : "";
        Bridge.Feed(m, [Ln(2701, "CMD", "1|0|1")]);
        check(ctext.Contains($"DCSCAMP.token == \"{m.Mission}\"") && ctext.Contains($"\"C{gone2.Id}\"") && m.DcsNotes.Contains("hook"),
            $"Live-Befehl für den Hook: {cmd.Length} Datei, entfernt den angekommenen Frachter, Quittung erkannt");
        // F10 → Kampagne: Lage, JTAC an der Front, Pop-up-Ziel (Divert); nach der Landung nächster Einsatz mit derselben Maschine
        Tasking.FunkFile = Path.Combine(dir, "funk.txt");
        string Last() { var l = Directory.GetFiles(Miz.CmdDir, "*.lua").Order().ToList(); return l.Count == 0 ? "" : File.ReadAllText(l[^1]); }
        var fr0 = Sim.Front(m, m.Axes[0])!.Value; (pf.X, pf.Z) = (m.Nodes[fr0.B].X, m.Nodes[fr0.B].Z);   // über der eigenen Front
        Bridge.Feed(m, [Ln(2710, "MENU", $"F{pf.Id}-1|lage")]); var c1 = Last();
        Bridge.Feed(m, [Ln(2720, "MENU", $"F{pf.Id}-1|jtac")]); var c2 = Last();
        Bridge.Feed(m, [Ln(2730, "MENU", $"F{pf.Id}-1|ziele")]); var c3 = Last();
        var dk = Regex.Match(c3, "\\[\"k\"\\] = \"(D:[^\"]+)\"").Groups[1].Value;
        Bridge.Feed(m, [Ln(2740, "MENU", $"F{pf.Id}-1|pick|{dk}")]); var c4 = Last();
        check(c1.Contains("[\"say\"]") && c2.Contains("[\"smoke\"]") && c2.Contains("{POS}") && dk != "" && pf.Task == dk.Split(':')[1] && c4.Contains("[\"mark\"]") && File.ReadAllLines(Tasking.FunkFile).Length >= 4,
            $"F10 Kampagne: Lage, JTAC mit Rauch und Koordinaten, Pop-up-Ziel {dk} übernommen ({pf.Task} {Air.TargetName(m, pf.TargetKind, pf.Target)}), Funksprüche für DCS-ATC");
        Console.WriteLine("       " + File.ReadAllLines(Tasking.FunkFile)[1]);
        // Hubschrauber landet im Gelände neben einem abgeschossenen Piloten: aufgenommen, zählt nicht als Landung; die Lage nennt ihn mit Rauch
        var ty0 = pf.Type; pf.Type = "UH-1H";
        var dq = Air.SqnOf(m, pf); var dn = new Downed { Id = m.NewId(), Side = pf.Side, Sqn = dq.Id, Pilot = 0, X = pf.X + 3000, Z = pf.Z, T = m.Time };
        dq.Pilots[0].Status = "vermisst"; m.Downed.Add(dn);
        Bridge.Feed(m, [Ln(2750, "MENU", $"F{pf.Id}-1|lage")]); var c5 = Last();
        Bridge.Feed(m, [Ln(2760, "LAND", $"F{pf.Id}-1||{dn.X + 200:0}|{dn.Z:0}")]);
        check(c5.Contains("Pilot down") && c5.Contains("[\"smoke\"]") && !m.Downed.Contains(dn) && dq.Pilots[0].Status == "aktiv" && !m.DcsNotes.Contains($"land:F{pf.Id}-1"),
            $"CSAR im Cockpit: Lage nennt {dq.Pilots[0].Name} mit Rauch, Landung 200 m daneben nimmt ihn auf (zählt nicht als Landung am Platz)");
        pf.Type = ty0;
        lines.Clear();
        for (int n = 1; n <= pf.Alive; n++) lines.Add(Ln(4800, "LAND", $"F{pf.Id}-{n}|Kutaisi"));
        Bridge.Feed(m, lines);
        var hb0 = m.Objs.First(o => o.Id == Air.SqnOf(m, pf).Base); (pf.X, pf.Z) = (hb0.X, hb0.Z);
        Bridge.Feed(m, [Ln(4810, "MENU", $"F{pf.Id}-1|weiter")]);
        var nk = Regex.Match(Last(), "\\[\"k\"\\] = \"(N:[^\"]+)\"").Groups[1].Value;
        int nfl = m.Flights.Count;
        Bridge.Feed(m, [Ln(4820, "MENU", $"F{pf.Id}-1|pick|{nk}")]);
        var nf = m.Flights.Skip(nfl).FirstOrDefault(f => f.Player);
        Bridge.Feed(m, [Ln(4900, "TAKEOFF", $"F{pf.Id}-1"), Ln(6900, "POS", $"F{pf.Id} {pf.Callsign}|{hb0.X + 5000:0}|{hb0.Z:0}|3000"), Ln(7000, "LAND", $"F{pf.Id}-1|Kutaisi"), Ln(7100, "END")]);
        check(nk != "" && nf != null && nf.Callsign == pf.Callsign && nf.N == 1 && nf.Status == "gelandet" && Math.Abs(nf.X - hb0.X - 5000) < 1 && pf.Status == "gelandet",
            $"Nächster Einsatz nach der Landung: {nk} → {nf?.Callsign} ({nf?.Task}, 1 Maschine) startet erneut und landet ({nf?.Status})");
        check(m.Mission == "" && !m.Flights.Any(f => f.InDcs) && !m.Units.Any(u => u.InDcs) && !m.Objs.Any(o => o.InDcs), "Missionsende: alles zurück in der Kampagne");
        check(m.Time >= m.MissionStart.AddMinutes(80), $"Kriegsuhr folgte DCS bis {m.Time:HH:mm}");
        check(pf.Status == "gelandet", $"Spielerflug gelandet ({pf.Status})");
        check(site == null || site.Health < hs, $"SAM-Radar zerstört übernommen ({(site == null ? "kein SAM in der Mission" : $"{hs:P0} → {site.Health:P0}")})");
        check(unit == null || unit.Str < hu || !m.Units.Contains(unit), "Fahrzeugverlust übernommen");
        check(bridge.Health < hb, $"Bombentreffer an {bridge.Name}: {hb:P0} → {bridge.Health:P0}");
        check(m.Debrief.Any(d => d.Contains("Testpilot boards")), "Spielername im Debriefing");
        var tp = m.Logbook.FirstOrDefault(c => c.Name == "Testpilot");
        check(m.Me == "Testpilot" && tp is { Sorties: 2, Air: 2, Status: "aktiv" } && tp.Book[1].Ground == (unit != null ? 1 : 0) && tp.Book[1].Result == "landed at Kutaisi" && Math.Abs(tp.Book[1].Hours - 0.9) < 0.05
              && tp.Medals.Any(x => x.StartsWith("Distinguished Flying Cross")) && tp.Cur == null,
            $"Logbuch: {tp?.Rank} {tp?.Name}, {tp?.Sorties} Einsätze {tp?.Hours:0.0} h, {tp?.Air} Luft/{tp?.Ground} Boden, Orden: {string.Join(", ", tp?.Medals ?? [])}");

        // Abschuss über Feindgebiet: vermisst → CSAR rettet; dann gefallen → nächster Einsatz mit neuem Piloten
        var cw = World.FromJson(snap, m.Th); var cf = cw.Flights.First(f => f.Id == pf.Id); var cq = Air.SqnOf(cw, cf);
        Miz.Fly(cw, dir);
        int ct0 = (int)(cw.MissionStart - cw.MissionStart.Date).TotalSeconds;
        string Cn(int dt, string kind, string rest) => $"DCSCAMP|{cw.Mission}|{kind}|{ct0 + dt}|{rest}";
        var enemy = cw.Nodes.First(n => n.Owner == cf.Side.Enemy()); var cfoe = cw.Flights.First(f => f.Side != cf.Side);
        int slot = cf.Pilots[0];
        Bridge.Feed(cw, [Cn(0, "START", ""), Cn(100, "BIRTH", $"F{cf.Id}-1|Maverick"), Cn(200, "TAKEOFF", $"F{cf.Id}-1"), Cn(900, "EJECT", $"F{cf.Id}-1"),
            Cn(901, "KILL", $"F{cfoe.Id}-1|F{cf.Id}-1|R-77"), Cn(902, "DEAD", $"F{cf.Id}-1|{enemy.X:0}|{enemy.Z:0}")]);
        var mav = cw.Logbook.First(c => c.Name == "Maverick"); var dm = cw.Downed.FirstOrDefault(d => d.Pilot == ~mav.Id);
        check(mav.Status == "vermisst" && dm != null && Air.PilotOf(cw, dm) == mav && mav.Medals.Any(x => x.StartsWith("Purple Heart")) && mav.Book[0].Result.Contains("MIA") && cq.Pilots[slot].Status == "aktiv",
            $"Abschuss über Feindgebiet: {mav.Name} vermisst, CSAR-Fall angelegt, Purple Heart, Staffelpilot des Platzes bleibt ({cq.Pilots[slot].Status}) – {mav.Book[0].Result}");
        Air.Saved(cw, dm!, "test rescue");
        bool home = mav.Status == "aktiv";
        if (cf.Alive > 0)
        {
            Bridge.Feed(cw, [Cn(1000, "BIRTH", $"F{cf.Id}-2|Maverick"), Cn(1100, "TAKEOFF", $"F{cf.Id}-2"), Cn(1500, "PILOTDEAD", $"F{cf.Id}-2"), Cn(1501, "DEAD", $"F{cf.Id}-2|{enemy.X:0}|{enemy.Z:0}"),
                Cn(1600, "BIRTH", $"F{cf.Id}-3|Maverick")]);
        }
        check(home && (cf.Alive == 0 && cf.N < 2 || mav.Status == "gefallen" && cw.Logbook.Count(c => c.Name == "Maverick") == 2 && cw.Events.Any(e => e.Text.Contains("KIA after"))),
            $"CSAR bringt {mav.Name} heim, später gefallen ({En.Of(mav.Status)}) → neuer Pilot im Logbuch ({cw.Logbook.Count(c => c.Name == "Maverick")} Einträge)");
        foreach (var d in m.Debrief) Console.WriteLine("       " + d);

        // Einzelspieler bricht vor dem Start ab: Flug bleibt geplant und kann gleich wieder geflogen werden
        var ab = World.FromJson(snap, m.Th);
        var af = ab.Flights.Where(f => f.Side == ab.Player && f.Status == "geplant" && Air.SqnOf(ab, f).Player && f.Takeoff > ab.Time.AddMinutes(30)).MinBy(f => f.Takeoff)!;
        foreach (var f in ab.Flights) f.Player = false;
        af.Player = true; af.Start = "kalt";
        Miz.Fly(ab, dir);
        int at0 = (int)(ab.MissionStart - ab.MissionStart.Date).TotalSeconds;
        var (wid, w0) = ab.WhStart.First(kv => kv.Value.Keys.Any(k => k.Contains('@')));          // Lagerabgleich: DCS meldet 5 t Kerosin und 3 Waffen weniger als geplant
        var wo = ab.Base(wid)!; var wk = w0.Keys.First(k => k.Contains('@')); var wc = wk.Split('@')[0];
        double wf = wo.Fuel; int wa = Air.Stores(ab, wo).GetValueOrDefault(wc);
        Bridge.Feed(ab, [$"DCSCAMP|{ab.Mission}|START|{at0}", $"DCSCAMP|{ab.Mission}|WH|{at0 + 5}|{wid}|{(w0["fuel"] - 5) * 1000:0}|{wk.Split('@')[1]}={w0[wk] - 3}", $"DCSCAMP|{ab.Mission}|END|{at0 + 10}"]);
        check(ab.Mission == "" && af.Status == "geplant" && af.Player && !af.InDcs, $"Abbruch vor dem Start: {af.Callsign} bleibt geplant ({af.Status})");
        check(Math.Abs(wf - 5 - wo.Fuel) < 0.01 && Air.Stores(ab, wo).GetValueOrDefault(wc) == Math.Max(0, wa - 3) && ab.WhStart.Count == 0,
            $"Lagerabgleich {wo.Name}: Kerosin {wf:0.#} → {wo.Fuel:0.#} t (−5), {wc} {wa} → {Air.Stores(ab, wo).GetValueOrDefault(wc)} (−3)");

        // Nachbesprechung: Spielerflug startet, Ziel schwer getroffen, ein Bodenabschuss → Bewertung „Erfolg“ mit Ablauf
        var db = World.FromJson(snap, m.Th); Flight? sf = null;
        foreach (var id in db.Flights.Where(f => f.Side == db.Player && f.Status == "geplant" && Air.SqnOf(db, f).Player && f.TargetKind == "obj" && f.Task != "Recon" && f.Takeoff > db.Time.AddMinutes(3)).OrderBy(f => f.Takeoff).Select(f => f.Id).Take(6).ToList())
        {
            db = World.FromJson(snap, m.Th); sf = db.Flights.First(f => f.Id == id);
            foreach (var f in db.Flights) f.Player = false;
            sf.Player = true; sf.Start = "Startbahn";
            try { Miz.Fly(db, dir); break; } catch (ArgumentException) { sf = null; }   // Platz inzwischen zu (Kriegsverlauf): nächster Flug
        }
        if (sf == null) check(false, "Nachbesprechung: kein Spielerflug mit Zielobjekt");
        else
        {
            int dt0 = (int)(db.MissionStart - db.MissionStart.Date).TotalSeconds;
            Bridge.Feed(db, [$"DCSCAMP|{db.Mission}|START|{dt0}", $"DCSCAMP|{db.Mission}|TAKEOFF|{dt0 + 60}|F{sf.Id}-1", $"DCSCAMP|{db.Mission}|KILL|{dt0 + 600}|F{sf.Id}-1|U{db.Units.First(u => u.Side != sf.Side).Id}-1|GBU-12"]);
            db.Objs.First(o => o.Id == sf.Target).Health = 0.1;
            Bridge.Feed(db, [$"DCSCAMP|{db.Mission}|END|{dt0 + 900}"]);
            var r = db.Sorties.FirstOrDefault(x => x.Flight == sf.Id);
            check(r is { Grade: 0, Ground: 1, Player: true } && r.Log.Any(l => l.Contains("takes off")) && db.News.LastOrDefault()?.Contains(sf.Callsign) == true && db.Sorties.All(x => db.Flights.First(f => f.Id == x.Flight).Pkg == sf.Pkg),
                $"Nachbesprechung: {sf.Callsign} {sf.Task} → {(r == null ? "keine Bewertung" : $"{new[] { "Erfolg", "teilweise", "Fehlschlag" }[r.Grade]}: {r.Why}, {r.Log.Count} Meldungen")}; Paket: {db.Sorties.Count} Flüge bewertet");
        }

        // Mehrspieler: Plätze laut Besatzung als Client, KI auf den übrigen, kein Player-Platz
        var mp = World.FromJson(snap, m.Th); var mf = mp.Flights.First(f => f.Id == pf.Id);
        mp.Mp = true; mf.N = mf.Alive = 2; mf.Crew = "KP";
        var mf2 = mp.Flights.Where(f => f.Side == Side.Blue && f.Status == "geplant" && Air.SqnOf(mp, f).Player && f.Id != mf.Id && f.Takeoff < mf.Takeoff.AddHours(2)).MaxBy(f => f.Takeoff);   // später startender zweiter Flug
        if (mf2 != null) { mf2.Player = true; mf2.Crew = new string('P', mf2.N); mf2.Tot = mf2.Tot.AddMinutes(60); Air.Retime(mf2); }   // startet 1 h später: Platz erst dann
        Miz.Fly(mp, dir);
        using var z = ZipFile.OpenRead(Directory.GetFiles(dir, "*.miz")[0]);
        string Entry(string n) { using var r = new StreamReader(z.GetEntry(n)!.Open()); return r.ReadToEnd(); }
        var mg = new List<object?>(); void Walk2(object? o) { if (o is not Dictionary<string, object?> d) return; if (d.ContainsKey("groupId")) mg.Add(d); else foreach (var v in d.Values) Walk2(v); }
        Walk2(Lua.Parse(Entry("mission"))["mission"]);
        var mu = Lua.Tabs(Lua.Get(mg.First(g => Lua.Get(g, "name") as string == $"F{mf.Id} {mf.Callsign}"), "units")).Select(u => Lua.Str(u, "skill")).ToList();
        check(mu.Count == 2 && mu[0] is not ("Client" or "Player") && mu[1] == "Client" && !Entry("mission").Contains("\"Player\"") && Entry("l10n/DEFAULT/DcsCampaignData.lua").Contains("[\"mp\"] = true"),
            $"Mehrspieler: Besatzung KP → Maschine 1 KI ({mu.FirstOrDefault()}), Maschine 2 Client-Platz");
        var load = Path.Combine(Miz.CmdDir, "load.miz");
        check(File.Exists(load) && File.ReadAllText(load) == mp.MissionFile, "Mehrspieler: Hook lädt die neue Mission auf dem Server");
        if (mf2 != null)
        {
            var g2 = mg.FirstOrDefault(g => Lua.Get(g, "name") as string == $"F{mf2.Id} {mf2.Callsign}");
            var m2 = Lua.Tabs(Lua.Get(g2, "units")).Select(u => Lua.Str(u, "skill")).ToList();
            double st2 = Lua.Num(g2, "start_time"), due = (mf2.Takeoff.AddMinutes(-25) - mp.MissionStart).TotalSeconds;
            var cd = Entry("l10n/DEFAULT/DcsCampaignData.lua");
            check(m2.Count == mf2.Alive && m2.All(s => s == "Client") && cd.Contains($"[\"F{mf.Id}\"]") && cd.Contains($"[\"F{mf2.Id}\"]") && Entry("l10n/DEFAULT/dictionary").Contains(mf2.Callsign)
                  && z.Entries.Any(e => e.FullName.StartsWith("KNEEBOARD/IMAGES/Kampagne-") && e.FullName.EndsWith($" {mf2.Callsign}-1-Auftrag.png")) && (due > 600 ? Math.Abs(st2 - due) < 61 : st2 == 0),
                $"Mehrspieler: zwei Spielerflüge in einer Mission ({mf.Callsign}, {mf2.Callsign} {m2.Count}× Client), Heimatplätze, Kniebrett je Flug, Start nach {st2 / 60:0} min");
        }
        // Mehrspieler-Ende: Mensch in Maschine 2 landet → alle gelandet → Mission ausgewertet; nie besetzter zweiter Flug geht zurück in die Staffel
        int mt0 = (int)(mp.MissionStart - mp.MissionStart.Date).TotalSeconds;
        string Mn(int dt, string kind, string rest) => $"DCSCAMP|{mp.Mission}|{kind}|{mt0 + dt}|{rest}";
        Bridge.Feed(mp, [Mn(0, "START", ""), Mn(600, "BIRTH", $"F{mf.Id}-2|Max"), Mn(900, "TAKEOFF", $"F{mf.Id}-1"), Mn(920, "TAKEOFF", $"F{mf.Id}-2"), Mn(3000, "LAND", $"F{mf.Id}-2|Kutaisi"), Mn(3400, "T", "")]);   // 5 min Nachlauf für „Nächster Einsatz“
        check(mp.Mission == "" && File.Exists(Path.Combine(Miz.CmdDir, "ende.lua")) && mf.Status != "gestrichen" && (mf2 == null || mf2.Status == "gestrichen" && mf2.Result == "Player did not take off"),
            $"Mehrspieler: alle Spieler gelandet → Mission ausgewertet, KI-Flügelmann fliegt weiter ({mf.Status}), leerer Flug zurück in die Staffel ({mf2?.Status})");
    }

    static void Report(World w, int day)
    {
        Console.WriteLine($"--- Tag {day}: {w.Time:dd.MM. HH:mm}, Wetter Wolken {w.Wx.Cloud:P0}{(w.Wx.Rain ? " Regen" : "")}");
        foreach (var ax in w.Axes)
        {
            string Side(int n) => string.Join(" ", w.Units.Where(u => u.Node == n && u.Next < 0).Select(u => $"{u.Kind}/{u.Order}/{Sim.Power(u):0.00}"));
            Console.WriteLine($"       {ax.Name,-18} Front: {(Sim.Front(w, ax) is var (r, b) ? $"{w.Nodes[r].Name} [{Side(r)}] | {w.Nodes[b].Name} [{Side(b)}]" : "durchbrochen")}");
        }
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            var us = w.Units.Where(u => u.Side == s).ToList();
            var qs = w.Sqns.Where(q => q.Side == s).ToList();
            var fl = w.Flights.Where(f => f.Side == s).ToList();
            Console.WriteLine($"       {s.De(),-5} {us.Count} Verbände Ø Stärke {us.Average(u => u.Str):P0} Ø Versorgung {us.Average(u => u.Supply):P0} | Flugzeuge {qs.Sum(q => q.Ac)} klar, {qs.Sum(q => q.Lost)} verloren | Flüge {fl.Count(f => f.Status == "gelandet")} gelandet, {fl.Count(f => !f.Done)} offen | Depots {w.Objs.Where(o => o.Side == s && o.Kind == OK.Depot).Sum(o => o.Stock):0} t ({string.Join(", ", w.Objs.Where(o => o.Side == s && o.Kind == OK.Depot).Select(o => $"{o.Name.Split(' ')[^1]} {o.Stock:0}"))})");
        }
        foreach (var g in w.Flights.GroupBy(f => (f.Side, f.Task)).OrderBy(g => g.Key.Side).ThenBy(g => g.Key.Task))
            Console.Write($"{g.Key.Side.De()[0]}:{g.Key.Task} {g.Count()}  ");
        Console.WriteLine();
        var today = w.Events.Where(e => e.T > w.Time.AddHours(-24)).ToList();
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            var l = today.Where(e => e.Side == s && e.Kind == "verlust").ToList();
            Console.WriteLine($"       Verluste {s.De()}: Luftkampf {l.Count(e => e.Text.Contains("air combat"))}, SAM {l.Count(e => e.Text.Contains("(SA-") || e.Text.Contains("Patriot") || e.Text.Contains("Hawk") || e.Text.Contains("NASAMS"))}, Flak {l.Count(e => e.Text.Contains("air defense"))}"
                + $" | SAM {w.Objs.Count(o => o.Side == s && o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles > 0)}/{w.Objs.Count(o => o.Side == s && o.Kind == OK.Sam)} feuerbereit, {w.Objs.Count(o => o.Side == s && o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles == 0)} leer"
                + $" | Plätze: {string.Join(", ", w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == s && w.Sqns.Any(q => q.Base == o.Id)).Select(o => $"{o.Name.Split('-')[0]} {o.Health:P0}/{o.Stock:0}t"))}");
        }
        foreach (var e in w.Events.Where(e => e.T > w.Time.AddHours(-24) && e.Kind is "front" or "verlust").TakeLast(6)) Console.WriteLine($"       {e.T:HH:mm} {e.Text}");
    }
}
