using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace HiddenApp;

/// <summary>
/// "Iniciar com o Windows". Como o HiddenApp precisa de administrador, a chave "Run" do
/// registro não funciona (o Windows ignora programas que pedem elevação). Por isso é criada
/// uma Tarefa Agendada que roda ao fazer logon com privilégio máximo — sem prompt do UAC.
/// </summary>
internal static class StartupManager
{
    const string TaskName = "HiddenApp";
    public const string TrayArgument = "--tray";

    public static bool IsEnabled() => SchTasks($"/Query /TN \"{TaskName}\"").code == 0;

    public static void Enable()
    {
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        string user = WindowsIdentity.GetCurrent().Name;

        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Inicia o HiddenApp na bandeja do sistema ao entrar no Windows.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <Delay>PT5S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exe)}</Command>
                  <Arguments>{TrayArgument}</Arguments>
                  <WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exe))}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;

        string tmp = Path.Combine(Path.GetTempPath(), "HiddenApp_task.xml");
        File.WriteAllText(tmp, xml, Encoding.Unicode);
        try
        {
            var (code, output) = SchTasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F");
            if (code != 0) throw new InvalidOperationException($"Não foi possível criar a tarefa de inicialização:\n{output}");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    public static void Disable() => SchTasks($"/Delete /TN \"{TaskName}\" /F");

    /// <summary>Se o .exe mudou de lugar, atualiza a tarefa para apontar para o atual.</summary>
    public static void RefreshIfEnabled()
    {
        try { if (IsEnabled()) Enable(); } catch { }
    }

    static (int code, string output) SchTasks(string args)
    {
        var psi = new ProcessStartInfo("schtasks", args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output.Trim());
    }
}
