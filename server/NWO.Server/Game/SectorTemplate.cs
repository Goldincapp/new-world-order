namespace NWO.Server.Game;

/// <summary>
/// The terrain of a settlement sector, parcel by parcel. It matches the sector the client draws,
/// so what players see and what the server enforces are the same land.
/// Codes: hi = highland (2 or 3 is its height), gr = grassland, wa = water, sa = desert oil field,
/// pa = road, to = town, fi = farmland.
/// </summary>
public static class SectorTemplate
{
    const string Grid = """
        hi3 hi3 hi2 hi2 gr1 gr1 wa0 gr1 gr1 gr1 sa1 sa1 sa1 sa1
        hi3 hi3 hi2 hi2 gr1 gr1 wa0 wa0 gr1 gr1 sa1 sa1 sa1 sa1
        hi3 hi3 hi2 hi2 gr1 gr1 gr1 wa0 wa0 gr1 sa1 pa1 sa1 sa1
        hi2 hi2 hi2 hi2 gr1 gr1 gr1 gr1 wa0 gr1 sa1 pa1 sa1 sa1
        hi2 hi2 hi2 hi2 gr1 gr1 gr1 gr1 wa0 gr1 sa1 pa1 sa1 sa1
        gr1 gr1 gr1 gr1 gr1 gr1 gr1 wa0 wa0 gr1 sa1 pa1 sa1 sa1
        gr1 gr1 gr1 gr1 gr1 gr1 wa0 wa0 gr1 gr1 sa1 pa1 sa1 sa1
        gr1 gr1 gr1 gr1 gr1 wa0 wa0 gr1 gr1 gr1 gr1 pa1 gr1 gr1
        gr1 pa1 pa1 pa1 wa0 wa0 pa1 pa1 pa1 pa1 pa1 pa1 gr1 gr1
        to1 to1 to1 to1 wa0 gr1 gr1 gr1 gr1 gr1 gr1 gr1 gr1 gr1
        to1 to1 to1 to1 wa0 gr1 gr1 gr1 fi1 fi1 fi1 gr1 gr1 gr1
        to1 to1 to1 to1 wa0 wa0 gr1 gr1 fi1 fi1 fi1 gr1 gr1 gr1
        to1 to1 to1 to1 gr1 wa0 gr1 gr1 fi1 fi1 fi1 gr1 gr1 gr1
        to1 to1 to1 to1 gr1 wa0 wa0 gr1 gr1 gr1 gr1 gr1 gr1 gr1
        """;

    public const int Size = 14;
    /// <summary>The sector hall: every settler's entry point to their home base, never owned.</summary>
    public static readonly (int i, int j) Hall = (6, 12);

    static readonly string[,] Cells = Parse();

    static string[,] Parse()
    {
        var rows = Grid.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var c = new string[Size, Size];
        for (var j = 0; j < Size; j++)
        {
            var cols = rows[j].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < Size; i++) c[i, j] = cols[i];
        }
        return c;
    }

    public static string Code(int i, int j) => Cells[i, j];

    /// <summary>What lies under a parcel. Uses the same hash as the client so both agree.</summary>
    public static string Resource(int i, int j) => Cells[i, j] switch
    {
        "sa1" => "oil",
        "fi1" => "grain",
        "hi3" => "stone",
        "hi2" => "ore",
        "gr1" => Hash(i * 1.7, j * 0.9) < 0.5 ? "timber" : "grain",
        "to1" => "housing",
        _ => "none",
    };

    /// <summary>Roads, water, the sector hall and the town (owned by its residents) can't be claimed.</summary>
    public static bool Claimable(int i, int j) => Cells[i, j] is not ("wa0" or "pa1" or "to1") && (i, j) != Hall;

    public static double Hash(double a, double b)
    {
        var s = Math.Sin(a * 127.1 + b * 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }
}
