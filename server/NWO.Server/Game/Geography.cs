namespace NWO.Server.Game;

/// <summary>
/// The real geography under Aurelia (Germany on the old world map), so the land looks like the place it was and
/// players recognise where their land would be in the real world. Rivers, lakes and mountain ranges shape the land,
/// and the old cities are ruins where they really stood. Coordinates are
/// longitude, latitude; everything is turned into map parcels through NationMap. Approximate on purpose: a parcel
/// is a few kilometres across. The names are only for reading this file: players never see real place names.
/// </summary>
public static class Geography
{
    public record River(string Name, double Width, double[] Path);
    public record Lake(string Name, double Lon, double Lat, double Rx, double Ry);
    /// <summary>A mountain range: a ridge line (or a single point) with a width in degrees and a peak height 0-9.</summary>
    public record Range(string Name, double Width, double Peak, bool Forest, double[] Ridge);
    /// <summary>A named area that changes the land: forest, farm, heath, lakes, oil, coast.</summary>
    public record Area(string Name, string Kind, double Lon, double Lat, double Rx, double Ry);
    /// <summary>An old city, now ruins. Size 1 (town) to 5 (metropolis).</summary>
    public record City(string Name, double Lon, double Lat, int Size);

    public static readonly River[] Rivers =
    [
        new("Rhine", 1.3, [9.00, 47.66, 8.60, 47.60, 8.20, 47.60, 7.59, 47.56, 7.62, 47.90, 7.75, 48.58, 8.20, 48.97, 8.47, 49.49, 8.27, 50.00, 7.93, 50.08, 7.60, 50.36, 7.10, 50.73, 6.96, 50.94, 6.77, 51.23, 6.76, 51.43, 6.25, 51.83, 6.00, 51.87]),
        new("Elbe", 1.3, [14.15, 50.90, 13.74, 51.05, 13.47, 51.16, 13.00, 51.56, 12.65, 51.87, 12.25, 51.85, 11.63, 52.13, 11.97, 52.54, 11.75, 53.00, 11.00, 53.25, 10.57, 53.37, 9.99, 53.55, 9.50, 53.65, 8.70, 53.87]),
        new("Danube", 1.2, [8.50, 47.95, 9.20, 48.05, 9.99, 48.40, 10.80, 48.70, 11.42, 48.76, 12.10, 49.02, 12.57, 48.88, 13.46, 48.57, 13.80, 48.55]),
        new("Main", 0.9, [11.90, 50.10, 10.90, 49.90, 10.23, 50.05, 9.93, 49.79, 9.60, 49.80, 9.15, 49.97, 8.68, 50.11, 8.27, 50.00]),
        new("Weser", 1.0, [9.65, 51.42, 9.36, 52.10, 8.92, 52.29, 9.20, 52.60, 8.80, 53.08, 8.58, 53.55]),
        new("Moselle", 0.8, [6.38, 49.47, 6.64, 49.75, 7.00, 49.95, 7.30, 50.10, 7.60, 50.36]),
        new("Neckar", 0.7, [8.60, 48.40, 9.18, 48.78, 9.20, 49.10, 8.95, 49.40, 8.47, 49.49]),
        new("Ems", 0.7, [8.40, 51.90, 7.70, 52.30, 7.30, 52.70, 7.20, 53.20]),
        new("Oder", 1.0, [14.99, 51.15, 14.75, 51.60, 14.60, 52.00, 14.55, 52.35, 14.20, 52.80, 14.28, 53.06, 14.40, 53.30]),
        new("Saale", 0.7, [11.90, 50.30, 11.60, 50.90, 11.95, 51.48, 11.85, 51.90]),
        new("Spree", 0.6, [14.40, 51.60, 14.30, 52.00, 13.40, 52.52, 13.10, 52.40, 12.50, 52.60, 12.20, 52.90, 12.00, 52.90]),
        new("Isar", 0.6, [11.25, 47.50, 11.58, 48.14, 12.00, 48.50, 12.90, 48.80]),
        new("Inn", 0.8, [12.20, 47.60, 12.50, 48.00, 12.90, 48.25, 13.46, 48.57]),
    ];

