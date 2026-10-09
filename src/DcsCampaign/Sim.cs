namespace DcsCampaign;

/// Bodenkrieg, Logistik, Aufklärung, Wetter. Takt 1 Minute Kriegszeit.
static class Sim
{
    public static readonly Dictionary<UK, double> Pow = new() { [UK.Armor] = 1.4, [UK.Mech] = 1.0, [UK.Motor] = 0.7, [UK.Art] = 0.35, [UK.Shorad] = 0.25 };
    static readonly Dictionary<UK, double> Kmh = new() { [UK.Armor] = 22, [UK.Mech] = 25, [UK.Motor] = 32, [UK.Art] = 20, [UK.Shorad] = 25 };

    // ---------- Logistik-Kennzahlen (Kalibrierknöpfe) ----------
    public const double UnitMun = 100, UnitPol = 60;     // t Vorrat eines Bataillons bei 100 %
    const double LineKm = 130000, LineRate = 0.07;       // Versorgungslinie: Reichweite (m Weg), Füllrate je Stunde direkt am Depot
    const double PolPerM = 1 / 350000.0;                 // Sprit je m Marsch (voller Vorrat = 350 km)

    /// Materialfluss in t je Seite und Posten (nur Diagnose: --balance).
    public static readonly Dictionary<string, double> Flow = []; public static void Fl(Side s, string k, double t) { if (t != 0) Flow[$"{s.De()} {k}"] = Flow.GetValueOrDefault($"{s.De()} {k}") + t; }
    public static Side Enemy(this Side s) => s == Side.Red ? Side.Blue : Side.Red;
    public static string De(this Side s) => s == Side.Red ? "Red" : s == Side.Blue ? "Blue" : "neutral";   // Anzeige (englisch)

    public static void Advance(World w, DateTime until)
    {
        while (w.Time.AddMinutes(1) <= until) { w.Time = w.Time.AddMinutes(1); Tick(w); }
    }

    public static void Tick(World w)
    {
        Move(w, 1 / 60.0);
        Air.Tick(w);
        if (w.Time.Minute % 10 == 0) { Combat(w); Intel(w); }
        if (w.Time.Minute % 30 == 0) Air.Plan(w);
        if (w.Time.Minute == 0) Hourly(w);
    }

    // ---------- Hilfen ----------

    /// Kampfkraft: unter 35 % Munition sinkt sie bis auf 30 %.
    public static double Power(Unit u) => u.Str * Pow[u.Kind] * Math.Clamp(0.3 + 0.7 * u.Supply / 0.35, 0.3, 1) * (0.5 + 0.5 * u.Morale) * (0.8 + 0.4 * u.Exp);
    public static bool Broken(Edge e, World w) => e.Bridge >= 0 && w.Objs.First(o => o.Id == e.Bridge).Health < 0.3;

