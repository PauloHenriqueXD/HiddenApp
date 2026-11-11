namespace HiddenApp;

/// <summary>
/// Aba "Auto clique". Layout em três colunas, sem rolagem:
///   [Macros + configuração da macro] [Pontos + ponto selecionado] [Prévia da janela alvo]
/// e uma barra fixa embaixo com Iniciar / Parar / Testar / Teclas de atalho.
/// </summary>
public class AutoClickPage : Panel
{
    readonly AppSettings _settings;
    readonly ThemedToolTip _tips;
    readonly List<Macro> _macros = MacroStore.Load();
    readonly ClickRunner _runner = new();
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 600 };

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
    readonly TableLayoutPanel _leftColumn = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
    readonly Card _macrosCard = new("Macros");
    readonly Card _settingsCard = new("Macro selecionada");
    readonly Card _pointsCard = new("Pontos de clique", "rodam em loop, na ordem");
    readonly Card _previewCard = new("Janela alvo");
    readonly Panel _actionBar = new() { Dock = DockStyle.Bottom, Height = Theme.S(58), Padding = new Padding(Theme.S(7), Theme.S(8), Theme.S(7), 0) };

    readonly ListBox _macroList = NewList(50);
    readonly Label _emptyMacros = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted",
        Text = "Nenhuma macro ainda.\nClique no + verde para criar a primeira\nou importe uma macro recebida." };

    readonly TableLayoutPanel _settingsForm = NewForm(2);
    readonly TextBox _name = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
    readonly ComboBox _target = NewCombo();
    readonly ComboBox _mode = NewCombo("Segundo plano (não mexe no mouse)", "Clique real (move o mouse)");
    readonly ComboBox _loopKind = NewCombo("Sem parar", "Número de vezes");
    readonly NumericUpDown _loops = NewNum(1, 1_000_000, 10);
    readonly NumericUpDown _jitter = NewNum(0, 50, 0);

    readonly ListBox _stepList = NewList(42);
    readonly Label _emptySteps = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted" };
    readonly TableLayoutPanel _stepEditor = NewForm(4);
    readonly ComboBox _button = NewCombo("Esquerdo", "Direito", "Meio");
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
    readonly ContextMenuStrip _exportMenu = new();
    readonly RoundButton _btnAddPoint = new("+ Adicionar pontos", ButtonKind.Primary);
    readonly RoundButton _btnMovePoint = RoundButton.IconButton(Theme.Glyph.Pin, "Reposicionar ponto", ButtonKind.Secondary);
    readonly RoundButton _btnRemovePoint = RoundButton.IconButton(Theme.Glyph.Delete, "Remover ponto", ButtonKind.Danger);
    readonly RoundButton _btnUp = RoundButton.IconButton(Theme.Glyph.Up, "Subir ponto", ButtonKind.Secondary);
    readonly RoundButton _btnDown = RoundButton.IconButton(Theme.Glyph.Down, "Descer ponto", ButtonKind.Secondary);
    readonly TextBox _stepLabel = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, PlaceholderText = "Ex.: Botão Coletar, Fechar janela..." };
    readonly TableLayoutPanel _stepNameForm = NewForm(2);
    readonly RoundButton _btnStart = new("▶ Iniciar (F7)", ButtonKind.Success);
    readonly RoundButton _btnStop = new("■ Parar (F8)", ButtonKind.Danger);
    readonly RoundButton _btnTest = new("Testar ponto");
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

    public event Action<string>? StatusMessage;
    public event Action? RunningChanged;
    /// <summary>Aviso para mostrar na bandeja quando a janela do app está escondida.</summary>
    public event Action<string, string>? Notify;
    public event Action? HotkeysRequested;

    public bool IsRunning => _runner.IsRunning;
    public string? RunningName => _runner.Current?.Name;
    public IReadOnlyList<Macro> Macros => _macros;

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
        _leftColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        _leftColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        _leftColumn.Controls.Add(_macrosCard, 0, 0);
        _leftColumn.Controls.Add(_settingsCard, 0, 1);
        _grid.Controls.Add(_leftColumn, 0, 0);
        _grid.Controls.Add(_pointsCard, 1, 0);
        _grid.Controls.Add(_previewCard, 2, 0);

        BuildMacrosCard();
        BuildSettingsCard();
        BuildPointsCard();
        BuildPreviewCard();
        BuildActionBar();
        SetupTips();

        Controls.Add(_grid);
        Controls.Add(_actionBar);

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); MacroStore.Save(_macros); };
        _runner.Progress += text => OnUi(() => SetStatus(text));
        _runner.Stopped += reason => OnUi(() =>
        {
            UpdateButtons();
            _macroList.Invalidate();
            RunningChanged?.Invoke();
            Say(reason, notify: true);
        });

        RefreshMacroList(_macros.FirstOrDefault(m => m.Id == _settings.LastMacroId) ?? _macros.FirstOrDefault());
        ApplyTheme();
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

    static void AddField(TableLayoutPanel form, string label, Control field)
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
        _macroList.SelectedIndexChanged += (_, _) => { if (!_loading) LoadMacro(_macroList.SelectedItem as Macro); };
        _btnNew.Click += (_, _) => NewMacro();
        _btnDuplicate.Click += (_, _) =>
        {
            if (_current == null) return;
            var copy = _current.Clone();
            copy.Name = L.F("{0} (cópia)", _current.Name);
            _macros.Add(copy);
            SaveNow();
            RefreshMacroList(copy);
        };
        _btnDelete.Click += (_, _) =>
        {
            if (_current == null) return;
            if (Msg.Show($"Excluir a macro \"{_current.Name}\"?", "HiddenApp",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int i = _macros.IndexOf(_current);
            _macros.Remove(_current);
            SaveNow();
            RefreshMacroList(_macros.Count == 0 ? null : _macros[Math.Clamp(i, 0, _macros.Count - 1)]);
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

        _macrosCard.Controls.Add(_macroList);
        _macrosCard.Controls.Add(_emptyMacros);
        _macrosCard.Controls.Add(Toolbar(_btnNew, _btnDuplicate, _btnDelete, _btnImport, _btnExport));
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

        _settingsCard.Controls.Add(_settingsForm);
    }

    void BuildPointsCard()
    {
        _stepList.DrawItem += DrawStepItem;
        _stepList.SelectedIndexChanged += (_, _) => { if (!_loading) LoadStep(); };
        _btnAddPoint.Click += (_, _) => TogglePick(PickKind.Add);
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

        // Nome do ponto (linha inteira) e, abaixo, dois campos por linha para caber sem rolagem
        AddField(_stepNameForm, "Nome", _stepLabel);
        _stepLabel.TextChanged += (_, _) => ChangeStep(s => s.Label = string.IsNullOrWhiteSpace(_stepLabel.Text) ? null : _stepLabel.Text.Trim());
        AddField(_stepEditor, "Botão", _button);
        AddField(_stepEditor, "Ação", _action);
        AddField(_stepEditor, "Cliques", _repeat);
        AddField(_stepEditor, "Intervalo (ms)", _interval);
        AddField(_stepEditor, "Segurar (ms)", _hold);
        AddField(_stepEditor, "Espera depois (ms)", _delay);
        _button.SelectedIndexChanged += (_, _) => ChangeStep(s => s.Button = (ClickButton)Math.Max(0, _button.SelectedIndex));
        _action.SelectedIndexChanged += (_, _) =>
        {
            _hold.Enabled = _action.SelectedIndex == 2;
            ChangeStep(s => s.Action = (ClickAction)Math.Max(0, _action.SelectedIndex));
        };
        _hold.ValueChanged += (_, _) => ChangeStep(s => s.HoldMs = (int)_hold.Value);
        _repeat.ValueChanged += (_, _) => { _interval.Enabled = _repeat.Value > 1; ChangeStep(s => s.Repeat = (int)_repeat.Value); };
        _interval.ValueChanged += (_, _) => ChangeStep(s => s.IntervalMs = (int)_interval.Value);
        _delay.ValueChanged += (_, _) => ChangeStep(s => s.DelayAfterMs = (int)_delay.Value);

        var editorTitle = new Label
        {
            Text = "Ponto selecionado", AutoSize = false, Dock = DockStyle.Top, Height = Theme.S(30),
            Font = new Font("Segoe UI Semibold", 9.5f), TextAlign = ContentAlignment.BottomLeft
        };
        var editor = new Panel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        editor.Controls.Add(_stepEditor);
        editor.Controls.Add(_stepNameForm);
        editor.Controls.Add(editorTitle);
        editor.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, 0, editor.Width, 0); };

        _pointsCard.Controls.Add(_stepList);
        _pointsCard.Controls.Add(_emptySteps);
        _pointsCard.Controls.Add(Toolbar(_btnAddPoint, _btnMovePoint, _btnRemovePoint, _btnUp, _btnDown));
        _pointsCard.Controls.Add(editor);
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
        _btnStop.Click += (_, _) => StopRunning();
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
        _tips.Set(_stepLabel, "Nome do ponto", "Um nome ou descrição opcional, para quem ver a macro entender o que cada ponto faz.");
        _tips.Set(_btnAddPoint, "Adicionar pontos",
            "Liga o modo de adicionar: cada clique na prévia (à direita) vira um ponto, na ordem. Clique em \"✓ Concluir\" quando terminar.\n\n" +
            "Atalho: posicione o mouse na própria janela alvo e aperte a tecla de captura.");
        _tips.Set(_btnMovePoint, "Reposicionar ponto", "Muda o lugar do ponto selecionado: depois de clicar aqui, clique na prévia na nova posição.");
        _tips.Set(_btnRemovePoint, "Remover ponto", "Apaga o ponto selecionado.");
        _tips.Set(_btnUp, "Subir ponto", "Move o ponto selecionado para antes na sequência.");
        _tips.Set(_btnDown, "Descer ponto", "Move o ponto selecionado para depois na sequência.");
        _tips.Set(_btnTest, "Testar ponto",
            "Dá um único clique no ponto selecionado, para conferir se acerta o lugar e se o modo de clique funciona nesse programa.");
        _tips.Set(_btnHotkeys, "Teclas de atalho", "Troca as teclas de iniciar, parar e capturar ponto. Elas funcionam mesmo com outra janela em uso.");
        _tips.Set(_mode, "Modo de clique",
            "Segundo plano: manda o clique para a janela sem mexer no mouse — você continua usando o PC. " +
            "Nem todo programa aceita (muitos jogos ignoram).\n\n" +
            "Clique real: move o mouse e clica de verdade. Funciona em quase tudo, mas a janela precisa estar na frente; " +
            "se você trocar de janela, o auto clique pausa sozinho.");
        _tips.Set(_loopKind, "Repetir", "Sem parar: a sequência de pontos roda até você apertar a tecla de parar.\nNúmero de vezes: roda a sequência inteira essa quantidade de vezes.");
        _tips.Set(_jitter, "Variação aleatória", "Muda um pouco cada intervalo (em %), para os cliques não terem um ritmo perfeitamente exato. 0 = tempos exatos.");
        _tips.Set(_repeat, "Cliques", "Quantos cliques seguidos neste mesmo ponto antes de ir para o próximo.");
        _tips.Set(_interval, "Intervalo", "Tempo entre os cliques seguidos neste ponto, em milissegundos (1000 ms = 1 segundo).");
        _tips.Set(_hold, "Segurar", "Por quanto tempo o botão fica apertado (só na ação \"Segurar\").");
        _tips.Set(_delay, "Espera depois", "Tempo de espera depois deste ponto, antes de ir para o próximo.");
    }

    // =====================================================================
    //  Tema
    // =====================================================================
    public void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Back;
        _grid.BackColor = p.Back;
        _leftColumn.BackColor = p.Back;
        _actionBar.BackColor = p.Back;
        foreach (var card in new[] { _macrosCard, _settingsCard, _pointsCard, _previewCard }) card.BackColor = p.Surface;
        _preview.BackColor = p.Back;
        ThemeChildren(this, p);
        UpdatePreviewHint();
        foreach (var c in new Control[] { _macroList, _stepList }) Theme.ApplyScrollbars(c);
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
        var m = new Macro { Name = $"Macro {_macros.Count + 1}" };
        _macros.Add(m);
        SaveNow();
        RefreshMacroList(m);
        _target.Focus();
        SetStatus("Macro criada. Agora escolha a janela alvo e adicione os pontos de clique.");
    }

    void RefreshMacroList(Macro? select)
    {
        _loading = true;
        _macroList.BeginUpdate();
        _macroList.Items.Clear();
        foreach (var m in _macros) _macroList.Items.Add(m);
        _macroList.EndUpdate();
        _loading = false;
        bool any = _macros.Count > 0;
        _macroList.Visible = any;
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
            _mode.SelectedIndex = Math.Max(0, Array.IndexOf(ModeOrder, m?.Mode ?? ClickMode.SegundoPlano));
            _loopKind.SelectedIndex = (m?.Loops ?? 0) > 0 ? 1 : 0;
            _loops.Value = Math.Clamp(m?.Loops > 0 ? m.Loops : 10, (int)_loops.Minimum, (int)_loops.Maximum);
            _loops.Enabled = _loopKind.SelectedIndex == 1;
            _jitter.Value = Math.Clamp(m?.JitterPercent ?? 0, 0, 50);
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
                ? "Nenhum ponto ainda.\n\n1. Escolha a janela alvo (à esquerda)\n2. Clique em \"+ Adicionar pontos\"\n3. Clique na prévia nos lugares desejados"
                : $"Nenhum ponto ainda.\n\nClique em \"+ Adicionar pontos\" e depois na prévia,\nou posicione o mouse na janela alvo e aperte {_captureKey}.";
    }

    MacroStep? SelectedStep => _stepList.SelectedItem as MacroStep;

    void LoadStep()
    {
        var s = SelectedStep;
        _loading = true;
        try
        {
            _stepLabel.Text = s?.Label ?? "";
            _button.SelectedIndex = (int)(s?.Button ?? ClickButton.Esquerdo);
            _action.SelectedIndex = (int)(s?.Action ?? ClickAction.Clique);
            _hold.Value = Math.Clamp(s?.HoldMs ?? 100, (int)_hold.Minimum, (int)_hold.Maximum);
            _repeat.Value = Math.Clamp(s?.Repeat ?? 1, 1, (int)_repeat.Maximum);
            _interval.Value = Math.Clamp(s?.IntervalMs ?? 100, 1, (int)_interval.Maximum);
            _delay.Value = Math.Clamp(s?.DelayAfterMs ?? 100, 0, (int)_delay.Maximum);
            _hold.Enabled = _action.SelectedIndex == 2;
            _interval.Enabled = _repeat.Value > 1;
        }
        finally { _loading = false; }
        UpdateButtons();
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
        if (kind == PickKind.Move && SelectedStep == null) { Say("Selecione o ponto que quer reposicionar."); return; }
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
        if (_runner.IsRunning) return;
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
            // O ponto novo herda a configuração do último, para agilizar sequências parecidas
            step = _current.Steps.LastOrDefault()?.Clone() ?? new MacroStep();
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
    public void StartMacro(Macro macro)
    {
        if (_runner.IsRunning) return;
        _macroList.SelectedItem = macro;
        StartSelected();
    }

    /// <summary>Tecla de iniciar (F7) ou botão "Iniciar".</summary>
    public void StartSelected()
    {
        if (_runner.IsRunning) return;
        CancelPick();
        var m = _current ?? _macros.FirstOrDefault(x => x.Id == _settings.LastMacroId);
        if (m == null) { Say("Crie uma macro na aba Auto clique antes de iniciar.", notify: true); return; }
        if (m.Steps.Count == 0) { Say($"A macro \"{m.Name}\" ainda não tem pontos de clique.", notify: true); return; }

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
            _runner.Start(m, window);
            Say($"Rodando \"{m.Name}\" — aperte {_stopKey} para parar.", notify: true);
        }
        catch (Exception ex) { Say(ex.Message, notify: true); }
        UpdateButtons();
        _macroList.Invalidate();
        RunningChanged?.Invoke();
    }

    /// <summary>Tecla de parar (F8) ou botão "Parar".</summary>
    public void StopRunning() => _runner.Stop();

    void TestStep()
    {
        if (_runner.IsRunning || _current == null || SelectedStep is not MacroStep s) { Say("Selecione um ponto para testar."); return; }
        if (_targetHandle == IntPtr.Zero || !Native.IsWindow(_targetHandle)) { Say("A janela alvo não está aberta."); return; }
        CancelPick();
        var test = new Macro
        {
            Name = "Teste", Mode = _current.Mode, Loops = 1, TargetExe = _current.TargetExe,
            Steps = { new MacroStep { RelX = s.RelX, RelY = s.RelY, Button = s.Button, Action = s.Action, HoldMs = s.HoldMs, Repeat = 1, DelayAfterMs = 0 } }
        };
        try { _runner.Start(test, _targetHandle); UpdateButtons(); }
        catch (Exception ex) { Say(ex.Message); }
    }

    void UpdateButtons()
    {
        bool running = _runner.IsRunning;
        bool has = _current != null;
        _settingsForm.Enabled = has && !running;
        _stepEditor.Enabled = _stepNameForm.Enabled = has && !running && SelectedStep != null;
        _btnStart.Enabled = has && !running;
        _btnStop.Enabled = running;
        _btnTest.Enabled = has && !running && SelectedStep != null;
        _btnNew.Enabled = _btnImport.Enabled = _btnExport.Enabled = !running;
        _btnDuplicate.Enabled = _btnDelete.Enabled = has && !running;
        _btnAddPoint.Enabled = has && !running;
        _btnMovePoint.Enabled = _btnRemovePoint.Enabled = _btnUp.Enabled = _btnDown.Enabled = SelectedStep != null && !running;
        _macroList.Enabled = !running;
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
        _tips.Set(_btnStop, "Parar", $"Para o auto clique na hora. Atalho: {stop} (funciona mesmo com outra janela em uso).");
        UpdatePreviewHint();
        UpdateEmptySteps();
    }

    void SetStatus(string text) => _status.Text = L.T(text);

    void Say(string text, bool notify = false)
    {
        SetStatus(text);
        StatusMessage?.Invoke(text);
        if (notify && FindForm() is Form f && !f.Visible) Notify?.Invoke("Auto clique", L.T(text));
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
        if (e.Index < 0 || _macroList.Items[e.Index] is not Macro m) return;
        var p = Theme.Current;
        var g = e.Graphics;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color bg = selected ? p.Selection : p.Surface;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);

        int x = e.Bounds.Left + Theme.S(10);
        int top = e.Bounds.Top + (e.Bounds.Height - BoldFont.Height - SmallFont.Height - Theme.S(4)) / 2;
        bool running = _runner.IsRunning && _runner.Current?.Id == m.Id;
        int pillW = running ? TextRenderer.MeasureText(L.T("RODANDO"), PillFont).Width + Theme.S(10) : 0;
        TextRenderer.DrawText(g, m.Name, BoldFont, new Rectangle(x, top, e.Bounds.Width - Theme.S(24) - pillW, BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (running)
            DrawPill(g, "RODANDO", p.Success, bg, e.Bounds.Right - pillW - Theme.S(8), new Rectangle(0, top, 0, BoldFont.Height));

        string info = L.F("{0} · {1} ponto(s)", m.TargetName, m.Steps.Count);
        TextRenderer.DrawText(g, info, SmallFont, new Rectangle(x, top + BoldFont.Height + Theme.S(4), e.Bounds.Width - Theme.S(20), SmallFont.Height),
            p.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

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
        using (var cb = new SolidBrush(p.Accent)) g.FillEllipse(cb, circle);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g, (e.Index + 1).ToString(), PillFont, circle, p.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        int x = circle.Right + Theme.S(10);
        int top = e.Bounds.Top + (e.Bounds.Height - BoldFont.Height - SmallFont.Height - Theme.S(2)) / 2;
        string position = L.F("Posição {0} × {1}", s.RelX.ToString("P0"), s.RelY.ToString("P0"));
        TextRenderer.DrawText(g, s.Label ?? position, BoldFont,
            new Rectangle(x, top, e.Bounds.Right - x - Theme.S(6), BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, (s.Label != null ? position + " · " : "") + s.Describe(), SmallFont,
            new Rectangle(x, top + BoldFont.Height + Theme.S(2), e.Bounds.Right - x - Theme.S(6), SmallFont.Height), p.Muted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
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
            _runner.Stop();
            if (_saveTimer.Enabled) MacroStore.Save(_macros);
            _saveTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
