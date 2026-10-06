using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class WarehouseManageDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtSearch = new();

    public WarehouseManageDialog()
    {
        Text = "🏢 Depo, Şube & Araç Yönetim Merkezi";
        ClientSize = new Size(950, 580);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        RefreshWarehouses();
    }

    private void BuildUI()
    {
        // 1. Başlık
        var header = new CardPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(20, 12, 20, 12) };
        var lblTitle = new Label { Text = "🏢 Depo, Şube ve Saha Aracı Tanımları", Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = "İşletmenize ait merkez depo, şubeler ve mobil satış araçlarını tanımlayıp yönetebilirsiniz.", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Toolbar
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(12, 12, 12, 12) };
        _txtSearch.Width = 220;
        _txtSearch.PlaceholderText = "🔍 Depo / Şube Ara...";
        _txtSearch.TextChanged += (s, e) => RefreshWarehouses();

        var btnAdd = UITheme.CreateButton("+ Yeni Depo / Şube", UITheme.Primary, Color.White, (s, e) => AddWarehouse(), 160, 34);
        var btnEdit = UITheme.CreateButton("✏️ Düzenle", UITheme.Secondary, Color.White, (s, e) => EditWarehouse(), 100, 34);
        var btnSetDefault = UITheme.CreateButton("⭐ Varsayılan Yap", Color.FromArgb(217, 119, 6), Color.White, (s, e) => SetDefaultWarehouse(), 140, 34);
        var btnDelete = UITheme.CreateButton("🗑️ Sil / Pasif", UITheme.Danger, Color.White, (s, e) => DeleteWarehouse(), 110, 34);
        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshWarehouses(), 40, 34);

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flow.Controls.Add(_txtSearch);
        flow.Controls.Add(btnAdd);
        flow.Controls.Add(btnEdit);
        flow.Controls.Add(btnSetDefault);
        flow.Controls.Add(btnDelete);
        flow.Controls.Add(btnRefresh);
        toolbar.Controls.Add(flow);
        Controls.Add(toolbar);

        // 3. Grid
        var gridCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(15, 10, 15, 15) };
        UITheme.ApplyGridStyle(_grid);
        _grid.CellDoubleClick += (s, e) => EditWarehouse();
        gridCard.Controls.Add(_grid);
        Controls.Add(gridCard);
    }

    private void RefreshWarehouses()
    {
        try
        {
            var dt = WarehouseService.GetAllWarehouses(_txtSearch.Text?.Trim());
            _grid.DataSource = dt;
            if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
            if (_grid.Columns["Mevcut Stok Adedi"] != null) _grid.Columns["Mevcut Stok Adedi"].DefaultCellStyle.Format = "N2";
        }
        catch { }
    }

    private long GetSelectedId()
    {
        if (_grid.SelectedRows.Count == 0) return -1;
        return Convert.ToInt64(_grid.SelectedRows[0].Cells["Id"].Value);
    }

    private void AddWarehouse()
    {
        using var dlg = new WarehouseEditDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            RefreshWarehouses();
        }
    }

    private void EditWarehouse()
    {
        long id = GetSelectedId();
        if (id <= 0)
        {
            MessageBox.Show("Lütfen düzenlemek istediğiniz depoyu seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new WarehouseEditDialog(id);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            RefreshWarehouses();
        }
    }

    private void SetDefaultWarehouse()
    {
        long id = GetSelectedId();
        if (id <= 0)
        {
            MessageBox.Show("Lütfen varsayılan yapmak istediğiniz depoyu seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var w = WarehouseService.GetById(id);
        if (w != null)
        {
            w.IsDefault = true;
            WarehouseService.SaveWarehouse(w);
            AuditLogService.Log("Depo", "Varsayılan Yapıldı", id, w.Name, "Depo varsayılan ana depo olarak belirlendi");
            RefreshWarehouses();
        }
    }

    private void DeleteWarehouse()
    {
        long id = GetSelectedId();
        if (id <= 0) return;

        var w = WarehouseService.GetById(id);
        if (w == null) return;

        if (w.IsDefault)
        {
            MessageBox.Show("Varsayılan ana depo silinemez! Önce başka bir depoyu varsayılan yapmalısınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show($"'{w.Name}' deposu silinsin mi? (Hareket kaydı varsa pasife alınacaktır)", "Depo Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            WarehouseService.DeleteWarehouse(id);
            AuditLogService.Log("Depo", "Silindi / Pasife Alındı", id, w.Name, $"Depo silindi / pasife alındı");
            RefreshWarehouses();
        }
    }
}

public class WarehouseEditDialog : BaseModernForm
{
    private readonly long _id;
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtCode = new();
    private readonly ComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtPerson = new();
    private readonly TextBox _txtPhone = new();
    private readonly CheckBox _chkDefault = new() { Text = "Varsayılan Ana Depo Olarak Ayarla", AutoSize = true, Font = UITheme.RegularFont };

    public WarehouseEditDialog(long id = 0) : base(id == 0 ? "Yeni Depo / Şube / Araç Ekle" : "Depo / Şube Bilgilerini Düzenle", 550, 480)
    {
        _id = id;

        _cmbType.Items.AddRange(new object[] { "Depo", "Şube", "Araç / Saha", "Konsinye / Diğer" });
        _cmbType.SelectedIndex = 0;

        AddRow("Depo / Şube Adı (*)", _txtName);
        AddRow("Depo Kodu", _txtCode);
        AddRow("Depo Türü (*)", _cmbType);
        AddRow("Yetkili / Sorumlu", _txtPerson);
        AddRow("İletişim / Tel", _txtPhone);
        AddRow("", _chkDefault);

        BtnSave.Click += SaveClick;

        if (_id > 0)
        {
            LoadData();
        }
        else
        {
            _txtCode.Text = $"DEP-{DateTime.Now:fff}";
        }
    }

    private void LoadData()
    {
        var w = WarehouseService.GetById(_id);
        if (w != null)
        {
            _txtName.Text = w.Name;
            _txtCode.Text = w.Code ?? "";
            _cmbType.SelectedItem = w.Type;
            _txtPerson.Text = w.ResponsiblePerson ?? "";
            _txtPhone.Text = w.Phone ?? "";
            _chkDefault.Checked = w.IsDefault;
        }
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Depo / Şube adı boş bırakılamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            var w = new Warehouse
            {
                Id = _id,
                Name = _txtName.Text.Trim(),
                Code = _txtCode.Text.Trim(),
                Type = _cmbType.SelectedItem?.ToString() ?? "Depo",
                ResponsiblePerson = _txtPerson.Text.Trim(),
                Phone = _txtPhone.Text.Trim(),
                IsDefault = _chkDefault.Checked
            };

            WarehouseService.SaveWarehouse(w);
            AuditLogService.Log("Depo", _id == 0 ? "Eklendi" : "Güncellendi", _id, w.Name, $"Tür: {w.Type}, Kod: {w.Code}, Sorumlu: {w.ResponsiblePerson}");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
