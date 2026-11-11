using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HiddenApp;

/// <summary>
/// Cria uma "cópia" de um programa feita de hard links (não ocupa espaço extra).
/// Como o .exe da cópia fica em outro caminho, o firewall consegue bloquear só ela.
/// As cópias ficam em  X:\HiddenAppClones\  (no mesmo disco do programa original,
/// porque hard links só funcionam dentro do mesmo disco).
/// Um programa pode ter várias cópias numeradas, cada uma com bloqueio e perfil próprios:
///   Nome_HASH      → cópia 1
///   Nome_HASH_2    → cópia 2 ...
/// </summary>
internal static class CloneManager
{
    const string CloneFolderName = "HiddenAppClones";
    const int MaxFiles = 30000;

    const string MetaFileName = "_HiddenApp.json";
    const string DataDirName = "_HiddenAppData";

    public record CloneResult(string CloneExe, int Number, int Linked, int Copied, int Failed);

    /// <summary>Uma cópia existente de um programa.</summary>
    public record CloneEntry(string Dir, int Number, string ExePath, string Label, string Args)
    {
        public bool IsBlocked => FirewallManager.IsBlocked(ExePath);
    }

    /// <summary>Dados salvos dentro da pasta da cópia (nome dado pelo usuário e argumentos usados).</summary>
    class CloneMeta
    {
        public string? Label { get; set; }
        public string? Args { get; set; }
    }

    static readonly Regex CloneFolderRegex = new(@"_[0-9A-F]{8}(?:_(\d+))?$", RegexOptions.IgnoreCase);

    public static string CloneRootFor(string path) =>
        Path.Combine(Path.GetPathRoot(Path.GetFullPath(path))!, CloneFolderName);

    public static bool IsClone(string? exePath) =>
        exePath != null &&
        exePath.StartsWith(CloneRootFor(exePath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Pasta raiz da cópia a que um .exe (de dentro de uma cópia) pertence.</summary>
    public static string CloneDirOf(string cloneExePath)
    {
        string root = CloneRootFor(cloneExePath);
        string rel = Path.GetRelativePath(root, cloneExePath);
        return Path.Combine(root, rel.Split(Path.DirectorySeparatorChar)[0]);
    }

    /// <summary>Nome base das pastas de cópia de um programa (sempre o mesmo para o mesmo programa).</summary>
    static string BaseFolderName(string exePath)
    {
        string srcDir = Path.GetDirectoryName(Path.GetFullPath(exePath))!;
        string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(srcDir.ToLowerInvariant())))[..8];
        return $"{Path.GetFileNameWithoutExtension(exePath)}_{hash}";
    }

    /// <summary>Pasta da cópia número <paramref name="number"/> de um programa.</summary>
    public static string CloneDirFor(string exePath, int number)
    {
        string name = BaseFolderName(exePath) + (number <= 1 ? "" : $"_{number}");
        return Path.Combine(CloneRootFor(exePath), name);
    }

