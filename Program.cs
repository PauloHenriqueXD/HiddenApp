namespace HiddenApp;

internal static class Program
{
    const string MutexName = "HiddenApp_SingleInstance";
    const string ShowEventName = "HiddenApp_ShowWindow";
    /// <summary>Reinício pedido pelo próprio app (ex.: troca de idioma): espera a instância anterior fechar.</summary>
    public const string RestartArgument = "--restart";

    /// <summary>Marcado por "Desinstalar" para apagar %AppData%\\HiddenApp ao fechar.</summary>
    public static bool WipeDataOnExit;

    [STAThread]
    static void Main(string[] args)
    {
        bool justUpdated = args.Any(a => a.Equals(UpdateManager.UpdatedArgument, StringComparison.OrdinalIgnoreCase));
        bool restarting = args.Any(a => a.Equals(RestartArgument, StringComparison.OrdinalIgnoreCase));
        L.Init(AppSettings.Load().Language);

        // Só uma cópia do HiddenApp por vez: se já estiver aberto (ex.: na bandeja), só mostra a janela.
        // Logo após uma atualização, espera a versão antiga terminar de fechar.
        var mutex = new Mutex(false, MutexName);
        bool owned;
        try { owned = mutex.WaitOne(justUpdated || restarting ? TimeSpan.FromSeconds(20) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { owned = true; } // a versão antiga fechou sem liberar
        if (!owned)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return;
        }

        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        ApplicationConfiguration.Initialize();
        bool startInTray = !justUpdated && !restarting && args.Any(a => a.Equals(StartupManager.TrayArgument, StringComparison.OrdinalIgnoreCase));
        var form = new MainForm(startInTray, justUpdated);

        new Thread(() =>
        {
            try
            {
                while (showEvent.WaitOne())
                    if (form.IsHandleCreated) form.BeginInvoke(form.ShowFromTray);
            }
            catch { }
        }) { IsBackground = true }.Start();

        Application.Run(form);

        // "Desinstalar" com a opção de apagar os dados: só depois que tudo fechou (nada salva de novo)
        if (WipeDataOnExit)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HiddenApp");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
        }
        GC.KeepAlive(mutex);
    }
}
