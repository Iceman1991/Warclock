namespace DcsCampaign;

/// DCS-Koordinaten (x = Nord, z = Ost, Meter) <-> Lat/Lon. Transversal-Mercator je Karte (Mittelmeridian, Versatz), Use() schaltet um.
// ponytail: eine Projektion für den ganzen Prozess – es läuft immer nur eine Karte; parallel mehrere Karten bräuchten die Projektion am World.
static class Geo
{
    public const double NM = 1852, Ft = 0.3048, Kt = 0.514444;
    const double A = 6378137, F = 1 / 298.257223563, K0 = 0.9996;
    static double Lon0 = 33, FE = -99516.9999999732, FN = -4998114.999999984;
    public static void Use(MapDef m) => (Lon0, FE, FN) = (m.Lon0, m.FE, m.FN);
    static readonly double E2 = F * (2 - F), Ep2 = E2 / (1 - E2);

    static double M(double phi) =>
        A * ((1 - E2 / 4 - 3 * E2 * E2 / 64 - 5 * E2 * E2 * E2 / 256) * phi
           - (3 * E2 / 8 + 3 * E2 * E2 / 32 + 45 * E2 * E2 * E2 / 1024) * Math.Sin(2 * phi)
           + (15 * E2 * E2 / 256 + 45 * E2 * E2 * E2 / 1024) * Math.Sin(4 * phi)
           - 35 * E2 * E2 * E2 / 3072 * Math.Sin(6 * phi));

    public static (double X, double Z) ToXZ(double lat, double lon) => Tm(lat, lon, Lon0, FE, FN);
    /// ohne Versatz, beliebiger Mittelmeridian (Projektion einer Karte bestimmen)
    public static (double X, double Z) Raw(double lat, double lon, double lon0) => Tm(lat, lon, lon0, 0, 0);

    static (double North, double East) Tm(double lat, double lon, double lon0, double fe, double fn)
    {
        double phi = lat * Math.PI / 180, lam = (lon - lon0) * Math.PI / 180;
        double s = Math.Sin(phi), c = Math.Cos(phi), t = Math.Tan(phi);
        double n = A / Math.Sqrt(1 - E2 * s * s), T = t * t, C = Ep2 * c * c, a = lam * c;
        double east = fe + K0 * n * (a + (1 - T + C) * Math.Pow(a, 3) / 6 + (5 - 18 * T + T * T + 72 * C - 58 * Ep2) * Math.Pow(a, 5) / 120);
        double north = fn + K0 * (M(phi) + n * t * (a * a / 2 + (5 - T + 9 * C + 4 * C * C) * Math.Pow(a, 4) / 24
                       + (61 - 58 * T + T * T + 600 * C - 330 * Ep2) * Math.Pow(a, 6) / 720));
        return (north, east);
    }

    public static (double Lat, double Lon) ToLL(double x, double z)
    {
        double m = (x - FN) / K0, mu = m / (A * (1 - E2 / 4 - 3 * E2 * E2 / 64 - 5 * E2 * E2 * E2 / 256));
        double e1 = (1 - Math.Sqrt(1 - E2)) / (1 + Math.Sqrt(1 - E2));
        double p1 = mu + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu) + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
                  + 151 * Math.Pow(e1, 3) / 96 * Math.Sin(6 * mu) + 1097 * Math.Pow(e1, 4) / 512 * Math.Sin(8 * mu);
        double s = Math.Sin(p1), c = Math.Cos(p1), t = Math.Tan(p1);
        double C1 = Ep2 * c * c, T1 = t * t, n1 = A / Math.Sqrt(1 - E2 * s * s), r1 = A * (1 - E2) / Math.Pow(1 - E2 * s * s, 1.5);
        double d = (z - FE) / (n1 * K0);
        double lat = p1 - n1 * t / r1 * (d * d / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * Ep2) * Math.Pow(d, 4) / 24
                     + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * Ep2 - 3 * C1 * C1) * Math.Pow(d, 6) / 720);
        double lon = (d - (1 + 2 * T1 + C1) * Math.Pow(d, 3) / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * Ep2 + 24 * T1 * T1) * Math.Pow(d, 5) / 120) / c;
        return (lat * 180 / Math.PI, Lon0 + lon * 180 / Math.PI);
    }

    public static double Dist(double x1, double z1, double x2, double z2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));
    public static double Dist(P a, P b) => Dist(a.X, a.Z, b.X, b.Z);
    /// Kurs in Grad (rechtweisend im DCS-Gitter) von a nach b.
    public static double Brg(P a, P b) => (Math.Atan2(b.Z - a.Z, b.X - a.X) * 180 / Math.PI + 360) % 360;
    public static P Move(P a, double brgDeg, double m) => new(a.X + Math.Cos(brgDeg * Math.PI / 180) * m, a.Z + Math.Sin(brgDeg * Math.PI / 180) * m);
    public static P Lerp(P a, P b, double f) => new(a.X + (b.X - a.X) * f, a.Z + (b.Z - a.Z) * f);

    /// Abstand Punkt -> Strecke.
    public static double SegDist(P p, P a, P b)
    {
        double dx = b.X - a.X, dz = b.Z - a.Z, l2 = dx * dx + dz * dz;
        double f = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
        return Dist(p.X, p.Z, a.X + f * dx, a.Z + f * dz);
    }

    /// MGRS (UTM-Zone, 100-km-Quadrat, 5+5 Ziffern) wie im Kniebrett/JTAC – Norwegen-Sonderzonen nicht nötig.
    public static string Mgrs(double x, double z) { var (lat, lon) = ToLL(x, z); return MgrsLL(lat, lon); }
    public static string MgrsLL(double lat, double lon)
    {
        int zone = (int)Math.Floor((lon + 180) / 6) + 1;
        var (n, e) = Tm(lat, lon, zone * 6 - 183, 500000, lat < 0 ? 10000000 : 0);
        char band = "CDEFGHJKLMNPQRSTUVWX"[Math.Clamp((int)Math.Floor((lat + 80) / 8), 0, 19)];
        char col = (((zone - 1) % 3) switch { 0 => "ABCDEFGH", 1 => "JKLMNPQR", _ => "STUVWXYZ" })[Math.Clamp((int)(e / 100000) - 1, 0, 7)];
        char row = "ABCDEFGHJKLMNPQRSTUV"[((int)Math.Floor(n / 100000) + (zone % 2 == 0 ? 5 : 0)) % 20];
        return $"{zone}{band} {col}{row} {(int)(e % 100000):00000} {(int)(n % 100000):00000}";
    }

    public static string LLText(double x, double z)
    {
        var (lat, lon) = ToLL(x, z);
        static string F(double v, char p, char n, int w) { var a = Math.Abs(v); int d = (int)a; double m = (a - d) * 60; return $"{(v >= 0 ? p : n)} {d.ToString().PadLeft(w, '0')}°{m:00.000}'"; }
        return $"{F(lat, 'N', 'S', 2)} {F(lon, 'E', 'W', 3)}";
    }
}

public readonly record struct P(double X, double Z);
