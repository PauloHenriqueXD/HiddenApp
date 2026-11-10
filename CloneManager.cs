using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace HiddenApp;

/// <summary>
/// Cria uma "cópia" de um programa feita de hard links (não ocupa espaço extra).
/// Como o .exe da cópia fica em outro caminho, o firewall consegue bloquear só ela.
/// As cópias ficam em  X:\HiddenAppClones\  (no mesmo disco do programa original,
/// porque hard links só funcionam dentro do mesmo disco).
/// </summary>
internal static class CloneManager
{
    const string CloneFolderName = "HiddenAppClones";
    const int MaxFiles = 30000;

    public record CloneResult(string CloneExe, int Linked, int Copied, int Failed);

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

    /// <summary>Pasta da cópia para um programa (sempre a mesma para o mesmo programa).</summary>
    public static string CloneDirFor(string exePath)
    {
        string srcDir = Path.GetDirectoryName(Path.GetFullPath(exePath))!;
        string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(srcDir.ToLowerInvariant())))[..8];
        string name = Path.GetFileNameWithoutExtension(exePath);
        return Path.Combine(CloneRootFor(exePath), $"{name}_{hash}");
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
    public static CloneResult CreateOrUpdate(string exePath, IProgress<string>? progress = null)
    {
        exePath = Path.GetFullPath(exePath);
        if (IsClone(exePath)) throw new InvalidOperationException("Esse programa já é uma cópia do HiddenApp.");

        string srcDir = Path.GetDirectoryName(exePath)!;
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (srcDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Programas da pasta do Windows não podem ser copiados.");

        string dstDir = CloneDirFor(exePath);
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

        return new CloneResult(cloneExe, linked, copied, failed);
    }

    /// <summary>
    /// Sugere argumentos para programas que só deixam abrir uma instância
    /// (sem isso eles só "repassam" a janela para o programa original).
    /// </summary>
    public static string SuggestArgs(string exePath)
    {
        string dataDir = Path.Combine(CloneDirFor(exePath), "_HiddenAppData");
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
        if (args.Contains("_HiddenAppData"))
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(cloneExe)!, "_HiddenAppData"));

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
