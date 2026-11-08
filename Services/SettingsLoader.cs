using System.Text.Json;

namespace KeyboardController.Services;

// Plain data holder
public sealed class AppSettings
{
    public const int DefaultPort = 8181;

    // Local port the web server listens on (your tunnel must forward to it)
    public int Port { get; init; } = DefaultPort;

    // User-provided public address, null means "not configured"
    public string? PublicUrl { get; init; }
}

public static class SettingsLoader
{
    public const string FileName = "settings.json";

    // Written as raw text (not serialized) so it can contain explanatory comments
    private const string Template = """
        {
          // Local port the app listens on.
          // Point your own tunnel / reverse proxy to http://127.0.0.1:<port>
          "port": 8181,

          // Public address of your own tunnel or domain, for example "https://pad.example.com".
          // The app checks that the address really leads to this program.
          // If it is empty or does not work, the built-in Localtunnel is started automatically.
          "publicUrl": ""
        }
        """;

    public static AppSettings Load(string path)
    {
        // First run: create a template so the user can see what is configurable
        if (!File.Exists(path))
        {
            TryWriteTemplate(path);
            return new AppSettings();
        }

        try
        {
            // Skip comments and tolerate trailing commas: users edit this file by hand
            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            using var doc = JsonDocument.Parse(File.ReadAllText(path), options);
            var root = doc.RootElement;

            // Each field is parsed on its own: one mistake must not reset the others
            int port = ReadPort(root);
            string? url = ReadUrl(root);
            return new AppSettings { Port = port, PublicUrl = url };
        }
        catch (Exception ex)
        {
            // A broken file must not crash the app: report and fall back to defaults
            AppLog.Write($"[!] Cannot read {FileName}: {ex.Message}. Using defaults.");
            return new AppSettings();
        }
    }

    private static int ReadPort(JsonElement root)
    {
        if (!root.TryGetProperty("port", out var el)) return AppSettings.DefaultPort;

        // Valid TCP port range is 1..65535
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var port) && port is >= 1 and <= 65535)
            return port;

        AppLog.Write($"[!] Invalid port in {FileName}, using {AppSettings.DefaultPort}");
        return AppSettings.DefaultPort;
    }

    private static string? ReadUrl(JsonElement root)
    {
        if (!root.TryGetProperty("publicUrl", out var el) || el.ValueKind != JsonValueKind.String)
            return null;

        var text = el.GetString()?.Trim();
        if (string.IsNullOrEmpty(text)) return null; // empty means "not configured", not an error

        // Accept only absolute http(s) URLs, anything else would give the user a dead link
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // Remove the trailing slash so we can safely append paths or query strings later
            return uri.ToString().TrimEnd('/');
        }

        AppLog.Write($"[!] Invalid publicUrl in {FileName}: \"{text}\". It must start with http:// or https://");
        return null;
    }

    private static void TryWriteTemplate(string path)
    {
        try
        {
            File.WriteAllText(path, Template);
        }
        catch (Exception ex)
        {
            // The folder may be read-only (for example Program Files): not critical
            AppLog.Write($"[!] Cannot create {FileName}: {ex.Message}");
        }
    }
}