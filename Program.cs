using System.Diagnostics;
using System.Windows;
using SchoolDesk.Core;
using SchoolDesk.Security;

namespace SchoolDesk;

public static class ExitCodes
{
    /// <summary>Администратор штатно вышел в обычный Windows.</summary>
    public const int AdminExit = 42;
}

public static class Program
{
    // Режимы:
    //  (без аргументов)       — оболочка-супервизор (регистрируется как Winlogon\Shell ученика)
    //  --ui                   — сам интерфейс
    //  --setup [--student N]  — первичная настройка (запускать от администратора)
    //  --apply <file>         — повышенное применение настроек (служебный)
    //  --reset-password       — сбросить пароль школы (от администратора)
    //  --remove-policies      — убрать политики браузера и AppLocker (от администратора)
    [STAThread]
    public static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "";
        switch (mode)
        {
            case "--apply":
                return args.Length > 1 ? ConfigService.RunApplyHelper(args[1]) : 1;

            case "--remove-policies":
                if (!Elevation.IsAdmin()) return 2;
                BrowserPolicy.Remove();
                AppLockerPolicy.Remove();
                return 0;

            case "--reset-password":
                if (!Elevation.IsAdmin()) return 2;
                var cur = SettingsStore.Load();
                cur.PasswordHash = "";
                SettingsStore.Write(cur);
                Log.Warn("Пароль школы сброшен администратором Windows");
                return 0;

            case "--setup":
                return RunSetup(args);

            case "--ui":
                return RunUi();

            default:
                return Supervisor.Run();
        }
    }

    static Application CreateApp()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log.Error("Необработанное исключение UI", e.Exception); e.Handled = true; };
        return app;
    }

    static int RunSetup(string[] args)
    {
        CreateApp();
        var s = SettingsStore.Load();
        var i = Array.IndexOf(args, "--student");
        if (i >= 0 && i + 1 < args.Length) s.StudentAccount = args[i + 1];
        var w = new SetupWindow(s);
        if (w.ShowDialog() != true) return 1;
        var ok = ConfigService.Save(w.Result, out var msg);
        MessageBox.Show(msg, "SchoolDesk", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        return ok ? 0 : 1;
    }

    static int RunUi()
    {
        using var mutex = new Mutex(true, @"Local\SchoolDeskUi", out var created);
        if (!created) return 0;
        var app = CreateApp();
        Log.Info("Интерфейс запущен");
        return app.Run(new MainWindow());
    }
}

/// <summary>
/// Процесс-оболочка. Запускает интерфейс и перезапускает его, если он закрыт/упал.
/// Если администратор вышел в Windows — ждёт закрытия проводника и возвращает школьный режим.
/// Сам не завершается: иначе Windows разлогинит пользователя.
/// </summary>
static class Supervisor
{
    public static int Run()
    {
        using var mutex = new Mutex(true, @"Local\SchoolDeskSupervisor", out var created);
        if (!created) return 0;

        var self = Environment.ProcessPath!;
        int quick = 0;
        while (true)
        {
            var started = DateTime.UtcNow;
            int code;
            try
            {
                using var p = Process.Start(new ProcessStartInfo(self, "--ui") { UseShellExecute = false })!;
                p.WaitForExit();
                code = p.ExitCode;
            }
            catch (Exception e) { Log.Error("Запуск интерфейса", e); code = -1; }

            if (code == ExitCodes.AdminExit)
            {
                WaitForExplorerToClose();
                quick = 0;
                continue;
            }
            Log.Warn($"Интерфейс завершился (код {code}), перезапуск");
            quick = (DateTime.UtcNow - started).TotalSeconds < 5 ? quick + 1 : 0;
            Thread.Sleep(quick >= 5 ? 10_000 : 500); // защита от бесконечного быстрого цикла
        }
    }

    static void WaitForExplorerToClose()
    {
        var sid = Process.GetCurrentProcess().SessionId;
        Thread.Sleep(3000);
        while (Process.GetProcessesByName("explorer").Any(x => x.SessionId == sid))
            Thread.Sleep(2000);
        Log.Info("Проводник закрыт — возврат в школьный режим");
    }
}

static class Util
{
    public static string? ResolveExe(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (Path.IsPathRooted(path)) return File.Exists(path) ? path : null;
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Append(Environment.SystemDirectory);
        foreach (var d in dirs)
        {
            try
            {
                var c = Path.Combine(d.Trim(), path);
                if (File.Exists(c)) return c;
                if (!c.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(c + ".exe")) return c + ".exe";
            }
            catch { }
        }
        return null;
    }
}

static class Auth
{
    /// <summary>Просит пароль школы. Если пароль ещё не задан — запускает первичную настройку.</summary>
    public static bool Ask(Window owner, string title)
    {
        var s = SettingsStore.Load();
        if (string.IsNullOrEmpty(s.PasswordHash))
        {
            var w = new SetupWindow(s) { Owner = owner };
            if (w.ShowDialog() != true) return false;
            if (!ConfigService.Save(w.Result, out var m)) { MessageBox.Show(owner, m, "SchoolDesk"); return false; }
            return true;
        }
        return new PasswordDialog(title, s.PasswordHash) { Owner = owner }.ShowDialog() == true;
    }
}
