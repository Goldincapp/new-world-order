using System.Net.Http.Headers;
using System.Net.Http.Json;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// In-game feedback: players send a bug, an idea or a balance note from inside the game, with where they were and,
/// if they choose, a screenshot. Everything is kept on the server (through world wipes) for the developers to work through.
/// If NWO_GITHUB_TOKEN and NWO_GITHUB_REPO ("owner/repo") are set, each one also opens a GitHub issue labelled
/// "feedback", so coding agents watching the repo can pick it up.
/// </summary>
public static class FeedbackDesk
{
    public static readonly string[] Kinds = ["bug", "idea", "balance", "other"];
    public const int MaxText = 2000, MaxShot = 700_000, PerDay = 30;
    public static readonly TimeSpan Gap = TimeSpan.FromSeconds(20);

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    static string? Repo => Environment.GetEnvironmentVariable("NWO_GITHUB_REPO");
    static string? Token => Environment.GetEnvironmentVariable("NWO_GITHUB_TOKEN");
    public static bool LinkedToGitHub => !string.IsNullOrEmpty(Repo) && !string.IsNullOrEmpty(Token);

    static readonly Dictionary<string, string> KindName = new() { ["bug"] = "Bug", ["idea"] = "Idea", ["balance"] = "Balance", ["other"] = "Feedback" };

    /// <summary>Open a GitHub issue for this feedback. Returns its web address, or null when GitHub isn't linked or the call fails.</summary>
    public static async Task<string?> OpenIssue(Feedback f)
    {
        if (!LinkedToGitHub) return null;
        try
        {
            var firstLine = f.Text.Split('\n')[0].Trim();
            var title = $"[{KindName.GetValueOrDefault(f.Kind, "Feedback")}] {(firstLine.Length > 70 ? firstLine[..70] + "…" : firstLine)}";
            var body = $"""
                {f.Text}

                ---
                **From:** {f.Name} · {f.At:yyyy-MM-dd HH:mm} UTC · feedback #{f.Id}
                **Where:** {f.Context ?? "(no context)"}
                **Screenshot:** {(string.IsNullOrEmpty(f.Shot) ? "none" : $"saved on the server: `GET /api/admin/feedback/{f.Id}/shot` with the admin key")}

                _Sent from the in-game Feedback button._
                """;
            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{Repo}/issues")
            {
                Content = JsonContent.Create(new { title, body, labels = new[] { "feedback", f.Kind } }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            req.Headers.UserAgent.ParseAdd("NWO-Server");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var res = await Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) { Console.WriteLine($"GitHub issue failed: {(int)res.StatusCode}"); return null; }
            var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return json.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        }
        catch (Exception e) { Console.WriteLine($"GitHub issue failed: {e.Message}"); return null; }
    }
}
