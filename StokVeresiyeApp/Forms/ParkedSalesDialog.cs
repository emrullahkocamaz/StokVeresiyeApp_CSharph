using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ParkedSalesDialog : BaseModernForm
{
    private readonly ListBox _lstParked = new();
    public ParkedSaleModel? SelectedParkedSale { get; private set; }

    public ParkedSalesDialog() : base("📂 Bekleyen / Askıdaki Fişler (Masa & Plaka Takibi)", 750, 520)
    {
        _lstParked.Dock = DockStyle.Fill;
        _lstParked.Font = new Font("Segoe UI", 10.5f);
        _lstParked.ItemHeight = 32;

        var pnlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        pnlContainer.Controls.Add(_lstParked);

        if (Controls.Find("bodyPanel", true).FirstOrDefault() is Panel bodyPanel)
        {
            bodyPanel.Controls.Clear();
            bodyPanel.Controls.Add(pnlContainer);
        }
        else
        {
            Controls.Add(pnlContainer);
            pnlContainer.BringToFront();
        }

        BtnSave.Text = "⚡ Bu Fişi Kasaya Geri Yükle";
        BtnSave.Click += RestoreClick;

        var btnDelete = UITheme.CreateButton("🗑️ Fişi Sil", Color.FromArgb(239, 68, 68), Color.White, (s, e) =>
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.ParkedSales) && !curUser.HasPermission(UserPermissions.QuickSale) && !curUser.IsSuperUser)
            {
                MessageBox.Show("Askıdaki fişi silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_lstParked.SelectedItem is ParkedSaleModel p)
            {
                if (MessageBox.Show($"'{p.DisplayText}' fişini silmek istediğinize emin misiniz?", "Fişi Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ParkedSalesService.Remove(p.Id);
                    LoadList();
                }
            }
        }, 120, 36);

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            btnDelete.Location = new Point(16, 12);
            actionPanel.Controls.Add(btnDelete);
        }

        _lstParked.DoubleClick += (s, e) => RestoreClick(s, e);
        LoadList();
    }

    private void LoadList()
    {
        _lstParked.Items.Clear();
        var all = ParkedSalesService.GetAll();
        foreach (var item in all)
        {
            _lstParked.Items.Add(item);
        }
        if (_lstParked.Items.Count > 0)
        {
            _lstParked.SelectedIndex = 0;
            BtnSave.Enabled = true;
        }
        else
        {
            BtnSave.Enabled = false;
        }
    }

    private void RestoreClick(object? sender, EventArgs e)
    {
        if (_lstParked.SelectedItem is ParkedSaleModel p)
        {
            SelectedParkedSale = ParkedSalesService.Pop(p.Id);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show("Lütfen geri yüklemek için bir fiş seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
