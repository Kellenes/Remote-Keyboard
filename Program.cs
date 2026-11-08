using System.Net.WebSockets;
using System.Text;
using KeyboardController.Services;
using KeyboardController.UI;

// AppContext.BaseDirectory is the folder of the .exe and works with PublishSingleFile.
// Do not use the current directory: it differs when the app is started via a shortcut.
var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsLoader.FileName);
var settings = SettingsLoader.Load(settingsPath);
int port = settings.Port;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Listen on loclhost on port 8181
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

var app = builder.Build();

// Enable automatic serving of index.html from the wwwroot folder
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

var inputHandler = new InputHandler();

// New value on every launch, see UrlVerifier for the reason
var instanceId = UrlVerifier.NewInstanceId();

// Public identification endpoint: it must reveal nothing except "this is our app"
app.Map("/health", async context =>
{
    // Forbid caching, otherwise a proxy could return a reply from a previous launch
    context.Response.Headers.CacheControl = "no-store";
    context.Response.ContentType = "application/json";

    // JSON is built by hand: serializing anonymous types relies on reflection
    // and may break in the trimmed Release build
    await context.Response.WriteAsync($"{{\"app\":\"{UrlVerifier.AppId}\",\"instance\":\"{instanceId}\"}}");
});

// Handle WebSocket
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    AppLog.Write("[+] Device connected.");

    var buffer = new byte[1024 * 4];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                inputHandler.ProcessCommand(json);
            }
        }
    }
    catch { }
    finally
    {
        AppLog.Write("[-] Device disconnected.");
    }
});

// Tray init
var trayService = new TrayService();
trayService.Initialize();
AppLog.Write("Icon added to system tray");

// Start the web server
try
{
    await app.StartAsync();
}
catch (IOException ex)
{
    // Kestrel throws an IOException subclass when the port is already in use
    AppLog.Write($"[!] Cannot start the server on port {port}: {ex.Message}");
    AppLog.Write($"[!] Change \"port\" in {SettingsLoader.FileName} and restart.");
    return;
}

string? publicUrl = null;

if (settings.PublicUrl != null)
{
    AppLog.Write($"[*] Checking custom URL: {settings.PublicUrl}");

    bool alive = await UrlVerifier.IsOurServerAsync(
        settings.PublicUrl,
        instanceId,
        attempts: 6,
        attemptTimeout: TimeSpan.FromSeconds(4),
        pause: TimeSpan.FromSeconds(10),
        // Cancel the check if the user closes the app meanwhile
        ct: app.Lifetime.ApplicationStopping);

    if (alive)
    {
        publicUrl = settings.PublicUrl;
        AppLog.Write("[+] Custom URL leads to this app");
    }
    else
    {
        AppLog.Write("[!] Custom URL does not lead to this app, falling back to Localtunnel");
    }
}

// Reached when no custom URL is set or when the check failed
if (publicUrl == null)
{
    var lt = new TunnelService();
    AppLog.Write("[*] Connecting to Localtunnel...");
    publicUrl = await lt.StartAsync(port, "pad-" + Environment.MachineName.ToLower());
}

if (!string.IsNullOrEmpty(publicUrl))
{
    Console.ForegroundColor = ConsoleColor.Green;
    AppLog.Write($"\n==========================================");
    AppLog.Write($" Link: {publicUrl}");
    AppLog.Write($"==========================================\n");
    Console.ResetColor();
}
else
{
    AppLog.Write($"[!] No public link. Local address: http://localhost:{port}");
}


await app.WaitForShutdownAsync();