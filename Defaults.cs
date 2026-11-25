namespace SchoolDesk.Core;

public static class Defaults
{
    public static AppSettings Create()
    {
        var s = new AppSettings();
        s.BlockedProcesses.AddRange(new[]
        {
            "steam", "steamwebhelper", "EpicGamesLauncher", "RobloxPlayerBeta", "RobloxPlayerLauncher",
            "Battle.net", "EADesktop", "Origin", "LeagueClient", "Minecraft", "MinecraftLauncher", "Discord"
        });
        s.BlockedDomains.AddRange(new[]
        {
            // Яндекс Игры (все основные зоны + поддомены)
            "yandex.ru/games", "yandex.by/games", "yandex.kz/games", "yandex.uz/games", "yandex.com/games",
            "games.yandex.ru", "games.yandex.com", "yandexgames.ru", "yandex.net/games",
            // популярные игровые порталы (список редактируется в панели администратора)
            "poki.com", "crazygames.com", "miniclip.com", "y8.com", "friv.com", "kizi.com",
            "agame.com", "addictinggames.com", "games.mail.ru", "vk.com/games", "roblox.com", "miniplay.com"
        });
        s.Apps.AddRange(DiscoverApps());
        return s;
    }

    static AppEntry Make(string name, string cat, params string[] candidates)
    {
        var found = candidates.FirstOrDefault(File.Exists);
        return new AppEntry { Name = name, Category = cat, Path = found ?? candidates[0], Enabled = found != null };
    }

    static string[] Office(string exe)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        var rels = new[]
        {
            @"Microsoft Office\root\Office16\", @"Microsoft Office\Office16\",
            @"Microsoft Office\Office15\", @"Microsoft Office\Office14\"
        };
        return roots.SelectMany(r => rels.Select(rel => Path.Combine(r, rel, exe))).ToArray();
    }

    static IEnumerable<AppEntry> DiscoverApps()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Make("Google Chrome", "Интернет",
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"));
        yield return Make("Word", "Офис", Office("WINWORD.EXE"));
        yield return Make("Excel", "Офис", Office("EXCEL.EXE"));
        yield return Make("PowerPoint", "Офис", Office("POWERPNT.EXE"));
        yield return Make("Scratch", "Творчество",
            Path.Combine(pf, @"Scratch 3\Scratch 3.exe"),
            Path.Combine(pf, @"Scratch Desktop\Scratch Desktop.exe"),
            Path.Combine(local, @"Programs\Scratch 3\Scratch 3.exe"),
            Path.Combine(local, @"Programs\Scratch Desktop\Scratch Desktop.exe"));
        yield return Make("Paint", "Творчество", Path.Combine(Environment.SystemDirectory, "mspaint.exe"));
    }
}
