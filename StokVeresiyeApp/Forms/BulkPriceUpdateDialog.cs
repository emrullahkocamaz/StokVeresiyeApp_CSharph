using System.Data;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class BulkPriceUpdateDialog : BaseModernForm
{
    private readonly KryptonComboBox _cmbCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbTargetPrice = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbActionType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtValue = new() { Text = "10" };
    private readonly KryptonComboBox _cmbRounding = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _gridPreview = new();
    private readonly Label _lblSummary = new();
    private DataTable? _previewDt;

    public BulkPriceUpdateDialog() : base("🏷️ Toplu Fiyat Güncelleme Sihirbazı (Zam & İndirim)", 980, 720)
    {
        // 1. Kategoriler
        _cmbCategory.Items.Add("(Tüm Kategoriler)");
        var dtCats = Database.Query("SELECT DISTINCT Category FROM Products WHERE IsActive=1 AND Category IS NOT NULL AND Category != '' ORDER BY Category");
        foreach (DataRow r in dtCats.Rows)
        {
            _cmbCategory.Items.Add(r["Category"].ToString() ?? "");
        }
        _cmbCategory.SelectedIndex = 0;

        // 2. Hangi Fiyat?
        _cmbTargetPrice.Items.AddRange(new object[] { "Satış Fiyatı (SalePrice)", "Alış Fiyatı (PurchasePrice)", "Toptan Fiyatı (WholesalePrice)", "Özel Fiyat (SpecialPrice)" });
        _cmbTargetPrice.SelectedIndex = 0;

        // 3. İşlem Tipi
        _cmbActionType.Items.AddRange(new object[] { "% Zam Uygula (Fiyatı Artır)", "% İndirim Uygula (Fiyatı Düşür)", "Sabit Tutar Artır (+ ₺)", "Sabit Tutar İndir (- ₺)" });
        _cmbActionType.SelectedIndex = 0;

        // 4. Yuvarlama Seçenekleri
        _cmbRounding.Items.AddRange(new object[] { 
            "Yuvarlama Yapma (Kuruşlu Kalsın)", 
            "En Yakın 0.50 ₺ (50 Kuruşa Yuvarla)", 
            "En Yakın 1.00 ₺ (Tam Sayıya Yuvarla)", 
            "Psikolojik Fiyat (.90 Kuruş Yap - Örn: 19.90 ₺)", 
            "Psikolojik Fiyat (.99 Kuruş Yap - Örn: 19.99 ₺)" 
        });
        _cmbRounding.SelectedIndex = 0;

        // Form Satırları
        AddRow("Kategori Filtresi", _cmbCategory, 38);
        AddRow("Değişecek Fiyat Türü", _cmbTargetPrice, 38);
        AddRow("Uygulama Şekli", _cmbActionType, 38);
        AddRow("Oran / Tutar (% veya ₺)", _txtValue, 38);
        AddRow("Kuruş Yuvarlama", _cmbRounding, 38);

        // Canlı Önizleme Butonu & Özet Paneli
        var pnlPreviewBar = new Panel { Height = 45, Dock = DockStyle.Fill };
        var btnPreview = UITheme.CreateKryptonButton("🔍 Değişiklikleri Önizle", Color.FromArgb(79, 70, 229), Color.White, (s, e) => CalculatePreview(), 180, 36);
        btnPreview.Dock = DockStyle.Left;

        _lblSummary.Text = "Henüz önizleme hesaplanmadı. Lütfen oran girip 'Değişiklikleri Önizle' butonuna tıklayınız.";
        _lblSummary.ForeColor = UITheme.TextSecondary;
        _lblSummary.Font = new Font("Segoe UI", 9.5f, FontStyle.Italic);
        _lblSummary.Dock = DockStyle.Fill;
        _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
        _lblSummary.Padding = new Padding(12, 0, 0, 0);

        pnlPreviewBar.Controls.Add(_lblSummary);
        pnlPreviewBar.Controls.Add(btnPreview);

        AddRow("Canlı Kontrol", pnlPreviewBar, 48);

        // Önizleme Tablosu (DataGridView)
        SetupGrid();
        var pnlGridContainer = new Panel { Height = 280, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        pnlGridContainer.Controls.Add(_gridPreview);
        AddRow("Önizleme Listesi", pnlGridContainer, 290);

        BtnSave.Text = "⚡ Toplu Fiyatları Uygula ve Kaydet";
        BtnSave.StateCommon.Back.Color1 = Color.FromArgb(16, 185, 129);
        BtnSave.StateCommon.Back.Color2 = Color.FromArgb(16, 185, 129);
        BtnSave.Click += ApplyBulkUpdateClick;

        // Olaylar
        _cmbCategory.SelectedIndexChanged += (s, e) => CalculatePreview();
        _cmbTargetPrice.SelectedIndexChanged += (s, e) => CalculatePreview();
        _cmbActionType.SelectedIndexChanged += (s, e) => CalculatePreview();
        _cmbRounding.SelectedIndexChanged += (s, e) => CalculatePreview();

        CalculatePreview();
    }

    private void SetupGrid()
    {
        _gridPreview.Dock = DockStyle.Fill;
        _gridPreview.ReadOnly = true;
        _gridPreview.AllowUserToAddRows = false;
        _gridPreview.AllowUserToDeleteRows = false;
        _gridPreview.BackgroundColor = Color.White;
        _gridPreview.BorderStyle = BorderStyle.Fixed3D;
        _gridPreview.RowHeadersVisible = false;
        _gridPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridPreview.Font = new Font("Segoe UI", 9.5f);
        _gridPreview.ColumnHeadersHeight = 36;
        _gridPreview.RowTemplate.Height = 30;
    }

    private string GetTargetColumnName()
    {
        return _cmbTargetPrice.SelectedIndex switch
        {
            1 => "PurchasePrice",
            2 => "WholesalePrice",
            3 => "SpecialPrice",
            _ => "SalePrice"
        };
    }

    private void CalculatePreview()
    {
        double val = 0;
        string valStr = _txtValue.Text.Trim().Replace('.', ',');
        if (!double.TryParse(valStr, out val) || val <= 0)
        {
            _lblSummary.Text = "⚠️ Geçerli bir oran veya tutar giriniz (örn: 15).";
            _lblSummary.ForeColor = Color.Firebrick;
            return;
        }

        string col = GetTargetColumnName();
        string catFilter = _cmbCategory.SelectedIndex > 0 ? "AND Category = @cat" : "";

        string sql = $@"
SELECT 
    Id, 
    Code AS [Ürün Kodu], 
    Name AS [Ürün Adı], 
    Category AS [Kategori], 
    {col} AS [Mevcut Fiyat]
FROM Products 
WHERE IsActive = 1 {catFilter}
ORDER BY Name";

        DataTable dt;
        if (_cmbCategory.SelectedIndex > 0)
            dt = Database.Query(sql, ("@cat", _cmbCategory.Text));
        else
            dt = Database.Query(sql);

        dt.Columns.Add("Yeni Fiyat", typeof(double));
        dt.Columns.Add("Fark (₺)", typeof(double));
        dt.Columns.Add("Değişim %", typeof(string));

        int actionIdx = _cmbActionType.SelectedIndex;
        int roundIdx = _cmbRounding.SelectedIndex;

        int changedCount = 0;
        double totalOld = 0;
        double totalNew = 0;

        foreach (DataRow r in dt.Rows)
        {
            double current = Convert.ToDouble(r["Mevcut Fiyat"]);
            double updated = current;

            switch (actionIdx)
            {
                case 0: // % Zam
                    updated = current * (1 + val / 100.0);
                    break;
                case 1: // % İndirim
                    updated = current * (1 - val / 100.0);
                    break;
                case 2: // Sabit Tutar Artır
                    updated = current + val;
                    break;
                case 3: // Sabit Tutar İndir
                    updated = Math.Max(0, current - val);
                    break;
            }

            // Yuvarlama
            updated = ApplyRounding(updated, roundIdx);

            double diff = updated - current;
            double pctDiff = current > 0 ? (diff / current) * 100.0 : 0;

            r["Yeni Fiyat"] = Math.Round(updated, 2);
            r["Fark (₺)"] = Math.Round(diff, 2);
            r["Değişim %"] = $"{(diff >= 0 ? "+" : "")}{pctDiff:N1} %";

            totalOld += current;
            totalNew += updated;
            changedCount++;
        }

        _previewDt = dt;
        _gridPreview.DataSource = dt;

        if (_gridPreview.Columns["Id"] != null) _gridPreview.Columns["Id"].Visible = false;
        if (_gridPreview.Columns["Mevcut Fiyat"] != null) _gridPreview.Columns["Mevcut Fiyat"].DefaultCellStyle.Format = "N2 ₺";
        if (_gridPreview.Columns["Yeni Fiyat"] != null) _gridPreview.Columns["Yeni Fiyat"].DefaultCellStyle.Format = "N2 ₺";
        if (_gridPreview.Columns["Fark (₺)"] != null) _gridPreview.Columns["Fark (₺)"].DefaultCellStyle.Format = "N2 ₺";

        _lblSummary.Text = $"✅ {changedCount} adet ürün etkilenecek. Ortalama Toplam: {totalOld:N2} ₺ ➔ {totalNew:N2} ₺ (Fark: {(totalNew - totalOld):N2} ₺)";
        _lblSummary.ForeColor = Color.FromArgb(21, 128, 61);
    }

    private static double ApplyRounding(double price, int roundIdx)
    {
        return roundIdx switch
        {
            1 => Math.Round(price * 2, MidpointRounding.AwayFromZero) / 2.0, // En yakın 0.50 ₺
            2 => Math.Round(price, MidpointRounding.AwayFromZero), // En yakın 1.00 ₺
            3 => Math.Floor(price) + 0.90, // .90 Kuruş
            4 => Math.Floor(price) + 0.99, // .99 Kuruş
            _ => Math.Round(price, 2)
        };
    }

    private void ApplyBulkUpdateClick(object? sender, EventArgs e)
    {
        if (_previewDt == null || _previewDt.Rows.Count == 0)
        {
            MessageBox.Show("Güncellenecek ürün bulunamadı. Lütfen önce önizleme hesaplayınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        string col = GetTargetColumnName();
        string colDesc = _cmbTargetPrice.Text;
        int count = _previewDt.Rows.Count;

        var ask = MessageBox.Show(
            $"DİKKAT: Toplam {count} adet ürünün '{colDesc}' alanı güncellenecektir!\n\nBu işlemi onaylıyor musunuz?",
            "Toplu Fiyat Güncelleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (ask != DialogResult.Yes)
        {
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            int updatedCount = 0;
            foreach (DataRow r in _previewDt.Rows)
            {
                long id = Convert.ToInt64(r["Id"]);
                double newPrice = Convert.ToDouble(r["Yeni Fiyat"]);
                double oldPrice = Convert.ToDouble(r["Mevcut Fiyat"]);

                Database.Execute($"UPDATE Products SET {col} = @p WHERE Id = @id", ("@p", newPrice), ("@id", id));
                updatedCount++;
            }

            // Denetim kaydı ekle
            try
            {
                AuditLogService.Log(
                    "Products",
                    "BulkPriceUpdate",
                    null,
                    $"{count} Adet Ürün Toplu Fiyat",
                    $"Alan: {colDesc}",
                    $"İşlem: {_cmbActionType.Text} {_txtValue.Text} - {_cmbRounding.Text}",
                    $"Kullanıcı {count} adet ürünün fiyatını toplu güncelledi."
                );
            }
            catch { }

            MessageBox.Show($"Başarılı! Toplam {updatedCount} adet ürünün fiyatı güncellendi.", "Fiyatlar Güncellendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Güncelleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