    public static readonly Lake[] Lakes =
    [
        new("Lake Constance", 9.35, 47.62, 0.30, 0.10), new("Chiemsee", 12.45, 47.87, 0.06, 0.05), new("Lake Starnberg", 11.32, 47.92, 0.03, 0.08),
        new("Ammersee", 11.12, 48.00, 0.03, 0.07), new("Müritz", 12.68, 53.45, 0.10, 0.12), new("Lake Schwerin", 11.45, 53.65, 0.05, 0.12),
        new("Plauer See", 12.30, 53.48, 0.07, 0.04), new("Steinhuder Meer", 9.33, 52.47, 0.05, 0.03),
    ];

    public static readonly Range[] Ranges =
    [
        new("Bavarian Alps", 0.22, 9, false, [9.80, 47.45, 10.50, 47.40, 11.00, 47.42, 11.60, 47.50, 12.20, 47.60, 12.80, 47.60, 13.05, 47.55]),
        new("Black Forest", 0.30, 6, true, [8.10, 47.75, 8.20, 48.20, 8.35, 48.75]),
        new("Swabian Jura", 0.12, 5, false, [8.80, 48.15, 9.50, 48.40, 10.40, 48.75]),
        new("Bavarian Forest", 0.30, 6, true, [12.60, 49.25, 13.30, 48.95, 13.70, 48.75]),
        new("Harz", 0.13, 5, true, [10.30, 51.82, 10.95, 51.68]),
        new("Thuringian Forest", 0.12, 5, true, [10.20, 50.85, 11.40, 50.45]),
        new("Ore Mountains", 0.15, 6, true, [12.20, 50.35, 13.90, 50.75]),
        new("Rhön", 0.15, 4, false, [9.95, 50.45]),
        new("Vogelsberg", 0.13, 4, true, [9.25, 50.50]),
        new("Sauerland", 0.22, 4, true, [7.80, 51.20, 8.60, 51.20]),
        new("Eifel", 0.30, 4, true, [6.70, 50.30]),
        new("Hunsrück", 0.15, 4, true, [6.90, 49.75, 7.60, 50.00]),
        new("Taunus", 0.10, 4, true, [8.00, 50.20, 8.60, 50.30]),
        new("Palatinate Forest", 0.22, 4, true, [7.85, 49.30]),
        new("Odenwald", 0.18, 4, true, [8.90, 49.65]),
        new("Spessart", 0.18, 3, true, [9.40, 50.00]),
        new("Fichtelgebirge", 0.15, 5, true, [11.90, 50.05]),
        new("Teutoburg Forest", 0.06, 3, true, [7.90, 52.25, 8.50, 52.00, 9.00, 51.75]),
        new("Saxon Switzerland", 0.12, 4, true, [14.10, 50.90]),
    ];

