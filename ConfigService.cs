using System.ComponentModel;
using System.Diagnostics;
using SchoolDesk.Core;

namespace SchoolDesk.Security;

/// <summary>
/// Конфигурация лежит в ProgramData\SchoolDesk, ученик имеет там только чтение.
/// Сохранение выполняет повышенный процесс (UAC с данными администратора Windows):
/// пароль школы — это удобство, настоящая граница безопасности — учётная запись Windows.
/// </summary>
public static class ConfigService
{
    public static bool Save(AppSettings s, out string message)
    {
        try
        {
            if (Elevation.IsAdmin())
            {
                message = string.Join("\n", Commit(s));
                return true;
            }

            var stage = Path.Combine(Path.GetTempPath(), $"schooldesk-{Guid.NewGuid():N}.json");
            File.WriteAllText(stage, SettingsStore.Serialize(s));
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath!,
                    Arguments = $"--apply \"{stage}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });
                p!.WaitForExit();
                if (p.ExitCode == 0) { message = "Настройки сохранены и применены."; return true; }
                message = $"Не удалось применить настройки (код {p.ExitCode}). См. журнал.";
                return false;
            }
            finally { try { File.Delete(stage); } catch { } }
        }
        catch (Win32Exception)
        {
            message = "Операция отменена: нужны права администратора Windows.";
            return false;
        }
        catch (Exception e)
        {
            Log.Error("Сохранение настроек", e);
            message = "Ошибка: " + e.Message;
            return false;
        }
    }

    static List<string> Commit(AppSettings s)
    {
        var msgs = new List<string>();
        SettingsStore.Write(s);
        msgs.Add("Настройки сохранены.");
        try { BrowserPolicy.Apply(s); msgs.Add("Политики браузера применены."); }
        catch (Exception e) { Log.Error("Browser policy", e); msgs.Add("Ошибка политик браузера: " + e.Message); }
        msgs.Add(AppLockerPolicy.Apply(s));
        Log.Info("Конфигурация сохранена");
        return msgs;
    }

    /// <summary>Точка входа повышенного процесса: SchoolDesk.exe --apply &lt;файл&gt;.</summary>
    public static int RunApplyHelper(string stagePath)
    {
        try
        {
            if (!Elevation.IsAdmin()) return 2;
            var s = SettingsStore.Deserialize(File.ReadAllText(stagePath));
            if (s == null) return 3;
            Commit(s);
            return 0;
        }
        catch (Exception e) { Log.Error("apply helper", e); return 1; }
    }
}
