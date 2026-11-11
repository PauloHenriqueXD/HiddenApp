namespace HiddenApp;

/// <summary>
/// Executa uma macro numa thread separada: percorre os passos em loop — cliques e teclas — no modo
/// "segundo plano" (mensagens direto para a janela) ou "real" (mouse e teclado de verdade).
/// Para com <see cref="Stop"/> (tecla F8), quando o número de voltas acaba ou se a janela fecha.
/// </summary>
internal sealed class ClickRunner
{
    CancellationTokenSource? _cts;
    readonly Random _random = new();

    public bool IsRunning { get; private set; }
    public Macro? Current { get; private set; }

    /// <summary>Situação atual (ex.: "Volta 3 · ponto 2/4 · 128 cliques"). Chamado fora da thread da interface.</summary>
    public event Action<string>? Progress;
    /// <summary>A macro parou; o texto diz o motivo. Chamado fora da thread da interface.</summary>
    public event Action<string>? Stopped;

    public void Start(Macro macro, IntPtr window)
    {
        if (IsRunning) return;
        if (macro.Steps.Count == 0) throw new InvalidOperationException("A macro não tem nenhum passo.");
        int noKey = macro.Steps.FindIndex(s => s.Kind == StepKind.Tecla && s.Key == 0);
        if (noKey >= 0) throw new InvalidOperationException(L.F("O passo {0} não tem tecla escolhida.", noKey + 1));
        if (!Native.IsWindow(window)) throw new InvalidOperationException("A janela alvo não está aberta.");

        IsRunning = true;
        Current = macro;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        // Cópia dos dados: editar a macro enquanto ela roda não muda a execução atual
        var steps = macro.Steps.Select(s => s.Clone()).ToList();
        var thread = new Thread(() => Run(macro, steps, window, token)) { IsBackground = true, Name = "Macro" };
        thread.Start();
    }

    public void Stop() => _cts?.Cancel();