    public static readonly Area[] Areas =
    [
        new("Lüneburg Heath", "heath", 10.00, 53.10, 0.45, 0.25), new("Spreewald", "forest", 13.95, 51.85, 0.20, 0.12),
        new("Mecklenburg Lakes", "lakes", 12.50, 53.40, 0.90, 0.25), new("Magdeburg Börde", "farm", 11.40, 52.05, 0.40, 0.25),
        new("Hildesheim Börde", "farm", 9.95, 52.15, 0.30, 0.15), new("Lower Bavaria", "farm", 12.60, 48.80, 0.40, 0.20),
        new("Kraichgau", "farm", 8.80, 49.20, 0.20, 0.15), new("Uckermark", "farm", 13.80, 53.20, 0.30, 0.25),
        new("Lower Rhine", "farm", 6.40, 51.60, 0.30, 0.25), new("Emsland", "oil", 7.25, 52.60, 0.30, 0.30),
        new("Dithmarschen", "oil", 9.00, 54.05, 0.25, 0.18), new("Weser-Ems", "oil", 8.30, 52.90, 0.30, 0.20),
        new("Upper Rhine Plain", "oil", 8.15, 49.15, 0.15, 0.30), new("Ruhr", "city", 7.20, 51.48, 0.35, 0.12),
        new("Allgäu", "farm", 10.30, 47.70, 0.35, 0.15), new("Franconian Switzerland", "forest", 11.30, 49.75, 0.20, 0.15),
        new("Holstein", "farm", 10.20, 54.10, 0.45, 0.30), new("Western Pomerania", "farm", 13.50, 53.90, 0.50, 0.25),
        new("Altmark", "farm", 11.50, 52.70, 0.35, 0.25), new("Vogtland", "forest", 12.20, 50.45, 0.20, 0.15),
        new("East Frisia", "farm", 7.50, 53.40, 0.40, 0.15), new("Westphalia", "farm", 7.80, 51.85, 0.50, 0.25),
        new("Swabia", "farm", 10.20, 48.30, 0.40, 0.25), new("Franconia", "farm", 10.60, 49.60, 0.50, 0.30),
        new("Lusatia", "forest", 14.40, 51.40, 0.35, 0.25), new("Brandenburg", "farm", 13.00, 52.30, 0.80, 0.40),
        new("Saxony-Anhalt", "farm", 11.80, 51.70, 0.40, 0.30), new("Hesse", "forest", 9.00, 50.80, 0.50, 0.35),
        new("Saarland", "farm", 6.95, 49.40, 0.20, 0.15), new("Rhineland", "farm", 7.20, 50.50, 0.40, 0.30),
    ];

