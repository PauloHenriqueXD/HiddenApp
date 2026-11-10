using System.Diagnostics;

namespace HiddenApp;

internal record WindowInfo(IntPtr Handle, string Title, uint Pid, string ProcessName, string? ExePath);

/// <summary>Lista as janelas "de verdade" abertas (as mesmas que aparecem no Alt+Tab).</summary>
internal static class WindowEnumerator
{
    public static List<WindowInfo> GetOpenWindows()
    {
        var result = new List<WindowInfo>();
        uint myPid = (uint)Environment.ProcessId;

        Native.EnumWindows((hWnd, _) =>
        {
            if (!IsAltTabWindow(hWnd)) return true;
            string title = Native.GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            var info = Describe(hWnd, title);
            if (info != null && info.Pid != myPid) result.Add(info);
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static WindowInfo? Describe(IntPtr hWnd, string? title = null)
    {
        title ??= Native.GetTitle(hWnd);
        Native.GetWindowThreadProcessId(hWnd, out uint pid);
        string? path = Native.GetProcessPath(pid);

        // Apps da Microsoft Store (UWP) ficam dentro do ApplicationFrameHost.exe;
        // o processo real que acessa a internet é o de uma janela filha.
        if (path != null && Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            uint hostPid = pid;
            Native.EnumChildWindows(hWnd, (child, _) =>
            {
                Native.GetWindowThreadProcessId(child, out uint childPid);
                if (childPid != hostPid) { pid = childPid; return false; }
                return true;
            }, IntPtr.Zero);
            path = Native.GetProcessPath(pid);
        }

        string name;
        try { name = Process.GetProcessById((int)pid).ProcessName; }
        catch { name = path != null ? Path.GetFileNameWithoutExtension(path) : "?"; }

        return new WindowInfo(hWnd, title, pid, name, path);
    }

    static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (!Native.IsWindowVisible(hWnd)) return false;
        if (Native.GetWindow(hWnd, Native.GW_OWNER) != IntPtr.Zero) return false;
        long ex = Native.GetWindowLongPtr(hWnd, Native.GWL_EXSTYLE).ToInt64();
        if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return false;
        // janelas "escondidas" do Windows 10/11 (apps suspensos, etc.)
        if (Native.DwmGetWindowAttribute(hWnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        return true;
    }

    /// <summary>PIDs de todos os processos rodando a partir de qualquer um dos .exe informados.</summary>
    public static HashSet<uint> GetPidsForExes(IEnumerable<string> exePaths)
    {
        var set = new HashSet<string>(exePaths, StringComparer.OrdinalIgnoreCase);
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = Native.GetProcessPath((uint)p.Id);
                if (path != null && set.Contains(path)) pids.Add((uint)p.Id);
            }
            catch { }
            finally { p.Dispose(); }
        }
        return pids;
    }

    /// <summary>Todos os PIDs que estão rodando a partir do mesmo .exe.</summary>
    public static HashSet<uint> GetPidsForExe(string exePath)
    {
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = Native.GetProcessPath((uint)p.Id);
                if (path != null && path.Equals(exePath, StringComparison.OrdinalIgnoreCase))
                    pids.Add((uint)p.Id);
            }
            catch { }
            finally { p.Dispose(); }
        }
        return pids;
    }
}
