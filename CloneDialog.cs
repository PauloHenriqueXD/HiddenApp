namespace HiddenApp;

/// <summary>Janela para escolher o programa, os argumentos e se a cópia já abre sem internet.</summary>
public class CloneDialog : Form
{
    readonly TextBox _exe = new() { Width = Theme.S(470), BorderStyle = BorderStyle.FixedSingle };
    readonly TextBox _args = new() { Width = Theme.S(570), BorderStyle = BorderStyle.FixedSingle };
    readonly ToggleSwitch _startBlocked = new("Abrir a cópia já sem internet") { Checked = true, Margin = new Padding(0, Theme.S(12), 0, Theme.S(4)) };

    public string ExePath => _exe.Text.Trim().Trim('"');
    public string Arguments => _args.Text.Trim();
    public bool StartBlocked => _startBlocked.Checked;

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

        foreach (var tb in new[] { _exe, _args })
        {
            tb.BackColor = p.Surface2;
            tb.ForeColor = p.Text;
            tb.Font = new Font("Segoe UI", 10.5f);
        }

        var browse = new RoundButton("Procurar...") { Margin = new Padding(Theme.S(8), 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe", Title = "Escolha o programa" };
            if (dlg.ShowDialog(this) == DialogResult.OK) { _exe.Text = dlg.FileName; SuggestArgs(); }
        };
        _exe.Leave += (_, _) => SuggestArgs();

        var ok = new RoundButton("Abrir cópia", ButtonKind.Primary) { DialogResult = DialogResult.OK, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        var cancel = new RoundButton("Cancelar") { DialogResult = DialogResult.Cancel, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) =>
        {
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
            Text = "Cria uma cópia do programa feita de hard links (não ocupa espaço). Só a cópia pode ter a " +
                   "internet cortada; o programa original continua normal."
        };

        Label FieldLabel(string text) => new()
        {
            Text = text, AutoSize = true, ForeColor = p.Text, Font = new Font("Segoe UI Semibold", 9.5f),
            Margin = new Padding(0, Theme.S(8), 0, Theme.S(4))
        };

        var help = new Label
        {
            AutoSize = true, MaximumSize = new Size(Theme.S(570), 0), ForeColor = p.Muted, Margin = new Padding(0, Theme.S(4), 0, 0),
            Text = "Programas que só permitem uma janela aberta (Chrome, Edge, Firefox...) precisam de um parâmetro " +
                   "para abrir separados. Para navegadores conhecidos ele é preenchido automaticamente."
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
        layout.Controls.Add(FieldLabel("Argumentos extras (opcional)"));
        layout.Controls.Add(_args);
        layout.Controls.Add(help);
        layout.Controls.Add(_startBlocked);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        _exe.Text = initialExe ?? "";
        SuggestArgs();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    void SuggestArgs()
    {
        if (_args.Text.Length == 0 && File.Exists(ExePath))
            _args.Text = CloneManager.SuggestArgs(ExePath);
    }
}