    public static readonly City[] Cities =
    [
        new("Berlin", 13.40, 52.52, 5), new("Hamburg", 10.00, 53.55, 5), new("Munich", 11.58, 48.14, 5), new("Cologne", 6.96, 50.94, 4),
        new("Frankfurt", 8.68, 50.11, 4), new("Stuttgart", 9.18, 48.78, 4), new("Düsseldorf", 6.78, 51.23, 4), new("Dortmund", 7.47, 51.51, 4),
        new("Essen", 7.01, 51.46, 4), new("Leipzig", 12.37, 51.34, 4), new("Bremen", 8.80, 53.08, 4), new("Dresden", 13.74, 51.05, 4),
        new("Hanover", 9.73, 52.37, 4), new("Nuremberg", 11.08, 49.45, 4), new("Duisburg", 6.76, 51.43, 3), new("Bochum", 7.22, 51.48, 3),
        new("Wuppertal", 7.15, 51.26, 3), new("Bielefeld", 8.53, 52.02, 3), new("Bonn", 7.10, 50.73, 3), new("Münster", 7.63, 51.96, 3),
        new("Karlsruhe", 8.40, 49.01, 3), new("Mannheim", 8.47, 49.49, 3), new("Augsburg", 10.90, 48.37, 3), new("Wiesbaden", 8.24, 50.08, 3),
        new("Braunschweig", 10.52, 52.27, 3), new("Kiel", 10.13, 54.32, 3), new("Chemnitz", 12.92, 50.83, 3), new("Aachen", 6.08, 50.78, 3),
        new("Halle", 11.97, 51.48, 3), new("Magdeburg", 11.63, 52.13, 3), new("Freiburg", 7.85, 47.99, 3), new("Lübeck", 10.69, 53.87, 3),
        new("Mainz", 8.27, 50.00, 3), new("Erfurt", 11.03, 50.98, 3), new("Rostock", 12.10, 54.09, 3), new("Kassel", 9.48, 51.31, 3),
        new("Potsdam", 13.06, 52.39, 2), new("Saarbrücken", 6.99, 49.24, 3), new("Osnabrück", 8.05, 52.28, 2), new("Oldenburg", 8.21, 53.14, 2),
        new("Regensburg", 12.10, 49.02, 2), new("Würzburg", 9.93, 49.79, 2), new("Ulm", 9.99, 48.40, 2), new("Göttingen", 9.93, 51.54, 2),
        new("Heidelberg", 8.69, 49.40, 2), new("Trier", 6.64, 49.75, 2), new("Koblenz", 7.60, 50.36, 2), new("Ingolstadt", 11.42, 48.76, 2),
        new("Schwerin", 11.41, 53.63, 2), new("Passau", 13.46, 48.57, 1), new("Bamberg", 10.89, 49.89, 1), new("Flensburg", 9.44, 54.79, 1),
        new("Cottbus", 14.33, 51.76, 2), new("Jena", 11.59, 50.93, 1), new("Weimar", 11.33, 50.98, 1), new("Konstanz", 9.17, 47.66, 1),
        new("Garmisch", 11.10, 47.49, 1), new("Berchtesgaden", 13.00, 47.63, 1), new("Stralsund", 13.09, 54.31, 1), new("Greifswald", 13.38, 54.09, 1),
        new("Frankfurt on the Oder", 14.55, 52.35, 1), new("Görlitz", 14.99, 51.15, 1), new("Hof", 11.92, 50.31, 1), new("Bayreuth", 11.58, 49.94, 1),
        new("Fulda", 9.68, 50.55, 1), new("Siegen", 8.02, 50.88, 1), new("Paderborn", 8.75, 51.72, 2), new("Wolfsburg", 10.79, 52.42, 2),
        new("Emden", 7.21, 53.37, 1), new("Wilhelmshaven", 8.12, 53.53, 1), new("Cuxhaven", 8.70, 53.87, 1), new("Lüneburg", 10.41, 53.25, 1),
        new("Stendal", 11.86, 52.60, 1), new("Neubrandenburg", 13.26, 53.56, 1), new("Zwickau", 12.50, 50.72, 1), new("Plauen", 12.14, 50.50, 1),
        new("Landshut", 12.15, 48.54, 1), new("Rosenheim", 12.13, 47.86, 1), new("Kempten", 10.32, 47.73, 1), new("Pforzheim", 8.70, 48.89, 1),
        new("Reutlingen", 9.21, 48.49, 1), new("Kaiserslautern", 7.77, 49.44, 1), new("Darmstadt", 8.65, 49.87, 2), new("Gießen", 8.68, 50.58, 1),
        new("Marburg", 8.77, 50.81, 1), new("Dessau", 12.25, 51.83, 1), new("Wittenberg", 12.65, 51.87, 1), new("Neuruppin", 12.80, 52.92, 1),
        new("Eberswalde", 13.82, 52.83, 1), new("Husum", 9.05, 54.48, 1), new("Celle", 10.08, 52.62, 1), new("Hamelin", 9.36, 52.10, 1),
    ];

    // ---------- In map parcels (computed once) ----------
    static readonly (string name, double w, (double x, double y)[] pts)[] RiverPts =
        Rivers.Select(r => (r.Name, r.Width, Pts(r.Path))).ToArray();
    static readonly (string name, double w, double peak, bool forest, (double x, double y)[] pts)[] RangePts =
        Ranges.Select(r => (r.Name, r.Width * NationMap.ParcelsPerDegree, r.Peak, r.Forest, Pts(r.Ridge))).ToArray();
    static readonly (string name, double x, double y, double r, int size)[] CityPts =
        Cities.Select(c => { var (x, y) = NationMap.ToMap(c.Lon, c.Lat); return (c.Name, x, y, 1.5 + c.Size * c.Size * 0.4, c.Size); }).ToArray();

    static (double x, double y)[] Pts(double[] lonLat) =>
        Enumerable.Range(0, lonLat.Length / 2).Select(k => NationMap.ToMap(lonLat[2 * k], lonLat[2 * k + 1])).ToArray();

