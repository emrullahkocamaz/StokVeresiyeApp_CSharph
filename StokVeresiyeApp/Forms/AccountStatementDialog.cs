using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class AccountStatementDialog : Form
{
    private readonly long _accountId;
    private readonly DataGridView _grid = new();
    private readonly Label _lblInfo = new();
    private readonly Label _lblBalance = new();

    public AccountStatementDialog(long accountId)
    {
        _accountId = accountId;
        Text = "Cari Hesap Ekstresi ve Hareket Detayı";
        ClientSize = new Size(1020, 660);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        LoadStatement();
    }

    private void BuildUI()
    {
        // Üst Özet Paneli
        var topPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(20, 15, 20, 15)
        };

        _lblInfo.Font = UITheme.SubHeaderFont;
        _lblInfo.ForeColor = UITheme.TextPrimary;
        _lblInfo.Dock = DockStyle.Left;
        _lblInfo.Width = 550;

        _lblBalance.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        _lblBalance.ForeColor = UITheme.Primary;
        _lblBalance.Dock = DockStyle.Right;
        _lblBalance.TextAlign = ContentAlignment.MiddleRight;
        _lblBalance.Width = 350;

        topPanel.Controls.Add(_lblInfo);
        topPanel.Controls.Add(_lblBalance);

        // Grid Paneli
        var gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        UITheme.ApplyGridStyle(_grid);
        gridPanel.Controls.Add(_grid);

        // Alt Buton Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(15, 14, 15, 14)
        };

        var btnAddPayment = UITheme.CreateButton("+ Tahsilat / Ödeme Ekle", UITheme.Success, Color.White, AddPaymentClick, 180, 36);
        var btnExportExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Primary, Color.White, ExportExcelClick, 140, 36);
        var btnWhatsApp = UITheme.CreateButton("📲 WhatsApp ile Gönder", Color.FromArgb(37, 211, 102), Color.White, SendWhatsAppClick, 190, 36);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 36);

        btnAddPayment.Dock = DockStyle.Left;
        btnExportExcel.Dock = DockStyle.Left;
        btnWhatsApp.Dock = DockStyle.Left;
        btnClose.Dock = DockStyle.Right;

        bottomPanel.Controls.Add(btnAddPayment);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        bottomPanel.Controls.Add(btnExportExcel);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        bottomPanel.Controls.Add(btnWhatsApp);
        bottomPanel.Controls.Add(btnClose);

        // Dock hiyerarşisi: Grid (Fill) önce, sonra Bottom ve Top eklenip SendToBack yapılır
        Controls.Add(gridPanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
        topPanel.SendToBack();
        bottomPanel.SendToBack();
    }

    private void LoadStatement()
    {
        var acc = AccountService.GetById(_accountId);
        if (acc == null)
        {
            MessageBox.Show("Cari bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        _lblInfo.Text = $"{acc.Name} ({acc.Type})\nTelefon: {(string.IsNullOrEmpty(acc.Phone) ? "-" : acc.Phone)} | Vergi No: {(string.IsNullOrEmpty(acc.TaxNumber) ? "-" : acc.TaxNumber)}";

        var rawDt = AccountService.GetAccountStatement(_accountId);

        // Yürüyen Bakiye hesaplamalı DataTable oluşturalım
        var dt = new DataTable();
        dt.Columns.Add("Tarih", typeof(string));
        dt.Columns.Add("İşlem", typeof(string));
        dt.Columns.Add("Belge No", typeof(string));
        dt.Columns.Add("Borç (₺)", typeof(double));
        dt.Columns.Add("Alacak (₺)", typeof(double));
        dt.Columns.Add("Bakiye (₺)", typeof(double));
        dt.Columns.Add("Ödeme Yöntemi", typeof(string));
        dt.Columns.Add("Kasa/Banka", typeof(string));
        dt.Columns.Add("Açıklama", typeof(string));

        double runningBalance = 0;
        foreach (DataRow r in rawDt.Rows)
        {
            double debit = Convert.ToDouble(r["Borç"]);
            double credit = Convert.ToDouble(r["Alacak"]);

            if (acc.Type == "Müşteri")
            {
                runningBalance += (debit - credit);
            }
            else // Tedarikçi
            {
                runningBalance += (credit - debit);
            }

            dt.Rows.Add(
                r["Tarih"],
                r["İşlem"],
                r["Belge No"],
                debit,
                credit,
                runningBalance,
                r["Ödeme Yöntemi"],
                r["Kasa/Banka"],
                r["Açıklama"]
            );
        }

        _grid.DataSource = dt;

        // Sayı formatları
        if (_grid.Columns["Borç (₺)"] != null) _grid.Columns["Borç (₺)"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Alacak (₺)"] != null) _grid.Columns["Alacak (₺)"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Bakiye (₺)"] != null) _grid.Columns["Bakiye (₺)"].DefaultCellStyle.Format = "N2";

        string bakiyeTur = runningBalance >= 0 
            ? (acc.Type == "Müşteri" ? "(Alacaklıyız)" : "(Borçluyuz)") 
            : (acc.Type == "Müşteri" ? "(Müşteri Alacaklı)" : "(Fazla Ödeme)");
        
        _lblBalance.Text = $"Güncel Bakiye: {Math.Abs(runningBalance):N2} ₺\n{bakiyeTur}";
        _lblBalance.ForeColor = runningBalance > 0 ? (acc.Type == "Müşteri" ? UITheme.Success : UITheme.Danger) : UITheme.TextSecondary;
    }

    private void AddPaymentClick(object? sender, EventArgs e)
    {
        var acc = AccountService.GetById(_accountId);
        string defaultType = acc?.Type == "Müşteri" ? "Tahsilat" : "Ödeme";
        using var f = new AccountMovementForm(_accountId, defaultType);
        if (f.ShowDialog() == DialogResult.OK)
        {
            LoadStatement();
        }
    }

    private void ExportExcelClick(object? sender, EventArgs e)
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak hareket bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Cari_Ekstre_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(dt, "Cari Ekstre", sfd.FileName);
                MessageBox.Show("Ekstre başarıyla Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SendWhatsAppClick(object? sender, EventArgs e)
    {
        var acc = AccountService.GetById(_accountId);
        if (acc == null) return;

        string phone = acc.Phone ?? "";
        // Rakamları temizle
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(digitsOnly))
        {
            MessageBox.Show("Bu cariye ait kayıtlı telefon numarası bulunamadı.\nLütfen cari kartını düzenleyip geçerli bir cep telefonu numarası giriniz.", "Telefon Eksik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Türkiye numarası formatlama (10 hane ise 90 ekle, 11 hane ve 0 ile başlıyorsa 0'ı 90 yap)
        if (digitsOnly.Length == 10 && digitsOnly.StartsWith("5"))
        {
            digitsOnly = "90" + digitsOnly;
        }
        else if (digitsOnly.Length == 11 && digitsOnly.StartsWith("05"))
        {
            digitsOnly = "90" + digitsOnly.Substring(1);
        }

        // Ekstre mesajı hazırlama
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Sayın *{acc.Name}*,");
        sb.AppendLine($"📅 *{DateTime.Now:dd.MM.yyyy HH:mm}* tarihi itibarıyla hesap özetiniz:");
        sb.AppendLine();
        sb.AppendLine($"💰 *{_lblBalance.Text.Replace("\n", " ")}*");
        sb.AppendLine();

        if (_grid.DataSource is DataTable dt && dt.Rows.Count > 0)
        {
            sb.AppendLine("📋 *Son Hesap Hareketleri:*");
            int count = 0;
            // Son 5 hareketi tersten alalım
            for (int i = dt.Rows.Count - 1; i >= 0 && count < 5; i--, count++)
            {
                var r = dt.Rows[i];
                string t = r["Tarih"]?.ToString() ?? "";
                string islem = r["İşlem"]?.ToString() ?? "";
                double borc = Convert.ToDouble(r["Borç (₺)"]);
                double alacak = Convert.ToDouble(r["Alacak (₺)"]);
                double tutar = borc > 0 ? borc : alacak;
                string tutarStr = tutar > 0 ? $"{tutar:N2} ₺" : "";
                string aciklama = r["Açıklama"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(aciklama)) aciklama = $" ({aciklama})";

                sb.AppendLine($"• {t} | {islem}: {tutarStr}{aciklama}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Detaylı bilgi için lütfen bizimle iletişime geçiniz.");
        sb.AppendLine("Sağlıklı ve bereketli günler dileriz.");

        string msg = sb.ToString();
        string url = $"https://wa.me/{digitsOnly}?text={Uri.EscapeDataString(msg)}";

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WhatsApp açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
