using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class CustomerDisplayForm : Form
{
    private static CustomerDisplayForm? _instance;
    public static CustomerDisplayForm? Instance => _instance;

    private readonly Label _lblProductName = new();
    private readonly Label _lblQtyPrice = new();
    private readonly Label _lblTotal = new();
    private readonly Label _lblFooter = new();

    public CustomerDisplayForm()
    {
        Text = "BİLENSİS - Müşteri Bilgi Ekranı (Customer Display)";
        Size = new Size(860, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 23, 42); // Koyu Lacivert Kurumsal Arka Plan
        Icon = AppResources.AppIcon;

        BuildInterface();

        FormClosed += (s, e) => _instance = null;
    }

    public static void ShowOrToggle()
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new CustomerDisplayForm();
            _instance.PositionToTargetScreen();
            _instance.Show();
        }
        else
        {
            _instance.BringToFront();
            _instance.Activate();
        }
    }

    private void PositionToTargetScreen()
    {
        // 2. Ekran varsa tam ekran aç, yoksa pencere olarak aç
        if (Screen.AllScreens.Length > 1)
        {
            var secondaryScreen = Screen.AllScreens[1];
            StartPosition = FormStartPosition.Manual;
            Location = secondaryScreen.Bounds.Location;
            Size = secondaryScreen.Bounds.Size;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
        }
    }

    private void BuildInterface()
    {
        Controls.Clear();

        var rootTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Color.Transparent
        };
        rootTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));  // 0: Logo & Başlık
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));   // 1: Ürün Adı & Detay
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));   // 2: Dev Toplam Tutar Kartı
        rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));  // 3: Alt Bilgi Şeridi

        // 1. Üst Başlık & Logo
        var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var picLogo = new PictureBox
        {
            Size = new Size(52, 52),
            Location = new Point(0, 8),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = RibbonIconFactory.CreateIcon("bilensis_logo", 52)
        };
        var lblBrand = new Label
        {
            Text = "BİLENSİS",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(64, 6),
            Size = new Size(240, 36),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var lblSub = new Label
        {
            Text = "Müşteri Bilgi & Kasa Ekranı",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(147, 197, 253),
            Location = new Point(66, 42),
            Size = new Size(240, 22),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var lblClock = new Label
        {
            Dock = DockStyle.Right,
            Width = 200,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            TextAlign = ContentAlignment.MiddleRight
        };
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) => lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
        timer.Start();
        lblClock.Text = DateTime.Now.ToString("HH:mm:ss");

        pnlHeader.Controls.Add(picLogo);
        pnlHeader.Controls.Add(lblBrand);
        pnlHeader.Controls.Add(lblSub);
        pnlHeader.Controls.Add(lblClock);
        rootTable.Controls.Add(pnlHeader, 0, 0);

        // 2. Ürün Bilgisi Kartı
        var pnlProductCard = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 10),
            Padding = new Padding(24)
        };
        pnlProductCard.Paint += (s, e) =>
        {
            using var brush = new LinearGradientBrush(
                pnlProductCard.ClientRectangle,
                Color.FromArgb(30, 41, 59),
                Color.FromArgb(51, 65, 85),
                LinearGradientMode.Vertical
            );
            e.Graphics.FillRectangle(brush, pnlProductCard.ClientRectangle);
            using var pen = new Pen(Color.FromArgb(71, 85, 105), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlProductCard.Width - 1, pnlProductCard.Height - 1);
        };

        _lblProductName.Text = "Kasa Hazır • Hoş Geldiniz";
        _lblProductName.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
        _lblProductName.ForeColor = Color.White;
        _lblProductName.Dock = DockStyle.Top;
        _lblProductName.Height = 70;
        _lblProductName.TextAlign = ContentAlignment.MiddleCenter;

        _lblQtyPrice.Text = "Ürün okutulduğunda detaylar burada görünecektir.";
        _lblQtyPrice.Font = new Font("Segoe UI", 16f, FontStyle.Regular);
        _lblQtyPrice.ForeColor = Color.FromArgb(203, 213, 225);
        _lblQtyPrice.Dock = DockStyle.Fill;
        _lblQtyPrice.TextAlign = ContentAlignment.MiddleCenter;

        pnlProductCard.Controls.Add(_lblQtyPrice);
        pnlProductCard.Controls.Add(_lblProductName);
        rootTable.Controls.Add(pnlProductCard, 0, 1);

        // 3. Dev Toplam Tutar Kartı (Canlı Yeşil Vurgu)
        var pnlTotalCard = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 10),
            Padding = new Padding(20)
        };
        pnlTotalCard.Paint += (s, e) =>
        {
            using var brush = new LinearGradientBrush(
                pnlTotalCard.ClientRectangle,
                Color.FromArgb(6, 78, 59),
                Color.FromArgb(4, 120, 87),
                LinearGradientMode.Horizontal
            );
            e.Graphics.FillRectangle(brush, pnlTotalCard.ClientRectangle);
            using var pen = new Pen(Color.FromArgb(52, 211, 153), 2.5f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlTotalCard.Width - 1, pnlTotalCard.Height - 1);
        };

        var lblTotalTitle = new Label
        {
            Text = "ÖDENECEK TOPLAM TUTAR",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 250, 229),
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblTotal.Text = "0,00 ₺";
        _lblTotal.Font = new Font("Segoe UI", 48f, FontStyle.Bold);
        _lblTotal.ForeColor = Color.White;
        _lblTotal.Dock = DockStyle.Fill;
        _lblTotal.TextAlign = ContentAlignment.MiddleCenter;

        pnlTotalCard.Controls.Add(_lblTotal);
        pnlTotalCard.Controls.Add(lblTotalTitle);
        rootTable.Controls.Add(pnlTotalCard, 0, 2);

        // 4. Alt Teşekkür Şeridi
        _lblFooter.Text = "✨ Bizi Tercih Ettiğiniz İçin Teşekkür Ederiz • İyi Günler Dileriz";
        _lblFooter.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblFooter.ForeColor = Color.FromArgb(148, 163, 184);
        _lblFooter.Dock = DockStyle.Fill;
        _lblFooter.TextAlign = ContentAlignment.MiddleCenter;
        rootTable.Controls.Add(_lblFooter, 0, 3);

        Controls.Add(rootTable);
    }

    public static void UpdateCart(string productName, double qty, double unitPrice, double totalAmount)
    {
        if (_instance == null || _instance.IsDisposed) return;

        try
        {
            _instance.Invoke(() =>
            {
                _instance._lblProductName.Text = string.IsNullOrWhiteSpace(productName) ? "Kasa Hazır" : productName;
                _instance._lblQtyPrice.Text = qty > 0 ? $"{qty:N2} Adet / Kg  ×  {unitPrice:N2} ₺" : "İşlem Bekleniyor...";
                _instance._lblTotal.Text = $"{totalAmount:N2} ₺";
            });
        }
        catch { }
    }

    public static void ResetCart()
    {
        if (_instance == null || _instance.IsDisposed) return;
        try
        {
            _instance.Invoke(() =>
            {
                _instance._lblProductName.Text = "Kasa Hazır • Hoş Geldiniz";
                _instance._lblQtyPrice.Text = "Yeni alışveriş için ürün bekleniyor...";
                _instance._lblTotal.Text = "0,00 ₺";
            });
        }
        catch { }
    }
}
