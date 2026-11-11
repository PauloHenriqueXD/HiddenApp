using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiddenApp;

public enum ClickButton { Esquerdo, Direito, Meio }

public enum ClickAction { Clique, CliqueDuplo, Segurar }

public enum ClickMode
{
    /// <summary>Manda o clique direto para o controle embaixo do ponto, sem mexer no mouse (não funciona em todo programa).</summary>
    SegundoPlano,
    /// <summary>Move o mouse de verdade e clica (funciona em quase tudo, mas a janela precisa estar na frente).</summary>
    Real,
    /// <summary>Outra forma de clique sem mexer no mouse: ativa a janela por mensagem e envia o clique direto (SendMessage).</summary>
    SegundoPlanoAlternativo,
    /// <summary>Traz a janela para a frente por um instante, clica de verdade e devolve o mouse e a janela anterior.</summary>
    RealComRetorno,
}

/// <summary>Um ponto da macro: onde clicar e como.</summary>
public class MacroStep
{
    /// <summary>Posição relativa à área interna da janela (0 a 1), para continuar certa se a janela mudar de tamanho.</summary>
    public double RelX { get; set; } = 0.5;
    public double RelY { get; set; } = 0.5;

    public ClickButton Button { get; set; } = ClickButton.Esquerdo;
    public ClickAction Action { get; set; } = ClickAction.Clique;

    /// <summary>Quanto tempo segurar o botão (só para a ação "Segurar").</summary>
    public int HoldMs { get; set; } = 100;

    /// <summary>Quantos cliques seguidos neste mesmo ponto.</summary>
    public int Repeat { get; set; } = 1;

    /// <summary>Intervalo entre os cliques repetidos neste ponto.</summary>
    public int IntervalMs { get; set; } = 100;

    /// <summary>Espera depois de terminar este ponto, antes de ir para o próximo.</summary>
    public int DelayAfterMs { get; set; } = 100;

    public MacroStep Clone() => (MacroStep)MemberwiseClone();

    public string Describe()
    {
        string action = Action switch
        {
            ClickAction.CliqueDuplo => "Duplo",
            ClickAction.Segurar => $"Segurar {Ms(HoldMs)}",
            _ => "Clique",
        };
        string times = Repeat > 1 ? $"{Repeat}x a cada {Ms(IntervalMs)}" : "1x";
        return $"{Button} · {action} · {times} · depois espera {Ms(DelayAfterMs)}";
    }

    public static string Ms(int ms) => ms >= 1000 && ms % 100 == 0 ? $"{ms / 1000.0:0.#} s" : $"{ms} ms";
}

/// <summary>Uma macro de auto clique: janela alvo + pontos que rodam em loop.</summary>
public class Macro
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nova macro";

    /// <summary>Programa alvo (caminho do .exe) e título da janela, para reencontrá-la depois.</summary>
    public string? TargetExe { get; set; }
    public string? TargetTitle { get; set; }

    public ClickMode Mode { get; set; } = ClickMode.SegundoPlano;

    /// <summary>Quantas vezes repetir a sequência de pontos (0 = sem parar).</summary>
    public int Loops { get; set; }

    /// <summary>Variação aleatória nos intervalos, em % (deixa os cliques menos "robóticos").</summary>
    public int JitterPercent { get; set; }

    public List<MacroStep> Steps { get; set; } = new();

    public Macro Clone()
    {
        var copy = (Macro)MemberwiseClone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Steps = Steps.Select(s => s.Clone()).ToList();
        return copy;
    }

    [JsonIgnore]
    public string TargetName => TargetExe == null ? "nenhuma janela escolhida" : Path.GetFileNameWithoutExtension(TargetExe);
}

/// <summary>Macros salvas em %AppData%\HiddenApp\macros.json.</summary>
internal static class MacroStore
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HiddenApp", "macros.json");

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<Macro> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<Macro>>(File.ReadAllText(FilePath), Options) ?? new();
        }
        catch { }
        return new();
    }

    // ---------- Compartilhar macros (arquivo .hiddenmacro) ----------
    public const string FileExtension = ".hiddenmacro";
    public const string FileFilter = "Macro do HiddenApp (*.hiddenmacro)|*.hiddenmacro|Todos os arquivos (*.*)|*.*";

    /// <summary>Conteúdo do arquivo exportado: identifica o formato para não aceitar qualquer JSON.</summary>
    class MacroFile
    {
        public string Formato { get; set; } = "HiddenApp.Macro";
        public int Versao { get; set; } = 1;
        public List<Macro> Macros { get; set; } = new();
    }

    public static void Export(IEnumerable<Macro> macros, string path)
    {
        var file = new MacroFile { Macros = macros.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(file, Options));
    }

    /// <summary>Lê um arquivo exportado. As macros recebem um Id novo e valores fora do normal são corrigidos.</summary>
    public static List<Macro> Import(string path)
    {
        MacroFile? file;
        try { file = JsonSerializer.Deserialize<MacroFile>(File.ReadAllText(path), Options); }
        catch (Exception ex) { throw new InvalidOperationException("O arquivo não é uma macro válida do HiddenApp.", ex); }
        if (file == null || file.Formato != "HiddenApp.Macro" || file.Macros.Count == 0)
            throw new InvalidOperationException("O arquivo não é uma macro válida do HiddenApp.");
        if (file.Versao > 1)
            throw new InvalidOperationException("Esta macro foi criada numa versão mais nova do HiddenApp. Atualize o programa para importá-la.");

        foreach (var m in file.Macros) Sanitize(m);
        return file.Macros;
    }

    /// <summary>Garante valores dentro dos limites (arquivos podem ter sido editados à mão).</summary>
    static void Sanitize(Macro m)
    {
        m.Id = Guid.NewGuid().ToString("N");
        m.Name = string.IsNullOrWhiteSpace(m.Name) ? "Macro importada" : m.Name.Trim()[..Math.Min(m.Name.Trim().Length, 80)];
        if (!Enum.IsDefined(m.Mode)) m.Mode = ClickMode.SegundoPlano;
        m.Loops = Math.Clamp(m.Loops, 0, 1_000_000);
        m.JitterPercent = Math.Clamp(m.JitterPercent, 0, 50);
        m.Steps ??= new();
        if (m.Steps.Count > 500) m.Steps = m.Steps.Take(500).ToList();
        foreach (var s in m.Steps)
        {
            s.RelX = double.IsFinite(s.RelX) ? Math.Clamp(s.RelX, 0, 1) : 0.5;
            s.RelY = double.IsFinite(s.RelY) ? Math.Clamp(s.RelY, 0, 1) : 0.5;
            if (!Enum.IsDefined(s.Button)) s.Button = ClickButton.Esquerdo;
            if (!Enum.IsDefined(s.Action)) s.Action = ClickAction.Clique;
            s.HoldMs = Math.Clamp(s.HoldMs, 10, 3_600_000);
            s.Repeat = Math.Clamp(s.Repeat, 1, 1_000_000);
            s.IntervalMs = Math.Clamp(s.IntervalMs, 1, 3_600_000);
            s.DelayAfterMs = Math.Clamp(s.DelayAfterMs, 0, 3_600_000);
        }
    }

    public static void Save(List<Macro> macros)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(macros, Options));
        }
        catch { }
    }
}
