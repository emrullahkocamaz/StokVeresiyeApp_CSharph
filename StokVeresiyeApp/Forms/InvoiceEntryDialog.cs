using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class InvoiceEntryDialog : Form
{
    private readonly TextBox _txtInvoiceNo = new();
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly ComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _txtNote = new() { Width = 250 };

    private readonly DataGridView _gridItems = new();
    private readonly Label _lblSubTotal = new() { Text = "0,00 ₺", Font = UITheme.TitleFont, AutoSize = true };
    private readonly Label _lblVatTotal = new() { Text = "0,00 ₺", Font = UITheme.TitleFont, AutoSize = true };
    private readonly Label _lblGrandTotal = new() { Text = "0,00 ₺", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };

    private readonly CheckBox _chkUpdateStock = new() { Text = "Ürünleri seçilen depoya otomatik stok girişi yap (Alış Hareketi)", Checked = true, AutoSize = true };
    private readonly CheckBox _chkUpdateAccount = new() { Text = "Tedarikçi cari hesabına borç/alacak kaydı düş", Checked = true, AutoSize = true };

    private string? _selectedFilePath;
    private ParsedInvoiceResult? _parsedResult;
    private readonly DataTable _itemsTable = new();

    public InvoiceEntryDialog(string? initialFilePath = null)
    {
        _selectedFilePath = initialFilePath;
        Text = "📄 E-Fatura / Alış Faturası Girişi & Stok Giriş Sihirbazı";
        ClientSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        InitializeTable();
        BuildUI();
        LoadCombos();

        if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            ProcessSelectedFile(_selectedFilePath);
        }
    }

    private void InitializeTable()
    {
        _itemsTable.Columns.Add("ProductId", typeof(long));
        _itemsTable.Columns.Add("LineNo", typeof(int));
        _itemsTable.Columns.Add("Status", typeof(string));
        _itemsTable.Columns.Add("Barcode", typeof(string));
        _itemsTable.Columns.Add("ItemCode", typeof(string));
        _itemsTable.Columns.Add("ItemName", typeof(string));
        _itemsTable.Columns.Add("Quantity", typeof(double));
        _itemsTable.Columns.Add("Unit", typeof(string));
        _itemsTable.Columns.Add("UnitPrice", typeof(double));
        _itemsTable.Columns.Add("DiscountPercent", typeof(double));
        _itemsTable.Columns.Add("DiscountAmount", typeof(double));
        _itemsTable.Columns.Add("VatPercent", typeof(double));
        _itemsTable.Columns.Add("VatAmount", typeof(double));
        _itemsTable.Columns.Add("OtherTaxes", typeof(double));
        _itemsTable.Columns.Add("LineTotal", typeof(double));

        _gridItems.DataSource = _itemsTable;
    }

    private void BuildUI()
    {
        // 1. Üst Başlık (Header)
        var header = new CardPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(20, 10, 20, 10) };
        var lblTitle = new Label { Text = "📄 E-Fatura / Alış Faturası Girişi", Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = "Tedarikçinizden gelen PDF veya XML e-faturayı seçerek ürün ve fiyatları tek tıkla depoya ve cariye işleyebilirsiniz.", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Toolbar & Dosya Seçimi
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15, 10, 15, 10) };
        var flowToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        var btnSelectFile = UITheme.CreateButton("📂 E-Fatura Seç (.pdf / .xml)", UITheme.Primary, Color.White, (s, e) => SelectFile(), 200, 36);
        var btnOpenPdf = UITheme.CreateButton("👁️ PDF Belgeyi Aç", UITheme.Secondary, Color.White, (s, e) => OpenOriginalPdf(), 150, 36);
        var btnGenBarcodes = UITheme.CreateButton("⚡ Otomatik Barkod Üret", Color.FromArgb(16, 185, 129), Color.White, (s, e) => GenerateMissingBarcodes(false), 175, 36);
        var btnAddRow = UITheme.CreateButton("➕ Manuel Kalem Ekle", Color.FromArgb(79, 70, 229), Color.White, (s, e) => AddManualRow(), 160, 36);
        var btnDeleteRow = UITheme.CreateButton("🗑️ Seçili Satırı Sil", UITheme.Danger, Color.White, (s, e) => DeleteSelectedRow(), 150, 36);

        flowToolbar.Controls.Add(btnSelectFile);
        flowToolbar.Controls.Add(btnOpenPdf);
        flowToolbar.Controls.Add(btnGenBarcodes);
        flowToolbar.Controls.Add(btnAddRow);
        flowToolbar.Controls.Add(btnDeleteRow);
        toolbar.Controls.Add(flowToolbar);
        Controls.Add(toolbar);

        // 3. Fatura Başlık Bilgileri
        var formPanel = new CardPanel { Dock = DockStyle.Top, Height = 115, Padding = new Padding(15, 10, 15, 10) };
        var flowFields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true };

        // Fatura No
        var pnlNo = CreateFieldPanel("Fatura No:", _txtInvoiceNo, 160);
        // Tarih
        var pnlDate = CreateFieldPanel("Fatura Tarihi:", _dtpDate, 130);
        // Tedarikçi Cari
        var pnlAccount = CreateFieldPanel("Tedarikçi (Cari):", _cmbAccount, 260);
        var btnNewAccount = UITheme.CreateButton("➕ Yeni Cari", Color.FromArgb(16, 185, 129), Color.White, (s, e) => QuickCreateAccount(), 100, 28);
        btnNewAccount.Margin = new Padding(0, 20, 15, 0);

        // Hedef Depo
        var pnlWarehouse = CreateFieldPanel("Hedef Depo:", _cmbWarehouse, 180);
        var btnNewWarehouse = UITheme.CreateButton("➕ Yeni Depo", Color.FromArgb(13, 148, 136), Color.White, (s, e) => QuickCreateWarehouse(), 105, 28);
        btnNewWarehouse.Margin = new Padding(0, 20, 15, 0);

        // Açıklama
        var pnlNote = CreateFieldPanel("Açıklama / Not:", _txtNote, 220);

        flowFields.Controls.Add(pnlNo);
        flowFields.Controls.Add(pnlDate);
        flowFields.Controls.Add(pnlAccount);
        flowFields.Controls.Add(btnNewAccount);
        flowFields.Controls.Add(pnlWarehouse);
        flowFields.Controls.Add(btnNewWarehouse);
        flowFields.Controls.Add(pnlNote);

        formPanel.Controls.Add(flowFields);
        Controls.Add(formPanel);

        // 4. Alt Toplamlar ve Onay Bölümü
        var bottomPanel = new CardPanel { Dock = DockStyle.Bottom, Height = 95, Padding = new Padding(20, 10, 20, 10) };

        var pnlLeftBottom = new Panel { Dock = DockStyle.Left, Width = 440 };
        _chkUpdateStock.Location = new Point(5, 12);
        _chkUpdateAccount.Location = new Point(5, 42);
        pnlLeftBottom.Controls.Add(_chkUpdateStock);
        pnlLeftBottom.Controls.Add(_chkUpdateAccount);
        bottomPanel.Controls.Add(pnlLeftBottom);

        var pnlRightBottom = new Panel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var flowTotals = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        var cardMatrah = CreateMetricBox("Matrah (KDV Hariç)", _lblSubTotal, Color.FromArgb(71, 85, 105), 150);
        var cardVat = CreateMetricBox("KDV Toplamı", _lblVatTotal, Color.FromArgb(99, 102, 241), 140);
        var cardGrand = CreateMetricBox("GENEL TOPLAM", _lblGrandTotal, Color.FromArgb(16, 185, 129), 180, isHighlight: true);

        var btnSave = UITheme.CreateButton("💾 Faturayı Onayla & Depoya Giriş Yap", UITheme.Success, Color.White, (s, e) => SaveInvoiceClick(), 290, 52);
        btnSave.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        btnSave.Margin = new Padding(12, 6, 0, 0);

        flowTotals.Controls.Add(cardMatrah);
        flowTotals.Controls.Add(cardVat);
        flowTotals.Controls.Add(cardGrand);
        flowTotals.Controls.Add(btnSave);

        pnlRightBottom.Controls.Add(flowTotals);
        bottomPanel.Controls.Add(pnlRightBottom);

        Controls.Add(bottomPanel);

        // 5. Grid (Orta Alan)
        var gridPanel = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        UITheme.ApplyGridStyle(_gridItems);
        _gridItems.ReadOnly = false;
        _gridItems.AllowUserToAddRows = false;
        _gridItems.RowHeadersVisible = false;
        _gridItems.CellValueChanged += (s, e) => RecalculateTotals();

        gridPanel.Controls.Add(_gridItems);
        Controls.Add(gridPanel);

        // Z-Index düzeni
        gridPanel.BringToFront();
        bottomPanel.SendToBack();
        formPanel.SendToBack();
        toolbar.SendToBack();
        header.SendToBack();

        FormatGridColumns();
    }

    private void FormatGridColumns()
    {
        if (_gridItems.Columns.Count == 0) return;

        if (_gridItems.Columns["ProductId"] is { } colProductId)
            colProductId.Visible = false;

        if (_gridItems.Columns["LineNo"] is { } colLineNo)
        {
            colLineNo.HeaderText = "Sıra";
            colLineNo.Width = 45;
            colLineNo.ReadOnly = true;
        }

        if (_gridItems.Columns["Status"] is { } colStatus)
        {
            colStatus.HeaderText = "Durum";
            colStatus.Width = 110;
            colStatus.ReadOnly = true;
        }

        if (_gridItems.Columns["Barcode"] is { } colBarcode)
        {
            colBarcode.HeaderText = "Barkod No";
            colBarcode.Width = 120;
        }

        if (_gridItems.Columns["ItemCode"] is { } colItemCode)
        {
            colItemCode.HeaderText = "Ürün Kodu";
            colItemCode.Width = 100;
        }

        if (_gridItems.Columns["ItemName"] is { } colItemName)
        {
            colItemName.HeaderText = "Mal / Hizmet (Ürün Adı)";
            colItemName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        if (_gridItems.Columns["Quantity"] is { } colQuantity)
        {
            colQuantity.HeaderText = "Miktar";
            colQuantity.Width = 70;
            colQuantity.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["Unit"] is { } colUnit)
        {
            colUnit.HeaderText = "Birim";
            colUnit.Width = 60;
        }

        if (_gridItems.Columns["UnitPrice"] is { } colUnitPrice)
        {
            colUnitPrice.HeaderText = "Birim Fiyat";
            colUnitPrice.Width = 95;
            colUnitPrice.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["DiscountPercent"] is { } colDiscPct)
        {
            colDiscPct.HeaderText = "İsk %";
            colDiscPct.Width = 55;
            colDiscPct.DefaultCellStyle.Format = "N0";
        }

        if (_gridItems.Columns["DiscountAmount"] is { } colDiscAmount)
        {
            colDiscAmount.HeaderText = "İskonto";
            colDiscAmount.Width = 75;
            colDiscAmount.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["VatPercent"] is { } colVatPct)
        {
            colVatPct.HeaderText = "KDV %";
            colVatPct.Width = 60;
            colVatPct.DefaultCellStyle.Format = "N0";
        }

        if (_gridItems.Columns["VatAmount"] is { } colVatAmount)
        {
            colVatAmount.HeaderText = "KDV Tutarı";
            colVatAmount.Width = 85;
            colVatAmount.DefaultCellStyle.Format = "N2";
            colVatAmount.ReadOnly = true;
        }

        if (_gridItems.Columns["OtherTaxes"] is { } colOtherTaxes)
        {
            colOtherTaxes.HeaderText = "Diğer Verg.";
            colOtherTaxes.Width = 75;
            colOtherTaxes.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["LineTotal"] is { } colLineTotal)
        {
            colLineTotal.HeaderText = "Mal Hizmet Tutarı";
            colLineTotal.Width = 110;
            colLineTotal.DefaultCellStyle.Format = "N2";
            colLineTotal.ReadOnly = true;
        }
    }

    private Panel CreateFieldPanel(string labelText, Control control, int width)
    {
        var pnl = new Panel { Width = width, Height = 55, Margin = new Padding(0, 0, 10, 0) };
        var lbl = new Label { Text = labelText, Font = UITheme.SmallFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 18 };
        control.Dock = DockStyle.Bottom;
        pnl.Controls.Add(lbl);
        pnl.Controls.Add(control);
        return pnl;
    }

    private static Panel CreateMetricBox(string title, Label valueLabel, Color accentColor, int width, bool isHighlight = false)
    {
        var box = new Panel
        {
            Width = width,
            Height = 62,
            Margin = new Padding(6, 4, 6, 4),
            BackColor = isHighlight ? Color.FromArgb(240, 253, 244) : Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 6, 10, 6)
        };
        box.Paint += (s, e) =>
        {
            using var pen = new Pen(isHighlight ? accentColor : Color.FromArgb(203, 213, 225), isHighlight ? 2f : 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.TopRight
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = isHighlight ? new Font("Segoe UI", 13.5f, FontStyle.Bold) : new Font("Segoe UI", 11f, FontStyle.Bold);
        valueLabel.ForeColor = isHighlight ? accentColor : Color.FromArgb(30, 41, 59);
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.AutoSize = false;

        box.Controls.Add(valueLabel);
        box.Controls.Add(lblTitle);
        return box;
    }

    private void LoadCombos()
    {
        // 1. Tedarikçiler
        var accounts = Database.Query("SELECT Id, Name FROM Accounts WHERE IsActive = 1 ORDER BY Name;");
        _cmbAccount.Items.Clear();
        _cmbAccount.DisplayMember = "Name";
        _cmbAccount.ValueMember = "Id";
        foreach (DataRow row in accounts.Rows)
        {
            _cmbAccount.Items.Add(new ComboBoxItem(Convert.ToInt64(row["Id"]), row["Name"].ToString()!));
        }

        // 2. Depolar
        var warehouses = Database.Query("SELECT Id, Name FROM Warehouses WHERE IsActive = 1 ORDER BY IsDefault DESC, Name;");
        _cmbWarehouse.Items.Clear();
        _cmbWarehouse.DisplayMember = "Name";
        _cmbWarehouse.ValueMember = "Id";
        foreach (DataRow row in warehouses.Rows)
        {
            _cmbWarehouse.Items.Add(new ComboBoxItem(Convert.ToInt64(row["Id"]), row["Name"].ToString()!));
        }
        if (_cmbWarehouse.Items.Count > 0)
            _cmbWarehouse.SelectedIndex = 0;
    }

    private void SelectFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "E-Fatura Dosyası Seçin (.pdf veya .xml)",
            Filter = "E-Fatura Dosyaları (*.pdf;*.xml)|*.pdf;*.xml|PDF Belgeleri (*.pdf)|*.pdf|XML Belgeleri (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            ProcessSelectedFile(ofd.FileName);
        }
    }

    private void ProcessSelectedFile(string filePath)
    {
        _selectedFilePath = filePath;
        Cursor = Cursors.WaitCursor;

        try
        {
            _parsedResult = InvoiceParserService.ParseInvoiceFile(filePath);

            if (!_parsedResult.Success)
            {
                MessageBox.Show(
                    "Fatura okunurken uyarı oluştu:\n\n" + _parsedResult.ErrorMessage + 
                    "\n\nFatura bilgilerini manuel olarak düzenleyebilirsiniz.",
                    "Fatura Okuma",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }

            // Alanları doldur
            if (!string.IsNullOrWhiteSpace(_parsedResult.InvoiceNumber))
                _txtInvoiceNo.Text = _parsedResult.InvoiceNumber;

            _dtpDate.Value = _parsedResult.InvoiceDate;

            // Cari Eşleme
            try
            {
                AutoMatchAccount(_parsedResult.SupplierTaxNumber, _parsedResult.SupplierName);
            }
            catch { }

            // Kalemleri doldur
            _itemsTable.Rows.Clear();
            int counter = 1;
            foreach (var item in _parsedResult.Items)
            {
                var matchedProduct = InvoiceService.FindProductByNameOrCode(item.ItemName, item.Barcode ?? item.ItemCode);
                var row = _itemsTable.NewRow();
                row["ProductId"] = matchedProduct?.Id ?? (object)DBNull.Value;
                row["LineNo"] = item.LineNo > 0 ? item.LineNo : counter++;
                row["Status"] = matchedProduct != null ? "✅ Kayıtlı" : "⚠️ Yeni Ürün";
                row["Barcode"] = matchedProduct?.Barcode ?? item.Barcode ?? "";
                row["ItemCode"] = matchedProduct?.Code ?? item.ItemCode ?? "";
                row["ItemName"] = matchedProduct?.Name ?? item.ItemName;
                row["Quantity"] = item.Quantity > 0 ? item.Quantity : 1;
                row["Unit"] = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "Adet";
                row["UnitPrice"] = item.UnitPrice;
                row["DiscountPercent"] = item.DiscountPercent;
                row["DiscountAmount"] = item.DiscountAmount;
                row["VatPercent"] = item.VatPercent;
                row["VatAmount"] = item.VatAmount > 0 ? item.VatAmount : Math.Round(item.Quantity * item.UnitPrice * (item.VatPercent / 100.0), 2);
                row["OtherTaxes"] = item.OtherTaxes;
                row["LineTotal"] = item.LineTotal > 0 ? item.LineTotal : Math.Round((item.Quantity * item.UnitPrice) - item.DiscountAmount, 2);
                _itemsTable.Rows.Add(row);
            }

            RecalculateTotals();

            // Eğer kalem toplamı 0 ama faturadan toplam tutar çekildiyse alt toplamlara yansıt
            if (_itemsTable.Rows.Count == 0 && _parsedResult.GrandTotal > 0)
            {
                _lblSubTotal.Text = _parsedResult.SubTotal.ToString("N2") + " ₺";
                _lblVatTotal.Text = _parsedResult.VatTotal.ToString("N2") + " ₺";
                _lblGrandTotal.Text = _parsedResult.GrandTotal.ToString("N2") + " ₺";
            }

            FormatGridColumns();

            string countMsg = _parsedResult.Items.Count > 0 
                ? $"{_parsedResult.Items.Count} adet kalem başarıyla aktarıldı." 
                : "Fatura tutarları aktarıldı. 'Manuel Kalem Ekle' butonuyla ürünleri girebilirsiniz.";

            MessageBox.Show(
                $"Fatura ({_parsedResult.FileType}) başarıyla yüklendi!\n\n" +
                $"Fatura No: {_parsedResult.InvoiceNumber}\n" +
                $"Tarih: {_parsedResult.InvoiceDate:dd.MM.yyyy}\n" +
                $"Tedarikçi: {(!string.IsNullOrWhiteSpace(_parsedResult.SupplierName) ? _parsedResult.SupplierName : "[Seçiniz]")}\n" +
                $"Genel Toplam: {_parsedResult.GrandTotal:N2} ₺\n\n" +
                countMsg,
                "E-Fatura Yüklendi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            // Barkod kontrolü: Faturada veya sistemde barkodu olmayan ürünleri tespit et
            int missingBarcodeCount = 0;
            foreach (DataRow row in _itemsTable.Rows)
            {
                string b = row["Barcode"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(b))
                {
                    missingBarcodeCount++;
                }
            }

            if (missingBarcodeCount > 0)
            {
                var askResult = MessageBox.Show(
                    $"Faturadaki {missingBarcodeCount} adet ürünün barkod numarası bulunamadı veya okunamadı.\n\n" +
                    "Bu ürünler için sistem tarafından dükkanınıza özel benzersiz EAN-13 iç barkod numarası (örn: 200...) otomatik oluşturulsun mu?\n\n" +
                    "• [Evet] : Tüm barkodsuz ürünlere anında benzersiz barkod atanır (Raf etiketi ve barkod basımına hazır olur).\n" +
                    "• [Hayır] : Ürünler barkodsuz olarak bırakılır.",
                    "Barkod Numarası Oluşturulsun mu?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (askResult == DialogResult.Yes)
                {
                    GenerateMissingBarcodes(false);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fatura işlenirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void AutoMatchAccount(string? taxNumber, string? name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = name.Trim();
                if (name.Length > 120) name = name.Substring(0, 120);
            }

            var matched = InvoiceService.FindAccountByTaxOrName(taxNumber, name);
            if (matched != null)
            {
                for (int i = 0; i < _cmbAccount.Items.Count; i++)
                {
                    if (_cmbAccount.Items[i] is ComboBoxItem item && item.Id == matched.Id)
                    {
                        _cmbAccount.SelectedIndex = i;
                        return;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(name) && name.Length >= 4 && !name.Contains("Toplam") && !name.Contains("Matrah") && !name.Contains("KDV"))
            {
                var res = MessageBox.Show(
                    $"Faturadaki satıcı firma '{name}' sistemde bulunamadı.\n\n" +
                    "Bu firmayı sisteminize yeni bir Tedarikçi Cari olarak otomatik eklemek ister misiniz?",
                    "Yeni Cari Oluşturulsun mu?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (res == DialogResult.Yes)
                {
                    Database.Execute(@"
INSERT INTO Accounts (Name, Type, Phone, TaxOffice, TaxNumber, Address, PriceGroup, DefaultDiscountPercent, BalanceLimit, IsActive)
VALUES (@n, 'Tedarikçi', '', @to, @tax, @addr, 'Perakende', 0, 0, 1);",
                        ("@n", name),
                        ("@to", (object?)_parsedResult?.SupplierTaxOffice ?? DBNull.Value),
                        ("@tax", (object?)taxNumber ?? DBNull.Value),
                        ("@addr", (object?)_parsedResult?.SupplierAddress ?? DBNull.Value)
                    );

                    LoadCombos();
                    AutoMatchAccount(taxNumber, name);
                }
            }
        }
        catch { }
    }

    private void QuickCreateAccount()
    {
        using var accForm = new AccountForm();
        if (accForm.ShowDialog() == DialogResult.OK)
        {
            LoadCombos();
            if (!string.IsNullOrWhiteSpace(_parsedResult?.SupplierName))
            {
                AutoMatchAccount(_parsedResult.SupplierTaxNumber, _parsedResult.SupplierName);
            }
        }
    }

    private void QuickCreateWarehouse()
    {
        using var whForm = new WarehouseManageDialog();
        whForm.ShowDialog();
        LoadCombos();
    }

    /// <summary>
    /// Barkodu boş olan ürünlere otomatik benzersiz EAN-13 mağaza iç barkodu üretir.
    /// </summary>
    private void GenerateMissingBarcodes(bool silent = false)
    {
        int generatedCount = 0;
        foreach (DataRow row in _itemsTable.Rows)
        {
            string b = row["Barcode"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(b))
            {
                string newBarcode = BarcodeHelper.GenerateUniqueInternalBarcode("200");
                row["Barcode"] = newBarcode;
                string status = row["Status"]?.ToString() ?? "";
                if (status.Contains("Yeni"))
                {
                    row["Status"] = "⚠️ Yeni (İç Barkodlu)";
                }
                generatedCount++;
            }
        }

        if (generatedCount > 0)
        {
            if (!silent)
            {
                MessageBox.Show(
                    $"{generatedCount} adet ürüne dükkanınıza özel benzersiz EAN-13 iç barkod numarası başarıyla atandı!\n\n" +
                    "Ürünler kaydedildiğinde bu barkodlar ürün kartına işlenecek ve barkod basımına hazır olacaktır.",
                    "İç Barkodlar Oluşturuldu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
        else if (!silent)
        {
            MessageBox.Show(
                "Faturadaki tüm ürünlerin zaten barkod numarası tanımlıdır.",
                "Bilgi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    private void AddManualRow()
    {
        var row = _itemsTable.NewRow();
        row["ProductId"] = DBNull.Value;
        row["LineNo"] = _itemsTable.Rows.Count + 1;
        row["Status"] = "⚠️ Yeni Ürün";
        row["Barcode"] = "";
        row["ItemCode"] = "URN-" + DateTime.Now.ToString("HHmmss");
        row["ItemName"] = "Yeni Ürün";
        row["Quantity"] = 1.0;
        row["Unit"] = "Adet";
        row["UnitPrice"] = 0.0;
        row["DiscountPercent"] = 0.0;
        row["DiscountAmount"] = 0.0;
        row["VatPercent"] = 20.0;
        row["VatAmount"] = 0.0;
        row["OtherTaxes"] = 0.0;
        row["LineTotal"] = 0.0;
        _itemsTable.Rows.Add(row);

        RecalculateTotals();
    }

    private void DeleteSelectedRow()
    {
        if (_gridItems.CurrentRow != null && !_gridItems.CurrentRow.IsNewRow)
        {
            _gridItems.Rows.Remove(_gridItems.CurrentRow);
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        double subTotal = 0;
        double vatTotal = 0;

        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;

            double qty = Convert.ToDouble(row["Quantity"] == DBNull.Value ? 0 : row["Quantity"]);
            double price = Convert.ToDouble(row["UnitPrice"] == DBNull.Value ? 0 : row["UnitPrice"]);
            double discPercent = Convert.ToDouble(row["DiscountPercent"] == DBNull.Value ? 0 : row["DiscountPercent"]);
            double vatPercent = Convert.ToDouble(row["VatPercent"] == DBNull.Value ? 20 : row["VatPercent"]);
            double otherTaxes = Convert.ToDouble(row["OtherTaxes"] == DBNull.Value ? 0 : row["OtherTaxes"]);

            double gross = Math.Round(qty * price, 2);
            double discAmount = Math.Round(gross * (discPercent / 100.0), 2);
            row["DiscountAmount"] = discAmount;

            double netLine = gross - discAmount;
            double lineVat = Math.Round(netLine * (vatPercent / 100.0), 2);
            double lineTotal = netLine; // Mal Hizmet Tutarı = Net Matrah

            row["VatAmount"] = lineVat;
            row["LineTotal"] = lineTotal;

            subTotal += netLine;
            vatTotal += lineVat;
        }

        // Eğer faturadan genel toplam ve matrah çekildiyse ve tablodaki satırlar boşsa veya toplam 0 ise faturadakini koru
        if (subTotal == 0 && _parsedResult != null && _parsedResult.SubTotal > 0)
        {
            subTotal = _parsedResult.SubTotal;
            vatTotal = _parsedResult.VatTotal;
        }

        double grandTotal = subTotal + vatTotal;

        _lblSubTotal.Text = subTotal.ToString("N2") + " ₺";
        _lblVatTotal.Text = vatTotal.ToString("N2") + " ₺";
        _lblGrandTotal.Text = grandTotal.ToString("N2") + " ₺";
    }

    private void OpenOriginalPdf()
    {
        if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _selectedFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDF dosyası açılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            MessageBox.Show("Açılacak bir PDF dosyası henüz seçilmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveInvoiceClick()
    {
        // Doğrulamalar
        if (string.IsNullOrWhiteSpace(_txtInvoiceNo.Text))
        {
            MessageBox.Show("Lütfen fatura numarasını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbAccount.SelectedItem is not ComboBoxItem accItem)
        {
            MessageBox.Show("Lütfen faturanın ait olduğu tedarikçi cariyi seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbWarehouse.SelectedItem is not ComboBoxItem wItem)
        {
            MessageBox.Show("Lütfen ürünlerin gireceği hedef depoyu seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_itemsTable.Rows.Count == 0)
        {
            MessageBox.Show("Faturada en az 1 adet ürün kalemi olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Barkodu olmayan ürünler için kullanıcıya oluşturulsun mu diye sor
        bool hasMissingBarcode = false;
        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            string b = row["Barcode"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(b))
            {
                hasMissingBarcode = true;
                break;
            }
        }

        if (hasMissingBarcode)
        {
            var askBarcode = MessageBox.Show(
                "Faturadaki bazı ürünlerde barkod numarası bulunmuyor.\n\n" +
                "Barkodu olmayan bu ürünler için dükkanınıza özel otomatik iç barkod (örn: 200...) üretilsin mi?\n\n" +
                "• [Evet]: Eksik barkodları otomatik üretip kaydeder.\n" +
                "• [Hayır]: Barkodsuz olarak kayda devam eder.\n" +
                "• [İptal]: Kayıt işlemini durdurur.",
                "Barkod Oluşturulsun mu?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (askBarcode == DialogResult.Cancel)
            {
                return;
            }
            if (askBarcode == DialogResult.Yes)
            {
                GenerateMissingBarcodes(silent: true);
            }
        }

        // Invoice nesnesini doldur
        var invoice = new Invoice
        {
            InvoiceNumber = _txtInvoiceNo.Text.Trim(),
            InvoiceDate = _dtpDate.Value,
            InvoiceType = "Alış Faturası",
            AccountId = accItem.Id,
            AccountName = accItem.Text,
            WarehouseId = wItem.Id,
            WarehouseName = wItem.Text,
            PdfPath = _selectedFilePath,
            Note = _txtNote.Text.Trim(),
            CustomizationId = _parsedResult?.CustomizationId,
            Scenario = _parsedResult?.Scenario,
            InvoiceKind = _parsedResult?.InvoiceKind,
            OrderNumber = _parsedResult?.OrderNumber,
            OrderDate = _parsedResult?.OrderDate,
            RelatedStore = _parsedResult?.RelatedStore,
            CargoId = _parsedResult?.CargoId,
            ReferenceNo = _parsedResult?.ReferenceNo,
            IssueTime = _parsedResult?.IssueTime,
            CreatedAt = DateTime.Now
        };

        double subTotal = 0;
        double vatTotal = 0;
        int rowIdx = 1;

        foreach (DataRow row in _itemsTable.Rows)
        {
            double qty = Convert.ToDouble(row["Quantity"]);
            double price = Convert.ToDouble(row["UnitPrice"]);
            double discPercent = Convert.ToDouble(row["DiscountPercent"]);
            double discAmount = Convert.ToDouble(row["DiscountAmount"]);
            double vatPercent = Convert.ToDouble(row["VatPercent"]);
            double vatAmount = Convert.ToDouble(row["VatAmount"]);
            double otherTaxes = Convert.ToDouble(row["OtherTaxes"]);
            double lineTotal = Convert.ToDouble(row["LineTotal"]);

            subTotal += (qty * price) - discAmount;
            vatTotal += vatAmount;

            invoice.Items.Add(new InvoiceItem
            {
                ProductId = row["ProductId"] != DBNull.Value ? Convert.ToInt64(row["ProductId"]) : null,
                LineNo = rowIdx++,
                Barcode = row["Barcode"]?.ToString()?.Trim(),
                ItemCode = row["ItemCode"]?.ToString()?.Trim(),
                ItemName = row["ItemName"]?.ToString()?.Trim() ?? "Ürün",
                Quantity = qty > 0 ? qty : 1,
                Unit = row["Unit"]?.ToString() ?? "Adet",
                UnitPrice = price,
                DiscountPercent = discPercent,
                DiscountAmount = discAmount,
                VatPercent = vatPercent,
                VatAmount = vatAmount,
                OtherTaxes = otherTaxes,
                LineTotal = lineTotal
            });
        }

        invoice.SubTotal = subTotal;
        invoice.VatTotal = vatTotal;
        invoice.GrandTotal = subTotal + vatTotal;

        try
        {
            InvoiceService.SaveInvoice(invoice, _chkUpdateStock.Checked, _chkUpdateAccount.Checked);

            MessageBox.Show(
                $"Fatura ({invoice.InvoiceNumber}) başarıyla sisteme kaydedildi ve işlendi!\n\n" +
                $"Tedarikçi: {invoice.AccountName}\n" +
                $"Hedef Depo: {invoice.WarehouseName}\n" +
                $"Kalem Sayısı: {invoice.Items.Count}\n" +
                $"Genel Toplam: {invoice.GrandTotal:N2} ₺\n\n" +
                (_chkUpdateStock.Checked ? "✅ Ürünler hedef depoya stok girişi yapıldı.\n" : "") +
                (_chkUpdateStock.Checked ? "✅ Sistemde olmayan ürünler için otomatik stok kartı ve barkod oluşturuldu.\n" : "") +
                (_chkUpdateAccount.Checked ? "✅ Tedarikçi cari ekstresine alış kaydı düşüldü.\n" : "") +
                "✅ PDF belgesi güvenle arşivlendi.",
                "Fatura ve Stok Girişi Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fatura kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private class ComboBoxItem
    {
        public long Id { get; set; }
        public string Text { get; set; }

        public ComboBoxItem(long id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() => Text;
    }
}
