using System.Text.Json;

namespace HiddenApp;

/// <summary>Preferências salvas em %AppData%\HiddenApp\settings.json.</summary>
internal class AppSettings
{
    public bool DarkTheme { get; set; } = true;
    public bool TrayHintShown { get; set; }

    /// <summary>Programas favoritos (caminho completo do .exe, inclusive de cópias).</summary>
    public List<string> Favorites { get; set; } = new();

    // ---------- Atualizações ----------
    /// <summary>Receber versões beta (pré-lançamento) além das versões estáveis.</summary>
    public bool IncludePrereleases { get; set; }
    /// <summary>Última versão sobre a qual o usuário já foi perguntado (para não perguntar toda hora).</summary>
    public string? LastPromptedVersion { get; set; }
    public DateTime LastPromptedUtc { get; set; }

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HiddenApp", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
