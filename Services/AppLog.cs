namespace KeyboardController.Services;

// Simple in-memory log storage shared by the whole app
public static class AppLog
{
    private static readonly List<string> _lines = new();

    // Lines can be written from many threads (web server, tunnel, tray), so we lock
    private static readonly object _lock = new();

    // Keep memory usage bounded: old lines are dropped when the limit is reached
    private const int MaxLines = 1000;

    // Raised for every new line; may be called from ANY thread
    public static event Action<string>? LineAdded;

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        lock (_lock)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines)
                _lines.RemoveAt(0);
        }

        // Still print to console, useful when running from Visual Studio
        Console.WriteLine(line);

        LineAdded?.Invoke(line);
    }

    // Returns a copy so the caller can enumerate it without holding the lock
    public static string[] GetAll()
    {
        lock (_lock)
        {
            return _lines.ToArray();
        }
    }
}