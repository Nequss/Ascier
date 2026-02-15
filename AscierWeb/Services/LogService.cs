namespace AscierWeb.Services;

// serwis logów aplikacji - bufor kołowy z broadcastem przez signalr
// max 500 wpisów w pamięci, najstarsze wypadają
public sealed class LogService
{
    private readonly Queue<LogEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 500;

    public event Action<LogEntry>? OnLog;

    public void Log(string level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        };

        lock (_lock)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxEntries)
                _entries.Dequeue();
        }

        OnLog?.Invoke(entry);
    }

    public void Info(string msg) => Log("info", msg);
    public void Error(string msg) => Log("error", msg);
    public void Warn(string msg) => Log("warn", msg);

    public List<LogEntry> GetRecent(int count = 100)
    {
        lock (_lock)
        {
            return _entries.TakeLast(Math.Min(count, _entries.Count)).ToList();
        }
    }
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "";
    public string Message { get; init; } = "";
}
