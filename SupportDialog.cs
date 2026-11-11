namespace HiddenApp;

/// <summary>Janela "Apoiar o projeto": QR code do Pix, código copia e cola e aviso de conferência.</summary>
public class SupportDialog : Form
{
    public SupportDialog()
    {
        var p = Theme.Current;
        Text = "Apoiar o HiddenApp";
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
        int width = Theme.S(420);

        var title = new Label
        {
            Text = "Apoiar o projeto ❤", AutoSize = true, ForeColor = p.Text,
            Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, 0, 0, Theme.S(4))
        };
        var intro = new Label
        {
            AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = p.Muted, Margin = new Padding(0, 0, 0, Theme.S(14)),
            Text = "O HiddenApp é gratuito. Se ele te ajuda, você pode apoiar o desenvolvimento com uma doação " +
                   "por Pix, de qualquer valor. Obrigado!"
        };

        // QR code sempre preto no fundo branco (em qualquer tema), para todo app de banco conseguir ler
        var qr = new PictureBox
        {
            Image = Donation.CreateQrImage(Math.Max(4, Theme.S(6))),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(Theme.S(240), Theme.S(240)),
            BackColor = Color.White,
            Padding = new Padding(Theme.S(8)),
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, Theme.S(6))
        };
        var qrHint = new Label
        {
            Text = "Abra o app do seu banco → Pix → Pagar com QR code", AutoSize = true, ForeColor = p.Muted,
            Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, Theme.S(14))
        };

        var codeLabel = new Label
        {
            Text = "Ou use o Pix Copia e Cola", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f),
            ForeColor = p.Text, Margin = new Padding(0, 0, 0, Theme.S(4))
        };
        var code = new TextBox
        {
            Text = Donation.Payload, ReadOnly = true, Multiline = true, WordWrap = true,
            Width = width, Height = Theme.S(54), BorderStyle = BorderStyle.FixedSingle,
            BackColor = p.Surface2, ForeColor = p.Text, Font = new Font("Consolas", 8.5f), TabStop = false
        };
        var copy = new RoundButton("Copiar código Pix", ButtonKind.Primary) { Margin = new Padding(0, Theme.S(8), 0, 0) };
        copy.Click += async (_, _) =>
        {
            try
            {
                Clipboard.SetText(Donation.Payload);
                copy.Text = "Copiado! ✓";
                await Task.Delay(2000);
                if (!copy.IsDisposed) copy.Text = "Copiar código Pix";
            }
            catch { MessageBox.Show(this, "Não foi possível copiar. Selecione o código e copie manualmente.", Text); }
        };

        // Aviso de segurança: conferir o recebedor e baixar só do site oficial
        var check = new Label
        {
            AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = p.Text, Margin = new Padding(0, Theme.S(16), 0, 0),
            Text = $"🔒 Antes de confirmar, confira no app do banco se o recebedor é:\n{Donation.ReceiverFullName}"
        };
        var official = new Label
        {
            AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = p.Muted, Margin = new Padding(0, Theme.S(6), 0, 0),
            Text = $"Se o nome for outro, não pague: você pode estar usando uma cópia falsa do programa. " +
                   $"O HiddenApp oficial só é distribuído em {Donation.OfficialSite}."
        };
        // Fora do Brasil: GitHub Sponsors (cartão de crédito, valor livre, mensal ou uma vez)
        var abroadTitle = new Label
        {
            Text = "Fora do Brasil?", AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = p.Text,
            Margin = new Padding(0, Theme.S(18), 0, Theme.S(2))
        };
        var abroadText = new Label
        {
            AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = p.Muted, Margin = new Padding(0, 0, 0, Theme.S(6)),
            Text = "O Pix só funciona com contas brasileiras. De outros países, apoie pelo GitHub Sponsors " +
                   "(cartão de crédito, valor livre, uma vez ou todo mês)."
        };
        var sponsors = new RoundButton("❤ Apoiar pelo GitHub Sponsors") { Margin = new Padding(0) };
        sponsors.Click += (_, _) => UpdateManager.OpenUrl(Donation.SponsorsUrl);

        var link = new LinkLabel
        {
            Text = "Abrir a página oficial", AutoSize = true, Margin = new Padding(0, Theme.S(4), 0, 0),
            LinkColor = p.Accent, ActiveLinkColor = p.Accent, VisitedLinkColor = p.Accent
        };
        link.LinkClicked += (_, _) => UpdateManager.OpenUrl("https://" + Donation.OfficialSite);

        var close = new RoundButton("Fechar") { DialogResult = DialogResult.OK, Margin = new Padding(0) };
        CancelButton = close;
        AcceptButton = close;
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill,
            Margin = new Padding(0, Theme.S(16), 0, 0)
        };
        buttons.Controls.Add(close);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.S(22), Theme.S(18), Theme.S(22), Theme.S(18)) };
        foreach (Control c in new Control[] { title, intro, qr, qrHint, codeLabel, code, copy, check, official, link, abroadTitle, abroadText, sponsors, buttons })
            layout.Controls.Add(c);
        Controls.Add(layout);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }
}
