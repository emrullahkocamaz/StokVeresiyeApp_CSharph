using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class InvoiceMetaDialog : Form
{
    private readonly long _productId;
    private readonly string _productName;
    private DataRow? _invRow;
    private DataTable _itemsTable = new();

    public InvoiceMetaDialog(long productId, string productName)
    {
        _productId = productId;
        _productName = productName;

        InitializeComponent();
        LoadInvoiceData();
    }

    private void InitializeComponent()
    {
        Text = $"Fatura Meta Bilgileri ve Ürün Kalemleri - {_productName}";
        Size = new Size(960, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
    }

    private void LoadInvoiceData()
    {
        var (invRow, itemsDt) = InvoiceService.GetProductInvoiceMetaAndAllItems(_productId);
        _invRow = invRow;
        _itemsTable = itemsDt;

        Controls.Clear();

        if (_invRow == null)
        {
            var pnlEmpty = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
            var lblEmpty = new Label
            {
                Text = $"Bu ürüne ({_productName}) ait sisteme işlenmiş bir alış faturası veya belge kaydı henüz bulunmamaktadır.\n\n" +
                       "E-Fatura / Alış Faturası Girişi ekranından fatura yükleyerek bu ürünün tüm fatura meta bilgilerini ve faturadaki diğer ürünleri görüntüleyebilirsiniz.",
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var btnCloseEmpty = new Button
            {
                Text = "Kapat",
                Size = new Size(120, 40),
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(100, 116, 139),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCloseEmpty.Click += (s, e) => Close();
            pnlEmpty.Controls.Add(lblEmpty);
            pnlEmpty.Controls.Add(btnCloseEmpty);
            Controls.Add(pnlEmpty);
            return;
        }

        BuildUi();
    }

    private void BuildUi()
    {
        // 1. Üst Başlık Banner
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(13, 148, 136), // Turkuaz Marka Rengi
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = $"📄 Fatura Meta Bilgileri: {_invRow!["InvoiceNumber"]}",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 10)
        };

        var lblSub = new Label
        {
            Text = $"Tedarikçi: {_invRow["AccountName"]} | Ürün: {_productName}",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(204, 251, 241),
            AutoSize = true,
            Location = new Point(16, 36)
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSub);
        Controls.Add(headerPanel);

        // 2. Meta Bilgileri Grid/Panel
        var metaContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 195,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };
        metaContainer.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawRectangle(pen, 0, 0, metaContainer.Width - 1, metaContainer.Height - 1);
        };

        var tableMeta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        tableMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        tableMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
        tableMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        tableMeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));

        void AddMetaRow(int row, string label1, string val1, string label2, string val2)
        {
            tableMeta.Controls.Add(new Label { Text = label1, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 0, row);
            tableMeta.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(val1) ? "-" : val1, Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), ForeColor = Color.FromArgb(15, 23, 42), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 1, row);
            tableMeta.Controls.Add(new Label { Text = label2, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 2, row);
            tableMeta.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(val2) ? "-" : val2, Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), ForeColor = Color.FromArgb(15, 23, 42), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 3, row);
        }

        string invDateStr = _invRow["InvoiceDate"]?.ToString() ?? "-";
        string issueTimeStr = _invRow["IssueTime"]?.ToString() ?? "-";
        string invNum = _invRow["InvoiceNumber"]?.ToString() ?? "-";
        string customId = _invRow["CustomizationId"]?.ToString() ?? "-";
        string scenario = _invRow["Scenario"]?.ToString() ?? "-";
        string invKind = _invRow["InvoiceKind"]?.ToString() ?? _invRow["InvoiceType"]?.ToString() ?? "-";
        string orderNo = _invRow["OrderNumber"]?.ToString() ?? "-";
        string orderDate = _invRow["OrderDate"]?.ToString() ?? "-";
        string relStore = _invRow["RelatedStore"]?.ToString() ?? "-";
        string cargoId = _invRow["CargoId"]?.ToString() ?? "-";
        string refNo = _invRow["ReferenceNo"]?.ToString() ?? "-";
        double grandTotal = Convert.ToDouble(_invRow["GrandTotal"]);

        AddMetaRow(0, "Fatura No / Belge No:", invNum, "Özelleştirme No:", customId);
        AddMetaRow(1, "Fatura Tarihi:", invDateStr, "Düzenleme Saati:", issueTimeStr);
        AddMetaRow(2, "Senaryo:", scenario, "Fatura Tipi:", invKind);
        AddMetaRow(3, "Sipariş No:", orderNo, "Sipariş Tarihi:", orderDate);
        AddMetaRow(4, "İlgili Kitabevi / Alıcı:", relStore, "Kargo ID / Sevk No:", cargoId);
        AddMetaRow(5, "Referans No (ETTN):", refNo, "Fatura Genel Toplamı:", $"{grandTotal:N2} ₺");

        metaContainer.Controls.Add(tableMeta);
        Controls.Add(metaContainer);

        // 3. Alt Butonlar ve Özet Çubuğu
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 8)
        };
        bottomPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
        };

        var btnOpenPdf = new Button
        {
            Text = "📄 Fatura PDF Belgesini Aç",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Size = new Size(210, 38),
            BackColor = Color.FromArgb(13, 148, 136),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Left
        };
        btnOpenPdf.FlatAppearance.BorderSize = 0;
        btnOpenPdf.Click += (s, e) =>
        {
            byte[]? pdfData = _invRow["PdfData"] == DBNull.Value ? null : (byte[]?)_invRow["PdfData"];
            string? pdfPath = _invRow["PdfPath"] == DBNull.Value ? null : _invRow["PdfPath"]?.ToString();
            InvoiceService.OpenPdfDataOrPath(pdfData, pdfPath, invNum);
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Size = new Size(100, 38),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Right
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClose.Click += (s, e) => Close();

        var lblItemCount = new Label
        {
            Text = $"Toplam {_itemsTable.Rows.Count} Kalem Ürün Bulunmaktadır.",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        bottomPanel.Controls.Add(lblItemCount);
        bottomPanel.Controls.Add(btnOpenPdf);
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);

        // 4. Orta Bölüm: "Bu Faturadaki Tüm Ürünler" Grid
        var gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15, 10, 15, 5),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var lblGridTitle = new Label
        {
            Text = "📦 Bu Faturada Yer Alan Tüm Kalemler ve Ürünler:",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 26
        };

        var gridItems = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = _itemsTable,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        };
        UITheme.ApplyGridStyle(gridItems);

        // Grid Sütun Düzenlemesi
        if (gridItems.Columns.Count > 0)
        {
            if (gridItems.Columns["Id"] is { } cId) cId.Visible = false;
            if (gridItems.Columns["Sıra No"] is { } cSira) { cSira.Width = 50; }
            if (gridItems.Columns["Barkod"] is { } cBar) { cBar.Width = 125; }
            if (gridItems.Columns["Ürün Kodu"] is { } cKod) { cKod.Width = 100; }
            if (gridItems.Columns["Ürün Adı"] is { } cAd) { cAd.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
            if (gridItems.Columns["Miktar"] is { } cMiktar) { cMiktar.Width = 65; cMiktar.DefaultCellStyle.Format = "N2"; }
            if (gridItems.Columns["Birim"] is { } cBirim) { cBirim.Width = 60; }
            if (gridItems.Columns["Birim Fiyat (₺)"] is { } cFiyat) { cFiyat.Width = 90; cFiyat.DefaultCellStyle.Format = "N2"; }
            if (gridItems.Columns["İskonto %"] is { } cIskP) { cIskP.Width = 60; cIskP.DefaultCellStyle.Format = "N0"; }
            if (gridItems.Columns["İskonto (₺)"] is { } cIskA) { cIskA.Width = 80; cIskA.DefaultCellStyle.Format = "N2"; }
            if (gridItems.Columns["KDV %"] is { } cKdvP) { cKdvP.Width = 55; cKdvP.DefaultCellStyle.Format = "N0"; }
            if (gridItems.Columns["KDV Tutarı"] is { } cKdvA) { cKdvA.Width = 75; cKdvA.DefaultCellStyle.Format = "N2"; }
            if (gridItems.Columns["Satır Toplamı (₺)"] is { } cTot) { cTot.Width = 95; cTot.DefaultCellStyle.Format = "N2"; }
        }

        gridPanel.Controls.Add(gridItems);
        gridPanel.Controls.Add(lblGridTitle);
        Controls.Add(gridPanel);

        // Z-Index düzeni
        gridPanel.BringToFront();
        bottomPanel.SendToBack();
        metaContainer.SendToBack();
        headerPanel.SendToBack();
    }
}
