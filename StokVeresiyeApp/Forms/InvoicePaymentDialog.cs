using System.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class InvoicePaymentDialog : BaseModernForm
{
    private readonly long _invoiceId;
    private readonly double _grandTotal;
    private double _currentPaid;
    private double _remaining;
    private readonly string _invoiceNo;
    private readonly string _invoiceType;
    private readonly string _accountName;

    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly ComboBox _cmbMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtAmount = new() { Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly TextBox _txtNote = new() { PlaceholderText = "Ödeme açıklaması (İsteğe bağlı)..." };
    private readonly Label _lblRemainingPreview = new() { Font = UITheme.RegularFont, ForeColor = UITheme.Primary, AutoSize = true };
    private readonly DataGridView _gridHistory = new();

    public bool PaymentMade { get; private set; } = false;

    public InvoicePaymentDialog(long invoiceId, string invoiceNo, string invoiceType, string accountName, double grandTotal, double paidAmount)
        : base($"💳 Fatura Ödeme / Tahsilat - No: {invoiceNo}", 720, 680)
    {
        _invoiceId = invoiceId;
        _invoiceNo = invoiceNo;
        _invoiceType = invoiceType;
        _accountName = accountName;
        _grandTotal = grandTotal;
        _currentPaid = paidAmount;
        _remaining = Math.Max(0, grandTotal - paidAmount);

        _cmbMethod.Items.AddRange(new object[] { "Nakit", "Kredi Kartı", "Havale / EFT" });
        _cmbMethod.SelectedIndex = 0;

        // Üst Özet Bilgi Kartı
        var pnlInfo = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 15)
        };

        var flowInfo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2
        };
        flowInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        flowInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        flowInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        var lblAcc = new Label { Text = $"Cari: {_accountName}\nTür: {_invoiceType}", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Fill };
        var lblTot = new Label { Text = $"Fatura Toplamı:\n{_grandTotal:N2} ₺", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = UITheme.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter };
        var lblRem = new Label { Text = $"Ödenen: {_currentPaid:N2} ₺\nKALAN: {_remaining:N2} ₺", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = _remaining > 0 ? Color.FromArgb(220, 38, 38) : UITheme.Success, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight };

        flowInfo.Controls.Add(lblAcc, 0, 0);
        flowInfo.Controls.Add(lblTot, 1, 0);
        flowInfo.Controls.Add(lblRem, 2, 0);
        pnlInfo.Controls.Add(flowInfo);

        // Ödeme Giriş Alanı
        _txtAmount.Text = _remaining.ToString("N2");
        _txtAmount.TextChanged += (s, e) => UpdateRemainingPreview();

        var pnlAmountRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        _txtAmount.Width = 150;
        var btnPayAll = UITheme.CreateButton("Tamamını Doldur", UITheme.Primary, Color.White, (s, e) =>
        {
            _txtAmount.Text = _remaining.ToString("N2");
        }, 125, 30);
        btnPayAll.Margin = new Padding(8, 0, 0, 0);
        pnlAmountRow.Controls.Add(_txtAmount);
        pnlAmountRow.Controls.Add(btnPayAll);

        AddRow("Fatura Bilgisi", pnlInfo, 120);
        AddRow("Ödeme Tarihi", _dtpDate, 42);
        AddRow("Ödeme Yöntemi", _cmbMethod, 42);
        AddRow("Ödenecek Tutar (₺) (*)", pnlAmountRow, 42);
        AddRow("İşlem Sonrası Kalan", _lblRemainingPreview, 42);
        AddRow("Açıklama / Dekont Notu", _txtNote, 52);

        // Geçmiş Ödemeler Tablosu Kartı
        var pnlHistoryCard = new CardPanel
        {
            Height = 160,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0)
        };
        var lblHistTitle = new Label
        {
            Text = "📋 Bu Faturaya Ait Ödeme & Tahsilat Geçmişi:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 24
        };
        UITheme.ApplyGridStyle(_gridHistory);
        pnlHistoryCard.Controls.Add(_gridHistory);
        pnlHistoryCard.Controls.Add(lblHistTitle);
        AddRow("Ödeme Geçmişi", pnlHistoryCard, 180);

        UpdateRemainingPreview();
        LoadPaymentHistory();

        // Butonlar
        BtnSave.Text = "✅ Ödemeyi Kaydet & Düş";
        BtnSave.Width = 200;
        BtnSave.Click += (s, e) => OnSavePayment();

        BtnCancel.Text = "❌ Kapat";
        BtnCancel.Click += (s, e) => 
        { 
            DialogResult = PaymentMade ? DialogResult.OK : DialogResult.Cancel; 
            Close(); 
        };
    }

    private void UpdateRemainingPreview()
    {
        double val = ParseNumber(_txtAmount.Text);
        double afterRem = _remaining - val;
        if (val <= 0)
        {
            _lblRemainingPreview.Text = "Lütfen ödenecek bir tutar girin.";
            _lblRemainingPreview.ForeColor = UITheme.TextMuted;
        }
        else if (afterRem < -0.01)
        {
            _lblRemainingPreview.Text = $"⚠️ Tutar kalan borçtan ({_remaining:N2} ₺) fazla olamaz!";
            _lblRemainingPreview.ForeColor = Color.Firebrick;
        }
        else if (Math.Abs(afterRem) < 0.01)
        {
            _lblRemainingPreview.Text = "🎉 Fatura borcu TAMAMEN kapanacaktır.";
            _lblRemainingPreview.ForeColor = UITheme.Success;
        }
        else
        {
            _lblRemainingPreview.Text = $"Faturadan {val:N2} ₺ düşülecek. Faturada Kalan: {afterRem:N2} ₺";
            _lblRemainingPreview.ForeColor = UITheme.Primary;
        }
    }

    private void LoadPaymentHistory()
    {
        try
        {
            var dt = InvoiceService.GetInvoicePayments(_invoiceId);
            _gridHistory.DataSource = dt;
            if (_gridHistory.Columns["Id"] is { } colId)
            {
                colId.Visible = false;
            }
            if (_gridHistory.Columns["Ödenen Tutar"] is { } colAmount)
            {
                colAmount.DefaultCellStyle.Format = "N2";
                colAmount.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        catch { }
    }

    private void OnSavePayment()
    {
        double amt = ParseNumber(_txtAmount.Text);
        if (amt <= 0)
        {
            MessageBox.Show("Lütfen 0'dan büyük geçerli bir ödeme tutarı giriniz.", "Geçersiz Tutar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (amt > _remaining + 0.01)
        {
            MessageBox.Show($"Ödenecek tutar kalan tutardan ({_remaining:N2} ₺) fazla olamaz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string method = _cmbMethod.SelectedItem?.ToString() ?? "Nakit";
        string note = _txtNote.Text.Trim();

        var result = InvoiceService.AddInvoicePayment(_invoiceId, amt, method, note, _dtpDate.Value);
        if (!result.Success)
        {
            MessageBox.Show(result.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PaymentMade = true;
        MessageBox.Show(result.Message, "Ödeme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Durumu güncelle
        _currentPaid += amt;
        _remaining = result.NewRemaining;
        _txtAmount.Text = _remaining.ToString("N2");
        UpdateRemainingPreview();
        LoadPaymentHistory();

        if (_remaining <= 0.01)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
