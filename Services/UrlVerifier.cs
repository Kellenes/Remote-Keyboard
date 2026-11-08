using System.Security.Cryptography;
using System.Text.Json;

namespace KeyboardController.Services;

public static class UrlVerifier
{
    public const string AppId = "RemoteKeyboardController";

    // Random value that exists only in the memory of the current process.
    // A foreign site or another PC running this app cannot know it.
    public static string NewInstanceId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    // Tries several times because a user's tunnel may still be starting
    // (for example cloudflared launched together with Windows).
    public static async Task<bool> IsOurServerAsync(
        string baseUrl, string instanceId, int attempts, TimeSpan attemptTimeout, TimeSpan pause, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = attemptTimeout };

        // Some tunnels show an HTML warning page to anything that looks like a browser.
        // These headers ask them to skip it. Unknown headers are ignored by other servers.
        http.DefaultRequestHeaders.Add("bypass-tunnel-reminder", "1");
        http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "1");

        for (int i = 1; i <= attempts; i++)
        {
            if (await CheckOnceAsync(http, baseUrl, instanceId, attemptTimeout, ct)) return true;

            // No pause after the last attempt, there is nothing to wait for
            if (i < attempts)
            {
                try { await Task.Delay(pause, ct); }
                catch (OperationCanceledException) { return false; } // the app is closing
            }
        }

        return false;
    }

    private static async Task<bool> CheckOnceAsync(
        HttpClient http, string baseUrl, string instanceId, TimeSpan timeout, CancellationToken ct)
    {
        // One deadline for the whole attempt, including reading the body:
        // HttpClient.Timeout does not cover a server that sends the body very slowly
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        attemptCts.CancelAfter(timeout);
        var token = attemptCts.Token;

        try
        {
            // ResponseHeadersRead: do not download the whole body automatically,
            // the address can point to anything, including a huge file
            using var response = await http.GetAsync($"{baseUrl}/health", HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return false;

            // Read at most 1 KB. A valid reply is about 80 bytes, a longer body is not ours.
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var buffer = new byte[1024];
            int total = 0, read;
            while (total < buffer.Length &&
                   (read = await stream.ReadAsync(buffer.AsMemory(total), token)) > 0)
            {
                total += read;
            }

            // A truncated or non-JSON body throws JsonException and counts as "not ours"
            using var doc = JsonDocument.Parse(buffer.AsMemory(0, total));
            var root = doc.RootElement;

            return root.TryGetProperty("app", out var app) && app.GetString() == AppId
                && root.TryGetProperty("instance", out var inst) && inst.GetString() == instanceId;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // DNS error, timeout, TLS error, bad JSON: all mean the same, the link is not usable.
            // The message helps the user to find a typo in the address.
            AppLog.Write($"    [check failed] {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            // The app is closing: not an error of the link
            return false;
        }
    }
}