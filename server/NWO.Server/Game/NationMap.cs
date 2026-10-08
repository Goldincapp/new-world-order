namespace NWO.Server.Game;

/// <summary>
/// Region 1 as one continuous map in the shape of Aurelia (the outline of Germany on the world map).
/// The 50 sectors are 34x34-parcel squares laid over that outline: the squares whose centres fall inside it,
/// numbered in reading order from the north-west. Every parcel has a global position (X, Y) on this map, so land
/// can run seamlessly from one sector into the next. Parcels outside the border can't be claimed; strips of the
/// country that no sector square covers are border wilds.
/// </summary>
public static class NationMap
{
    public const int S = SectorTemplate.Size;
    /// <summary>Parcels per degree of longitude/latitude: at this scale exactly 50 squares fit the outline.</summary>
    public const double ParcelsPerDegree = 45, CosLat = 0.6293; // cos(51°)
    const double Scale = ParcelsPerDegree;
    static readonly double MinX, MinY;
    /// <summary>Old-world longitude, latitude to map parcels.</summary>
    public static (double x, double y) ToMap(double lon, double lat) => ((lon * CosLat - MinX) * Scale, (-lat - MinY) * Scale);
    public static (double lon, double lat) ToLonLat(double x, double y) => ((x / Scale + MinX) / CosLat, -(y / Scale + MinY));
    /// <summary>Margin of sea and neighbouring land drawn around the nation, in parcels.</summary>
    public const int Margin = 20;

    /// <summary>Aurelia's border as longitude, latitude pairs.</summary>
    static readonly double[] Border =
    [
        9.52, 47.52, 9.35, 47.60, 9.18, 47.67, 8.88, 47.66, 8.73, 47.70, 8.57, 47.78, 8.44, 47.73, 8.57, 47.65,
        8.43, 47.59, 8.20, 47.61, 7.93, 47.56, 7.70, 47.57, 7.53, 47.67, 7.59, 47.91, 7.58, 48.06, 7.71, 48.28,
        7.76, 48.41, 7.84, 48.64, 8.12, 48.87, 8.00, 49.01, 7.80, 49.04, 7.61, 49.06, 7.45, 49.15, 7.20, 49.11,
        7.04, 49.11, 6.89, 49.21, 6.73, 49.16, 6.61, 49.29, 6.46, 49.44, 6.38, 49.60, 6.49, 49.71, 6.32, 49.84,
        6.20, 49.92, 6.11, 50.03, 6.18, 50.23, 6.36, 50.32, 6.29, 50.49, 6.15, 50.64, 6.01, 50.73, 6.05, 50.91,
        5.90, 50.98, 6.13, 51.15, 6.17, 51.36, 6.14, 51.55, 5.95, 51.76, 6.09, 51.85, 6.30, 51.85, 6.52, 51.85,
        6.74, 51.91, 6.71, 52.06, 6.86, 52.14, 6.98, 52.21, 7.04, 52.38, 6.83, 52.44, 6.70, 52.50, 6.75, 52.63,
        7.01, 52.63, 7.12, 52.89, 7.19, 53.19, 7.15, 53.33, 7.08, 53.48, 7.21, 53.66, 7.63, 53.70, 8.01, 53.69,
        8.17, 53.54, 8.33, 53.61, 8.49, 53.51, 8.51, 53.67, 8.58, 53.84, 8.90, 53.84, 9.21, 53.86, 9.59, 53.60,
        9.78, 53.55, 9.63, 53.60, 9.31, 53.86, 9.07, 53.90, 8.92, 53.97, 8.91, 54.26, 8.74, 54.30, 8.83, 54.43,
        8.96, 54.54, 8.79, 54.70, 8.68, 54.79, 8.86, 54.90, 9.19, 54.84, 9.34, 54.81, 9.50, 54.84, 9.66, 54.83,
        9.89, 54.78, 10.02, 54.67, 9.94, 54.51, 10.14, 54.49, 10.36, 54.44, 10.73, 54.32, 10.96, 54.38, 11.06, 54.28,
        10.81, 54.08, 11.10, 54.01, 11.40, 53.95, 11.70, 54.11, 12.11, 54.17, 12.30, 54.28, 12.58, 54.47, 12.78, 54.45,
        13.03, 54.41, 13.15, 54.28, 13.45, 54.14, 13.73, 54.15, 13.82, 54.02, 13.87, 53.85, 14.02, 53.77, 14.25, 53.73,
        14.30, 53.56, 14.42, 53.28, 14.37, 53.10, 14.19, 52.98, 14.25, 52.78, 14.51, 52.64, 14.62, 52.53, 14.55, 52.36,
        14.68, 52.25, 14.70, 52.11, 14.69, 51.96, 14.60, 51.83, 14.68, 51.70, 14.71, 51.54, 14.91, 51.46, 15.02, 51.25,
        14.96, 51.09, 14.82, 50.87, 14.66, 50.83, 14.56, 50.95, 14.37, 51.03, 14.20, 50.86, 14.00, 50.80, 13.70, 50.72,
        13.56, 50.70, 13.44, 50.60, 13.27, 50.58, 13.02, 50.49, 12.87, 50.42, 12.71, 50.41, 12.55, 50.39, 12.36, 50.27,
        12.17, 50.29, 12.18, 50.15, 12.28, 50.04, 12.46, 49.96, 12.45, 49.80, 12.50, 49.64, 12.63, 49.46, 12.75, 49.37,
        12.92, 49.33, 13.14, 49.16, 13.29, 49.10, 13.40, 48.98, 13.55, 48.96, 13.69, 48.88, 13.82, 48.77, 13.80, 48.62,
        13.67, 48.52, 13.49, 48.58, 13.41, 48.39, 13.21, 48.30, 12.90, 48.20, 12.76, 48.11, 12.85, 47.99, 12.95, 47.89,
        12.91, 47.75, 13.06, 47.66, 13.03, 47.51, 12.88, 47.51, 12.77, 47.64, 12.59, 47.66, 12.44, 47.67, 12.27, 47.70,
        11.72, 47.58, 11.57, 47.55, 11.39, 47.49, 11.21, 47.41, 11.04, 47.39, 10.90, 47.47, 10.74, 47.52, 10.48, 47.54,
        10.40, 47.42, 10.24, 47.28, 10.10, 47.38, 9.97, 47.50, 9.75, 47.58, 9.55, 47.53,    ];

