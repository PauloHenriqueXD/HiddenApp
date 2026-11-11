using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiddenApp;

public enum ClickButton { Esquerdo, Direito, Meio }

public enum ClickAction { Clique, CliqueDuplo, Segurar }

public enum ClickMode
{
    /// <summary>Manda o clique direto para a janela, sem mexer no mouse (não funciona em todo programa).</summary>
    SegundoPlano,
    /// <summary>Move o mouse de verdade e clica (funciona em quase tudo, mas ocupa o mouse).</summary>
    Real,
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
    public int HoldMs { get; set; } = 1000;

    /// <summary>Quantos cliques seguidos neste mesmo ponto.</summary>
    public int Repeat { get; set; } = 1;

    /// <summary>Intervalo entre os cliques repetidos neste ponto.</summary>
    public int IntervalMs { get; set; } = 100;

    /// <summary>Espera depois de terminar este ponto, antes de ir para o próximo.</summary>
    public int DelayAfterMs { get; set; } = 500;

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
