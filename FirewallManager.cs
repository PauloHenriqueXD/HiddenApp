using System.Diagnostics;
using System.Text.Json;

namespace HiddenApp;

/// <summary>
/// Cria/remove regras no Firewall do Windows que bloqueiam um .exe específico.
/// As regras ficam salvas no Windows, então o bloqueio continua mesmo se o HiddenApp
/// for fechado ou o PC reiniciado — até você desbloquear.
/// </summary>
internal static class FirewallManager
{
    const string RulePrefix = "HiddenApp - ";

    static readonly string StateFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HiddenApp", "blocked.json");

    static readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Blocked => _blocked;

    public static bool IsBlocked(string exePath) => BlockedEntryFor(exePath) != null;

    /// <summary>
    /// Entrada da lista de bloqueados que cobre esse .exe. Para cópias, qualquer .exe
    /// dentro da mesma pasta de cópia é coberto pelo bloqueio da cópia.
    /// </summary>
    static string? BlockedEntryFor(string exePath)
    {
        if (_blocked.Contains(exePath)) return exePath;
        if (!CloneManager.IsClone(exePath)) return null;
        string dir = CloneManager.CloneDirOf(exePath);
        return _blocked.FirstOrDefault(b => CloneManager.IsClone(b) &&
            CloneManager.CloneDirOf(b).Equals(dir, StringComparison.OrdinalIgnoreCase));
    }

    static string RuleName(string exePath) => RulePrefix + exePath;

    /// <summary>Carrega a lista salva e confere no firewall se cada regra ainda existe.</summary>
    public static void Load()
    {
        _blocked.Clear();
        if (!File.Exists(StateFile)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StateFile)) ?? new();
            foreach (var path in list)
                if (RuleExists(path)) _blocked.Add(path);
        }
        catch { }
        Save();
    }

    static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(_blocked.ToList()));
    }

    /// <summary>
    /// Executáveis afetados pelo bloqueio. Para uma cópia do HiddenApp, bloqueia todos os .exe
    /// da pasta da cópia (muitos programas usam um launcher + outro .exe para a internet).
    /// </summary>
    public static List<string> TargetsFor(string exePath)
    {
        var list = new List<string> { exePath };
        if (CloneManager.IsClone(exePath))
        {
            string cloneDir = CloneManager.CloneDirOf(exePath);
            try
            {
                list.AddRange(Directory.EnumerateFiles(cloneDir, "*.exe", SearchOption.AllDirectories)
                    .Where(f => !f.Equals(exePath, StringComparison.OrdinalIgnoreCase)));
            }
            catch { }
        }
        return list;
    }

    public static void Block(string exePath)
    {
        if (IsBlocked(exePath)) return;

        foreach (var target in TargetsFor(exePath))
        {
            string name = RuleName(target);
            // Remove restos antigos com o mesmo nome, se houver
            Netsh($"advfirewall firewall delete rule name=\"{name}\"");

            foreach (var dir in new[] { "out", "in" })
            {
                var (code, output) = Netsh(
                    $"advfirewall firewall add rule name=\"{name}\" dir={dir} action=block " +
                    $"program=\"{target}\" enable=yes profile=any");
                if (code != 0)
                    throw new InvalidOperationException($"Falha ao criar regra de firewall ({dir}) para {target}:\n{output}");
            }
        }

        _blocked.Add(exePath);
        Save();
    }

    public static void Unblock(string exePath)
    {
        exePath = BlockedEntryFor(exePath) ?? exePath;
        foreach (var target in TargetsFor(exePath))
            Netsh($"advfirewall firewall delete rule name=\"{RuleName(target)}\"");
        _blocked.Remove(exePath);
        Save();
    }

    public static void UnblockAll()
    {
        foreach (var path in _blocked.ToList()) Unblock(path);
    }

    static bool RuleExists(string exePath) =>
        Netsh($"advfirewall firewall show rule name=\"{RuleName(exePath)}\"").code == 0;

    static (int code, string output) Netsh(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output.Trim());
    }
}
