using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class SplitPaymentDialog : BaseModernForm
{
    private readonly double _targetTotal;
    private readonly TextBox _txtCash = new() { Text = "0,00", Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly TextBox _txtCard = new() { Text = "0,00", Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly TextBox _txtTransfer = new() { Text = "0,00", Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly TextBox _txtCredit = new() { Text = "0,00", Font = new Font("Segoe UI", 11f, FontStyle.Bold) };

    private readonly Label _lblTarget = new() { Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };
    private readonly Label _lblEnteredTotal = new() { Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };
    private readonly Label _lblDifference = new() { Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };

    public double CashAmount { get; private set; }
    public double CardAmount { get; private set; }
    public double TransferAmount { get; private set; }
    public double CreditAmount { get; private set; }

    public SplitPaymentDialog(double targetTotal, bool allowCredit = true) 
        : base("💳 Parçalı / Çoklu Ödeme Dağıtımı", 580, 520)
    {
        _targetTotal = targetTotal;
        _lblTarget.Text = $"{_targetTotal:N2} ₺";

        // Varsayılan olarak nakit kutusuna toplamı ata
        _txtCash.Text = _targetTotal.ToString("N2");

        BuildInterface(allowCredit);
        CalculateDifference();
    }

    private void BuildInterface(bool allowCredit)
    {
        var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(16) };

        int top = 10;
        void AddRow(string label, Control ctl, int height = 34)
        {
            var lbl = new Label
            {
                Text = label,
                Left = 8,
                Top = top + 6,
                Width = 200,
                Font = UITheme.RegularFont,
                ForeColor = UITheme.TextSecondary
            };
            ctl.Left = 215;
            ctl.Top = top;
            ctl.Width = 240;
            ctl.Height = height;
            card.Controls.Add(lbl);
            card.Controls.Add(ctl);
            top += height + 10;
        }

        AddRow("🎯 Toplam Fiş Tutarı", _lblTarget, 28);
        AddRow("💵 Nakit Ödeme (₺)", _txtCash);
        AddRow("💳 Kredi Kartı Ödeme (₺)", _txtCard);
        AddRow("🏦 Havale / EFT (₺)", _txtTransfer);

        if (allowCredit)
        {
            AddRow("📝 Veresiye / Kalan Borç (₺)", _txtCredit);
        }
        else
        {
            _txtCredit.Enabled = false;
        }

        AddRow("📊 Dağıtılan Toplam", _lblEnteredTotal, 26);
        AddRow("⚖️ Kalan / Fark", _lblDifference, 26);

        // Hızlı Dağıtım Butonları
        var pnlQuick = new FlowLayoutPanel
        {
            Left = 8,
            Top = top + 4,
            Width = 460,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight
        };

        var btnAllCash = UITheme.CreateButton("Hepsi Nakit", UITheme.Primary, Color.White, (s, e) =>
        {
            _txtCash.Text = _targetTotal.ToString("N2");
            _txtCard.Text = "0,00";
            _txtTransfer.Text = "0,00";
            _txtCredit.Text = "0,00";
        }, 100, 30);

        var btnAllCard = UITheme.CreateButton("Hepsi Kart", UITheme.Secondary, Color.White, (s, e) =>
        {
            _txtCash.Text = "0,00";
            _txtCard.Text = _targetTotal.ToString("N2");
            _txtTransfer.Text = "0,00";
            _txtCredit.Text = "0,00";
        }, 100, 30);

        var btnHalf = UITheme.CreateButton("50% Nakit - 50% Kart", Color.FromArgb(13, 148, 136), Color.White, (s, e) =>
        {
            double half = Math.Round(_targetTotal / 2.0, 2);
            _txtCash.Text = half.ToString("N2");
            _txtCard.Text = (_targetTotal - half).ToString("N2");
            _txtTransfer.Text = "0,00";
            _txtCredit.Text = "0,00";
        }, 160, 30);

        pnlQuick.Controls.Add(btnAllCash);
        pnlQuick.Controls.Add(btnAllCard);
        pnlQuick.Controls.Add(btnHalf);
        card.Controls.Add(pnlQuick);

        container.Controls.Add(card);

        if (Controls.Find("bodyPanel", true).FirstOrDefault() is Panel bodyPanel)
        {
            bodyPanel.Controls.Clear();
            bodyPanel.Controls.Add(container);
        }
        else
        {
            Controls.Add(container);
            container.BringToFront();
        }

        _txtCash.TextChanged += (s, e) => CalculateDifference();
        _txtCard.TextChanged += (s, e) => CalculateDifference();
        _txtTransfer.TextChanged += (s, e) => CalculateDifference();
        _txtCredit.TextChanged += (s, e) => CalculateDifference();

        BtnSave.Text = "Ödeme Dağıtımını Onayla";
        BtnSave.Click += ConfirmClick;
    }

    private void CalculateDifference()
    {
        double cash = ParseNumber(_txtCash.Text);
        double card = ParseNumber(_txtCard.Text);
        double transfer = ParseNumber(_txtTransfer.Text);
        double credit = ParseNumber(_txtCredit.Text);

        double totalEntered = cash + card + transfer + credit;
        _lblEnteredTotal.Text = $"{totalEntered:N2} ₺";

        double diff = totalEntered - _targetTotal;
        if (Math.Abs(diff) < 0.01)
        {
            _lblDifference.Text = "✅ Dağıtım Tamam (0,00 ₺)";
            _lblDifference.ForeColor = UITheme.Success;
            BtnSave.Enabled = true;
        }
        else if (diff > 0)
        {
            _lblDifference.Text = $"⚠️ Fazla Girildi: +{diff:N2} ₺";
            _lblDifference.ForeColor = UITheme.Danger;
            BtnSave.Enabled = false;
        }
        else
        {
            _lblDifference.Text = $"⚠️ Eksik Kalan: {Math.Abs(diff):N2} ₺";
            _lblDifference.ForeColor = Color.FromArgb(234, 88, 12);
            BtnSave.Enabled = false;
        }
    }

    private void ConfirmClick(object? sender, EventArgs e)
    {
        double cash = ParseNumber(_txtCash.Text);
        double card = ParseNumber(_txtCard.Text);
        double transfer = ParseNumber(_txtTransfer.Text);
        double credit = ParseNumber(_txtCredit.Text);

        double totalEntered = cash + card + transfer + credit;
        if (Math.Abs(totalEntered - _targetTotal) > 0.01)
        {
            MessageBox.Show("Girilen ödeme tutarlarının toplamı fiş tutarına eşit olmalıdır.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        CashAmount = cash;
        CardAmount = card;
        TransferAmount = transfer;
        CreditAmount = credit;

        DialogResult = DialogResult.OK;
        Close();
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
