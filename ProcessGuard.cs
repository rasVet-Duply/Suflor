using System.Diagnostics;
using SchoolDesk.Core;

namespace SchoolDesk.Security;

/// <summary>Второй рубеж защиты: завершает запрещённые процессы в сессии ученика (раз в 2 с).</summary>
public sealed class ProcessGuard : IDisposable
{
    readonly System.Threading.Timer _timer;
    readonly int _sessionId = Process.GetCurrentProcess().SessionId;
    volatile HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? Blocked;

    public ProcessGuard() => _timer = new System.Threading.Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);

    public static string Norm(string n)
    {
        n = n.Trim().Trim('"');
        return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
    }

    public void Configure(IEnumerable<string> names, bool enabled)
    {
        _names = new HashSet<string>(names.Select(Norm).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
        var on = enabled && _names.Count > 0;
        _timer.Change(on ? 0 : Timeout.Infinite, on ? 2000 : Timeout.Infinite);
    }

    void Tick()
    {
        try
        {
            var names = _names;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.SessionId == _sessionId && names.Contains(p.ProcessName))
                    {
                        var n = p.ProcessName;
                        p.Kill(true);
                        Log.Warn($"Завершён запрещённый процесс: {n}");
                        Blocked?.Invoke(n);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();
}
