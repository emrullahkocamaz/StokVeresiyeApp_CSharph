using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class DailyCashClosingDialog : BaseModernForm
{
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly TextBox _txtOpeningCash = new() { Text = "0,00" };
    private readonly TextBox _txtCashSales = new() { ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249) };
    private readonly TextBox _txtCardSales = new() { ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249) };
    private readonly TextBox _txtTransferSales = new() { ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249) };
    private readonly TextBox _txtCashExpenses = new() { ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249) };
    private readonly Label _lblExpectedCash = new() { Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };
    private readonly TextBox _txtCountedCash = new() { Text = "0,00", Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly Label _lblDifference = new() { Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };
    private readonly TextBox _txtNotes = new() { Multiline = true, Height = 48, PlaceholderText = "Gün sonu notları veya sayım açıklaması..." };

    private readonly RichTextBox _txtZReportPreview = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Font = new Font("Consolas", 9.5f),
        BackColor = Color.FromArgb(248, 250, 252),
        ForeColor = Color.FromArgb(15, 23, 42)
    };

    private readonly DataGridView _gridHistory = new();
    private double _expectedCashVal = 0;

    public DailyCashClosingDialog() : base("🔒 Günlük Kasa Gün Sonu & Z Raporu Kapatma", 920, 680)
    {
        BuildInterface();
        CalculateDaySummary();
        RefreshHistoryGrid();
    }

    private void BuildInterface()
    {
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 420,
            SplitterWidth = 8,
            BackColor = UITheme.BorderColor
        };

        // Sol Panel: Giriş ve Hesaplama Alanları
        var leftCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        int top = 8;
        void AddRow(string label, Control ctl, int height = 30)
        {
            var lbl = new Label
            {
                Text = label,
                Left = 4,
                Top = top + 4,
                Width = 175,
                Font = UITheme.RegularFont,
                ForeColor = UITheme.TextSecondary
            };
            ctl.Left = 185;
            ctl.Top = top;
            ctl.Width = 205;
            ctl.Height = height;
            leftScroll.Controls.Add(lbl);
            leftScroll.Controls.Add(ctl);
            top += height + 8;
        }

        AddRow("Kapanış Tarihi", _dtpDate);
        AddRow("Sabah Açılış Kasası (₺)", _txtOpeningCash);
        AddRow("Nakit Satışlar / Giriş (₺)", _txtCashSales);
        AddRow("Kredi Kartı Satışları (₺)", _txtCardSales);
        AddRow("Havale / EFT Satışları (₺)", _txtTransferSales);
        AddRow("Nakit Masraflar / Çıkış (₺)", _txtCashExpenses);
        AddRow("Sistemde Olması Gereken", _lblExpectedCash);
        AddRow("Fiziki Sayılan Nakit (₺)", _txtCountedCash);
        AddRow("Kasa Farkı", _lblDifference);
        AddRow("Açıklama / Not", _txtNotes, 48);

        _dtpDate.ValueChanged += (s, e) => CalculateDaySummary();
        _txtOpeningCash.TextChanged += (s, e) => RecalculateBalance();
        _txtCountedCash.TextChanged += (s, e) => RecalculateBalance();

        leftCard.Controls.Add(leftScroll);
        mainSplit.Panel1.Controls.Add(leftCard);

        // Sağ Panel: Z Raporu Canlı Önizleme ve Geçmiş Kapanışlar
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = UITheme.RegularFont };
        var tabPreview = new TabPage("📄 Canlı Z Raporu Önizleme") { Padding = new Padding(8) };
        tabPreview.Controls.Add(_txtZReportPreview);

        var tabHistory = new TabPage("📜 Geçmiş Z Raporları") { Padding = new Padding(8) };
        UITheme.ApplyGridStyle(_gridHistory);
        tabHistory.Controls.Add(_gridHistory);

        tabs.TabPages.Add(tabPreview);
        tabs.TabPages.Add(tabHistory);
        mainSplit.Panel2.Controls.Add(tabs);

        // Alt Butonlar
        BtnSave.Text = "🔒 Gün Sonunu Onayla & Kapat";
        BtnSave.BackColor = UITheme.Primary;
        BtnSave.Click += SaveClosingClick;

        var btnPrint = UITheme.CreateButton("🖨️ Z Raporunu Kopyala", Color.FromArgb(13, 148, 136), Color.White, (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtZReportPreview.Text))
            {
                Clipboard.SetText(_txtZReportPreview.Text);
                MessageBox.Show("Z Raporu metni panoya kopyalandı! Termal yazıcıya gönderebilir veya yapıştırabilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }, 180, 36);

        // ActionPanel BaseModernForm'da altta yer alır
        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            btnPrint.Location = new Point(16, 12);
            actionPanel.Controls.Add(btnPrint);
        }

        // Base container'a mainSplit ekle
        if (Controls.Find("bodyPanel", true).FirstOrDefault() is Panel bodyPanel)
        {
            bodyPanel.Controls.Clear();
            bodyPanel.Controls.Add(mainSplit);
        }
        else
        {
            Controls.Add(mainSplit);
            mainSplit.BringToFront();
        }
    }

    private void CalculateDaySummary()
    {
        try
        {
            var stats = DailyRegisterService.GetDailyStats(_dtpDate.Value);
            _txtOpeningCash.Text = stats.OpeningCash.ToString("N2");
            _txtCashSales.Text = stats.CashSales.ToString("N2");
            _txtCardSales.Text = stats.CardSales.ToString("N2");
            _txtTransferSales.Text = stats.TransferSales.ToString("N2");
            _txtCashExpenses.Text = stats.CashExpenses.ToString("N2");

            RecalculateBalance();
        }
        catch { }
    }

    private void RecalculateBalance()
    {
        double opening = ParseNumber(_txtOpeningCash.Text);
        double cashSales = ParseNumber(_txtCashSales.Text);
        double cashExpenses = ParseNumber(_txtCashExpenses.Text);
        double cardSales = ParseNumber(_txtCardSales.Text);
        double transferSales = ParseNumber(_txtTransferSales.Text);

        _expectedCashVal = opening + cashSales - cashExpenses;
        _lblExpectedCash.Text = $"{_expectedCashVal:N2} ₺";

        double counted = ParseNumber(_txtCountedCash.Text);
        double diff = counted - _expectedCashVal;

        if (Math.Abs(diff) < 0.01)
        {
            _lblDifference.Text = "✅ Kasa Tam (0,00 ₺)";
            _lblDifference.ForeColor = UITheme.Success;
        }
        else if (diff > 0)
        {
            _lblDifference.Text = $"🟢 Kasa Fazlası: +{diff:N2} ₺";
            _lblDifference.ForeColor = Color.FromArgb(16, 185, 129);
        }
        else
        {
            _lblDifference.Text = $"🔴 Kasa Eksiği: {diff:N2} ₺";
            _lblDifference.ForeColor = UITheme.Danger;
        }

        var dummyClosing = new DailyRegisterClosing
        {
            ClosingDate = _dtpDate.Value.ToString("yyyy-MM-dd"),
            ClosedBy = UserService.CurrentUser?.FullName ?? "Admin",
            OpeningCash = opening,
            CashSales = cashSales,
            CardSales = cardSales,
            TransferSales = transferSales,
            CashExpenses = cashExpenses,
            ExpectedCash = _expectedCashVal,
            CountedCash = counted,
            DifferenceCash = diff,
            Notes = _txtNotes.Text.Trim(),
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _txtZReportPreview.Text = DailyRegisterService.GenerateZReportText(dummyClosing);
    }

    private void SaveClosingClick(object? sender, EventArgs e)
    {
        double counted = ParseNumber(_txtCountedCash.Text);
        double diff = counted - _expectedCashVal;

        string warning = Math.Abs(diff) > 0.01
            ? $"\n\n⚠️ DİKKAT: Sayılan kasa ile sistem arasında {diff:N2} ₺ fark tespit edildi!"
            : "";

        var confirm = MessageBox.Show(
            $"{_dtpDate.Value:dd.MM.yyyy} tarihli Kasa Gün Sonunu kapatmak ve Z Raporunu arşivlemek istiyor musunuz?{warning}",
            "Gün Sonu Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirm != DialogResult.Yes)
        {
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            var closing = new DailyRegisterClosing
            {
                ClosingDate = _dtpDate.Value.ToString("yyyy-MM-dd"),
                ClosedBy = UserService.CurrentUser?.FullName ?? "Admin",
                OpeningCash = ParseNumber(_txtOpeningCash.Text),
                CashSales = ParseNumber(_txtCashSales.Text),
                CardSales = ParseNumber(_txtCardSales.Text),
                TransferSales = ParseNumber(_txtTransferSales.Text),
                CashExpenses = ParseNumber(_txtCashExpenses.Text),
                ExpectedCash = _expectedCashVal,
                CountedCash = counted,
                DifferenceCash = diff,
                Notes = _txtNotes.Text.Trim(),
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            DailyRegisterService.SaveClosing(closing);

            // Otomatik Bulut / Harici Disk Veritabanı Yedeği
            var curUser = UserService.CurrentUser;
            if (curUser == null || curUser.HasPermission(UserPermissions.CloudBackup) || curUser.IsSuperUser)
            {
                var backupRes = CloudBackupService.ExecuteBackup(silent: true);
                if (backupRes.Success)
                {
                    MessageBox.Show($"Kasa Gün Sonu başarıyla kapatıldı ve Z Raporu arşivlendi!\n\n☁️ Otomatik Bulut/Disk Veritabanı Yedeği Alındı:\n{backupRes.BackupFilePath}", "Gün Sonu & Yedekleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Kasa Gün Sonu başarıyla kapatıldı ve Z Raporu kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("Kasa Gün Sonu başarıyla kapatıldı ve Z Raporu kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            RefreshHistoryGrid();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Gün sonu kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }

    private void RefreshHistoryGrid()
    {
        try
        {
            _gridHistory.DataSource = DailyRegisterService.GetClosingsHistory();
        }
        catch { }
    }

    private static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string clean = text.Replace("₺", "").Replace("%", "").Trim();
        if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double val)) return val;
        if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val)) return val;
        return 0;
    }
}
