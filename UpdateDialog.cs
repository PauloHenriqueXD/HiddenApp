namespace HiddenApp;

/// <summary>
/// Mostra a versão nova, as novidades e pergunta se o usuário quer atualizar.
/// Retorna DialogResult.OK quando a atualização foi instalada (o app deve encerrar em seguida).
/// </summary>
public class UpdateDialog : Form
{
    readonly UpdateInfo _update;
    readonly RoundButton _btnUpdate;
    readonly RoundButton _btnLater = new("Depois");
    readonly ThinProgress _progress = new() { Visible = false };
    readonly Label _status = new() { AutoSize = true, Visible = false };
    bool _installing;

    public UpdateDialog(UpdateInfo update)
    {
        _update = update;
        var p = Theme.Current;

        Text = "Atualização do HiddenApp";
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

        // ---------- Cabeçalho ----------
        var logo = new PictureBox
        {
            Image = Theme.Logo, SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(Theme.S(48), Theme.S(48)), Margin = new Padding(0, 0, Theme.S(12), 0)
        };
        var title = new Label
        {
            Text = update.IsPrerelease ? "Nova versão beta disponível" : "Nova versão disponível",
            AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), ForeColor = p.Text, Margin = new Padding(0)
        };
        var versions = new Label
        {
            AutoSize = true, ForeColor = p.Muted, Margin = new Padding(0, Theme.S(4), 0, 0),
            Text = $"Sua versão: {UpdateManager.CurrentVersion}     →     Nova versão: {update.Version}"
        };
        var titles = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        titles.Controls.Add(title);
        titles.Controls.Add(versions);
        var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, Theme.S(14)) };
        header.Controls.Add(logo);
        header.Controls.Add(titles);

        // ---------- Novidades ----------
        var notesLabel = new Label
        {
            Text = "Novidades", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f),
            ForeColor = p.Text, Margin = new Padding(0, 0, 0, Theme.S(4))
        };
        var notes = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
            Width = Theme.S(560), Height = Theme.S(220), BackColor = p.Surface2, ForeColor = p.Text,
            Font = new Font("Segoe UI", 9.5f), TabStop = false,
            Text = FormatNotes(update.Notes),
        };

        var link = new LinkLabel
        {
            Text = "Ver esta versão no GitHub", AutoSize = true, Margin = new Padding(0, Theme.S(6), 0, 0),
            LinkColor = p.Accent, ActiveLinkColor = p.Accent, VisitedLinkColor = p.Accent
        };
        link.LinkClicked += (_, _) => UpdateManager.OpenUrl(update.PageUrl);

        var warning = new Label
        {
            AutoSize = true, MaximumSize = new Size(Theme.S(560), 0), ForeColor = p.Muted,
            Margin = new Padding(0, Theme.S(10), 0, 0),
            Text = update.IsPrerelease
                ? "Esta é uma versão beta (pré-lançamento): pode ter falhas. Você recebe betas porque ativou essa opção."
                : "Os programas bloqueados e suas configurações são mantidos. O HiddenApp reinicia sozinho após atualizar."
        };

        _status.ForeColor = p.Muted;
        _status.Margin = new Padding(0, Theme.S(12), 0, Theme.S(4));
        _progress.Width = Theme.S(560);

        // ---------- Botões ----------
        bool canUpdate = UpdateManager.CanSelfUpdate;
        _btnUpdate = new RoundButton(canUpdate ? "Atualizar agora" : "Baixar no GitHub", ButtonKind.Primary)
        {
            Margin = new Padding(Theme.S(8), 0, 0, 0)
        };
        _btnLater.Margin = new Padding(Theme.S(8), 0, 0, 0);
        _btnLater.Click += (_, _) => { if (!_installing) { DialogResult = DialogResult.Cancel; Close(); } };
        _btnUpdate.Click += async (_, _) =>
        {
            if (!canUpdate) { UpdateManager.OpenUrl(update.PageUrl); DialogResult = DialogResult.Cancel; Close(); return; }
            await InstallAsync();
        };
        AcceptButton = _btnUpdate;
        CancelButton = _btnLater;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
            Margin = new Padding(0, Theme.S(16), 0, 0)
        };
        buttons.Controls.Add(_btnUpdate);
        buttons.Controls.Add(_btnLater);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.S(22), Theme.S(18), Theme.S(22), Theme.S(18)) };
        layout.Controls.Add(header);
        layout.Controls.Add(notesLabel);
        layout.Controls.Add(notes);
        layout.Controls.Add(link);
        layout.Controls.Add(warning);
        layout.Controls.Add(_status);
        layout.Controls.Add(_progress);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
    }

    async Task InstallAsync()
    {
        _installing = true;
        _btnUpdate.Enabled = _btnLater.Enabled = false;
        ControlBox = false;
        _status.Visible = _progress.Visible = true;
        _status.Text = "Baixando a versão nova...";

        var progress = new Progress<int>(pct =>
        {
            _progress.Value = pct;
            _status.Text = pct < 100 ? $"Baixando a versão nova... {pct}%" : "Conferindo o arquivo...";
        });

        try
        {
            await UpdateManager.InstallAsync(_update, progress);
            _status.Text = "Pronto! Reiniciando o HiddenApp...";
            await Task.Delay(600);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _installing = false;
            ControlBox = true;
            _btnUpdate.Enabled = _btnLater.Enabled = true;
            _progress.Visible = false;
            _status.Text = "A atualização não foi concluída.";
            MessageBox.Show(this, ex.Message, "HiddenApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_installing && DialogResult != DialogResult.OK) e.Cancel = true; // não fecha no meio da troca
        base.OnFormClosing(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    /// <summary>Deixa o texto das novidades (Markdown do GitHub) mais legível em texto simples.</summary>
    static string FormatNotes(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "Sem descrição para esta versão.";
        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(l =>
        {
            string t = l.TrimEnd();
            if (t.StartsWith("### ")) return "\n" + t[4..].ToUpperInvariant();
            if (t.StartsWith("## ")) return "\n" + t[3..].ToUpperInvariant();
            if (t.StartsWith("# ")) return t[2..].ToUpperInvariant();
            string indent = new(' ', t.Length - t.TrimStart().Length);
            string body = t.TrimStart();
            if (body.StartsWith("- ") || body.StartsWith("* ")) body = "•  " + body[2..];
            return indent + body.Replace("**", "").Replace("`", "");
        });
        return string.Join("\r\n", lines).Trim();
    }
}
