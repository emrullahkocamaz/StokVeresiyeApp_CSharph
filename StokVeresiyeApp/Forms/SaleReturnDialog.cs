using System.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

/// <summary>Satış iadesi ve değişimi: satış belgesini bul, iade edilecek kalemleri seç, gerekirse yeni ürün ver.</summary>
public class SaleReturnDialog : Form
{
    private readonly TextBox _txtSearch = new();
    private readonly DateTimePicker _dtFrom = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    private readonly DateTimePicker _dtTo = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    private readonly DataGridView _gridSales = new();
    private readonly DataGridView _gridLines = new();
    private readonly DataGridView _gridNew = new();
    private readonly TextBox _txtProduct = new();
    private readonly ComboBox _cmbMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _txtNote = new() { Width = 300 };
    private readonly Label _lblSummary = new();
    private readonly Label _lblSaleInfo = new();
    private readonly Button _btnDone;

    private readonly DataTable _sales = new();
    private readonly DataTable _lines = new();
    private readonly DataTable _newItems = new();
    private string _docNo = "";
    private long _accountId;
    private long _warehouseId;

    public SaleReturnDialog()
    {
        Text = "↩️ Satış İadesi & Değişim";
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        _dtFrom.Value = DateTime.Today.AddDays(-30);
        _dtTo.Value = DateTime.Today;

        _lines.Columns.Add("ProductId", typeof(long));
        _lines.Columns.Add("Ürün", typeof(string));
        _lines.Columns.Add("Satılan", typeof(double));
        _lines.Columns.Add("İade Edilmiş", typeof(double));
        _lines.Columns.Add("İade Edilebilir", typeof(double));
        _lines.Columns.Add("İade Adet", typeof(double));
        _lines.Columns.Add("Birim Fiyat", typeof(double));
        _lines.Columns.Add("İade Tutarı", typeof(double));

        _newItems.Columns.Add("ProductId", typeof(long));
        _newItems.Columns.Add("Ürün", typeof(string));
        _newItems.Columns.Add("Adet", typeof(double));
        _newItems.Columns.Add("Birim Fiyat", typeof(double));
        _newItems.Columns.Add("Tutar", typeof(double));

        _cmbMethod.Items.AddRange(new object[] { "Nakit", "Kredi Kartı", "Havale/EFT", SaleReturnService.ToCurrentAccount });
        _cmbMethod.SelectedIndex = 0;
        _cmbMethod.SelectedIndexChanged += (s, e) => UpdateSummary();

        _btnDone = UITheme.CreateButton("✅ İadeyi / Değişimi Tamamla", UITheme.Success, Color.White, (s, e) => Complete(), 250, 40);

        // Ana yerleşim: üstte arama+satışlar, ortada kalemler, altında değişim ürünleri, en altta özet
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // arama çubuğu
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));    // satış listesi
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 34));    // iade kalemleri
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));    // değişim ürünleri
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // özet + buton

        // 1) Arama çubuğu
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        _txtSearch.Width = 280;
        _txtSearch.PlaceholderText = "Belge no, müşteri, ürün veya barkod...";
        _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoadSales(); e.SuppressKeyPress = true; } };
        bar.Controls.Add(new Label { Text = "1) Satışı bul:", AutoSize = true, Margin = new Padding(0, 8, 6, 0), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });
        bar.Controls.Add(_txtSearch);
        bar.Controls.Add(_dtFrom);
        bar.Controls.Add(new Label { Text = "–", AutoSize = true, Margin = new Padding(2, 8, 2, 0) });
        bar.Controls.Add(_dtTo);
        bar.Controls.Add(UITheme.CreateButton("🔍 Ara", UITheme.Primary, Color.White, (s, e) => LoadSales(), 90, 30));
        root.Controls.Add(bar, 0, 0);

        // 2) Satış listesi
        StyleGrid(_gridSales);
        _gridSales.DataSource = _sales;
        _gridSales.SelectionChanged += (s, e) => OnSaleSelected();
        root.Controls.Add(Group("Satış belgeleri", _gridSales), 0, 1);

        // 3) İade kalemleri
        StyleGrid(_gridLines);
        _gridLines.ReadOnly = false;
        _gridLines.DataSource = _lines;
        _gridLines.CellEndEdit += (s, e) => { RecalcLines(); };
        _gridLines.DataError += (s, e) => e.ThrowException = false;
        var pnlLines = Group("2) İade edilecek ürünler — 'İade Adet' ve 'Birim Fiyat' sütunları düzenlenebilir", _gridLines);
        root.Controls.Add(pnlLines, 0, 2);

        // 4) Değişim ürünleri
        StyleGrid(_gridNew);
        _gridNew.ReadOnly = false;
        _gridNew.DataSource = _newItems;
        _gridNew.CellEndEdit += (s, e) => RecalcNew();
        _gridNew.DataError += (s, e) => e.ThrowException = false;
        _gridNew.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && _gridNew.CurrentRow != null && !_gridNew.CurrentRow.IsNewRow)
            {
                _gridNew.Rows.Remove(_gridNew.CurrentRow);
                RecalcNew();
            }
        };
        var newHost = new Panel { Dock = DockStyle.Fill };
        var newTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
        _txtProduct.Width = 320;
        _txtProduct.PlaceholderText = "Barkod veya ürün adı yazıp Enter (Del: satırı sil)";
        _txtProduct.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddNewProduct(); e.SuppressKeyPress = true; } };
        newTop.Controls.Add(_txtProduct);
        newTop.Controls.Add(UITheme.CreateButton("➕ Ekle", UITheme.Secondary, Color.White, (s, e) => AddNewProduct(), 90, 30));
        newHost.Controls.Add(_gridNew);
        newHost.Controls.Add(newTop);
        _gridNew.BringToFront();
        root.Controls.Add(Group("3) Değişimde verilecek yeni ürünler (isteğe bağlı)", newHost), 0, 3);

        // 5) Özet + ödeme
        var foot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _lblSummary.AutoSize = true;
        _lblSummary.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _lblSummary.Margin = new Padding(0, 2, 0, 4);
        _lblSaleInfo.AutoSize = true;
        _lblSaleInfo.ForeColor = UITheme.TextSecondary;
        var payRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        payRow.Controls.Add(new Label { Text = "Fark / iade yöntemi:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        payRow.Controls.Add(_cmbMethod);
        payRow.Controls.Add(new Label { Text = "Not:", AutoSize = true, Margin = new Padding(14, 6, 6, 0) });
        payRow.Controls.Add(_txtNote);
        left.Controls.Add(_lblSummary);
        left.Controls.Add(payRow);
        left.Controls.Add(_lblSaleInfo);
        foot.Controls.Add(left, 0, 0);
        _btnDone.Anchor = AnchorStyles.Right;
        foot.Controls.Add(_btnDone, 1, 0);
        root.Controls.Add(foot, 0, 4);

        Controls.Add(root);
        LoadSales();
        UpdateSummary();
    }

    private static Control Group(string title, Control content)
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 6) };
        var lbl = new Label { Text = title, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = UITheme.TextPrimary };
        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
        host.Controls.Add(lbl);
        content.BringToFront();
        return host;
    }

    private void StyleGrid(DataGridView g)
    {
        UITheme.ApplyGridStyle(g);
        g.AllowUserToAddRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.ReadOnly = true;
        g.DataBindingComplete += (s, e) =>
        {
            foreach (var name in new[] { "ProductId", "AccountId", "WarehouseId" })
                if (g.Columns[name] != null) g.Columns[name]!.Visible = false;
            foreach (DataGridViewColumn c in g.Columns)
                if (c.ValueType == typeof(double)) c.DefaultCellStyle.Format = "N2";
            if (g == _gridLines)
                foreach (DataGridViewColumn c in g.Columns)
                    c.ReadOnly = c.Name != "İade Adet" && c.Name != "Birim Fiyat";
            if (g == _gridNew)
                foreach (DataGridViewColumn c in g.Columns)
                    c.ReadOnly = c.Name != "Adet" && c.Name != "Birim Fiyat";
        };
    }

    private void LoadSales()
    {
        try
        {
            var dt = SaleReturnService.FindSales(_txtSearch.Text, _dtFrom.Value.Date, _dtTo.Value.Date);
            _sales.Clear();
            _sales.Merge(dt);
            if (_gridSales.Columns["Tutar"] != null) _gridSales.Columns["Tutar"]!.DefaultCellStyle.Format = "N2";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Satışlar yüklenemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        if (_sales.Rows.Count == 0) { _lines.Clear(); _docNo = ""; }
        else OnSaleSelected();
    }

    private void OnSaleSelected()
    {
        if (_gridSales.CurrentRow == null || _gridSales.CurrentRow.Index < 0 || _gridSales.CurrentRow.Index >= _sales.Rows.Count) return;
        var row = ((DataRowView)_gridSales.CurrentRow.DataBoundItem).Row;
        string doc = row["Belge No"]?.ToString() ?? "";
        if (doc == _docNo && _lines.Rows.Count > 0) return;

        _docNo = doc;
        _accountId = Convert.ToInt64(row["AccountId"]);
        _lines.Clear();
        _newItems.Clear();

        var dt = SaleReturnService.GetSaleLines(doc);
        _warehouseId = 0;
        foreach (DataRow r in dt.Rows)
        {
            double sold = Convert.ToDouble(r["Satılan"]);
            double done = Convert.ToDouble(r["İade Edilmiş"]);
            double left = Math.Max(0, sold - done);
            if (_warehouseId == 0 && r["WarehouseId"] != DBNull.Value) _warehouseId = Convert.ToInt64(r["WarehouseId"]);
            _lines.Rows.Add(Convert.ToInt64(r["ProductId"]), r["Ürün"]?.ToString() ?? "", sold, done, left, 0.0, Math.Round(Convert.ToDouble(r["Birim Fiyat"]), 2), 0.0);
        }
        if (_warehouseId == 0) _warehouseId = WarehouseService.GetDefaultWarehouse()?.Id ?? 1;

        // Varsayılan yöntem: cariye ait bir satışsa mahsup, perakendeyse nakit
        _cmbMethod.SelectedIndex = _accountId > 0 ? 3 : 0;
        _lblSaleInfo.Text = $"Seçili belge: {doc}" + (_accountId > 0 ? $"  ·  Cari: {row["Cari"]}" : "  ·  Perakende müşteri");
        UpdateSummary();
    }

    private void RecalcLines()
    {
        foreach (DataRow r in _lines.Rows)
        {
            double max = Convert.ToDouble(r["İade Edilebilir"]);
            double q = Math.Clamp(Convert.ToDouble(r["İade Adet"]), 0, max);
            r["İade Adet"] = q;
            r["İade Tutarı"] = Math.Round(q * Convert.ToDouble(r["Birim Fiyat"]), 2);
        }
        UpdateSummary();
    }

    private void RecalcNew()
    {
        foreach (DataRow r in _newItems.Rows)
        {
            double q = Math.Max(0, Convert.ToDouble(r["Adet"]));
            r["Adet"] = q;
            r["Tutar"] = Math.Round(q * Convert.ToDouble(r["Birim Fiyat"]), 2);
        }
        UpdateSummary();
    }

    private void AddNewProduct()
    {
        string q = _txtProduct.Text.Trim();
        if (q.Length == 0) return;
        try
        {
            var dt = ProductService.GetAllProducts(q);
            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Ürün bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DataRow? pick = dt.Rows.Cast<DataRow>().FirstOrDefault(r => string.Equals(r["Barkod"]?.ToString(), q, StringComparison.OrdinalIgnoreCase)
                                                                      || string.Equals(r["Ürün Kodu"]?.ToString(), q, StringComparison.OrdinalIgnoreCase));
            if (pick == null && dt.Rows.Count == 1) pick = dt.Rows[0];
            if (pick == null)
            {
                var menu = new ContextMenuStrip();
                foreach (var r in dt.Rows.Cast<DataRow>().Take(12))
                {
                    var captured = r;
                    menu.Items.Add($"{r["Ürün Adı"]}   ·   {Convert.ToDouble(r["Satış Fiyatı"]):N2} ₺   ·   stok {r["Kalan Stok"]}", null, (s, e) => AddRowFor(captured));
                }
                menu.Show(_txtProduct, new Point(0, _txtProduct.Height));
                return;
            }
            AddRowFor(pick);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ürün eklenemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddRowFor(DataRow p)
    {
        long id = Convert.ToInt64(p["Id"]);
        var existing = _newItems.Rows.Cast<DataRow>().FirstOrDefault(r => (long)r["ProductId"] == id);
        if (existing != null) existing["Adet"] = Convert.ToDouble(existing["Adet"]) + 1;
        else _newItems.Rows.Add(id, p["Ürün Adı"]?.ToString() ?? "", 1.0, Math.Round(Convert.ToDouble(p["Satış Fiyatı"]), 2), 0.0);
        _txtProduct.Clear();
        _txtProduct.Focus();
        RecalcNew();
    }

    private SaleReturnRequest BuildRequest()
    {
        var req = new SaleReturnRequest
        {
            OriginalDocNo = _docNo,
            AccountId = _accountId,
            WarehouseId = _warehouseId,
            Method = _cmbMethod.SelectedItem?.ToString() ?? "Nakit",
            Note = _txtNote.Text.Trim()
        };
        foreach (DataRow r in _lines.Rows)
        {
            double q = Convert.ToDouble(r["İade Adet"]);
            if (q > 0) req.Returned.Add(new ReturnLine { ProductId = (long)r["ProductId"], Quantity = q, UnitPrice = Convert.ToDouble(r["Birim Fiyat"]) });
        }
        foreach (DataRow r in _newItems.Rows)
        {
            double q = Convert.ToDouble(r["Adet"]);
            if (q > 0) req.NewItems.Add(new ReturnLine { ProductId = (long)r["ProductId"], Quantity = q, UnitPrice = Convert.ToDouble(r["Birim Fiyat"]) });
        }
        return req;
    }

    private void UpdateSummary()
    {
        var req = BuildRequest();
        string diff;
        if (req.Net > 0.004) diff = $"Müşteri ödeyecek: {req.Net:N2} ₺";
        else if (req.Net < -0.004) diff = $"Müşteriye geri ödenecek: {-req.Net:N2} ₺";
        else diff = "Fark yok";
        _lblSummary.Text = $"İade: {req.ReturnTotal:N2} ₺    Yeni ürünler: {req.NewTotal:N2} ₺    →  {diff}";
        _lblSummary.ForeColor = req.Net > 0.004 ? UITheme.Primary : req.Net < -0.004 ? UITheme.Danger : UITheme.TextPrimary;
        _btnDone.Enabled = !string.IsNullOrEmpty(_docNo) && req.Returned.Count > 0;
    }

    private void Complete()
    {
        var req = BuildRequest();
        if (req.Returned.Count == 0)
        {
            MessageBox.Show("İade edilecek en az bir ürün için 'İade Adet' giriniz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (req.Method == SaleReturnService.ToCurrentAccount && _accountId <= 0)
        {
            MessageBox.Show("Perakende satışta cari hesaba mahsup yapılamaz. Nakit, kart veya havale seçiniz.", "Yöntem Uygun Değil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string kind = req.NewItems.Count > 0 ? "DEĞİŞİM" : "İADE";
        string sum = $"{kind} onayı\n\nBelge: {_docNo}\nİade: {req.ReturnTotal:N2} ₺\nYeni ürünler: {req.NewTotal:N2} ₺\n" +
                     (req.Net > 0.004 ? $"Müşteriden alınacak: {req.Net:N2} ₺ ({req.Method})" : req.Net < -0.004 ? $"Müşteriye geri ödenecek: {-req.Net:N2} ₺ ({req.Method})" : "Fark yok") +
                     "\n\nStok ve kasa/cari hareketleri işlenecek. Devam edilsin mi?";
        if (MessageBox.Show(sum, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        var guard = new SaleGuardOptions();
        while (true)
        {
            try
            {
                string doc = SaleReturnService.Process(req, guard);
                MessageBox.Show($"{kind} kaydedildi.\nİşlem belge no: {doc}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            catch (SaleBlockedException ex)
            {
                var ask = MessageBox.Show(ex.Message + "\n\nYine de devam edilsin mi?", "Uyarı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ask != DialogResult.Yes) return;
                if (ex.Kind == SaleBlockKind.InsufficientStock) guard.AllowNegativeStock = true; else guard.AllowOverCreditLimit = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
}
