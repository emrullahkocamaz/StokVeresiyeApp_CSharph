using System.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class AuditLogsDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtSearch = new();
    private readonly ComboBox _cmbEntity = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbAction = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _dtpStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private readonly DateTimePicker _dtpEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

    public AuditLogsDialog()
    {
        Text = "Kayıt Güvenlik & Denetim Logları (Audit Trail)";
        ClientSize = new Size(1180, 700);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        LoadLogs();
    }

    private void BuildUI()
    {
        // Başlık
        var header = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(20, 12, 20, 12)
        };
        var lblTitle = new Label
        {
            Text = "📜 Sistem Değişiklik ve Silme Logları (Denetim Kayıtları)",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };
        var lblSub = new Label
        {
            Text = "Silinen veya değiştirilen tüm cari hesaplar, hareketler ve işlemler burada tarih ve saat bazlı güvenle arşivlenir.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 20
        };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // Filtre Çubuğu
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtSearch.Width = 200;
        _txtSearch.PlaceholderText = "🔍 Loglarda Ara...";
        _txtSearch.TextChanged += (s, e) => LoadLogs();

        _cmbEntity.Width = 130;
        _cmbEntity.Items.AddRange(new object[] { "Tümü", "Kullanıcı", "Cari", "CariHareket", "Urun", "StokHareket", "Sistem" });
        _cmbEntity.SelectedIndex = 0;
        _cmbEntity.SelectedIndexChanged += (s, e) => LoadLogs();

        _cmbAction.Width = 140;
        _cmbAction.Items.AddRange(new object[] { "Tümü", "Giriş", "Güncelleme", "Silindi", "Silindi / Pasife Alındı", "Güncellendi", "Yeni Eklendi" });
        _cmbAction.SelectedIndex = 0;
        _cmbAction.SelectedIndexChanged += (s, e) => LoadLogs();

        _dtpStart.Width = 105;
        _dtpStart.ValueChanged += (s, e) => LoadLogs();

        _dtpEnd.Width = 105;
        _dtpEnd.ValueChanged += (s, e) => LoadLogs();

        var btnRefresh = UITheme.CreateButton("🔄 Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => LoadLogs(), 90, 34);
        var btnExportExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportExcelClick, 130, 34);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 80, 34);

        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        filterFlow.Controls.Add(_txtSearch);
        filterFlow.Controls.Add(_cmbEntity);
        filterFlow.Controls.Add(_cmbAction);
        filterFlow.Controls.Add(_dtpStart);
        filterFlow.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 7, 0, 0) });
        filterFlow.Controls.Add(_dtpEnd);
        filterFlow.Controls.Add(btnRefresh);
        filterFlow.Controls.Add(btnExportExcel);
        filterFlow.Controls.Add(btnClose);

        toolbar.Controls.Add(filterFlow);

        // Grid
        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        UITheme.ApplyGridStyle(_grid);
        _grid.CellFormatting += Grid_CellFormatting;
        gridContainer.Controls.Add(_grid);

        // WinForms Z-Order
        Controls.Add(gridContainer);
        Controls.Add(toolbar);
        Controls.Add(header);
        header.SendToBack();
        toolbar.SendToBack();
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        var row = _grid.Rows[e.RowIndex];
        string action = row.Cells["İşlem"]?.Value?.ToString() ?? "";

        if (action.Contains("Silin"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(254, 226, 226);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(185, 28, 28);
            row.Cells["İşlem"].Style.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        }
        else if (action.Contains("Güncellen"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(254, 243, 199);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(180, 83, 9);
            row.Cells["İşlem"].Style.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        }
        else if (action.Contains("Yeni"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(209, 250, 229);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(4, 120, 87);
        }
    }

    private void LoadLogs()
    {
        try
        {
            var dt = AuditLogService.GetLogs(
                _cmbEntity.SelectedItem?.ToString(),
                _cmbAction.SelectedItem?.ToString(),
                _dtpStart.Value.Date,
                _dtpEnd.Value.Date,
                _txtSearch.Text
            );
            _grid.DataSource = dt;

            if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Width = 60;
            if (_grid.Columns["İşlem Tarihi"] != null) _grid.Columns["İşlem Tarihi"].Width = 140;
            if (_grid.Columns["Kayıt Türü"] != null) _grid.Columns["Kayıt Türü"].Width = 100;
            if (_grid.Columns["İşlem"] != null) _grid.Columns["İşlem"].Width = 130;
            if (_grid.Columns["İlgili Kayıt / Başlık"] != null) _grid.Columns["İlgili Kayıt / Başlık"].Width = 200;
        }
        catch { }
    }

    private void ExportExcelClick(object? sender, EventArgs e)
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak log kaydı bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Sistem_Denetim_Loglari_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(dt, "Denetim Logları", sfd.FileName);
                MessageBox.Show("Log kayıtları başarıyla Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
