using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace HiddenApp;

internal static class Gfx
{
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        if (radius <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Estrela de 5 pontas centralizada em <paramref name="center"/>.</summary>
    public static void FillStar(Graphics g, Brush brush, PointF center, float outerRadius)
    {
        float inner = outerRadius * 0.45f;
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float r = i % 2 == 0 ? outerRadius : inner;
            pts[i] = new PointF(center.X + (float)(Math.Cos(angle) * r), center.Y + (float)(Math.Sin(angle) * r));
        }
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPolygon(brush, pts);
        g.SmoothingMode = old;
    }
}

public enum ButtonKind { Primary, Secondary, Danger, Success }

/// <summary>Cor de destaque do cartão: borda e faixa do título.</summary>
public enum CardAccent { Normal, Favorite, Danger }

/// <summary>Botão arredondado que segue o tema.</summary>
public class RoundButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;

    /// <summary>Se definido, o botão mostra só este ícone (o nome fica no balão de ajuda).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? IconGlyph { get; set; }

    static Font? _iconFont;
    static Font IconFont => _iconFont ??= Theme.IconFont(11f);

    bool _hover, _down;

    /// <summary>Botão quadrado só com ícone. O texto (nome) é usado por leitores de tela.</summary>
    public static RoundButton IconButton(string glyph, string name, ButtonKind kind) =>
        new(name, kind) { IconGlyph = glyph, Margin = new Padding(0, 0, Theme.S(6), Theme.S(8)) };

    public RoundButton(string text, ButtonKind kind = ButtonKind.Secondary)
    {
        Text = text;
        Kind = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AutoSize = true;
        Margin = new Padding(0, 0, Theme.S(8), Theme.S(8));
        Font = new Font("Segoe UI Semibold", 9.5f);
    }

    public override Size GetPreferredSize(Size proposed)
    {
        if (IconGlyph != null) return new Size(Theme.S(38), Theme.S(34));
        var s = TextRenderer.MeasureText(Text, Font);
        return new Size(s.Width + Theme.S(28), Math.Max(Theme.S(34), s.Height + Theme.S(14)));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        (Color fill, Color text, Color? border) = Kind switch
        {
            ButtonKind.Primary => (p.Accent, p.AccentText, (Color?)null),
            ButtonKind.Danger => (p.Danger, Color.White, null),
            ButtonKind.Success => (p.Success, p.SuccessText, null),
            _ => (p.Surface2, p.Text, p.Border),
        };

        if (!Enabled) { fill = Theme.Blend(fill, p.Surface, 0.55); text = Theme.Blend(text, p.Surface, 0.45); }
        else if (_down) fill = Theme.Blend(fill, p.IsDark ? Color.Black : Color.Black, 0.15);
        else if (_hover) fill = Theme.Blend(fill, p.IsDark ? Color.White : Color.Black, Kind == ButtonKind.Secondary ? 0.06 : 0.12);

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Gfx.RoundRect(r, Theme.S(8)))
        {
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
            if (border is Color bc)
            {
                using var pen = new Pen(_hover && Enabled ? p.Accent : bc);
                g.DrawPath(pen, path);
            }
        }
        TextRenderer.DrawText(g, IconGlyph ?? Text, IconGlyph != null ? IconFont : Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

/// <summary>Painel em forma de "cartão" arredondado com título.</summary>
public class Card : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Subtitle { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CardAccent Accent { get; set; } = CardAccent.Normal;
    static readonly Font TitleFont = new("Segoe UI Semibold", 11f);
    static readonly Font SubFont = new("Segoe UI", 9f);

    public Card(string title, string subtitle = "")
    {
        Title = title; Subtitle = subtitle;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Padding = new Padding(Theme.S(16), TitleFont.Height + Theme.S(26), Theme.S(16), Theme.S(14));
        Margin = new Padding(Theme.S(7));
        Dock = DockStyle.Fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Cartões de destaque ganham borda mais grossa na cor deles (amarelo = favoritos, vermelho = bloqueados)
        Color accentColor = Accent switch { CardAccent.Favorite => p.Favorite, CardAccent.Danger => p.Danger, _ => p.Accent };
        bool colored = Accent != CardAccent.Normal;
        float bw = colored ? Math.Max(2f, Theme.S(2)) : 1f;
        float inset = bw / 2f;
        using (var path = Gfx.RoundRect(new RectangleF(inset, inset, Width - 1 - bw, Height - 1 - bw), Theme.S(12)))
        {
            using var b = new SolidBrush(p.Surface);
            using var pen = new Pen(colored ? Theme.Blend(accentColor, p.Surface, 0.15) : p.Border, bw);
            g.FillPath(b, path);
            g.DrawPath(pen, path);
        }

        // Faixa de destaque à esquerda do título
        using (var accent = new SolidBrush(accentColor))
            g.FillRectangle(accent, Theme.S(16), Theme.S(14) + Theme.S(3), Theme.S(3), TitleFont.Height - Theme.S(6));

        int tx = Theme.S(25), ty = Theme.S(14);
        TextRenderer.DrawText(g, Title, TitleFont, new Point(tx, ty), p.Text);
        if (Subtitle.Length > 0)
        {
            int w = TextRenderer.MeasureText(Title, TitleFont).Width;
            TextRenderer.DrawText(g, Subtitle, SubFont,
                new Rectangle(tx + w + Theme.S(4), ty, Width - tx - w - Theme.S(20), TitleFont.Height), p.Muted,
                TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }
}

/// <summary>Interruptor liga/desliga com texto.</summary>
public class ToggleSwitch : Control
{
    bool _checked;
    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Muda o valor sem disparar o evento (para sincronizar com o menu da bandeja).</summary>
    public void SetSilently(bool value) { _checked = value; Invalidate(); }

    public ToggleSwitch(string text)
    {
        Text = text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        Height = Theme.S(30);
        Width = TextRenderer.MeasureText(text, Font).Width + Theme.S(56);
        Margin = new Padding(Theme.S(14), 0, 0, 0);
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float th = Theme.S(20), tw = Theme.S(38), k = Theme.S(14), kp = (th - k) / 2;
        var track = new RectangleF(1, (Height - th) / 2f, tw, th);
        using (var path = Gfx.RoundRect(track, th / 2))
        {
            using var b = new SolidBrush(_checked ? p.Accent : p.Surface2);
            using var pen = new Pen(_checked ? p.Accent : p.Border);
            g.FillPath(b, path);
            g.DrawPath(pen, path);
        }
        float knobX = _checked ? track.Right - k - kp : track.X + kp;
        using (var knob = new SolidBrush(_checked ? p.AccentText : p.Muted))
            g.FillEllipse(knob, knobX, track.Y + kp, k, k);

        int textX = Theme.S(46);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, Width - textX, Height), p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
    }
}

/// <summary>ListView com double-buffer (evita piscar ao desenhar as linhas customizadas).</summary>
public class ThemedListView : ListView
{
    public ThemedListView()
    {
        DoubleBuffered = true;
        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        MultiSelect = false;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        // Truque para aumentar a altura das linhas
        SmallImageList = new ImageList { ImageSize = new Size(1, Theme.S(34)) };
    }
}

/// <summary>Cores do menu da bandeja seguindo o tema.</summary>
public class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    public ThemedMenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Current.Text : Theme.Current.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.Muted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var p = Theme.Current;
        var r = new Rectangle(e.ImageRectangle.X - 1, e.ImageRectangle.Y - 1, e.ImageRectangle.Width + 2, e.ImageRectangle.Height + 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Gfx.RoundRect(r, 4))
        using (var b = new SolidBrush(p.Accent))
            e.Graphics.FillPath(b, path);
        TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, r, p.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    class MenuColors : ProfessionalColorTable
    {
        static Palette P => Theme.Current;
        public override Color ToolStripDropDownBackground => P.Surface;
        public override Color ImageMarginGradientBegin => P.Surface;
        public override Color ImageMarginGradientMiddle => P.Surface;
        public override Color ImageMarginGradientEnd => P.Surface;
        public override Color MenuBorder => P.Border;
        public override Color MenuItemBorder => P.Surface2;
        public override Color MenuItemSelected => P.Surface2;
        public override Color MenuItemSelectedGradientBegin => P.Surface2;
        public override Color MenuItemSelectedGradientEnd => P.Surface2;
        public override Color MenuItemPressedGradientBegin => P.Surface2;
        public override Color MenuItemPressedGradientEnd => P.Surface2;
        public override Color SeparatorDark => P.Border;
        public override Color SeparatorLight => P.Surface;
        public override Color CheckBackground => P.Accent;
        public override Color CheckSelectedBackground => P.Accent;
        public override Color CheckPressedBackground => P.Accent;
    }
}

/// <summary>Barra de progresso fina e arredondada que segue o tema.</summary>
public class ThinProgress : Control
{
    int _value;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public ThinProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = Theme.S(8);
        Margin = new Padding(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float r = Height / 2f;
        using (var track = Gfx.RoundRect(new RectangleF(0, 0, Width - 1, Height - 1), r))
        using (var b = new SolidBrush(p.Surface2))
            g.FillPath(b, track);
        float w = (Width - 1) * _value / 100f;
        if (w >= Height)
        {
            using var fill = Gfx.RoundRect(new RectangleF(0, 0, w, Height - 1), r);
            using var b = new SolidBrush(p.Accent);
            g.FillPath(b, fill);
        }
    }
}

/// <summary>
/// Balão de ajuda (tooltip) com título e explicação, desenhado com as cores do tema.
/// Use <see cref="Set"/> em vez de SetToolTip.
/// </summary>
public class ThemedToolTip : ToolTip
{
    static readonly Font TitleFont = new("Segoe UI Semibold", 9.5f);
    static readonly Font BodyFont = new("Segoe UI", 9f);
    readonly Dictionary<Control, (string Title, string Body)> _tips = new();

    public ThemedToolTip()
    {
        OwnerDraw = true;
        InitialDelay = 450;
        ReshowDelay = 150;
        AutoPopDelay = 30000;
        Popup += OnPopup;
        Draw += OnDraw;
    }

    public void Set(Control control, string title, string body)
    {
        _tips[control] = (title, body);
        SetToolTip(control, body);
    }

    static int MaxWidth => Theme.S(330);
    static int Pad => Theme.S(11);
    static readonly TextFormatFlags BodyFlags = TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding;

    void OnPopup(object? sender, PopupEventArgs e)
    {
        if (e.AssociatedControl == null || !_tips.TryGetValue(e.AssociatedControl, out var t)) return;
        var title = TextRenderer.MeasureText(t.Title, TitleFont);
        var body = TextRenderer.MeasureText(t.Body, BodyFont, new Size(MaxWidth, int.MaxValue), BodyFlags);
        e.ToolTipSize = new Size(Math.Max(title.Width, body.Width) + Pad * 2,
                                 title.Height + Theme.S(4) + body.Height + Pad * 2);
    }

    void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        using (var b = new SolidBrush(p.Surface2)) g.FillRectangle(b, e.Bounds);
        using (var pen = new Pen(p.Accent)) g.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
        if (e.AssociatedControl == null || !_tips.TryGetValue(e.AssociatedControl, out var t)) return;

        var titleSize = TextRenderer.MeasureText(t.Title, TitleFont);
        TextRenderer.DrawText(g, t.Title, TitleFont, new Point(Pad, Pad), p.Accent);
        var bodyRect = new Rectangle(Pad, Pad + titleSize.Height + Theme.S(4), e.Bounds.Width - Pad * 2, e.Bounds.Height);
        TextRenderer.DrawText(g, t.Body, BodyFont, bodyRect, p.Text, BodyFlags);
    }
}

/// <summary>Aba do topo (texto com sublinhado na cor de destaque quando selecionada).</summary>
public class TabButton : Control
{
    bool _selected, _hover;
    static readonly Font TabFont = new("Segoe UI Semibold", 10.5f);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public TabButton(string text)
    {
        Text = text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = TabFont;
        Width = TextRenderer.MeasureText(text, TabFont).Width + Theme.S(28);
        Height = Theme.S(42);
        Margin = new Padding(0, 0, Theme.S(4), 0);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Surface);
        if (_hover && !_selected)
        {
            using var hb = new SolidBrush(p.Surface2);
            g.FillRectangle(hb, 0, Theme.S(4), Width, Height - Theme.S(8));
        }
        Color text = _selected ? p.Text : _hover ? p.Text : p.Muted;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (_selected)
        {
            using var b = new SolidBrush(p.Accent);
            int h = Math.Max(2, Theme.S(3));
            g.FillRectangle(b, Theme.S(10), Height - h, Width - Theme.S(20), h);
        }
    }
}
