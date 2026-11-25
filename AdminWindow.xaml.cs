using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SchoolDesk.Core;
using SchoolDesk.Security;

namespace SchoolDesk;

public partial class AdminWindow : Window
{
    AppSettings _s;
    readonly ObservableCollection<AppEntry> _apps = new();

    public AdminWindow()
    {
        InitializeComponent();
        _s = SettingsStore.Clone(SettingsStore.Load());
        AppsList.ItemsSource = _apps;
        Fill();
    }

    void Fill()
    {
        _apps.Clear();
        foreach (var a in _s.Apps) _apps.Add(a);
        WallpaperBox.Text = _s.WallpaperPath;
        ModeBox.SelectedValue = _s.WallpaperMode;
        if (ModeBox.SelectedIndex < 0) ModeBox.SelectedIndex = 0;
        ColorBox.Text = _s.BackgroundColor;
        ProcEditor.Load(_s.BlockedProcesses);
        BlockEditor.Load(_s.BlockedDomains);
        AllowEditor.Load(_s.AllowedDomains);
        SchoolBox.Text = _s.SchoolName;
        PcBox.Text = _s.ComputerName;
        StudentBox.Text = _s.StudentAccount;
        Chk24.IsChecked = _s.Use24Hour;
        ChkSec.IsChecked = _s.ShowSeconds;
        ChkNotif.IsChecked = _s.ShowNotifications;
        ChkGuard.IsChecked = _s.Watchdog;
        ChkAppLocker.IsChecked = _s.UseAppLocker;
        LogBox.Text = Log.ReadTail();
    }

    bool Collect()
    {
        try { _ = new BrushConverter().ConvertFromString(ColorBox.Text.Trim()); }
        catch { Status.Text = "Неверный цвет фона. Пример: #1F3A5F"; return false; }

        _s.Apps = _apps.ToList();
        _s.WallpaperPath = WallpaperBox.Text.Trim();
        _s.WallpaperMode = ModeBox.SelectedValue as string ?? "Fill";
        _s.BackgroundColor = ColorBox.Text.Trim();
        _s.BlockedProcesses = ProcEditor.Items.ToList();
        _s.BlockedDomains = BlockEditor.Items.ToList();
        _s.AllowedDomains = AllowEditor.Items.ToList();
        _s.SchoolName = SchoolBox.Text.Trim();
        _s.ComputerName = PcBox.Text.Trim();
        _s.StudentAccount = StudentBox.Text.Trim();
        _s.Use24Hour = Chk24.IsChecked == true;
        _s.ShowSeconds = ChkSec.IsChecked == true;
        _s.ShowNotifications = ChkNotif.IsChecked == true;
        _s.Watchdog = ChkGuard.IsChecked == true;
        _s.UseAppLocker = ChkAppLocker.IsChecked == true;
        return true;
    }

    bool SaveNow()
    {
        if (!Collect()) return false;
        Status.Text = "Сохранение…";
        var ok = ConfigService.Save(_s, out var msg);
        Status.Text = msg;
        LogBox.Text = Log.ReadTail();
        return ok;
    }

    void Save_Click(object sender, RoutedEventArgs e) => SaveNow();
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---- приложения ----
    void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Программы (*.exe)|*.exe|Все файлы|*.*", Title = "Выберите программу" };
        if (dlg.ShowDialog(this) != true) return;
        var a = new AppEntry { Name = Path.GetFileNameWithoutExtension(dlg.FileName), Path = dlg.FileName, Category = "Приложения" };
        _apps.Add(a);
        AppsList.SelectedItem = a;
    }

    void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is AppEntry a) _apps.Remove(a);
    }

    void Up_Click(object sender, RoutedEventArgs e)
    {
        var i = AppsList.SelectedIndex;
        if (i > 0) { _apps.Move(i, i - 1); AppsList.SelectedIndex = i - 1; }
    }

    void Down_Click(object sender, RoutedEventArgs e)
    {
        var i = AppsList.SelectedIndex;
        if (i >= 0 && i < _apps.Count - 1) { _apps.Move(i, i + 1); AppsList.SelectedIndex = i + 1; }
    }

    void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is not AppEntry a) return;
        var dlg = new OpenFileDialog { Filter = "Программы (*.exe)|*.exe|Все файлы|*.*" };
        if (dlg.ShowDialog(this) == true) a.Path = dlg.FileName;
    }

    void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        if (AppsList.SelectedItem is not AppEntry a) return;
        var dlg = new OpenFileDialog { Filter = "Иконки и программы|*.png;*.ico;*.jpg;*.bmp;*.exe|Все файлы|*.*" };
        if (dlg.ShowDialog(this) == true) a.IconPath = dlg.FileName;
    }

    // ---- оформление ----
    void BrowseWallpaper_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*", Title = "Выберите обои" };
        if (dlg.ShowDialog(this) == true) WallpaperBox.Text = dlg.FileName;
    }

    void ClearWallpaper_Click(object sender, RoutedEventArgs e) => WallpaperBox.Text = "";

    void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try { ColorPreview.Background = (Brush)new BrushConverter().ConvertFromString(ColorBox.Text.Trim())!; }
        catch { ColorPreview.Background = Brushes.Transparent; }
    }

    // ---- общие ----
    void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Вернуть настройки по умолчанию (пароль сохранится)?", "SchoolDesk",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var hash = _s.PasswordHash;
        var student = _s.StudentAccount;
        _s = Defaults.Create();
        _s.PasswordHash = hash;
        _s.StudentAccount = student;
        Fill();
        Status.Text = "Значения по умолчанию загружены. Нажмите «Сохранить и применить».";
    }

    void RefreshLog_Click(object sender, RoutedEventArgs e) => LogBox.Text = Log.ReadTail();

    // ---- пароль ----
    void ChangePwd_Click(object sender, RoutedEventArgs e)
    {
        if (Lockout.IsLocked(out var left))
        {
            PwdStatus.Text = $"Слишком много попыток. Подождите {Math.Ceiling(left.TotalSeconds)} с.";
            return;
        }
        var ok = PasswordService.Verify(OldPwd.Password, _s.PasswordHash);
        Lockout.Register(ok);
        if (!ok) { PwdStatus.Text = "Текущий пароль неверен."; return; }
        var err = PasswordService.Validate(NewPwd.Password);
        if (err != null) { PwdStatus.Text = err; return; }
        if (NewPwd.Password != NewPwd2.Password) { PwdStatus.Text = "Новые пароли не совпадают."; return; }

        var previous = _s.PasswordHash;
        _s.PasswordHash = PasswordService.Hash(NewPwd.Password);
        if (SaveNow())
        {
            PwdStatus.Text = "Пароль изменён.";
            OldPwd.Clear(); NewPwd.Clear(); NewPwd2.Clear();
            Log.Info("Пароль изменён");
        }
        else
        {
            _s.PasswordHash = previous;
            PwdStatus.Text = "Пароль не изменён: " + Status.Text;
        }
    }
}