    /// <summary>The border in map parcels (X east, Y south).</summary>
    public static readonly (double x, double y)[] Poly;
    public static readonly int Width, Height;
    static readonly Dictionary<int, (int gx, int gy)> Origins = new();
    static readonly Dictionary<(int c, int r), int> ByCell = new();

    static NationMap()
    {
        var raw = Enumerable.Range(0, Border.Length / 2).Select(k => (x: Border[2 * k] * CosLat, y: -Border[2 * k + 1])).ToArray();
        MinX = raw.Min(p => p.x); MinY = raw.Min(p => p.y);
        Poly = raw.Select(p => ((p.x - MinX) * Scale, (p.y - MinY) * Scale)).ToArray();
        Width = (int)Math.Ceiling(Poly.Max(p => p.x)); Height = (int)Math.Ceiling(Poly.Max(p => p.y));
        var squares = new List<(int c, int r, double cover)>();
        for (var r = 0; r * S < Height; r++)
            for (var c = 0; c * S < Width; c++)
            {
                if (!InPoly(c * S + S / 2.0, r * S + S / 2.0)) continue;
                var n = 0;
                for (var a = 0; a < S; a += 2) for (var b = 0; b < S; b += 2) if (InPoly(c * S + a + 1, r * S + b + 1)) n++;
                squares.Add((c, r, n));
            }
        var picked = squares.OrderByDescending(q => q.cover).Take(50).OrderBy(q => q.r).ThenBy(q => q.c).ToList();
        // The capital, sector 8, is the square holding old Berlin: swap numbers with whatever was 8
        var (bx, by) = ((13.40 * CosLat - MinX) * Scale, (-52.52 - MinY) * Scale);
        var berlin = picked.FindIndex(q => bx >= q.c * S && bx < (q.c + 1) * S && by >= q.r * S && by < (q.r + 1) * S);
        if (berlin >= 0 && berlin != 7) (picked[7], picked[berlin]) = (picked[berlin], picked[7]);
        for (var k = 0; k < picked.Count; k++)
        {
            Origins[k + 1] = (picked[k].c * S, picked[k].r * S);
            ByCell[(picked[k].c, picked[k].r)] = k + 1;
        }
    }

