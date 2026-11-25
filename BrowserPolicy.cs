using Microsoft.Win32;
using SchoolDesk.Core;

namespace SchoolDesk.Security;

/// <summary>
/// Блокировка сайтов через управляемые политики Chrome и Edge (HKLM\SOFTWARE\Policies).
/// Браузер сам сверяет реальный URL (включая редиректы, поддомены и вложенные фреймы)
/// и показывает страницу «Заблокировано администратором».
/// Список разрешённых (URLAllowlist) имеет приоритет над запрещённым.
/// </summary>
public static class BrowserPolicy
{
    static readonly string[] Roots =
    {
        @"SOFTWARE\Policies\Google\Chrome",
        @"SOFTWARE\Policies\Microsoft\Edge"
    };

    static RegistryKey Hklm64() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    public static void Apply(AppSettings s)
    {
        using var hklm = Hklm64();
        foreach (var root in Roots)
        {
            using var k = hklm.CreateSubKey(root, true);
            WriteList(k, "URLBlocklist", s.BlockedDomains);
            WriteList(k, "URLAllowlist", s.AllowedDomains);
            // чтобы DNS-DoH не обходил фильтрацию
            k.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
            // инкогнито не отключает политики, но скрывает следы — отключаем
            k.SetValue(root.EndsWith("Edge") ? "InPrivateModeAvailability" : "IncognitoModeAvailability", 1, RegistryValueKind.DWord);
        }
        Log.Info($"Политики браузера применены: блок={s.BlockedDomains.Count}, исключения={s.AllowedDomains.Count}");
    }

    static void WriteList(RegistryKey parent, string name, IEnumerable<string> items)
    {
        parent.DeleteSubKeyTree(name, false);
        using var k = parent.CreateSubKey(name);
        int i = 1;
        foreach (var it in items.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            k.SetValue((i++).ToString(), it, RegistryValueKind.String);
    }

    public static void Remove()
    {
        using var hklm = Hklm64();
        foreach (var root in Roots)
        {
            using var k = hklm.OpenSubKey(root, true);
            if (k == null) continue;
            k.DeleteSubKeyTree("URLBlocklist", false);
            k.DeleteSubKeyTree("URLAllowlist", false);
            k.DeleteValue("DnsOverHttpsMode", false);
            k.DeleteValue("IncognitoModeAvailability", false);
            k.DeleteValue("InPrivateModeAvailability", false);
        }
        Log.Info("Политики браузера удалены");
    }
}
