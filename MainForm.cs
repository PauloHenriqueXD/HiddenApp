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
    readonly Label _versionLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f) };
    readonly ToggleSwitch _darkToggle = new("Tema escuro");
    readonly ToggleSwitch _startupToggle = new("Iniciar com o Windows");

    readonly TableLayoutPanel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(11)), ColumnCount = 2, RowCount = 2 };
    readonly Card _windowsCard = new("Janelas abertas", "duplo clique alterna o bloqueio");
    readonly Card _previewCard = new("Prévia ao vivo");
    readonly Card _blockedCard = new("Programas bloqueados", "continuam bloqueados com o app fechado");
    readonly Card _favoritesCard = new("Favoritos", "acesso rápido aos seus programas");
    readonly TableLayoutPanel _bottom = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };

    readonly ThemedListView _windows = new() { Dock = DockStyle.Fill };
    readonly ListBox _blockedList = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.S(44), IntegralHeight = false };
    readonly Label _emptyBlocked = new() { Dock = DockStyle.Fill, Text = "Nenhum programa bloqueado no momento.", TextAlign = ContentAlignment.MiddleCenter };
    readonly ListBox _favoritesList = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.S(44), IntegralHeight = false };
    readonly Label _emptyFavorites = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Text = "Nenhum favorito ainda.\nClique na estrela ★ ao lado de uma janela da lista ou use \"Adicionar...\"."
    };
    readonly ThemedToolTip _tips = new();
    readonly Dictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);

    readonly WindowPreview _preview = new() { Dock = DockStyle.Fill };
    readonly Label _previewTitle = new() { Dock = DockStyle.Top, Height = Theme.S(26), AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 10f) };
    readonly Label _previewSub = new() { Dock = DockStyle.Top, Height = Theme.S(28), AutoEllipsis = true };

    readonly Panel _statusBar = new() { Dock = DockStyle.Bottom, Height = Theme.S(32), Padding = new Padding(Theme.S(18), 0, Theme.S(18), 0) };
    readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Label _statusCount = new() { Dock = DockStyle.Right, AutoSize = false, Width = Theme.S(280), TextAlign = ContentAlignment.MiddleRight };

    readonly RoundButton _btnPick = RoundButton.IconButton(Theme.Glyph.Search, "Selecionar manualmente", ButtonKind.Secondary);
    readonly System.Windows.Forms.Timer _pickTimer = new() { Interval = 1000 };
    int _countdown;

    // ---------- Atualizações ----------
    readonly bool _justUpdated;
    UpdateInfo? _update;                 // versão nova disponível (null = está atualizado)
    bool _checkingUpdates, _updateDialogOpen, _balloonIsUpdate;
    readonly RoundButton _btnUpdate = new("Atualizar", ButtonKind.Primary) { Visible = false };
    readonly RoundButton _btnUpdatesMenu = new("Atualizações ▾");
    readonly RoundButton _btnSupport = new("❤ Apoiar");
    readonly RoundButton _btnLanguage = RoundButton.IconButton(Theme.Glyph.Globe, "Idioma", ButtonKind.Secondary);
    readonly ContextMenuStrip _languageMenu = new();
    readonly RoundButton _btnFeedback = RoundButton.IconButton(Theme.Glyph.Feedback, "Sugestões", ButtonKind.Secondary);
    readonly ContextMenuStrip _feedbackMenu = new();
    readonly ContextMenuStrip _updatesMenu = new();
    readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 6 * 60 * 60 * 1000 }; // a cada 6 horas

    // ---------- Abas e macros ----------
    readonly Panel _tabBar = new() { Dock = DockStyle.Top, Height = Theme.S(44) };
    readonly TabButton _tabInternet = new("Internet");
    readonly TabButton _tabClicker = new("Macro");
    AutoClickPage _autoClick = null!;
    const int HotkeyIdStart = 1, HotkeyIdStop = 2, HotkeyIdCapture = 3;

    readonly NotifyIcon _tray = new();
    readonly ContextMenuStrip _trayMenu = new();

    static readonly Font HeaderFont = new("Segoe UI Semibold", 8.5f);
    static readonly Font RowFont = new("Segoe UI", 9.5f);
    static readonly Font BoldFont = new("Segoe UI Semibold", 9.5f);
    static readonly Font PillFont = new("Segoe UI Semibold", 8f);
    static readonly Font SmallFont = new("Segoe UI", 8.5f);

    record RowData(WindowInfo Window, string Status, bool IsClone, string? CloneLabel);

    public MainForm(bool startInTray = false, bool justUpdated = false)
    {
        _allowShow = !startInTray;
        _autoClick = new AutoClickPage(_settings, _tips) { Visible = false };
        _justUpdated = justUpdated;

        Text = "HiddenApp";
        Icon = Theme.AppIcon;
        Width = Theme.S(1280); Height = Theme.S(760);
        MinimumSize = new Size(Theme.S(1120), Theme.S(600));
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        ToolStripManager.Renderer = new ThemedMenuRenderer();

        BuildHeader();
        BuildWindowsCard();
        BuildPreviewCard();
        BuildBlockedCard();
        BuildFavoritesCard();
        BuildTray();

        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        _body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _body.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.S(250)));
        _body.Controls.Add(_windowsCard, 0, 0);
        _body.Controls.Add(_previewCard, 1, 0);
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _favoritesCard.Accent = CardAccent.Favorite;
        _blockedCard.Accent = CardAccent.Danger;
        _bottom.Controls.Add(_favoritesCard, 0, 0);
        _bottom.Controls.Add(_blockedCard, 1, 0);
        _body.Controls.Add(_bottom, 0, 1);
        _body.SetColumnSpan(_bottom, 2);

        _statusBar.Controls.Add(_status);
        _statusBar.Controls.Add(_statusCount);
        _statusBar.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0); };

        BuildTabs();
        Controls.Add(_body);
        Controls.Add(_autoClick);
        Controls.Add(_statusBar);
        Controls.Add(_tabBar);
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
        SetStatus("Selecione uma janela e clique em \"Bloquear internet\". Passe o mouse nos botões para ver o que cada um faz.");

        Activated += (_, _) => { if (!_refreshing && _body.Visible) RefreshAll(); };
        SelectTab(_settings.LastTab);
        RegisterHotkeys(showErrors: false);
        L.Apply(this); // traduz a interface (se o idioma não for português)

        // ---------- Atualizações ----------
        UpdateManager.CleanupOldVersion();
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(manual: false);
        _updateTimer.Start();
        _ = StartupUpdateCheckAsync();
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
        _versionLabel.Text = "v" + UpdateManager.CurrentVersion;
        _versionLabel.Location = new Point(_appTitle.Left + _appTitle.PreferredWidth + Theme.S(4),
                                           _appTitle.Top + titleH - _versionLabel.PreferredHeight - Theme.S(4));

        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, (_header.Height - Theme.S(30)) / 2, Theme.S(18), 0) };
        _btnUpdate.Margin = new Padding(0, -Theme.S(2), Theme.S(8), 0);
        _btnUpdatesMenu.Margin = new Padding(0, -Theme.S(2), Theme.S(4), 0);
        _btnUpdate.Click += (_, _) => ShowUpdateDialog();
        _btnUpdatesMenu.Click += (_, _) =>
        {
            BuildUpdatesMenu();
            _updatesMenu.Show(_btnUpdatesMenu, new Point(0, _btnUpdatesMenu.Height));
        };
        _tips.Set(_btnUpdate, "Nova versão disponível", "Há uma versão nova do HiddenApp. Clique para ver as novidades e atualizar.");
        _btnSupport.Margin = new Padding(0, -Theme.S(2), Theme.S(4), 0);
        _btnSupport.Click += (_, _) => ShowSupport();
        _tips.Set(_btnSupport, "Apoiar o projeto", "O HiddenApp é gratuito. Se ele te ajuda, você pode fazer uma doação por Pix (QR code ou copia e cola).");
        _tips.Set(_btnUpdatesMenu, "Atualizações", "Verificar se há versão nova, escolher receber versões beta e ver a versão atual.");
        _tips.Set(_startupToggle, "Iniciar com o Windows", "Abre o HiddenApp escondido perto do relógio quando você entra no Windows, sem pedir permissão de administrador.");
        _tips.Set(_darkToggle, "Tema escuro", "Alterna entre o tema escuro e o tema claro.");
        _btnLanguage.Margin = new Padding(0, -Theme.S(2), Theme.S(4), 0);
        _btnLanguage.Click += (_, _) => ShowLanguageMenu();
        _tips.Set(_btnLanguage, "Idioma", "Escolhe o idioma do HiddenApp: português, inglês ou chinês. O app reinicia para aplicar.");
        _btnFeedback.Margin = new Padding(0, -Theme.S(2), Theme.S(4), 0);
        _btnFeedback.Click += (_, _) => ShowFeedbackMenu();
        _tips.Set(_btnFeedback, "Sugestões", "Mande um comentário, uma sugestão ou relate um problema. Abre um formulário no navegador, já com a versão do HiddenApp e do Windows; nada é enviado sem você confirmar.");
        right.Controls.Add(_btnUpdate);
        right.Controls.Add(_btnFeedback);
        right.Controls.Add(_btnLanguage);
        right.Controls.Add(_btnSupport);
        right.Controls.Add(_btnUpdatesMenu);
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
        _header.Controls.Add(_versionLabel);
        _header.Controls.Add(right);
        _header.Padding = new Padding(0, 0, 0, 1); // deixa a linha de baixo visível também atrás dos botões
        _header.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1); };
    }

    void BuildWindowsCard()
    {
        _windows.Columns.Add("", Theme.S(38)); // estrela de favorito
        _windows.Columns.Add("Janela", Theme.S(400));
        _windows.Columns.Add("Programa", Theme.S(200));
        _windows.Columns.Add("PID", Theme.S(70));
        _windows.Columns.Add("Internet", Theme.S(125));
        _windows.DrawColumnHeader += DrawWindowsHeader;
        _windows.DrawItem += (_, e) => e.DrawDefault = false;
        _windows.DrawSubItem += DrawWindowsCell;
        _windows.DoubleClick += (_, _) => { if (!_lastClickOnStar) ToggleSelected(); };
        _windows.MouseDown += (_, e) => _lastClickOnStar = IsStarArea(e.Location);
        _windows.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !IsStarArea(e.Location)) return;
            if (_windows.HitTest(e.Location).Item?.Tag is RowData row && row.Window.ExePath is string path)
                ToggleFavorite(path);
        };
        _windows.MouseMove += (_, e) =>
        {
            int hover = IsStarArea(e.Location) ? _windows.HitTest(e.Location).Item?.Index ?? -1 : -1;
            _windows.Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
            if (hover != _hoverStarRow) { _hoverStarRow = hover; _windows.Invalidate(); }
        };
        _windows.MouseLeave += (_, _) => { if (_hoverStarRow >= 0) { _hoverStarRow = -1; _windows.Invalidate(); } };
        _windows.SelectedIndexChanged += (_, _) => { if (!_refreshing) UpdatePreview(); };
        _windows.ItemSelectionChanged += (_, _) => _windows.Invalidate();
        _windows.MouseUp += (_, _) => _windows.Invalidate();
        _windows.ClientSizeChanged += (_, _) => FitColumns();

        // Ações principais com ícone e texto; as demais só com ícone (o nome aparece no balão)
        var btnBlock = RoundButton.IconTextButton(Theme.Glyph.Lock, "Bloquear internet", ButtonKind.Danger);
        var btnUnblock = RoundButton.IconTextButton(Theme.Glyph.Unlock, "Liberar internet", ButtonKind.Success);
        var btnClone = RoundButton.IconButton(Theme.Glyph.NewWindow, "Abrir cópia bloqueável", ButtonKind.Primary);
        var btnRefresh = RoundButton.IconButton(Theme.Glyph.Refresh, "Atualizar", ButtonKind.Secondary);
        btnBlock.Click += (_, _) => BlockSelected();
        btnUnblock.Click += (_, _) => UnblockSelected();
        btnClone.Click += (_, _) => OpenClone();
        btnRefresh.Click += (_, _) => RefreshAll();
        _btnPick.Click += (_, _) => StartPick();
        _pickTimer.Tick += PickTick;

        _tips.Set(btnBlock, "Bloquear internet",
            "Corta a internet do programa da janela selecionada. Vale para todas as janelas desse programa " +
            "e continua valendo mesmo com o HiddenApp fechado ou após reiniciar, até você liberar.\n\n" +
            "Atalho: duplo clique na janela da lista.");
        _tips.Set(btnUnblock, "Liberar internet", "Devolve a internet ao programa da janela selecionada.");
        _tips.Set(_btnPick, "Selecionar manualmente",
            "Clique aqui e, em até 3 segundos, clique na janela que você quer bloquear. " +
            "O HiddenApp identifica a janela, mostra a prévia e pergunta se deve bloquear.\n\n" +
            "Útil quando é difícil achar a janela certa na lista.");
        _tips.Set(btnClone, "Abrir cópia bloqueável",
            "Abre outra janela do programa a partir de uma cópia (que não ocupa espaço). " +
            "Só a cópia fica sem internet; o programa original continua normal.\n\n" +
            "Dá para ter várias cópias do mesmo programa, cada uma com nome e bloqueio próprios.");
        _tips.Set(btnRefresh, "Atualizar", "Recarrega a lista de janelas abertas.");

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        toolbar.Controls.AddRange(new Control[] { btnBlock, btnUnblock, _btnPick, btnClone, btnRefresh });

        // Botão direito numa janela
        var menu = new ContextMenuStrip();
        menu.Opening += (_, e) =>
        {
            menu.Items.Clear();
            var w = SelectedWindow;
            if (w?.ExePath is not string path) { e.Cancel = true; return; }
            bool blocked = FirewallManager.IsBlocked(path);
            menu.Items.Add(new ToolStripMenuItem(blocked ? "Liberar internet" : "Bloquear internet", null,
                (_, _) => { if (blocked) UnblockSelected(); else BlockSelected(); }) { Font = BoldFont });
            menu.Items.Add(IsFavorite(path)
                ? new ToolStripMenuItem("Remover dos favoritos", null, (_, _) => RemoveFavorite(path))
                : new ToolStripMenuItem("★ Adicionar aos favoritos", null, (_, _) => AddFavorite(path)));
            if (!CloneManager.IsClone(path))
                menu.Items.Add(new ToolStripMenuItem("Abrir cópia bloqueável deste programa...", null, (_, _) => OpenClone(path)));
            L.Apply(menu.Items);
        };
        _windows.ContextMenuStrip = menu;
        _windows.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _windows.HitTest(e.Location).Item;
            if (hit != null) hit.Selected = true;
        };

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
        _blockedList.DrawItem += (_, e) => DrawProgramItem(_blockedList, e);

        var btnUnblockSel = RoundButton.IconButton(Theme.Glyph.Unlock, "Liberar selecionado", ButtonKind.Success);
        var btnUnblockAll = RoundButton.IconButton(Theme.Glyph.SelectAll, "Liberar todos", ButtonKind.Secondary);
        var btnDeleteClones = RoundButton.IconButton(Theme.Glyph.Delete, "Apagar cópias", ButtonKind.Danger);
        btnUnblockSel.Click += (_, _) =>
        {
            if (_blockedList.SelectedItem is string path)
            {
                UnblockPath(path);
            }
        };
        btnUnblockAll.Click += (_, _) => UnblockAll();
        btnDeleteClones.Click += (_, _) => DeleteClones();

        _tips.Set(btnUnblockSel, "Liberar selecionado", "Devolve a internet ao programa selecionado nesta lista.");
        _tips.Set(btnUnblockAll, "Liberar todos", "Devolve a internet a todos os programas bloqueados de uma vez.");
        _tips.Set(btnDeleteClones, "Apagar cópias",
            "Apaga as cópias criadas pelo HiddenApp. Os programas originais não são afetados. " +
            "Cópias que estiverem abertas não são apagadas.");

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        toolbar.Controls.AddRange(new Control[] { btnUnblockSel, btnUnblockAll, btnDeleteClones });

        var menu = new ContextMenuStrip();
        menu.Opening += (_, e) =>
        {
            menu.Items.Clear();
            if (_blockedList.SelectedItem is not string path) { e.Cancel = true; return; }
            menu.Items.Add(new ToolStripMenuItem("Liberar internet", null, (_, _) => UnblockPath(path)) { Font = BoldFont });
            menu.Items.Add(IsFavorite(path)
                ? new ToolStripMenuItem("Remover dos favoritos", null, (_, _) => RemoveFavorite(path))
                : new ToolStripMenuItem("★ Adicionar aos favoritos", null, (_, _) => AddFavorite(path)));
            L.Apply(menu.Items);
        };
        _blockedList.ContextMenuStrip = menu;
        _blockedList.MouseDown += (_, e) => SelectOnRightClick(_blockedList, e);

        _blockedCard.Controls.Add(_blockedList);
        _blockedCard.Controls.Add(_emptyBlocked);
        _blockedCard.Controls.Add(toolbar);
    }

    void BuildFavoritesCard()
    {
        _favoritesList.DrawItem += (_, e) => DrawProgramItem(_favoritesList, e);
        _favoritesList.DoubleClick += (_, _) =>
        {
            if (SelectedFavorite is not string path) return;
            if (FirewallManager.IsBlocked(path)) UnblockPath(path); else BlockPath(path);
        };
        _favoritesList.MouseDown += (_, e) => SelectOnRightClick(_favoritesList, e);

        var btnBlock = RoundButton.IconButton(Theme.Glyph.Lock, "Bloquear", ButtonKind.Danger);
        var btnUnblock = RoundButton.IconButton(Theme.Glyph.Unlock, "Liberar", ButtonKind.Success);
        var btnOpen = RoundButton.IconButton(Theme.Glyph.Play, "Abrir", ButtonKind.Primary);
        var btnCopy = RoundButton.IconButton(Theme.Glyph.NewWindow, "Abrir cópia bloqueável", ButtonKind.Secondary);
        var btnAdd = RoundButton.IconButton(Theme.Glyph.Add, "Adicionar favorito", ButtonKind.Success);
        var btnRemove = RoundButton.IconButton(Theme.Glyph.Remove, "Remover dos favoritos", ButtonKind.Danger);
        btnCopy.Click += (_, _) => { if (SelectedFavorite is string f) OpenClone(f); else SetStatus("Selecione um favorito primeiro."); };
        btnBlock.Click += (_, _) => { if (SelectedFavorite is string f) BlockPath(f); else SetStatus("Selecione um favorito primeiro."); };
        btnUnblock.Click += (_, _) => { if (SelectedFavorite is string f) UnblockPath(f); else SetStatus("Selecione um favorito primeiro."); };
        btnOpen.Click += (_, _) => { if (SelectedFavorite is string f) LaunchProgram(f); else SetStatus("Selecione um favorito primeiro."); };
        btnAdd.Click += (_, _) => AddFavoriteBrowse();
        btnRemove.Click += (_, _) => { if (SelectedFavorite is string f) RemoveFavorite(f); else SetStatus("Selecione um favorito primeiro."); };

        _tips.Set(btnBlock, "Bloquear", "Corta a internet do favorito selecionado, mesmo que ele não esteja aberto agora.\n\nAtalho: duplo clique no favorito.");
        _tips.Set(btnUnblock, "Liberar", "Devolve a internet ao favorito selecionado.");
        _tips.Set(btnOpen, "Abrir programa", "Abre o programa selecionado. Cópias abrem com o mesmo perfil e as mesmas configurações de quando foram criadas.");
        _tips.Set(btnCopy, "Abrir cópia bloqueável",
            "Abre outra janela do favorito selecionado a partir de uma cópia (que não ocupa espaço), com bloqueio próprio. " +
            "Se o favorito já for uma cópia, escolha o programa original no diálogo.");
        _tips.Set(btnAdd, "Adicionar favorito",
            "Escolhe um programa (.exe) do computador para colocar nos favoritos.\n\n" +
            "Dica: também dá para clicar na estrela ao lado de uma janela da lista.");
        _tips.Set(btnRemove, "Remover dos favoritos", "Tira o programa dos favoritos. O bloqueio dele não muda.");

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        toolbar.Controls.AddRange(new Control[] { btnBlock, btnUnblock, btnOpen, btnCopy, btnAdd, btnRemove });

        var menu = new ContextMenuStrip();
        menu.Opening += (_, e) =>
        {
            menu.Items.Clear();
            if (SelectedFavorite is not string path) { e.Cancel = true; return; }
            bool blocked = FirewallManager.IsBlocked(path);
            menu.Items.Add(new ToolStripMenuItem(blocked ? "Liberar internet" : "Bloquear internet", null,
                (_, _) => { if (blocked) UnblockPath(path); else BlockPath(path); }) { Font = BoldFont });
            menu.Items.Add(new ToolStripMenuItem("Abrir programa", null, (_, _) => LaunchProgram(path)));
            if (!CloneManager.IsClone(path))
                menu.Items.Add(new ToolStripMenuItem("Abrir cópia bloqueável...", null, (_, _) => OpenClone(path)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Remover dos favoritos", null, (_, _) => RemoveFavorite(path)));
            L.Apply(menu.Items);
        };
        _favoritesList.ContextMenuStrip = menu;

        _favoritesCard.Controls.Add(_favoritesList);
        _favoritesCard.Controls.Add(_emptyFavorites);
        _favoritesCard.Controls.Add(toolbar);
    }

    static void SelectOnRightClick(ListBox list, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int i = list.IndexFromPoint(e.Location);
        if (i >= 0) list.SelectedIndex = i;
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
        _tabBar.BackColor = p.Surface;
        _autoClick.ApplyTheme();
        _appTitle.ForeColor = p.Text;
        _appSubtitle.ForeColor = p.Muted;
        _versionLabel.ForeColor = p.Muted;

        _statusBar.BackColor = p.Surface;
        _status.ForeColor = p.Muted;
        _statusCount.ForeColor = p.Muted;

        _body.BackColor = p.Back;
        foreach (var card in new[] { _windowsCard, _previewCard, _blockedCard, _favoritesCard })
            card.BackColor = p.Surface;

        _windows.BackColor = p.Surface;
        _windows.ForeColor = p.Text;
        _blockedList.BackColor = p.Surface;
        _blockedList.ForeColor = p.Text;
        _emptyBlocked.ForeColor = p.Muted;
        _bottom.BackColor = p.Back;
        _favoritesList.BackColor = p.Surface;
        _favoritesList.ForeColor = p.Text;
        _emptyFavorites.ForeColor = p.Muted;

        _preview.BackColor = p.Back;
        _previewTitle.ForeColor = p.Text;
        _previewSub.ForeColor = p.Muted;

        Theme.ApplyTitleBar(this);
        Theme.ApplyScrollbars(_windows);
        Theme.ApplyScrollbars(_blockedList);
        Theme.ApplyScrollbars(_favoritesList);
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    // =====================================================================
    //  Abas
    // =====================================================================
    void BuildTabs()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(Theme.S(14), Theme.S(2), 0, 0) };
        flow.Controls.Add(_tabInternet);
        flow.Controls.Add(_tabClicker);
        _tabBar.Controls.Add(flow);
        _tabBar.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, _tabBar.Height - 1, _tabBar.Width, _tabBar.Height - 1); };
        _tabBar.Padding = new Padding(0, 0, 0, 1);
        _tabInternet.Click += (_, _) => SelectTab(0);
        _tabClicker.Click += (_, _) => SelectTab(1);
        _tips.Set(_tabInternet, "Internet", "Bloquear e liberar a internet de programas, cópias bloqueáveis e favoritos.");
        _tips.Set(_tabClicker, "Macro",
            "Macros que clicam e apertam teclas sozinhas numa janela, em loop — na hora ou num horário agendado. Iniciar, parar e capturar ponto têm teclas de atalho.");

        _autoClick.StatusMessage += SetStatus;
        _autoClick.Notify += (title, text) => ShowBalloon(title, text);
        _autoClick.RunningChanged += UpdateTrayText;
        _autoClick.HotkeysRequested += EditHotkeys;
    }

    void SelectTab(int index)
    {
        index = index == 1 ? 1 : 0;
        _tabInternet.Selected = index == 0;
        _tabClicker.Selected = index == 1;
        _body.Visible = index == 0;
        _autoClick.Visible = index == 1;
        if (_settings.LastTab != index) { _settings.LastTab = index; _settings.Save(); }
        if (index == 0) { RefreshAll(); }
        SetStatus(index == 0
            ? "Selecione uma janela e clique em \"Bloquear internet\". Passe o mouse nos botões para ver o que cada um faz."
            : $"Macro: crie uma macro, escolha a janela alvo e adicione os passos (cliques e teclas). {HotkeyBox.Describe((Keys)_settings.HotkeyStart)} inicia, {HotkeyBox.Describe((Keys)_settings.HotkeyStop)} para.");
    }

    // =====================================================================
    //  Teclas de atalho globais (funcionam em qualquer programa)
    // =====================================================================
    bool RegisterOne(int id, Keys keys)
    {
        Native.UnregisterHotKey(Handle, id);
        uint mods = Native.MOD_NOREPEAT;
        if (keys.HasFlag(Keys.Control)) mods |= Native.MOD_CONTROL;
        if (keys.HasFlag(Keys.Alt)) mods |= Native.MOD_ALT;
        if (keys.HasFlag(Keys.Shift)) mods |= Native.MOD_SHIFT;
        return Native.RegisterHotKey(Handle, id, mods, (uint)(keys & Keys.KeyCode));
    }

    void RegisterHotkeys(bool showErrors)
    {
        var failed = new List<string>();
        var start = (Keys)_settings.HotkeyStart;
        var stop = (Keys)_settings.HotkeyStop;
        var capture = (Keys)_settings.HotkeyCapture;
        if (!RegisterOne(HotkeyIdStart, start)) failed.Add($"{HotkeyBox.Describe(start)} (iniciar)");
        if (!RegisterOne(HotkeyIdStop, stop)) failed.Add($"{HotkeyBox.Describe(stop)} (parar)");
        if (!RegisterOne(HotkeyIdCapture, capture)) failed.Add($"{HotkeyBox.Describe(capture)} (capturar ponto)");
        _autoClick.SetHotkeyLabels(HotkeyBox.Describe(start), HotkeyBox.Describe(stop), HotkeyBox.Describe(capture));

        if (failed.Count == 0) return;
        string msg = $"Estas teclas já estão sendo usadas por outro programa e não funcionarão como atalho: {string.Join(", ", failed)}. Troque em Macro → Teclas de atalho.";
        if (showErrors) Msg.Show(msg, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else SetStatus(msg);
    }

    void UnregisterHotkeys()
    {
        if (!IsHandleCreated) return;
        Native.UnregisterHotKey(Handle, HotkeyIdStart);
        Native.UnregisterHotKey(Handle, HotkeyIdStop);
        Native.UnregisterHotKey(Handle, HotkeyIdCapture);
    }

    void EditHotkeys()
    {
        using var dlg = new HotkeyDialog((Keys)_settings.HotkeyStart, (Keys)_settings.HotkeyStop, (Keys)_settings.HotkeyCapture);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _settings.HotkeyStart = (int)dlg.StartKey;
        _settings.HotkeyStop = (int)dlg.StopKey;
        _settings.HotkeyCapture = (int)dlg.CaptureKey;
        _settings.Save();
        RegisterHotkeys(showErrors: true);
        SetStatus($"Teclas de atalho: iniciar {HotkeyBox.Describe(dlg.StartKey)}, parar {HotkeyBox.Describe(dlg.StopKey)}, " +
                  $"capturar ponto {HotkeyBox.Describe(dlg.CaptureKey)}.");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && _autoClick != null)
        {
            switch ((int)m.WParam)
            {
                case HotkeyIdStart: _autoClick.StartSelected(); break;
                case HotkeyIdStop: _autoClick.StopRunning(); break;
                case HotkeyIdCapture: _autoClick.CaptureFromCursor(); break;
            }
            return;
        }
        base.WndProc(ref m);
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
        _tray.BalloonTipClicked += (_, _) => { if (_balloonIsUpdate && _update != null) ShowUpdateDialog(); };
    }

    void BuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(new ToolStripMenuItem("Abrir HiddenApp", null, (_, _) => ShowFromTray()) { Font = BoldFont });
        if (_autoClick.IsRunning)
            _trayMenu.Items.Add(new ToolStripMenuItem(_autoClick.RunningCount > 1
                    ? $"■ Parar todas as macros ({HotkeyBox.Describe((Keys)_settings.HotkeyStop)})"
                    : $"■ Parar macro ({HotkeyBox.Describe((Keys)_settings.HotkeyStop)})", null,
                (_, _) => _autoClick.StopRunning()) { Font = BoldFont, ForeColor = Theme.Current.Danger });
        // Iniciar macros pela bandeja (dá para rodar outras junto com as que já estão rodando)
        if (_autoClick.Macros.Count > 0)
        {
            var clickMenu = new ToolStripMenuItem("Macros");
            foreach (var macro in _autoClick.Macros)
            {
                var mm = macro;
                bool running = _autoClick.IsMacroRunning(mm);
                // Rodando: aparece marcada e o clique para só ela; parada: o clique inicia
                clickMenu.DropDownItems.Add(new ToolStripMenuItem($"{(running ? "■" : mm.Favorite ? "★" : "▶")} {mm.Name}", null,
                    (_, _) => { if (running) _autoClick.StopMacro(mm); else _autoClick.StartMacro(mm); })
                { ToolTipText = running ? L.T("Rodando — clique para parar") : $"{mm.TargetName} · {mm.Steps.Count} passo(s)", Checked = running });
            }
            clickMenu.DropDownItems.Add(new ToolStripSeparator());
            clickMenu.DropDownItems.Add(new ToolStripMenuItem("Abrir a aba Macro", null, (_, _) => { ShowFromTray(); SelectTab(1); }));
            _trayMenu.Items.Add(clickMenu);
        }
        if (_update != null)
            _trayMenu.Items.Add(new ToolStripMenuItem($"Atualizar para a versão {_update.Version}", null, (_, _) => ShowUpdateDialog())
            { Font = BoldFont, ForeColor = Theme.Current.Accent });
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
                    ShowBalloon("Internet liberada", label);
                }));
            }
            _trayMenu.Items.Add(sub);
            _trayMenu.Items.Add(new ToolStripMenuItem("Liberar todos", null, (_, _) => UnblockAll()));
        }

        if (_settings.Favorites.Count > 0)
        {
            var favMenu = new ToolStripMenuItem($"Favoritos ({_settings.Favorites.Count})");
            foreach (var path in _settings.Favorites.OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                bool favBlocked = FirewallManager.IsBlocked(path);
                string favName = DisplayName(path);
                var item = new ToolStripMenuItem(favName) { Checked = favBlocked, ToolTipText = favBlocked ? "Sem internet" : "Com internet" };
                item.DropDownItems.Add(new ToolStripMenuItem(favBlocked ? "Liberar internet" : "Bloquear internet", null, (_, _) =>
                {
                    if (favBlocked) UnblockPath(path); else BlockPath(path);
                    ShowBalloon(favBlocked ? "Internet liberada" : "Internet bloqueada", favName);
                }));
                item.DropDownItems.Add(new ToolStripMenuItem("Abrir programa", null, (_, _) => LaunchProgram(path)));
                favMenu.DropDownItems.Add(item);
            }
            _trayMenu.Items.Add(favMenu);
        }

        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem("Tema escuro", null, (_, _) => _darkToggle.Checked = !_darkToggle.Checked) { Checked = _darkToggle.Checked });
        _trayMenu.Items.Add(new ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => _startupToggle.Checked = !_startupToggle.Checked) { Checked = _startupToggle.Checked });
        _trayMenu.Items.Add(new ToolStripMenuItem("Verificar atualizações", null, async (_, _) => await CheckForUpdatesAsync(manual: true)));
        var feedback = new ToolStripMenuItem("Enviar sugestão ou comentário");
        feedback.DropDownItems.AddRange(Feedback.MenuItems());
        _trayMenu.Items.Add(feedback);
        _trayMenu.Items.Add(new ToolStripMenuItem("❤ Apoiar o projeto", null, (_, _) => ShowSupport()));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem("Desinstalar (remover tudo do computador)...", null, (_, _) => Uninstall()));
        _trayMenu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => ExitApp()));
        L.Apply(_trayMenu.Items);
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
                ShowBalloon("O HiddenApp continua rodando",
                    "Ele fica aqui perto do relógio. Clique com o botão direito no ícone para sair.");
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
        _autoClick.StopRunning();
        UnregisterHotkeys();
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
            Msg.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void ShowBalloon(string title, string text, bool isUpdate = false)
    {
        _balloonIsUpdate = isUpdate;
        _tray.ShowBalloonTip(isUpdate ? 10000 : 4000, L.T(title), L.T(text), ToolTipIcon.Info);
    }

    void UpdateTrayText()
    {
        int n = FirewallManager.Blocked.Count;
        string tip = n == 0 ? "HiddenApp" : $"HiddenApp - {n} bloqueado(s)";
        if (_autoClick?.RunningCount > 1) tip = $"HiddenApp - {_autoClick.RunningCount} macros rodando";
        else if (_autoClick?.IsRunning == true) tip = $"HiddenApp - macro: {_autoClick.RunningName}";
        if (_update != null) tip += $" - nova versão {_update.Version}";
        tip = L.T(tip);
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    // =====================================================================
    //  Atualizações
    // =====================================================================
    async Task StartupUpdateCheckAsync()
    {
        if (_justUpdated)
        {
            await Task.Delay(1500);
            ShowBalloon("HiddenApp atualizado", $"Você está usando a versão {UpdateManager.CurrentVersion}.");
            SetStatus($"HiddenApp atualizado para a versão {UpdateManager.CurrentVersion}.");
        }
        await Task.Delay(TimeSpan.FromSeconds(8)); // não atrasa a abertura do app
        await CheckForUpdatesAsync(manual: false);
    }

    /// <summary>
    /// Consulta o GitHub. Automático: só pergunta uma vez por versão a cada 24h (e, se a janela
    /// estiver escondida, avisa pela bandeja). Manual: sempre mostra o resultado.
    /// </summary>
    async Task CheckForUpdatesAsync(bool manual)
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        try
        {
            if (manual) SetStatus("Verificando atualizações...");
            var update = await UpdateManager.CheckAsync(_settings.IncludePrereleases);
            SetAvailableUpdate(update);

            if (update == null)
            {
                if (manual)
                {
                    SetStatus("Você já está usando a versão mais recente.");
                    Msg.Show(this, $"Você já está usando a versão mais recente ({UpdateManager.CurrentVersion}).",
                        "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (manual) { ShowUpdateDialog(); return; }

            bool alreadyAsked = _settings.LastPromptedVersion == update.Version.ToString() &&
                                DateTime.UtcNow - _settings.LastPromptedUtc < TimeSpan.FromHours(24);
            if (alreadyAsked) return;

            if (Visible && WindowState != FormWindowState.Minimized)
                ShowUpdateDialog();
            else
            {
                MarkPrompted(update);
                ShowBalloon($"Nova versão do HiddenApp: {update.Version}",
                    "Clique aqui para ver as novidades e atualizar.", isUpdate: true);
            }
        }
        catch (Exception ex)
        {
            // Sem internet / GitHub fora do ar: no automático fica em silêncio
            if (manual)
            {
                SetStatus("Não foi possível verificar atualizações.");
                Msg.Show(this, $"Não foi possível verificar atualizações:\n{ex.Message}",
                    "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally { _checkingUpdates = false; }
    }

    /// <summary>Mostra/esconde os avisos permanentes de versão nova (cabeçalho, bandeja, status).</summary>
    void SetAvailableUpdate(UpdateInfo? update)
    {
        _update = update;
        _btnUpdate.Visible = update != null;
        if (update != null)
        {
            _btnUpdate.Text = update.IsPrerelease ? $"Nova versão beta {update.Version}" : $"Nova versão {update.Version}";
            _versionLabel.Text = $"v{UpdateManager.CurrentVersion}  ·  atualização disponível";
        }
        else _versionLabel.Text = "v" + UpdateManager.CurrentVersion;
        _header.PerformLayout();
        UpdateTrayText();
    }

    void MarkPrompted(UpdateInfo update)
    {
        _settings.LastPromptedVersion = update.Version.ToString();
        _settings.LastPromptedUtc = DateTime.UtcNow;
        _settings.Save();
    }

    void ShowUpdateDialog()
    {
        if (_update == null || _updateDialogOpen) return;
        _updateDialogOpen = true;
        try
        {
            if (!Visible) ShowFromTray();
            MarkPrompted(_update);
            using var dlg = new UpdateDialog(_update);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ExitApp(); // a versão nova já foi aberta e assume em seguida
                return;
            }
            // "Depois": os avisos continuam no cabeçalho e na bandeja
            SetStatus($"Versão {_update.Version} disponível. Clique em \"Nova versão\" no topo quando quiser atualizar.");
        }
        finally { _updateDialogOpen = false; }
    }

    // =====================================================================
    //  Idioma
    // =====================================================================
    void ShowLanguageMenu()
    {
        _languageMenu.Items.Clear();
        string current = _settings.Language;
        _languageMenu.Items.Add(new ToolStripMenuItem("Automático (idioma do Windows)", null, (_, _) => ChangeLanguage(L.Auto))
        { Checked = current == L.Auto });
        _languageMenu.Items.Add(new ToolStripSeparator());
        L.Apply(_languageMenu.Items);
        foreach (var (code, name) in L.Languages)
            _languageMenu.Items.Add(new ToolStripMenuItem(name, null, (_, _) => ChangeLanguage(code)) { Checked = current == code });
        _languageMenu.Show(_btnLanguage, new Point(0, _btnLanguage.Height));
    }

    void ChangeLanguage(string code)
    {
        if (_settings.Language == code) return;
        _settings.Language = code;
        _settings.Save();
        if (Msg.Show("O novo idioma será usado quando o HiddenApp reiniciar. Reiniciar agora?", "HiddenApp",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (_autoClick.IsRunning) _autoClick.StopRunning();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Environment.ProcessPath ?? Application.ExecutablePath, Program.RestartArgument) { UseShellExecute = false });
            ExitApp();
        }
        catch (Exception ex) { Msg.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // =====================================================================
    //  Desinstalar: desfaz tudo o que o HiddenApp mudou no Windows
    // =====================================================================
    void Uninstall()
    {
        if (!Visible) ShowFromTray();
        if (Msg.Show(this,
                "Isto desfaz tudo o que o HiddenApp mudou no computador:\n\n" +
                "• remove todas as regras de firewall criadas por ele (a internet volta para todos os programas);\n" +
                "• apaga as cópias bloqueáveis (os programas originais não são afetados);\n" +
                "• remove a inicialização com o Windows.\n\n" +
                "Feche as cópias abertas antes. Continuar?",
                "Desinstalar o HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        bool wipeData = Msg.Show(this,
                "Apagar também suas configurações, favoritos e macros?\n\n" +
                "Escolha \"Não\" se pretende usar o HiddenApp de novo depois.",
                "Desinstalar o HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        Cursor = Cursors.WaitCursor;
        _autoClick.StopRunning();
        int inUse;
        try
        {
            FirewallManager.UnblockAll();              // antes de apagar as cópias (precisa listar os .exe delas)
            (_, inUse) = CloneManager.DeleteAllClones();
            FirewallManager.RemoveAllRules();          // sobras com o nome "HiddenApp - ..." (ex.: lista apagada à mão)
            if (StartupManager.IsEnabled()) StartupManager.Disable();
        }
        finally { Cursor = Cursors.Default; }

        Program.WipeDataOnExit = wipeData;
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        Msg.Show(this,
            (inUse > 0 ? L.F("{0} cópia(s) não puderam ser apagadas porque ainda estão abertas. Feche-as e apague a pasta HiddenAppClones manualmente.", inUse) + "\n\n" : "") +
            L.T("Pronto. O HiddenApp vai fechar e a pasta dele será aberta: para terminar, apague o arquivo HiddenApp.exe."),
            "Desinstalar o HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Information);

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Native.StartUnelevated(Path.Combine(winDir, "explorer.exe"), $"/select,\"{exe}\"", winDir);
        ExitApp();
    }

    void ShowFeedbackMenu()
    {
        _feedbackMenu.Items.Clear();
        _feedbackMenu.Items.AddRange(Feedback.MenuItems());
        L.Apply(_feedbackMenu.Items);
        _feedbackMenu.Show(_btnFeedback, new Point(0, _btnFeedback.Height));
    }

    void ShowSupport()
    {
        if (!Visible) ShowFromTray();
        using var dlg = new SupportDialog();
        dlg.ShowDialog(this);
    }

    void BuildUpdatesMenu()
    {
        _updatesMenu.Items.Clear();
        if (_update != null)
            _updatesMenu.Items.Add(new ToolStripMenuItem($"Atualizar para a versão {_update.Version}", null, (_, _) => ShowUpdateDialog())
            { Font = BoldFont });
        _updatesMenu.Items.Add(new ToolStripMenuItem("Verificar atualizações agora", null, async (_, _) => await CheckForUpdatesAsync(manual: true)));
        _updatesMenu.Items.Add(new ToolStripMenuItem("Receber versões beta (pré-lançamento)", null, async (_, _) =>
        {
            _settings.IncludePrereleases = !_settings.IncludePrereleases;
            _settings.LastPromptedVersion = null; // reavalia com a nova opção
            _settings.Save();
            SetStatus(_settings.IncludePrereleases
                ? "Você vai receber também as versões beta."
                : "Você vai receber só as versões estáveis.");
            await CheckForUpdatesAsync(manual: false);
        }) { Checked = _settings.IncludePrereleases });
        _updatesMenu.Items.Add(new ToolStripSeparator());
        _updatesMenu.Items.Add(new ToolStripMenuItem($"Versão atual: {UpdateManager.CurrentVersion}") { Enabled = false });
        _updatesMenu.Items.Add(new ToolStripMenuItem("Ver todas as versões no GitHub", null, (_, _) => UpdateManager.OpenUrl(UpdateManager.ReleasesPage)));
        _updatesMenu.Items.Add(new ToolStripSeparator());
        _updatesMenu.Items.Add(new ToolStripMenuItem("Desinstalar (remover tudo do computador)...", null, (_, _) => Uninstall()));
        L.Apply(_updatesMenu.Items);
    }

    // =====================================================================
    //  Listas
    // =====================================================================
    void SetStatus(string text) => _status.Text = L.T(text);

    void RefreshAll()
    {
        _refreshing = true;
        try
        {
            IntPtr selected = SelectedWindow?.Handle ?? IntPtr.Zero;
            _nameCache.Clear();

            _windows.BeginUpdate();
            _windows.Items.Clear();
            foreach (var w in WindowEnumerator.GetOpenWindows().OrderBy(w => w.ProcessName))
            {
                string status = w.ExePath == null ? "SEM ACESSO" : FirewallManager.IsBlocked(w.ExePath) ? "BLOQUEADA" : "LIBERADA";
                bool clone = CloneManager.IsClone(w.ExePath);
                string? cloneLabel = clone ? CloneManager.LabelOf(w.ExePath!) : null;
                var item = new ListViewItem(new[] { "", w.Title, w.ProcessName, w.Pid.ToString(), status })
                {
                    Tag = new RowData(w, status, clone, cloneLabel)
                };
                _windows.Items.Add(item);
            }
            _windows.EndUpdate();
            if (selected != IntPtr.Zero) SelectWindowInList(selected);

            _blockedList.BeginUpdate();
            _blockedList.Items.Clear();
            foreach (var path in FirewallManager.Blocked.OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase))
                _blockedList.Items.Add(path);
            _blockedList.EndUpdate();
            bool any = _blockedList.Items.Count > 0;
            _blockedList.Visible = any;
            _emptyBlocked.Visible = !any;

            string? selectedFav = SelectedFavorite;
            _favoritesList.BeginUpdate();
            _favoritesList.Items.Clear();
            foreach (var path in _settings.Favorites.OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase))
                _favoritesList.Items.Add(path);
            _favoritesList.EndUpdate();
            if (selectedFav != null) _favoritesList.SelectedItem = selectedFav;
            bool anyFav = _favoritesList.Items.Count > 0;
            _favoritesList.Visible = anyFav;
            _emptyFavorites.Visible = !anyFav;

            int n = FirewallManager.Blocked.Count;
            _statusCount.Text = n == 0 ? "Nenhum programa bloqueado" : $"{n} programa(s) sem internet";
            UpdateTrayText();
        }
        finally { _refreshing = false; }

        FitColumns();
        UpdatePreview();
    }

    void FitColumns()
    {
        if (_windows.Columns.Count < 5) return;
        int others = _windows.Columns[0].Width + _windows.Columns[2].Width + _windows.Columns[3].Width + _windows.Columns[4].Width;
        _windows.Columns[1].Width = Math.Max(Theme.S(150), _windows.ClientSize.Width - others - 1);
    }

    void UpdatePreview()
    {
        var w = SelectedWindow;
        _preview.SetSource(w?.Handle ?? IntPtr.Zero);
        _previewTitle.Text = w == null ? "Nenhuma janela selecionada" : w.Title;
        _previewSub.Text = w == null ? "Clique numa janela da lista para ver a prévia aqui."
                                     : $"{w.ProcessName}  ·  PID {w.Pid}" + (CloneManager.IsClone(w.ExePath) ? "  ·  " + L.T("cópia") : "");
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
                if (row.Window.ExePath is string exe)
                {
                    bool fav = IsFavorite(exe);
                    bool hover = e.ItemIndex == _hoverStarRow;
                    Color starColor = fav ? p.Favorite
                        : hover ? Theme.Blend(p.Favorite, p.Muted, 0.5)
                        : Theme.Blend(p.Muted, bg, 0.45);
                    using var starBrush = new SolidBrush(starColor);
                    Gfx.FillStar(g, starBrush, new PointF(bounds.Left + bounds.Width / 2f + Theme.S(2), bounds.Top + bounds.Height / 2f), Theme.S(8));
                }
                break;

            case 1:
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

            case 2:
                var r1 = new Rectangle(bounds.Left + Theme.S(12), bounds.Top, bounds.Width - Theme.S(16), bounds.Height);
                TextRenderer.DrawText(g, row.Window.ProcessName, RowFont, r1, p.Text, flags);
                if (row.IsClone)
                {
                    int w = TextRenderer.MeasureText(row.Window.ProcessName, RowFont).Width;
                    DrawPill(g, PillText(row.CloneLabel ?? "Cópia"), p.Accent, bg, bounds.Left + Theme.S(16) + w, bounds);
                }
                break;

            case 3:
                TextRenderer.DrawText(g, row.Window.Pid.ToString(), RowFont,
                    new Rectangle(bounds.Left + Theme.S(12), bounds.Top, bounds.Width - Theme.S(16), bounds.Height), p.Muted, flags);
                break;

            case 4:
                Color c = row.Status switch { "BLOQUEADA" => p.Danger, "LIBERADA" => p.Success, _ => p.Muted };
                DrawPill(g, row.Status, c, bg, bounds.Left + Theme.S(12), bounds);
                break;
        }
    }

    static void DrawPill(Graphics g, string text, Color color, Color bg, int x, Rectangle cell)
    {
        text = L.T(text);
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

    // ---------- Desenho das listas de programas (bloqueados e favoritos) ----------
    static string PillText(string text)
    {
        text = text.ToUpperInvariant();
        return text.Length > 18 ? text[..17] + "…" : text;
    }

    /// <summary>Nome amigável do programa (ex.: "Google Chrome") + nome da cópia, se for uma.</summary>
    string DisplayName(string path)
    {
        if (_nameCache.TryGetValue(path, out var cached)) return cached;
        string name;
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            name = string.IsNullOrWhiteSpace(info.FileDescription) ? Path.GetFileNameWithoutExtension(path) : info.FileDescription.Trim();
        }
        catch { name = Path.GetFileNameWithoutExtension(path); }
        if (CloneManager.IsClone(path)) name += $" — {CloneManager.LabelOf(path)}";
        _nameCache[path] = name;
        return name;
    }

    void DrawProgramItem(ListBox list, DrawItemEventArgs e)
    {
        if (e.Index < 0 || list.Items[e.Index] is not string path) return;
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

        // Etiqueta de situação
        bool blocked = FirewallManager.IsBlocked(path);
        bool exists = File.Exists(path);
        (string pill, Color pillColor) = blocked ? (L.T("SEM INTERNET"), p.Danger)
                                       : exists ? (L.T("COM INTERNET"), p.Success)
                                       : (L.T("NÃO ENCONTRADO"), p.Muted);
        int pillW = TextRenderer.MeasureText(pill, PillFont).Width + Theme.S(10);

        string name = DisplayName(path);
        int line1 = BoldFont.Height + Theme.S(4);
        int top = e.Bounds.Top + (e.Bounds.Height - line1 - SmallFont.Height) / 2;
        var firstLine = new Rectangle(e.Bounds.Left, top, e.Bounds.Width, line1);
        int nameMax = e.Bounds.Width - (textX - e.Bounds.Left) - pillW - Theme.S(16);
        int nameW = Math.Min(TextRenderer.MeasureText(name, BoldFont).Width, Math.Max(Theme.S(60), nameMax));
        TextRenderer.DrawText(g, name, BoldFont, new Rectangle(textX, top, nameW, line1), p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        DrawPill(g, pill, pillColor, bg, textX + nameW + Theme.S(6), firstLine);

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
            Msg.Show("Não foi possível descobrir o executável dessa janela.", "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Aviso: o bloqueio vale para o programa inteiro, não só para esta janela
        var others = WindowEnumerator.GetOpenWindows()
            .Count(x => x.Handle != w.Handle && string.Equals(x.ExePath, w.ExePath, StringComparison.OrdinalIgnoreCase));
        if (others > 0)
        {
            var r = Msg.Show(
                $"O programa \"{w.ProcessName}\" tem mais {others} janela(s) aberta(s).\n" +
                "O bloqueio de internet vale para o programa inteiro, então todas elas ficarão sem internet.\n\n" +
                "Dica: use \"Abrir cópia bloqueável...\" para bloquear só uma janela.\n\nContinuar?",
                "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
        }

        BlockPath(w.ExePath, w.ProcessName);
    }

    /// <summary>Bloqueia um programa pelo caminho (usado pela lista de janelas, favoritos e bandeja).</summary>
    void BlockPath(string path, string? name = null)
    {
        name ??= DisplayName(path);
        try
        {
            Cursor = Cursors.WaitCursor;
            FirewallManager.Block(path);
            // Derruba também as conexões que já estavam abertas
            int killed = Native.KillTcpConnections(WindowEnumerator.GetPidsForExes(FirewallManager.TargetsFor(path)));
            SetStatus(L.F("Internet bloqueada para {0}.", name) +
                      (killed > 0 ? " " + L.F("({0} conexão(ões) aberta(s) derrubada(s))", killed) : ""));
        }
        catch (Exception ex)
        {
            Msg.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
        RefreshAll();
    }

    void UnblockPath(string path)
    {
        FirewallManager.Unblock(path);
        SetStatus($"Internet liberada para {DisplayName(path)}.");
        RefreshAll();
    }

    // ---------- Favoritos ----------
    bool _lastClickOnStar;
    int _hoverStarRow = -1;

    /// <summary>O ponto está na coluna da estrela (primeira coluna da lista de janelas)?</summary>
    bool IsStarArea(Point pt) => _windows.Columns.Count > 0 && pt.X >= 0 && pt.X < _windows.Columns[0].Width;

    void ToggleFavorite(string path)
    {
        if (IsFavorite(path)) RemoveFavorite(path); else AddFavorite(path);
    }

    string? SelectedFavorite => _favoritesList.SelectedItem as string;

    bool IsFavorite(string path) => _settings.Favorites.Contains(path, StringComparer.OrdinalIgnoreCase);

    void AddFavorite(string path)
    {
        if (!IsFavorite(path))
        {
            _settings.Favorites.Add(path);
            _settings.Save();
        }
        RefreshAll();
        _favoritesList.SelectedItem = _settings.Favorites.First(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));
        SetStatus($"{DisplayName(path)} está nos favoritos.");
    }

    void RemoveFavorite(string path)
    {
        _settings.Favorites.RemoveAll(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));
        _settings.Save();
        RefreshAll();
        SetStatus($"{DisplayName(path)} saiu dos favoritos.");
    }

    void AddFavoriteBrowse()
    {
        using var dlg = new OpenFileDialog { Filter = L.T("Programas (*.exe)|*.exe"), Title = L.T("Escolha um programa para os favoritos") };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddFavorite(dlg.FileName);
    }

    /// <summary>Abre um programa (sem privilégio de admin). Cópias abrem com os argumentos salvos.</summary>
    void LaunchProgram(string path)
    {
        if (!File.Exists(path))
        {
            Msg.Show($"O programa não foi encontrado nesse caminho:\n{path}", "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string args = CloneManager.IsClone(path) ? CloneManager.ArgsOf(path) : "";
        try
        {
            CloneManager.Launch(path, args);
            SetStatus($"Abrindo {DisplayName(path)}...");
        }
        catch (Exception ex)
        {
            Msg.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
    async void OpenClone(string? program = null)
    {
        string? initial = program ?? SelectedWindow?.ExePath;
        if (CloneManager.IsClone(initial)) initial = null;

        using var dlg = new CloneDialog(initial);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string exe = dlg.ExePath, args = dlg.Arguments, copyName = dlg.CopyName;
        int copyNumber = dlg.CopyNumber;
        bool startBlocked = dlg.StartBlocked;

        try
        {
            UseWaitCursor = true;
            SetStatus("Verificando a pasta do programa...");
            int count = await Task.Run(() => CloneManager.CountFiles(exe));
            if (CloneManager.TooManyFiles(count) &&
                Msg.Show("A pasta desse programa tem muitos arquivos (mais de 30.000). " +
                                "A cópia pode demorar alguns minutos. Continuar?",
                                "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                SetStatus("Cancelado.");
                return;
            }

            var progress = new Progress<string>(SetStatus);
            var res = await Task.Run(() => CloneManager.CreateOrUpdate(exe, copyNumber, progress));
            CloneManager.SaveMeta(res.CloneExe, copyName, args);

            if (startBlocked)
            {
                // Refaz as regras: o programa pode ter sido atualizado e ganhado .exe novos
                if (FirewallManager.IsBlocked(res.CloneExe)) FirewallManager.Unblock(res.CloneExe);
                FirewallManager.Block(res.CloneExe);
            }

            SetStatus("Abrindo a cópia...");
            CloneManager.Launch(res.CloneExe, args);

            string info = L.F("{0} arquivos linkados", res.Linked) +
                          (res.Copied > 0 ? L.F(", {0} copiados", res.Copied) : "") +
                          (res.Failed > 0 ? L.F(", {0} com erro", res.Failed) : "");
            SetStatus(L.F("{0} aberta ({1}).", CloneManager.LabelOf(res.CloneExe), info) + (startBlocked ? " " + L.T("Ela já está sem internet.") : ""));

            // Confere se o programa abriu mesmo uma instância separada
            await Task.Delay(4000);
            if (WindowEnumerator.GetPidsForExes(FirewallManager.TargetsFor(res.CloneExe)).Count == 0)
            {
                Msg.Show(
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
            Msg.Show(ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            RefreshAll();
        }
    }

    void DeleteClones()
    {
        if (Msg.Show("Apagar todas as cópias criadas pelo HiddenApp?\n\n" +
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

    // ---------- "Selecionar manualmente": clica no botão e depois clica na janela desejada ----------
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

        var r = Msg.Show($"Bloquear a internet de:\n\n\"{w.Title}\"\n({w.ProcessName})?",
            "HiddenApp", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r == DialogResult.Yes) Block(w);
        else RefreshAll();
    }
}
