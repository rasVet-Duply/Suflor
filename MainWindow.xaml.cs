using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SchoolDesk.Core;
using SchoolDesk.Security;

namespace SchoolDesk;

public partial class MainWindow : Window
{
    const string BannedText = "Это приложение запрещено администратором";
    static readonly CultureInfo Ru = new("ru-RU");

    AppSettings _s = new();
    readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _noticeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly ProcessGuard _guard = new();
    bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _clock.Tick += (_, _) => UpdateClock();
        _noticeTimer.Tick += (_, _) => { Notice.Visibility = Visibility.Collapsed; _noticeTimer.Stop(); };
        _guard.Blocked += _ => Dispatcher.BeginInvoke(new Action(() => ShowNotice(BannedText)));
        Loaded += (_, _) => { Reload(); _clock.Start(); Activate(); };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose) e.Cancel = true;   // Alt+F4 не закрывает школьный интерфейс
        base.OnClosing(e);
    }

    void Reload()
    {
        _s = SettingsStore.Load();
        SchoolText.Text = _s.SchoolName;
        PcText.Text = _s.ComputerName;
        ApplyBackground();
        BuildTiles();
        UpdateClock();
        _guard.Configure(_s.BlockedProcesses, _s.Watchdog);
    }

    void UpdateClock()
    {
        var fmt = _s.Use24Hour ? (_s.ShowSeconds ? "HH:mm:ss" : "HH:mm") : (_s.ShowSeconds ? "h:mm:ss tt" : "h:mm tt");
        TimeText.Text = DateTime.Now.ToString(fmt, Ru);
        var d = DateTime.Now.ToString("dddd, d MMMM", Ru);
        DateText.Text = char.ToUpper(d[0], Ru) + d[1..];
    }

    void ApplyBackground()
    {
        try { Background = (Brush)new BrushConverter().ConvertFromString(_s.BackgroundColor)!; }
        catch { Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x3A, 0x5F)); }

        Wallpaper.Source = null;
        if (string.IsNullOrWhiteSpace(_s.WallpaperPath) || !File.Exists(_s.WallpaperPath)) return;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(_s.WallpaperPath);
            if (_s.WallpaperMode != "Center") bi.DecodePixelWidth = (int)SystemParameters.PrimaryScreenWidth;
            bi.EndInit();
            bi.Freeze();
            Wallpaper.Source = bi;
            Wallpaper.Stretch = _s.WallpaperMode switch
            {
                "Fit" => Stretch.Uniform,
                "Stretch" => Stretch.Fill,
                "Center" => Stretch.None,
                _ => Stretch.UniformToFill
            };
        }
        catch (Exception e) { Log.Error("Не удалось загрузить обои", e); }
    }

    void BuildTiles()
    {
        Groups.Children.Clear();
        var apps = _s.Apps.Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Path)).ToList();
        if (apps.Count == 0)
        {
            Groups.Children.Add(new TextBlock
            {
                Text = "Нет доступных приложений. Откройте настройки (значок шестерёнки).",
                Foreground = Brushes.White, FontSize = 18, Margin = new Thickness(10)
            });
            return;
        }
        foreach (var g in apps.GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "Приложения" : a.Category.Trim()))
        {
            Groups.Children.Add(new TextBlock
            {
                Text = g.Key, FontSize = 20, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 255, 255, 255)),
                Margin = new Thickness(10, 16, 0, 4)
            });
            var wrap = new WrapPanel();
            foreach (var a in g) wrap.Children.Add(MakeTile(a));
            Groups.Children.Add(wrap);
        }
    }

    Button MakeTile(AppEntry a)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(MakeIcon(a));
        stack.Children.Add(new TextBlock
        {
            Text = a.Name, Foreground = Brushes.White, FontSize = 15,
            TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            MaxWidth = 130, Margin = new Thickness(0, 10, 0, 0)
        });
        var b = new Button { Content = stack, Style = (Style)FindResource("Tile"), Tag = a };
        b.Click += (_, _) => Launch(a);
        return b;
    }

    FrameworkElement MakeIcon(AppEntry a)
    {
        var src = LoadIcon(a);
        if (src != null) return new Image { Source = src, Width = 52, Height = 52 };
        return new Border
        {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(26),
            Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
            Child = new TextBlock
            {
                Text = a.Name.Length > 0 ? a.Name[..1].ToUpper(Ru) : "?", Foreground = Brushes.White,
                FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    static ImageSource? LoadIcon(AppEntry a)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(a.IconPath) && File.Exists(a.IconPath))
            {
                var ext = Path.GetExtension(a.IconPath).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico" or ".gif")
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(a.IconPath);
                    bi.DecodePixelWidth = 128;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            var exe = Util.ResolveExe(string.IsNullOrWhiteSpace(a.IconPath) ? a.Path : a.IconPath) ?? Util.ResolveExe(a.Path);
            if (exe != null)
            {
                using var ic = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (ic != null)
                {
                    var bs = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    return bs;
                }
            }
        }
        catch { }
        return null;
    }

    void Launch(AppEntry a)
    {
        try
        {
            var exe = Util.ResolveExe(a.Path);
            if (exe == null)
            {
                Log.Warn($"Файл не найден: {a.Name} ({a.Path})");
                ShowNotice($"Не найден файл программы «{a.Name}»");
                return;
            }
            var name = Path.GetFileNameWithoutExtension(exe);
            if (_s.BlockedProcesses.Any(x => string.Equals(ProcessGuard.Norm(x), name, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warn($"Запуск запрещённой программы: {name}");
                ShowNotice(BannedText);
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = a.Arguments ?? "",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true
            });
            Log.Info($"Запуск: {a.Name}");
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось запустить {a.Name}", e);
            ShowNotice($"Не удалось запустить «{a.Name}»");
        }
    }

    void ShowNotice(string text)
    {
        if (!_s.ShowNotifications) return;
        NoticeText.Text = text;
        Notice.Visibility = Visibility.Visible;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (!Auth.Ask(this, "Настройки SchoolDesk")) return;
        new AdminWindow { Owner = this }.ShowDialog();
        Reload();
    }

    void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (!Auth.Ask(this, "Выход из школьного режима")) return;
        Log.Info("Администратор вышел в обычный рабочий стол");
        try
        {
            var sid = Process.GetCurrentProcess().SessionId;
            if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == sid))
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("Запуск проводника", ex); }
        _allowClose = true;
        _guard.Dispose();
        Application.Current.Shutdown(ExitCodes.AdminExit);
    }
}
