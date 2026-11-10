namespace HiddenApp;

public class MainForm : Form
{
    // ---------- Estado ----------
    readonly AppSettings _settings = AppSettings.Load();
    bool _allowShow;      // false = começa escondido na bandeja
    bool _exiting;        // true = "Sair" de verdade (não só esconder)
    bool _refreshing;
    readonly Dictionary<string, Bitmap?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    // ---------- Controles ----------
    readonly Panel _header = new() { Dock = DockStyle.Top, Height = Theme.S(72) };
    readonly Label _appTitle = new() { Text = "HiddenApp", AutoSize = true, Font = new Font("Segoe UI Semibold", 15f) };
    readonly Label _appSubtitle = new() { Text = "Corte a internet de qualquer janela com um clique", AutoSize = true };
    readonly ToggleSwitch _darkToggle = new("Tema escuro");
    readonly ToggleSwitch _startupToggle = new("Iniciar com o Windows");

    readonly TableLayoutPanel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(11)), ColumnCount = 2, RowCount = 2 };
    readonly Card _windowsCard = new("Janelas abertas", "duplo clique alterna o bloqueio");
    readonly Card _previewCard = new("Prévia ao vivo");
    readonly Card _blockedCard = new("Programas bloqueados", "continuam bloqueados mesmo com o HiddenApp fechado");

    readonly ThemedListView _windows = new() { Dock = DockStyle.Fill };
    readonly ListBox _blockedList = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.S(44), IntegralHeight = false };
    readonly Label _emptyBlocked = new() { Dock = DockStyle.Fill, Text = "Nenhum programa bloqueado no momento.", TextAlign = ContentAlignment.MiddleCenter };

    readonly WindowPreview _preview = new() { Dock = DockStyle.Fill };
    readonly Label _previewTitle = new() { Dock = DockStyle.Top, Height = Theme.S(26), AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 10f) };
    readonly Label _previewSub = new() { Dock = DockStyle.Top, Height = Theme.S(28), AutoEllipsis = true };

    readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = Theme.S(32), Padding = new Padding(Theme.S(18), 0, Theme.S(18), 0) };
    readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Label _statusCount = new() { Dock = DockStyle.Right, AutoSize = false, Width = Theme.S(280), TextAlign = ContentAlignment.MiddleRight };

    readonly RoundButton _btnPick = new("Escolher janela (3s)");
    readonly System.Windows.Forms.Timer _pickTimer = new() { Interval = 1000 };
    int _countdown;

    readonly NotifyIcon _tray = new();
    readonly ContextMenuStrip _trayMenu = new();

    static readonly Font HeaderFont = new("Segoe UI Semibold", 8.5f);
    static readonly Font RowFont = new("Segoe UI", 9.5f);
    static readonly Font BoldFont = new("Segoe UI Semibold", 9.5f);
    static readonly Font PillFont = new("Segoe UI Semibold", 8f);
    static readonly Font SmallFont = new("Segoe UI", 8.5f);

    record RowData(WindowInfo Window, string Status, bool IsClone);

    public MainForm(bool startInTray = false)
    {
        _allowShow = !startInTray;

        Text = "HiddenApp";
        Icon = Theme.AppIcon;
        Width = Theme.S(1280); Height = Theme.S(760);
        MinimumSize = new Size(Theme.S(980), Theme.S(600));
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        ToolStripManager.Renderer = new ThemedMenuRenderer();

        BuildHeader();
        BuildWindowsCard();
        BuildPreviewCard();
        BuildBlockedCard();
        BuildTray();

        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        _body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _body.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(230)));
        _body.Controls.Add(_windowsCard, 0, 0);
        _body.Controls.Add(_previewCard, 1, 0);
        _body.Controls.Add(_blockedCard, 0, 1);
        _body.SetColumnSpan(_blockedCard, 2);

        _statusBar.Controls.Add(_status);
        _statusBar.Controls.Add(_statusCount);
        _statusBar.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0); };

        Controls.Add(_body);
        Controls.Add(_statusBar);
        Controls.Add(_header);

        // ---------- Inicialização ----------
        Theme.Set(_settings.DarkTheme);
        _darkToggle.SetSilently(_settings.DarkTheme);
        _startupToggle.SetSilently(StartupManager.IsEnabled());
        Task.Run(StartupManager.RefreshIfEnabled);

        Theme.Changed += ApplyTheme;
        ApplyTheme();

        FirewallManager.Load();
        RefreshAll();
        SetStatus("Selecione uma janela e clique em \"Cortar internet\".");

        Activated += (_, _) => { if (!_refreshing) RefreshAll(); };
    }

    // =====================================================================
    //  Montagem da interface
    // =====================================================================
    void BuildHeader()
    {
        int logoSize = Theme.S(44);
        var logo = new PictureBox { Image = Theme.Logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(logoSize, logoSize), Location = new Point(Theme.S(18), (_header.Height - logoSize) / 2) };
        // Título e subtítulo empilhados e centralizados ao lado do logo
        int textX = logo.Right + Theme.S(12);
        int titleH = _appTitle.PreferredHeight, subH = _appSubtitle.PreferredHeight;
        _appTitle.Location = new Point(textX, (_header.Height - titleH - subH) / 2);
        _appSubtitle.Location = new Point(textX + Theme.S(2), _appTitle.Top + titleH);

        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, (_header.Height - Theme.S(30)) / 2, Theme.S(18), 0) };
        right.Controls.Add(_startupToggle);
        right.Controls.Add(_darkToggle);

        _darkToggle.CheckedChanged += (_, _) =>
        {
            _settings.DarkTheme = _darkToggle.Checked;
            _settings.Save();
            Theme.Set(_darkToggle.Checked);
        };
        _startupToggle.CheckedChanged += (_, _) => SetStartup(_startupToggle.Checked);

        _header.Controls.Add(logo);
        _header.Controls.Add(_appTitle);
        _header.Controls.Add(_appSubtitle);
        _header.Controls.Add(right);
        _header.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1); };
    }

    void BuildWindowsCard()
    {
        _windows.Columns.Add("Janela", Theme.S(400));
        _windows.Columns.Add("Programa", Theme.S(200));
        _windows.Columns.Add("PID", Theme.S(70));
        _windows.Columns.Add("Internet", Theme.S(125));
        _windows.DrawColumnHeader += DrawWindowsHeader;
        _windows.DrawItem += (_, e) => e.DrawDefault = false;
        _windows.DrawSubItem += DrawWindowsCell;
        _windows.DoubleClick += (_, _) => ToggleSelected();
        _windows.SelectedIndexChanged += (_, _) => { if (!_refreshing) UpdatePreview(); };
        _windows.ItemSelectionChanged += (_, _) => _windows.Invalidate();
        _windows.MouseUp += (_, _) => _windows.Invalidate();
        _windows.ClientSizeChanged += (_, _) => FitColumns();

        var btnBlock = new RoundButton("Cortar internet", ButtonKind.Danger);
        var btnUnblock = new RoundButton("Liberar internet");
        var btnClone = new RoundButton("Abrir cópia bloqueável...", ButtonKind.Primary);
        var btnRefresh = new RoundButton("Atualizar");
        btnBlock.Click += (_, _) => BlockSelected();
        btnUnblock.Click += (_, _) => UnblockSelected();
        btnClone.Click += (_, _) => OpenClone();
        btnRefresh.Click += (_, _) => RefreshAll();
        _btnPick.Click += (_, _) => StartPick();
        _pickTimer.Tick += PickTick;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        toolbar.Controls.AddRange(new Control[] { btnBlock, btnUnblock, _btnPick, btnClone, btnRefresh });

        _windowsCard.Controls.Add(_windows);
        _windowsCard.Controls.Add(toolbar);
    }

    void BuildPreviewCard()
    {
        _previewCard.Controls.Add(_preview);
        _previewCard.Controls.Add(_previewSub);
        _previewCard.Controls.Add(_previewTitle);
    }

    void BuildBlockedCard()
    {
        _blockedList.DrawItem += DrawBlockedItem;

        var btnUnblockSel = new RoundButton("Liberar selecionado");
        var btnUnblockAll = new RoundButton("Liberar todos");
        var btnDeleteClones = new RoundButton("Apagar cópias");
        btnUnblockSel.Click += (_, _) =>
        {
            if (_blockedList.SelectedItem is string path)
            {
                FirewallManager.Unblock(path);
                RefreshAll();
                SetStatus($"Internet liberada: {Path.GetFileName(path)}");
            }
        };
        btnUnblockAll.Click += (_, _) => UnblockAll();
        btnDeleteClones.Click += (_, _) => DeleteClones();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        toolbar.Controls.AddRange(new Control[] { btnUnblockSel, btnUnblockAll, btnDeleteClones });

        _blockedCard.Controls.Add(_blockedList);
        _blockedCard.Controls.Add(_emptyBlocked);
        _blockedCard.Controls.Add(toolbar);
    }

    // =====================================================================
    //  Tema
    // =====================================================================
    void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Back;
        ForeColor = p.Text;

        _header.BackColor = p.Surface;
        _appTitle.ForeColor = p.Text;
        _appSubtitle.ForeColor = p.Muted;

        _statusBar.BackColor = p.Surface;
        _status.ForeColor = p.Muted;
        _statusCount.ForeColor = p.Muted;

        _body.BackColor = p.Back;
        foreach (var card in new[] { _windowsCard, _previewCard, _blockedCard })
            card.BackColor = p.Surface;

        _windows.BackColor = p.Surface;
        _windows.ForeColor = p.Text;
        _blockedList.BackColor = p.Surface;
        _blockedList.ForeColor = p.Text;
        _emptyBlocked.ForeColor = p.Muted;

        _preview.BackColor = p.Back;
        _previewTitle.ForeColor = p.Text;
        _previewSub.ForeColor = p.Muted;

        Theme.ApplyTitleBar(this);
        Theme.ApplyScrollbars(_windows);
        Theme.ApplyScrollbars(_blockedList);
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyTheme();
        FitColumns();
    }

    // =====================================================================
    //  Bandeja do sistema (perto do relógio)
    // =====================================================================
    void BuildTray()
    {
        _tray.Icon = Theme.TrayIcon(SystemInformation.SmallIconSize.Width);
        _tray.Text = "HiddenApp";
        _tray.ContextMenuStrip = _trayMenu;
        _tray.Visible = true;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
        _trayMenu.Opening += (_, _) => BuildTrayMenu();
    }

    void BuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(new ToolStripMenuItem("Abrir HiddenApp", null, (_, _) => ShowFromTray()) { Font = BoldFont });
        _trayMenu.Items.Add(new ToolStripSeparator());

        var blocked = FirewallManager.Blocked.OrderBy(Path.GetFileName).ToList();
        if (blocked.Count == 0)
        {
            _trayMenu.Items.Add(new ToolStripMenuItem("Nenhum programa bloqueado") { Enabled = false });
        }
        else
        {
            var sub = new ToolStripMenuItem($"Programas bloqueados ({blocked.Count})");
            foreach (var path in blocked)
            {
                string label = Path.GetFileName(path) + (CloneManager.IsClone(path) ? " (cópia)" : "");
                sub.DropDownItems.Add(new ToolStripMenuItem($"Liberar {label}", null, (_, _) =>
                {
                    FirewallManager.Unblock(path);
                    RefreshAll();
                    _tray.ShowBalloonTip(2500, "Internet liberada", label, ToolTipIcon.Info);
                }));
            }
            _trayMenu.Items.Add(sub);
            _trayMenu.Items.Add(new ToolStripMenuItem("Liberar todos", null, (_, _) => UnblockAll()));
        }

        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem("Tema escuro", null, (_, _) => _darkToggle.Checked = !_darkToggle.Checked) { Checked = _darkToggle.Checked });
        _trayMenu.Items.Add(new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => _startupToggle.Checked = !_startupToggle.Checked) { Checked = _startupToggle.Checked });
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => ExitApp()));
    }

    /// <summary>Impede a janela de aparecer quando o app inicia direto na bandeja.</summary>
    protected override void SetVisibleCore(bool value)
    {
        if (!_allowShow)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    public void ShowFromTray()
    {
        _allowShow = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        RefreshAll();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // O "X" só esconde a janela: o HiddenApp continua na bandeja
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_settings.TrayHintShown)
            {
                _tray.ShowBalloonTip(5000, "O HiddenApp continua rodando",
                    "Ele fica aqui perto do relógio. Clique com o botão direito no ícone para sair.", ToolTipIcon.Info);
                _settings.TrayHintShown = true;
                _settings.Save();
            }
            return;
        }
        _tray.Visible = false;
        base.OnFormClosing(e);
    }

    void ExitApp()
    {
        _exiting = true;
        _tray.Visible = false;
        Close();
        Application.Exit();
    }

    void SetStartup(bool enable)
    {
        try
        {
            if (enable) StartupManager.Enable(); else StartupManager.Disable();
            SetStatus(enable ? "O HiddenApp vai abrir na bandeja quando o Windows iniciar." : "Inicialização com o Windows desativada.");
        }
        catch (Exception ex)
        {
            _startupToggle.SetSilently(!enable);
            MessageBox.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // =====================================================================
    //  Listas
    // =====================================================================
    void SetStatus(string text) => _status.Text = text;

    void RefreshAll()
    {
        _refreshing = true;
        try
        {
            IntPtr selected = SelectedWindow?.Handle ?? IntPtr.Zero;

            _windows.BeginUpdate();
            _windows.Items.Clear();
            foreach (var w in WindowEnumerator.GetOpenWindows().OrderBy(w => w.ProcessName))
            {
                string status = w.ExePath == null ? "SEM ACESSO" : FirewallManager.IsBlocked(w.ExePath) ? "BLOQUEADA" : "LIBERADA";
                bool clone = CloneManager.IsClone(w.ExePath);
                var item = new ListViewItem(new[] { w.Title, w.ProcessName, w.Pid.ToString(), status })
                {
                    Tag = new RowData(w, status, clone)
                };
                _windows.Items.Add(item);
            }
            _windows.EndUpdate();
            if (selected != IntPtr.Zero) SelectWindowInList(selected);

            _blockedList.BeginUpdate();
            _blockedList.Items.Clear();
            foreach (var path in FirewallManager.Blocked.OrderBy(Path.GetFileName))
                _blockedList.Items.Add(path);
            _blockedList.EndUpdate();
            bool any = _blockedList.Items.Count > 0;
            _blockedList.Visible = any;
            _emptyBlocked.Visible = !any;

            int n = FirewallManager.Blocked.Count;
            _statusCount.Text = n == 0 ? "Nenhum programa bloqueado" : $"{n} programa(s) sem internet";
            string tip = n == 0 ? "HiddenApp" : $"HiddenApp - {n} programa(s) bloqueado(s)";
            _tray.Text = tip.Length > 63 ? tip[..63] : tip;
        }
        finally { _refreshing = false; }

        FitColumns();
        UpdatePreview();
    }

    void FitColumns()
    {
        if (_windows.Columns.Count < 4) return;
        int others = _windows.Columns[1].Width + _windows.Columns[2].Width + _windows.Columns[3].Width;
        _windows.Columns[0].Width = Math.Max(Theme.S(150), _windows.ClientSize.Width - others - 1);
    }

    void UpdatePreview()
    {
        var w = SelectedWindow;
        _preview.SetSource(w?.Handle ?? IntPtr.Zero);
        _previewTitle.Text = w == null ? "Nenhuma janela selecionada" : w.Title;
        _previewSub.Text = w == null ? "Clique numa janela da lista para ver a prévia aqui."
                                     : $"{w.ProcessName}  ·  PID {w.Pid}" + (CloneManager.IsClone(w.ExePath) ? "  ·  cópia" : "");
    }

    void SelectWindowInList(IntPtr hWnd)
    {
        foreach (ListViewItem item in _windows.Items)
            if (item.Tag is RowData r && r.Window.Handle == hWnd)
            {
                item.Selected = true;
                item.EnsureVisible();
                return;
            }
    }

    WindowInfo? SelectedWindow =>
        _windows.SelectedItems.Count > 0 && _windows.SelectedItems[0].Tag is RowData r ? r.Window : null;

    Bitmap? GetIcon(string? path)
    {
        if (path == null) return null;
        if (_iconCache.TryGetValue(path, out var bmp)) return bmp;
        try { bmp = Icon.ExtractAssociatedIcon(path)?.ToBitmap(); } catch { bmp = null; }
        _iconCache[path] = bmp;
        return bmp;
    }

    // ---------- Desenho da lista de janelas ----------
    void DrawWindowsHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        var p = Theme.Current;
        using (var b = new SolidBrush(p.Surface2)) e.Graphics.FillRectangle(b, e.Bounds);
        var r = e.Bounds; r.X += Theme.S(12); r.Width -= Theme.S(12);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text.ToUpperInvariant(), HeaderFont, r, p.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    void DrawWindowsCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item?.Tag is not RowData row) return;
        var p = Theme.Current;
        var g = e.Graphics;
        var bounds = e.Bounds;
        Color bg = e.Item.Selected ? p.Selection : p.Surface;

        using (var b = new SolidBrush(bg)) g.FillRectangle(b, bounds);
        using (var line = new Pen(Theme.Blend(p.Border, p.Surface, 0.4)))
            g.DrawLine(line, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        switch (e.ColumnIndex)
        {
            case 0:
                var icon = GetIcon(row.Window.ExePath);
                if (icon != null)
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    int isz = Theme.S(20);
                    g.DrawImage(icon, bounds.Left + Theme.S(12), bounds.Top + (bounds.Height - isz) / 2, isz, isz);
                }
                TextRenderer.DrawText(g, row.Window.Title, RowFont,
                    new Rectangle(bounds.Left + Theme.S(42), bounds.Top, bounds.Width - Theme.S(48), bounds.Height), p.Text, flags);
                break;

            case 1:
                var r1 = new Rectangle(bounds.Left + Theme.S(12), bounds.Top, bounds.Width - Theme.S(16), bounds.Height);
                TextRenderer.DrawText(g, row.Window.ProcessName, RowFont, r1, p.Text, flags);
                if (row.IsClone)
                {
                    int w = TextRenderer.MeasureText(row.Window.ProcessName, RowFont).Width;
                    DrawPill(g, "CÓPIA", p.Accent, bg, bounds.Left + Theme.S(16) + w, bounds);
                }
                break;

            case 2:
                TextRenderer.DrawText(g, row.Window.Pid.ToString(), RowFont,
                    new Rectangle(bounds.Left + Theme.S(12), bounds.Top, bounds.Width - Theme.S(16), bounds.Height), p.Muted, flags);
                break;

            case 3:
                Color c = row.Status switch { "BLOQUEADA" => p.Danger, "LIBERADA" => p.Success, _ => p.Muted };
                DrawPill(g, row.Status, c, bg, bounds.Left + Theme.S(12), bounds);
                break;
        }
    }

    static void DrawPill(Graphics g, string text, Color color, Color bg, int x, Rectangle cell)
    {
        var size = TextRenderer.MeasureText(text, PillFont);
        float h = Theme.S(20);
        var r = new RectangleF(x, cell.Top + (cell.Height - h) / 2f, size.Width + Theme.S(10), h);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Gfx.RoundRect(r, h / 2))
        using (var b = new SolidBrush(Theme.Blend(color, bg, 0.80)))
            g.FillPath(b, path);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g, text, PillFont, Rectangle.Round(r), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    // ---------- Desenho da lista de bloqueados ----------
    void DrawBlockedItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _blockedList.Items[e.Index] is not string path) return;
        var p = Theme.Current;
        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color bg = selected ? p.Selection : p.Surface;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);

        int pad = Theme.S(8), isz = Theme.S(24), textX = e.Bounds.Left + Theme.S(42);
        var icon = GetIcon(path);
        if (icon != null)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, e.Bounds.Left + pad, e.Bounds.Top + (e.Bounds.Height - isz) / 2, isz, isz);
        }

        string name = Path.GetFileName(path);
        int line1 = BoldFont.Height + Theme.S(4);
        int top = e.Bounds.Top + (e.Bounds.Height - line1 - SmallFont.Height) / 2;
        var firstLine = new Rectangle(e.Bounds.Left, top, e.Bounds.Width, line1);
        TextRenderer.DrawText(g, name, BoldFont, new Rectangle(textX, top, e.Bounds.Width, line1), p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        int x = textX + TextRenderer.MeasureText(name, BoldFont).Width + Theme.S(4);
        DrawPill(g, "SEM INTERNET", p.Danger, bg, x, firstLine);
        if (CloneManager.IsClone(path))
        {
            int x2 = x + TextRenderer.MeasureText("SEM INTERNET", PillFont).Width + Theme.S(16);
            DrawPill(g, "CÓPIA", p.Accent, bg, x2, firstLine);
        }
        TextRenderer.DrawText(g, path, SmallFont,
            new Rectangle(textX, top + line1, e.Bounds.Width - Theme.S(50), SmallFont.Height), p.Muted,
            TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine);
    }

    // =====================================================================
    //  Ações
    // =====================================================================
    void ToggleSelected()
    {
        var w = SelectedWindow;
        if (w?.ExePath == null) return;
        if (FirewallManager.IsBlocked(w.ExePath)) UnblockSelected(); else BlockSelected();
    }

    void BlockSelected()
    {
        var w = SelectedWindow;
        if (w == null) { SetStatus("Selecione uma janela primeiro."); return; }
        Block(w);
    }

    void Block(WindowInfo w)
    {
        if (w.ExePath == null)
        {
            MessageBox.Show("Não foi possível descobrir o executável dessa janela.", "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Aviso: o bloqueio vale para o programa inteiro, não só para esta janela
        var others = WindowEnumerator.GetOpenWindows()
            .Count(x => x.Handle != w.Handle && string.Equals(x.ExePath, w.ExePath, StringComparison.OrdinalIgnoreCase));
        if (others > 0)
        {
            var r = MessageBox.Show(
                $"O programa \"{w.ProcessName}\" tem mais {others} janela(s) aberta(s).\n" +
                "O bloqueio de internet vale para o programa inteiro, então todas elas ficarão sem internet.\n\n" +
                "Dica: use \"Abrir cópia bloqueável...\" para bloquear só uma janela.\n\nContinuar?",
                "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            FirewallManager.Block(w.ExePath);
            // Corta também as conexões que já estavam abertas
            int killed = Native.KillTcpConnections(WindowEnumerator.GetPidsForExes(FirewallManager.TargetsFor(w.ExePath)));
            SetStatus($"Internet cortada para {w.ProcessName}. ({killed} conexão(ões) aberta(s) derrubada(s))");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
        RefreshAll();
    }

    void UnblockSelected()
    {
        var w = SelectedWindow;
        if (w?.ExePath == null) { SetStatus("Selecione uma janela primeiro."); return; }
        FirewallManager.Unblock(w.ExePath);
        SetStatus($"Internet liberada para {w.ProcessName}.");
        RefreshAll();
    }

    void UnblockAll()
    {
        FirewallManager.UnblockAll();
        RefreshAll();
        SetStatus("Todos os programas foram liberados.");
    }

    // ---------- Cópia bloqueável (hard links) ----------
    async void OpenClone()
    {
        string? initial = SelectedWindow?.ExePath;
        if (CloneManager.IsClone(initial)) initial = null;

        using var dlg = new CloneDialog(initial);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string exe = dlg.ExePath, args = dlg.Arguments;
        bool startBlocked = dlg.StartBlocked;

        try
        {
            UseWaitCursor = true;
            SetStatus("Verificando a pasta do programa...");
            int count = await Task.Run(() => CloneManager.CountFiles(exe));
            if (CloneManager.TooManyFiles(count) &&
                MessageBox.Show("A pasta desse programa tem muitos arquivos (mais de 30.000). " +
                                "A cópia pode demorar alguns minutos. Continuar?",
                                "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                SetStatus("Cancelado.");
                return;
            }

            var progress = new Progress<string>(SetStatus);
            var res = await Task.Run(() => CloneManager.CreateOrUpdate(exe, progress));

            if (startBlocked)
            {
                // Refaz as regras: o programa pode ter sido atualizado e ganhado .exe novos
                if (FirewallManager.IsBlocked(res.CloneExe)) FirewallManager.Unblock(res.CloneExe);
                FirewallManager.Block(res.CloneExe);
            }

            SetStatus("Abrindo a cópia...");
            CloneManager.Launch(res.CloneExe, args);

            string info = $"{res.Linked} arquivos linkados" +
                          (res.Copied > 0 ? $", {res.Copied} copiados" : "") +
                          (res.Failed > 0 ? $", {res.Failed} com erro" : "");
            SetStatus($"Cópia aberta ({info}). {(startBlocked ? "Ela já está sem internet." : "")}");

            // Confere se o programa abriu mesmo uma instância separada
            await Task.Delay(4000);
            if (WindowEnumerator.GetPidsForExes(FirewallManager.TargetsFor(res.CloneExe)).Count == 0)
            {
                MessageBox.Show(
                    "A cópia foi iniciada mas fechou logo em seguida.\n\n" +
                    "Normalmente isso acontece com programas que só permitem uma instância: " +
                    "eles repassam a janela para o programa original (que NÃO está bloqueado).\n\n" +
                    "Tente fechar o programa original antes, ou informe um argumento para abrir separado " +
                    "(ex.: uma pasta de dados/perfil diferente).",
                    "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            RefreshAll();
        }
    }

    void DeleteClones()
    {
        if (MessageBox.Show("Apagar todas as cópias criadas pelo HiddenApp?\n\n" +
                            "Os programas originais NÃO são afetados. Feche as cópias abertas antes.",
                            "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        // Tira as regras antes de apagar (depois de apagada não dá para listar os .exe da pasta)
        var blockedClones = FirewallManager.Blocked.Where(CloneManager.IsClone).ToList();
        foreach (var path in blockedClones) FirewallManager.Unblock(path);

        var (deleted, inUse) = CloneManager.DeleteAllClones();

        // Cópias que não puderam ser apagadas (ainda abertas) voltam a ficar bloqueadas
        foreach (var path in blockedClones.Where(File.Exists))
            FirewallManager.Block(path);
        SetStatus(inUse == 0
            ? $"{deleted} cópia(s) apagada(s)."
            : $"{deleted} cópia(s) apagada(s); {inUse} não puderam ser apagadas (ainda abertas?).");
        RefreshAll();
    }

    // ---------- "Escolher janela": clica no botão e depois clica na janela desejada ----------
    void StartPick()
    {
        _countdown = 3;
        _btnPick.Enabled = false;
        SetStatus("Clique na janela que deseja bloquear... 3");
        _pickTimer.Start();
    }

    void PickTick(object? sender, EventArgs e)
    {
        _countdown--;
        if (_countdown > 0) { SetStatus($"Clique na janela que deseja bloquear... {_countdown}"); return; }

        _pickTimer.Stop();
        _btnPick.Enabled = true;

        IntPtr fg = Native.GetForegroundWindow();
        var w = WindowEnumerator.Describe(fg);
        Activate();

        if (w == null || w.Pid == (uint)Environment.ProcessId)
        {
            SetStatus("Nenhuma outra janela foi selecionada. Tente de novo.");
            return;
        }

        // Seleciona na lista para a prévia mostrar a janela escolhida
        RefreshAll();
        SelectWindowInList(w.Handle);

        var r = MessageBox.Show($"Cortar a internet de:\n\n\"{w.Title}\"\n({w.ProcessName})?",
            "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r == DialogResult.Yes) Block(w);
        else RefreshAll();
    }
}
