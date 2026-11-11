namespace HiddenApp;

/// <summary>
/// Aba "Auto clique": lista de macros, configuração da macro (janela alvo, modo, loop, pontos)
/// e prévia ao vivo da janela alvo, onde dá para clicar para marcar os pontos.
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

    // ---------- Layout ----------
    readonly TableLayoutPanel _grid = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
    readonly Card _macrosCard = new("Macros");
    readonly Card _configCard = new("Configuração da macro");
    readonly Card _previewCard = new("Janela alvo", "clique para marcar os pontos");

    readonly ListBox _macroList = NewList(52);
    readonly Label _emptyMacros = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted",
        Text = "Nenhuma macro ainda.\nClique em \"Nova\" para criar a primeira." };

    readonly Panel _editorScroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly TextBox _name = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
    readonly ComboBox _target = NewCombo();
    readonly ComboBox _mode = NewCombo("Segundo plano (não mexe no mouse)", "Clique real (move o mouse)");
    readonly ComboBox _loopKind = NewCombo("Sem parar", "Número de vezes");
    readonly NumericUpDown _loops = NewNum(1, 1_000_000, 10);
    readonly NumericUpDown _jitter = NewNum(0, 50, 0);

    readonly ListBox _stepList = NewList(42);
    readonly ComboBox _button = NewCombo("Esquerdo", "Direito", "Meio");
    readonly ComboBox _action = NewCombo("Clique", "Clique duplo", "Apertar e segurar");
    readonly NumericUpDown _hold = NewNum(10, 3_600_000, 1000);
    readonly NumericUpDown _repeat = NewNum(1, 1_000_000, 1);
    readonly NumericUpDown _interval = NewNum(1, 3_600_000, 100);
    readonly NumericUpDown _delay = NewNum(0, 3_600_000, 500);
    readonly TableLayoutPanel _stepEditor = NewForm();

    readonly RoundButton _btnNew = new("Nova", ButtonKind.Primary);
    readonly RoundButton _btnDuplicate = new("Duplicar");
    readonly RoundButton _btnDelete = new("Excluir");
    readonly RoundButton _btnAddPoint = new("+ Adicionar ponto", ButtonKind.Primary);
    readonly RoundButton _btnMovePoint = new("Reposicionar");
    readonly RoundButton _btnRemovePoint = new("Remover");
    readonly RoundButton _btnUp = new("↑");
    readonly RoundButton _btnDown = new("↓");
    readonly RoundButton _btnStart = new("▶ Iniciar (F7)", ButtonKind.Success);
    readonly RoundButton _btnStop = new("■ Parar (F8)", ButtonKind.Danger);
    readonly RoundButton _btnTest = new("Testar ponto");
    readonly RoundButton _btnHotkeys = new("⌨ Teclas de atalho");
    readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };

    readonly WindowPreview _preview = new()
    {
        Dock = DockStyle.Fill, ClientAreaOnly = true,
        EmptyMessage = "Escolha a janela alvo na configuração da macro."
    };
    readonly Label _previewHint = new() { Dock = DockStyle.Top, AutoSize = false, Tag = "muted" };

    static readonly Font BoldFont = new("Segoe UI Semibold", 9.5f);
    static readonly Font SmallFont = new("Segoe UI", 8.5f);
    static readonly Font PillFont = new("Segoe UI Semibold", 8f);
    static readonly Font SectionFont = new("Segoe UI Semibold", 10f);

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
        Padding = new Padding(Theme.S(11));

        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _grid.Controls.Add(_macrosCard, 0, 0);
        _grid.Controls.Add(_configCard, 1, 0);
        _grid.Controls.Add(_previewCard, 2, 0);
        Controls.Add(_grid);

        BuildMacrosCard();
        BuildConfigCard();
        BuildPreviewCard();
        SetupTips();

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); MacroStore.Save(_macros); };
        _runner.Progress += text => OnUi(() => SetStatus(text));
        _runner.Stopped += reason => OnUi(() =>
        {
            SetStatus(reason);
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
        BorderStyle = BorderStyle.FixedSingle, Width = Theme.S(130), Anchor = AnchorStyles.Left
    };

    static TableLayoutPanel NewForm()
    {
        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static void AddRow(TableLayoutPanel form, string label, Control control, string? suffix = null)
    {
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, Theme.S(6), Theme.S(12), Theme.S(6)) };
        Control field = control;
        if (suffix != null)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left };
            control.Margin = new Padding(0, Theme.S(3), Theme.S(6), Theme.S(3));
            row.Controls.Add(control);
            row.Controls.Add(new Label { Text = suffix, AutoSize = true, Tag = "muted", Margin = new Padding(0, Theme.S(7), 0, 0) });
            field = row;
        }
        else control.Margin = new Padding(0, Theme.S(3), 0, Theme.S(3));
        form.Controls.Add(l);
        form.Controls.Add(field);
    }

    static Label Section(string text) => new()
    {
        Text = text, AutoSize = true, Font = SectionFont, Dock = DockStyle.Top,
        Margin = new Padding(0, Theme.S(14), 0, Theme.S(4)), Padding = new Padding(0, Theme.S(10), 0, Theme.S(2))
    };

    static FlowLayoutPanel Toolbar(params Control[] buttons)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, Theme.S(4), 0, 0) };
        f.Controls.AddRange(buttons);
        return f;
    }

    void BuildMacrosCard()
    {
        _macroList.DrawItem += DrawMacroItem;
        _macroList.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            LoadMacro(_macroList.SelectedItem as Macro);
        };
        _btnNew.Click += (_, _) => NewMacro();
        _btnDuplicate.Click += (_, _) =>
        {
            if (_current == null) return;
            var copy = _current.Clone();
            copy.Name = _current.Name + " (cópia)";
            _macros.Add(copy);
            SaveNow();
            RefreshMacroList(copy);
        };
        _btnDelete.Click += (_, _) =>
        {
            if (_current == null) return;
            if (MessageBox.Show($"Excluir a macro \"{_current.Name}\"?", "HiddenApp",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int i = _macros.IndexOf(_current);
            _macros.Remove(_current);
            SaveNow();
            RefreshMacroList(_macros.Count == 0 ? null : _macros[Math.Clamp(i, 0, _macros.Count - 1)]);
        };

        _macrosCard.Controls.Add(_macroList);
        _macrosCard.Controls.Add(_emptyMacros);
        _macrosCard.Controls.Add(Toolbar(_btnNew, _btnDuplicate, _btnDelete));
    }

    void BuildConfigCard()
    {
        // ----- Macro -----
        var form = NewForm();
        AddRow(form, "Nome", _name);
        AddRow(form, "Janela alvo", _target);
        AddRow(form, "Modo de clique", _mode);
        var loopRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Dock = DockStyle.Fill };
        _loopKind.Dock = DockStyle.None;
        _loopKind.Width = Theme.S(170);
        _loopKind.Margin = new Padding(0, Theme.S(3), Theme.S(8), Theme.S(3));
        _loops.Margin = new Padding(0, Theme.S(3), 0, Theme.S(3));
        loopRow.Controls.Add(_loopKind);
        loopRow.Controls.Add(_loops);
        AddRow(form, "Repetir a sequência", loopRow);
        AddRow(form, "Variação aleatória", _jitter, "% nos tempos (0 = exato)");

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
        };
        _mode.SelectedIndexChanged += (_, _) => Change(m => m.Mode = _mode.SelectedIndex == 1 ? ClickMode.Real : ClickMode.SegundoPlano);
        _loopKind.SelectedIndexChanged += (_, _) =>
        {
            _loops.Enabled = _loopKind.SelectedIndex == 1;
            Change(m => m.Loops = _loopKind.SelectedIndex == 1 ? (int)_loops.Value : 0);
        };
        _loops.ValueChanged += (_, _) => Change(m => m.Loops = _loopKind.SelectedIndex == 1 ? (int)_loops.Value : 0);
        _jitter.ValueChanged += (_, _) => Change(m => m.JitterPercent = (int)_jitter.Value);

        // ----- Pontos -----
        _stepList.Dock = DockStyle.Top;
        _stepList.Height = Theme.S(42) * 4 + 2;
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

        AddRow(_stepEditor, "Botão do mouse", _button);
        AddRow(_stepEditor, "Ação", _action);
        AddRow(_stepEditor, "Segurar por", _hold, "ms");
        AddRow(_stepEditor, "Cliques neste ponto", _repeat, "vezes seguidas");
        AddRow(_stepEditor, "Intervalo entre cliques", _interval, "ms");
        AddRow(_stepEditor, "Esperar antes do próximo", _delay, "ms");
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

        // Conteúdo rolável (de baixo para cima por causa do Dock = Top)
        var content = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, Theme.S(8), Theme.S(8)) };
        var stepSection = Section("Ponto selecionado");
        var pointsSection = Section("Pontos de clique — rodam em loop, na ordem");
        var pointsHint = new Label
        {
            AutoSize = false, Dock = DockStyle.Top, Tag = "muted", Height = Theme.S(40),
            Text = $"Clique em \"+ Adicionar ponto\" e depois na prévia ao lado, ou posicione o mouse na janela alvo e aperte {_captureKey}."
        };
        pointsHint.Name = "pointsHint";
        content.Controls.Add(_stepEditor);
        content.Controls.Add(stepSection);
        content.Controls.Add(Toolbar(_btnAddPoint, _btnMovePoint, _btnRemovePoint, _btnUp, _btnDown));
        content.Controls.Add(_stepList);
        content.Controls.Add(pointsHint);
        content.Controls.Add(pointsSection);
        content.Controls.Add(form);
        _editorScroll.Controls.Add(content);

        // ----- Barra de ações (fixa embaixo) -----
        _btnStart.Click += (_, _) => StartSelected();
        _btnStop.Click += (_, _) => StopRunning();
        _btnTest.Click += (_, _) => TestStep();
        _btnHotkeys.Click += (_, _) => HotkeysRequested?.Invoke();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, Theme.S(8), 0, 0) };
        actions.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnTest, _btnHotkeys });
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(84) };
        _status.Dock = DockStyle.Bottom;
        _status.Height = Theme.S(24);
        bottom.Controls.Add(actions);
        bottom.Controls.Add(_status);
        bottom.Paint += (_, e) => { using var pen = new Pen(Theme.Current.Border); e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0); };

        _configCard.Controls.Add(_editorScroll);
        _configCard.Controls.Add(bottom);
    }

    void BuildPreviewCard()
    {
        _previewHint.Height = Theme.S(44);
        _preview.PointPicked += (rx, ry) => ApplyPoint(rx, ry, null);
        _previewCard.Controls.Add(_preview);
        _previewCard.Controls.Add(_previewHint);
        UpdatePreviewHint();
    }

    void SetupTips()
    {
        _tips.Set(_btnNew, "Nova macro", "Cria uma macro vazia. Depois escolha a janela alvo e adicione os pontos de clique.");
        _tips.Set(_btnDuplicate, "Duplicar", "Cria uma cópia da macro selecionada, para fazer variações sem mexer na original.");
        _tips.Set(_btnDelete, "Excluir", "Apaga a macro selecionada.");
        _tips.Set(_btnAddPoint, "Adicionar ponto",
            "Depois de clicar aqui, clique na prévia (à direita) no lugar onde o auto clique deve clicar.\n\n" +
            "Atalho: posicione o mouse na própria janela alvo e aperte a tecla de captura.");
        _tips.Set(_btnMovePoint, "Reposicionar", "Muda o lugar do ponto selecionado: clique na prévia na nova posição.");
        _tips.Set(_btnRemovePoint, "Remover", "Apaga o ponto selecionado.");
        _tips.Set(_btnUp, "Subir", "Move o ponto selecionado para antes na sequência.");
        _tips.Set(_btnDown, "Descer", "Move o ponto selecionado para depois na sequência.");
        _tips.Set(_btnTest, "Testar ponto",
            "Dá um único clique no ponto selecionado, para conferir se acerta o lugar certo e se o modo de clique funciona nesse programa.");
        _tips.Set(_btnHotkeys, "Teclas de atalho", "Troca as teclas de iniciar, parar e capturar ponto. Elas funcionam mesmo com outra janela em uso.");
        _tips.Set(_mode, "Modo de clique",
            "Segundo plano: manda o clique direto para a janela, sem mexer no seu mouse — você pode continuar usando o PC. " +
            "Nem todo programa aceita (muitos jogos ignoram).\n\n" +
            "Clique real: move o mouse e clica de verdade. Funciona em quase tudo, mas a janela precisa estar na frente; " +
            "se você trocar de janela, o auto clique pausa sozinho.");
        _tips.Set(_jitter, "Variação aleatória", "Muda um pouco cada intervalo, para os cliques não terem um ritmo perfeitamente exato.");
    }

    // =====================================================================
    //  Tema
    // =====================================================================
    public void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Back;
        _grid.BackColor = p.Back;
        foreach (var card in new[] { _macrosCard, _configCard, _previewCard }) card.BackColor = p.Surface;
        _preview.BackColor = p.Back;
        ThemeChildren(this, p);
        UpdatePreviewHint();
        foreach (var c in new Control[] { _macroList, _stepList, _editorScroll })
            Theme.ApplyScrollbars(c);
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
        _name.Focus();
        _name.SelectAll();
        SetStatus("Macro criada. Escolha a janela alvo e adicione os pontos de clique.");
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
            _mode.SelectedIndex = m?.Mode == ClickMode.Real ? 1 : 0;
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
                var item = new TargetItem(w, $"{w.Title}  —  {w.ProcessName}");
                _target.Items.Add(item);
                if (w.Handle == _targetHandle) selected = item;
            }
            if (selected == null && _current.TargetExe != null)
            {
                selected = new TargetItem(null, $"{_current.TargetTitle ?? _current.TargetName}  —  (não está aberto)");
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
        LoadStep();
    }

    MacroStep? SelectedStep => _stepList.SelectedItem as MacroStep;

    void LoadStep()
    {
        var s = SelectedStep;
        _loading = true;
        try
        {
            _stepEditor.Enabled = s != null && !_runner.IsRunning;
            _button.SelectedIndex = (int)(s?.Button ?? ClickButton.Esquerdo);
            _action.SelectedIndex = (int)(s?.Action ?? ClickAction.Clique);
            _hold.Value = Math.Clamp(s?.HoldMs ?? 1000, (int)_hold.Minimum, (int)_hold.Maximum);
            _repeat.Value = Math.Clamp(s?.Repeat ?? 1, 1, (int)_repeat.Maximum);
            _interval.Value = Math.Clamp(s?.IntervalMs ?? 100, 1, (int)_interval.Maximum);
            _delay.Value = Math.Clamp(s?.DelayAfterMs ?? 500, 0, (int)_delay.Maximum);
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
    //  Marcar pontos (prévia ou tecla de captura)
    // =====================================================================
    void TogglePick(PickKind kind)
    {
        if (_pick == kind) { CancelPick(); return; }
        if (_current == null) { Say("Crie ou selecione uma macro primeiro."); return; }
        if (kind == PickKind.Move && SelectedStep == null) { Say("Selecione o ponto que quer reposicionar."); return; }
        if (_targetHandle == IntPtr.Zero || !Native.IsWindow(_targetHandle))
        {
            Say($"Escolha a janela alvo (ela precisa estar aberta). Ou posicione o mouse na janela e aperte {_captureKey}.");
            return;
        }
        _pick = kind;
        _preview.PickMode = true;
        _btnAddPoint.Text = kind == PickKind.Add ? "Cancelar" : "+ Adicionar ponto";
        _btnMovePoint.Text = kind == PickKind.Move ? "Cancelar" : "Reposicionar";
        UpdatePreviewHint();
        SetStatus(kind == PickKind.Add ? "Clique na prévia no lugar do novo ponto." : "Clique na prévia na nova posição do ponto.");
    }

    void CancelPick()
    {
        _pick = PickKind.None;
        _preview.PickMode = false;
        _btnAddPoint.Text = "+ Adicionar ponto";
        _btnMovePoint.Text = "Reposicionar";
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
        else if (!m.TargetExe.Equals(info.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            Say($"O mouse está sobre outro programa ({info.ProcessName}). A janela alvo desta macro é {m.TargetName}.", notify: true);
            return;
        }
        _targetHandle = window;
        RefreshTargets();
        UpdatePreview();
        ApplyPoint(rx, ry, notify: true);
    }

    void ApplyPoint(double rx, double ry, bool? notify)
    {
        if (_current == null) return;
        MacroStep step;
        if (_pick == PickKind.Move && SelectedStep is MacroStep sel)
        {
            step = sel;
            step.RelX = rx; step.RelY = ry;
            Say($"Ponto {_current.Steps.IndexOf(step) + 1} reposicionado.", notify ?? false);
        }
        else
        {
            // O ponto novo herda a configuração do último, para agilizar sequências parecidas
            step = _current.Steps.LastOrDefault()?.Clone() ?? new MacroStep();
            step.RelX = rx; step.RelY = ry;
            _current.Steps.Add(step);
            Say($"Ponto {_current.Steps.Count} adicionado ({rx:P0} × {ry:P0}).", notify ?? false);
        }
        CancelPick();
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
            string msg = $"Rodando \"{m.Name}\" — aperte {_stopKey} para parar.";
            SetStatus(msg);
            Say(msg, notify: true);
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
        _editorScroll.Enabled = has && !running;
        _btnStart.Enabled = has && !running;
        _btnStop.Enabled = running;
        _btnTest.Enabled = has && !running && SelectedStep != null;
        _btnNew.Enabled = _btnDuplicate.Enabled = _btnDelete.Enabled = !running;
        _btnDuplicate.Enabled &= has;
        _btnDelete.Enabled &= has;
        _macroList.Enabled = !running;
        _btnMovePoint.Enabled = _btnRemovePoint.Enabled = _btnUp.Enabled = _btnDown.Enabled = SelectedStep != null;
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
        if (_pick != PickKind.None)
        {
            _previewHint.Text = _pick == PickKind.Add
                ? "Clique na prévia no lugar do novo ponto."
                : "Clique na prévia na nova posição do ponto selecionado.";
            _previewHint.ForeColor = p.Accent;
        }
        else
        {
            _previewHint.Text = $"Use \"+ Adicionar ponto\" e clique aqui, ou posicione o mouse na janela alvo e aperte {_captureKey}.";
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
        foreach (Control c in Controls.Find("pointsHint", true))
            c.Text = $"Clique em \"+ Adicionar ponto\" e depois na prévia ao lado, ou posicione o mouse na janela alvo e aperte {capture}.";
        UpdatePreviewHint();
    }

    void SetStatus(string text) => _status.Text = text;

    void Say(string text, bool notify = false)
    {
        SetStatus(text);
        StatusMessage?.Invoke(text);
        if (notify && FindForm() is Form f && !f.Visible) Notify?.Invoke("Auto clique", text);
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
        int pillW = running ? TextRenderer.MeasureText("RODANDO", PillFont).Width + Theme.S(10) : 0;
        TextRenderer.DrawText(g, m.Name, BoldFont, new Rectangle(x, top, e.Bounds.Width - Theme.S(24) - pillW, BoldFont.Height), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (running)
            DrawPill(g, "RODANDO", p.Success, bg, e.Bounds.Right - pillW - Theme.S(8), new Rectangle(0, top, 0, BoldFont.Height));

        string info = $"{m.TargetName} · {m.Steps.Count} ponto(s) · {(m.Mode == ClickMode.Real ? "real" : "segundo plano")}";
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
        TextRenderer.DrawText(g, $"Posição {s.RelX:P0} × {s.RelY:P0}", BoldFont, new Point(x, top), p.Text);
        TextRenderer.DrawText(g, s.Describe(), SmallFont,
            new Rectangle(x, top + BoldFont.Height + Theme.S(2), e.Bounds.Right - x - Theme.S(6), SmallFont.Height), p.Muted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    static void DrawPill(Graphics g, string text, Color color, Color bg, int x, Rectangle line)
    {
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
