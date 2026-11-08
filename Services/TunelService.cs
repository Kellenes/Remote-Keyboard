using System.Net.Sockets;
using System.Text.Json;

namespace KeyboardController.Services;

public class TunnelService
{
    private readonly HttpClient _http = new();
    private CancellationTokenSource? _cts;

    public async Task<string?> StartAsync(int localPort, string? requestedSubdomain = null)
    {
        _cts = new CancellationTokenSource();

        try
        {
            // 1. Access the Localtunnel API to get a remote port and URL
            string requestUrl = string.IsNullOrWhiteSpace(requestedSubdomain)
                ? "https://localtunnel.me/?new"
                : $"https://localtunnel.me/{requestedSubdomain}";

            var response = await _http.GetStringAsync(requestUrl, _cts.Token);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            string assignedUrl = root.GetProperty("url").GetString()!;
            int remotePort = root.GetProperty("port").GetInt32();
            int maxConn = root.TryGetProperty("max_conn", out var mc) ? mc.GetInt32() : 10;

            AppLog.Write($"[✓] Assigned remote port: {remotePort}");

            // 2. Start maintaining a pool of connections to handle incoming requests
            _ = Task.Run(() => MaintainProxyPoolAsync(remotePort, localPort, maxConn, _cts.Token));

            return assignedUrl;
        }
        catch (Exception ex)
        {
            AppLog.Write($"[!] Failed to start tunnel: {ex.Message}");
            return null;
        }
    }

    private async Task MaintainProxyPoolAsync(int remotePort, int localPort, int maxConn, CancellationToken ct)
    {
        // Hold a list of tasks to manage multiple connections
        var tasks = new List<Task>();
        for (int i = 0; i < maxConn; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await HandleTunnelConnectionAsync(remotePort, localPort, ct);
                    }
                    catch
                    {
                        await Task.Delay(1000, ct); // Pause before reconnecting
                    }
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task HandleTunnelConnectionAsync(int remotePort, int localPort, CancellationToken ct)
    {
        using var remoteClient = new TcpClient();
        await remoteClient.ConnectAsync("localtunnel.me", remotePort, ct);

        using var localClient = new TcpClient();
        await localClient.ConnectAsync("127.0.0.1", localPort, ct);

        using var remoteStream = remoteClient.GetStream();
        using var localStream = localClient.GetStream();

        // Two-way data transfer between remote and local streams
        var task1 = remoteStream.CopyToAsync(localStream, ct);
        var task2 = localStream.CopyToAsync(remoteStream, ct);

        await Task.WhenAny(task1, task2);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}