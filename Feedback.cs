namespace HiddenApp;

/// <summary>
/// Comentários e sugestões. O app nunca envia nada sozinho: só abre, no navegador, um formulário já
/// preenchido com a versão do HiddenApp, do Windows e o idioma — a pessoa vê tudo antes de enviar.
///
/// Dois caminhos, à escolha de quem usa:
/// - Google Forms: sem conta, privado (as respostas vão para a planilha do dono do projeto).
/// - GitHub Issues: público, precisa de conta (modelos em .github/ISSUE_TEMPLATE).
/// </summary>
internal static class Feedback
{
    const string Repo = "PauloHenriqueXD/HiddenApp";

    /// <summary>
    /// Link "pré-preenchido" do Google Forms, gerado com as palavras VERSAO, WINDOWS e IDIOMA nos campos
    /// automáticos (elas são trocadas pelos valores reais). Vazio = opção do Google Forms escondida.
    /// </summary>
    public const string GoogleFormTemplate = "";

    public static bool HasGoogleForm => GoogleFormTemplate.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static string AppVersion => UpdateManager.CurrentVersion.ToString();

    public static string WindowsVersion
    {
        get
        {
            var v = Environment.OSVersion.Version;
            string name = v.Major == 10 && v.Build >= 22000 ? "Windows 11" : v.Major == 10 ? "Windows 10" : "Windows";
            return $"{name} ({v.Major}.{v.Minor}.{v.Build}, {(Environment.Is64BitOperatingSystem ? "64" : "32")} bits)";
        }
    }

    public static string Language => L.Current;

    /// <summary>Abre o Google Forms com versão, Windows e idioma preenchidos.</summary>
    public static void OpenGoogleForm()
    {
        if (!HasGoogleForm) return;
        string url = GoogleFormTemplate
            .Replace("=VERSAO", "=" + Uri.EscapeDataString(AppVersion))
            .Replace("=WINDOWS", "=" + Uri.EscapeDataString(WindowsVersion))
            .Replace("=IDIOMA", "=" + Uri.EscapeDataString(Language));
        UpdateManager.OpenUrl(url);
    }

    /// <summary>Abre uma issue nova no GitHub com o modelo escolhido ("sugestao" ou "problema").</summary>
    public static void OpenGitHub(string template)
    {
        string url = $"https://github.com/{Repo}/issues/new?template={template}.yml" +
                     $"&versao={Uri.EscapeDataString(AppVersion)}" +
                     $"&windows={Uri.EscapeDataString(WindowsVersion)}" +
                     $"&idioma={Uri.EscapeDataString(Language)}";
        UpdateManager.OpenUrl(url);
    }

    /// <summary>Itens do menu "Sugestões" (botão do topo e bandeja). Os textos são traduzidos depois.</summary>
    public static ToolStripItem[] MenuItems()
    {
        var items = new List<ToolStripItem>();
        if (HasGoogleForm)
        {
            items.Add(new ToolStripMenuItem("Enviar comentário ou sugestão (sem conta)", null, (_, _) => OpenGoogleForm()));
            items.Add(new ToolStripSeparator());
        }
        items.Add(new ToolStripMenuItem("Sugerir uma melhoria no GitHub", null, (_, _) => OpenGitHub("sugestao")));
        items.Add(new ToolStripMenuItem("Relatar um problema no GitHub", null, (_, _) => OpenGitHub("problema")));
        return items.ToArray();
    }
}
