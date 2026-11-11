using System.ComponentModel;

namespace HiddenApp;

/// <summary>
/// Painel que mostra uma miniatura AO VIVO de outra janela, usando o DWM
/// (a mesma tecnologia das prévias do Alt+Tab e da barra de tarefas).
/// </summary>
public class WindowPreview : Panel
{
    IntPtr _thumb = IntPtr.Zero;
    IntPtr _source = IntPtr.Zero;
    string _message = "Selecione uma janela na lista para ver a prévia.";
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 200 };
    Rectangle _destInPanel = Rectangle.Empty;
    bool _pickMode;

    /// <summary>Mostra só a área interna da janela (sem barra de título), usado pelo auto clique.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ClientAreaOnly { get; set; }

    /// <summary>Texto mostrado quando nenhuma janela foi escolhida.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyMessage { get; set; } = "Selecione uma janela na lista para ver a prévia.";

    /// <summary>Modo "marcar ponto": cursor em cruz e clique na prévia dispara <see cref="PointPicked"/>.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PickMode
    {
        get => _pickMode;
        set { _pickMode = value; Cursor = value ? Cursors.Cross : Cursors.Default; Invalidate(); }
    }

    /// <summary>Ponto clicado na prévia, relativo à área interna da janela (0 a 1).</summary>
    public event Action<double, double>? PointPicked;

    public WindowPreview()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        // Reajusta se a janela de origem mudar de tamanho, for minimizada ou fechada
        _timer.Tick += (_, _) => UpdateThumbnail();
        _timer.Start();
    }

    /// <summary>Define qual janela mostrar (IntPtr.Zero para limpar).</summary>
    public void SetSource(IntPtr hWnd)
    {
        if (hWnd == _source) return;
        Unregister();
        _source = hWnd;

        if (hWnd != IntPtr.Zero && FindForm() is Form form &&
            Native.DwmRegisterThumbnail(form.Handle, hWnd, out _thumb) != 0)
            _thumb = IntPtr.Zero;

        UpdateThumbnail();
    }

    void Unregister()
    {
        if (_thumb != IntPtr.Zero) Native.DwmUnregisterThumbnail(_thumb);
        _thumb = IntPtr.Zero;
    }

    public void UpdateThumbnail()
    {
        // Se a janela de destino ainda não existia quando a origem foi definida, registra agora
        if (_thumb == IntPtr.Zero && _source != IntPtr.Zero && Native.IsWindow(_source) &&
            FindForm() is Form host && host.IsHandleCreated &&
            Native.DwmRegisterThumbnail(host.Handle, _source, out _thumb) != 0)
            _thumb = IntPtr.Zero;

        string msg = "";
        if (_source == IntPtr.Zero) msg = EmptyMessage;
        else if (!Native.IsWindow(_source)) msg = "A janela foi fechada.";
        else if (Native.IsIconic(_source)) msg = "Janela minimizada.\nRestaure-a para ver a prévia.";
        else if (_thumb == IntPtr.Zero) msg = "Não foi possível gerar a prévia desta janela.";

        if (msg != _message) { _message = msg; Invalidate(); }
        if (_thumb == IntPtr.Zero || FindForm() is not Form form) return;

        bool visible = msg == "" && Visible && Width > 10 && Height > 10;

        // A miniatura é desenhada nas coordenadas do Form, então convertemos a área do painel
        Rectangle area = form.RectangleToClient(RectangleToScreen(ClientRectangle));
        area.Inflate(-Theme.S(8), -Theme.S(8));

        // Mantém a proporção da janela original
        Native.DwmQueryThumbnailSourceSize(_thumb, out var src);
        if (ClientAreaOnly && Native.GetClientRect(_source, out var crc) && crc.right > 0 && crc.bottom > 0)
            src = new Native.PSIZE { x = crc.right - crc.left, y = crc.bottom - crc.top };
        Rectangle dest = area;
        if (src.x > 0 && src.y > 0)
        {
            double scale = Math.Min((double)area.Width / src.x, (double)area.Height / src.y);
            scale = Math.Min(scale, 1.0); // não amplia além do tamanho real
            int w = (int)(src.x * scale), h = (int)(src.y * scale);
            dest = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        }

        var props = new Native.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = Native.DWM_TNP_RECTDESTINATION | Native.DWM_TNP_VISIBLE |
                      Native.DWM_TNP_OPACITY | Native.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new Native.RECT { left = dest.Left, top = dest.Top, right = dest.Right, bottom = dest.Bottom },
            opacity = 255,
            fVisible = visible,
            fSourceClientAreaOnly = ClientAreaOnly,
        };
        Native.DwmUpdateThumbnailProperties(_thumb, ref props);

        var destInPanel = RectangleToClient(form.RectangleToScreen(dest));
        if (destInPanel != _destInPanel) { _destInPanel = destInPanel; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Borda arredondada da área de prévia
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Gfx.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.S(8)))
        using (var pen = new Pen(Theme.Current.Border))
            e.Graphics.DrawPath(pen, path);
        if (_message == "")
        {
            // No modo "marcar ponto", contorna a prévia na cor de destaque
            if (_pickMode && !_destInPanel.IsEmpty)
            {
                using var pen = new Pen(Theme.Current.Accent, Math.Max(2, Theme.S(2)));
                var r = _destInPanel; r.Inflate(Theme.S(3), Theme.S(3));
                e.Graphics.DrawRectangle(pen, r);
            }
            return;
        }
        TextRenderer.DrawText(e.Graphics, L.T(_message), Font, ClientRectangle, Theme.Current.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (!_pickMode || _message != "" || _destInPanel.Width < 2 || _destInPanel.Height < 2) return;
        if (!_destInPanel.Contains(e.Location)) return;
        double rx = (e.X - _destInPanel.X) / (double)(_destInPanel.Width - 1);
        double ry = (e.Y - _destInPanel.Y) / (double)(_destInPanel.Height - 1);
        PointPicked?.Invoke(Math.Clamp(rx, 0, 1), Math.Clamp(ry, 0, 1));
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateThumbnail(); }
    protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); UpdateThumbnail(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateThumbnail(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        Unregister();
        base.Dispose(disposing);
    }
}
