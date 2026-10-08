using System.Text.Json;

namespace NWO.Server.Game;

/// <summary>
/// The whole region (Europe and its edges) as one continuous map, in the same parcels and projection as Aurelia's
/// (see NationMap), so the game can show any nation the way it shows Aurelia: its real outline, rivers, mountains,
/// lakes and the ruins of its old cities. Country shapes come from the client's map file (client/nwo-geo.js), the same
/// ones the Nation view draws. Only Aurelia's sectors can be settled; everywhere else is to look at, for now.
/// Detailed land is served a few 34x34 squares at a time; a coarse map of the whole region is served once.
/// </summary>
public static class WorldAtlas
{
    public const double LonMin = -25, LonMax = 62, LatMin = 24, LatMax = 72;
    record Ring(double[] Pts, double MinLon, double MaxLon, double MinLat, double MaxLat);
    record Country(string Id, Ring[] Rings);
    static Country[] countries = [];
    public static string? GeoPath { get; set; }

    static readonly object gate = new();
    static volatile bool loaded;
    static void Load()
    {
        lock (gate)
        {
            if (loaded) return;
            if (GeoPath is null || !File.Exists(GeoPath)) { Console.WriteLine($"World atlas: no map file at {GeoPath}"); loaded = true; return; }
            var text = File.ReadAllText(GeoPath);
            // the file is JavaScript (const GEO=[...];const WGEO=...): take the first array, bracket by bracket
            int start = text.IndexOf('['), depth = 0, end = start;
            for (var k = start; k < text.Length; k++) { if (text[k] == '[') depth++; else if (text[k] == ']' && --depth == 0) { end = k; break; } }
            var json = text[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            countries = doc.RootElement.EnumerateArray().Select(c => new Country(c.GetProperty("id").GetString() ?? "",
                c.GetProperty("r").EnumerateArray().Select(r =>
                {
                    var p = r.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                    double a = 999, b = -999, cmin = 999, d = -999;
                    for (var k = 0; k < p.Length; k += 2) { a = Math.Min(a, p[k]); b = Math.Max(b, p[k]); cmin = Math.Min(cmin, p[k + 1]); d = Math.Max(d, p[k + 1]); }
                    return new Ring(p, a, b, cmin, d);
                }).ToArray())).ToArray();
            Console.WriteLine($"World atlas: {countries.Length} countries");
            loaded = true; // only now: other threads must not read an empty atlas while this one loads
        }
    }

    /// <summary>The country (map id) at a longitude, latitude, or null at sea.</summary>
    public static string? CountryAt(double lon, double lat)
    {
        if (!loaded) Load();
        foreach (var c in countries)
            foreach (var r in c.Rings)
            {
                if (lon < r.MinLon || lon > r.MaxLon || lat < r.MinLat || lat > r.MaxLat) continue;
                var inside = false; var p = r.Pts; var n = p.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = p[2 * i], yi = p[2 * i + 1], xj = p[2 * j], yj = p[2 * j + 1];
                    if ((yi > lat) != (yj > lat) && lon < (xj - xi) * (lat - yi) / (yj - yi) + xi) inside = !inside;
                }
                if (inside) return c.Id;
            }
        return null;
    }

    /// <summary>The land at any map position outside Aurelia's sectors: sea (xs), or land from its real geography.</summary>
    public static string LandCode(int x, int y)
    {
        var (lon, lat) = NationMap.ToLonLat(x + 0.5, y + 0.5);
        if (lon < LonMin || lon > LonMax || lat < LatMin || lat > LatMax) return "xs";
        return CountryAt(lon, lat) is null ? "xs" : SectorTemplate.LandAt(x, y);
    }

    /// <summary>What's at any map position anywhere in the region.</summary>
    public static string CodeAt(int x, int y)
    {
        var n = NationMap.SectorAt(x, y);
        if (n != 0) { var (gx, gy) = NationMap.Origin(n); return SectorTemplate.Code(n, x - gx, y - gy); }
        return LandCode(x, y);
    }

    public static char ElevAt(int x, int y, string code) => code is "wa" or "xs" ? '0' : (char)('0' + Geography.Elevation(x + 0.5, y + 0.5));

    /// <summary>The region's bounds in map parcels.</summary>
    public static (int x0, int y0, int x1, int y1) Bounds()
    {
        var (ax, ay) = NationMap.ToMap(LonMin, LatMax); var (bx, by) = NationMap.ToMap(LonMax, LatMin);
        return ((int)Math.Floor(ax), (int)Math.Floor(ay), (int)Math.Ceiling(bx), (int)Math.Ceiling(by));
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, int), (string[] codes, string[] elev)> Squares = new();
    /// <summary>One 34x34 square's land codes and heights, cached.</summary>
    public static (string[] codes, string[] elev) Square(int c, int r) => Squares.GetOrAdd((c, r), key =>
    {
        int S = NationMap.S, gx = key.Item1 * S, gy = key.Item2 * S;
        var codes = new string[S]; var elev = new string[S];
        var sb = new System.Text.StringBuilder(); var eb = new System.Text.StringBuilder();
        for (var j = 0; j < S; j++)
        {
            sb.Clear(); eb.Clear();
            for (var i = 0; i < S; i++) { var code = CodeAt(gx + i, gy + j); sb.Append(code); eb.Append(ElevAt(gx + i, gy + j, code)); }
            codes[j] = sb.ToString(); elev[j] = eb.ToString();
        }
        return (codes, elev);
    });

    static string? overview;
    /// <summary>A coarse map of the whole region: one sample every few parcels, for the view from far away.</summary>
    public static string Overview(int step) => overview ??= BuildOverview(step);
    static string BuildOverview(int step)
    {
        var (x0, y0, x1, y1) = Bounds();
        int w = (x1 - x0) / step + 1, h = (y1 - y0) / step + 1;
        var codes = new string[h]; var elev = new string[h];
        Parallel.For(0, h, j =>
        {
            var sb = new System.Text.StringBuilder(); var eb = new System.Text.StringBuilder();
            for (var i = 0; i < w; i++) { int x = x0 + i * step, y = y0 + j * step; var code = CodeAt(x, y); sb.Append(code); eb.Append(ElevAt(x, y, code)); }
            codes[j] = sb.ToString(); elev[j] = eb.ToString();
        });
        return JsonSerializer.Serialize(new { x0, y0, step, w, h, codes, elev });
    }
}