    void Run(Macro macro, List<MacroStep> steps, IntPtr window, CancellationToken token)
    {
        string reason = "Macro parada.";
        long actions = 0;
        try
        {
            // Coordenadas na mesma escala de DPI da janela alvo
            using var dpi = new Native.DpiScope(window);

            if (macro.Mode == ClickMode.Real)
            {
                if (Native.IsIconic(window)) Native.ShowWindow(window, Native.SW_RESTORE);
                Native.ForceForeground(window);
            }

            for (int loop = 1; macro.Loops == 0 || loop <= macro.Loops; loop++)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    for (int r = 0; r < Math.Max(1, step.Repeat); r++)
                    {
                        WaitUntilReady(macro, window, token);
                        Progress?.Invoke($"Volta {loop}{(macro.Loops > 0 ? $"/{macro.Loops}" : "")} · passo {i + 1}/{steps.Count} · {actions} ações");
                        if (step.Kind == StepKind.Tecla) DoKey(macro.Mode, step, window, token);
                        else DoClick(macro.Mode, step, window, token);
                        actions++;
                        if (r < step.Repeat - 1) Sleep(step.IntervalMs, macro.JitterPercent, token);
                    }
                    Sleep(step.DelayAfterMs, macro.JitterPercent, token);
                }
            }
            reason = $"Macro \"{macro.Name}\" concluída ({actions} ações).";
        }
        catch (OperationCanceledException) { reason = $"Macro \"{macro.Name}\" parada ({actions} ações)."; }
        catch (WindowGoneException) { reason = $"A janela alvo foi fechada. Macro \"{macro.Name}\" parada."; }
        catch (Exception ex) { reason = $"A macro parou por um erro: {ex.Message}"; }
        finally
        {
            IsRunning = false;
            Current = null;
            Stopped?.Invoke(reason);
        }
    }

    sealed class WindowGoneException : Exception { }

    /// <summary>
    /// Segurança: espera enquanto a janela estiver minimizada e, no modo real, enquanto ela não estiver
    /// em primeiro plano (para não clicar em outro programa que você esteja usando).
    /// </summary>
    void WaitUntilReady(Macro macro, IntPtr window, CancellationToken token)
    {
        bool warned = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!Native.IsWindow(window)) throw new WindowGoneException();

            string? problem = null;
            if (Native.IsIconic(window)) problem = "Pausado: a janela alvo está minimizada.";
            else if (macro.Mode == ClickMode.Real &&
                     Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOT) != window)
                problem = "Pausado: traga a janela alvo para a frente para continuar (modo clique real).";

            if (problem == null) return;
            if (!warned) { Progress?.Invoke(problem); warned = true; }
            if (token.WaitHandle.WaitOne(300)) token.ThrowIfCancellationRequested();
        }
    }

    void Sleep(int ms, int jitterPercent, CancellationToken token)
    {
        if (ms <= 0) { token.ThrowIfCancellationRequested(); return; }
        if (jitterPercent > 0)
        {
            double factor = 1 + (_random.NextDouble() * 2 - 1) * jitterPercent / 100.0;
            ms = Math.Max(1, (int)(ms * factor));
        }
        if (token.WaitHandle.WaitOne(ms)) token.ThrowIfCancellationRequested();
    }

    // =====================================================================
    //  Cliques
    // =====================================================================

    /// <summary>Converte a posição relativa do ponto para coordenadas da área interna da janela.</summary>
    static Native.POINT ClientPoint(MacroStep step, IntPtr window)
    {
        Native.GetClientRect(window, out var rc);
        int w = Math.Max(1, rc.right - rc.left), h = Math.Max(1, rc.bottom - rc.top);
        return new Native.POINT
        {
            X = (int)Math.Round(Math.Clamp(step.RelX, 0, 1) * (w - 1)),
            Y = (int)Math.Round(Math.Clamp(step.RelY, 0, 1) * (h - 1)),
        };
    }

    void DoClick(ClickMode mode, MacroStep step, IntPtr window, CancellationToken token)
    {
        var pt = ClientPoint(step, window);
        if (mode == ClickMode.Real) RealClick(step, window, pt, token);
        else BackgroundClick(step, window, pt, token);
    }

    // ---------- Modo real: move o mouse e clica de verdade ----------
    void RealClick(MacroStep step, IntPtr window, Native.POINT clientPt, CancellationToken token)
    {
        var screen = clientPt;
        Native.ClientToScreen(window, ref screen);
        Native.SetCursorPos(screen.X, screen.Y);
        PressButton(step, token);
    }

    /// <summary>Aperta e solta (ou segura) o botão do mouse "de verdade", na posição atual do cursor.</summary>
    void PressButton(MacroStep step, CancellationToken token)
    {
        (uint down, uint up) = step.Button switch
        {
            ClickButton.Direito => (Native.MOUSEEVENTF_RIGHTDOWN, Native.MOUSEEVENTF_RIGHTUP),
            ClickButton.Meio => (Native.MOUSEEVENTF_MIDDLEDOWN, Native.MOUSEEVENTF_MIDDLEUP),
            _ => (Native.MOUSEEVENTF_LEFTDOWN, Native.MOUSEEVENTF_LEFTUP),
        };

        switch (step.Action)
        {
            case ClickAction.Segurar:
                Native.SendMouseButton(down);
                try { Sleep(step.HoldMs, 0, token); }
                finally { Native.SendMouseButton(up); } // solta o botão mesmo se mandarem parar
                break;
            case ClickAction.CliqueDuplo:
                Native.SendMouseButton(down); Native.SendMouseButton(up);
                Thread.Sleep(40);
                Native.SendMouseButton(down); Native.SendMouseButton(up);
                break;
            default:
                Native.SendMouseButton(down); Native.SendMouseButton(up);
                break;
        }
    }

    // ---------- Modo segundo plano: mensagens direto para a janela ----------
    void BackgroundClick(MacroStep step, IntPtr window, Native.POINT clientPt, CancellationToken token)
    {
        // O clique vai para o controle que está exatamente embaixo do ponto (botão, campo, área do jogo...)
        IntPtr target = window;
        var pt = clientPt;
        for (int depth = 0; depth < 16; depth++)
        {
            IntPtr child = Native.ChildWindowFromPointEx(target, pt,
                Native.CWP_SKIPINVISIBLE | Native.CWP_SKIPDISABLED | Native.CWP_SKIPTRANSPARENT);
            if (child == IntPtr.Zero || child == target) break;
            Native.MapWindowPoints(target, child, ref pt, 1);
            target = child;
        }

        (uint down, uint up, uint dbl, int mk) = step.Button switch
        {
            ClickButton.Direito => (Native.WM_RBUTTONDOWN, Native.WM_RBUTTONUP, Native.WM_RBUTTONDBLCLK, Native.MK_RBUTTON),
            ClickButton.Meio => (Native.WM_MBUTTONDOWN, Native.WM_MBUTTONUP, Native.WM_MBUTTONDBLCLK, Native.MK_MBUTTON),
            _ => (Native.WM_LBUTTONDOWN, Native.WM_LBUTTONUP, Native.WM_LBUTTONDBLCLK, Native.MK_LBUTTON),
        };
        IntPtr lp = Native.MakeLParam(pt.X, pt.Y);

        Native.PostMessage(target, Native.WM_MOUSEMOVE, IntPtr.Zero, lp);
        switch (step.Action)
        {
            case ClickAction.Segurar:
                Native.PostMessage(target, down, (IntPtr)mk, lp);
                try { Sleep(step.HoldMs, 0, token); }
                finally { Native.PostMessage(target, up, IntPtr.Zero, lp); }
                break;
            case ClickAction.CliqueDuplo:
                Native.PostMessage(target, down, (IntPtr)mk, lp);
                Native.PostMessage(target, up, IntPtr.Zero, lp);
                Native.PostMessage(target, dbl, (IntPtr)mk, lp);
                Native.PostMessage(target, up, IntPtr.Zero, lp);
                break;
            default:
                Native.PostMessage(target, down, (IntPtr)mk, lp);
                Native.PostMessage(target, up, IntPtr.Zero, lp);
                break;
        }
    }

    // =====================================================================
    //  Teclas
    // =====================================================================

    static readonly (Keys Flag, Keys Key)[] Modifiers =
        { (Keys.Control, Keys.ControlKey), (Keys.Alt, Keys.Menu), (Keys.Shift, Keys.ShiftKey) };

    void DoKey(ClickMode mode, MacroStep step, IntPtr window, CancellationToken token)
    {
        Keys all = step.KeyValue;
        Keys key = all & Keys.KeyCode;
        var mods = Modifiers.Where(m => all.HasFlag(m.Flag)).Select(m => m.Key).ToList();
        bool hold = step.Action == ClickAction.Segurar;

        if (mode == ClickMode.Real)
        {
            // Modificadores apertados antes e soltos depois, na ordem inversa
            foreach (var m in mods) Native.SendKey(m, up: false);
            try
            {
                Native.SendKey(key, up: false);
                try { if (hold) Sleep(step.HoldMs, 0, token); else Thread.Sleep(30); }
                finally { Native.SendKey(key, up: true); } // solta a tecla mesmo se mandarem parar
            }
            finally { for (int i = mods.Count - 1; i >= 0; i--) Native.SendKey(mods[i], up: true); }
        }
        else
        {
            // Segundo plano: mensagens para o controle com foco dentro da janela. Alt usa as mensagens "SYS".
            IntPtr target = Native.FocusedChild(window);
            bool alt = all.HasFlag(Keys.Alt);
            uint down = alt ? Native.WM_SYSKEYDOWN : Native.WM_KEYDOWN, up = alt ? Native.WM_SYSKEYUP : Native.WM_KEYUP;
            foreach (var m in mods) Native.PostMessage(target, down, (IntPtr)(int)m, Native.KeyLParam(m, false));
            try
            {
                Native.PostMessage(target, down, (IntPtr)(int)key, Native.KeyLParam(key, false));
                try { if (hold) Sleep(step.HoldMs, 0, token); }
                finally { Native.PostMessage(target, up, (IntPtr)(int)key, Native.KeyLParam(key, true)); }
            }
            finally
            {
                for (int i = mods.Count - 1; i >= 0; i--)
                    Native.PostMessage(target, up, (IntPtr)(int)mods[i], Native.KeyLParam(mods[i], true));
            }
        }
    }

    // =====================================================================
    //  Encontrar a janela alvo
    // =====================================================================

    /// <summary>
    /// Acha a janela da macro: usa a preferida se ainda for do mesmo programa; senão procura entre
    /// as janelas abertas do programa, dando preferência ao mesmo título.
    /// </summary>
    public static IntPtr FindWindow(Macro macro, IntPtr preferred)
    {
        if (preferred != IntPtr.Zero && Native.IsWindow(preferred))
        {
            if (macro.TargetExe == null) return preferred;
            var info = WindowEnumerator.Describe(preferred);
            if (info?.ExePath != null && info.ExePath.Equals(macro.TargetExe, StringComparison.OrdinalIgnoreCase))
                return preferred;
        }
        if (macro.TargetExe == null) return IntPtr.Zero;

        var candidates = WindowEnumerator.GetOpenWindows()
            .Where(w => w.ExePath != null && w.ExePath.Equals(macro.TargetExe, StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Macro importada de outro PC: o programa pode estar instalado em outra pasta — procura pelo nome do .exe
        if (candidates.Count == 0)
        {
            string exeName = Path.GetFileName(macro.TargetExe);
            candidates = WindowEnumerator.GetOpenWindows()
                .Where(w => w.ExePath != null && Path.GetFileName(w.ExePath).Equals(exeName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        var sameTitle = candidates.FirstOrDefault(w => w.Title == macro.TargetTitle);
        return (sameTitle ?? candidates.FirstOrDefault())?.Handle ?? IntPtr.Zero;
    }

    /// <summary>
    /// Ponto relativo (0 a 1) da posição atual do mouse dentro da janela, e a janela embaixo dele.
    /// Retorna false se o mouse não estiver sobre uma janela válida.
    /// </summary>
    public static bool CaptureCursor(out IntPtr window, out double relX, out double relY)
    {
        relX = relY = 0;
        Native.GetCursorPos(out var screenPt);
        window = Native.GetAncestor(Native.WindowFromPoint(screenPt), Native.GA_ROOT);
        if (window == IntPtr.Zero) return false;

        using var dpi = new Native.DpiScope(window);
        Native.GetCursorPos(out var pt);           // relido na escala da janela alvo
        Native.ScreenToClient(window, ref pt);
        Native.GetClientRect(window, out var rc);
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 1 || h <= 1 || pt.X < 0 || pt.Y < 0 || pt.X >= w || pt.Y >= h) return false;
        relX = pt.X / (double)(w - 1);
        relY = pt.Y / (double)(h - 1);
        return true;
    }
}