    /// <summary>Lista as cópias já criadas de um programa, em ordem.</summary>
    public static List<CloneEntry> ListClones(string exePath)
    {
        var list = new List<CloneEntry>();
        string root = CloneRootFor(exePath);
        if (!Directory.Exists(root)) return list;

        string baseName = BaseFolderName(exePath);
        string exeName = Path.GetFileName(exePath);
        foreach (var dir in Directory.GetDirectories(root, baseName + "*"))
        {
            string folder = Path.GetFileName(dir);
            int number;
            if (folder.Equals(baseName, StringComparison.OrdinalIgnoreCase)) number = 1;
            else if (!folder.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase) ||
                     !int.TryParse(folder[(baseName.Length + 1)..], out number)) continue;

            string exe = Path.Combine(dir, exeName);
            if (!File.Exists(exe)) continue;
            var meta = ReadMeta(dir);
            list.Add(new CloneEntry(dir, number, exe, LabelFrom(meta, number), meta.Args ?? ""));
        }
        return list.OrderBy(c => c.Number).ToList();
    }

    /// <summary>Próximo número livre para uma cópia nova.</summary>
    public static int NextNumber(string exePath)
    {
        var existing = ListClones(exePath);
        return existing.Count == 0 ? 1 : existing.Max(c => c.Number) + 1;
    }

    /// <summary>Número da cópia a partir de um .exe que está dentro dela.</summary>
    public static int NumberOf(string cloneExePath)
    {
        var m = CloneFolderRegex.Match(Path.GetFileName(CloneDirOf(cloneExePath)));
        return m.Success && m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out int n) ? n : 1;
    }

    /// <summary>Nome exibido de uma cópia: o nome dado pelo usuário ou "Cópia N".</summary>
    public static string LabelOf(string cloneExePath)
    {
        int n = NumberOf(cloneExePath);
        return LabelFrom(ReadMeta(CloneDirOf(cloneExePath)), n);
    }

    /// <summary>Argumentos usados da última vez para abrir esta cópia.</summary>
    public static string ArgsOf(string cloneExePath) => ReadMeta(CloneDirOf(cloneExePath)).Args ?? "";

    static string LabelFrom(CloneMeta meta, int number) =>
        string.IsNullOrWhiteSpace(meta.Label) ? $"Cópia {number}" : meta.Label.Trim();

    static CloneMeta ReadMeta(string cloneDir)
    {
        try
        {
            string f = Path.Combine(cloneDir, MetaFileName);
            if (File.Exists(f)) return JsonSerializer.Deserialize<CloneMeta>(File.ReadAllText(f)) ?? new();
        }
        catch { }
        return new();
    }

    /// <summary>Salva o nome e os argumentos da cópia (para reabrir do mesmo jeito depois).</summary>
    public static void SaveMeta(string cloneExePath, string? label, string args)
    {
        try
        {
            var meta = new CloneMeta { Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(), Args = args };
            File.WriteAllText(Path.Combine(CloneDirOf(cloneExePath), MetaFileName), JsonSerializer.Serialize(meta));
        }
        catch { }
    }

    /// <summary>Conta os arquivos da pasta do programa (para avisar se for gigante).</summary>
    public static int CountFiles(string exePath)
    {
        int n = 0;
        foreach (var _ in EnumerateFiles(Path.GetDirectoryName(exePath)!))
            if (++n > MaxFiles) break;
        return n;
    }

    public static bool TooManyFiles(int count) => count > MaxFiles;

    /// <summary>
    /// Cria (ou atualiza) a cópia. Arquivos que já existem e não mudaram são mantidos,
    /// então abrir a cópia de novo é instantâneo.
    /// </summary>
    public static CloneResult CreateOrUpdate(string exePath, int number, IProgress<string>? progress = null)
    {
        exePath = Path.GetFullPath(exePath);
        if (IsClone(exePath)) throw new InvalidOperationException("Esse programa já é uma cópia do HiddenApp.");

        string srcDir = Path.GetDirectoryName(exePath)!;
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (winDir.Length > 0 && srcDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Programas da pasta do Windows não podem ser copiados.");

        if (number < 1) number = NextNumber(exePath);
        string dstDir = CloneDirFor(exePath, number);
        Directory.CreateDirectory(dstDir);

        int linked = 0, copied = 0, failed = 0, done = 0;
        foreach (var src in EnumerateFiles(srcDir))
        {
            string rel = Path.GetRelativePath(srcDir, src);
            string dst = Path.Combine(dstDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            if (++done % 200 == 0) progress?.Report($"Preparando cópia... {done} arquivos");

            try
            {
                if (File.Exists(dst))
                {
                    var a = new FileInfo(src); var b = new FileInfo(dst);
                    if (a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc) { linked++; continue; }
                    File.Delete(dst); // o original foi atualizado: refaz o link
                }

                if (Native.CreateHardLink(dst, src, IntPtr.Zero)) linked++;
                else { File.Copy(src, dst, true); copied++; } // sem permissão p/ link: copia de verdade
            }
            catch { failed++; }
        }

        string cloneExe = Path.Combine(dstDir, Path.GetFileName(exePath));
        if (!File.Exists(cloneExe))
            throw new InvalidOperationException("Não foi possível criar o executável da cópia.");

        return new CloneResult(cloneExe, number, linked, copied, failed);
    }

    /// <summary>
    /// Sugere argumentos para programas que só deixam abrir uma instância
    /// (sem isso eles só "repassam" a janela para o programa original).
    /// </summary>
    public static string SuggestArgs(string exePath, int number)
    {
        string dataDir = Path.Combine(CloneDirFor(exePath, number), DataDirName);
        return Path.GetFileName(exePath).ToLowerInvariant() switch
        {
            "chrome.exe" or "msedge.exe" or "brave.exe" or "vivaldi.exe" or "opera.exe"
                => $"--user-data-dir=\"{dataDir}\"",
            "firefox.exe" => $"-no-remote -profile \"{dataDir}\"",
            _ => ""
        };
    }

    /// <summary>Abre a cópia como usuário comum. Retorna o PID (ou 0).</summary>
    public static int Launch(string cloneExe, string args)
    {
        // Firefox exige que a pasta do perfil exista
        if (args.Contains(DataDirName))
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(cloneExe)!, DataDirName));

        string workDir = Path.GetDirectoryName(cloneExe)!;
        int pid = Native.StartUnelevated(cloneExe, args, workDir);
        if (pid != 0) return pid;

        // Plano B: abre normalmente (vai herdar o modo administrador)
        var p = Process.Start(new ProcessStartInfo(cloneExe, args) { WorkingDirectory = workDir, UseShellExecute = false });
        return p?.Id ?? 0;
    }

    /// <summary>Apaga todas as cópias de todos os discos (só apaga os links, nunca o original).</summary>
    public static (int deleted, int inUse) DeleteAllClones()
    {
        int deleted = 0, inUse = 0;

        // Caminhos de todos os processos abertos, para não apagar uma cópia em uso
        var running = new List<string>();
        foreach (var p in Process.GetProcesses())
        {
            try { var path = Native.GetProcessPath((uint)p.Id); if (path != null) running.Add(path); }
            catch { }
            finally { p.Dispose(); }
        }

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            string root = Path.Combine(drive.RootDirectory.FullName, CloneFolderName);
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (running.Any(r => r.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                { inUse++; continue; }
                try { Directory.Delete(dir, true); deleted++; }
                catch { inUse++; } // programa da cópia ainda aberto
            }
            try { if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root); } catch { }
        }
        return (deleted, inUse);
    }

    /// <summary>Lista arquivos recursivamente, sem entrar em atalhos de pasta (junctions/symlinks).</summary>
    static IEnumerable<string> EnumerateFiles(string dir)
    {
        var stack = new Stack<string>();
        stack.Push(dir);
        while (stack.Count > 0)
        {
            string current = stack.Pop();
            string[] files, subdirs;
            try
            {
                files = Directory.GetFiles(current);
                subdirs = Directory.GetDirectories(current);
            }
            catch { continue; }

            foreach (var f in files) yield return f;
            foreach (var d in subdirs)
            {
                try
                {
                    if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                    if (Path.GetFileName(d).Equals("_HiddenAppData", StringComparison.OrdinalIgnoreCase)) continue;
                }
                catch { continue; }
                stack.Push(d);
            }
        }
    }
}
