namespace HiddenApp;

/// <summary>
/// Aba "Macro". Layout em três colunas, sem rolagem:
///   [Macros + configuração da macro] [Passos + passo selecionado] [Prévia da janela alvo]
/// e uma barra fixa embaixo com Iniciar / Parar / Testar / Teclas de atalho.
/// Passos são cliques num ponto da janela ou teclas. Macros podem iniciar sozinhas num horário.
/// </summary>
public class AutoClickPage : Panel
{
    readonly AppSettings _settings;
    readonly ThemedToolTip _tips;
    readonly List<Macro> _macros = MacroStore.Load();
    /// <summary>Uma macro rodando (ou um teste de passo).</summary>
    sealed class RunState
    {
        public required Macro Macro { get; init; }
        public required ClickRunner Runner { get; init; }
        public bool IsTest { get; init; }
        public string Text { get; set; } = "";
        public bool Paused => Text.StartsWith("Pausado", StringComparison.Ordinal);
    }

    /// <summary>Macros rodando agora. Várias em segundo plano ao mesmo tempo; no modo real, só uma.</summary>
    readonly List<RunState> _running = new();
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 600 };
    readonly System.Windows.Forms.Timer _scheduleTimer = new() { Interval = 10_000 };

    Macro? _current;
    IntPtr _targetHandle;
    bool _loading;
    enum PickKind { None, Add, Move }
    PickKind _pick;
    string _startKey = "F7", _stopKey = "F8", _captureKey = "F9";

    /// <summary>Ordem dos modos na lista (índice da lista → modo).</summary>
    static readonly ClickMode[] ModeOrder = { ClickMode.SegundoPlano, ClickMode.Real };

    // ---------- Layout ----------
    readonly TableLayoutPanel _grid = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
    readonly Card _settingsCard = new("Macro");
    readonly Card _pointsCard = new("Passos", "rodam em loop, na ordem");
    readonly Card _previewCard = new("Janela alvo");
    readonly Card _runCard = new("Em execução");
    readonly TableLayoutPanel _rightColumn = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
    readonly ListBox _runList = NewList(46);
    readonly Label _emptyRun = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted",
        Text = "Nenhuma macro rodando.\nDá para rodar várias ao mesmo tempo\n(no modo real, só uma por vez)." };
    readonly RoundButton _btnStopSel = RoundButton.IconButton(Theme.Glyph.Stop, "Parar a macro selecionada", ButtonKind.Danger);
    readonly RoundButton _btnStopAll = RoundButton.IconTextButton(Theme.Glyph.Stop, "Parar todas", ButtonKind.Secondary);
    readonly Panel _actionBar = new() { Dock = DockStyle.Bottom, Height = Theme.S(58), Padding = new Padding(Theme.S(7), Theme.S(8), Theme.S(7), 0) };

    // Caixa de seleção de macros (ocupa pouco espaço; os itens mostram estrela, nome, alvo e agendamento)
    readonly ComboBox _macroList = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Top,
        DrawMode = DrawMode.OwnerDrawVariable, ItemHeight = Theme.S(42), MaxDropDownItems = 12,
        Margin = new Padding(0), IntegralHeight = false
    };
    readonly RoundButton _btnFavorite = RoundButton.IconButton(Theme.Glyph.Star, "Favoritar macro", ButtonKind.Secondary);
    readonly Label _emptyMacros = new() { Dock = DockStyle.Top, Height = Theme.S(64), TextAlign = ContentAlignment.MiddleCenter, Tag = "muted",
        Text = "Nenhuma macro ainda.\nClique no + verde para criar a primeira\nou importe uma macro recebida." };

    readonly TableLayoutPanel _settingsForm = NewForm(2);
    readonly TextBox _name = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
    readonly ComboBox _target = NewCombo();
    readonly ComboBox _mode = NewCombo("Segundo plano", "Real");
    readonly ComboBox _loopKind = NewCombo("Sem parar", "Número de vezes");
    readonly NumericUpDown _loops = NewNum(1, 1_000_000, 10);
    readonly NumericUpDown _jitter = NewNum(0, 50, 0);
    readonly CheckBox _schedOn = new() { AutoSize = true, Margin = new Padding(0, Theme.S(5), Theme.S(4), 0) };
    readonly NumericUpDown _schedHour = NewNum(0, 23, 14);
    readonly NumericUpDown _schedMin = NewNum(0, 59, 0);
    readonly ComboBox _schedRepeat = NewCombo("Todo dia", "Só uma vez");

    readonly ListBox _stepList = NewList(42);
    readonly Label _emptySteps = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted" };
    readonly TableLayoutPanel _stepEditor = NewForm(4);
    readonly ComboBox _kind = NewCombo("Mouse", "Tecla");
    readonly ComboBox _button = NewCombo("Esquerdo", "Direito", "Meio");
    readonly HotkeyBox _keyBox = new() { Dock = DockStyle.Fill, PlaceholderText = "Clique aqui e aperte a tecla" };
    readonly Panel _buttonOrKey = new() { Dock = DockStyle.Fill, Margin = new Padding(0) };
    Label? _buttonLabel;
    readonly ComboBox _action = NewCombo("Clique", "Clique duplo", "Segurar");
    readonly NumericUpDown _hold = NewNum(10, 3_600_000, 100);
    readonly NumericUpDown _repeat = NewNum(1, 1_000_000, 1);
    readonly NumericUpDown _interval = NewNum(1, 3_600_000, 100);
    readonly NumericUpDown _delay = NewNum(0, 3_600_000, 100);

    readonly RoundButton _btnNew = RoundButton.IconButton(Theme.Glyph.Add, "Nova macro", ButtonKind.Success);
    readonly RoundButton _btnDuplicate = RoundButton.IconButton(Theme.Glyph.Copy, "Duplicar macro", ButtonKind.Primary);
    readonly RoundButton _btnDelete = RoundButton.IconButton(Theme.Glyph.Delete, "Excluir macro", ButtonKind.Danger);
    readonly RoundButton _btnImport = RoundButton.IconButton(Theme.Glyph.Import, "Importar macros", ButtonKind.Secondary);
    readonly RoundButton _btnExport = RoundButton.IconButton(Theme.Glyph.Export, "Exportar macros", ButtonKind.Secondary);
    readonly RoundButton _btnMacroUp = RoundButton.IconButton(Theme.Glyph.Up, "Subir macro", ButtonKind.Secondary);
    readonly RoundButton _btnMacroDown = RoundButton.IconButton(Theme.Glyph.Down, "Descer macro", ButtonKind.Secondary);
    readonly ContextMenuStrip _macroMenu = new();
    readonly ContextMenuStrip _exportMenu = new();
    readonly RoundButton _btnAddPoint = new("+ Adicionar pontos", ButtonKind.Primary);
    readonly RoundButton _btnAddKey = RoundButton.IconButton(Theme.Glyph.Keyboard, "Adicionar tecla", ButtonKind.Success);
    readonly RoundButton _btnMovePoint = RoundButton.IconButton(Theme.Glyph.Pin, "Reposicionar ponto", ButtonKind.Secondary);
    readonly RoundButton _btnRemovePoint = RoundButton.IconButton(Theme.Glyph.Delete, "Remover passo", ButtonKind.Danger);
    readonly RoundButton _btnUp = RoundButton.IconButton(Theme.Glyph.Up, "Subir passo", ButtonKind.Secondary);
    readonly RoundButton _btnDown = RoundButton.IconButton(Theme.Glyph.Down, "Descer passo", ButtonKind.Secondary);
    readonly TextBox _stepLabel = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, PlaceholderText = "Ex.: Botão Coletar, Fechar janela..." };
    readonly TableLayoutPanel _stepNameForm = NewForm(2);
    readonly RoundButton _btnStart = new("▶ Iniciar (F7)", ButtonKind.Success);
    readonly RoundButton _btnStop = new("■ Parar (F8)", ButtonKind.Danger);
    readonly RoundButton _btnTest = new("Testar passo");
    readonly RoundButton _btnHotkeys = new("⌨ Teclas de atalho");
    readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };

    readonly WindowPreview _preview = new()
    {
        Dock = DockStyle.Fill, ClientAreaOnly = true,
        EmptyMessage = "Escolha a janela alvo em \"Macro selecionada\" (à esquerda)."
    };
    readonly Label _previewHint = new() { Dock = DockStyle.Top, AutoSize = false };

    static readonly Font BoldFont = new("Segoe UI Semibold", 9.5f);
    static readonly Font SmallFont = new("Segoe UI", 8.5f);
    static readonly Font PillFont = new("Segoe UI Semibold", 8f);
    static readonly Font StarFont = new("Segoe UI Symbol", 12f);

    public event Action<string>? StatusMessage;
    public event Action? RunningChanged;
    /// <summary>Aviso para mostrar na bandeja quando a janela do app está escondida.</summary>
    public event Action<string, string>? Notify;
    public event Action? HotkeysRequested;

    /// <summary>Alguma macro rodando (testes de passo não contam).</summary>
    public bool IsRunning => _running.Any(r => !r.IsTest);
    public int RunningCount => _running.Count(r => !r.IsTest);
    public string? RunningName => _running.FirstOrDefault(r => !r.IsTest)?.Macro.Name;
    /// <summary>Macros na ordem da lista (favoritas primeiro).</summary>
    public IReadOnlyList<Macro> Macros => Ordered();

    internal AutoClickPage(AppSettings settings, ThemedToolTip tips)
    {
        _settings = settings;
        _tips = tips;
        Dock = DockStyle.Fill;
        Padding = new Padding(Theme.S(11), Theme.S(11), Theme.S(11), Theme.S(4));

        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _grid.Controls.Add(_settingsCard, 0, 0);
        _grid.Controls.Add(_pointsCard, 1, 0);
        _rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        _rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        _rightColumn.Controls.Add(_previewCard, 0, 0);
        _rightColumn.Controls.Add(_runCard, 0, 1);
        _grid.Controls.Add(_rightColumn, 2, 0);

        BuildMacrosCard();
        BuildSettingsCard();
        BuildPointsCard();
        BuildPreviewCard();
        BuildRunCard();
        BuildActionBar();
        SetupTips();

        Controls.Add(_grid);
        Controls.Add(_actionBar);

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); MacroStore.Save(_macros); };
        RefreshRunList();

        RefreshMacroList(_macros.FirstOrDefault(m => m.Id == _settings.LastMacroId) ?? Ordered().FirstOrDefault());
        ApplyTheme();

        _scheduleTimer.Tick += (_, _) => CheckSchedules();
        _scheduleTimer.Start();
    }

    // =====================================================================
    //  Montagem
    // =====================================================================
    static ListBox NewList(int itemHeight) => new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = Theme.S(itemHeight), IntegralHeight = false
    };

    static ComboBox NewCombo(params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill };
        c.Items.AddRange(items);
        return c;
    }

    static NumericUpDown NewNum(int min, int max, int value) => new()
    {
        Minimum = min, Maximum = max, Value = value, ThousandsSeparator = true,
        BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill
    };

    /// <summary>Formulário de rótulo + campo (2 colunas) ou dois pares por linha (4 colunas).</summary>
    static TableLayoutPanel NewForm(int columns)
    {
        var t = new TableLayoutPanel { ColumnCount = columns, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0) };
        for (int i = 0; i < columns; i++)
            t.ColumnStyles.Add(i % 2 == 0 ? new ColumnStyle(SizeType.AutoSize) : new ColumnStyle(SizeType.Percent, 100f / (columns / 2)));
        return t;
    }

    static Label AddField(TableLayoutPanel form, string label, Control field)
    {
        bool secondPair = form.ColumnCount == 4 && form.Controls.Count % 4 == 2;
        var l = new Label
        {
            Text = label, AutoSize = true, Anchor = AnchorStyles.Left,
            Margin = new Padding(secondPair ? Theme.S(10) : 0, Theme.S(6), Theme.S(8), Theme.S(6))
        };
        field.Margin = new Padding(0, Theme.S(3), 0, Theme.S(3));
        form.Controls.Add(l);
        form.Controls.Add(field);
        return l;
    }

    static FlowLayoutPanel Toolbar(params Control[] buttons)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, Theme.S(2)) };
        f.Controls.AddRange(buttons);
        return f;
    }

    void BuildMacrosCard()
    {
        _macroList.DrawItem += DrawMacroItem;
        _macroList.MeasureItem += (_, e) => e.ItemHeight = Theme.S(42);
        _macroList.DropDownWidth = Theme.S(420);
        _macroList.SelectedIndexChanged += (_, _) => { if (!_loading) LoadMacro(_macroList.SelectedItem as Macro); };
        _btnFavorite.Click += (_, _) => { if (_current != null) ToggleFavorite(_current); };
        _macroMenu.Opening += (_, e) =>
        {
            _macroMenu.Items.Clear();
            if (_current == null) { e.Cancel = true; return; }
            var m = _current;
            _macroMenu.Items.Add(new ToolStripMenuItem(m.Favorite ? "Remover dos favoritos" : "★ Adicionar aos favoritos", null, (_, _) => ToggleFavorite(m)));
            _macroMenu.Items.Add(new ToolStripMenuItem("Subir macro", null, (_, _) => MoveMacro(-1)));
            _macroMenu.Items.Add(new ToolStripMenuItem("Descer macro", null, (_, _) => MoveMacro(+1)));
            _macroMenu.Items.Add(new ToolStripSeparator());
            _macroMenu.Items.Add(new ToolStripMenuItem("Duplicar macro", null, (_, _) => _btnDuplicate.PerformClick()));
            _macroMenu.Items.Add(new ToolStripMenuItem("Excluir macro", null, (_, _) => _btnDelete.PerformClick()));
            L.Apply(_macroMenu.Items);
        };
        _macroList.ContextMenuStrip = _macroMenu;
        _btnMacroUp.Click += (_, _) => MoveMacro(-1);
        _btnMacroDown.Click += (_, _) => MoveMacro(+1);
        _btnNew.Click += (_, _) => NewMacro();
        _btnDuplicate.Click += (_, _) =>
        {
            if (_current == null) return;
            var copy = _current.Clone();
            copy.Name = L.F("{0} (cópia)", _current.Name);
            copy.ScheduleEnabled = false;
            copy.ScheduleLastRun = null;
            _macros.Insert(_macros.IndexOf(_current) + 1, copy);
            SaveNow();
            RefreshMacroList(copy);
        };
        _btnDelete.Click += (_, _) =>
        {
            if (_current == null) return;
            if (Msg.Show($"Excluir a macro \"{_current.Name}\"?", "HiddenApp",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var ordered = Ordered();
            int i = ordered.IndexOf(_current);
            _macros.Remove(_current);
            ordered.Remove(_current);
            SaveNow();
            RefreshMacroList(ordered.Count == 0 ? null : ordered[Math.Clamp(i, 0, ordered.Count - 1)]);
        };

        _btnImport.Click += (_, _) => ImportMacros();
        _btnExport.Click += (_, _) =>
        {
            _exportMenu.Items.Clear();
            _exportMenu.Items.Add(new ToolStripMenuItem("Exportar a macro selecionada...", null, (_, _) => ExportMacros(selectedOnly: true))
            { Enabled = _current != null });
            _exportMenu.Items.Add(new ToolStripMenuItem("Exportar todas as macros...", null, (_, _) => ExportMacros(selectedOnly: false))
            { Enabled = _macros.Count > 0 });
            L.Apply(_exportMenu.Items);
            _exportMenu.Show(_btnExport, new Point(0, _btnExport.Height));
        };

        // Montado em BuildSettingsCard: caixa de macros e botões no topo do cartão "Macro"
    }

    void BuildSettingsCard()
    {
        _target.DropDownWidth = Theme.S(560);
        AddField(_settingsForm, "Nome", _name);
        AddField(_settingsForm, "Janela alvo", _target);
        AddField(_settingsForm, "Modo", _mode);
        AddField(_settingsForm, "Repetir", _loopKind);
        AddField(_settingsForm, "Vezes", _loops);
        AddField(_settingsForm, "Variação (%)", _jitter);
        var sched = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        _schedHour.Width = _schedMin.Width = Theme.S(52);
        _schedHour.Dock = _schedMin.Dock = DockStyle.None;
        _schedHour.Margin = _schedMin.Margin = new Padding(0, Theme.S(1), 0, 0);
        var colon = new Label { Text = ":", AutoSize = true, Margin = new Padding(Theme.S(2), Theme.S(4), Theme.S(2), 0) };
        sched.Controls.AddRange(new Control[] { _schedOn, _schedHour, colon, _schedMin });
        AddField(_settingsForm, "Iniciar às", sched);
        AddField(_settingsForm, "Agenda", _schedRepeat);

        _name.TextChanged += (_, _) => Change(m => m.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Sem nome" : _name.Text.Trim());
        _target.DropDown += (_, _) => RefreshTargets();
        _target.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _current == null || _target.SelectedItem is not TargetItem { Window: { } w }) return;
            _current.TargetExe = w.ExePath;
            _current.TargetTitle = w.Title;
            _targetHandle = w.Handle;
            ScheduleSave();
            _macroList.Invalidate();
            UpdatePreview();
            UpdateEmptySteps();
        };
        _mode.SelectedIndexChanged += (_, _) => Change(m => m.Mode = ModeOrder[Math.Max(0, _mode.SelectedIndex)]);
        _loopKind.SelectedIndexChanged += (_, _) =>
        {
            _loops.Enabled = _loopKind.SelectedIndex == 1;
            Change(m => m.Loops = _loopKind.SelectedIndex == 1 ? (int)_loops.Value : 0);
        };
        _loops.ValueChanged += (_, _) => Change(m => m.Loops = _loopKind.SelectedIndex == 1 ? (int)_loops.Value : 0);
        _jitter.ValueChanged += (_, _) => Change(m => m.JitterPercent = (int)_jitter.Value);
        _schedOn.CheckedChanged += (_, _) => { UpdateScheduleFields(); ScheduleChanged(); };
        _schedHour.ValueChanged += (_, _) => ScheduleChanged();
        _schedMin.ValueChanged += (_, _) => ScheduleChanged();
        _schedRepeat.SelectedIndexChanged += (_, _) => ScheduleChanged();

        // Ordem de encaixe: o último adicionado fica mais acima
        var toolbar = Toolbar(_btnNew, _btnFavorite, _btnDuplicate, _btnDelete, _btnMacroUp, _btnMacroDown, _btnImport, _btnExport);
        toolbar.Padding = new Padding(0, Theme.S(8), 0, Theme.S(4));
        var line = new Panel { Dock = DockStyle.Top, Height = Theme.S(10) };
        line.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, Theme.S(4), line.Width, Theme.S(4)); };
        _settingsCard.Controls.Add(_settingsForm);
        _settingsCard.Controls.Add(line);
        _settingsCard.Controls.Add(toolbar);
        _settingsCard.Controls.Add(_emptyMacros);
        _settingsCard.Controls.Add(_macroList);
    }

    void BuildPointsCard()
    {
        _stepList.DrawItem += DrawStepItem;
        _stepList.SelectedIndexChanged += (_, _) => { if (!_loading) LoadStep(); };
        _btnAddPoint.Click += (_, _) => TogglePick(PickKind.Add);
        _btnAddKey.Click += (_, _) => AddKeyStep();
        _btnMovePoint.Click += (_, _) => TogglePick(PickKind.Move);
        _btnRemovePoint.Click += (_, _) =>
        {
            if (_current == null || _stepList.SelectedItem is not MacroStep step) return;
            int i = _current.Steps.IndexOf(step);
            _current.Steps.Remove(step);
            SaveNow();
            RefreshSteps(_current.Steps.Count == 0 ? null : _current.Steps[Math.Clamp(i, 0, _current.Steps.Count - 1)]);
        };
        _btnUp.Click += (_, _) => MoveStep(-1);
        _btnDown.Click += (_, _) => MoveStep(+1);

        // Nome do passo (linha inteira) e, abaixo, dois campos por linha para caber sem rolagem
        AddField(_stepNameForm, "Nome", _stepLabel);
        _stepLabel.TextChanged += (_, _) => ChangeStep(s => s.Label = string.IsNullOrWhiteSpace(_stepLabel.Text) ? null : _stepLabel.Text.Trim());

        // "Botão" (clique) e "Tecla" dividem o mesmo lugar: aparece um ou outro conforme o tipo do passo
        _button.Margin = _keyBox.Margin = new Padding(0);
        _buttonOrKey.Height = Math.Max(_button.PreferredHeight, _keyBox.PreferredHeight);
        _buttonOrKey.Controls.Add(_button);
        _buttonOrKey.Controls.Add(_keyBox);
        AddField(_stepEditor, "Tipo", _kind);
        _buttonLabel = AddField(_stepEditor, "Botão", _buttonOrKey);
        AddField(_stepEditor, "Ação", _action);
        AddField(_stepEditor, "Vezes", _repeat);
        AddField(_stepEditor, "Intervalo (ms)", _interval);
        AddField(_stepEditor, "Segurar (ms)", _hold);
        AddField(_stepEditor, "Espera depois (ms)", _delay);
        _kind.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || SelectedStep is not MacroStep s) return;
            s.Kind = _kind.SelectedIndex == 1 ? StepKind.Tecla : StepKind.Clique;
            if (s.Action == ClickAction.CliqueDuplo && s.Kind == StepKind.Tecla) s.Action = ClickAction.Clique;
            ScheduleSave();
            _stepList.Invalidate();
            LoadStep();
            if (s.Kind == StepKind.Tecla && s.Key == 0) _keyBox.Focus();
        };
        _button.SelectedIndexChanged += (_, _) => ChangeStep(s => s.Button = (ClickButton)Math.Max(0, _button.SelectedIndex));
        _keyBox.TextChanged += (_, _) => ChangeStep(s => s.Key = (int)_keyBox.Keys);
        _action.SelectedIndexChanged += (_, _) =>
        {
            var a = ActionFromIndex(_action.SelectedIndex);
            _hold.Enabled = a == ClickAction.Segurar;
            ChangeStep(s => s.Action = a);
        };
        _hold.ValueChanged += (_, _) => ChangeStep(s => s.HoldMs = (int)_hold.Value);
        _repeat.ValueChanged += (_, _) => { _interval.Enabled = _repeat.Value > 1; ChangeStep(s => s.Repeat = (int)_repeat.Value); };
        _interval.ValueChanged += (_, _) => ChangeStep(s => s.IntervalMs = (int)_interval.Value);
        _delay.ValueChanged += (_, _) => ChangeStep(s => s.DelayAfterMs = (int)_delay.Value);

        var editorTitle = new Label
        {
            Text = "Passo selecionado", AutoSize = false, Dock = DockStyle.Top, Height = Theme.S(30),
            Font = new Font("Segoe UI Semibold", 9.5f), TextAlign = ContentAlignment.BottomLeft
        };
        var editor = new Panel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        editor.Controls.Add(_stepEditor);
        editor.Controls.Add(_stepNameForm);
        editor.Controls.Add(editorTitle);
        editor.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, 0, editor.Width, 0); };

        _pointsCard.Controls.Add(_stepList);
        _pointsCard.Controls.Add(_emptySteps);
        _pointsCard.Controls.Add(Toolbar(_btnAddPoint, _btnAddKey, _btnMovePoint, _btnRemovePoint, _btnUp, _btnDown));
        _pointsCard.Controls.Add(editor);
    }

    void BuildRunCard()
    {
        _runList.DrawItem += DrawRunItem;
        _runList.DoubleClick += (_, _) =>
        {
            // Duplo clique: abre a macro para ver/editar
            if (_runList.SelectedItem is RunState r && _macros.Contains(r.Macro)) _macroList.SelectedItem = r.Macro;
        };
        _btnStopSel.Click += (_, _) =>
        {
            if (_runList.SelectedItem is RunState r) r.Runner.Stop();
            else Say("Selecione na lista a macro que quer parar.");
        };
        _btnStopAll.Click += (_, _) => StopRunning();
        _runCard.Controls.Add(_runList);
        _runCard.Controls.Add(_emptyRun);
        _runCard.Controls.Add(Toolbar(_btnStopSel, _btnStopAll));
    }

    void RefreshRunList()
    {
        var selected = _runList.SelectedItem;
        _runList.BeginUpdate();
        _runList.Items.Clear();
        foreach (var r in _running) _runList.Items.Add(r);
        if (selected != null && _runList.Items.Contains(selected)) _runList.SelectedItem = selected;
        else if (_runList.Items.Count > 0) _runList.SelectedIndex = 0;
        _runList.EndUpdate();
        bool any = _running.Count > 0;
        _runList.Visible = any;
        _emptyRun.Visible = !any;
        _btnStopSel.Enabled = _btnStopAll.Enabled = any;
        int n = RunningCount;
        _runCard.Subtitle = n == 0 ? "" : L.F("{0} rodando", n);
        _runCard.Invalidate();
    }

    void BuildPreviewCard()
    {
        _previewHint.Height = Theme.S(64);
        _preview.PointPicked += (rx, ry) => ApplyPoint(rx, ry, false);
        _previewCard.Controls.Add(_preview);
        _previewCard.Controls.Add(_previewHint);
    }

    void BuildActionBar()
    {
        _btnStart.Click += (_, _) => StartSelected();
        _btnStop.Click += (_, _) => StopCurrent();
        _btnTest.Click += (_, _) => TestStep();
        _btnHotkeys.Click += (_, _) => HotkeysRequested?.Invoke();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, Padding = new Padding(0) };
        buttons.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnTest, _btnHotkeys });
        _status.Padding = new Padding(Theme.S(8), 0, 0, Theme.S(8));
        _actionBar.Controls.Add(_status);
        _actionBar.Controls.Add(buttons);
    }

    void SetupTips()
    {
        _tips.Set(_btnNew, "Nova macro", "Cria uma macro vazia. Depois escolha a janela alvo e adicione os pontos de clique.");
        _tips.Set(_btnDuplicate, "Duplicar macro", "Cria uma cópia da macro selecionada, para fazer variações sem mexer na original.");
        _tips.Set(_btnDelete, "Excluir macro", "Apaga a macro selecionada (pede confirmação).");
        _tips.Set(_btnImport, "Importar macros", "Abre arquivos .hiddenmacro recebidos de outras pessoas e adiciona as macros na sua lista.");
        _tips.Set(_btnExport, "Exportar macros",
            "Salva a macro selecionada (ou todas) num arquivo .hiddenmacro, para compartilhar com outras pessoas.");
        _tips.Set(_btnStopSel, "Parar a macro selecionada", "Para só a macro selecionada na lista \"Em execução\". As outras continuam rodando.");
        _tips.Set(_btnStopAll, "Parar todas", "Para todas as macros que estão rodando (o mesmo que a tecla de parar).");
        _tips.Set(_runList, "Em execução", "Macros rodando agora, com o progresso de cada uma. Duplo clique abre a macro.");
        _tips.Set(_btnFavorite, "Favoritar macro", "Marca ou desmarca a macro selecionada como favorita. As favoritas ficam no topo da lista, com estrela.");
        _tips.Set(_macroList, "Macros", "Escolha a macro para ver e editar. As favoritas (★) ficam no topo; a ordem das demais muda com as setas.");
        _tips.Set(_btnMacroUp, "Subir macro", "Move a macro selecionada para cima na lista. As favoritas (com estrela) ficam sempre no topo.");
        _tips.Set(_btnMacroDown, "Descer macro", "Move a macro selecionada para baixo na lista. As favoritas (com estrela) ficam sempre no topo.");
        _tips.Set(_stepLabel, "Nome do passo", "Um nome ou descrição opcional, para quem ver a macro entender o que cada passo faz.");
        _tips.Set(_btnAddKey, "Adicionar tecla",
            "Adiciona um passo que aperta uma tecla (com Ctrl, Alt ou Shift, se quiser) depois do passo selecionado. " +
            "Depois clique na caixa \"Tecla\" e aperte a tecla desejada.");
        _tips.Set(_kind, "Tipo do passo", "Clique do mouse: clica num ponto da janela.\nTecla: aperta uma tecla do teclado na janela alvo.");
        _tips.Set(_keyBox, "Tecla", "Clique aqui e aperte a tecla (pode combinar com Ctrl, Alt ou Shift). As teclas de atalho do HiddenApp não podem ser usadas.");
        _tips.Set(_schedOn, "Agendar",
            "Inicia esta macro sozinha no horário escolhido (o HiddenApp precisa estar aberto, mesmo escondido perto do relógio, " +
            "e a janela alvo precisa estar aberta).");
        _tips.Set(_schedRepeat, "Agenda", "Todo dia: roda todos os dias nesse horário.\nSó uma vez: roda na próxima vez que der o horário e depois desliga o agendamento.");
        _tips.Set(_btnAddPoint, "Adicionar pontos",
            "Liga o modo de adicionar: cada clique na prévia (à direita) vira um ponto, na ordem. Clique em \"✓ Concluir\" quando terminar.\n\n" +
            "Atalho: posicione o mouse na própria janela alvo e aperte a tecla de captura.");
        _tips.Set(_btnMovePoint, "Reposicionar ponto", "Muda o lugar do ponto selecionado: depois de clicar aqui, clique na prévia na nova posição.");
        _tips.Set(_btnRemovePoint, "Remover passo", "Apaga o passo selecionado.");
        _tips.Set(_btnUp, "Subir passo", "Move o passo selecionado para antes na sequência.");
        _tips.Set(_btnDown, "Descer passo", "Move o passo selecionado para depois na sequência.");
        _tips.Set(_btnTest, "Testar passo",
            "Executa só o passo selecionado uma vez, para conferir se acerta o lugar (ou a tecla) e se o modo funciona nesse programa.");
        _tips.Set(_btnHotkeys, "Teclas de atalho", "Troca as teclas de iniciar, parar e capturar ponto. Elas funcionam mesmo com outra janela em uso.");
        _tips.Set(_mode, "Modo",
            "Segundo plano: manda cliques e teclas para a janela sem mexer no mouse — você continua usando o PC. " +
            "Nem todo programa aceita (muitos jogos ignoram).\n\n" +
            "Real: move o mouse, clica e aperta as teclas de verdade. Funciona em quase tudo, mas a janela precisa estar na frente; " +
            "se você trocar de janela, a macro pausa sozinha.");
        _tips.Set(_loopKind, "Repetir", "Sem parar: a sequência de passos roda até você apertar a tecla de parar.\nNúmero de vezes: roda a sequência inteira essa quantidade de vezes.");
        _tips.Set(_jitter, "Variação aleatória", "Muda um pouco cada intervalo (em %), para os cliques não terem um ritmo perfeitamente exato. 0 = tempos exatos.");
        _tips.Set(_repeat, "Vezes", "Quantas vezes seguidas este passo é feito (cliques ou toques na tecla) antes de ir para o próximo.");
        _tips.Set(_interval, "Intervalo", "Tempo entre as repetições deste passo, em milissegundos (1000 ms = 1 segundo).");
        _tips.Set(_hold, "Segurar", "Por quanto tempo o botão ou a tecla fica apertado (só na ação \"Segurar\").");
        _tips.Set(_delay, "Espera depois", "Tempo de espera depois deste passo, antes de ir para o próximo.");
    }

    // =====================================================================
    //  Tema
    // =====================================================================
    public void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Back;
        _grid.BackColor = p.Back;
        _actionBar.BackColor = p.Back;
        _rightColumn.BackColor = p.Back;
        foreach (var card in new[] { _settingsCard, _pointsCard, _previewCard, _runCard }) card.BackColor = p.Surface;
        _preview.BackColor = p.Back;
        ThemeChildren(this, p);
        UpdatePreviewHint();
        Theme.ApplyScrollbars(_stepList);
        Theme.ApplyScrollbars(_runList);
        Invalidate(true);
    }

    static void ThemeChildren(Control root, Palette p)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox or ComboBox or NumericUpDown:
                    c.BackColor = p.Surface2; c.ForeColor = p.Text; break;
                case ListBox:
                    c.BackColor = p.Surface; c.ForeColor = p.Text; break;
                case CheckBox:
                    c.ForeColor = p.Text; break;
                case Label l:
                    l.ForeColor = (l.Tag as string) == "muted" ? p.Muted : p.Text; break;
            }
            ThemeChildren(c, p);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme();
    }

    // =====================================================================
    //  Macros e pontos
    // =====================================================================
    void NewMacro()
    {
        var m = new Macro { Name = $"Macro {_macros.Count + 1}", Mode = ClickMode.Real };
        _macros.Add(m);
        SaveNow();
        RefreshMacroList(m);
        _target.Focus();
        SetStatus("Macro criada. Agora escolha a janela alvo e adicione os passos (cliques e teclas).");
    }

    /// <summary>Ordem de exibição: favoritas primeiro; dentro de cada grupo, a ordem escolhida pelo usuário.</summary>
    List<Macro> Ordered() => _macros.Where(m => m.Favorite).Concat(_macros.Where(m => !m.Favorite)).ToList();

    void ToggleFavorite(Macro m)
    {
        m.Favorite = !m.Favorite;
        SaveNow();
        RefreshMacroList(_current ?? m);
        Say(m.Favorite ? $"\"{m.Name}\" está nos favoritos." : $"\"{m.Name}\" saiu dos favoritos.");
    }

    /// <summary>Sobe/desce a macro selecionada dentro do seu grupo (favoritas ou não).</summary>
    void MoveMacro(int delta)
    {
        if (_current == null) return;
        var ordered = Ordered();
        int i = ordered.IndexOf(_current), j = i + delta;
        if (j < 0 || j >= ordered.Count || ordered[j].Favorite != _current.Favorite) return;
        int a = _macros.IndexOf(_current), b = _macros.IndexOf(ordered[j]);
        (_macros[a], _macros[b]) = (_macros[b], _macros[a]);
        SaveNow();
        RefreshMacroList(_current);
    }

    void RefreshMacroList(Macro? select)
    {
        _loading = true;
        _macroList.BeginUpdate();
        _macroList.Items.Clear();
        foreach (var m in Ordered()) _macroList.Items.Add(m);
        _macroList.EndUpdate();
        _loading = false;
        bool any = _macros.Count > 0;
        _emptyMacros.Visible = !any;
        if (select != null) _macroList.SelectedItem = select;
        else LoadMacro(null);
    }

    void LoadMacro(Macro? m)
    {
        CancelPick();
        _current = m;
        if (m != null) { _settings.LastMacroId = m.Id; _settings.Save(); }
        _loading = true;
        try
        {
            _name.Text = m?.Name ?? "";
            _mode.SelectedIndex = Math.Max(0, Array.IndexOf(ModeOrder, m?.Mode ?? ClickMode.Real));
            _loopKind.SelectedIndex = (m?.Loops ?? 0) > 0 ? 1 : 0;
            _loops.Value = Math.Clamp(m?.Loops > 0 ? m.Loops : 10, (int)_loops.Minimum, (int)_loops.Maximum);
            _loops.Enabled = _loopKind.SelectedIndex == 1;
            _jitter.Value = Math.Clamp(m?.JitterPercent ?? 0, 0, 50);
            var at = m?.ScheduleAt ?? new TimeSpan(14, 0, 0);
            _schedOn.Checked = m?.ScheduleEnabled ?? false;
            _schedHour.Value = at.Hours;
            _schedMin.Value = at.Minutes;
            _schedRepeat.SelectedIndex = m?.ScheduleRepeat == ScheduleRepeat.UmaVez ? 1 : 0;
            UpdateScheduleFields();
            _targetHandle = m != null ? ClickRunner.FindWindow(m, IntPtr.Zero) : IntPtr.Zero;
            RefreshTargets();
        }
        finally { _loading = false; }
        RefreshSteps(m?.Steps.FirstOrDefault());
        UpdatePreview();
        UpdateButtons();
    }

    /// <summary>Item da lista de janelas alvo (janela aberta ou o programa salvo que não está aberto).</summary>
    sealed record TargetItem(WindowInfo? Window, string Text)
    {
        public override string ToString() => Text;
    }

    void RefreshTargets()
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            _target.Items.Clear();
            if (_current == null) return;
            TargetItem? selected = null;
            foreach (var w in WindowEnumerator.GetOpenWindows().Where(w => w.Pid != (uint)Environment.ProcessId).OrderBy(w => w.ProcessName))
            {
                var item = new TargetItem(w, $"{w.ProcessName}  —  {w.Title}");
                _target.Items.Add(item);
                if (w.Handle == _targetHandle) selected = item;
            }
            if (selected == null && _current.TargetExe != null)
            {
                selected = new TargetItem(null, $"{_current.TargetName}  —  {L.T("(não está aberto)")}");
                _target.Items.Insert(0, selected);
            }
            if (selected != null) _target.SelectedItem = selected;
        }
        finally { _loading = wasLoading; }
    }

    void RefreshSteps(MacroStep? select)
    {
        _loading = true;
        _stepList.BeginUpdate();
        _stepList.Items.Clear();
        if (_current != null)
            foreach (var s in _current.Steps) _stepList.Items.Add(s);
        _stepList.EndUpdate();
        if (select != null) _stepList.SelectedItem = select;
        _loading = false;
        _macroList.Invalidate();
        UpdateEmptySteps();
        LoadStep();
    }

    void UpdateEmptySteps()
    {
        bool any = _current != null && _current.Steps.Count > 0;
        _stepList.Visible = any;
        _emptySteps.Visible = !any;
        _emptySteps.Text = _current == null
            ? "Crie ou selecione uma macro à esquerda."
            : _current.TargetExe == null
                ? "Nenhum passo ainda.\n\n1. Escolha a janela alvo (à esquerda)\n2. Clique em \"+ Adicionar pontos\" e depois na prévia,\n   ou no botão do teclado para adicionar uma tecla"
                : $"Nenhum passo ainda.\n\nClique em \"+ Adicionar pontos\" e depois na prévia,\nposicione o mouse na janela alvo e aperte {_captureKey},\nou use o botão do teclado para adicionar uma tecla.";
    }

    MacroStep? SelectedStep => _stepList.SelectedItem as MacroStep;

    void LoadStep()
    {
        var s = SelectedStep;
        _loading = true;
        try
        {
            bool key = s?.Kind == StepKind.Tecla;
            _stepLabel.Text = s?.Label ?? "";
            _kind.SelectedIndex = key ? 1 : 0;
            _button.Visible = !key;
            _keyBox.Visible = key;
            if (_buttonLabel != null) _buttonLabel.Text = key ? "Tecla" : "Botão";
            _button.SelectedIndex = (int)(s?.Button ?? ClickButton.Esquerdo);
            _keyBox.Keys = (Keys)(s?.Key ?? 0);
            if ((s?.Key ?? 0) == 0) _keyBox.Text = "";
            // Ações: clique tem "Clique / Clique duplo / Segurar"; tecla tem "Apertar / Segurar"
            _action.Items.Clear();
            _action.Items.AddRange(key ? new object[] { L.T("Apertar"), L.T("Segurar") }
                                       : new object[] { L.T("Clique"), L.T("Clique duplo"), L.T("Segurar") });
            _action.SelectedIndex = IndexFromAction(s?.Action ?? ClickAction.Clique, key);
            _hold.Value = Math.Clamp(s?.HoldMs ?? 100, (int)_hold.Minimum, (int)_hold.Maximum);
            _repeat.Value = Math.Clamp(s?.Repeat ?? 1, 1, (int)_repeat.Maximum);
            _interval.Value = Math.Clamp(s?.IntervalMs ?? 100, 1, (int)_interval.Maximum);
            _delay.Value = Math.Clamp(s?.DelayAfterMs ?? 100, 0, (int)_delay.Maximum);
            _hold.Enabled = (s?.Action ?? ClickAction.Clique) == ClickAction.Segurar;
            _interval.Enabled = _repeat.Value > 1;
        }
        finally { _loading = false; }
        UpdateButtons();
    }

    bool SelectedIsKey => SelectedStep?.Kind == StepKind.Tecla;

    ClickAction ActionFromIndex(int index) => SelectedIsKey
        ? (index == 1 ? ClickAction.Segurar : ClickAction.Clique)
        : index switch { 1 => ClickAction.CliqueDuplo, 2 => ClickAction.Segurar, _ => ClickAction.Clique };

    static int IndexFromAction(ClickAction action, bool key) => key
        ? (action == ClickAction.Segurar ? 1 : 0)
        : action switch { ClickAction.CliqueDuplo => 1, ClickAction.Segurar => 2, _ => 0 };

    /// <summary>Adiciona um passo de tecla depois do passo selecionado (ou no fim) e pede a tecla.</summary>
    void AddKeyStep()
    {
        if (_current == null) { Say("Crie ou selecione uma macro primeiro."); return; }
        CancelPick();
        var step = new MacroStep { Kind = StepKind.Tecla };
        int at = SelectedStep is MacroStep sel ? _current.Steps.IndexOf(sel) + 1 : _current.Steps.Count;
        _current.Steps.Insert(at, step);
        SaveNow();
        RefreshSteps(step);
        _keyBox.Focus();
        SetStatus("Passo de tecla adicionado: clique na caixa \"Tecla\" e aperte a tecla desejada.");
    }

    // =====================================================================
    //  Agendamento: iniciar a macro sozinha num horário
    // =====================================================================
    void UpdateScheduleFields() => _schedHour.Enabled = _schedMin.Enabled = _schedRepeat.Enabled = _schedOn.Checked;

    void ScheduleChanged()
    {
        if (_loading || _current == null) return;
        var m = _current;
        m.ScheduleEnabled = _schedOn.Checked;
        m.ScheduleTime = $"{(int)_schedHour.Value:00}:{(int)_schedMin.Value:00}";
        m.ScheduleRepeat = _schedRepeat.SelectedIndex == 1 ? ScheduleRepeat.UmaVez : ScheduleRepeat.TodoDia;
        // Se o horário de hoje já passou, a próxima vez é amanhã (não dispara na hora só por ter ligado)
        m.ScheduleLastRun = DateTime.Now.TimeOfDay >= m.ScheduleAt ? DateTime.Today : null;
        ScheduleSave();
        _macroList.Invalidate();
        SetStatus(m.ScheduleEnabled
            ? L.F("A macro vai iniciar sozinha {0} às {1}.", L.T(m.ScheduleLastRun == DateTime.Today ? "amanhã" : "hoje"), m.ScheduleTime)
            : L.T("Agendamento desligado."));
    }

    /// <summary>Confere as macros agendadas (a cada 10 s). Só dispara até 2 minutos depois do horário.</summary>
    void CheckSchedules()
    {
        var now = DateTime.Now;
        foreach (var m in _macros.Where(m => m.ScheduleEnabled).ToList())
        {
            var at = now.Date + m.ScheduleAt;
            if (now < at || now - at > TimeSpan.FromMinutes(2) || m.ScheduleLastRun?.Date == now.Date) continue;

            m.ScheduleLastRun = now.Date;
            if (m.ScheduleRepeat == ScheduleRepeat.UmaVez) m.ScheduleEnabled = false;
            SaveNow();
            if (m == _current) { _loading = true; _schedOn.Checked = m.ScheduleEnabled; UpdateScheduleFields(); _loading = false; }
            _macroList.Invalidate();

            if (IsMacroRunning(m)) continue; // já está rodando
            if (m.Mode == ClickMode.Real && RealRun() is RunState other)
            {
                Say($"A macro \"{m.Name}\" estava agendada para {m.ScheduleTime}, mas \"{other.Macro.Name}\" já está usando o modo real.", notify: true);
                continue;
            }
            Say($"Horário agendado ({m.ScheduleTime}): iniciando \"{m.Name}\".", notify: true);
            StartMacro(m);
        }
    }

    void MoveStep(int delta)
    {
        if (_current == null || SelectedStep is not MacroStep s) return;
        int i = _current.Steps.IndexOf(s), j = i + delta;
        if (j < 0 || j >= _current.Steps.Count) return;
        _current.Steps.RemoveAt(i);
        _current.Steps.Insert(j, s);
        SaveNow();
        RefreshSteps(s);
    }

    void Change(Action<Macro> apply)
    {
        if (_loading || _current == null) return;
        apply(_current);
        ScheduleSave();
        _macroList.Invalidate();
    }

    void ChangeStep(Action<MacroStep> apply)
    {
        if (_loading || SelectedStep is not MacroStep s) return;
        apply(s);
        ScheduleSave();
        _stepList.Invalidate();
    }

    void ScheduleSave() { _saveTimer.Stop(); _saveTimer.Start(); }
    void SaveNow() { _saveTimer.Stop(); MacroStore.Save(_macros); }

    // =====================================================================
    //  Importar / exportar
    // =====================================================================
    void ExportMacros(bool selectedOnly)
    {
        var list = selectedOnly ? (_current != null ? new List<Macro> { _current } : new()) : _macros.ToList();
        if (list.Count == 0) return;
        SaveNow();
        string suggested = selectedOnly ? SafeFileName(list[0].Name) : "Minhas macros";
        using var dlg = new SaveFileDialog
        {
            Filter = L.T(MacroStore.FileFilter), DefaultExt = MacroStore.FileExtension, AddExtension = true,
            FileName = suggested + MacroStore.FileExtension, Title = L.T("Exportar macro")
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            MacroStore.Export(list, dlg.FileName);
            Say(list.Count == 1
                ? $"Macro \"{list[0].Name}\" exportada para {Path.GetFileName(dlg.FileName)}."
                : $"{list.Count} macros exportadas para {Path.GetFileName(dlg.FileName)}.");
        }
        catch (Exception ex) { Msg.Show($"Não foi possível exportar:\n{ex.Message}", "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ImportMacros()
    {
        using var dlg = new OpenFileDialog { Filter = L.T(MacroStore.FileFilter), Multiselect = true, Title = L.T("Importar macros") };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        var imported = new List<Macro>();
        var errors = new List<string>();
        foreach (var file in dlg.FileNames)
        {
            try { imported.AddRange(MacroStore.Import(file)); }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
        foreach (var m in imported)
        {
            // Evita nomes repetidos
            string baseName = m.Name, name = baseName;
            for (int n = 2; _macros.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)); n++)
                name = $"{baseName} ({n})";
            m.Name = name;
            _macros.Add(m);
        }
        if (imported.Count > 0)
        {
            SaveNow();
            RefreshMacroList(imported[0]);
            Say(imported.Count == 1
                ? $"Macro \"{imported[0].Name}\" importada. Confira a janela alvo e use \"Testar ponto\" antes de iniciar."
                : $"{imported.Count} macros importadas. Confira a janela alvo de cada uma antes de iniciar.");
        }
        if (errors.Count > 0)
            Msg.Show($"Alguns arquivos não puderam ser importados:\n\n{string.Join("\n", errors)}",
                "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    static string SafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "macro" : name;
    }

    // =====================================================================
    //  Marcar pontos (prévia ou tecla de captura)
    // =====================================================================
    void TogglePick(PickKind kind)
    {
        if (_pick == kind) { CancelPick(); return; }
        if (_current == null) { Say("Crie ou selecione uma macro primeiro."); return; }
        if (kind == PickKind.Move && SelectedStep?.Kind != StepKind.Clique) { Say("Selecione o ponto que quer reposicionar."); return; }
        if (_targetHandle == IntPtr.Zero || !Native.IsWindow(_targetHandle))
        {
            Say($"Primeiro escolha a janela alvo (ela precisa estar aberta). Ou posicione o mouse na janela e aperte {_captureKey}.");
            _target.Focus();
            return;
        }
        _pick = kind;
        _preview.PickMode = true;
        _btnAddPoint.Text = kind == PickKind.Add ? "✓ Concluir" : "+ Adicionar pontos";
        _btnMovePoint.IconGlyph = kind == PickKind.Move ? Theme.Glyph.Cancel : Theme.Glyph.Pin;
        _btnMovePoint.Kind = kind == PickKind.Move ? ButtonKind.Primary : ButtonKind.Secondary;
        _btnMovePoint.Invalidate();
        UpdatePreviewHint();
        SetStatus(kind == PickKind.Add
            ? "Modo de adicionar ligado: clique na prévia (à direita) em cada lugar, na ordem. \"✓ Concluir\" quando terminar."
            : "Clique na prévia (à direita) na nova posição do ponto.");
    }

    void CancelPick()
    {
        _pick = PickKind.None;
        _preview.PickMode = false;
        _btnAddPoint.Text = "+ Adicionar pontos";
        _btnMovePoint.IconGlyph = Theme.Glyph.Pin;
        _btnMovePoint.Kind = ButtonKind.Secondary;
        _btnMovePoint.Invalidate();
        UpdatePreviewHint();
    }

    /// <summary>Tecla de captura (F9): usa a posição atual do mouse na janela embaixo dele.</summary>
    public void CaptureFromCursor()
    {
        if (CurrentBusy) return;
        if (!ClickRunner.CaptureCursor(out var window, out double rx, out double ry))
        {
            Say("Posicione o mouse dentro de uma janela e aperte a tecla de captura.", notify: true);
            return;
        }
        if (window == FindForm()?.Handle)
        {
            Say("Posicione o mouse na janela do programa alvo, não no HiddenApp.");
            return;
        }
        var info = WindowEnumerator.Describe(window);
        if (info?.ExePath == null) { Say("Não foi possível identificar o programa dessa janela.", notify: true); return; }

        if (_current == null) NewMacro();
        var m = _current!;
        bool targetOpen = _targetHandle != IntPtr.Zero && Native.IsWindow(_targetHandle);
        if (m.TargetExe == null || !targetOpen)
        {
            m.TargetExe = info.ExePath;
            m.TargetTitle = info.Title;
        }
        else if (_targetHandle != window &&
                 !Path.GetFileName(m.TargetExe).Equals(Path.GetFileName(info.ExePath), StringComparison.OrdinalIgnoreCase))
        {
            Say($"O mouse está sobre outro programa ({info.ProcessName}). A janela alvo desta macro é {m.TargetName}.", notify: true);
            return;
        }
        _targetHandle = window;
        RefreshTargets();
        UpdatePreview();
        ApplyPoint(rx, ry, notify: true);
    }

    void ApplyPoint(double rx, double ry, bool notify)
    {
        if (_current == null) return;
        MacroStep step;
        if (_pick == PickKind.Move && SelectedStep is MacroStep sel)
        {
            step = sel;
            step.RelX = rx; step.RelY = ry;
            CancelPick();
            Say($"Ponto {_current.Steps.IndexOf(step) + 1} reposicionado.", notify);
        }
        else
        {
            // O ponto novo herda a configuração do último clique, para agilizar sequências parecidas
            step = _current.Steps.LastOrDefault(s => s.Kind == StepKind.Clique)?.Clone() ?? new MacroStep();
            step.Label = null;
            step.RelX = rx; step.RelY = ry;
            _current.Steps.Add(step);
            Say(_pick == PickKind.Add
                ? $"Ponto {_current.Steps.Count} adicionado. Clique em outro lugar da prévia para mais pontos, ou em \"✓ Concluir\"."
                : $"Ponto {_current.Steps.Count} adicionado.", notify);
        }
        SaveNow();
        RefreshSteps(step);
    }

    // =====================================================================
    //  Executar
    // =====================================================================
    public bool IsMacroRunning(Macro m) => _running.Any(r => !r.IsTest && r.Macro.Id == m.Id);

    public void StopMacro(Macro m)
    {
        foreach (var r in _running.Where(r => r.Macro.Id == m.Id).ToList()) r.Runner.Stop();
    }

    /// <summary>Macro selecionada rodando (ou em teste): a edição dela fica bloqueada.</summary>
    bool CurrentBusy => _current != null && _running.Any(r => r.Macro.Id == _current.Id);

    /// <summary>O modo real usa o mouse e o teclado de verdade: só uma macro por vez.</summary>
    RunState? RealRun() => _running.FirstOrDefault(r => r.Macro.Mode == ClickMode.Real);

    /// <summary>Inicia uma macro sem mudar a seleção (menu da bandeja, agendamento).</summary>
    public void StartMacro(Macro macro) => Start(macro);

    /// <summary>Tecla de iniciar (F7) ou botão "Iniciar": inicia a macro selecionada.</summary>
    public void StartSelected()
    {
        CancelPick();
        var m = _current ?? _macros.FirstOrDefault(x => x.Id == _settings.LastMacroId);
        if (m == null) { Say("Crie uma macro na aba Macro antes de iniciar.", notify: true); return; }
        Start(m);
    }

    void Start(Macro m)
    {
        if (_running.Any(r => r.Macro.Id == m.Id)) { Say($"A macro \"{m.Name}\" já está rodando.", notify: true); return; }
        if (m.Steps.Count == 0) { Say($"A macro \"{m.Name}\" ainda não tem passos.", notify: true); return; }
        if (m.Mode == ClickMode.Real && RealRun() is RunState other)
        {
            Say($"Só uma macro no modo real pode rodar por vez (\"{other.Macro.Name}\" já está usando o mouse e o teclado). " +
                "Pare-a ou use o modo segundo plano.", notify: true);
            return;
        }

        var window = ClickRunner.FindWindow(m, m == _current ? _targetHandle : IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            Say(m.TargetExe == null
                ? $"Escolha a janela alvo da macro \"{m.Name}\"."
                : $"A janela alvo ({m.TargetName}) não está aberta.", notify: true);
            return;
        }
        SaveNow();
        try
        {
            Launch(m, window, test: false);
            Say(RunningCount > 1
                ? $"Rodando \"{m.Name}\" junto com outras macros — {_stopKey} para todas."
                : $"Rodando \"{m.Name}\" — aperte {_stopKey} para parar.", notify: true);
        }
        catch (Exception ex) { Say(ex.Message, notify: true); }
    }

    /// <summary>Cria um executor para a macro e acompanha o progresso dele na lista "Em execução".</summary>
    void Launch(Macro m, IntPtr window, bool test)
    {
        var state = new RunState { Macro = m, Runner = new ClickRunner(), IsTest = test };
        state.Runner.Progress += text => OnUi(() =>
        {
            state.Text = text;
            _runList.Invalidate();
            if (_current?.Id == m.Id) SetStatus(text);
        });
        state.Runner.Stopped += reason => OnUi(() =>
        {
            _running.Remove(state);
            AfterRunChange();
            if (test) SetStatus(reason); else Say(reason, notify: true);
        });
        _running.Add(state);
        try { state.Runner.Start(m, window); }
        catch { _running.Remove(state); throw; }
        AfterRunChange();
    }

    void AfterRunChange()
    {
        RefreshRunList();
        UpdateButtons();
        _macroList.Invalidate();
        RunningChanged?.Invoke();
    }

    /// <summary>Tecla de parar (F8) ou "Parar todas": para todas as macros que estão rodando.</summary>
    public void StopRunning()
    {
        foreach (var r in _running.ToList()) r.Runner.Stop();
    }

    /// <summary>Botão "Parar" da barra: para a macro selecionada.</summary>
    void StopCurrent()
    {
        if (_current != null) StopMacro(_current);
    }

    void TestStep()
    {
        if (CurrentBusy || _current == null || SelectedStep is not MacroStep s) { Say("Selecione um passo para testar."); return; }
        if (_targetHandle == IntPtr.Zero || !Native.IsWindow(_targetHandle)) { Say("A janela alvo não está aberta."); return; }
        if (_current.Mode == ClickMode.Real && RealRun() is RunState other)
        {
            Say($"Só uma macro no modo real pode rodar por vez (\"{other.Macro.Name}\" já está usando o mouse e o teclado). " +
                "Pare-a ou use o modo segundo plano.");
            return;
        }
        CancelPick();
        var test = new Macro
        {
            Id = _current.Id, Name = L.F("Teste: {0}", _current.Name), Mode = _current.Mode, Loops = 1, TargetExe = _current.TargetExe,
            Steps = { CloneForTest(s) }
        };
        try { Launch(test, _targetHandle, test: true); }
        catch (Exception ex) { Say(ex.Message); }
    }

    static MacroStep CloneForTest(MacroStep s)
    {
        var c = s.Clone();
        c.Repeat = 1;
        c.DelayAfterMs = 0;
        return c;
    }

    void UpdateButtons()
    {
        bool running = CurrentBusy; // só a macro selecionada fica travada enquanto roda
        bool has = _current != null;
        _settingsForm.Enabled = has && !running;
        _stepEditor.Enabled = _stepNameForm.Enabled = has && !running && SelectedStep != null;
        _btnStart.Enabled = has && !running;
        _btnStop.Enabled = running;
        _btnTest.Enabled = has && !running && SelectedStep != null;
        _btnDelete.Enabled = has && !running;
        _btnDuplicate.Enabled = _btnMacroUp.Enabled = _btnMacroDown.Enabled = _btnFavorite.Enabled = has;
        _btnFavorite.IconGlyph = _current?.Favorite == true ? Theme.Glyph.StarFill : Theme.Glyph.Star;
        _btnFavorite.Kind = _current?.Favorite == true ? ButtonKind.Favorite : ButtonKind.Secondary;
        _btnFavorite.Invalidate();
        _btnAddPoint.Enabled = _btnAddKey.Enabled = has && !running;
        _btnRemovePoint.Enabled = _btnUp.Enabled = _btnDown.Enabled = SelectedStep != null && !running;
        _btnMovePoint.Enabled = SelectedStep?.Kind == StepKind.Clique && !running;
    }

    // =====================================================================
    //  Prévia, textos e avisos
    // =====================================================================
    void UpdatePreview()
    {
        _preview.SetSource(_targetHandle != IntPtr.Zero && Native.IsWindow(_targetHandle) ? _targetHandle : IntPtr.Zero);
        UpdatePreviewHint();
    }

    void UpdatePreviewHint()
    {
        var p = Theme.Current;
        if (_pick == PickKind.Add)
        {
            _previewHint.Text = "➜ Clique na prévia em cada lugar onde quer clicar, na ordem.\nQuando terminar, clique em \"✓ Concluir\".";
            _previewHint.ForeColor = p.Accent;
        }
        else if (_pick == PickKind.Move)
        {
            _previewHint.Text = "➜ Clique na prévia na nova posição do ponto selecionado.";
            _previewHint.ForeColor = p.Accent;
        }
        else
        {
            _previewHint.Text = "Para marcar pontos: clique em \"+ Adicionar pontos\" e depois aqui na prévia.\n" +
                                $"Ou posicione o mouse na própria janela e aperte {_captureKey}.";
            _previewHint.ForeColor = p.Muted;
        }
    }

    public void SetHotkeyLabels(string start, string stop, string capture)
    {
        _startKey = start; _stopKey = stop; _captureKey = capture;
        _btnStart.Text = $"▶ Iniciar ({start})";
        _btnStop.Text = $"■ Parar ({stop})";
        _tips.Set(_btnStart, "Iniciar", $"Começa a macro selecionada. Atalho: {start} (funciona mesmo com outra janela em uso).");
        _tips.Set(_btnStop, "Parar", $"Para a macro selecionada. A tecla {stop} para todas as macros que estiverem rodando (funciona mesmo com outra janela em uso).");
        UpdatePreviewHint();
        UpdateEmptySteps();
    }

    void SetStatus(string text) => _status.Text = L.T(text);

    void Say(string text, bool notify = false)
    {
        SetStatus(text);
        StatusMessage?.Invoke(text);
        if (notify && FindForm() is Form f && !f.Visible) Notify?.Invoke("Macro", L.T(text));
    }

    void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }

    // =====================================================================
    //  Desenho das listas
    // =====================================================================
    void DrawMacroItem(object? sender, DrawItemEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        bool editArea = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool selected = !editArea && (e.State & DrawItemState.Selected) != 0;
        Color bg = selected ? p.Selection : editArea ? p.Surface2 : p.Surface;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);
        if (e.Index < 0 || _macroList.Items[e.Index] is not Macro m) return;

        // Estrela: amarela = favorita; cinza = clique para favoritar
        var star = new Rectangle(e.Bounds.Left + Theme.S(4), e.Bounds.Top, StarAreaWidth - Theme.S(4), e.Bounds.Height);
        TextRenderer.DrawText(g, m.Favorite ? "★" : "☆", StarFont, star, m.Favorite ? p.Favorite : Theme.Blend(p.Muted, bg, 0.35),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int x = e.Bounds.Left + StarAreaWidth + Theme.S(2);
        int top = e.Bounds.Top + (e.Bounds.Height - BoldFont.Height - SmallFont.Height - Theme.S(4)) / 2;
        bool running = IsMacroRunning(m);
        int pillW = running ? TextRenderer.MeasureText(L.T("RODANDO"), PillFont).Width + Theme.S(10) : 0;
        TextRenderer.DrawText(g, m.Name, BoldFont, new Rectangle(x, top, e.Bounds.Right - x - Theme.S(14) - pillW, BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (running)
            DrawPill(g, "RODANDO", p.Success, bg, e.Bounds.Right - pillW - Theme.S(8), new Rectangle(0, top, 0, BoldFont.Height));

        string info = L.F("{0} · {1} passo(s)", m.TargetName, m.Steps.Count);
        if (m.ScheduleEnabled)
            info += " · " + (m.ScheduleRepeat == ScheduleRepeat.TodoDia ? L.F("todo dia às {0}", m.ScheduleTime) : L.F("às {0}", m.ScheduleTime));
        TextRenderer.DrawText(g, info, SmallFont, new Rectangle(x, top + BoldFont.Height + Theme.S(4), e.Bounds.Right - x - Theme.S(8), SmallFont.Height),
            p.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    static int StarAreaWidth => Theme.S(28);

    void DrawStepItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _stepList.Items[e.Index] is not MacroStep s) return;
        var p = Theme.Current;
        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color bg = selected ? p.Selection : p.Surface;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);

        // Número do ponto num círculo
        int d = Theme.S(24);
        var circle = new Rectangle(e.Bounds.Left + Theme.S(8), e.Bounds.Top + (e.Bounds.Height - d) / 2, d, d);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool key = s.Kind == StepKind.Tecla;
        // Número do passo: círculo azul para clique, verde para tecla
        using (var cb = new SolidBrush(key ? p.Success : p.Accent)) g.FillEllipse(cb, circle);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g, (e.Index + 1).ToString(), PillFont, circle, key ? p.SuccessText : p.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        int x = circle.Right + Theme.S(10);
        int top = e.Bounds.Top + (e.Bounds.Height - BoldFont.Height - SmallFont.Height - Theme.S(2)) / 2;
        string position = key
            ? (s.Key == 0 ? L.T("Tecla (escolha a tecla)") : L.F("Tecla {0}", HotkeyBox.Describe(s.KeyValue)))
            : L.F("Posição {0} × {1}", s.RelX.ToString("P0"), s.RelY.ToString("P0"));
        TextRenderer.DrawText(g, s.Label ?? position, BoldFont,
            new Rectangle(x, top, e.Bounds.Right - x - Theme.S(6), BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, (s.Label != null && !key ? position + " · " : "") + s.Describe(), SmallFont,
            new Rectangle(x, top + BoldFont.Height + Theme.S(2), e.Bounds.Right - x - Theme.S(6), SmallFont.Height), p.Muted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    void DrawRunItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _runList.Items[e.Index] is not RunState r) return;
        var p = Theme.Current;
        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color bg = selected ? p.Selection : p.Surface;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);

        // Bolinha: verde = rodando, amarela = pausada
        int d = Theme.S(10);
        var dot = new Rectangle(e.Bounds.Left + Theme.S(10), e.Bounds.Top + (e.Bounds.Height - d) / 2, d, d);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var db = new SolidBrush(r.Paused ? p.Favorite : p.Success)) g.FillEllipse(db, dot);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

        int x = dot.Right + Theme.S(10);
        int top = e.Bounds.Top + (e.Bounds.Height - BoldFont.Height - SmallFont.Height - Theme.S(2)) / 2;
        string mode = r.Macro.Mode == ClickMode.Real ? "REAL" : "SEGUNDO PLANO";
        int pillW = TextRenderer.MeasureText(L.T(mode), PillFont).Width + Theme.S(10);
        TextRenderer.DrawText(g, r.Macro.Name, BoldFont, new Rectangle(x, top, e.Bounds.Right - x - pillW - Theme.S(14), BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        DrawPill(g, mode, r.Macro.Mode == ClickMode.Real ? p.Accent : p.Muted, bg, e.Bounds.Right - pillW - Theme.S(8),
            new Rectangle(0, top, 0, BoldFont.Height));
        string info = string.IsNullOrEmpty(r.Text) ? L.F("{0} · iniciando...", r.Macro.TargetName) : L.F("{0} · {1}", r.Macro.TargetName, L.T(r.Text));
        TextRenderer.DrawText(g, info, SmallFont, new Rectangle(x, top + BoldFont.Height + Theme.S(2), e.Bounds.Right - x - Theme.S(8), SmallFont.Height),
            r.Paused ? p.Favorite : p.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    static void DrawPill(Graphics g, string text, Color color, Color bg, int x, Rectangle line)
    {
        text = L.T(text);
        var size = TextRenderer.MeasureText(text, PillFont);
        float h = Theme.S(20);
        var r = new RectangleF(x, line.Top + (line.Height - h) / 2f, size.Width + Theme.S(10), h);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Gfx.RoundRect(r, h / 2))
        using (var b = new SolidBrush(Theme.Blend(color, bg, 0.80)))
            g.FillPath(b, path);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g, text, PillFont, Rectangle.Round(r), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRunning();
            if (_saveTimer.Enabled) MacroStore.Save(_macros);
            _saveTimer.Dispose();
            _scheduleTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
