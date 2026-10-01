namespace NWO.Server.Game;

/// <summary>Admin tools are only open to requests carrying the NWO_ADMIN_KEY secret.</summary>
public static class Admin
{
    /// <summary>Test-server tools any player can use (practice sieges). Turn off before real players arrive.</summary>
    public static bool DevTools => Environment.GetEnvironmentVariable("NWO_DEV_TOOLS") == "1";

    public static bool Allowed(HttpContext ctx)
    {
        var key = Environment.GetEnvironmentVariable("NWO_ADMIN_KEY");
        return !string.IsNullOrEmpty(key) && ctx.Request.Headers["X-Admin-Key"] == key;
    }
}
