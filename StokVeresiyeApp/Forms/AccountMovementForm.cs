using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class AccountMovementForm : BaseModernForm
{
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblBlacklistWarning = new() 
    { 
        Text = "⚠️ DİKKAT: Bu cari KARA LİSTEYE alınmıştır!", 
        ForeColor = Color.Firebrick, 
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Visible = false,
        AutoSize = true
    };
    private readonly ComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtDocNo = new();
    private readonly TextBox _txtAmount = new() { Text = "0,00" };
    private readonly ComboBox _cmbMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbCashBank = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _cmbNote = new() { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly CheckBox _chkHasDueDate = new() { Text = "Vade / Ödeme Sözü Belirle", AutoSize = true, Font = UITheme.RegularFont };
    private readonly DateTimePicker _dtpDueDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(7), Enabled = false, Width = 130 };

    public AccountMovementForm(long? defaultAccountId = null, string? defaultType = null) : base("Cari Finansal Hareket Ekle", 620, 560)
    {
        // Cariler
        var dtAccounts = Database.Query(@"
SELECT 
    Id, 
    Name + ' (' + Type + ')' + CASE WHEN COALESCE(IsBlacklisted, 0) = 1 THEN ' [⛔ KARA LİSTE]' ELSE '' END AS Display,
    COALESCE(IsBlacklisted, 0) AS IsBlacklisted
FROM Accounts 
WHERE IsActive=1 
ORDER BY Name");
        _cmbAccount.DataSource = dtAccounts;
        _cmbAccount.DisplayMember = "Display";
        _cmbAccount.ValueMember = "Id";

        _cmbAccount.SelectedIndexChanged += (s, e) => UpdateBlacklistWarning();

        if (defaultAccountId.HasValue && defaultAccountId.Value > 0)
        {
            _cmbAccount.SelectedValue = defaultAccountId.Value;
        }

        // İşlem Türü
        _cmbType.Items.AddRange(new object[] { "Tahsilat", "Ödeme", "Satış", "Alış" });
        _cmbType.SelectedItem = !string.IsNullOrEmpty(defaultType) ? defaultType : "Tahsilat";

        // Ödeme Yöntemleri
        _cmbMethod.Items.AddRange(new object[] { "Nakit", "Kredi Kartı", "Havale / EFT", "Çek / Senet", "Açık Hesap (Veresiye)" });
        _cmbMethod.SelectedIndex = 0;

        // Kasa/Banka
        _cmbCashBank.Items.AddRange(new object[] { "Merkez Kasa", "Banka Hesabı", "POS Hesabı", "Şube Kasası" });
        _cmbCashBank.Text = "Merkez Kasa";

        // Vade paneli
        var pnlDue = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        _chkHasDueDate.CheckedChanged += (s, e) => _dtpDueDate.Enabled = _chkHasDueDate.Checked;
        pnlDue.Controls.Add(_chkHasDueDate);
        pnlDue.Controls.Add(_dtpDueDate);

        // Akıllı Açıklama Hafızası
        LoadFrequentDescriptions();

        AddRow("İşlem Tarihi", _dtpDate);
        AddRow("Cari Firma / Müşteri (*)", _cmbAccount);
        AddRow("Güvenlik Durumu", _lblBlacklistWarning);
        AddRow("İşlem Türü (*)", _cmbType);
        AddRow("Fatura / Belge / Makbuz No", _txtDocNo);
        AddRow("İşlem Tutarı (₺) (*)", _txtAmount);
        AddRow("Ödeme Yöntemi", _cmbMethod);
        AddRow("Vade / Söz Tarihi", pnlDue);
        AddRow("Kasa / Banka Hesabı", _cmbCashBank);
        AddRow("Açıklama / Akıllı Not", _cmbNote);

        BtnSave.Click += SaveClick;

        UpdateBlacklistWarning();
    }

    private void LoadFrequentDescriptions()
    {
        try
        {
            var notes = TransactionService.GetFrequentDescriptions();
            _cmbNote.Items.Clear();
            foreach (var note in notes)
            {
                _cmbNote.Items.Add(note);
            }
        }
        catch { }
    }

    private bool IsSelectedAccountBlacklisted()
    {
        if (_cmbAccount.SelectedItem is System.Data.DataRowView rowView)
        {
            return Convert.ToInt32(rowView["IsBlacklisted"]) == 1;
        }
        return false;
    }

    private void UpdateBlacklistWarning()
    {
        bool isBlacklisted = IsSelectedAccountBlacklisted();
        _lblBlacklistWarning.Visible = isBlacklisted;
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        if (_cmbAccount.SelectedValue == null || ParseNumber(_txtAmount.Text) <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir cari ve pozitif bir işlem tutarı giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // Kara Liste Güvenlik Kontrolü
        if (IsSelectedAccountBlacklisted())
        {
            string txType = _cmbType.Text;
            if (txType == "Satış" || _cmbMethod.Text == "Açık Hesap (Veresiye)")
            {
                var ask = MessageBox.Show(
                    "⚠️ DİKKAT: Seçili cari [KARA LİSTE]'de kayıtlı bir müşteridir!\n\nBu müşteriye Veresiye / Borç işlemi yapmak istediğinize emin misiniz?",
                    "Kara Liste Güvenlik Uyarısı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2
                );

                if (ask != DialogResult.Yes)
                {
                    DialogResult = DialogResult.None;
                    return;
                }
            }
        }

        try
        {
            string? dueDateStr = _chkHasDueDate.Checked ? _dtpDueDate.Value.ToString("yyyy-MM-dd") : null;

            Database.Execute(@"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note, DueDate) 
VALUES($d, $a, $t, $doc, $amt, $m, $cb, $n, $due)",
                ("$d", _dtpDate.Value.ToString("yyyy-MM-dd")),
                ("$a", _cmbAccount.SelectedValue),
                ("$t", _cmbType.Text),
                ("$doc", string.IsNullOrWhiteSpace(_txtDocNo.Text) ? null : _txtDocNo.Text.Trim()),
                ("$amt", ParseNumber(_txtAmount.Text)),
                ("$m", _cmbMethod.Text),
                ("$cb", string.IsNullOrWhiteSpace(_cmbCashBank.Text) ? "Merkez Kasa" : _cmbCashBank.Text.Trim()),
                ("$n", string.IsNullOrWhiteSpace(_cmbNote.Text) ? null : _cmbNote.Text.Trim()),
                ("$due", dueDateStr != null ? dueDateStr : (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
