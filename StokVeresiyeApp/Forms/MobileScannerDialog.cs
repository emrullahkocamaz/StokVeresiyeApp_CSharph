using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class MobileScannerDialog : Form
{
    private readonly PictureBox _picQr = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(240, 240), BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox _cmbIpAddresses = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly LinkLabel _lnkUrl = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), LinkColor = Color.FromArgb(20, 176, 186) };
    private readonly Label _lblStatus = new() { AutoSize = true, Font = UITheme.TitleFont, ForeColor = UITheme.Success };
    private readonly DataGridView _gridLogs = new();
    private readonly CheckBox _chkAutoSelectDesktop = new() { Text = "Telefonda okutulan ürünü masaüstü stok tablosunda otomatik seçili yap", Checked = true, AutoSize = true };
    private readonly DataTable _logTable = new();

    public MobileScannerDialog()
    {
        Text = "📱 Mobil Canlı Barkod Okuyucu - Telefon Kamerası ile Depo Asistanı";
        ClientSize = new Size(980, 650);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        // Servisi başlat
        if (!MobileScannerService.IsRunning)
        {
            MobileScannerService.Start();
        }

        BuildUI();
        LoadNetworkInfo();
        InitializeLogTable();

        Load += (s, e) => FormatLogColumns();

        // Olay dinleyicisi
        MobileScannerService.BarcodeScannedFromMobile += OnBarcodeScanned;
        FormClosed += (s, e) => MobileScannerService.BarcodeScannedFromMobile -= OnBarcodeScanned;
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new CardPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(20, 12, 20, 12) };
        var lblTitle = new Label
        {
            Text = "📱 Mobil Canlı Barkod Okuyucu (Wi-Fi & QR Bağlantı)",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };
        var lblDesc = new Label
        {
            Text = "Telefonunuza uygulama yüklemeden, tarayıcı ve kamera üzerinden reyonda/depoda hızlı barkod okutun; anlık stok, fiyat ve SKT görün.",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Fill
        };
        header.Controls.Add(lblDesc);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Ana Gövde
        var mainContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

        // Sol Alan: QR Kod ve Bağlantı Kartı
        var leftCard = new CardPanel { Dock = DockStyle.Left, Width = 380, Padding = new Padding(15) };
        var pnlQrCenter = new Panel { Height = 250, Dock = DockStyle.Top };
        _picQr.Location = new Point((leftCard.Width - 30 - 240) / 2, 5);
        pnlQrCenter.Controls.Add(_picQr);

        var pnlIp = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 5, 0, 5) };
        var lblIp = new Label { Text = "Wi-Fi IP:", AutoSize = true, Location = new Point(5, 8), ForeColor = UITheme.TextSecondary };
        _cmbIpAddresses.Location = new Point(70, 5);
        _cmbIpAddresses.SelectedIndexChanged += (s, e) => UpdateQrCode();
        pnlIp.Controls.Add(lblIp);
        pnlIp.Controls.Add(_cmbIpAddresses);

        var pnlUrl = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(5) };
        _lnkUrl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lnkUrl.Location = new Point(5, 10);
        _lnkUrl.LinkClicked += (s, e) => OpenInBrowser();
        pnlUrl.Controls.Add(_lnkUrl);

        var flowBtns = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight };
        var btnCopy = UITheme.CreateButton("📋 Linki Kopyala", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => CopyUrl(), 140, 32);
        var btnOpen = UITheme.CreateButton("🌐 Tarayıcıda Aç", Color.FromArgb(20, 176, 186), Color.White, (s, e) => OpenInBrowser(), 140, 32);

        flowBtns.Controls.Add(btnCopy);
        flowBtns.Controls.Add(btnOpen);

        var lblHelp = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "💡 Nasıl Kullanılır?\n" +
                   "1. Telefonunuzu bu bilgisayarla aynı Wi-Fi ağına bağlayın.\n" +
                   "2. Telefonunuzun kamerasını QR koda tutarak çıkan linke dokunun.\n" +
                   "3. Kamera izni verip reyondaki ürünün barkoduna doğrultun.\n" +
                   "4. 'BİP' sesiyle ürün detayları cep telefonunuzda açılacaktır."
        };
        // Dock sırası: son eklenen önce yerleşir -> görsel sıranın tersi ile ekle
        leftCard.Controls.Add(lblHelp);
        leftCard.Controls.Add(flowBtns);
        leftCard.Controls.Add(pnlUrl);
        leftCard.Controls.Add(pnlIp);
        leftCard.Controls.Add(pnlQrCenter);

        // Sağ Alan: Canlı Tarama Günlüğü (Staff / Reyon Hareketleri)
        var rightCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(15), Margin = new Padding(15, 0, 0, 0) };
        var pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 66 };
        var lblLogTitle = new Label { Text = "⚡ Canlı Mobil Tarama Kayıtları", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Location = new Point(0, 4), AutoSize = true };
        _lblStatus.Text = "🟢 Mobil Sunucu Dinleniyor (Port: " + MobileScannerService.Port + ")";
        _lblStatus.Font = UITheme.SmallFont;
        _lblStatus.Location = new Point(2, 36);
        pnlRightHeader.Controls.Add(lblLogTitle);
        pnlRightHeader.Controls.Add(_lblStatus);

        UITheme.ApplyGridStyle(_gridLogs);
        _gridLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _gridLogs.Dock = DockStyle.Fill;

        var pnlRightBottom = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5, 10, 5, 0) };
        pnlRightBottom.Controls.Add(_chkAutoSelectDesktop);

        // Fill önce, Bottom/Top sonra eklenir (son eklenen önce dock edilir)
        rightCard.Controls.Add(_gridLogs);
        rightCard.Controls.Add(pnlRightBottom);
        rightCard.Controls.Add(pnlRightHeader);

        mainContainer.Controls.Add(rightCard);
        mainContainer.Controls.Add(leftCard);
        Controls.Add(mainContainer);

        // Z-Order
        Controls.SetChildIndex(header, 0);
        Controls.SetChildIndex(mainContainer, 1);
        mainContainer.BringToFront();
    }

    private void LoadNetworkInfo()
    {
        _cmbIpAddresses.Items.Clear();
        var ips = MobileScannerService.GetLocalIpAddresses();
        foreach (var ip in ips)
        {
            _cmbIpAddresses.Items.Add(ip);
        }

        if (_cmbIpAddresses.Items.Count > 0)
        {
            // İlk harici IP'yi seç (127.0.0.1 değilse)
            int selIdx = 0;
            for (int i = 0; i < ips.Count; i++)
            {
                if (!ips[i].StartsWith("127."))
                {
                    selIdx = i;
                    break;
                }
            }
            _cmbIpAddresses.SelectedIndex = selIdx;
        }

        UpdateQrCode();
    }

    private void UpdateQrCode()
    {
        string selectedIp = _cmbIpAddresses.SelectedItem?.ToString() ?? "localhost";
        string url = MobileScannerService.BuildUrl(selectedIp);
        _lnkUrl.Text = url;

        try
        {
            var qrBmp = BarcodeRenderer.GenerateQrCode(url, 240, 240);
            _picQr.Image?.Dispose();
            _picQr.Image = qrBmp;
        }
        catch { }
    }

    private void InitializeLogTable()
    {
        if (_logTable.Columns.Count == 0)
        {
            _logTable.Columns.Add("Saat", typeof(string));
            _logTable.Columns.Add("Barkod", typeof(string));
            _logTable.Columns.Add("Ürün Adı", typeof(string));
            _logTable.Columns.Add("Kalan Stok", typeof(string));
            _logTable.Columns.Add("Fiyat (₺)", typeof(string));
            _logTable.Columns.Add("Cihaz", typeof(string));
        }

        _gridLogs.DataSource = _logTable;
        FormatLogColumns();

        // Geçmiş kayıtları yükle
        lock (MobileScannerService.RecentScans)
        {
            foreach (var item in MobileScannerService.RecentScans)
            {
                _logTable.Rows.Add(
                    item.Timestamp.ToString("HH:mm:ss"),
                    item.Barcode,
                    item.ProductName,
                    item.Stock.ToString("N0"),
                    item.Price.ToString("N2"),
                    item.DeviceInfo
                );
            }
        }
    }

    private void FormatLogColumns()
    {
        try
        {
            if (_gridLogs.Columns.Count == 0) return;

            if (_gridLogs.Columns["Saat"] is { } colSaat)
            {
                colSaat.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colSaat.Width = 75;
            }
            if (_gridLogs.Columns["Barkod"] is { } colBarkod)
            {
                colBarkod.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colBarkod.Width = 115;
            }
            if (_gridLogs.Columns["Ürün Adı"] is { } colUrun)
            {
                colUrun.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            if (_gridLogs.Columns["Kalan Stok"] is { } colStok)
            {
                colStok.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colStok.Width = 85;
            }
            if (_gridLogs.Columns["Fiyat (₺)"] is { } colFiyat)
            {
                colFiyat.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colFiyat.Width = 85;
            }
            if (_gridLogs.Columns["Cihaz"] is { } colCihaz)
            {
                colCihaz.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colCihaz.Width = 100;
            }
        }
        catch { }
    }

    private void OnBarcodeScanned(string barcode, Models.Product? prod)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnBarcodeScanned(barcode, prod)));
            return;
        }

        _logTable.Rows.InsertAt(_logTable.NewRow(), 0);
        var r = _logTable.Rows[0];
        r["Saat"] = DateTime.Now.ToString("HH:mm:ss");
        r["Barkod"] = barcode;
        r["Ürün Adı"] = prod?.Name ?? "(Bilinmeyen Ürün)";
        r["Kalan Stok"] = prod != null ? prod.CurrentStock.ToString("N0") : "-";
        r["Fiyat (₺)"] = prod != null ? prod.SalePrice.ToString("N2") : "-";
        r["Cihaz"] = "Mobil Kamera";

        if (_gridLogs.Rows.Count > 0)
        {
            _gridLogs.Rows[0].Selected = true;
        }
    }

    private void CopyUrl()
    {
        try
        {
            Clipboard.SetText(_lnkUrl.Text);
            MessageBox.Show("Mobil barkod linki panoya kopyalandı:\n" + _lnkUrl.Text, "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { }
    }

    private void OpenInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lnkUrl.Text,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tarayıcı açılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}
