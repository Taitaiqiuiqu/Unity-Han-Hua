namespace OneClickChineseMod.Core;

public class RpmLimiter
{
    private readonly int _maxRpm;
    private readonly Queue<DateTime> _window;
    private readonly object _lock = new();
    private static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(60);

    public int MaxRpm => _maxRpm;

    public RpmLimiter(int maxRpm)
    {
        _maxRpm = maxRpm;
        _window = new Queue<DateTime>();
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            CleanExpired();
            if (_window.Count >= _maxRpm)
                return false;
            _window.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    public int GetCurrentRpm()
    {
        lock (_lock)
        {
            CleanExpired();
            return _window.Count;
        }
    }

    private void CleanExpired()
    {
        var cutoff = DateTime.UtcNow - WindowSize;
        while (_window.Count > 0 && _window.Peek() < cutoff)
        {
            _window.Dequeue();
        }
    }
}
