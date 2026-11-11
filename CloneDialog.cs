namespace HiddenApp;

/// <summary>
/// Janela para escolher o programa, qual cópia abrir (nova ou existente), o nome da cópia,
/// os argumentos e se a cópia já abre sem internet.
/// </summary>
public class CloneDialog : Form
{
    readonly TextBox _exe = new() { Width = Theme.S(470), BorderStyle = BorderStyle.FixedSingle };
    readonly ComboBox _copy = new() { Width = Theme.S(570), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
    readonly TextBox _name = new() { Width = Theme.S(570), BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Ex.: Trabalho, Pessoal, Jogo..." };
    readonly TextBox _args = new() { Width = Theme.S(570), BorderStyle = BorderStyle.FixedSingle };
    readonly ToggleSwitch _startBlocked = new("Abrir a cópia já sem internet") { Checked = true, Margin = new Padding(0, Theme.S(12), 0, Theme.S(4)) };

    string _lastExe = "";
    string _lastSuggestedArgs = "";

    /// <summary>Item da lista "Qual cópia": nova ou uma existente.</summary>
    sealed record CopyChoice(int Number, bool IsNew, string Text, string Label, string Args)
    {
        public override string ToString() => Text;
    }

    public string ExePath => _exe.Text.Trim().Trim('"');
    public string Arguments => _args.Text.Trim();
    public bool StartBlocked => _startBlocked.Checked;
    public string CopyName => _name.Text.Trim();
    /// <summary>Número da cópia escolhida (nova ou existente).</summary>
    public int CopyNumber => (_copy.SelectedItem as CopyChoice)?.Number ?? 0;

    public CloneDialog(string? initialExe)
    {
        var p = Theme.Current;
        Text = "Abrir cópia bloqueável";
        Icon = Theme.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = p.Surface;
        ForeColor = p.Text;

        foreach (Control c in new Control[] { _exe, _copy, _name, _args })
        {
            c.BackColor = p.Surface2;
            c.ForeColor = p.Text;
            c.Font = new Font("Segoe UI", 10.5f);
        }

        var browse = new RoundButton("Procurar...") { Margin = new Padding(Theme.S(8), 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe", Title = "Escolha o programa" };
            if (dlg.ShowDialog(this) == DialogResult.OK) { _exe.Text = dlg.FileName; ProgramChanged(); }
        };
        _exe.Leave += (_, _) => ProgramChanged();
        _copy.SelectedIndexChanged += (_, _) => CopyChanged();

        var ok = new RoundButton("Abrir cópia", ButtonKind.Primary) { DialogResult = DialogResult.OK, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        var cancel = new RoundButton("Cancelar") { DialogResult = DialogResult.Cancel, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) =>
        {
            ProgramChanged();
            if (!File.Exists(ExePath))
            {
                MessageBox.Show(this, "Arquivo não encontrado.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var title = new Label
        {
            Text = "Abrir cópia bloqueável", AutoSize = true, ForeColor = p.Text,
            Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, 0, 0, Theme.S(2))
        };
        var subtitle = new Label
        {
            AutoSize = true, MaximumSize = new Size(Theme.S(570), 0), ForeColor = p.Muted, Margin = new Padding(0, 0, 0, Theme.S(14)),
            Text = "Abre o programa a partir de uma cópia feita de hard links (não ocupa espaço). Cada cópia tem seu " +
                   "próprio bloqueio: dá para ter várias do mesmo programa, umas com e outras sem internet, " +
                   "e o original continua normal."
        };

        Label FieldLabel(string text) => new()
        {
            Text = text, AutoSize = true, ForeColor = p.Text, Font = new Font("Segoe UI Semibold", 9.5f),
            Margin = new Padding(0, Theme.S(8), 0, Theme.S(4))
        };
        Label Hint(string text) => new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(Theme.S(570), 0), ForeColor = p.Muted,
            Margin = new Padding(0, Theme.S(4), 0, 0)
        };

        var exeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        exeRow.Controls.AddRange(new Control[] { _exe, browse });

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
            Margin = new Padding(0, Theme.S(18), 0, 0)
        };
        buttons.Controls.AddRange(new Control[] { ok, cancel });

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.S(22), Theme.S(18), Theme.S(22), Theme.S(18)) };
        layout.Controls.Add(title);
        layout.Controls.Add(subtitle);
        layout.Controls.Add(FieldLabel("Programa (.exe)"));
        layout.Controls.Add(exeRow);
        layout.Controls.Add(FieldLabel("Qual cópia abrir"));
        layout.Controls.Add(_copy);
        layout.Controls.Add(FieldLabel("Nome da cópia (opcional)"));
        layout.Controls.Add(_name);
        layout.Controls.Add(Hint("Ajuda a diferenciar as cópias na lista. Sem nome, ela aparece como \"Cópia 1\", \"Cópia 2\"..."));
        layout.Controls.Add(FieldLabel("Argumentos extras (opcional)"));
        layout.Controls.Add(_args);
        layout.Controls.Add(Hint("Programas que só permitem uma janela aberta (Chrome, Edge, Firefox...) precisam de um parâmetro " +
                                 "para abrir separados. Para navegadores conhecidos ele é preenchido automaticamente, " +
                                 "com um perfil próprio para cada cópia."));
        layout.Controls.Add(_startBlocked);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        _exe.Text = initialExe ?? "";
        ProgramChanged();
    }

    /// <summary>Recarrega a lista de cópias quando o programa muda.</summary>
    void ProgramChanged()
    {
        string exe = ExePath;
        if (exe.Equals(_lastExe, StringComparison.OrdinalIgnoreCase)) return;
        _lastExe = exe;

        _copy.Items.Clear();
        if (!File.Exists(exe)) { _copy.Enabled = false; return; }
        _copy.Enabled = true;

        var existing = CloneManager.ListClones(exe);
        int next = existing.Count == 0 ? 1 : existing.Max(c => c.Number) + 1;
        _copy.Items.Add(new CopyChoice(next, true, $"Nova cópia (Cópia {next})", "", ""));
        foreach (var c in existing)
        {
            string state = c.IsBlocked ? "sem internet" : "com internet";
            string label = c.Label == $"Cópia {c.Number}" ? c.Label : $"{c.Label} (Cópia {c.Number})";
            _copy.Items.Add(new CopyChoice(c.Number, false, $"{label}  ·  {state}", c.Label == $"Cópia {c.Number}" ? "" : c.Label, c.Args));
        }
        _copy.SelectedIndex = 0;
    }

    /// <summary>Preenche nome e argumentos conforme a cópia escolhida.</summary>
    void CopyChanged()
    {
        if (_copy.SelectedItem is not CopyChoice choice) return;
        _name.Text = choice.Label;

        // Só troca os argumentos se o usuário não tiver digitado algo próprio
        bool userEdited = _args.Text.Length > 0 && _args.Text != _lastSuggestedArgs;
        string suggestion = !choice.IsNew && choice.Args.Length > 0
            ? choice.Args
            : CloneManager.SuggestArgs(ExePath, choice.Number);
        if (!userEdited) _args.Text = suggestion;
        _lastSuggestedArgs = suggestion;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }
}
