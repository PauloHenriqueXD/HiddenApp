using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HiddenApp;

/// <summary>
/// Traduções da interface. O código continua com os textos em português; ao exibir, cada texto é
/// procurado no dicionário do idioma escolhido (Lang/en.json, Lang/zh-CN.json, embutidos no .exe).
///
/// Textos com partes variáveis usam modelos: a chave "Internet bloqueada para {0}." traduz também
/// "Internet bloqueada para Google Chrome.". As partes variáveis também são traduzidas, se tiverem tradução.
/// </summary>
public static class L
{
    public const string Auto = "auto";

    /// <summary>Idiomas disponíveis: código → nome no próprio idioma.</summary>
    public static readonly (string Code, string Name)[] Languages =
    {
        ("pt-BR", "Português (Brasil)"),
        ("en", "English"),
        ("zh-CN", "简体中文"),
    };

    static Dictionary<string, string>? _exact;
    static List<(Regex Pattern, string Target)> _templates = new();
    static readonly Dictionary<string, string> _cache = new();
    static readonly HashSet<Control> _attached = new();

    /// <summary>Idioma em uso (pt-BR, en ou zh-CN).</summary>
    public static string Current { get; private set; } = "pt-BR";

    /// <summary>Escolhe o idioma: "auto" usa o idioma do Windows (português, chinês ou, nos demais, inglês).</summary>
    public static void Init(string? setting)
    {
        string code = string.IsNullOrEmpty(setting) || setting == Auto ? FromWindows() : setting;
        if (!Languages.Any(l => l.Code == code)) code = "pt-BR";
        Current = code;
        _cache.Clear();
        _templates = new();
        _exact = null;
        if (code == "pt-BR") return;

        try
        {
            using var stream = typeof(L).Assembly.GetManifestResourceStream($"lang.{code}.json");
            if (stream == null) return;
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
            _exact = new Dictionary<string, string>(map.Count);
            foreach (var (pt, tr) in map)
            {
                if (string.IsNullOrEmpty(tr)) continue;
                if (Regex.IsMatch(pt, @"\{\d+\}")) _templates.Add((BuildPattern(pt), tr));
                else _exact[pt] = tr;
            }
            // Modelos mais longos primeiro (mais específicos)
            _templates = _templates.OrderByDescending(t => t.Pattern.ToString().Length).ToList();
        }
        catch { _exact = null; }
    }

    static string FromWindows()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return lang switch { "pt" => "pt-BR", "zh" => "zh-CN", _ => "en" };
    }

    static Regex BuildPattern(string template)
    {
        var parts = Regex.Split(template, @"(\{\d+\})");
        var sb = new System.Text.StringBuilder("^");
        foreach (var p in parts)
        {
            var m = Regex.Match(p, @"^\{(\d+)\}$");
            sb.Append(m.Success ? $"(?<p{m.Groups[1].Value}>.+?)" : Regex.Escape(p));
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    /// <summary>Traduz um texto (exato ou por modelo). Sem tradução, devolve o próprio texto.</summary>
    public static string T(string? text)
    {
        if (string.IsNullOrEmpty(text) || _exact == null) return text ?? "";
        if (_exact.TryGetValue(text, out var exact)) return exact;
        if (_cache.TryGetValue(text, out var cached)) return cached;

        string result = text;
        foreach (var (pattern, target) in _templates)
        {
            var m = pattern.Match(text);
            if (!m.Success) continue;
            result = Regex.Replace(target, @"\{(\d+)\}", r =>
            {
                var g = m.Groups["p" + r.Groups[1].Value];
                return g.Success ? T(g.Value) : r.Value; // partes variáveis também são traduzidas
            });
            break;
        }
        if (_cache.Count > 2000) _cache.Clear();
        _cache[text] = result;
        return result;
    }

    /// <summary>Traduz um modelo e preenche as partes variáveis ({0}, {1}...).</summary>
    public static string F(string template, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(template), args.Select(a => a is string s ? T(s) : a).ToArray());

    // =====================================================================
    //  Aplicação automática na interface
    // =====================================================================

    /// <summary>
    /// Traduz os textos de um controle e de todos os filhos — e continua traduzindo quando os textos
    /// mudarem depois (status, botões que trocam de nome, etc.). Campos de digitação não são tocados.
    /// </summary>
    public static void Apply(Control root)
    {
        if (_exact == null) return;
        Attach(root);
    }

    static void Attach(Control c)
    {
        if (!_attached.Add(c)) return;

        switch (c)
        {
            case TextBox tb:
                tb.PlaceholderText = T(tb.PlaceholderText);
                break;
            case NumericUpDown or ListBox:
                break;
            case ComboBox cb:
                for (int i = 0; i < cb.Items.Count; i++)
                    if (cb.Items[i] is string s) cb.Items[i] = T(s);
                break;
            case ListView lv:
                foreach (ColumnHeader col in lv.Columns) col.Text = T(col.Text);
                TranslateText(c);
                break;
            default:
                TranslateText(c);
                c.TextChanged += (_, _) => TranslateText(c);
                break;
        }

        c.ControlAdded += (_, e) => { if (e.Control != null) Attach(e.Control); };
        c.Disposed += (_, _) => _attached.Remove(c);
        foreach (Control child in c.Controls) Attach(child);
    }

    static void TranslateText(Control c)
    {
        string t = T(c.Text);
        if (t != c.Text) c.Text = t;
    }

    /// <summary>Traduz os itens de um menu (chamar depois de montar o menu, antes de mostrar).</summary>
    public static void Apply(ToolStripItemCollection items)
    {
        if (_exact == null) return;
        foreach (ToolStripItem item in items)
        {
            item.Text = T(item.Text);
            if (!string.IsNullOrEmpty(item.ToolTipText)) item.ToolTipText = T(item.ToolTipText);
            if (item is ToolStripMenuItem mi && mi.DropDownItems.Count > 0) Apply(mi.DropDownItems);
        }
    }
}

/// <summary>MessageBox que traduz o texto e o título.</summary>
public static class Msg
{
    public static DialogResult Show(string text) => MessageBox.Show(L.T(text));
    public static DialogResult Show(string text, string caption) => MessageBox.Show(L.T(text), L.T(caption));
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        MessageBox.Show(L.T(text), L.T(caption), buttons, icon);
    public static DialogResult Show(IWin32Window owner, string text, string caption) =>
        MessageBox.Show(owner, L.T(text), L.T(caption));
    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        MessageBox.Show(owner, L.T(text), L.T(caption), buttons, icon);
}