    static double DistToPath((double x, double y)[] p, double x, double y)
    {
        if (p.Length == 1) return Math.Sqrt((x - p[0].x) * (x - p[0].x) + (y - p[0].y) * (y - p[0].y));
        var best = double.MaxValue;
        for (var k = 1; k < p.Length; k++)
        {
            var (ax, ay) = p[k - 1]; var (bx, by) = p[k];
            double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            var t = len2 == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / len2, 0, 1);
            double ex = ax + t * dx - x, ey = ay + t * dy - y;
            best = Math.Min(best, ex * ex + ey * ey);
        }
        return Math.Sqrt(best);
    }

    /// <summary>How strongly an elliptical area (in degrees) covers this parcel: 1 at its centre, fading to 0 at its edge.</summary>
    static double Cover(double lon, double lat, double rx, double ry, double x, double y)
    {
        var (cx, cy) = NationMap.ToMap(lon, lat);
        double ax = rx * NationMap.ParcelsPerDegree * NationMap.CosLat, ay = ry * NationMap.ParcelsPerDegree;
        var d = Math.Sqrt((x - cx) * (x - cx) / (ax * ax) + (y - cy) * (y - cy) / (ay * ay));
        return Math.Clamp(1.2 - d, 0, 1);
    }

    /// <summary>Is this parcel part of a river or lake?</summary>
    public static bool Water(double x, double y)
    {
        foreach (var (_, w, pts) in RiverPts) if (DistToPath(pts, x, y) < w * 0.75) return true;
        foreach (var l in Lakes) if (Cover(l.Lon, l.Lat, l.Rx, l.Ry, x, y) > 0.2) return true;
        return false;
    }

    /// <summary>Height of the land, 0 (plains) to 9 (the high Alps).</summary>
    public static int Elevation(double x, double y)
    {
        double e = 0;
        foreach (var (_, w, peak, _, pts) in RangePts)
        {
            var d = DistToPath(pts, x, y) / w;
            e = Math.Max(e, peak * Math.Exp(-d * d * 1.6));
        }
        if (e > 0.5) e += (SectorTemplateNoise(x * 0.35, y * 0.35) - 0.5) * 2.2;
        return (int)Math.Clamp(Math.Round(e), 0, 9);
    }

    static double SectorTemplateNoise(double x, double y) => SectorTemplate.Noise(x, y);

    /// <summary>How much of each kind of land this spot has, from its real geography. Whatever is left stays barren.</summary>
    public static Dictionary<string, double> Mix(double x, double y, int elevation)
    {
        var lonlat = NationMap.ToLonLat(x, y);
        var m = new Dictionary<string, double> { ["wa"] = .02, ["ro"] = .03, ["oi"] = .01, ["fe"] = .07, ["gr"] = .12, ["sc"] = .08, ["ru"] = .02 };
        var e = elevation;
        // Hills are wooded; the high mountains are bare rock
        double forest = 0;
        foreach (var (_, w, peak, isForest, pts) in RangePts)
        {
            var d = DistToPath(pts, x, y) / w;
            if (isForest) forest = Math.Max(forest, Math.Exp(-d * d * 1.2));
        }
        m["sc"] += forest * 0.32;
        m["ro"] += e >= 7 ? 0.45 : e * 0.035;
        m["fe"] *= Math.Max(0.2, 1 - e * 0.12); m["gr"] *= Math.Max(0.3, 1 - e * 0.08);
        // The north German plain: open grass and farmland
        if (lonlat.lat > 52.3) { m["gr"] += .05; m["fe"] += .03; }
        foreach (var a in Areas)
        {
            var c = Cover(a.Lon, a.Lat, a.Rx, a.Ry, x, y);
            if (c <= 0) continue;
            switch (a.Kind)
            {
                case "farm": m["fe"] += .22 * c; m["gr"] += .08 * c; break;
                case "forest": m["sc"] += .28 * c; break;
                case "heath": m["sc"] += .15 * c; m["gr"] += .08 * c; m["fe"] *= 1 - .6 * c; break;
                case "lakes": m["wa"] += .10 * c; break;
                case "oil": m["oi"] += .30 * c; break;
                case "city": m["ru"] += .30 * c; break;
            }
        }
        // The old cities, now ruins
        foreach (var (_, cx, cy, r, size) in CityPts)
        {
            var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d < r * 1.6) m["ru"] += 0.85 * Math.Clamp(1.4 - d / r, 0, 1);
        }
        var sum = m.Values.Sum();
        if (sum > 0.9) foreach (var k in m.Keys.ToList()) m[k] *= 0.9 / sum;
        return m;
    }

    static HashSet<(int x, int y)>? roads;
    /// <summary>The old road network: each city joined to its nearest neighbours, and every sector hall joined to the nearest road.
    /// Roads cross rivers on bridges.</summary>
    public static HashSet<(int x, int y)> Roads => roads ??= BuildRoads();
    static HashSet<(int x, int y)> BuildRoads()
    {
        var set = new HashSet<(int x, int y)>();
        var edges = new HashSet<(int a, int b)>();
        for (var a = 0; a < CityPts.Length; a++)
        {
            var near = Enumerable.Range(0, CityPts.Length).Where(b => b != a)
                .OrderBy(b => Dist(CityPts[a], CityPts[b])).Take(CityPts[a].size >= 3 ? 3 : 2)
                .Where(b => Dist(CityPts[a], CityPts[b]) < 75);
            foreach (var b in near) edges.Add((Math.Min(a, b), Math.Max(a, b)));
        }
        foreach (var (a, b) in edges) Line(set, CityPts[a].x, CityPts[a].y, CityPts[b].x, CityPts[b].y);
        // Every sector hall gets a road to the network
        foreach (var n in NationMap.Sectors)
        {
            var (gx, gy) = NationMap.Origin(n);
            int hx = gx + SectorTemplate.Hall.i, hy = gy + SectorTemplate.Hall.j;
            (int x, int y) best = default; var bd = double.MaxValue;
            foreach (var c in set) { var d = (c.x - hx) * (c.x - hx) + (c.y - hy) * (c.y - hy); if (d < bd) { bd = d; best = c; } }
            if (bd < double.MaxValue) Line(set, hx + 0.5, hy + 0.5, best.x + 0.5, best.y + 0.5);
        }
        return set;
    }
    static double Dist((string, double x, double y, double, int) a, (string, double x, double y, double, int) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    /// <summary>A road one parcel wide along a straight line, with no diagonal gaps.</summary>
    static void Line(HashSet<(int x, int y)> set, double x0, double y0, double x1, double y1)
    {
        int cx = (int)Math.Floor(x0), cy = (int)Math.Floor(y0), tx = (int)Math.Floor(x1), ty = (int)Math.Floor(y1);
        set.Add((cx, cy));
        var steps = 0;
        while ((cx != tx || cy != ty) && steps++ < 2000)
        {
            // step along whichever axis keeps us closest to the true line
            double dx = x1 - x0, dy = y1 - y0, len = Math.Sqrt(dx * dx + dy * dy);
            int sx = Math.Sign(tx - cx), sy = Math.Sign(ty - cy);
            double Err(int px, int py) => Math.Abs((px + 0.5 - x0) * dy - (py + 0.5 - y0) * dx) / Math.Max(1e-9, len);
            if (sx != 0 && (sy == 0 || Err(cx + sx, cy) <= Err(cx, cy + sy))) cx += sx; else cy += sy;
            set.Add((cx, cy));
        }
    }

    /// <summary>Where the old towns stood on the map (no names), so the client can draw their ruins: position and size 1-5.</summary>
    public static IEnumerable<object> TownSpots() => CityPts.Select(c => new { x = Math.Round(c.x, 1), y = Math.Round(c.y, 1), size = c.size });
}
