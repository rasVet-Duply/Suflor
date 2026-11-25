namespace SchoolDesk.Core;

public static class Log
{
    static readonly object L = new();
    public static string Dir => Path.Combine(SettingsStore.Dir, "logs");

    public static void Info(string m) => Write("INFO", m);
    public static void Warn(string m) => Write("WARN", m);
    public static void Error(string m, Exception? e = null) => Write("ERROR", e == null ? m : $"{m}: {e}");

    static void Write(string level, string m)
    {
        try
        {
            lock (L)
            {
                Directory.CreateDirectory(Dir);
                var file = Path.Combine(Dir, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss} [{level}] {Environment.UserName}: {m}{Environment.NewLine}");
            }
        }
        catch { /* журнал не должен ронять программу */ }
    }

    public static string ReadTail(int lines = 300)
    {
        try
        {
            if (!Directory.Exists(Dir)) return "Журнал пуст.";
            var files = new DirectoryInfo(Dir).GetFiles("*.log").OrderByDescending(f => f.Name).Take(3);
            var all = new List<string>();
            foreach (var f in files.Reverse())
            {
                using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                all.AddRange(sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            return string.Join(Environment.NewLine, all.TakeLast(lines).Select(x => x.TrimEnd('\r')));
        }
        catch (Exception e) { return "Не удалось прочитать журнал: " + e.Message; }
    }
}
