using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ExpenseManageDialog : BaseModernForm
{
    private readonly DateTimePicker _dtpPeriodStart = new() { Format = DateTimePickerFormat.Short, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) };
    private readonly DateTimePicker _dtpPeriodEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly ComboBox _cmbPeriodQuick = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // Yeni Gider Giriş Kontrolleri
    private readonly DateTimePicker _dtpExpenseDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly ComboBox _cmbExpenseCat = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _txtExpenseAmount = new() { Text = "0,00" };
    private readonly ComboBox _cmbExpenseMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtExpenseNote = new();

    // Özet Göstergeleri
    private readonly Label _lblRevenue = new();
    private readonly Label _lblCogs = new();
    private readonly Label _lblGrossProfit = new();
    private readonly Label _lblTotalExpenses = new();
    private readonly Label _lblNetProfit = new();

    private readonly DataGridView _gridExpenses = new();

    public ExpenseManageDialog() : base("📉 Net Kâr / Zarar & Dükkan Gider Yönetimi", 1120, 800)
    {
        // 1. Dönem Seçimi Paneli
        _cmbPeriodQuick.Items.AddRange(new object[] { "Bu Ay", "Geçen Ay", "Son 30 Gün", "Son 3 Ay", "Bu Yıl", "Tüm Zamanlar" });
        _cmbPeriodQuick.SelectedIndex = 0;

        var pnlPeriod = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0) };
        pnlPeriod.Controls.Add(_cmbPeriodQuick);
        pnlPeriod.Controls.Add(new Label { Text = " Başlangıç: ", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        pnlPeriod.Controls.Add(_dtpPeriodStart);
        pnlPeriod.Controls.Add(new Label { Text = " Bitiş: ", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        pnlPeriod.Controls.Add(_dtpPeriodEnd);

        var btnRefreshPeriod = UITheme.CreateButton("🔄 Hesapla", Color.FromArgb(79, 70, 229), Color.White, (s, e) => RefreshAll(), 100, 32);
        btnRefreshPeriod.Margin = new Padding(10, 0, 0, 0);
        pnlPeriod.Controls.Add(btnRefreshPeriod);

        AddRow("Raporlama Dönemi", pnlPeriod, 42);

        // 2. Finansal Özet Dashboard Kartları
        var pnlDashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Height = 72,
            Margin = new Padding(0)
        };
        for (int i = 0; i < 5; i++)
            pnlDashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        pnlDashboard.Controls.Add(CreateStatCard("1. Satış Hasılatı", _lblRevenue, Color.FromArgb(30, 41, 59)), 0, 0);
        pnlDashboard.Controls.Add(CreateStatCard("2. Satılan Mal Maliyeti", _lblCogs, Color.FromArgb(100, 116, 139)), 1, 0);
        pnlDashboard.Controls.Add(CreateStatCard("3. Brüt Kâr", _lblGrossProfit, Color.FromArgb(2, 132, 199)), 2, 0);
        pnlDashboard.Controls.Add(CreateStatCard("4. Dükkan Giderleri", _lblTotalExpenses, Color.FromArgb(220, 38, 38)), 3, 0);
        pnlDashboard.Controls.Add(CreateStatCard("⭐ NET KÂR / ZARAR", _lblNetProfit, Color.FromArgb(16, 185, 129)), 4, 0);

        AddRow("Finansal Göstergeler", pnlDashboard, 78);

        // 3. Hızlı Gider Ekleme Bölümü
        _cmbExpenseCat.Items.AddRange(new object[] { 
            "Dükkan Kirası", 
            "Elektrik Faturası", 
            "Su Faturası", 
            "Doğalgaz / Isınma", 
            "Personel / Maaş / Avans", 
            "Muhasebe & Mali Müşavir", 
            "Yol & Nakliye & Kargo", 
            "Yemek & Çay Masrafı", 
            "Temizlik & Ambalaj (Poşet vb.)", 
            "Tadilat & Bakım-Onarım", 
            "Vergi & SGK Ödemeleri", 
            "Diğer Genel Giderler" 
        });
        _cmbExpenseCat.SelectedIndex = 0;

        _cmbExpenseMethod.Items.AddRange(new object[] { "Nakit (Kasa)", "Kredi Kartı", "Banka / Havale", "Şahsi Cep / Ortak" });
        _cmbExpenseMethod.SelectedIndex = 0;

        var pnlAddExpense = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Height = 40,
            Margin = new Padding(0)
        };
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Tarih
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Kategori
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Tutar
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Yöntem
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));  // Not
        pnlAddExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Buton

        _dtpExpenseDate.Dock = DockStyle.Fill;
        _cmbExpenseCat.Dock = DockStyle.Fill;
        _txtExpenseAmount.Dock = DockStyle.Fill;
        _cmbExpenseMethod.Dock = DockStyle.Fill;
        _txtExpenseNote.Dock = DockStyle.Fill;
        _txtExpenseNote.PlaceholderText = "Açıklama (örn: Ocak Ayı Kirası)...";

        var btnAddExpense = UITheme.CreateButton("➕ Masraf Ekle", Color.FromArgb(16, 185, 129), Color.White, (s, e) => SaveExpenseClick(), 105, 32);
        btnAddExpense.Dock = DockStyle.Fill;

        pnlAddExpense.Controls.Add(_dtpExpenseDate, 0, 0);
        pnlAddExpense.Controls.Add(_cmbExpenseCat, 1, 0);
        pnlAddExpense.Controls.Add(_txtExpenseAmount, 2, 0);
        pnlAddExpense.Controls.Add(_cmbExpenseMethod, 3, 0);
        pnlAddExpense.Controls.Add(_txtExpenseNote, 4, 0);
        pnlAddExpense.Controls.Add(btnAddExpense, 5, 0);

        AddRow("Yeni Gider Ekle", pnlAddExpense, 44);

        // 4. Masraflar Listesi Grid
        SetupGrid();
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Height = 340, Padding = new Padding(0, 6, 0, 0) };
        pnlGrid.Controls.Add(_gridExpenses);
        AddRow("Dönem Masrafları", pnlGrid, 350);

        BtnSave.Visible = false;
        BtnCancel.Text = "Kapat";

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            var btnDelete = UITheme.CreateButton("🗑️ Seçili Gideri Sil", Color.FromArgb(239, 68, 68), Color.White, (s, e) => DeleteExpenseClick(), 160, 36);
            btnDelete.Dock = DockStyle.Left;
            actionPanel.Controls.Add(btnDelete);
        }

        _cmbPeriodQuick.SelectedIndexChanged += QuickPeriodChanged;
        RefreshAll();
    }

    private Control CreateStatCard(string title, Label lblValue, Color accentColor)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(3),
            Padding = new Padding(8, 6, 8, 6)
        };

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = UITheme.TextMuted,
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 16
        };

        lblValue.Text = "0,00 ₺";
        lblValue.ForeColor = accentColor;
        lblValue.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        lblValue.Dock = DockStyle.Fill;
        lblValue.TextAlign = ContentAlignment.MiddleLeft;

        pnl.Controls.Add(lblValue);
        pnl.Controls.Add(lblTitle);
        return pnl;
    }

    private void SetupGrid()
    {
        _gridExpenses.Dock = DockStyle.Fill;
        _gridExpenses.ReadOnly = true;
        _gridExpenses.AllowUserToAddRows = false;
        _gridExpenses.AllowUserToDeleteRows = false;
        _gridExpenses.BackgroundColor = Color.White;
        _gridExpenses.BorderStyle = BorderStyle.Fixed3D;
        _gridExpenses.RowHeadersVisible = false;
        _gridExpenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridExpenses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridExpenses.Font = new Font("Segoe UI", 9.5f);
        _gridExpenses.ColumnHeadersHeight = 36;
        _gridExpenses.RowTemplate.Height = 30;
    }

    private void QuickPeriodChanged(object? sender, EventArgs e)
    {
        DateTime today = DateTime.Today;
        switch (_cmbPeriodQuick.SelectedIndex)
        {
            case 0: // Bu Ay
                _dtpPeriodStart.Value = new DateTime(today.Year, today.Month, 1);
                _dtpPeriodEnd.Value = today;
                break;
            case 1: // Geçen Ay
                var prevMonth = today.AddMonths(-1);
                _dtpPeriodStart.Value = new DateTime(prevMonth.Year, prevMonth.Month, 1);
                _dtpPeriodEnd.Value = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                break;
            case 2: // Son 30 Gün
                _dtpPeriodStart.Value = today.AddDays(-30);
                _dtpPeriodEnd.Value = today;
                break;
            case 3: // Son 3 Ay
                _dtpPeriodStart.Value = today.AddMonths(-3);
                _dtpPeriodEnd.Value = today;
                break;
            case 4: // Bu Yıl
                _dtpPeriodStart.Value = new DateTime(today.Year, 1, 1);
                _dtpPeriodEnd.Value = today;
                break;
            case 5: // Tüm Zamanlar
                _dtpPeriodStart.Value = new DateTime(2020, 1, 1);
                _dtpPeriodEnd.Value = today;
                break;
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        string start = _dtpPeriodStart.Value.ToString("yyyy-MM-dd");
        string end = _dtpPeriodEnd.Value.ToString("yyyy-MM-dd") + " 23:59:59";

        // 1. Masrafları Listele
        string sqlExp = @"
SELECT 
    Id,
    ExpenseDate AS [Tarih],
    Category AS [Gider Kategorisi],
    Amount AS [Tutar (₺)],
    PaymentMethod AS [Ödeme Şekli],
    Note AS [Açıklama]
FROM Expenses
WHERE ExpenseDate >= @start AND ExpenseDate <= @end
ORDER BY ExpenseDate DESC, Id DESC";

        DataTable dtExp = Database.Query(sqlExp, ("@start", start), ("@end", end));
        _gridExpenses.DataSource = dtExp;

        if (_gridExpenses.Columns["Id"] != null) _gridExpenses.Columns["Id"].Visible = false;
        if (_gridExpenses.Columns["Tutar (₺)"] != null) _gridExpenses.Columns["Tutar (₺)"].DefaultCellStyle.Format = "N2 ₺";

        double totalExpenses = 0;
        foreach (DataRow r in dtExp.Rows)
        {
            totalExpenses += Convert.ToDouble(r["Tutar (₺)"]);
        }

        // 2. Satış Hasılatı ve COGS (Maliyet) Hesaplama
        // StockMovements üzerinden: MovementType IN ('Satılan', 'Satış', 'Giden')
        string sqlSales = @"
SELECT 
    COALESCE(SUM(sm.Quantity * sm.UnitPrice), 0) AS TotalRevenue,
    COALESCE(SUM(sm.Quantity * COALESCE(p.PurchasePrice, 0)), 0) AS TotalCogs
FROM StockMovements sm
LEFT JOIN Products p ON p.Id = sm.ProductId
WHERE sm.MovementType IN ('Satılan', 'Satış', 'Giden')
  AND sm.MovementDate >= @start AND sm.MovementDate <= @end";

        DataTable dtSales = Database.Query(sqlSales, ("@start", start), ("@end", end));
        double totalRevenue = 0;
        double totalCogs = 0;

        if (dtSales.Rows.Count > 0)
        {
            totalRevenue = Convert.ToDouble(dtSales.Rows[0]["TotalRevenue"]);
            totalCogs = Convert.ToDouble(dtSales.Rows[0]["TotalCogs"]);
        }

        double grossProfit = totalRevenue - totalCogs;
        double netProfit = grossProfit - totalExpenses;

        _lblRevenue.Text = $"{totalRevenue:N2} ₺";
        _lblCogs.Text = $"{totalCogs:N2} ₺";
        _lblGrossProfit.Text = $"{grossProfit:N2} ₺";
        _lblTotalExpenses.Text = $"{totalExpenses:N2} ₺";

        if (netProfit >= 0)
        {
            _lblNetProfit.Text = $"+{netProfit:N2} ₺";
            _lblNetProfit.ForeColor = Color.FromArgb(16, 185, 129); // Yeşil
        }
        else
        {
            _lblNetProfit.Text = $"{netProfit:N2} ₺";
            _lblNetProfit.ForeColor = Color.FromArgb(220, 38, 38); // Kırmızı
        }
    }

    private void SaveExpenseClick()
    {
        string amtStr = _txtExpenseAmount.Text.Trim().Replace('.', ',');
        if (!double.TryParse(amtStr, out double amount) || amount <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir masraf tutarı giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Database.Execute(@"
INSERT INTO Expenses (ExpenseDate, Category, Amount, PaymentMethod, Note, CreatedAt)
VALUES (@date, @cat, @amt, @method, @note, @created)",
                ("@date", _dtpExpenseDate.Value.ToString("yyyy-MM-dd")),
                ("@cat", _cmbExpenseCat.Text.Trim()),
                ("@amt", amount),
                ("@method", _cmbExpenseMethod.Text.Trim()),
                ("@note", _txtExpenseNote.Text.Trim()),
                ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            );

            _txtExpenseAmount.Text = "0,00";
            _txtExpenseNote.Text = string.Empty;
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Masraf eklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteExpenseClick()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.FinanceExpenses) && !curUser.HasPermission(UserPermissions.FinanceDeleteMovement) && !curUser.IsSuperUser)
        {
            MessageBox.Show("Gider / masraf kaydı silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_gridExpenses.CurrentRow?.DataBoundItem is DataRowView rv)
        {
            long id = Convert.ToInt64(rv["Id"]);
            string cat = rv["Gider Kategorisi"]?.ToString() ?? "Gider";
            double amt = Convert.ToDouble(rv["Tutar (₺)"]);

            if (MessageBox.Show($"'{cat}' kategorisindeki {amt:N2} ₺ tutarındaki gideri silmek istediğinize emin misiniz?", "Gideri Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Database.Execute("DELETE FROM Expenses WHERE Id=@id", ("@id", id));
                RefreshAll();
            }
        }
        else
        {
            MessageBox.Show("Lütfen silmek için bir gider kaydı seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
