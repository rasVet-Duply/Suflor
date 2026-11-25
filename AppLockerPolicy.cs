using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using SchoolDesk.Core;

namespace SchoolDesk.Security;

/// <summary>
/// Формирует и применяет политику AppLocker (коллекция Exe):
///  • администраторам разрешено всё;
///  • всем разрешены %WINDIR% и %PROGRAMFILES% (там установлены Chrome/Office);
///  • явно разрешены программы из списка администратора (в т.ч. вне Program Files);
///  • запрещены процессы из чёрного списка;
///  • для ученической учётной записи запрещены cmd, PowerShell, regedit, taskmgr и т.п.
/// Всё остальное (игры в AppData, Загрузках, на флешках) не запустится — это «белый список».
/// AppLocker полноценно работает в Enterprise/Education; в Pro — не гарантируется; в Home недоступен.
/// </summary>
public static class AppLockerPolicy
{
    const string Everyone = "S-1-1-0";
    const string Admins = "S-1-5-32-544";

    static readonly string[] StudentDeniedTools =
    {
        "cmd", "powershell", "powershell_ise", "pwsh", "regedit", "taskmgr", "mmc",
        "wscript", "cscript", "mshta", "msconfig", "control", "wt"
    };

    public static bool IsEditionSupported(out string edition)
    {
        edition = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", "") as string ?? "";
        return edition.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase)
            || edition.StartsWith("Education", StringComparison.OrdinalIgnoreCase)
            || edition.StartsWith("Professional", StringComparison.OrdinalIgnoreCase);
    }

    static string? ResolveSid(string account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        try { return new NTAccount(account).Translate(typeof(SecurityIdentifier)).Value; }
        catch { return null; }
    }

    public static string BuildXml(AppSettings s)
    {
        var rules = new StringBuilder();
        void Rule(string name, string sid, string action, string path) =>
            rules.Append($"<FilePathRule Id=\"{Guid.NewGuid()}\" Name=\"{SecurityElement.Escape(name)}\" Description=\"SchoolDesk\" " +
                         $"UserOrGroupSid=\"{sid}\" Action=\"{action}\"><Conditions><FilePathCondition Path=\"{SecurityElement.Escape(path)}\" />" +
                         "</Conditions></FilePathRule>");

        Rule("SchoolDesk: администраторы", Admins, "Allow", "*");
        Rule("SchoolDesk: Windows", Everyone, "Allow", @"%WINDIR%\*");
        Rule("SchoolDesk: Program Files", Everyone, "Allow", @"%PROGRAMFILES%\*");

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        foreach (var a in s.Apps.Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Path)))
        {
            var p = Environment.ExpandEnvironmentVariables(a.Path.Trim().Trim('"'));
            if (!Path.IsPathRooted(p)) continue;
            Rule("Разрешено: " + a.Name, Everyone, "Allow", p);
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir) &&
                !dir.StartsWith(pf, StringComparison.OrdinalIgnoreCase) &&
                !dir.StartsWith(pf86, StringComparison.OrdinalIgnoreCase) &&
                !dir.StartsWith(win, StringComparison.OrdinalIgnoreCase))
                Rule("Разрешено (папка): " + a.Name, Everyone, "Allow", dir + @"\*");
        }

        foreach (var b in s.BlockedProcesses.Select(ProcessGuard.Norm).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            Rule("Запрещено: " + b, Everyone, "Deny", @"*\" + b + ".exe");

        var studentSid = ResolveSid(s.StudentAccount);
        if (studentSid != null)
            foreach (var t in StudentDeniedTools)
                Rule("Ученик: запрет " + t, studentSid, "Deny", @"*\" + t + ".exe");

        return "<AppLockerPolicy Version=\"1\"><RuleCollection Type=\"Exe\" EnforcementMode=\"Enabled\">" +
               rules + "</RuleCollection></AppLockerPolicy>";
    }

    public static string Apply(AppSettings s)
    {
        if (!s.UseAppLocker) return "AppLocker отключён в настройках.";
        if (!IsEditionSupported(out var ed))
            return $"AppLocker недоступен в редакции Windows «{ed}» — пропущено. Работают политики браузера и мониторинг процессов.";
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "schooldesk-applocker.xml");
            File.WriteAllText(tmp, BuildXml(s), Encoding.UTF8);
            Run("sc.exe", "config appidsvc start= auto");
            Run("sc.exe", "start appidsvc");
            var code = Run("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-AppLockerPolicy -XmlPolicy '{tmp}'\"");
            try { File.Delete(tmp); } catch { }
            Log.Info($"AppLocker: код {code}");
            return code == 0 ? "Политика AppLocker применена." : $"Не удалось применить AppLocker (код {code}). См. журнал.";
        }
        catch (Exception e)
        {
            Log.Error("AppLocker", e);
            return "Ошибка AppLocker: " + e.Message;
        }
    }

    public static void Remove()
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "schooldesk-applocker-empty.xml");
            File.WriteAllText(tmp, "<AppLockerPolicy Version=\"1\" />", Encoding.UTF8);
            Run("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-AppLockerPolicy -XmlPolicy '{tmp}'\"");
            try { File.Delete(tmp); } catch { }
        }
        catch (Exception e) { Log.Error("AppLocker remove", e); }
    }

    static int Run(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false })!;
        if (!p.WaitForExit(60_000)) { try { p.Kill(); } catch { } return -1; }
        return p.ExitCode;
    }
}