    static bool InPoly(double x, double y)
    {
        var inside = false;
        for (int i = 0, j = Poly.Length - 1; i < Poly.Length; j = i++)
        {
            var (xi, yi) = Poly[i]; var (xj, yj) = Poly[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    public static bool Has(int sector) => Origins.ContainsKey(sector);
    public static (int gx, int gy) Origin(int sector) => Origins[sector];
    /// <summary>Is the parcel at map position (X, Y) inside Aurelia's border?</summary>
    public static bool Inside(int x, int y) => InPoly(x + 0.5, y + 0.5);
    /// <summary>The sector covering map position (X, Y), or 0 for border wilds and beyond.</summary>
    public static int SectorAt(int x, int y) => x < 0 || y < 0 ? 0 : ByCell.GetValueOrDefault((x / S, y / S));
    public static IEnumerable<int> Sectors => Origins.Keys.OrderBy(n => n);

    /// <summary>Beyond the border: the North and Baltic Seas to the north, foreign land everywhere else.</summary>
    public static bool Sea(int x, int y)
    {
        var (lon, lat) = ToLonLat(x + 0.5, y + 0.5);
        if (lon > 8.3 && lon < 11.2 && lat > 54.75) return false; // Denmark
        if (lon > 14.2) return lat > 54.2;                         // the Polish coast
        if (lon < 7.2) return lat > 53.45;                         // the Dutch coast
        return lat > 53.35;
    }

    /// <summary>What's at any map position: a sector's land, border wilds, sea (xs) or foreign land (xx).</summary>
    public static string CodeAt(int x, int y)
    {
        var n = SectorAt(x, y);
        if (n != 0) return SectorTemplate.Code(n, x - Origins[n].gx, y - Origins[n].gy);
        if (Inside(x, y)) return SectorTemplate.LandAt(x, y);
        return Sea(x, y) ? "xs" : "xx";
    }

    static string? json;
    /// <summary>Everything the client needs to draw the whole nation as one map: the layout, the border, the old cities, and an atlas
    /// of every parcel's land code and height (0-9) for the whole nation plus a margin of sea and foreign land.</summary>
    public static string Json() => json ??= Build();
    static string Build()
    {
        int aw = Width + 2 * Margin, ah = Height + 2 * Margin;
        var codes = new string[ah]; var elev = new string[ah];
        var sb = new System.Text.StringBuilder(); var eb = new System.Text.StringBuilder();
        for (var ay = 0; ay < ah; ay++)
        {
            sb.Clear(); eb.Clear();
            for (var ax = 0; ax < aw; ax++)
            {
                int x = ax - Margin, y = ay - Margin;
                var c = CodeAt(x, y);
                sb.Append(c);
                eb.Append(c is "wa" or "xs" ? '0' : (char)('0' + Geography.Elevation(x + 0.5, y + 0.5)));
            }
            codes[ay] = sb.ToString(); elev[ay] = eb.ToString();
        }
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            size = S, width = Width, height = Height, margin = Margin, aw, ah,
            // the projection, so the client can line this map up with the world and nation maps
            ppd = ParcelsPerDegree, cos = CosLat, minX = MinX, minY = MinY,
            sectors = Sectors.Select(n => new { n, gx = Origins[n].gx, gy = Origins[n].gy, closed = Economy.Closed.Contains(n), biome = SectorTemplate.Biome(n) }),
            poly = Poly.Select(p => new[] { Math.Round(p.x, 1), Math.Round(p.y, 1) }),
            towns = Geography.TownSpots(),
            codes, elev,
        });
    }
}
