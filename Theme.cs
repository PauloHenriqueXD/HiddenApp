using System.Runtime.InteropServices;

namespace HiddenApp;

/// <summary>Paleta de cores de um tema.</summary>
public record Palette(
    bool IsDark,
    Color Back,       // fundo da janela
    Color Surface,    // cartões
    Color Surface2,   // campos, cabeçalhos, hover
    Color Border,
    Color Text,
    Color Muted,      // textos secundários
    Color Accent,     // ciano do ícone
    Color AccentText, // texto sobre o Accent
    Color Danger,     // vermelho do ícone
    Color Success,
    Color SuccessText, // texto sobre o Success (botão verde)
    Color Favorite,    // amarelo da estrela / borda dos favoritos
    Color Selection);

/// <summary>Tema atual (escuro/claro) e ajustes visuais do Windows (barra de título, scrollbars).</summary>
public static class Theme
{
    public static readonly Palette Dark = new(true,
        Back: Hex("#0A0F1E"), Surface: Hex("#121A2E"), Surface2: Hex("#1B2541"), Border: Hex("#26335A"),
        Text: Hex("#E8ECF5"), Muted: Hex("#8A96B3"), Accent: Hex("#22C7EE"), AccentText: Hex("#03121C"),
        Danger: Hex("#FF4D6D"), Success: Hex("#34D399"), SuccessText: Hex("#03140D"), Favorite: Hex("#FBBF24"), Selection: Hex("#1D3A63"));

    public static readonly Palette Light = new(false,
        Back: Hex("#EEF1F7"), Surface: Hex("#FFFFFF"), Surface2: Hex("#F0F3F9"), Border: Hex("#D5DBE7"),
        Text: Hex("#162033"), Muted: Hex("#5B6782"), Accent: Hex("#0A8BD0"), AccentText: Hex("#FFFFFF"),
        Danger: Hex("#D7354F"), Success: Hex("#159A66"), SuccessText: Hex("#FFFFFF"), Favorite: Hex("#D69E00"), Selection: Hex("#D3EAF9"));

    public static Palette Current { get; private set; } = Dark;
    public static event Action? Changed;

    public static void Set(bool dark)
    {
        Current = dark ? Dark : Light;
        Changed?.Invoke();
    }

    // ---------- Escala da tela (125%, 150%...) ----------
    public static readonly float Dpi = GetDpi();
    static float GetDpi() { using var g = Graphics.FromHwnd(IntPtr.Zero); return g.DpiX / 96f; }
    /// <summary>Converte um tamanho pensado para 100% de zoom para a escala atual da tela.</summary>
    public static int S(int px) => (int)Math.Round(px * Dpi);

    static Color Hex(string h) => ColorTranslator.FromHtml(h);

    public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    // ---------- Integração com o Windows ----------
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string? app, string? idList);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_CAPTION_COLOR = 35, DWMWA_TEXT_COLOR = 36;

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Barra de título escura/clara (Win10) e com a cor do tema (Win11).</summary>
    public static void ApplyTitleBar(Form f)
    {
        if (!f.IsHandleCreated) return;
        int dark = Current.IsDark ? 1 : 0;
        DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int caption = ColorRef(Current.Surface), text = ColorRef(Current.Text);
        DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
    }

    /// <summary>Barras de rolagem escuras/claras em listas.</summary>
    public static void ApplyScrollbars(Control c)
    {
        if (c.IsHandleCreated) SetWindowTheme(c.Handle, Current.IsDark ? "DarkMode_Explorer" : "Explorer", null);
    }

    // ---------- Recursos (ícone e logo embutidos no .exe) ----------
    static Icon? _icon;
    static Image? _logo;

    public static Icon AppIcon => _icon ??= LoadIcon();
    public static Image Logo => _logo ??= Image.FromStream(Resource("logo.png"));

    // ---------- Ícones (fonte de ícones do Windows: Fluent no Win11, MDL2 no Win10) ----------
    static string? _iconFamily;
    public static Font IconFont(float size)
    {
        _iconFamily ??= FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        return new Font(_iconFamily, size);
    }

    /// <summary>Códigos dos ícones usados nos botões (iguais nas duas fontes de ícones).</summary>
    public static class Glyph
    {
        public const string Add = "\uE710", Copy = "\uE8C8", Delete = "\uE74D", Import = "\uE896", Export = "\uE898",
                            Up = "\uE74A", Down = "\uE74B", Pin = "\uE707", Cancel = "\uE711", Globe = "\uE774", Feedback = "\uED15",
                            Keyboard = "\uE765", Lock = "\uE72E", Unlock = "\uE785", Search = "\uE721", NewWindow = "\uE8A7",
                            Refresh = "\uE72C", SelectAll = "\uE8B3", Play = "\uE768", Remove = "\uE738",
                            Star = "\uE734", StarFill = "\uE735";
    }

    public static Icon TrayIcon(int size) => new(Resource("HiddenApp.ico"), new Size(size, size));

    static Icon LoadIcon() => new(Resource("HiddenApp.ico"));

    static Stream Resource(string name) =>
        typeof(Theme).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Recurso não encontrado: {name}");
}
