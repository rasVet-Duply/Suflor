using System.Text.Encodings.Web;
using System.Text.Json;

namespace SchoolDesk.Core;

public static class SettingsStore
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SchoolDesk");
    public static string FilePath => Path.Combine(Dir, "settings.json");

    static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(AppSettings s) => JsonSerializer.Serialize(s, Opt);
    public static AppSettings? Deserialize(string json) => JsonSerializer.Deserialize<AppSettings>(json, Opt);
    public static AppSettings Clone(AppSettings s) => Deserialize(Serialize(s))!;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = Deserialize(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch (Exception e)
        {
            Log.Error("Не удалось прочитать настройки, использую значения по умолчанию", e);
            try { File.Copy(FilePath, FilePath + ".broken", true); } catch { }
        }
        return Defaults.Create();
    }

    /// <summary>Атомарная запись. Требует прав на запись в ProgramData\SchoolDesk.</summary>
    public static void Write(AppSettings s)
    {
        Directory.CreateDirectory(Dir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, Serialize(s));
        File.Move(tmp, FilePath, true);
    }
}
