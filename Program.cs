namespace HiddenApp;

internal static class Program
{
    const string MutexName = "HiddenApp_SingleInstance";
    const string ShowEventName = "HiddenApp_ShowWindow";

    [STAThread]
    static void Main(string[] args)
    {
        // Só uma cópia do HiddenApp por vez: se já estiver aberto (ex.: na bandeja), só mostra a janela
        using var mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return;
        }

        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        ApplicationConfiguration.Initialize();
        bool startInTray = args.Any(a => a.Equals(StartupManager.TrayArgument, StringComparison.OrdinalIgnoreCase));
        var form = new MainForm(startInTray);

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
    }
}
