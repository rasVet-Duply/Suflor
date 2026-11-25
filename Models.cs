using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SchoolDesk.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class AppEntry : Observable
{
    string _name = "", _path = "", _args = "", _icon = "", _cat = "Приложения";
    bool _enabled = true;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Path { get => _path; set => Set(ref _path, value); }
    public string Arguments { get => _args; set => Set(ref _args, value); }
    public string IconPath { get => _icon; set => Set(ref _icon, value); }
    public string Category { get => _cat; set => Set(ref _cat, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
}

public class AppSettings
{
    public int Version { get; set; } = 1;
    public string SchoolName { get; set; } = "Школа";
    public string ComputerName { get; set; } = Environment.MachineName;
    public string StudentAccount { get; set; } = "Student";
    public string PasswordHash { get; set; } = "";

    public string WallpaperPath { get; set; } = "";
    /// <summary>Fill | Fit | Stretch | Center</summary>
    public string WallpaperMode { get; set; } = "Fill";
    public string BackgroundColor { get; set; } = "#FF1F3A5F";

    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool Watchdog { get; set; } = true;
    public bool UseAppLocker { get; set; } = true;

    public List<AppEntry> Apps { get; set; } = new();
    public List<string> BlockedProcesses { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();
    public List<string> AllowedDomains { get; set; } = new();
}
