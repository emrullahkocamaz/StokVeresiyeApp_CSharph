using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class AccountForm : BaseModernForm
{
    private readonly KryptonTextBox _txtName = new();
    private readonly KryptonComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtPhone = new();
    private readonly KryptonTextBox _txtEmail = new();
    private readonly KryptonTextBox _txtTaxOffice = new();
    private readonly KryptonTextBox _txtTaxNumber = new();
    private readonly KryptonComboBox _cmbPriceGroup = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtDiscount = new() { Text = "0" };
    private readonly KryptonTextBox _txtLimit = new() { Text = "0,00" };
    private readonly KryptonCheckBox _chkBlacklist = new() { Text = "⛔ Bu cariyi KARA LİSTEYE al (Riskli Müşteri - Veresiye uyarısı verir)", AutoSize = true };
    private readonly KryptonTextBox _txtAddress = new() { Multiline = true, Height = 60 };
    private readonly KryptonTextBox _txtDesc = new() { Multiline = true, Height = 50 };

    private readonly long _id = -1;

    public AccountForm(long? accountId = null) : base(accountId.HasValue && accountId.Value > 0 ? "Cari Kart Düzenle" : "Yeni Cari Tanımla", 640, 660)
    {
        _id = accountId ?? -1;

        _cmbType.Items.AddRange(new object[] { "Müşteri", "Tedarikçi" });
        _cmbType.SelectedIndex = 0;

        _cmbPriceGroup.Items.AddRange(new object[] { "Perakende", "Toptan", "Özel / Bayi" });
        _cmbPriceGroup.SelectedIndex = 0;

        AddRow("Cari / Firma Adı (*)", _txtName);
        AddRow("Cari Türü", _cmbType);
        AddRow("Fiyat Tarifesi / Grubu", _cmbPriceGroup);
        AddRow("Cariye Özel Sabit İskonto %", _txtDiscount);
        AddRow("Telefon No", _txtPhone);
        AddRow("E-Posta Adresi", _txtEmail);
        AddRow("Vergi Dairesi", _txtTaxOffice);

        // Vergi / TC Kimlik No ve GİB Otomatik Sorgulama Butonu
        var pnlTax = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        pnlTax.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlTax.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
        pnlTax.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var btnTaxLookup = UITheme.CreateKryptonButton("🔍 GİB Sorgula", Color.FromArgb(13, 148, 136), Color.White, async (s, e) =>
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.TaxLookup) && !curUser.HasPermission(UserPermissions.Accounts))
            {
                MessageBox.Show("GİB / VKN / TCKN cari bilgi sorgulama yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string taxNo = _txtTaxNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(taxNo))
            {
                MessageBox.Show("Lütfen önce 10 haneli Vergi Kimlik No (VKN) veya 11 haneli T.C. Kimlik No giriniz.", "Bilgi Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTaxNumber.Focus();
                return;
            }

            var res = await TaxLookupService.LookupAsync(taxNo);
            if (res.Success)
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text) || _txtName.Text == "Yeni Cari")
                {
                    _txtName.Text = res.Title;
                }
                if (!string.IsNullOrWhiteSpace(res.TaxOffice))
                {
                    _txtTaxOffice.Text = res.TaxOffice;
                }
                MessageBox.Show($"{res.Message}\n\nUnvan: {res.Title}\nVergi Dairesi: {res.TaxOffice}\nŞehir: {res.City}", "GİB Mükellef Bilgisi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(res.Message, "Doğrulama Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }, 125, 34);

        _txtTaxNumber.Dock = DockStyle.Fill;
        _txtTaxNumber.CueHint.CueHintText = "10 haneli VKN veya 11 haneli TCKN";
        pnlTax.Controls.Add(_txtTaxNumber, 0, 0);
        pnlTax.Controls.Add(btnTaxLookup, 1, 0);

        AddRow("Vergi / TC Kimlik No", pnlTax, 40);
        AddRow("Açık Hesap / Kredi Limiti (₺)", _txtLimit);
        AddRow("Kara Liste Durumu", _chkBlacklist);
        AddRow("Adres", _txtAddress, 70);
        AddRow("Özel Açıklama / Not", _txtDesc, 60);

        BtnSave.Click += SaveClick;

        if (_id > 0)
        {
            LoadData();
        }
    }

    private string _originalData = "";

    private void LoadData()
    {
        var q = Database.Query("SELECT Name, Type, Phone, Email, TaxOffice, TaxNumber, PriceGroup, DefaultDiscountPercent, BalanceLimit, Address, Description, IsBlacklisted FROM Accounts WHERE Id=$id", ("$id", _id));
        if (q.Rows.Count == 0) return;
        var r = q.Rows[0];
        _txtName.Text = r["Name"]?.ToString() ?? "";
        _cmbType.Text = r["Type"]?.ToString() ?? "Müşteri";
        _cmbPriceGroup.Text = r["PriceGroup"] != DBNull.Value ? r["PriceGroup"].ToString() ?? "Perakende" : "Perakende";
        _txtDiscount.Text = (r["DefaultDiscountPercent"] != DBNull.Value ? Convert.ToDouble(r["DefaultDiscountPercent"]) : 0).ToString("N2");
        _txtPhone.Text = r["Phone"]?.ToString() ?? "";
        _txtEmail.Text = r["Email"]?.ToString() ?? "";
        _txtTaxOffice.Text = r["TaxOffice"]?.ToString() ?? "";
        _txtTaxNumber.Text = r["TaxNumber"]?.ToString() ?? "";
        _txtLimit.Text = Convert.ToDouble(r["BalanceLimit"]).ToString("N2");
        _chkBlacklist.Checked = r["IsBlacklisted"] != DBNull.Value && Convert.ToBoolean(r["IsBlacklisted"]);
        _txtAddress.Text = r["Address"]?.ToString() ?? "";
        _txtDesc.Text = r["Description"]?.ToString() ?? "";

        _originalData = $"Ad: {_txtName.Text}, Tür: {_cmbType.Text}, FiyatGrubu: {_cmbPriceGroup.Text}, İskonto: {_txtDiscount.Text}%, Tel: {_txtPhone.Text}, E-Posta: {_txtEmail.Text}, Vergi: {_txtTaxOffice.Text}/{_txtTaxNumber.Text}, Limit: {_txtLimit.Text} ₺, KaraListe: {_chkBlacklist.Checked}, Adres: {_txtAddress.Text}";
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        string name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Cari / Firma Adı alanı zorunludur.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // Mükerrer Cari / Firma adı kontrolü
        var existingCount = Database.ExecuteScalar(
            _id < 0
                ? "SELECT COUNT(1) FROM Accounts WHERE Name = @n"
                : "SELECT COUNT(1) FROM Accounts WHERE Name = @n AND Id <> @id",
            ("@n", name),
            ("@id", _id)
        );

        if (Convert.ToInt32(existingCount) > 0)
        {
            MessageBox.Show(
                $"'{name}' cari/firma adı zaten kayıtlıdır.\nLütfen farklı bir cari adı giriniz.",
                "Mükerrer Cari Adı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            _txtName.Focus();
            _txtName.SelectAll();
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            string newDetails = $"Ad: {name}, Tür: {_cmbType.Text}, FiyatGrubu: {_cmbPriceGroup.Text}, İskonto: {_txtDiscount.Text}%, Tel: {_txtPhone.Text.Trim()}, E-Posta: {_txtEmail.Text.Trim()}, Vergi: {_txtTaxOffice.Text.Trim()}/{_txtTaxNumber.Text.Trim()}, Limit: {_txtLimit.Text} ₺, KaraListe: {_chkBlacklist.Checked}, Adres: {_txtAddress.Text.Trim()}";

            if (_id < 0)
            {
                var newIdScalar = Database.ExecuteScalar(@"
INSERT INTO Accounts(Name, Type, Phone, Email, TaxOffice, TaxNumber, PriceGroup, DefaultDiscountPercent, BalanceLimit, Address, Description, IsBlacklisted) 
VALUES($n, $t, $p, $e, $to, $tn, $pg, $dp, $bl, $addr, $d, $blk);
SELECT SCOPE_IDENTITY();",
                    ("$n", name),
                    ("$t", _cmbType.Text),
                    ("$p", _txtPhone.Text.Trim()),
                    ("$e", _txtEmail.Text.Trim()),
                    ("$to", _txtTaxOffice.Text.Trim()),
                    ("$tn", _txtTaxNumber.Text.Trim()),
                    ("$pg", _cmbPriceGroup.Text),
                    ("$dp", ParseNumber(_txtDiscount.Text)),
                    ("$bl", ParseNumber(_txtLimit.Text)),
                    ("$addr", _txtAddress.Text.Trim()),
                    ("$d", _txtDesc.Text.Trim()),
                    ("$blk", _chkBlacklist.Checked ? 1 : 0));

                long newId = newIdScalar != null && newIdScalar != DBNull.Value ? Convert.ToInt64(newIdScalar) : 0;
                Services.AuditLogService.Log("Cari", "Yeni Eklendi", newId, name, null, newDetails, "Yeni cari kartı tanımlandı");
            }
            else
            {
                Database.Execute(@"
UPDATE Accounts SET 
    Name=$n, 
    Type=$t, 
    Phone=$p, 
    Email=$e, 
    TaxOffice=$to, 
    TaxNumber=$tn, 
    PriceGroup=$pg,
    DefaultDiscountPercent=$dp,
    BalanceLimit=$bl, 
    Address=$addr, 
    Description=$d,
    IsBlacklisted=$blk
WHERE Id=$id",
                    ("$n", _txtName.Text.Trim()),
                    ("$t", _cmbType.Text),
                    ("$p", _txtPhone.Text.Trim()),
                    ("$e", _txtEmail.Text.Trim()),
                    ("$to", _txtTaxOffice.Text.Trim()),
                    ("$tn", _txtTaxNumber.Text.Trim()),
                    ("$pg", _cmbPriceGroup.Text),
                    ("$dp", ParseNumber(_txtDiscount.Text)),
                    ("$bl", ParseNumber(_txtLimit.Text)),
                    ("$addr", _txtAddress.Text.Trim()),
                    ("$d", _txtDesc.Text.Trim()),
                    ("$blk", _chkBlacklist.Checked ? 1 : 0),
                    ("$id", _id));

                Services.AuditLogService.Log("Cari", "Güncellendi", _id, _txtName.Text.Trim(), _originalData, newDetails, "Cari kart bilgileri güncellendi");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
