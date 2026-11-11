using System.Runtime.InteropServices;
using System.Text;

namespace HiddenApp;

/// <summary>Chamadas diretas à API do Windows (user32, kernel32, iphlpapi).</summary>
internal static class Native
{
    // ---------- Janelas ----------
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);

    // ---------- Miniatura ao vivo (mesma API do Alt+Tab) ----------
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE size);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props);

    [StructLayout(LayoutKind.Sequential)] public struct PSIZE { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
    }

    public const uint DWM_TNP_RECTDESTINATION = 0x1, DWM_TNP_OPACITY = 0x4,
                      DWM_TNP_VISIBLE = 0x8, DWM_TNP_SOURCECLIENTAREAONLY = 0x10;

    public const uint GW_OWNER = 4;
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const int DWMWA_CLOAKED = 14;

    public static string GetTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // ---------- Processos ----------
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder name, ref int size);

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>Caminho completo do .exe de um processo (funciona até para processos elevados).</summary>
    public static string? GetProcessPath(uint pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    // ---------- Hard links ----------
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateHardLink(string newFile, string existingFile, IntPtr reserved);

    // ---------- Abrir programa SEM privilégio de admin ----------
    // Usa o "token" do Explorer (que roda como usuário comum) para criar o processo.
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attrs, int impersonationLevel, int tokenType, out IntPtr newToken);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessWithTokenW(IntPtr token, uint logonFlags, string? appName, StringBuilder cmdLine,
        uint creationFlags, IntPtr env, string? curDir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint TOKEN_DUPLICATE = 0x0002;
    const uint TOKEN_ALL_FOR_PRIMARY = 0x018B; // QUERY | ASSIGN_PRIMARY | DUPLICATE | ADJUST_DEFAULT | ADJUST_SESSIONID

    /// <summary>Abre um programa como usuário comum (não elevado). Retorna o PID ou 0 se falhar.</summary>
    public static int StartUnelevated(string exePath, string args, string workingDir)
    {
        IntPtr shell = GetShellWindow();
        if (shell == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(shell, out uint explorerPid);

        IntPtr hProc = OpenProcess(PROCESS_QUERY_INFORMATION, false, explorerPid);
        if (hProc == IntPtr.Zero) return 0;
        IntPtr tok = IntPtr.Zero, primary = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(hProc, TOKEN_DUPLICATE, out tok)) return 0;
            if (!DuplicateTokenEx(tok, TOKEN_ALL_FOR_PRIMARY, IntPtr.Zero, 2 /*Impersonation*/, 1 /*Primary*/, out primary)) return 0;

            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
            var cmd = new StringBuilder($"\"{exePath}\" {args}".TrimEnd());
            if (!CreateProcessWithTokenW(primary, 0, exePath, cmd, 0, IntPtr.Zero, workingDir, ref si, out var pi)) return 0;
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }
        finally
        {
            if (primary != IntPtr.Zero) CloseHandle(primary);
            if (tok != IntPtr.Zero) CloseHandle(tok);
            CloseHandle(hProc);
        }
    }

    // ---------- Auto clique: mouse, coordenadas e mensagens ----------
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT pt, uint flags);
    [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT pt, uint count);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public const uint GA_ROOT = 2;
    public const uint CWP_SKIPINVISIBLE = 0x1, CWP_SKIPDISABLED = 0x2, CWP_SKIPTRANSPARENT = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }

    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x02, MOUSEEVENTF_LEFTUP = 0x04,
                      MOUSEEVENTF_RIGHTDOWN = 0x08, MOUSEEVENTF_RIGHTUP = 0x10,
                      MOUSEEVENTF_MIDDLEDOWN = 0x20, MOUSEEVENTF_MIDDLEUP = 0x40;

    /// <summary>Envia um evento de botão do mouse "de verdade" (na posição atual do cursor).</summary>
    public static void SendMouseButton(uint flag)
    {
        var input = new[] { new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flag } } };
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }

    public const uint WM_MOUSEMOVE = 0x0200,
                      WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
                      WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206,
                      WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_MBUTTONDBLCLK = 0x0209;
    public const int MK_LBUTTON = 0x1, MK_RBUTTON = 0x2, MK_MBUTTON = 0x10;

    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// Traz uma janela para a frente. O Windows costuma bloquear isso para programas em segundo plano;
    /// "anexar" a entrada à janela que está na frente libera a troca.
    /// </summary>
    public static void ForceForeground(IntPtr hWnd)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == hWnd) return;
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        finally { if (attached) AttachThreadInput(me, fgThread, false); }
    }

    public static IntPtr MakeLParam(int x, int y) => (IntPtr)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    // ---------- DPI: usar o mesmo "sistema de coordenadas" da janela alvo ----------
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hWnd);

    /// <summary>
    /// Enquanto existir, a thread atual usa a mesma escala de DPI da janela alvo, para que
    /// posições de mouse e da janela fiquem nas mesmas coordenadas que ela entende.
    /// </summary>
    public readonly struct DpiScope : IDisposable
    {
        readonly IntPtr _old;
        public DpiScope(IntPtr targetWindow)
        {
            IntPtr ctx = targetWindow != IntPtr.Zero ? GetWindowDpiAwarenessContext(targetWindow) : IntPtr.Zero;
            _old = ctx != IntPtr.Zero ? SetThreadDpiAwarenessContext(ctx) : IntPtr.Zero;
        }
        public void Dispose() { if (_old != IntPtr.Zero) SetThreadDpiAwarenessContext(_old); }
    }

    // ---------- Teclas de atalho globais ----------
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

    // ---------- Conexões TCP ----------
    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int af, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll")]
    public static extern uint SetTcpEntry(ref MIB_TCPROW row);

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPROW_OWNER_PID
    {
        public uint state, localAddr, localPort, remoteAddr, remotePort, owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_TCPROW
    {
        public uint state, localAddr, localPort, remoteAddr, remotePort;
    }

    const int AF_INET = 2;
    const int TCP_TABLE_OWNER_PID_ALL = 5;
    const uint MIB_TCP_STATE_DELETE_TCB = 12;

    /// <summary>
    /// Derruba as conexões TCP (IPv4) já abertas pelos PIDs informados.
    /// A regra de firewall impede conexões novas; isto corta as que já existiam.
    /// </summary>
    public static int KillTcpConnections(HashSet<uint> pids)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        IntPtr buf = Marshal.AllocHGlobal(size);
        int killed = 0;
        try
        {
            if (GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != 0) return 0;
            int count = Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            IntPtr ptr = buf + 4;
            for (int i = 0; i < count; i++, ptr += rowSize)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(ptr);
                if (!pids.Contains(r.owningPid)) continue;
                if (r.state == 2) continue; // LISTEN: não é conexão com a internet
                var row = new MIB_TCPROW
                {
                    state = MIB_TCP_STATE_DELETE_TCB,
                    localAddr = r.localAddr, localPort = r.localPort,
                    remoteAddr = r.remoteAddr, remotePort = r.remotePort
                };
                if (SetTcpEntry(ref row) == 0) killed++;
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return killed;
    }
}
