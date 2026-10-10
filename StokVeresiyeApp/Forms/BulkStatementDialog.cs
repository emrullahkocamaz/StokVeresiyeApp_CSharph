using System.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

/// <summary>Borçlu müşterilere cari ekstre PDF'ini toplu olarak WhatsApp veya e-posta ile gönderir.</summary>
public class BulkStatementDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly CheckBox _chkOnlyDebtors = new() { Text = "Yalnızca borcu olanlar", Checked = true, AutoSize = true };
    private readonly NumericUpDown _numMin = new() { Minimum = 0, Maximum = 10000000, DecimalPlaces = 2, Value = 1, Width = 100 };
    private readonly RadioButton _rbWhatsApp = new() { Text = "WhatsApp", Checked = true, AutoSize = true };
    private readonly RadioButton _rbMail = new() { Text = "E-posta (otomatik)", AutoSize = true };
    private readonly RadioButton _rbPdfOnly = new() { Text = "Yalnızca PDF üret", AutoSize = true };
    private readonly Label _lblInfo = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _btnSend;
    private readonly DataTable _table = new();
    private bool _busy;

    public BulkStatementDialog()
    {
        Text = "📨 Toplu Cari Ekstre Gönderimi";
        ClientSize = new Size(960, 620);
        MinimumSize = new Size(820, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        _table.Columns.Add("Seç", typeof(bool));
        _table.Columns.Add("Id", typeof(long));
        _table.Columns.Add("Cari Adı", typeof(string));
        _table.Columns.Add("Telefon", typeof(string));
        _table.Columns.Add("E-Posta", typeof(string));
        _table.Columns.Add("Bakiye (₺)", typeof(double));
        _table.Columns.Add("Durum", typeof(string));

        // Üst filtre paneli
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(16, 10, 16, 0), WrapContents = true, BackColor = UITheme.CardBg };
        top.Controls.Add(MakeLabel("Filtre:"));
        top.Controls.Add(_chkOnlyDebtors);
        top.Controls.Add(MakeLabel("   En az bakiye (₺):"));
        top.Controls.Add(_numMin);
        top.Controls.Add(UITheme.CreateButton("Listele", UITheme.Primary, Color.White, (s, e) => LoadList(), 90, 30));
        top.Controls.Add(UITheme.CreateButton("Tümünü Seç", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => SetAll(true), 110, 30));
        top.Controls.Add(UITheme.CreateButton("Seçimi Kaldır", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => SetAll(false), 120, 30));
        var breaker = new Panel { Width = 2000, Height = 0, Margin = new Padding(0) };
        top.Controls.Add(breaker);
        top.SetFlowBreak(breaker, true);
        top.Controls.Add(MakeLabel("Gönderim şekli:"));
        top.Controls.Add(_rbWhatsApp);
        top.Controls.Add(_rbMail);
        top.Controls.Add(_rbPdfOnly);

        // Alt panel
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 96, BackColor = UITheme.CardBg, Padding = new Padding(16, 10, 16, 10) };
        _lblInfo.Dock = DockStyle.Top;
        _lblInfo.Height = 24;
        _lblInfo.Font = UITheme.SmallFont;
        _lblInfo.ForeColor = UITheme.TextSecondary;
        _progress.Dock = DockStyle.Top;
        _progress.Height = 10;
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        btnRow.Controls.Add(UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 36));
        _btnSend = UITheme.CreateButton("🚀 Seçilenlere Gönder", UITheme.Success, Color.White, async (s, e) => await SendAsync(), 200, 36);
        btnRow.Controls.Add(_btnSend);
        bottom.Controls.Add(btnRow);
        bottom.Controls.Add(_progress);
        bottom.Controls.Add(_lblInfo);

        UITheme.ApplyGridStyle(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.DataSource = _table;
        _grid.AllowUserToAddRows = false;
        _grid.ReadOnly = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn c in _grid.Columns) c.ReadOnly = c.Name != "Seç";
            if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
            if (_grid.Columns["Seç"] != null) _grid.Columns["Seç"].Width = 50;
            if (_grid.Columns["Bakiye (₺)"] != null) _grid.Columns["Bakiye (₺)"].DefaultCellStyle.Format = "N2";
        };
        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            UpdateInfo();
        };

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        gridHost.Controls.Add(_grid);

        Controls.Add(gridHost);
        Controls.Add(bottom);
        Controls.Add(top);
        gridHost.BringToFront();

        LoadList();
    }

    private static Label MakeLabel(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(0, 6, 8, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

    private void LoadList()
    {
        _table.Rows.Clear();
        double min = (double)_numMin.Value;
        foreach (DataRow r in AccountService.GetAllAccounts(null, "Müşteri").Rows)
        {
            double bal = Convert.ToDouble(r["Bakiye (₺)"]);
            if (_chkOnlyDebtors.Checked && bal < Math.Max(min, 0.01)) continue;
            _table.Rows.Add(false, Convert.ToInt64(r["Id"]), r["Cari Adı"]?.ToString() ?? "", r["Telefon"]?.ToString() ?? "",
                r["E-Posta"]?.ToString() ?? "", bal, "");
        }
        UpdateInfo();
    }

    private void SetAll(bool v)
    {
        foreach (DataRow r in _table.Rows) r["Seç"] = v;
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        int sel = _table.Rows.Cast<DataRow>().Count(r => (bool)r["Seç"]);
        double total = _table.Rows.Cast<DataRow>().Where(r => (bool)r["Seç"]).Sum(r => (double)r["Bakiye (₺)"]);
        _lblInfo.Text = $"{_table.Rows.Count} cari listelendi · {sel} seçili · seçilenlerin toplam bakiyesi {total:N2} ₺";
    }

    private async Task SendAsync()
    {
        if (_busy) return;
        var selected = _table.Rows.Cast<DataRow>().Where(r => (bool)r["Seç"]).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Lütfen en az bir cari seçiniz.", "Seçim Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool wa = _rbWhatsApp.Checked, mail = _rbMail.Checked;
        if (MessageBox.Show($"{selected.Count} cariye ekstre {(wa ? "WhatsApp ile" : mail ? "e-posta ile" : "PDF olarak")} hazırlanacak. Devam edilsin mi?",
                "Toplu Gönderim", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _busy = true;
        _btnSend.Enabled = false;
        _progress.Maximum = selected.Count;
        _progress.Value = 0;
        int ok = 0, fail = 0;

        try
        {
            foreach (var row in selected)
            {
                long id = (long)row["Id"];
                var acc = AccountService.GetById(id);
                if (acc == null) { row["Durum"] = "❌ Cari yok"; fail++; _progress.Value++; continue; }

                try
                {
                    string pdf = await Task.Run(() => AccountStatementService.CreatePdf(id));
                    double bal = AccountStatementService.GetBalance(acc);

                    if (wa)
                    {
                        var r = AccountStatementService.PrepareForWhatsApp(acc, pdf, bal);
                        row["Durum"] = r.Success ? "✅ WhatsApp açıldı" : "❌ " + r.Message;
                        if (r.Success)
                        {
                            ok++;
                            var next = MessageBox.Show($"{acc.Name} için WhatsApp sohbeti açıldı ve PDF panoya kopyalandı.\n\nMesaj kutusuna Ctrl+V yapıp gönderin, sonra Tamam'a basın.\n(İptal: kalan carileri atla)",
                                $"Gönderim {_progress.Value + 1}/{selected.Count}", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                            if (next == DialogResult.Cancel) { _progress.Value++; break; }
                        }
                        else fail++;
                    }
                    else if (mail)
                    {
                        var r = await Task.Run(() => AccountStatementService.SendByEmail(acc, pdf, bal));
                        row["Durum"] = r.Success ? "✅ Gönderildi" : "❌ " + r.Message;
                        if (r.Success) ok++; else fail++;
                    }
                    else
                    {
                        row["Durum"] = "✅ " + Path.GetFileName(pdf);
                        ok++;
                    }
                }
                catch (Exception ex)
                {
                    row["Durum"] = "❌ " + ex.Message;
                    fail++;
                }
                _progress.Value++;
            }
        }
        finally
        {
            _busy = false;
            _btnSend.Enabled = true;
        }

        string folder = Path.Combine(PdfArchiveService.ArchiveRoot, "CariEkstreler");
        MessageBox.Show($"İşlem tamamlandı.\n\nBaşarılı: {ok}\nBaşarısız: {fail}" + (_rbPdfOnly.Checked ? $"\n\nPDF klasörü:\n{folder}" : ""),
            "Toplu Gönderim", MessageBoxButtons.OK, fail == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
