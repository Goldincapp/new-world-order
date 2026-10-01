namespace NWO.Server.Game;

/// <summary>
/// The land of each sector: a 34 by 34 grid of parcels, generated from the sector's number and its land type,
/// so every sector is different but always the same for everyone. Mostly barren ground that is worth little
/// until someone builds on it, with pockets of farmland, scrub, rock, oil sands and ruins.
/// Codes: ba barren, sc scrub, gr grassland, fe fertile, ro rocky, oi oil sands, ru ruins, wa water, pa road, ha sector hall.
/// </summary>
public static class SectorTemplate
{
    public const int Size = 34;
    public static readonly (int i, int j) Hall = (Size / 2, Size / 2);

    /// <summary>The land type of each sector in Vellmoor province (10 columns by 5 rows), matching the province map.</summary>
    static readonly string[][] Biomes =
    [
        ["forest", "forest", "hills", "lake", "farm", "farm", "forest", "lake", "farm", "hills"],
        ["forest", "town", "yours", "farm", "oil", "oil", "town", "farm", "oil", "forest"],
        ["hills", "city", "farm", "town", "oil", "grass", "grass", "town", "farm", "lake"],
        ["hills", "farm", "grass", "forest", "town", "farm", "oil", "forest", "town", "farm"],
        ["lake", "forest", "farm", "forest", "hills", "grass", "hills", "farm", "forest", "grass"],
    ];

    public static string Biome(int sector)
    {
        if (Region2.Contains(sector)) return Region2.Biome(sector);
        int c = (sector - 1) / 5, r = (sector - 1) % 5;
        return Biomes[r][c];
    }

    static readonly Dictionary<int, string[,]> Cache = new();

    public static string[,] Grid(int sector)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(sector, out var g)) Cache[sector] = g = Generate(sector);
            return g;
        }
    }

    /// <summary>The grid as one row of codes per line, for sending to the client.</summary>
    public static string[] Rows(int sector)
    {
        var g = Grid(sector);
        return Enumerable.Range(0, Size).Select(j => string.Concat(Enumerable.Range(0, Size).Select(i => g[i, j]))).ToArray();
    }

    public static string Code(int sector, int i, int j) => Grid(sector)[i, j];

    public static string Resource(int sector, int i, int j) => Code(sector, i, j) switch
    {
        "oi" => "oil",
        "fe" or "gr" => "grain",
        "sc" => "timber",
        "ro" => Hash(i * 1.3 + sector, j * 0.7) < 0.5 ? "ore" : "stone",
        "ru" => "salvage",
        _ => "none",
    };

    public static bool Claimable(int sector, int i, int j) =>
        i >= 0 && j >= 0 && i < Size && j < Size && Code(sector, i, j) is not ("wa" or "pa" or "ha");

    // ---------- Generation ----------

    static string[,] Generate(int sector)
    {
        var b = Biome(sector);
        var g = new string[Size, Size];
        double seed = sector * 17.31;
        // How much of each kind of land this biome has, on top of the barren baseline.
        // Share of the sector each kind of land takes. Whatever is left stays barren: about half of every sector.
        var shares = b switch
        {
            "farm" => new (string code, double share)[] { ("wa", .03), ("fe", .18), ("gr", .12), ("sc", .04), ("ro", .03), ("oi", .02), ("ru", .03) },
            "forest" => [("wa", .04), ("sc", .22), ("gr", .08), ("fe", .04), ("ro", .05), ("oi", .01), ("ru", .03)],
            "hills" => [("wa", .02), ("ro", .22), ("sc", .08), ("gr", .05), ("fe", .02), ("oi", .03), ("ru", .03)],
            "lake" => [("wa", .16), ("gr", .10), ("fe", .07), ("sc", .08), ("ro", .03), ("oi", .01), ("ru", .03)],
            "oil" or "yours" => [("wa", .02), ("oi", .20), ("ro", .06), ("gr", .05), ("fe", .03), ("sc", .04), ("ru", .05)],
            "town" => [("wa", .03), ("ru", .14), ("gr", .10), ("fe", .06), ("sc", .05), ("ro", .03), ("oi", .02)],
            "city" => [("wa", .02), ("ru", .24), ("gr", .06), ("fe", .03), ("sc", .03), ("ro", .04), ("oi", .02)],
            // The Ashlands: richer than anything in Region 1, which is why people fight over it.
            "deepoil" => [("wa", .02), ("oi", .34), ("ro", .08), ("ru", .06), ("gr", .03), ("sc", .03)],
            "breadbasket" => [("wa", .05), ("fe", .32), ("gr", .14), ("sc", .03), ("ru", .03), ("oi", .02)],
            "ironhills" => [("wa", .02), ("ro", .36), ("oi", .06), ("sc", .05), ("ru", .04), ("gr", .03)],
            "deadcity" => [("wa", .02), ("ru", .40), ("ro", .06), ("oi", .04), ("gr", .04), ("fe", .02)],
            _ => [("wa", .03), ("gr", .16), ("fe", .07), ("sc", .08), ("ro", .04), ("oi", .02), ("ru", .04)],
        };
        for (var j = 0; j < Size; j++) for (var i = 0; i < Size; i++) g[i, j] = "ba";
        var layer = 0;
        foreach (var (code, share) in shares)
        {
            // Each kind of land follows its own noise field, so it forms natural patches.
            // Take the highest-scoring free cells until this kind has its share.
            layer++;
            var scale = code == "ru" ? 2.2 : code == "wa" ? 0.7 : 1.0;
            var cells = (from j in Enumerable.Range(0, Size) from i in Enumerable.Range(0, Size)
                         where g[i, j] == "ba"
                         select (i, j, score: Fbm(i / 6.0 * scale + seed + layer * 13.7, j / 6.0 * scale - seed * 0.5 + layer * 7.1)
                                              + (code == "ru" ? Hash(i + seed, j + layer) * 0.4 : 0)))
                        .OrderByDescending(c => c.score).Take((int)(share * Size * Size));
            foreach (var c in cells.ToList()) g[c.i, c.j] = code;
        }
        // Roads: a cross through the hall and a ring around it.
        var (hi, hj) = Hall;
        for (var k = 0; k < Size; k++) { g[hi, k] = "pa"; g[k, hj] = "pa"; }
        for (var k = 6; k < Size - 6; k++) { g[k, 6] = "pa"; g[k, Size - 7] = "pa"; g[6, k] = "pa"; g[Size - 7, k] = "pa"; }
        g[hi, hj] = "ha";
        return g;
    }

    public static double Hash(double a, double b)
    {
        var s = Math.Sin(a * 127.1 + b * 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }

    static double ValueNoise(double x, double y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        double xf = x - xi, yf = y - yi, u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        double a = Hash(xi, yi), b2 = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b2 - a) * u + (c - a) * v + (a - b2 - c + d) * u * v;
    }

    /// <summary>Smooth noise in roughly 0..1.</summary>
    static double Fbm(double x, double y)
    {
        double q = 0, amp = 0.5, fr = 1, norm = 0;
        for (var i = 0; i < 4; i++) { q += amp * ValueNoise(x * fr, y * fr); norm += amp; fr *= 2.03; amp *= 0.5; }
        return q / norm;
    }
}
