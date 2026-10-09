using System.Globalization;
using System.Text;

namespace DcsCampaign;

/// Lua-Tabellen lesen (DCS-Datendateien) und schreiben (.miz). Tabellen = Dictionary mit String-Schlüsseln ("1","2",… für Listen).
static class Lua
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// Alle Zuweisungen "name = <wert>" bzw. "local name = <wert>" auf oberster Ebene.
    public static Dictionary<string, object?> Parse(string s)
    {
        var r = new Dictionary<string, object?>();
        var p = new P(s);
        while (true)
        {
            p.Ws();
            if (p.I >= s.Length) break;
            if (char.IsLetter(s[p.I]) || s[p.I] == '_')
            {
                var id = p.Ident();
                if (id == "local") { p.Ws(); id = p.Ident(); }
                var ids = new List<string> { id };
                p.Ws();
                while (p.I < s.Length && s[p.I] == ',') { p.I++; p.Ws(); ids.Add(p.Ident()); p.Ws(); }
                if (p.I < s.Length && s[p.I] == '=' && (p.I + 1 >= s.Length || s[p.I + 1] != '='))
                {
                    p.I++;
                    try
                    {
                        for (int k = 0; k < ids.Count; k++)
                        {
                            if (k > 0) { p.Ws(); if (p.I < s.Length && s[p.I] == ',') p.I++; else break; }
                            r[ids[k]] = p.Vars[ids[k]] = p.Value();
                        }
                    }
                    catch (FormatException) { p.SkipLine(); }
                }
                else p.SkipLine();
            }
            else p.SkipLine();
        }
        return r;
    }

    public static IEnumerable<T> Arr<T>(object? t) where T : class
    {
        if (t is not Dictionary<string, object?> d) yield break;
        for (int i = 1; d.TryGetValue(i.ToString(Inv), out var v); i++) if (v is T x) yield return x;
    }
    public static IEnumerable<Dictionary<string, object?>> Tabs(object? t) =>
        t is Dictionary<string, object?> d ? d.Values.OfType<Dictionary<string, object?>>() : [];
    public static double Num(object? t, string k, double def = 0) => t is Dictionary<string, object?> d && d.TryGetValue(k, out var v) && v is double n ? n : def;
    public static string Str(object? t, string k) => t is Dictionary<string, object?> d && d.TryGetValue(k, out var v) && v is string s ? s : "";
    public static object? Get(object? t, string k) => t is Dictionary<string, object?> d && d.TryGetValue(k, out var v) ? v : null;

    sealed class P(string s)
    {
        public int I;
        public readonly Dictionary<string, object?> Vars = [];
        public void Ws()
        {
            while (I < s.Length)
            {
                if (char.IsWhiteSpace(s[I])) I++;
                else if (s[I] == '-' && I + 1 < s.Length && s[I + 1] == '-')
                {
                    if (I + 3 < s.Length && s[I + 2] == '[' && s[I + 3] == '[') { var e = s.IndexOf("]]", I, StringComparison.Ordinal); I = e < 0 ? s.Length : e + 2; }
                    else SkipLine();
                }
                else break;
            }
        }
        public void SkipLine() { var e = s.IndexOf('\n', I); I = e < 0 ? s.Length : e + 1; }
        public string Ident() { int a = I; while (I < s.Length && (char.IsLetterOrDigit(s[I]) || s[I] == '_' || s[I] == '.')) I++; return s[a..I]; }

        public object? Value()
        {
            Ws();
            if (I >= s.Length) throw new FormatException();
            char c = s[I];
            if (c == '{') return Table();
            if (c == '"' || c == '\'') return Str();
            if (c == '[' && I + 1 < s.Length && s[I + 1] == '[') { var e = s.IndexOf("]]", I, StringComparison.Ordinal); var v = s[(I + 2)..e]; I = e + 2; return v; }
            if (c == '-' || c == '.' || char.IsDigit(c)) return Number();
            if (char.IsLetter(c) || c == '_')
            {
                var id = Ident();
                if (id == "true") return true;
                if (id == "false") return false;
                if (id == "nil") return null;
                Ws();
                if (I < s.Length && s[I] == '(')   // _("Text") u. ä. -> erstes Argument
                {
                    I++;
                    object? a = null;
                    Ws();
                    if (s[I] != ')') a = Value();
                    int depth = 1;
                    while (I < s.Length && depth > 0) { if (s[I] == '(') depth++; else if (s[I] == ')') depth--; I++; }
                    return a;
                }
                return Vars.GetValueOrDefault(id);
            }
            throw new FormatException($"Lua: unexpected '{c}' at {I}");
        }

        double Number()
        {
            int a = I;
            if (s[I] == '-') I++;
            while (I < s.Length && (char.IsLetterOrDigit(s[I]) || s[I] == '.' || ((s[I] == '-' || s[I] == '+') && (s[I - 1] == 'e' || s[I - 1] == 'E')))) I++;
            var t = s[a..I];
            if (t.StartsWith("0x") || t.StartsWith("-0x")) return (t[0] == '-' ? -1 : 1) * Convert.ToInt64(t.TrimStart('-')[2..], 16);
            return double.Parse(t, NumberStyles.Float, Inv);
        }

        string Str()
        {
            char q = s[I++];
            var b = new StringBuilder();
            while (I < s.Length && s[I] != q)
            {
                char c = s[I++];
                if (c != '\\') { b.Append(c); continue; }
                char e = s[I++];
                switch (e)
                {
                    case 'n': b.Append('\n'); break;
                    case 't': b.Append('\t'); break;
                    case 'r': b.Append('\r'); break;
                    case '\n': b.Append('\n'); break;
                    default:
                        if (char.IsDigit(e)) { int v = e - '0'; for (int k = 0; k < 2 && I < s.Length && char.IsDigit(s[I]); k++) v = v * 10 + (s[I++] - '0'); b.Append((char)v); }
                        else b.Append(e);
                        break;
                }
            }
            I++;
            return b.ToString();
        }

        Dictionary<string, object?> Table()
        {
            var t = new Dictionary<string, object?>();
            I++;
            int n = 1;
            while (true)
            {
                Ws();
                if (I >= s.Length) throw new FormatException("Lua: unclosed table");
                if (s[I] == '}') { I++; return t; }
                if (s[I] == ',' || s[I] == ';') { I++; continue; }
                string? key = null;
                if (s[I] == '[' && s[I + 1] != '[')
                {
                    I++;
                    var k = Value();
                    Ws(); I++;               // ]
                    Ws(); I++;               // =
                    key = k is double d ? d.ToString(Inv) : k?.ToString() ?? "";
                }
                else if (char.IsLetter(s[I]) || s[I] == '_')
                {
                    int save = I;
                    var id = Ident();
                    Ws();
                    if (I < s.Length && s[I] == '=' && s[I + 1] != '=') { I++; key = id; }
                    else I = save;
                }
                var v = Value();
                t[key ?? (n++).ToString(Inv)] = v;
            }
        }
    }

    // ---------- Schreiben ----------

    public static string Write(string name, object? v)
    {
        var b = new StringBuilder();
        b.Append(name).Append(" = ");
        Val(b, v, 0);
        b.Append('\n');
        return b.ToString();
    }

    public static string Quote(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var c in s)
            b.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "", _ => c.ToString() });
        return b.Append('"').ToString();
    }

    static void Val(StringBuilder b, object? v, int ind)
    {
        switch (v)
        {
            case null: b.Append("nil"); break;
            case bool x: b.Append(x ? "true" : "false"); break;
            case string x: b.Append(Quote(x)); break;
            case int or long: b.Append(Convert.ToString(v, Inv)); break;
            case double x: b.Append(double.IsFinite(x) ? x.ToString("R", Inv) : "0"); break;
            case float x: b.Append(((double)x).ToString("R", Inv)); break;
            case System.Collections.IDictionary d:
                b.Append("{\n");
                foreach (System.Collections.DictionaryEntry e in d)
                {
                    b.Append('\t', ind + 1).Append('[');
                    b.Append(e.Key is string ks ? Quote(ks) : Convert.ToString(e.Key, Inv));
                    b.Append("] = ");
                    Val(b, e.Value, ind + 1);
                    b.Append(",\n");
                }
                b.Append('\t', ind).Append('}');
                break;
            case System.Collections.IEnumerable list:
                b.Append("{\n");
                int i = 1;
                foreach (var e in list)
                {
                    b.Append('\t', ind + 1).Append('[').Append(i++).Append("] = ");
                    Val(b, e, ind + 1);
                    b.Append(",\n");
                }
                b.Append('\t', ind).Append('}');
                break;
            default: b.Append(Convert.ToString(v, Inv)); break;
        }
    }
}
