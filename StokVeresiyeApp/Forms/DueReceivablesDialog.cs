using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class DueReceivablesDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly ComboBox _cmbFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtSearch = new();
    private readonly Label _lblSummary = new();

    public DueReceivablesDialog(string initialFilter = "Geçmiş")
    {
        Text = "📅 Vade & Ödeme Sözü Takibi (Vadesi Geçen Alacaklar)";
        ClientSize = new Size(1180, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();

        _cmbFilter.SelectedItem = initialFilter;
        if (_cmbFilter.SelectedIndex < 0) _cmbFilter.SelectedIndex = 0;
        LoadData();
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 75,
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = "📅 Müşteri Vade & Ödeme Sözü Takip Paneli",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        var lblSub = new Label
        {
            Text = "Veresiye satışlarda müşterilerinize verilen ödeme vadelerini takip edebilir, vadesi geçen alacakları tek tıkla WhatsApp ile hatırlatabilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 22
        };

        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Filtre ve Araç Çubuğu
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtSearch.Width = 220;
        _txtSearch.PlaceholderText = "🔍 Müşteri Adı veya Açıklama...";
        _txtSearch.TextChanged += (s, e) => LoadData();

        _cmbFilter.Width = 190;
        _cmbFilter.Items.AddRange(new object[] { "Geçmiş", "Bugün", "Gelecek", "Tümü" });
        _cmbFilter.SelectedIndexChanged += (s, e) => LoadData();

        var btnRefresh = UITheme.CreateButton("🔄 Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => LoadData(), 85, 34);

        _lblSummary.AutoSize = true;
        _lblSummary.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _lblSummary.ForeColor = Color.FromArgb(220, 38, 38);
        _lblSummary.TextAlign = ContentAlignment.MiddleRight;
        _lblSummary.Dock = DockStyle.Right;

        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        filterFlow.Controls.Add(new Label { Text = "Vade Durumu:", AutoSize = true, Margin = new Padding(0, 7, 5, 0), Font = UITheme.TitleFont });
        filterFlow.Controls.Add(_cmbFilter);
        filterFlow.Controls.Add(_txtSearch);
        filterFlow.Controls.Add(btnRefresh);

        toolbar.Controls.Add(filterFlow);
        toolbar.Controls.Add(_lblSummary);
        Controls.Add(toolbar);

        // 3. Grid Paneli
        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        UITheme.ApplyGridStyle(_grid);
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (s, e) => SendWhatsAppReminder();
        gridContainer.Controls.Add(_grid);
        Controls.Add(gridContainer);

        // 4. Alt Buton Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(15, 14, 15, 14)
        };

        var btnWhatsApp = UITheme.CreateButton("📲 WhatsApp Vade Hatırlatması Gönder", Color.FromArgb(37, 211, 102), Color.White, (s, e) => SendWhatsAppReminder(), 270, 36);
        var btnPayment = UITheme.CreateButton("🟢 Tahsilat Al (F6)", UITheme.Success, Color.White, (s, e) => MakePayment(), 140, 36);
        var btnStatement = UITheme.CreateButton("📑 Hesap Ekstresi (F10)", UITheme.Info, Color.White, (s, e) => OpenStatement(), 170, 36);
        var btnExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Primary, Color.White, ExportExcelClick, 130, 36);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 36);

        btnWhatsApp.Dock = DockStyle.Left;
        btnPayment.Dock = DockStyle.Left;
        btnStatement.Dock = DockStyle.Left;
        btnExcel.Dock = DockStyle.Left;
        btnClose.Dock = DockStyle.Right;

        bottomPanel.Controls.Add(btnWhatsApp);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        bottomPanel.Controls.Add(btnPayment);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        bottomPanel.Controls.Add(btnStatement);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        bottomPanel.Controls.Add(btnExcel);
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);

        // WinForms Z-Order
        gridContainer.BringToFront();
        header.SendToBack();
        toolbar.SendToBack();
        bottomPanel.SendToBack();
    }

    private void LoadData()
    {
        try
        {
            string filter = _cmbFilter.SelectedItem?.ToString() ?? "Tümü";
            var dt = TransactionService.GetDueReceivables(filter);

            string search = _txtSearch.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search))
            {
                var filteredRows = dt.AsEnumerable()
                    .Where(r => (r.Field<string>("Müşteri")?.ToLowerInvariant().Contains(search) ?? false) ||
                                (r.Field<string>("Açıklama")?.ToLowerInvariant().Contains(search) ?? false) ||
                                (r.Field<string>("Telefon")?.ToLowerInvariant().Contains(search) ?? false));

                if (filteredRows.Any())
                    dt = filteredRows.CopyToDataTable();
                else
                    dt.Clear();
            }

            _grid.DataSource = dt;

            if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
            if (_grid.Columns["AccountId"] != null) _grid.Columns["AccountId"].Visible = false;

            if (_grid.Columns["Veresiye Tutarı (₺)"] != null) _grid.Columns["Veresiye Tutarı (₺)"].DefaultCellStyle.Format = "N2";
            if (_grid.Columns["Güncel Toplam Bakiye (₺)"] != null) _grid.Columns["Güncel Toplam Bakiye (₺)"].DefaultCellStyle.Format = "N2";

            if (_grid.Columns["Gecikme (Gün)"] != null) _grid.Columns["Gecikme (Gün)"].Width = 110;
            if (_grid.Columns["Vade Tarihi"] != null) _grid.Columns["Vade Tarihi"].Width = 110;

            // Özet etiket
            double totalDue = 0;
            foreach (DataRow r in dt.Rows)
            {
                totalDue += Convert.ToDouble(r["Veresiye Tutarı (₺)"]);
            }
            _lblSummary.Text = $"Listelenen Kayıt: {dt.Rows.Count} Adet | Toplam Tutar: {totalDue:N2} ₺";
        }
        catch { }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        var row = _grid.Rows[e.RowIndex];

        if (_grid.Columns.Contains("Gecikme (Gün)"))
        {
            int days = Convert.ToInt32(row.Cells["Gecikme (Gün)"].Value ?? 0);
            if (days > 0)
            {
                // Vadesi geçmiş (Kırmızı)
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                if (e.ColumnIndex == _grid.Columns["Gecikme (Gün)"].Index)
                {
                    e.Value = $"⚠️ {days} Gün Gecikti";
                    e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
                    e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);
                    e.FormattingApplied = true;
                }
            }
            else if (days == 0)
            {
                // Bugün vadeli (Sarı/Turuncu)
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
                if (e.ColumnIndex == _grid.Columns["Gecikme (Gün)"].Index)
                {
                    e.Value = "🔔 BUGÜN VADELİ";
                    e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
                    e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9);
                    e.FormattingApplied = true;
                }
            }
            else
            {
                // Gelecek vadeli (Yeşil)
                if (e.ColumnIndex == _grid.Columns["Gecikme (Gün)"].Index)
                {
                    e.Value = $"{Math.Abs(days)} Gün Kaldı";
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                    e.FormattingApplied = true;
                }
            }
        }
    }

    private void SendWhatsAppReminder()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.DigitalReceipt) && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("WhatsApp ile borç ve vade hatırlatma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_grid.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen WhatsApp hatırlatması göndermek istediğiniz müşteriyi tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var row = _grid.SelectedRows[0];
        string customerName = row.Cells["Müşteri"].Value?.ToString() ?? "";
        string phone = row.Cells["Telefon"].Value?.ToString() ?? "";
        string dueDate = row.Cells["Vade Tarihi"].Value?.ToString() ?? "";
        double amount = Convert.ToDouble(row.Cells["Veresiye Tutarı (₺)"].Value ?? 0);
        double balance = Convert.ToDouble(row.Cells["Güncel Toplam Bakiye (₺)"].Value ?? 0);
        int days = Convert.ToInt32(row.Cells["Gecikme (Gün)"].Value ?? 0);

        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digitsOnly))
        {
            MessageBox.Show($"'{customerName}' müşterisine ait kayıtlı cep telefonu bulunamadı.", "Telefon Eksik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (digitsOnly.Length == 10 && digitsOnly.StartsWith("5")) digitsOnly = "90" + digitsOnly;
        else if (digitsOnly.Length == 11 && digitsOnly.StartsWith("05")) digitsOnly = "90" + digitsOnly.Substring(1);

        string durumMesaji = days > 0 
            ? $"⚠️ *{dueDate}* vadeli ({days} gün önce) olan *{amount:N2} ₺* tutarındaki veresiye ödemenizin vadesi geçmiştir."
            : (days == 0 
                ? $"🔔 *{amount:N2} ₺* tutarındaki veresiye ödemenizin vadesi *BUGÜN* dolmaktadır." 
                : $"ℹ️ *{dueDate}* vadeli *{amount:N2} ₺* tutarındaki veresiye ödemenizi hatırlatırız.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Sayın *{customerName}*,");
        sb.AppendLine();
        sb.AppendLine(durumMesaji);
        sb.AppendLine($"💰 *Güncel Toplam Bakiyeniz:* {balance:N2} ₺");
        sb.AppendLine();
        sb.AppendLine("Ödemenizi en kısa sürede gerçekleştirmenizi rica eder, bereketli ve hayırlı işler dileriz.");

        string url = $"https://wa.me/{digitsOnly}?text={Uri.EscapeDataString(sb.ToString())}";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WhatsApp açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MakePayment()
    {
        if (_grid.SelectedRows.Count == 0) return;
        long accId = Convert.ToInt64(_grid.SelectedRows[0].Cells["AccountId"].Value);
        using var f = new AccountMovementForm(accId, "Tahsilat");
        if (f.ShowDialog() == DialogResult.OK)
        {
            LoadData();
        }
    }

    private void OpenStatement()
    {
        if (_grid.SelectedRows.Count == 0) return;
        long accId = Convert.ToInt64(_grid.SelectedRows[0].Cells["AccountId"].Value);
        using var f = new AccountStatementDialog(accId);
        f.ShowDialog();
        LoadData();
    }

    private void ExportExcelClick(object? sender, EventArgs e)
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak kayıt bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Vade_Takip_Raporu_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(dt, "Vade Takibi", sfd.FileName);
                MessageBox.Show("Vade takip raporu Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