    /// Kürzester Weg (Knotenliste ohne Start) durch eigenes Gebiet; Ziel darf feindlich sein. Konvois meiden zerstörte Brücken.
    /// Angriff: unbesetzte Feindorte dürfen durchquert (und dabei eingenommen) werden. Rail: nur Gleise, sonst nur Straßen. Schnee sperrt Pässe.
    public static List<int>? Path(World w, int from, int to, Side side, bool convoy, double maxCost = double.MaxValue, bool attack = false, bool rail = false)
    {
        var held = attack ? w.Units.Where(u => u.Side != side && u.Next < 0).Select(u => u.Node).ToHashSet() : null;
        if (from == to) return [];
        var dist = new Dictionary<int, double> { [from] = 0 };
        var prev = new Dictionary<int, int>();
        var pq = new PriorityQueue<int, double>();
        pq.Enqueue(from, 0);
        while (pq.TryDequeue(out var n, out var d))
        {
            if (d > dist[n]) continue;
            if (n == to) break;
            foreach (var ei in w.Nodes[n].Adj)
            {
                var e = w.Edges[ei];
                int m = e.Other(n);
                if (e.Rail != rail || e.Snow && w.Wx.Snow) continue;
                if (m != to && w.Nodes[m].Owner != side && (held == null || held.Contains(m))) continue;
                double c = e.Cost;
                if (Broken(e, w)) { if (convoy) continue; c *= 5; }
                double nd = d + c;
                if (nd > maxCost || dist.TryGetValue(m, out var od) && od <= nd) continue;
                dist[m] = nd; prev[m] = n;
                pq.Enqueue(m, nd);
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var path = new List<int>();
        for (int v = to; v != from; v = prev[v]) path.Add(v);
        path.Reverse();
        return path;
    }

    public static double PathLen(World w, int from, List<int> path)
    {
        double l = 0; int a = from;
        foreach (var b in path) { l += Geo.Dist(w.Nodes[a].X, w.Nodes[a].Z, w.Nodes[b].X, w.Nodes[b].Z); a = b; }
        return l;
    }

    public static Edge? EdgeOf(World w, int a, int b, bool rail = false) => w.Nodes[a].Adj.Select(i => w.Edges[i]).FirstOrDefault(e => e.Other(a) == b && e.Rail == rail);
    public static (string What, string Items) Kind(Convoy c) => c.Navy ? (c.Name, "ships") : c.Reinf >= 0 ? (c.Ship ? "Troopship" : "Troop train", c.Ship ? "holds" : "cars")
        : c.Ship ? ("Freighter", "holds") : c.Rail ? ("Freight train", "cars") : ("Convoy", "trucks");
    public static int StartNode(World w, Convoy c) => c.Origin >= 0 ? c.Origin : w.Objs.First(o => o.Id == c.From).Node;

    /// Nachschub über Brücken zählen (Abriegelungsziele nach Fluss).
    static void FlowOn(World w, int from, List<int> path, double t, bool rail = false)
    {
        int a = from;
        foreach (var b in path) { if (EdgeOf(w, a, b, rail) is { Bridge: >= 0 } e) w.Objs.First(o => o.Id == e.Bridge).Flow += t; a = b; }
    }

    public static (int R, int B)? Front(World w, Axis ax)
    {
        for (int i = 0; i + 1 < ax.Keys.Count; i++)
            if (w.Nodes[ax.Keys[i]].Owner == Side.Red && w.Nodes[ax.Keys[i + 1]].Owner == Side.Blue) return (ax.Keys[i], ax.Keys[i + 1]);
        return null;
    }

    public static void Order(World w, Unit u, int dest, string order, string why)
    {
        u.Order = order; u.Why = why;
        if (u.Node == dest && u.Next < 0) { u.Path.Clear(); return; }
        var p = Path(w, u.Next >= 0 ? u.Next : u.Node, dest, u.Side, false, attack: order is "Angriff" or "Ausbruch");
        if (p == null) { u.Why = why + " (no route)"; return; }
        u.Path = p;
    }

    // ---------- Bewegung ----------

    static void Move(World w, double hours)
    {
        foreach (var u in w.Units.ToList())                                // Häuserkampf beim Einmarsch kann Verbände aufreiben
        {
            if (u.InDcs || !w.Units.Contains(u)) continue;
            if (u.Next < 0 && u.Path.Count > 0)
            {
                int nx = u.Path[0];
                u.Path.RemoveAt(0);
                if (w.Nodes[nx].Owner != u.Side && w.Units.Any(o => o.Side != u.Side && o.Node == nx && o.Next < 0)) { u.Path.Clear(); }
                else { u.Next = nx; u.Prog = 0; }
            }
            if (u.Next >= 0)
            {
                var e = EdgeOf(w, u.Node, u.Next);
                double len = e?.Len ?? Geo.Dist(w.Nodes[u.Node].X, w.Nodes[u.Node].Z, w.Nodes[u.Next].X, w.Nodes[u.Next].Z);
                double slow = e == null ? 1 : e.Len / e.Cost * (Broken(e, w) ? 0.2 : 1);
                if (w.Wx.Rain) slow *= 0.8;
                double sp = Kmh[u.Kind] * 1000 * slow * (u.Fuel < 0.03 ? 0 : u.Fuel < 0.15 ? 0.5 : 1);
                if (sp == 0 && !u.Why.StartsWith("out of fuel")) u.Why = $"out of fuel, stranded short of {w.Nodes[u.Next].Name}";
                u.Prog += sp * hours;
                u.Fuel = Math.Max(0, u.Fuel - sp * hours * PolPerM);
                if (u.Prog >= len)
                {
                    u.Node = u.Next; u.Next = -1; u.Prog = 0;
                    Arrive(w, u);
                }
            }
            Place(w, u);
        }
        foreach (var c in w.Convoys.ToList())
        {
            if (c.InDcs) continue;
            if (c.Navy) { Patrol(w, c, hours); continue; }
            if (c.Ship) { Sail(w, c, hours); continue; }
            if (c.Leg >= c.Path.Count) { Deliver(w, c); continue; }
            int from = c.Leg == 0 ? StartNode(w, c) : c.Path[c.Leg - 1], to = c.Path[c.Leg];
            var what = Kind(c).What;
            if (w.Nodes[to].Owner != c.Side) { Fl(c.Side, "- Transport verloren", c.Tons + c.Fuel); w.Convoys.Remove(c); w.Log(c.Side, "konvoi", $"{what} to {ObjName(w, c.To)} cut off at {w.Nodes[to].Name}, cargo lost.", c.X, c.Z); continue; }
            var e = EdgeOf(w, from, to, c.Rail);
            if (e != null && (Broken(e, w) || e.Snow && w.Wx.Snow))
            {
                var p = Path(w, from, w.Objs.First(o => o.Id == c.To).Node, c.Side, true, rail: c.Rail);
                if (p == null)
                {
                    if (!Broken(e, w)) continue;                           // Pass verschneit: warten, bis geräumt
                    Fl(c.Side, "- Transport verloren", c.Tons + c.Fuel); w.Convoys.Remove(c); w.Log(c.Side, "konvoi", $"{what} to {ObjName(w, c.To)} turns back: {ObjName(w, e.Bridge)} destroyed, no detour.", c.X, c.Z); continue;
                }
                c.Path = c.Path.Take(c.Leg).Concat(p).ToList();
                continue;
            }
            double len = e?.Len ?? Geo.Dist(w.Nodes[from].X, w.Nodes[from].Z, w.Nodes[to].X, w.Nodes[to].Z);
            c.Prog += (c.Rail ? 60000 : 35000) * (e == null ? 1 : e.Len / e.Cost) * hours;
            var pa = w.Nodes[from]; var pb = w.Nodes[to];
            double f = Math.Min(1, c.Prog / Math.Max(1, len));
            c.X = pa.X + (pb.X - pa.X) * f; c.Z = pa.Z + (pb.Z - pa.Z) * f;
            if (c.Prog >= len)
            {
                if (e is { Bridge: >= 0 }) w.Objs.First(o => o.Id == e.Bridge).Flow += c.Tons + c.Fuel + (c.Reinf >= 0 ? 300 : 0);
                c.Leg++; c.Prog = 0;
            }
        }
    }

    /// Frachter: geradewegs auf den Hafen zu, bis die Küste kommt (Reede), dann entladen. Hafen zerstört: auf Reede warten.
    static void Sail(World w, Convoy c, double hours)
    {
        var p = w.Objs.First(o => o.Id == c.To);
        double d = Math.Max(1, Geo.Dist(c.X, c.Z, p.X, p.Z)), step = 25000 * hours;
        double nx = c.X + (p.X - c.X) / d * step, nz = c.Z + (p.Z - c.Z) / d * step;
        if (d > step && w.Th.Water(nx, nz)) { c.X = nx; c.Z = nz; }
        else if (p.Health >= 0.3 || p.Side != c.Side) Deliver(w, c);
    }

    /// Kriegsschiffe: einsatzbereit vor den nächsten feindlichen Hafen (Blockade), angeschlagen oder leergeschossen zurück vor den Heimathafen (nachladen).
    /// Bekannte feindliche Schiffe in Reichweite bekommen Seezielflugkörper: Frachter bis 60 km, Kriegsschiffe bis 150 km.
    // ponytail: Kurs gierig um Landzungen (±25…80°), Wasser-A* falls Verbände hängen bleiben
    internal static void Patrol(World w, Convoy c, double hours)
    {
        var home = w.Objs.First(o => o.Id == c.To);
        bool fit = c.Trucks * 2 > c.Trucks0 && c.Missiles >= 4;
        var (port, dest) = fit ? w.Objs.Where(o => o.Kind == OK.Port && o.Side == c.Side.Enemy() && o.Health > 0).Select(o => (o, p: Scenario.Offshore(w, o, 35000)))
            .Where(t => t.p != null).OrderBy(t => Geo.Dist(t.p!.Value.X, t.p.Value.Z, home.X, home.Z)).FirstOrDefault() : default;   // kein feindlicher Hafen mehr: default
        if (dest == null && home.Side == c.Side) (port, dest) = (home, Scenario.Offshore(w, home));
        if (dest is { } d)
        {
            var at = new P(c.X, c.Z); double dist = Geo.Dist(c.X, c.Z, d.X, d.Z);
            if (dist > 2000)
            {
                foreach (var o in new[] { 0, 25, -25, 50, -50, 80, -80 })
                    if (Geo.Move(at, Geo.Brg(at, d) + o, Math.Min(dist, 30000 * hours)) is var n && w.Th.Water(n.X, n.Z)) { c.X = n.X; c.Z = n.Z; break; }
                if (port!.Side != c.Side && Geo.Dist(c.X, c.Z, d.X, d.Z) <= 2000) w.Log(Side.None, "see", $"{c.Name} blockades {port.Name}.", c.X, c.Z);
            }
            else if (port == home && w.Time.Minute == 0) c.Missiles = Math.Min(c.Ssm * c.Trucks / Math.Max(1, c.Trucks0), c.Missiles + 2);   // vor dem Heimathafen nachladen
        }
        foreach (var e in w.Convoys.Where(e => e.Side != c.Side && e.Ship && !e.InDcs && Geo.Dist(c.X, c.Z, e.X, e.Z) < (e.Navy ? 150000 : 60000)).ToList())
        {
            if (c.Missiles <= 0 || !w.Intel(c.Side).ContainsKey($"C{e.Id}") || !w.Chance(0.6 * hours)) continue;
            int shots = Math.Min(c.Missiles, e.Navy ? 4 : 2), hits = Enumerable.Range(0, shots).Count(_ => w.Chance(!e.Navy ? 0.6 : NavyRange(e) > 40000 ? 0.2 : 0.35));
            c.Missiles -= shots;
            string what = e.Navy ? e.Name : $"{Kind(e).What} bound for {ObjName(w, e.To)}";
            int sunk = Sink(w, e, e.Navy ? hits : hits > 0 ? e.Trucks : 0);
            w.Log(sunk > 0 ? Side.None : c.Side, "see", sunk == 0 ? $"{c.Name} fires {shots} anti-ship missiles at {what} – no hits." : e.Navy ? $"{what}: {sunk} ship(s) sunk by {c.Name}." : $"{what} sunk by {c.Name}.", e.X, e.Z);
        }
    }

    /// Treffer auf Schiffe: Frachter sinkt je Treffer, Kriegsschiff nach zwei; Ladung und Flugkörper gehen anteilig mit unter.
    public static int Sink(World w, Convoy c, int hits)
    {
        int n = Math.Min(c.Trucks, c.Navy ? hits / 2 + (hits % 2 == 1 && w.Chance(0.5) ? 1 : 0) : hits);
        if (n <= 0) return 0;
        double keep = 1 - (double)n / c.Trucks;
        Fl(c.Side, "- versenkt", (c.Tons + c.Fuel) * (1 - keep));
        c.Tons *= keep; c.Fuel *= keep; c.Missiles = (int)(c.Missiles * keep); c.Trucks -= n;
        if (c.Trucks <= 0) w.Convoys.Remove(c);
        return n;
    }

    /// Reichweite der Schiffsflugabwehr (stärkstes noch schwimmendes Schiff): S-300F / SM-2 75 km, SM-1 40 km, Osa-M 15 km, sonst Nahbereich.
    public static double NavyRange(Convoy c) => c.Types.Take(c.Trucks).Select(t => t switch { "MOSCOW" or "TICONDEROG" or "USS_Arleigh_Burke_IIa" => 75000.0, "PERRY" => 40000, "REZKY" or "ALBATROS" or "NEUSTRASH" => 15000, _ => 5000 }).DefaultIfEmpty(0).Max();

    /// Produktion: Werk = Hallen, ohne Strom (Kraftwerk < 30 %) nur 40 %; Hafen = Gesamtzustand
    static double Prod(Obj f) => f.Kind == OK.Factory ? f.P("Production halls") * (f.P("Power plant") < 0.3 ? 0.4 : 1) : f.Health;

    static void Place(World w, Unit u)
    {
        var a = w.Nodes[u.Node];
        if (u.Next < 0)
        {
            // mehrere Verbände pro Ort leicht versetzt, damit man sie sieht
            double ang = u.Id * 2.399, r = 900 + (u.Id % 3) * 500;
            u.X = a.X + Math.Cos(ang) * r; u.Z = a.Z + Math.Sin(ang) * r;
            return;
        }
        var b = w.Nodes[u.Next];
        double len = Math.Max(1, Geo.Dist(a.X, a.Z, b.X, b.Z)), f = Math.Min(1, u.Prog / (EdgeOf(w, u.Node, u.Next)?.Len ?? len));
        u.X = a.X + (b.X - a.X) * f; u.Z = a.Z + (b.Z - a.Z) * f;
    }

    static void Arrive(World w, Unit u)
    {
        var n = w.Nodes[u.Node];
        if (n.Owner != u.Side && !w.Units.Any(o => o.Side != u.Side && o.Node == n.Id && o.Next < 0))
        {
            if (Garrison(w, n) > 0) { Hit(w, u, 0.03); u.Morale = Math.Max(0, u.Morale - 0.05); if (!w.Units.Contains(u)) return; }   // Häuserkampf gegen die Ortsverteidigung
            Capture(w, n, u.Side, u.Name);
        }
        if (u.Order is "Angriff" or "Ausbruch" && u.Path.Count == 0 && n.Owner == u.Side)
        {
            // Ziel genommen: sichern, Kräfte sammeln – der nächste Angriff wird stündlich neu entschieden
            u.Why = u.Order == "Ausbruch" ? $"broke out of the pocket to {n.Name}" : $"took {n.Name}, consolidating";
            u.Order = "Halten";
            u.Morale = Math.Max(0, u.Morale - 0.1); u.Supply = Math.Max(0, u.Supply - 0.1);
        }
    }

    public static void Capture(World w, Node n, Side side, string by)
    {
        var flip = n.Key == n.Id ? w.Nodes.Where(m => m.Key == n.Id).ToList() : [n];
        foreach (var m in flip) m.Owner = side;
        n.Owner = side;
        var ids = flip.Select(m => m.Id).ToHashSet();
        w.Log(side, "front", $"{n.Name} captured by {side.De()} ({by}).", n.X, n.Z);
        foreach (var ax in w.Axes.Where(a => a.Keys.Contains(n.Id))) ax.Pause = w.Time.AddHours(ax.Op == "Angriff" && ax.OpSide == side ? 3 : 8);   // operative Pause: neu ordnen, nachführen (im Schwerpunkt kürzer)
        foreach (var o in w.Objs.Where(o => ids.Contains(o.Node) && o.Side != side && o.Kind != OK.Bridge))
        {
            if (o.Kind is OK.Sam or OK.Ewr or OK.Cp or OK.Tel) { o.Health = 0; w.Log(side, "front", $"{o.Name} abandoned and blown up during the withdrawal.", o.X, o.Z); continue; }
            Fl(o.Side, "- erobert", o.Stock + o.Fuel); Fl(side, "+ Beute", (o.Stock + o.Fuel) * 0.3);
            o.Side = side; o.Health *= 0.5; o.Stock *= 0.3; o.Fuel *= 0.3; o.Arms = [];   // erbeutete Waffen passen nicht
            w.Log(side, "front", $"{o.Name} captured (damaged).", o.X, o.Z);
            if (o.Kind == OK.Airbase) Evacuate(w, o);
        }
        foreach (var c in w.Convoys.Where(c => c.Side != side && c.Leg < c.Path.Count && ids.Contains(c.Path[c.Leg])).ToList()) w.Convoys.Remove(c);
        if (n.Key == n.Id && w.Axes.Any(a => a.Keys.Contains(n.Id))) Escalate(w, 1, $"{n.Name} has fallen");
    }

    static void Evacuate(World w, Obj ab)
    {
        foreach (var q in w.Sqns.Where(q => q.Base == ab.Id))
        {
            var nb = w.Objs.Where(o => o.Kind == OK.Airbase && o.Side == q.Side && o.Health > 0.3 && o.Id != ab.Id).MinBy(o => Geo.Dist(o.X, o.Z, ab.X, ab.Z));
            if (nb == null) { q.Lost += q.Ac; q.Ac = 0; continue; }
            q.Base = nb.Id;
            w.Log(q.Side, "luft", $"{q.Name} relocated to {nb.Name}.", nb.X, nb.Z);
        }
        foreach (var f in w.Flights.Where(f => f.Status == "geplant" && w.Sqns.First(q => q.Id == f.Sqn).Base == ab.Id)) f.Status = "gestrichen";
    }

    // ---------- Gefecht ----------

    static void Combat(World w)
    {
        foreach (var ax in w.Axes)
        {
            if (Front(w, ax) is not var (rf, bf) || w.Units.Any(u => u.InDcs && (u.Node == rf || u.Node == bf))) continue;   // diese Front kämpft gerade in DCS
            foreach (var att in new[] { Side.Red, Side.Blue })
            {
                int own = att == Side.Red ? rf : bf, tgt = att == Side.Red ? bf : rf;
                var tn = w.Nodes[tgt];
                if (ax.Op == "Angriff" && ax.OpSide == att && w.Time < ax.OpSince.AddHours(2)) { Barrage(w, att, tn); continue; }   // Feuervorbereitung
                var a = w.Units.Where(u => u.Side == att && u.Order == "Angriff" && u.Axis == ax.Id && u.Node == own && u.Next < 0 && !u.InDcs).ToList();
                if (a.Count == 0) continue;
                var d = w.Units.Where(u => u.Side != att && u.Node == tgt && u.Next < 0).ToList();
                if (d.Count == 0)
                {
                    // Ortsverteidigung (Polizei, Reservisten) in Städten und Schlüsselorten: ein schwacher Vorstoß bleibt hängen
                    double g = Garrison(w, tn), sa = Odds(w, att, a, tn).A;
                    if (sa < g * 1.2)
                    {
                        foreach (var u in a) { u.Order = "Halten"; u.Why = $"local defense of {tn.Name} holds ({sa:0.0} vs. {g:0.0})"; }
                        w.Log(att, "front", $"Local defense of {tn.Name} repels the {att.De()} advance.", tn.X, tn.Z);
                        continue;
                    }
                    foreach (var u in a.Where(u => u.Kind is not (UK.Art or UK.Shorad))) { if (g > 0) Hit(w, u, 0.01); Order(w, u, tgt, "Angriff", $"advancing on {tn.Name}"); }
                    continue;
                }
                var (A, D) = Odds(w, att, a, tn);
                if (A <= 0 || D <= 0) continue;
                if (!w.Daylight && att == Side.Red) A *= 0.85;                    // Nachtkampf: Blau besser ausgerüstet
                double r = A / D;
                double dl = 0.012 * Math.Pow(r, 0.7) * (0.5 + w.Rnd()), al = 0.010 * Math.Pow(1 / r, 0.7) * (0.5 + w.Rnd());
                foreach (var g in w.Units.Where(u => u.Kind == UK.Art && u.Next < 0 && Geo.Dist(u.X, u.Z, tn.X, tn.Z) < 30000)) g.Supply = Math.Max(0, g.Supply - 0.005);   // Artillerie schießt mit
                foreach (var u in d) { Hit(w, u, dl * (u.Kind is UK.Art or UK.Shorad ? 0.5 : 1)); u.Morale = Math.Max(0, u.Morale - dl * 1.6); u.Supply = Math.Max(0, u.Supply - 0.006); u.Why = $"under attack at {tn.Name} (ratio {r:0.0}:1)"; }
                foreach (var u in a) { Hit(w, u, al); u.Morale = Math.Max(0, u.Morale - al * 1.4); u.Supply = Math.Max(0, u.Supply - 0.008); u.Fuel = Math.Max(0, u.Fuel - 0.002); u.Why = $"attacking {tn.Name} (ratio {r:0.0}:1)"; }
                if (d.FirstOrDefault(u => w.Units.Contains(u)) is { } du && !w.Events.Any(e => e.T > w.Time.AddHours(-6) && e.Kind == "front" && e.Text.StartsWith("CAS requested") && e.Text.Contains(tn.Name)))
                    w.Log(du.Side, "front", $"CAS requested: {du.Name} under attack at {tn.Name}.", tn.X, tn.Z);
                d.RemoveAll(u => !w.Units.Contains(u)); a.RemoveAll(u => !w.Units.Contains(u));
                if (d.Count == 0 || d.Average(u => u.Str) < 0.35 || d.Average(u => u.Morale) < 0.3)
                {
                    foreach (var u in d) Retreat(w, u, ax);
                    w.Log(att, "front", $"Defense at {tn.Name} collapses – {att.Enemy().De()} withdraws.", tn.X, tn.Z);
                }
                else if (a.Count == 0 || a.Average(u => u.Morale) < 0.3)
                {
                    foreach (var u in a) { u.Order = "Halten"; u.Why = $"attack on {tn.Name} called off"; }
                    w.Log(att, "front", $"{att.De()} attack on {tn.Name} repulsed.", tn.X, tn.Z);
                }
            }
        }
    }

    /// Kampfkraft der Ortsverteidigung ohne Verbände: Städte und Orte mit Depot/Hafen/Flugplatz/Werk, im Gebirge stärker.
    public static double Garrison(World w, Node n)
    {
        bool site = w.Objs.Any(o => o.Node == n.Id && o.Side == n.Owner && o.Kind is OK.Depot or OK.Port or OK.Airbase or OK.Factory or OK.Cp);
        double g = (n.Size > 6000 ? 0.5 : 0) + (site ? 0.3 : 0) + (n.Key == n.Id ? 0.2 : 0);
        return g * (1 + Math.Clamp(w.Th.Height(n.X, n.Z) / 3000, 0, 0.4));
    }

    /// Artillerievorbereitung: Stellungen zerschlagen (Eingraben weg), Moral und etwas Stärke kosten. Verbraucht Munition der Geschütze.
    static void Barrage(World w, Side att, Node tn)
    {
        var guns = w.Units.Where(u => u.Side == att && u.Kind == UK.Art && u.Next < 0 && u.Supply > 0.05 && Geo.Dist(u.X, u.Z, tn.X, tn.Z) < 30000).ToList();
        if (guns.Count == 0) return;
        double fire = guns.Sum(Power);
        foreach (var g in guns) { g.Supply = Math.Max(0, g.Supply - 0.01); g.Why = $"preparatory fire on {tn.Name}"; }
        foreach (var u in w.Units.Where(u => u.Side != att && u.Node == tn.Id && u.Next < 0).ToList())
        {
            u.Dig = Math.Max(0, u.Dig - 0.02 * fire); u.Morale = Math.Max(0, u.Morale - 0.01 * fire); u.Why = $"under artillery fire at {tn.Name}";
            Hit(w, u, 0.002 * fire);
        }
    }

    /// Kräfteverhältnis: Angreifer (+ Artillerie in 30 km) gegen Verteidiger am Ort (eingegraben, Gelände, Stadt, Artillerie).
    public static (double A, double D) Odds(World w, Side att, IEnumerable<Unit> a, Node tn)
    {
        double Art(bool mine) => w.Units.Where(u => u.Kind == UK.Art && (u.Side == att) == mine && u.Next < 0 && Geo.Dist(u.X, u.Z, tn.X, tn.Z) < 30000).Sum(Power) * 0.6;
        double terr = 1 + Math.Clamp(w.Th.Height(tn.X, tn.Z) / 3000, 0, 0.4) + (tn.Size > 6000 ? 0.1 : 0);   // Gebirge/Pässe: bis +40 %
        var d = w.Units.Where(u => u.Side != att && u.Node == tn.Id && u.Next < 0);
        return (a.Sum(Power) + Art(true), d.Sum(u => Power(u) * (1 + u.Dig)) * terr + Art(false));
    }

    public static void Hit(World w, Unit u, double loss)
    {
        // Wracks bleiben liegen (auch in Folgemissionen): je verlorenes Fahrzeug eins, verstreut um die Stellung
        var tpl = Scenario.Templates[u.Template];
        for (w.WreckAcc += Math.Min(u.Str, loss) * u.Veh; w.WreckAcc >= 1; w.WreckAcc--)
        {
            int n = w.Wrecks.Count + (int)(w.Time.Ticks / TimeSpan.TicksPerMinute % 997);
            var p = Geo.Move(new P(u.X, u.Z), n * 137.5 % 360, 150 + n * 211 % 1200);
            w.Wrecks.Add(new Wreck { Type = tpl[n % tpl.Length], X = p.X, Z = p.Z, Hdg = n * 53 % 360, T = w.Time, Side = u.Side });
        }
        if (w.Wrecks.Count > 400) w.Wrecks.RemoveRange(0, w.Wrecks.Count - 400);
        u.Str = Math.Max(0, u.Str - loss);
        if (u.Str >= 0.08) return;
        w.Units.Remove(u);
        w.Log(u.Side.Enemy(), "front", $"{u.Name} ({u.Side.De()}) wiped out.", u.X, u.Z);
    }

    static void Retreat(World w, Unit u, Axis ax)
    {
        int i = ax.Keys.IndexOf(u.Node);
        int step = u.Side == Side.Red ? -1 : 1;
        for (int k = i + step; k >= 0 && k < ax.Keys.Count; k += step)
            if (w.Nodes[ax.Keys[k]].Owner == u.Side) { Order(w, u, ax.Keys[k], "Rückzug", $"withdrawing to {w.Nodes[ax.Keys[k]].Name}"); u.Dig = 0; return; }
        var safe = w.Nodes.Where(n => n.Owner == u.Side && n.Id != u.Node).MinBy(n => Geo.Dist(n.X, n.Z, u.X, u.Z));
        if (safe != null) Order(w, u, safe.Id, "Rückzug", $"withdrawing to {safe.Name}");
    }

    // ---------- Stündlich: KI, Logistik, Reparatur, Wetter ----------

    /// Kräfteverlauf: Bodenstärke (Kampfkraft der Verbände), einsatzbereite und reparierbare Maschinen ohne Unterstützer, SAM-Stellungen
    public static void Snapshot(World w)
    {
        double G(Side s) => Math.Round(w.Units.Where(u => u.Side == s).Sum(u => u.Str * Pow[u.Kind]), 1);
        int A(Side s) => w.Sqns.Where(q => q.Side == s && q.Roles.Any(r => r is not ("AWACS" or "Tanker" or "Transport" or "CSAR"))).Sum(q => q.Ac + q.Repair.Count + w.Flights.Where(f => f.Sqn == q.Id && Air.Up(f)).Sum(f => f.Alive));
        int M(Side s) => w.Objs.Count(o => o.Side == s && o.Kind == OK.Sam && !o.Decoy && o.Health > 0.3);
        w.History.Add(new Snap { T = w.Time, RedG = G(Side.Red), BlueG = G(Side.Blue), RedA = A(Side.Red), BlueA = A(Side.Blue), RedS = M(Side.Red), BlueS = M(Side.Blue) });
    }

    static double Sup(World w, Side s) => s == w.Player ? 1 : w.SupplyPct / 100.0;   // Nachschub des Gegners nach Schwierigkeit

    static void Hourly(World w)
    {
        Weather(w);
        int hour = (int)Math.Round((w.Time - w.Start).TotalHours);
        if (hour >= 96) Escalate(w, 2, "Four days of war"); else if (hour >= 36) Escalate(w, 1, "The war drags on");
        Join(w, hour);
        for (int i = 0; i < w.S.Reinf.Length; i++) if (Due(w, w.S.Reinf[i].H, w.S.Reinf[i].S) == hour) Reinforce(w, i);
        foreach (var a in w.S.AirReinf.Where(a => Due(w, a.H, a.S) == hour)) Scenario.Arrive(w, a);
        Rockets(w);
        Launchers(w);
        Truce(w, hour);
        Ops(w);
        foreach (var ax in w.Axes) Operate(w, ax);
        Logistics(w);
        Air.Csar(w);
        w.Wrecks.RemoveAll(k => k.T < w.Time.AddDays(-4));
        if (w.Time.Hour == 6) { Report(w); if (w.History.Count == 0 || w.Time - w.History[^1].T > TimeSpan.FromHours(12)) Snapshot(w); }
        foreach (var u in w.Units.ToList())
        {
            if (u.Order is "Halten" or "Verteidigen" && u.Next < 0) u.Dig = Math.Min(0.4, u.Dig + 0.03);
            if (u.Order != "Angriff") u.Morale = Math.Min(1, u.Morale + (u.Supply > 0.3 && u.Fuel > 0.1 ? 0.03 : 0.005));
            if (u.Order == "Auffrischen" && u.Next < 0 && Depot(w, u, 15000) is { } dp && dp.Stock > 50)
            {
                double add = Math.Min(0.03, 1 - u.Str);
                u.Str += add; dp.Stock -= add * 300; Fl(u.Side, "- Auffrischen", add * 300);
                if (u.Str > 0.85 && u.Morale > 0.7) { u.Order = "Reserve"; u.Role = "reserve"; u.Why = "refitted"; }
            }
            // Kessel: solange Sprit da ist, Ausbruch zum nächsten eigenen Depot; ohne Munition und Sprit bricht die Moral, schließlich Kapitulation
            if (u.Dp < 0 && u.Fuel > 0.08 && u.Next < 0 && u.Path.Count == 0 && !u.InDcs
                && w.Objs.Where(o => o.Kind == OK.Depot && o.Side == u.Side && o.Health > 0.2).MinBy(o => Geo.Dist(o.X, o.Z, u.X, u.Z)) is { } home
                && Path(w, u.Node, home.Node, u.Side, false, attack: true) is { Count: > 0 })   // kein Weg: nächste Stunde neu versuchen
            {
                Order(w, u, home.Node, "Ausbruch", $"breaking out of the pocket at {w.Nodes[u.Node].Name} toward {home.Name}");
                w.Log(u.Side, "front", $"{u.Name} is encircled at {w.Nodes[u.Node].Name} and breaks out toward {home.Name}.", u.X, u.Z);
            }
            if (u.Dp < 0 && u.Supply < 0.1 && u.Fuel < 0.1)
            {
                u.Morale = Math.Max(0, u.Morale - 0.04);
                if (u.Morale < 0.15) { w.Units.Remove(u); w.Log(Side.None, "front", $"{u.Name} ({u.Side.De()}) encircled without ammo or fuel – surrenders at {w.Nodes[u.Node].Name}.", u.X, u.Z); }
            }
        }
        foreach (var o in w.Objs)
        {
            // Instandsetzung auch aus 0 %: Plätze schnell, Depots (verteilte Lager) mittel, Werke langsam. Radare: Ersatz in ~10 h, unter 10 % = zerstört. Brücken: Pioniere (unten).
            // Pistenkrater: Pioniere flicken mit Material vom Platz (4 t/h); ohne Material kaum Fortschritt
            double rep = o.Kind switch { OK.Airbase => o.Stock > 20 ? 0.06 : 0.01, OK.Depot => 0.03, OK.Sam or OK.Ewr => o.Health >= 0.1 ? 0.02 : 0, OK.Bridge or OK.Tel => 0.0, _ => 0.01 };
            if (o.Health < 1)
            {
                bool was = Air.Open(o, 0.35);
                o.Fix(rep);                                                   // Teil für Teil, Piste zuerst
                if (o.Kind == OK.Airbase && o.Stock > 20) { o.Stock -= 4; Fl(o.Side, "- Pioniere", 4); }
                if (o.Kind == OK.Airbase && !was && Air.Open(o, 0.35)) w.Log(o.Side, "logistik", $"Runway at {o.Name} repaired – airfield open again.", o.X, o.Z);
            }
            if (o.Kind == OK.Sam && o.Health > 0.3 && o.Missiles < Scenario.Sams[o.Sys].Missiles && w.Time.Hour % 6 == 0) o.Missiles = Scenario.Sams[o.Sys].Missiles;
            if (o.Kind == OK.Airbase && o.Arms != null && o.Stock > 40 && o.P("Ammo dump") > 0.3)   // Waffen aus dem Material des Platzes: ~3 % des Solls je Stunde, 0,2 t je Stück
                foreach (var (c, full) in Air.Full(w, o))
                    if (o.Arms.GetValueOrDefault(c) < full)
                    {
                        int add = Math.Max(1, full * 3 / 100);
                        o.Arms[c] = Math.Min(full, o.Arms.GetValueOrDefault(c) + add); o.Stock -= add * 0.2; Fl(o.Side, "- Waffen", add * 0.2);
                    }
            if (o.Kind == OK.Bridge) o.Flow *= 0.96;                       // gleitender Tagesfluss
        }
        // Pioniere: Brücken baut die Seite wieder auf, der das Gelände gehört – 10 t Material pro Stunde aus einem Depot im Umkreis von 60 km.
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Bridge && o.Health < 1 && !o.InDcs))
        {
            var s = Air.OwnerAt(w, o.X, o.Z);
            var d = w.Objs.Where(d => d.Kind == OK.Depot && d.Side == s && d.Health > 0.2 && d.Stock > 30 && Geo.Dist(d.X, d.Z, o.X, o.Z) < 60000).MinBy(d => Geo.Dist(d.X, d.Z, o.X, o.Z));
            if (d == null) continue;
            double before = o.Health;
            o.Health = Math.Min(1, o.Health + 0.03); d.Stock -= 10; Fl(d.Side, "- Pioniere", 10);
            if (before < 0.3 && o.Health >= 0.3) w.Log(s, "logistik", $"{o.Name} temporarily restored by engineers (material from {d.Name}).", o.X, o.Z);
        }
        // IADS: SAM, deren Stellung der Gegner genau kennt, zieht um (1 h stumm). Seine Sichtung zeigt dann ins Leere.
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Sam && o.Health > 0.3 && !o.InDcs).ToList())
            if (w.Intel(o.Side.Enemy()).TryGetValue($"O{o.Id}", out var k) && Geo.Dist(k.X, k.Z, o.X, o.Z) < 500 && w.Chance(0.15))
            {
                var (nx, nz) = Scenario.Near(w, o.X, o.Z, 6000);
                if (Air.OwnerAt(w, nx, nz) != o.Side) continue;
                o.X = nx; o.Z = nz; o.QuietUntil = w.Time.AddHours(1);
                w.Log(o.Side, "flugabwehr", $"{o.Name} relocates – the enemy had located it.", o.X, o.Z);
            }
        foreach (var q in w.Sqns)
        {
            int due = q.Repair.RemoveAll(t => t <= w.Time);
            q.Ac += due;
            // Ersatz aus der Heimat: 1 Maschine + Pilot pro Tag bis zur Sollstärke
            int have = q.Ac + q.Repair.Count + w.Flights.Where(f => f.Sqn == q.Id && Air.Up(f)).Sum(f => f.Alive);
            if (w.Time.Hour == 6 && have < q.Size)
            {
                q.Ac++;
                q.Pilots.Add(new Pilot { Name = Scenario.PilotName(w, q.Side, q.Country), Skill = Math.Clamp(1 + (q.Side != w.Player ? w.SkillAdj : 0), 0, 3) });
                w.Log(q.Side, "luft", $"{q.Name}: replacement aircraft arrived ({have + 1}/{q.Size}).");
            }
        }
        foreach (var p in w.Sqns.SelectMany(q => q.Pilots)) p.Fatigue = Math.Max(0, p.Fatigue - 0.08);
        var cut = w.Time.AddHours(-12);                                     // Bewegliches veraltet nach 12 h, Stellungen (SAM) nach 48 h
        foreach (var d in new[] { w.IntelRed, w.IntelBlue })
            foreach (var k in d.Where(kv => kv.Value.T < (kv.Key[0] == 'O' ? cut.AddHours(-36) : cut)).Select(kv => kv.Key).ToList()) d.Remove(k);
        w.Flights.RemoveAll(f => f.Done && f.Land < w.Time.AddHours(-30));
        w.Pkgs.RemoveAll(p => !w.Flights.Any(f => f.Pkg == p.Id));
        Victory(w);
    }

    /// Raketen und Drohnen (Startlage): Salve auf das wertvollste bekannte Ziel des Gegners (Häfen, Werke, Depots, Plätze).
    /// Jede Patriot-Batterie in Reichweite fängt mit 60 % ab (2 Flugkörper). Getroffene oder zerstörte Startstellung = weniger bzw. keine Salven.
    static void Rockets(World w)
    {
        foreach (var (s, from, perDay) in w.S.Rockets)
        {
            if (w.Time.Hour % (24 / perDay) != 0) continue;
            var src = w.Objs.FirstOrDefault(o => o.Name == from);
            if (src == null || src.Side != s || src.Health < 0.3) continue;
            if (RocketTarget(w, src, 1e9) is { } t) Salvo(w, src, t, 0.3 * src.Health);
        }
    }

    /// Wertvollstes bekanntes Ziel des Gegners in Reichweite, das die Kriegsphase freigibt.
    static Obj? RocketTarget(World w, Obj src, double range)
    {
        var know = w.Intel(src.Side);
        return w.Objs.Where(o => o.Side == src.Side.Enemy() && o.Kind is OK.Port or OK.Factory or OK.Depot or OK.Airbase && o.Health > 0.3 && Roe(w, o) && know.ContainsKey($"O{o.Id}") && Geo.Dist(o.X, o.Z, src.X, src.Z) < range)
            .MaxBy(o => o.Value * o.Health * (0.6 + 0.8 * w.Rnd()));
    }

    /// Einschlag auf t – jede Patriot-Batterie in Reichweite fängt mit 60 % ab (2 Flugkörper).
    static void Salvo(World w, Obj src, Obj t, double dmg)
    {
        var pat = w.Objs.Where(o => o.Side == t.Side && o.Kind == OK.Sam && o.Sys == "Patriot" && o.Health > 0.3 && o.Missiles >= 2 && !o.Decoy && Geo.Dist(o.X, o.Z, t.X, t.Z) < o.Range).ToList();
        foreach (var p in pat) p.Missiles -= 2;
        if (pat.Count > 0 && w.Chance(1 - Math.Pow(0.4, pat.Count)))
        {
            w.Log(Side.None, "luft", $"Missile attack from {src.Name} on {t.Name} – intercepted ({string.Join(", ", pat.Select(p => p.Name))}).", t.X, t.Z);
            return;
        }
        double before = t.Health;
        t.Health = Math.Max(0, t.Health - dmg);
        if (t.Kind == OK.Depot) { t.Stock *= 0.85; t.Fuel *= 0.85; }
        w.Log(Side.None, "luft", $"Missile impact from {src.Name}: {t.Name} hit, condition {before:P0} → {t.Health:P0}.", t.X, t.Z);
    }

    /// Raketenwerfer (Scud/MLRS): schießen bevorzugt nachts, danach packen sie zusammen. Der Start verrät die Stellung (Satellit, AWACS) –
    /// Jagd auf den Werfer. Kennt der Gegner die Stellung genau, fährt er weiter (shoot and scoot). Täglich 2 Raketen Nachschub.
    static void Launchers(World w)
    {
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Tel && o.Health > 0.3 && !o.InDcs).ToList())
        {
            var foe = w.Intel(o.Side.Enemy());
            if (w.Time.Hour == 6) o.Missiles = Math.Min(6, o.Missiles + 2);
            if (w.Time >= o.QuietUntil && foe.TryGetValue($"O{o.Id}", out var k) && Geo.Dist(k.X, k.Z, o.X, o.Z) < 500)
            {
                var (nx, nz) = Scenario.Near(w, o.X, o.Z, 15000);
                if (Air.OwnerAt(w, nx, nz) != o.Side) continue;
                (o.X, o.Z, o.QuietUntil) = (nx, nz, w.Time.AddHours(1));
                w.Log(o.Side, "luft", $"{o.Name} moved to a new hide – the enemy had located it.", o.X, o.Z);
                continue;
            }
            if (w.Time < o.QuietUntil || o.Missiles == 0 || !w.Chance(w.Daylight ? 0.1 : 0.25) || RocketTarget(w, o, o.Sys == "MLRS" ? 80000 : 300000) is not { } t) continue;
            o.Missiles--;
            Salvo(w, o, t, 0.25);
            foe[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = "Launch detected", Str = o.Health };
            o.QuietUntil = w.Time.AddHours(1 + w.Rnd());
        }
    }

    /// Luftlandung am Ziel: abgesetzte Kräfte (Stärke nach gelandeten Maschinen) nehmen den Ort, wenn ihn niemand hält.
    public static void Drop(World w, Flight f)
    {
        var n = w.Nodes[f.Target];
        var tpl = w.S.Oob.Where(o => o.S == f.Side).OrderBy(o => o.K == UK.Motor ? 0 : 1).First().Tpl;
        var u = Scenario.NewUnit(w, f.Side, UK.Motor, -1, "reserve", n, tpl, $"Air assault group {f.Callsign}");
        (u.Str, u.Supply, u.Order) = (Math.Clamp(0.25 * f.Alive, 0.1, 0.5), 0.5, "Halten");
        f.Result = $"Troops landed at {n.Name}.";
        if (n.Owner != f.Side && !w.Units.Any(x => x.Side != f.Side && x.Node == n.Id)) Capture(w, n, f.Side, "air assault");
        w.Log(f.Side, "front", $"{f.Callsign}: air assault group landed at {n.Name} behind the enemy front.", n.X, n.Z);
        News(w, "AIR ASSAULT", $"{f.Side.De()} troops land at {n.Name}.");
    }

    // ---------- Kriegsphasen, Verbündete, Waffenstillstand ----------

    /// Kriegsphase (ROE): 0 nur militärische Ziele, 1 + Werke/Häfen, 2 + Regierung/Generalstab (Gefechtsstände ab Wert 5).
    public static bool Roe(World w, Obj o) => o.Kind switch { OK.Factory or OK.Port => w.Esc >= 1, OK.Cp => o.Value < 5 || w.Esc >= 2, _ => true };

    public static void Escalate(World w, int lvl, string why)
    {
        if (w.Esc >= lvl) return;
        w.Esc = lvl;
        var what = lvl == 1 ? "factories, power plants and ports are cleared as targets" : "government and high-command targets are cleared";
        w.Log(Side.None, "ato", $"Escalation – {why}: {what}.");
        News(w, $"WAR ESCALATES – PHASE {lvl + 1}", $"{why}: {what}.");
    }

    static double Ground(World w, Side s) => w.Units.Where(u => u.Side == s).Sum(u => u.Str * Pow[u.Kind]);

    /// Stunde einer Verstärkung: H ≥ 0 ab Kriegsbeginn, H < 0 so viele Stunden nach dem Kriegseintritt des Verbündeten (-1 = noch nicht).
    static int Due(World w, int h, Side s) => h >= 0 ? h : (s == Side.Red ? w.JoinRed : w.JoinBlue) is var j and >= 0 ? j - h : -1;

    /// Verbündeter tritt ein (Setup.Join): wenn der Krieg auf Phase 3 eskaliert oder die Seite nach zwei Tagen klar unterliegt.
    static void Join(World w, int hour)
    {
        foreach (var (s, text) in w.S.Join)
        {
            if ((s == Side.Red ? w.JoinRed : w.JoinBlue) >= 0 || !(w.Esc >= 2 || hour >= 48 && Ground(w, s) < 0.6 * Ground(w, s.Enemy()))) continue;
            if (s == Side.Red) w.JoinRed = hour; else w.JoinBlue = hour;
            w.Log(Side.None, "front", text);
            News(w, "NEW ALLY ENTERS THE WAR", text);
        }
    }

    /// Waffenstillstand: verliert die KI-Seite nach vier Tagen klar, bietet sie ihn an (höchstens zweimal, 48 h Abstand, 12 h gültig). Annehmen = Kriegsende (Web).
    static void Truce(World w, int hour)
    {
        var ai = w.Player.Enemy();
        if (w.Truce != "" && w.Time >= w.TruceAt.AddHours(12)) { w.Truce = ""; w.Log(w.Player, "ato", "The ceasefire offer has lapsed – the war goes on."); }
        if (w.Over != "" || hour < 96 || w.Truce != "" || w.Truces >= 2 || w.Time < w.TruceAt.AddHours(48) || Ground(w, ai) >= 0.7 * Ground(w, w.Player)) return;
        (w.Truces, w.TruceAt) = (w.Truces + 1, w.Time);
        w.Truce = $"{ai.De()} offers a ceasefire along the current front line.";
        w.Log(w.Player, "ato", $"{w.Truce} Accept or reject it in the campaign view (valid 12 h).");
        News(w, "CEASEFIRE OFFER", w.Truce);
    }

    /// Verstärkung reist an: per Schiff in den Hafen, per Bahn vom Kartenrand zum Depot – oder wird vor Ort aufgestellt.
    static void Reinforce(World w, int i)
    {
        var r = w.S.Reinf[i];
        if (Scenario.Find(w, r.At) is not { } t) return;
        var to = t.Owner == r.S ? w.Objs.Where(o => o.Node == t.Id && o.Side == r.S && o.Kind is OK.Port or OK.Depot).OrderBy(o => o.Kind == OK.Port ? 0 : 1).FirstOrDefault() : null;
        if (to?.Kind == OK.Port && to.Health > 0 && Scenario.SeaGate(w, to) is var (x, z, _))
        {
            w.Convoys.Add(new Convoy { Id = w.NewId(), Side = r.S, From = -1, To = to.Id, Trucks = 4, Trucks0 = 4, X = x, Z = z, Ship = true, Reinf = i });
            w.Log(r.S, "front", $"Troopship with {r.Name} en route to {to.Name}.", x, z);
            return;
        }
        if (to != null)
        {
            var best = w.S.Gates.Where(g => g.S == r.S).Select(g => Scenario.Find(w, g.At)).OfType<Node>().Where(g => g.Owner == r.S && g.Id != t.Id)   // am Einspeiseort selbst: gleich da
                .Select(g => (g, p: Path(w, g.Id, t.Id, r.S, true, rail: true))).Where(x => x.p != null).OrderBy(x => PathLen(w, x.g.Id, x.p!)).FirstOrDefault();
            if (best.p != null)
            {
                w.Convoys.Add(new Convoy { Id = w.NewId(), Side = r.S, From = -1, Origin = best.g.Id, To = to.Id, Path = best.p, Trucks = 20, Trucks0 = 20, X = best.g.X, Z = best.g.Z, Rail = true, Reinf = i });
                w.Log(r.S, "front", $"Troop train with {r.Name} rolling from {best.g.Name} to {t.Name}.", best.g.X, best.g.Z);
                return;
            }
        }
        Spawn(w, i, t, 1);
    }

    static void Spawn(World w, int i, Node at, double str)
    {
        var r = w.S.Reinf[i];
        var n = at.Owner == r.S ? at : w.Nodes.Where(n => n.Owner == r.S).MinBy(n => Geo.Dist(n.X, n.Z, at.X, at.Z))!;
        var u = Scenario.NewUnit(w, r.S, r.K, -1, "reserve", n, r.Tpl, r.Name);
        u.Str = Math.Clamp(str, 0.1, 1);
        w.Log(r.S, "front", $"Reinforcements: {u.Name} arrives at {n.Name}{(str < 0.99 ? $" ({u.Str:P0} combat-ready – losses in transit)" : "")}.", n.X, n.Z);
    }

    /// Kriegsende: eine Seite hat alle Schlüsselorte der Achsen oder alle Flugplätze verloren oder nur noch 20 % der gegnerischen Bodenstärke.
    static void Victory(World w)
    {
        if (w.Over != "") return;
        double Str(Side s) => w.Units.Where(u => u.Side == s).Sum(u => u.Str);
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            double r = Str(s) / Math.Max(0.01, Str(s.Enemy()));
            var econ = w.Objs.Where(o => o.Side == s && o.Kind is OK.Port or OK.Factory or OK.Depot or OK.Cp).ToList();
            double e = econ.Sum(o => o.Value * o.Health) / Math.Max(0.01, econ.Sum(o => o.Value));
            bool land = w.Axes.Count > 0;                                   // ohne Landfront (Luft-/Seekrieg) zählt die Kriegswirtschaft – frühestens nach 7 Tagen (Instandsetzung läuft)
            string why = land && w.Axes.SelectMany(a => a.Keys).All(k => w.Nodes[k].Owner != s) ? "has lost all key towns"
                : !w.Objs.Any(o => o.Kind == OK.Airbase && o.Side == s && o.Health > 0) ? "has lost all airfields"
                : land && r < 0.2 ? $"is down to {r:P0} of the enemy's ground strength"
                : !land && econ.Count > 0 && e < 0.25 && w.Time >= w.Start.AddDays(7) ? $"has lost its war economy (ports, factories, depots, HQs at {e:P0})" : "";
            if (why == "") continue;
            w.Over = $"End of war – {s.Enemy().De()} wins: {s.De()} {why}.";
            w.Speed = 0;
            w.Log(Side.None, "kriegsende", w.Over);
            return;
        }
    }

    static void Weather(World w)
    {
        // Fronten ziehen von West nach Ost über das Kampfgebiet: davor Aufzug + Südwestwind, an der Front Regen und tiefe Basis, dahinter Aufklaren mit Nordwest.
        var x = w.Wx;
        double mid = w.Nodes.Average(n => n.Z);
        bool ahead = x.FrontZ < mid - 60000;
        x.FrontZ += x.FrontKmh * 1000;
        if (ahead && x.FrontZ >= mid - 60000) w.Log(Side.None, "wetter", $"Weather front reaches the battle area: {(x.FrontStr > 0.75 ? "strong – " : "")}rain, low clouds and wind for about {120 / x.FrontKmh:0} hours.");
        if (x.FrontKmh == 0 || x.FrontZ > mid + 400000)
        {
            x.FrontZ = mid - 500000 - w.Rnd() * 700000; x.FrontKmh = 25 + w.Rnd() * 20; x.FrontStr = 0.5 + w.Rnd() * 0.5;
        }
        double dz = mid - x.FrontZ, k = x.FrontStr * Math.Exp(-Math.Pow(dz / 80000, 2)), shield = x.FrontStr * Math.Exp(-Math.Pow((dz - 60000) / 160000, 2));
        x.FrontKm = dz / 1000;
        x.Cloud = Math.Clamp(0.15 + 0.85 * shield + (w.Rnd() - 0.5) * 0.1, 0, 1);
        x.Rain = k > 0.55 || dz < 0 && k > 0.3 && w.Rnd() < 0.4;          // hinter der Front noch Schauer
        x.Base = Math.Clamp(3200 - 2700 * k + (w.Rnd() - 0.5) * 300, 400, 3500);
        x.Wind = 3 + 11 * k + w.Rnd() * 2;
        x.WindDir = (dz > 0 ? 220 : 300) + (w.Rnd() - 0.5) * 30;
        x.Qnh = 766 - 16 * k - (dz > 0 ? 4 * shield : 0);
        x.Temp = w.S.Temp + 6 * Math.Sin((w.Time.Hour - 9) / 24.0 * 2 * Math.PI) - x.Cloud * 3 - (dz < 0 ? 3 * x.FrontStr : 0);
        x.Vis = x.Rain ? (k > 0.8 ? 4000 : 8000) : x.Cloud > 0.9 ? 15000 : 40000;
        // Schnee auf den Hochpässen (ca. 2400 m): Niederschlag bei unter 13 °C im Tal. Räumen dauert 12 h nach dem letzten Schneefall.
        bool was = x.Snow;
        if (x.Rain && x.Temp < 13) x.SnowUntil = w.Time.AddHours(12);
        x.Snow = w.Time < x.SnowUntil;
        if (x.Snow != was && w.S.SnowText != "") w.Log(Side.None, "wetter", x.Snow ? $"Snowfall in the high mountains: {w.S.SnowText} closed." : $"Passes cleared: {w.S.SnowText} open again.");
    }

    // ---------- Operationen: Schwerpunkt je Seite ----------

    /// Sicherungslinie: an jeder Straße über die Front je Seite ein Stützpunkt (Zug) bei 35 % vom eigenen Ort Richtung Feind, wo kein Verband steht,
    /// dazwischen alle ~9 km entlang der Frontlinie (auch im Gebirge) 3 km hinter der Linie. Gehört dem nächsten eigenen Kampfverband
    /// (Verluste in DCS treffen ihn). Abgeleitet aus Besitz und Verbänden, nicht gespeichert.
    public record Post(Side Side, double X, double Z, int Unit, int Toward);
    public static List<Post> Line(World w)
    {
        var l = new List<Post>();
        foreach (var e in w.Edges.Where(e => !e.Rail))
        {
            Node a = w.Nodes[e.A], b = w.Nodes[e.B];
            if (a.Owner == b.Owner || a.Owner == Side.None || b.Owner == Side.None) continue;
            foreach (var (o, t) in new[] { (a, b), (b, a) })
            {
                if (w.Units.Any(u => u.Node == o.Id && u.Side == o.Owner && u.Next < 0)) continue;
                double x = o.X + (t.X - o.X) * 0.35, z = o.Z + (t.Z - o.Z) * 0.35;
                if (w.Units.Where(u => u.Side == o.Owner && u.Kind is not (UK.Art or UK.Shorad)).MinBy(u => Geo.Dist(u.X, u.Z, x, z)) is { } u) l.Add(new(o.Owner, x, z, u.Id, t.Id));
            }
        }
        foreach (var m in FrontLine(w))
            foreach (var s in new[] { Side.Red, Side.Blue })
            {
                if (l.Any(p => p.Side == s && Geo.Dist(p.X, p.Z, m.X, m.Z) < 9000) || w.Units.Any(u => u.Side == s && Geo.Dist(u.X, u.Z, m.X, m.Z) < 9000)) continue;
                if (w.Nodes.Where(n => n.Owner == s).MinBy(n => Geo.Dist(n.X, n.Z, m.X, m.Z)) is not { } o || w.Nodes.Where(n => n.Owner == s.Enemy()).MinBy(n => Geo.Dist(n.X, n.Z, m.X, m.Z)) is not { } t) continue;
                double d = Math.Max(1, Geo.Dist(o.X, o.Z, m.X, m.Z)), k = Math.Min(3000, d * 0.4) / d, x = m.X + (o.X - m.X) * k, z = m.Z + (o.Z - m.Z) * k;
                if (w.Units.Where(u => u.Side == s && u.Kind is not (UK.Art or UK.Shorad)).MinBy(u => Geo.Dist(u.X, u.Z, x, z)) is { } u) l.Add(new(s, x, z, u.Id, t.Id));
            }
        return l;
    }

    /// Frontlinie wie auf der Karte: Nulllinie von (Abstand zum nächsten blauen − zum nächsten roten Ort) im 2,5-km-Raster, bis 40 km von Orten,
    /// ohne Meer. Punkte auf der Linie, nur bei Besitzwechsel neu gerechnet.
    static (Theater? Th, string Key, List<P> Pts) frontCache = (null, "", []);
    public static List<P> FrontLine(World w)
    {
        var key = string.Concat(w.Nodes.Select(n => (int)n.Owner));
        if (frontCache.Th == w.Th && frontCache.Key == key) return frontCache.Pts;
        const double C = 2500;
        double x0 = w.Nodes.Min(n => n.X) - 40000, z0 = w.Nodes.Min(n => n.Z) - 40000;
        int rows = (int)((w.Nodes.Max(n => n.X) + 40000 - x0) / C), cols = (int)((w.Nodes.Max(n => n.Z) + 40000 - z0) / C);
        var D = new double[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                double x = x0 + r * C, z = z0 + c * C, bb = double.MaxValue, br = double.MaxValue;
                D[r, c] = double.NaN;
                if (w.Th.Water(x, z)) continue;
                foreach (var n in w.Nodes)
                {
                    double q = (n.X - x) * (n.X - x) + (n.Z - z) * (n.Z - z);
                    if (n.Owner == Side.Blue) bb = Math.Min(bb, q); else if (n.Owner == Side.Red) br = Math.Min(br, q);
                }
                if (Math.Min(bb, br) < 1.6e9) D[r, c] = Math.Sqrt(bb) - Math.Sqrt(br);
            }
        var pts = new List<P>();
        void Cut(int r, int c, int r2, int c2)
        {
            double a = D[r, c], b = D[r2, c2];
            if (double.IsNaN(a) || double.IsNaN(b) || (a < 0) == (b < 0)) return;
            double t = a / (a - b);
            pts.Add(new(x0 + (r + (r2 - r) * t) * C, z0 + (c + (c2 - c) * t) * C));
        }
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++) { if (c + 1 < cols) Cut(r, c, r, c + 1); if (r + 1 < rows) Cut(r, c, r + 1, c); }
        frontCache = (w.Th, key, pts);
        return pts;
    }

    static IEnumerable<Unit> AtFront(World w, Side s, int node) => w.Units.Where(u => u.Side == s && u.Node == node && u.Next < 0 && u.Kind is not (UK.Art or UK.Shorad));

    /// Je Seite höchstens ein Schwerpunkt: Bereitstellung (Reserven, Artillerie, Vorrat heran, 12–24 h) → Angriff (2 h Feuervorbereitung, dann Sturm)
    /// → Kulmination (Munition, Sprit, Verluste, Moral, 72 h) → Konsolidierung (12 h). Danach greift oft der Gegner an derselben Achse an.
    static void Ops(World w)
    {
        foreach (var ax in w.Axes.Where(a => a.Op != ""))
        {
            var s = ax.OpSide; double h = (w.Time - ax.OpSince).TotalHours;
            if (Front(w, ax) is not var (rf, bf)) { EndOp(w, ax, $"Axis {ax.Name}: front dissolved."); continue; }
            int own = s == Side.Red ? rf : bf, enemy = s == Side.Red ? bf : rf;
            if (ax.Op != "Konsolidierung") foreach (var u in AtFront(w, s, own)) u.Axis = ax.Id;   // im Schwerpunkt kämpft alles, was am Frontort steht
            var at = AtFront(w, s, own).ToList();
            var op = w.Units.Where(u => u.Side == s && u.Axis == ax.Id && u.Kind is not (UK.Art or UK.Shorad) && u.Order is not ("Auffrischen" or "Rückzug")
                                        && Geo.Dist(u.X, u.Z, w.Nodes[own].X, w.Nodes[own].Z) < 60000).ToList();   // auch die schon Vorgerückten
            var (A, D) = Odds(w, s, at, w.Nodes[enemy]);
            double mun = op.Count == 0 ? 0 : op.Average(u => u.Supply), pol = op.Count == 0 ? 0 : op.Average(u => u.Fuel);
            switch (ax.Op)
            {
                case "Bereitstellung":
                    if (h >= 6 && A < 1.3 * D) Reserves(w, s, ax, own, 1, $"reinforcing the attack on {w.Nodes[enemy].Name}");
                    if (!ax.Warned && h >= 6 && w.Chance(0.25))
                    {
                        ax.Warned = true;
                        w.Log(s.Enemy(), "front", $"Recon: {s.De()} massing forces at {w.Nodes[own].Name} – attack on axis {ax.Name} likely.", w.Nodes[own].X, w.Nodes[own].Z);
                    }
                    bool moving = w.Units.Any(u => u.Side == s && u.Axis == ax.Id && u.Order == "Verlegen");
                    if (h >= 12 && (!moving || h >= 24) && mun >= 0.6 && pol >= 0.5 && A >= 1.1 * D && (w.Daylight || s == Side.Blue) && w.Chance(0.35))
                    {
                        Stage(ax, "Angriff", w.Time);
                        w.Log(Side.None, "front", $"{s.De()} attacks on axis {ax.Name}: artillery fire on {w.Nodes[enemy].Name}, then assault ({A:0.0} vs. {D:0.0}).", w.Nodes[enemy].X, w.Nodes[enemy].Z);
                    }
                    else if (h >= 30) EndOp(w, ax, $"{s.De()} calls off the attack on axis {ax.Name}: {(A < 1.1 * D ? $"too weak ({A:0.0} vs. {D:0.0})" : "insufficient supply")}.");
                    break;
                case "Angriff":
                    double str = op.Count == 0 ? 0 : op.Average(u => u.Str), mor = op.Count == 0 ? 0 : op.Average(u => u.Morale);
                    string? why = op.Count == 0 || str < 0.45 ? "losses too high" : mun < 0.3 ? "ammo exhausted" : pol < 0.15 ? "fuel exhausted" : mor < 0.4 ? "morale exhausted"
                        : h >= 72 ? "forces exhausted after three days" : h >= 8 && A <= D && !op.Any(u => u.Order == "Angriff") ? $"no breakthrough ({A:0.0} vs. {D:0.0})" : null;
                    if (why != null && w.Time >= ax.Pause)
                    {
                        Stage(ax, "Konsolidierung", w.Time);
                        foreach (var u in op.Where(u => u.Order == "Angriff")) { u.Order = "Halten"; u.Path.Clear(); u.Why = $"attack halted: {why}"; }
                        w.Log(Side.None, "front", $"{s.De()} attack on axis {ax.Name} stalls at {w.Nodes[enemy].Name}: {why}.", w.Nodes[enemy].X, w.Nodes[enemy].Z);
                    }
                    break;
                case "Konsolidierung":
                    if (h >= 12) EndOp(w, ax, null);
                    break;
            }
        }
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            if (w.Axes.Any(a => a.Op != "" && a.OpSide == s) || w.Time < w.CmdOf(s).NextOp) continue;
            Axis? best = null; double bs = 0; int bown = -1, ben = -1;
            foreach (var ax in w.Axes.Where(a => a.Op == "" && w.Time >= a.Pause))
            {
                if (Front(w, ax) is not var (rf, bf)) continue;
                int own = s == Side.Red ? rf : bf, enemy = s == Side.Red ? bf : rf;
                var force = AtFront(w, s, own).Concat(FreeReserves(w, s, own).Take(2)).ToList();
                var (A, D) = Odds(w, s, force, w.Nodes[enemy]);
                if (A < 1.5 || force.Average(u => u.Str) < 0.6) continue;              // mindestens zwei kampfkräftige Bataillone
                bool counter = ax.OpSide == s.Enemy() && w.Time < ax.OpSince.AddHours(24);   // Gegner hat sich hier gerade festgelaufen
                double score = A / Math.Max(D, 0.05) * (ax.Push == s ? 1.15 : 0.85) * (counter ? 1.4 : 1) * (0.85 + 0.3 * w.Rnd());
                if (score > bs) { bs = score; best = ax; bown = own; ben = enemy; }
            }
            if (best == null || bs < 1.25) continue;
            bool ctr = best.OpSide == s.Enemy() && w.Time < best.OpSince.AddHours(24);
            Stage(best, "Bereitstellung", w.Time); best.OpSide = s; best.Warned = false;
            int n = Reserves(w, s, best, bown, 2, $"staging for the attack on {w.Nodes[ben].Name}");
            w.Log(s, "front", $"Commander {s.De()}: {(ctr ? "counterattack" : "main effort")} on axis {best.Name} – staging at {w.Nodes[bown].Name}{(n > 0 ? $", {n} reserve(s) moving up" : "")}, attack in 12–24 h.", w.Nodes[bown].X, w.Nodes[bown].Z);
        }
    }

    static void Stage(Axis ax, string op, DateTime t) { ax.Op = op; ax.OpSince = t; }
    static void EndOp(World w, Axis ax, string? why)
    {
        if (why != null) w.Log(Side.None, "front", why);
        ax.Op = ""; ax.OpSince = w.Time;                                    // OpSide bleibt: Gegenangriff erkennen
        w.CmdOf(ax.OpSide).NextOp = w.Time.AddHours(12);
    }

    static IEnumerable<Unit> FreeReserves(World w, Side s, int node) =>
        w.Units.Where(u => u.Side == s && u.Role == "reserve" && u.Order == "Reserve" && u.Next < 0 && u.Path.Count == 0 && u.Kind is not (UK.Art or UK.Shorad)
                           && Geo.Dist(u.X, u.Z, w.Nodes[node].X, w.Nodes[node].Z) < 250000)
               .OrderBy(u => Geo.Dist(u.X, u.Z, w.Nodes[node].X, w.Nodes[node].Z));

    static int Reserves(World w, Side s, Axis ax, int node, int max, string why)
    {
        var l = FreeReserves(w, s, node).Take(max).ToList();
        foreach (var u in l) { u.Role = "front"; u.Axis = ax.Id; Order(w, u, node, "Verlegen", why); }
        return l.Count;
    }

    /// Örtliche Führung einer Achse: an die Front verlegen, Sturm im Schwerpunkt, sonst nur bei klarer Überlegenheit angreifen, Reserven zur Abwehr, Auffrischen.
    static void Operate(World w, Axis ax)
    {
        if (Front(w, ax) is not var (rf, bf)) return;
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            int own = s == Side.Red ? rf : bf, enemy = s == Side.Red ? bf : rf;
            var on = w.Nodes[own]; var en = w.Nodes[enemy];
            bool myOp = ax.Op != "" && ax.OpSide == s;
            var mine = w.Units.Where(u => u.Side == s && u.Axis == ax.Id && !u.InDcs).ToList();
            foreach (var u in mine.Where(u => u.Role == "front" && u.Order != "Rückzug" && u.Order != "Auffrischen" && u.Node != own && u.Next < 0 && u.Path.Count == 0
                                              && (u.Kind != UK.Art || myOp && Geo.Dist(u.X, u.Z, en.X, en.Z) > 25000)))
                Order(w, u, own, "Verlegen", u.Kind == UK.Art ? $"artillery moving into position against {en.Name}" : $"moving up to the front at {on.Name}");
            foreach (var u in mine.Where(u => u.Order == "Rückzug" && u.Next < 0 && u.Path.Count == 0))
            {
                if (u.Str < 0.6 || u.Morale < 0.5) { var dp = NearestDepot(w, u); if (dp != null) Order(w, u, dp.Node, "Auffrischen", $"refitting at {dp.Name}"); else u.Order = "Halten"; }
                else { u.Order = "Halten"; u.Role = "front"; }
            }
            var at = mine.Where(u => u.Node == own && u.Next < 0).ToList();
            bool Combatant(Unit u) => u.Kind is not (UK.Art or UK.Shorad);
            bool Fit(Unit u) => u.Supply >= 0.3 && u.Fuel >= 0.1 && u.Morale > 0.45;
            var (A, D) = Odds(w, s, at.Where(Combatant), en);                                        // unser Angriff
            var (EA, ED) = Odds(w, s.Enemy(), w.Units.Where(u => u.Side != s && Combatant(u) && (u.Node == enemy && u.Next < 0 || u.Order == "Angriff" && Geo.Dist(u.X, u.Z, on.X, on.Z) < 40000)), on);   // sein Angriff, auch anrückend
            bool assault = myOp && ax.Op == "Angriff" && w.Time >= ax.OpSince.AddHours(2) && w.Time >= ax.Pause && A > D;
            bool local = ax.Op == "" && w.Time >= ax.Pause && at.Count > 0 && at.Average(u => u.Supply) > 0.5 && at.Average(u => u.Fuel) > 0.35
                         && at.Average(u => u.Morale) > 0.55 && A > 2.0 * Math.Max(D, 0.05);   // ohne Schwerpunkt nur bei klarer Überlegenheit
            bool was = at.Any(u => u.Order == "Angriff");
            foreach (var u in at.Where(Combatant))
            {
                if ((assault || local) && Fit(u)) { if (u.Order != "Angriff") { u.Order = "Angriff"; u.Why = $"attacking {en.Name} ({A:0.0} vs. {D:0.0})"; u.Dig = 0; } }
                else if (u.Order == "Angriff") { u.Order = "Verteidigen"; u.Why = !Fit(u) ? $"not fit to attack (ammo {u.Supply:P0}, fuel {u.Fuel:P0}, morale {u.Morale:P0})" : $"too weak to attack {en.Name} ({A:0.0} vs. {D:0.0})"; }
            }
            if (local && !was && at.Any(u => u.Order == "Angriff")) w.Log(s, "front", $"Local attack by {s.De()} on {en.Name} ({A:0.0} vs. {D:0.0}).", en.X, en.Z);
            foreach (var u in at.Where(u => u.Order is "Halten" or "Verlegen" or "Reserve"))
            { u.Order = "Verteidigen"; u.Why = myOp && ax.Op == "Bereitstellung" ? $"staging at {on.Name} for the attack on {en.Name}" : $"defending {on.Name}"; }
            // Reserven zur Abwehr, wenn der Gegner durchbrechen könnte
            if (!myOp && EA > ED * 0.9) Reserves(w, s, ax, own, 1, $"reserve to {on.Name}: enemy superior ({EA:0.0} vs. {ED:0.0})");
            if (!(myOp && ax.Op == "Angriff"))
                foreach (var u in at.Where(u => u.Str < 0.3 && u.Order != "Angriff")) { var dp = NearestDepot(w, u); if (dp != null && dp.Node != u.Node) Order(w, u, dp.Node, "Auffrischen", $"badly weakened, refitting at {dp.Name}"); }
        }
    }

    // ---------- Logistik ----------

    public static Obj? Depot(World w, Unit u, double maxCost) =>
        w.Objs.Where(o => o.Kind == OK.Depot && o.Side == u.Side && o.Health > 0.2 && o.Node >= 0 && Geo.Dist(o.X, o.Z, u.X, u.Z) < maxCost * 1.2)
              .Select(o => (o, p: Path(w, u.Node, o.Node, u.Side, true, maxCost)))
              .Where(t => t.p != null).OrderBy(t => PathLen(w, u.Node, t.p!)).Select(t => t.o).FirstOrDefault();

    static Obj? NearestDepot(World w, Unit u) => Depot(w, u, 120000);

    /// Versorgungslinie eines Verbands: nächstes erreichbares Depot mit Vorrat (leere nur als Notlösung), Weg durch eigenes Gebiet ohne zerstörte Brücken.
    static (Obj? Dp, List<int> Path) Source(World w, Unit u)
    {
        (Obj? Dp, List<int> Path, double Score) best = (null, [], double.MaxValue);
        foreach (var o in w.Objs.Where(o => o.Kind == OK.Depot && o.Side == u.Side && o.Health > 0.2 && o.Node >= 0 && Geo.Dist(o.X, o.Z, u.X, u.Z) < LineKm))
        {
            if (Path(w, u.Node, o.Node, u.Side, true, LineKm * 2.5) is not { } p) continue;
            double len = PathLen(w, u.Node, p);
            if (len > LineKm) continue;
            double score = len + (o.Stock < 20 ? 5e5 : 0) + (o.Fuel < 20 ? 5e5 : 0);
            if (score < best.Score) best = (o, p, score);
        }
        return (best.Dp, best.Path);
    }

    public static string ObjName(World w, int id) => w.Objs.FirstOrDefault(o => o.Id == id)?.Name ?? "?";

    /// Ladung zu einem Depot mit Platz für genau diese Güter schicken (Armeedepots zuerst, das leerste/nächste vorn): Güterzug über das Gleis (bis max t),
    /// sonst Lkw-Kolonne (160 t) über die Straße.
    static bool Dispatch(World w, Side s, Obj? src, int node, double mun, double pol, double max)
    {
        var n0 = w.Nodes[node];
        var dps = w.Objs.Where(o => o.Kind == OK.Depot && o.Side == s && o.Health > 0.2 && (mun > 1 && o.Stock < o.Cap * 0.9 || pol > 1 && o.Fuel < o.Cap * 0.9))
            .OrderBy(o => o.Name.StartsWith("Army") ? 0 : 1)
            .ThenBy(o => (mun > 1 ? o.Stock : 0) / o.Cap + (pol > 1 ? o.Fuel : 0) / o.Cap + Geo.Dist(o.X, o.Z, n0.X, n0.Z) / 300000).ToList();
        foreach (bool rail in new[] { true, false })
            foreach (var d in dps)
            {
                if (Path(w, node, d.Node, s, true, rail: rail) is not { } path) continue;
                double k = Math.Min(1, (rail ? max : 160) / Math.Max(1, mun + pol)), m = mun * k, p = pol * k;
                int n = (int)Math.Ceiling((m + p) / (rail ? 25 : 10));
                w.Convoys.Add(new Convoy { Id = w.NewId(), Side = s, From = src?.Id ?? -1, Origin = node, To = d.Id, Path = path, Tons = m, Fuel = p, Trucks = n, Trucks0 = n, X = w.Nodes[node].X, Z = w.Nodes[node].Z, Rail = rail });
                if (src != null) { src.Stock -= m; src.Fuel -= p; }
                return true;
            }
        return false;
    }

    static void Logistics(World w)
    {
        // Häfen: jeden Morgen läuft ein Frachter von See ein, solange keiner unterwegs ist – zu Kriegsbeginn sind die ersten schon unterwegs.
        foreach (var p in w.Objs.Where(o => o.Kind == OK.Port && o.Health > 0 && o.Side != Side.None && (w.Time.Hour == 6 || w.Time - w.Start <= TimeSpan.FromHours(1)) && !w.Convoys.Any(c => c.Ship && !c.Navy && c.Reinf < 0 && c.To == o.Id)))
        {
            if (Scenario.SeaGate(w, p) is not var (x, z, k)) { w.Log(p.Side, "logistik", $"{p.Name}: no freighter – the port of origin is destroyed.", p.X, p.Z); continue; }
            if (w.S.Sea.Any(g => g.From == p.Name)) continue;                 // selbst Herkunftshafen
            var (m, f) = w.S.Cargo(p); m *= k * Sup(w, p.Side); f *= k * Sup(w, p.Side);
            w.Convoys.Add(new Convoy { Id = w.NewId(), Side = p.Side, From = -1, To = p.Id, Tons = m, Fuel = f, Trucks = 8, Trucks0 = 8, X = x, Z = z, Ship = true });
            Fl(p.Side, "+ Frachter", m + f);
        }
        // Einspeisung von außerhalb der Karte: um 05 und 17 Uhr je Bahnanschluss ein Zug – nur solange der Ort gehalten wird.
        if (w.Time.Hour % 12 == 5)
            foreach (var (s, at, mun, pol) in w.S.Gates)
            {
                if (Scenario.Find(w, at) is not { } g) continue;
                if (g.Owner != s) { w.Log(s, "logistik", $"Supply feed via {g.Name} cut – town in enemy hands.", g.X, g.Z); continue; }
                if (Dispatch(w, s, null, g.Id, mun * Sup(w, s), pol * Sup(w, s), 800)) Fl(s, "+ Einspeisung", (mun + pol) * Sup(w, s));
                else w.Log(s, "logistik", $"Supply feed via {g.Name}: no route to a depot with space.", g.X, g.Z);
            }
        // Werke produzieren Munition (max. 600 t Lager), Häfen löschen Frachter. Ab 200 t geht die Ladung weiter.
        foreach (var f in w.Objs.Where(o => o.Kind is OK.Factory or OK.Port && o.Health > 0))
        {
            if (f.Kind == OK.Factory)
            {
                double made = f.Output * Prod(f) * Sup(w, f.Side);
                Fl(f.Side, "+ Produktion", made); Fl(f.Side, "- Werkslager voll", Math.Max(0, f.Stock + made - 600));
                f.Stock = Math.Min(600, f.Stock + made);
            }
            if (f.Node < 0 || f.Stock + f.Fuel < 200 || w.Convoys.Any(c => c.From == f.Id)) continue;   // Hinterland ohne Straßenanschluss: Lager bleibt
            if (!Dispatch(w, f.Side, f, f.Node, f.Stock, f.Fuel, 600) && w.Time.Hour % 6 == 0) w.Log(f.Side, "logistik", $"{f.Name}: no onward shipment (route cut), stock {f.Stock + f.Fuel:0} t.", f.X, f.Z);
        }
        foreach (var ab in w.Objs.Where(o => o.Kind == OK.Airbase && o.Node < 0))   // Plätze im Hinterland außerhalb der Karte: Bahn aus dem Landesinneren, bis 60 %
        {
            if (ab.BaseId < 0) { ab.Stock = ab.Fuel = ab.Cap; continue; }   // Träger: Versorgungsschiffe
            double m = Math.Clamp(ab.Cap * 0.6 - ab.Stock, 0, 5), p = Math.Clamp(ab.Cap * 0.6 - ab.Fuel, 0, 15);
            Fl(ab.Side, "+ Hinterland-Plätze", m + p); ab.Stock += m; ab.Fuel += p;
        }
        // Lkw-Kolonnen: Armeedepot → Frontdepot / Flugplatz. Depots im Schwerpunkt einer Operation zuerst und voller (85 %), je Armeedepot höchstens 3 Kolonnen gleichzeitig.
        var prio = w.Axes.Where(a => a.Op is "Bereitstellung" or "Angriff").SelectMany(a => w.Units.Where(u => u.Side == a.OpSide && u.Axis == a.Id && u.Dp >= 0).Select(u => u.Dp)).ToHashSet();
        foreach (var t in w.Objs.Where(o => (o.Kind == OK.Depot && !o.Name.StartsWith("Army") || o.Kind == OK.Airbase && o.Node >= 0) && o.Health > 0.2)
                     .OrderByDescending(o => prio.Contains(o.Id)).ThenBy(o => Math.Min(o.Stock, o.Fuel) / Math.Max(1, o.Cap)).ToList())
        {
            double low = prio.Contains(t.Id) ? 0.85 : 0.6;
            if (t.Stock >= t.Cap * low && t.Fuel >= t.Cap * low || w.Convoys.Count(c => c.To == t.Id) >= (prio.Contains(t.Id) ? 2 : 1)) continue;
            Obj? src = null; List<int>? path = null;
            foreach (var s in w.Objs.Where(o => o.Kind == OK.Depot && o.Name.StartsWith("Army") && o.Side == t.Side && o.Health > 0.2 && o.Stock + o.Fuel > 400 && o.Id != t.Id
                                                && w.Convoys.Count(c => c.From == o.Id) < 3).OrderBy(o => Geo.Dist(o.X, o.Z, t.X, t.Z)))
                if ((path = Path(w, s.Node, t.Node, t.Side, true)) != null) { src = s; break; }
            if (src == null || path == null) { if (w.Time.Hour % 6 == 0) w.Log(t.Side, "logistik", $"{t.Name}: no supply route (bridge destroyed, cut off or army depots empty).", t.X, t.Z); continue; }
            double dm = Math.Max(0, t.Cap * 0.9 - t.Stock), dpol = Math.Max(0, t.Cap * 0.9 - t.Fuel), k = 160 / Math.Max(1, dm + dpol);
            double m = Math.Min(Math.Min(dm, dm * k), Math.Max(0, src.Stock - 150)), p = Math.Min(Math.Min(dpol, dpol * k), Math.Max(0, src.Fuel - 150));
            if (m + p < 20) continue;
            src.Stock -= m; src.Fuel -= p;
            int n = (int)Math.Ceiling((m + p) / 10);
            w.Convoys.Add(new Convoy { Id = w.NewId(), Side = t.Side, From = src.Id, Origin = src.Node, To = t.Id, Path = path, Tons = m, Fuel = p, Trucks = n, Trucks0 = n, X = src.X, Z = src.Z });
        }
        // Verbände: Grundverbrauch, dann Nachschub über die Versorgungslinie (Leistung sinkt mit der Weglänge, bei Regen und beschädigtem Depot)
        foreach (var u in w.Units.Where(u => !u.InDcs))
        {
            u.Supply = Math.Max(0, u.Supply - 0.004); u.Fuel = Math.Max(0, u.Fuel - 0.003);
            var (dp, path) = Source(w, u);
            u.Dp = dp?.Id ?? -1;
            if (dp == null)
            {
                u.Line = 0;
                if (u.Supply < 0.5 || u.Fuel < 0.4) u.Why = $"cut off – no depot reachable (ammo {u.Supply:P0}, fuel {u.Fuel:P0})";
                continue;
            }
            double len = PathLen(w, u.Node, path);
            u.Line = Math.Clamp(1 - len / LineKm, 0, 1) * (w.Wx.Rain ? 0.8 : 1) * Math.Min(1, dp.Health / 0.6);
            double r = LineRate * u.Line;
            double am = Math.Max(0, Math.Min(Math.Min(r, 1 - u.Supply), dp.Stock / UnitMun)), ap = Math.Max(0, Math.Min(Math.Min(r, 1 - u.Fuel), dp.Fuel / UnitPol));
            u.Supply += am; u.Fuel += ap; dp.Stock -= am * UnitMun; dp.Fuel -= ap * UnitPol;
            Fl(u.Side, "- Truppenversorgung", am * UnitMun + ap * UnitPol);
            FlowOn(w, u.Node, path, am * UnitMun + ap * UnitPol);
            if (u.Supply < 0.4 || u.Fuel < 0.3)
                u.Why = $"ammo {u.Supply:P0}, fuel {u.Fuel:P0}: " + (dp.Stock < 20 || dp.Fuel < 20 ? $"{dp.Name} empty" : $"{len / 1000:0} km supply route from {dp.Name}, line {u.Line:P0}");
        }
    }

    static void Deliver(World w, Convoy c)
    {
        var t = w.Objs.FirstOrDefault(o => o.Id == c.To);
        w.Convoys.Remove(c);
        if (c.Reinf >= 0) { Spawn(w, c.Reinf, w.Nodes[t?.Node ?? (c.Path.Count > 0 ? c.Path[^1] : StartNode(w, c))], c.Trucks / (double)Math.Max(1, c.Trucks0)); return; }
        if (t == null || t.Side != c.Side) { Fl(c.Side, "- Transport verloren", c.Tons + c.Fuel); return; }
        Fl(c.Side, "- Lager voll bei Ankunft", Math.Max(0, t.Stock + c.Tons - t.Cap) + Math.Max(0, t.Fuel + c.Fuel - t.Cap));
        t.Stock = Math.Min(t.Cap, t.Stock + c.Tons); t.Fuel = Math.Min(t.Cap, t.Fuel + c.Fuel);
    }

    // ---------- Lagebericht ----------

    /// Morgendlicher Lagebericht (06:00) für die Spielerseite: Front, Verluste, Logistik, vermutete Absicht des Gegners – mit Unsicherheit.
    public static void Report(World w)
    {
        var me = w.Player; var foe = me.Enemy();
        var ev = w.Events.Where(e => e.T > w.Time.AddHours(-24)).ToList();
        var r = new List<string> { $"SITREP {w.Time:dd.MM.yyyy HH:mm} – {me.De()}", "", "FRONT (24 h)" };
        var cap = ev.Where(e => e.Kind == "front" && e.Text.Contains(" captured by ")).ToList();
        r.Add(cap.Count == 0 ? "No ground changed hands." : $"{cap.Count(e => e.Side == me)} towns taken, {cap.Count(e => e.Side == foe)} lost.");
        r.AddRange(cap.TakeLast(4).Select(e => $"  {e.T:HH:mm} {e.Text}"));
        foreach (var ax in w.Axes)
            r.Add(Front(w, ax) is var (rf, bf) ? $"  {ax.Name}: {w.Nodes[me == Side.Red ? rf : bf].Name} vs. {w.Nodes[me == Side.Red ? bf : rf].Name}" : $"  {ax.Name}: broken through");
        int Lost(Side s) => ev.Count(e => e.Kind == "verlust" && e.Side == s && e.Text.Contains("loses an aircraft"));
        int Ground(Side s) => ev.Count(e => e.Text.Contains($"({s.De()}) wiped out") || e.Text.Contains($"({s.De()}) encircled without"));
        r.AddRange(["", "LOSSES (24 h)", $"Aircraft: own {Lost(me)}, enemy {Lost(foe)} (reported)", $"Battalions wiped out/surrendered: own {Ground(me)}, enemy {Ground(foe)}"]);
        int missing = w.Downed.Count(d => d.Side == me);
        if (missing > 0) r.Add($"{missing} pilot(s) missing – CSAR under way.");
        r.AddRange(["", "LOGISTICS"]);
        var low = w.Objs.Where(o => o.Side == me && o.Kind == OK.Depot && o.Cap > 0 && Math.Min(o.Stock, o.Fuel) < o.Cap * 0.25).Select(o => $"{o.Name} ({o.Stock:0} t ammo, {o.Fuel:0} t fuel)").ToList();
        r.Add(low.Count == 0 ? "Depots adequately stocked." : "Low: " + string.Join(", ", low));
        int cut = w.Units.Count(u => u.Side == me && u.Line == 0);
        if (cut > 0) r.Add($"{cut} battalion(s) without a supply line.");
        r.AddRange(w.S.Gates.Where(g => g.S == me && Scenario.Find(w, g.At) is { } n && n.Owner != me).Select(g => $"Supply feed via {g.At} cut."));
        r.AddRange(ev.Where(e => e.Kind == "logistik" && e.Text.Contains("destroyed")).TakeLast(3).Select(e => $"  {e.T:HH:mm} {e.Text}"));
        r.AddRange(["", "ENEMY (intel)"]);
        var know = w.Intel(me);
        var ops = w.Axes.Where(a => a.Op != "" && a.OpSide == foe && (a.Warned || a.Op != "Bereitstellung")).ToList();
        foreach (var a in ops) r.Add(a.Op == "Bereitstellung" ? $"Staging detected on axis {a.Name} – attack likely in 12–24 h." : $"Enemy conducting operations on axis {a.Name} ({En.Of(a.Op)}).");
        if (ops.Count == 0)
        {
            var seen = w.Units.Where(u => u.Side == foe && know.ContainsKey($"U{u.Id}")).Select(u => (u, k: know[$"U{u.Id}"])).ToList();
            var best = w.Axes.Select(a => (a, l: seen.Where(s => Front(w, a) is var (rf, bf) && Geo.Dist(s.k.X, s.k.Z, w.Nodes[foe == Side.Red ? rf : bf].X, w.Nodes[foe == Side.Red ? rf : bf].Z) < 40000).ToList()))
                .OrderByDescending(t => t.l.Count).FirstOrDefault();
            if (best.l is not { Count: > 0 }) r.Add("Enemy situation unclear – recon needed.");
            else
            {
                double age = best.l.Average(s => (w.Time - s.k.T).TotalHours);
                string sure = age < 3 ? "confirmed" : age < 12 ? "probable" : "assumed, reports outdated";
                r.Add($"Enemy main effort probably on axis {best.a.Name}: {best.l.Count} battalion(s) detected ({sure}, avg. {age:0} h old).");
            }
        }
        w.Report = r;
        w.Log(me, "lage", $"Sitrep {w.Time:dd.MM. HH:mm} available.");
        Paper(w, ev);
    }

    public static void News(World w, string head, string sub)
    {
        w.News.Add($"{w.Time:yyyy-MM-ddTHH:mm}|{head}|{sub}");
        if (w.News.Count > 60) w.News.RemoveRange(0, w.News.Count - 60);
    }

    /// Zeitung zwischen den Einsätzen: die drei größten Ereignisse der letzten 24 h als Schlagzeilen
    static void Paper(World w, List<Ev> ev)
    {
        var me = w.Player; var foe = me.Enemy();
        var c = new List<(double S, string H, string Sub)>();
        if (w.Over != "") c.Add((100, "The war is over", w.Over));
        foreach (var e in ev.Where(e => e.Kind == "front" && e.Text.Contains(" captured by ")))
        {
            var town = e.Text[..e.Text.IndexOf(" captured by ")];
            c.Add((w.Nodes.Any(n => n.Name == town && n.Key == n.Id) ? 9 : 5, e.Side == me ? $"{me.De()} forces take {town}" : $"{town} falls to {foe.De()}", e.Text));
        }
        foreach (var a in w.Axes.Where(a => a.Op == "Angriff" && a.OpSince > w.Time.AddHours(-24)))
            c.Add((8, $"{a.OpSide.De()} offensive on the {a.Name} axis", a.OpSide == me ? "Our troops are on the move – the air force supports the attack." : "Enemy armour advances – close air support requested."));
        foreach (var e in ev.Where(e => e.Kind == "front" && (e.Text.Contains(" wiped out") || e.Text.Contains(" surrenders at "))))
            c.Add((6, e.Text.Contains(" surrenders at ") ? "Encircled battalion surrenders" : $"{e.Text[..Math.Max(0, e.Text.IndexOf(" ("))]} wiped out", e.Text));
        int Lost(Side s) => ev.Count(e => e.Kind == "verlust" && e.Side == s && e.Text.Contains("loses an aircraft"));
        int lm = Lost(me), lf = Lost(foe);
        if (lm + lf >= 3) c.Add((lm + lf, lf > lm ? $"{foe.De()} loses {lf} aircraft in heavy air fighting" : lm > lf ? $"Black day for {me.De()} airmen: {lm} aircraft lost" : $"Air war in the balance: {lm} aircraft lost on each side",
            $"Reported losses in the last 24 hours: {me.De()} {lm}, {foe.De()} {lf}."));
        foreach (var e in ev.Where(e => e.Text.StartsWith("CSAR")).TakeLast(2)) c.Add((5, "Downed airman rescued", e.Text));
        foreach (var e in ev.Where(e => e.Kind == "luft" && e.Text.Contains(" is now an ace")).TakeLast(2))
            c.Add((e.Side == me ? 6 : 4, e.Side == me ? $"New ace: {e.Text[..e.Text.IndexOf(" (")]}" : $"Enemy ace {e.Text[..e.Text.IndexOf(" (")]} feared by our pilots", e.Text));
        foreach (var e in ev.Where(e => e.Kind == "spieler" && System.Text.RegularExpressions.Regex.IsMatch(e.Text, @": .*(Medal|Cross|Heart|Star)\.$")).TakeLast(2))
            c.Add((4, $"{e.Text[..e.Text.IndexOf(": ")]} decorated", e.Text));
        foreach (var e in ev.Where(e => e.Kind == "see" && (e.Text.Contains(" sunk") || e.Text.Contains(" blockades "))).TakeLast(2))
            c.Add((7, e.Text.Contains(" blockades ") ? $"Naval blockade off {e.Text[(e.Text.IndexOf(" blockades ") + 11)..].TrimEnd('.')}" : e.Text.Contains(" bound for ") ? "Freighter sunk at sea" : $"Warships sunk – {e.Text[..e.Text.IndexOf(':')]}", e.Text));
        foreach (var e in ev.Where(e => e.Kind == "logistik" && e.Text.Contains("destroyed")).TakeLast(2)) c.Add((3, "Supply lines under attack", e.Text));
        if (w.Downed.Count(d => d.Side == me) is var mia and > 0) c.Add((3, $"{mia} airm{(mia == 1 ? "a" : "e")}n missing behind enemy lines", "Rescue helicopters stand by – the search goes on."));
        if (w.Wx.Snow) c.Add((2, "Snow closes the mountain passes", "Convoys detour, the front holds still."));
        if (c.Count == 0) c.Add((0, "Quiet day along the front", "No ground changed hands in the last 24 hours."));
        foreach (var (_, h, sub) in c.OrderByDescending(x => x.S).DistinctBy(x => x.H).Take(3).Reverse()) News(w, h, sub);
    }

    // ---------- Aufklärung (Fog of War) ----------

    static void Intel(World w)
    {
        foreach (var s in new[] { Side.Red, Side.Blue })
        {
            var know = w.Intel(s);
            var eyes = w.Units.Where(u => u.Side == s).Select(u => (u.X, u.Z, R: 12000.0, Src: "Ground contact")).ToList();
            eyes.AddRange(w.Flights.Where(f => f.Side == s && Air.Up(f) && f.Alt < 9000 && f.Task is "CAS" or "BAI" or "Recon" or "Interdiction" or "Strike")
                .Select(f => (f.X, f.Z, R: f.Task == "Recon" ? 25000.0 : 12000.0, Src: $"Flight {f.Callsign}")));
            foreach (var u in w.Units.Where(u => u.Side != s))
                foreach (var e in eyes)
                    if (Geo.Dist(u.X, u.Z, e.X, e.Z) < e.R)
                    {
                        double err = e.Src == "Ground contact" ? ((u.Id * 7919 + w.Time.Hour * 104729) % 1000 / 1000.0 - 0.5) * 0.4 : 0;   // Bodenkontakt schätzt ±20 %, Flugzeuge zählen
                        know[$"U{u.Id}"] = new Sight { X = u.X, Z = u.Z, T = w.Time, Src = e.Src, Str = Math.Clamp(u.Str * (1 + err), 0.05, 1) };
                        break;
                    }
            foreach (var o in w.Objs.Where(o => o.Decoy && !o.Exposed && o.Side != s && o.Health > 0.3))   // Aufklärer im Tiefflug entlarven Attrappen
                if (w.Flights.Any(f => f.Side == s && f.Task == "Recon" && Air.Up(f) && Geo.Dist(f.X, f.Z, o.X, o.Z) < 25000))
                {
                    o.Exposed = true; know.Remove($"O{o.Id}");
                    w.Log(s, "luft", $"Recon: {o.Name} is a decoy – removed from the target list.", o.X, o.Z);
                }
            foreach (var c in w.Convoys.Where(c => c.Side != s))
                if (c.Ship) { if (Radar(w, s, c.X, c.Z, 30) is { } src) know[$"C{c.Id}"] = new Sight { X = c.X, Z = c.Z, T = w.Time, Src = src, Str = c.Trucks }; }   // Seeziele: Küstenradar/AWACS
                else foreach (var e in eyes)
                    if (Geo.Dist(c.X, c.Z, e.X, e.Z) < e.R * 1.5) { know[$"C{c.Id}"] = new Sight { X = c.X, Z = c.Z, T = w.Time, Src = e.Src, Str = c.Trucks }; break; }
            foreach (var o in w.Objs.Where(o => o.Side != s && o.Kind is not (OK.Sam or OK.Tel)))   // feste Anlagen sind aus Karten bekannt
                if (!know.ContainsKey($"O{o.Id}")) know[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = "Pre-war intel", Str = 1 };
            foreach (var o in w.Objs.Where(o => o.Side != s && o.Kind == OK.Tel && o.Health > 0))   // Raketenwerfer: nur wer nah genug ist, sieht sie
                foreach (var e in eyes)
                    if (Geo.Dist(o.X, o.Z, e.X, e.Z) < e.R * 0.5) { know[$"O{o.Id}"] = new Sight { X = o.X, Z = o.Z, T = w.Time, Src = e.Src, Str = o.Health }; break; }
            foreach (var f in w.Flights.Where(f => f.Side != s && Air.Up(f)))
                if (Radar(w, s, f.X, f.Z, f.Alt) is { } src) know[$"F{f.Id}"] = new Sight { X = f.X, Z = f.Z, T = w.Time, Src = src, Str = f.Alive };
        }
    }

    /// Sieht eine Seite einen Punkt in der Luft? (Frühwarnradar mit Radarhorizont, AWACS, eigene Jäger)
    public static string? Radar(World w, Side s, double x, double z, double alt)
    {
        foreach (var o in w.Objs.Where(o => o.Side == s && o.Kind == OK.Ewr && o.Health > 0.3))
        {
            double d = Geo.Dist(o.X, o.Z, x, z), horizon = 4120 * (Math.Sqrt(30) + Math.Sqrt(Math.Max(0, alt - w.Th.Height(x, z))));
            if (d < Math.Min(o.Range, horizon)) return o.Name;
        }
        foreach (var f in w.Flights.Where(f => f.Side == s && f.Task == "AWACS" && f.Status == "im Einsatz"))
            if (Geo.Dist(f.X, f.Z, x, z) < 320000) return $"AWACS {f.Callsign}";
        foreach (var f in w.Flights.Where(f => f.Side == s && f.Missiles > 0 && Air.Up(f)))
            if (Geo.Dist(f.X, f.Z, x, z) < 80000) return $"Radar {f.Callsign}";
        foreach (var c in w.Convoys.Where(c => c.Navy && c.Side == s))       // Schiffsradar, Mast 25 m
            if (Geo.Dist(c.X, c.Z, x, z) < Math.Min(150000, 4120 * (5 + Math.Sqrt(Math.Max(0, alt))))) return c.Name;
        return null;
    }
}
