namespace HiddenApp;

/// <summary>Caixa que "grava" uma tecla de atalho: clique nela e aperte a tecla (com Ctrl/Alt/Shift, se quiser).</summary>
public class HotkeyBox : TextBox
{
    Keys _keys;

    public HotkeyBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.FixedSingle;
        Width = Theme.S(220);
        Cursor = Cursors.Hand;
        ShortcutsEnabled = false;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Keys Keys
    {
        get => _keys;
        set { _keys = value; Text = Describe(value); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys code = keyData & Keys.KeyCode;
        if (code is Keys.Tab or Keys.Escape) return base.ProcessCmdKey(ref msg, keyData); // navegação / fechar
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return true; // só modificador
        Keys = keyData;
        return true;
    }

    /// <summary>Texto amigável, ex.: "Ctrl + Shift + F7".</summary>
    public static string Describe(Keys keys)
    {
        var parts = new List<string>();
        if (keys.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (keys.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (keys.HasFlag(Keys.Shift)) parts.Add("Shift");
        Keys code = keys & Keys.KeyCode;
        parts.Add(code switch
        {
            >= Keys.D0 and <= Keys.D9 => ((int)(code - Keys.D0)).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (int)(code - Keys.NumPad0),
            Keys.Oemcomma => ",", Keys.OemPeriod => ".", Keys.OemMinus => "-", Keys.Oemplus => "=",
            Keys.Space => L.T("Espaço"), Keys.Return => "Enter", Keys.Back => "Backspace", Keys.Delete => "Delete",
            Keys.Insert => "Insert", Keys.Home => "Home", Keys.End => "End", Keys.PageUp => "Page Up", Keys.PageDown => "Page Down",
            Keys.Pause => "Pause", Keys.Scroll => "Scroll Lock",
            _ => code.ToString(),
        });
        return string.Join(" + ", parts);
    }
}

/// <summary>Janela para trocar as teclas de iniciar, parar e capturar ponto das macros.</summary>
public class HotkeyDialog : Form
{
    readonly HotkeyBox _start = new(), _stop = new(), _capture = new();

    public Keys StartKey => _start.Keys;
    public Keys StopKey => _stop.Keys;
    public Keys CaptureKey => _capture.Keys;

    public HotkeyDialog(Keys start, Keys stop, Keys capture)
    {
        var p = Theme.Current;
        Text = "Teclas de atalho";
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

        _start.Keys = start; _stop.Keys = stop; _capture.Keys = capture;
        foreach (var box in new[] { _start, _stop, _capture })
        {
            box.BackColor = p.Surface2;
            box.ForeColor = p.Text;
            box.Font = new Font("Segoe UI Semibold", 10.5f);
        }

        var title = new Label { Text = "Teclas de atalho das macros", AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), ForeColor = p.Text };
        var intro = new Label
        {
            AutoSize = true, MaximumSize = new Size(Theme.S(440), 0), ForeColor = p.Muted, Margin = new Padding(0, Theme.S(4), 0, Theme.S(14)),
            Text = "Clique numa caixa e aperte a tecla desejada (pode combinar com Ctrl, Alt ou Shift). " +
                   "As teclas funcionam em qualquer programa, mesmo com o HiddenApp escondido perto do relógio."
        };

        var form = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
        void Row(string label, HotkeyBox box)
        {
            form.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = p.Text, Anchor = AnchorStyles.Left, Margin = new Padding(0, Theme.S(8), Theme.S(14), Theme.S(8)) });
            box.Margin = new Padding(0, Theme.S(4), 0, Theme.S(4));
            form.Controls.Add(box);
        }
        Row("Iniciar macro", _start);
        Row("Parar macro", _stop);
        Row("Capturar ponto (posição do mouse)", _capture);

        var reset = new RoundButton("Restaurar padrão") { Margin = new Padding(0) };
        reset.Click += (_, _) => { _start.Keys = Keys.F7; _stop.Keys = Keys.F8; _capture.Keys = Keys.F9; };
        var ok = new RoundButton("Salvar", ButtonKind.Primary) { DialogResult = DialogResult.OK, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        var cancel = new RoundButton("Cancelar") { DialogResult = DialogResult.Cancel, Margin = new Padding(Theme.S(8), 0, 0, 0) };
        CancelButton = cancel;
        ok.Click += (_, _) =>
        {
            var keys = new[] { _start.Keys, _stop.Keys, _capture.Keys };
            if (keys.Distinct().Count() < 3)
            {
                Msg.Show(this, "Cada ação precisa de uma tecla diferente.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.S(18), 0, 0) };
        buttons.Controls.AddRange(new Control[] { ok, cancel });
        var bottom = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.S(18), 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.Controls.Add(reset);
        buttons.Margin = new Padding(0);
        bottom.Controls.Add(buttons);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(Theme.S(22), Theme.S(18), Theme.S(22), Theme.S(18)) };
        layout.Controls.Add(title);
        layout.Controls.Add(intro);
        layout.Controls.Add(form);
        layout.Controls.Add(bottom);
        Controls.Add(layout);
        L.Apply(this);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }
}
