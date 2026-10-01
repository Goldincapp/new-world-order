using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Guest accounts for the test server: the phone keeps a random secret token, the server keeps only its hash.
/// Google or email sign-in can be linked to the same player later.
/// </summary>
public static partial class Auth
{
    [GeneratedRegex("^[A-Za-z0-9_.-]{3,16}$")]
    private static partial Regex NameRule();

    public static bool ValidName(string? name) => name is not null && NameRule().IsMatch(name);

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>A recovery code people can write down: 12 characters in three groups, no look-alike letters.</summary>
    public static string NewRecoveryCode()
    {
        var b = RandomNumberGenerator.GetBytes(12);
        var c = new string(b.Select(x => CodeChars[x % CodeChars.Length]).ToArray());
        return $"{c[..4]}-{c[4..8]}-{c[8..]}";
    }

    public static string? NormalizeCode(string? code)
    {
        if (code is null) return null;
        var c = new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return c.Length == 12 ? c : null;
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string? TokenFrom(HttpContext ctx)
    {
        var h = ctx.Request.Headers.Authorization.ToString();
        if (h.StartsWith("Bearer ", StringComparison.Ordinal)) return h[7..];
        var q = ctx.Request.Query["access_token"].ToString();
        return string.IsNullOrEmpty(q) ? null : q;
    }

    public static async Task<Player?> PlayerFrom(HttpContext ctx, GameDb db, bool withParcels = false)
    {
        var token = TokenFrom(ctx);
        if (token is null) return null;
        var hash = Hash(token);
        var q = db.Players.AsQueryable();
        if (withParcels) q = q.Include(p => p.Parcels).Include(p => p.HomeTiles);
        return await q.FirstOrDefaultAsync(p => p.TokenHash == hash);
    }
}
