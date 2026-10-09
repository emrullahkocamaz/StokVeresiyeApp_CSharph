using System.Data;
using Krypton.Toolkit;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class CurrencyRatesDialog : BaseModernForm
{
    private readonly DataGridView _grid = new();
    private readonly KryptonTextBox _txtAmount = new() { Text = "100" };
    private readonly KryptonComboBox _cmbFrom = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonLabel _lblResult = new() { Text = "0,00 ₺" };
    private List<CurrencyRateItem> _rates = new();

    public CurrencyRatesDialog() : base("💱 Canlı TCMB Döviz Kurları & Çevirici", 850, 620)
    {
        // Çevirici Paneli
        _cmbFrom.Items.AddRange(new object[] { "USD (Dolar) ➔ TRY", "EUR (Euro) ➔ TRY", "GBP (Sterlin) ➔ TRY", "TRY ➔ USD (Dolar)", "TRY ➔ EUR (Euro)" });
        _cmbFrom.SelectedIndex = 0;

        _lblResult.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblResult.ForeColor = Color.FromArgb(16, 185, 129);

        AddRow("Çevrilecek Tutar", _txtAmount, 38);
        AddRow("Döviz Yönü", _cmbFrom, 38);
        AddRow("Hesaplanan Karşılık", _lblResult, 40);

        _txtAmount.TextChanged += (s, e) => CalculateConvert();
        _cmbFrom.SelectedIndexChanged += (s, e) => CalculateConvert();

        // Grid
        SetupGrid();
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Height = 280, Padding = new Padding(0, 8, 0, 0) };
        pnlGrid.Controls.Add(_grid);
        AddRow("Canlı TCMB Tablosu", pnlGrid, 290);

        BtnSave.Visible = false;
        BtnCancel.Text = "Kapat";

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            var btnRefresh = UITheme.CreateKryptonButton("🔄 Kurları Yenile (TCMB)", Color.FromArgb(79, 70, 229), Color.White, async (s, e) =>
            {
                await RefreshRatesAsync();
            }, 190, 36);
            btnRefresh.Dock = DockStyle.Left;
            actionPanel.Controls.Add(btnRefresh);
        }

        _ = RefreshRatesAsync();
    }

    private void SetupGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Font = new Font("Segoe UI", 9.5f);
        _grid.ColumnHeadersHeight = 36;
        _grid.RowTemplate.Height = 32;
    }

    private async Task RefreshRatesAsync()
    {
        try
        {
            _rates = await CurrencyService.GetTcmbRatesAsync(true);
            var dt = new DataTable();
            dt.Columns.Add("Döviz Kodu");
            dt.Columns.Add("Döviz Adı");
            dt.Columns.Add("Döviz Alış (₺)", typeof(double));
            dt.Columns.Add("Döviz Satış (₺)", typeof(double));
            dt.Columns.Add("Efektif Satış (₺)", typeof(double));

            foreach (var r in _rates)
            {
                dt.Rows.Add(r.Code, r.Name, r.ForexBuying, r.ForexSelling, r.BanknoteSelling);
            }

            _grid.DataSource = dt;
            if (_grid.Columns["Döviz Alış (₺)"] != null) _grid.Columns["Döviz Alış (₺)"].DefaultCellStyle.Format = "N4 ₺";
            if (_grid.Columns["Döviz Satış (₺)"] != null) _grid.Columns["Döviz Satış (₺)"].DefaultCellStyle.Format = "N4 ₺";
            if (_grid.Columns["Efektif Satış (₺)"] != null) _grid.Columns["Efektif Satış (₺)"].DefaultCellStyle.Format = "N4 ₺";

            CalculateConvert();
        }
        catch { }
    }

    private void CalculateConvert()
    {
        if (_rates == null || _rates.Count == 0) return;
        string txt = _txtAmount.Text.Trim().Replace('.', ',');
        if (!double.TryParse(txt, out double amt) || amt <= 0)
        {
            _lblResult.Text = "0,00 ₺";
            return;
        }

        var usd = _rates.FirstOrDefault(r => r.Code == "USD");
        var eur = _rates.FirstOrDefault(r => r.Code == "EUR");
        var gbp = _rates.FirstOrDefault(r => r.Code == "GBP");

        double res = 0;
        string symbol = "₺";

        switch (_cmbFrom.SelectedIndex)
        {
            case 0: // USD -> TRY
                if (usd != null) res = amt * usd.ForexSelling;
                symbol = "₺";
                break;
            case 1: // EUR -> TRY
                if (eur != null) res = amt * eur.ForexSelling;
                symbol = "₺";
                break;
            case 2: // GBP -> TRY
                if (gbp != null) res = amt * gbp.ForexSelling;
                symbol = "₺";
                break;
            case 3: // TRY -> USD
                if (usd != null && usd.ForexSelling > 0) res = amt / usd.ForexSelling;
                symbol = "$";
                break;
            case 4: // TRY -> EUR
                if (eur != null && eur.ForexSelling > 0) res = amt / eur.ForexSelling;
                symbol = "€";
                break;
        }

        _lblResult.Text = $"{res:N2} {symbol}";
    }
}
