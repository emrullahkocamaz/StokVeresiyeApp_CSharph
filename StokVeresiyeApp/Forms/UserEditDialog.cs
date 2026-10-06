using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class UserEditDialog : Form
{
    private readonly long _userId;
    private User? _existingUser;

    private TextBox _txtUsername = new();
    private TextBox _txtFullName = new();
    private ComboBox _cmbRole = new();
    private TextBox _txtPassword = new();
    private CheckBox _chkIsActive = new() { Text = "Hesap Aktif (Girişe İzin Ver)", Checked = true, AutoSize = true };
    private Dictionary<string, CheckBox> _permissionBoxes = new();

    // Depo Yetkilendirme
    private CheckBox _chkAllWarehouses = new() { Text = "Tüm Depolara Yetkili (Kısıtlama Yok)", Checked = true, AutoSize = true };
    private CheckedListBox _clbWarehouses = new() { Height = 100, CheckOnClick = true };
    private List<Warehouse> _allWarehouses = new();

    public UserEditDialog(long userId = 0)
    {
        _userId = userId;
        Text = _userId > 0 ? "Kullanıcı Düzenle" : "Yeni Kullanıcı Tanımla";
        Width = 620;
        Height = 840;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        if (_userId > 0)
        {
            LoadUserData();
        }
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 12, 20, 12)
        };
        var lblTitle = new Label
        {
            Text = _userId > 0 ? "✏️ Kullanıcı Bilgilerini Düzenle" : "👤 Yeni Kullanıcı & Yetkilendirme",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Alt Butonlar
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        var btnCancel = UITheme.CreateButton("İptal", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 100, 34);
        btnCancel.Dock = DockStyle.Right;

        var btnSave = UITheme.CreateButton("💾 Kaydet", UITheme.Primary, Color.White, SaveClick, 130, 34);
        btnSave.Dock = DockStyle.Right;
        AcceptButton = btnSave;

        bottom.Controls.Add(btnSave);
        bottom.Controls.Add(btnCancel);
        Controls.Add(bottom);

        // 3. İçerik Alanı
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16),
            AutoScroll = true
        };

        int top = 8;

        // Kullanıcı Adı
        var lblUser = new Label { Text = "Kullanıcı Adı (Giriş Adı):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 260, Height = 20 };
        var lblFullName = new Label { Text = "Adı Soyadı / Unvanı:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 280, Top = top, Width = 270, Height = 20 };
        top += 22;

        _txtUsername.Left = 0;
        _txtUsername.Top = top;
        _txtUsername.Width = 260;
        _txtUsername.Height = 28;

        _txtFullName.Left = 280;
        _txtFullName.Top = top;
        _txtFullName.Width = 270;
        _txtFullName.Height = 28;

        content.Controls.Add(lblUser);
        content.Controls.Add(_txtUsername);
        content.Controls.Add(lblFullName);
        content.Controls.Add(_txtFullName);

        top += 38;

        // Rol ve Şifre
        var lblRole = new Label { Text = "Kullanıcı Rolü:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 260, Height = 20 };
        var lblPass = new Label { Text = _userId > 0 ? "Yeni Şifre (Değişmeyecekse Boş):" : "Giriş Şifresi:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 280, Top = top, Width = 270, Height = 20 };
        top += 22;

        _cmbRole.Left = 0;
        _cmbRole.Top = top;
        _cmbRole.Width = 260;
        _cmbRole.Height = 28;
        _cmbRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbRole.Items.AddRange(new object[] { "SuperAdmin", "Admin", "Yönetici", "Kullanıcı", "Kasiyer" });
        _cmbRole.SelectedIndex = 3; // Kullanıcı
        _cmbRole.SelectedIndexChanged += RoleChanged;

        _txtPassword.Left = 280;
        _txtPassword.Top = top;
        _txtPassword.Width = 270;
        _txtPassword.Height = 28;
        _txtPassword.UseSystemPasswordChar = true;

        content.Controls.Add(lblRole);
        content.Controls.Add(_cmbRole);
        content.Controls.Add(lblPass);
        content.Controls.Add(_txtPassword);

        top += 38;

        // Aktiflik
        _chkIsActive.Left = 0;
        _chkIsActive.Top = top;
        _chkIsActive.Font = UITheme.RegularFont;
        content.Controls.Add(_chkIsActive);

        top += 34;

        // Depo Yetkilendirme Paneli
        var lblWarehouseHeader = new Label
        {
            Text = "🏬 Yetkili Depo Tanımı (Tek veya Çoklu Depo):",
            Font = UITheme.TitleFont,
            ForeColor = UITheme.PrimaryDark,
            Left = 0,
            Top = top,
            Width = 550,
            Height = 20
        };
        content.Controls.Add(lblWarehouseHeader);
        top += 24;

        _chkAllWarehouses.Left = 0;
        _chkAllWarehouses.Top = top;
        _chkAllWarehouses.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        _chkAllWarehouses.ForeColor = Color.FromArgb(2, 132, 199);
        _chkAllWarehouses.CheckedChanged += (s, e) =>
        {
            _clbWarehouses.Enabled = !_chkAllWarehouses.Checked;
            if (_chkAllWarehouses.Checked)
            {
                for (int i = 0; i < _clbWarehouses.Items.Count; i++)
                    _clbWarehouses.SetItemChecked(i, true);
            }
        };
        content.Controls.Add(_chkAllWarehouses);
        top += 26;

        _clbWarehouses.Left = 0;
        _clbWarehouses.Top = top;
        _clbWarehouses.Width = 550;
        _clbWarehouses.Height = 85;
        _clbWarehouses.BorderStyle = BorderStyle.FixedSingle;
        _clbWarehouses.Font = UITheme.RegularFont;
        _clbWarehouses.Enabled = false;

        // Depoları Doldur
        _allWarehouses = WarehouseService.GetActiveWarehouses();
        foreach (var w in _allWarehouses)
        {
            _clbWarehouses.Items.Add($"{w.Name} (ID: {w.Id})", true);
        }
        content.Controls.Add(_clbWarehouses);
        top += 95;

        // Yetkiler Başlığı ve Butonları
        var permHeaderPanel = new Panel { Left = 0, Top = top, Width = 550, Height = 28 };
        var lblPerms = new Label { Text = "🔑 Modül ve İşlem Yetkileri:", Font = UITheme.TitleFont, ForeColor = UITheme.PrimaryDark, Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        
        var btnSelectAll = UITheme.CreateButton("Tümünü Seç", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => SetAllPermissions(true), 90, 24);
        btnSelectAll.Dock = DockStyle.Right;
        var btnClearAll = UITheme.CreateButton("Temizle", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => SetAllPermissions(false), 70, 24);
        btnClearAll.Dock = DockStyle.Right;

        permHeaderPanel.Controls.Add(lblPerms);
        permHeaderPanel.Controls.Add(btnSelectAll);
        permHeaderPanel.Controls.Add(btnClearAll);
        content.Controls.Add(permHeaderPanel);

        top += 32;

        // Yetki Kutucukları Paneli
        var pnlPerms = new Panel
        {
            Left = 0,
            Top = top,
            Width = 550,
            Height = 260,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8),
            AutoScroll = true
        };

        int permTop = 6;
        foreach (var kvp in UserPermissions.Descriptions)
        {
            var chk = new CheckBox
            {
                Text = $"{kvp.Value} ({kvp.Key})",
                Tag = kvp.Key,
                Left = 12,
                Top = permTop,
                Width = 510,
                Height = 24,
                Font = UITheme.RegularFont
            };
            _permissionBoxes[kvp.Key] = chk;
            pnlPerms.Controls.Add(chk);
            permTop += 26;
        }

        content.Controls.Add(pnlPerms);
        Controls.Add(content);

        content.BringToFront();
        header.SendToBack();
        bottom.SendToBack();
    }

    private void RoleChanged(object? sender, EventArgs e)
    {
        string role = _cmbRole.SelectedItem?.ToString() ?? "";
        
        if (role == "Admin" || role == "SuperAdmin")
        {
            SetAllPermissions(true);
        }
        else if (role == "Yönetici")
        {
            // Yönetici: Kullanıcı ve Lisans hariç tüm modüller
            SetAllPermissions(true);
            if (_permissionBoxes.TryGetValue(UserPermissions.Users, out var chkU)) chkU.Checked = false;
            if (_permissionBoxes.TryGetValue(UserPermissions.License, out var chkL)) chkL.Checked = false;
        }
        else if (role == "Kasiyer")
        {
            // Kasiyer: Hızlı Satış, Barkod Etiketi, Ürünler (Görüntüleme), Fiş Bekletme, Parçalı Tahsilat, Dijital Fiş
            SetAllPermissions(false);
            if (_permissionBoxes.TryGetValue(UserPermissions.QuickSale, out var chkQ)) chkQ.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ParkedSales, out var chkPark)) chkPark.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.SplitPayment, out var chkSplit)) chkSplit.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.DigitalReceipt, out var chkDig)) chkDig.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Products, out var chkP)) chkP.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Accounts, out var chkA)) chkA.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.BarcodeLabels, out var chkB)) chkB.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.MobileScanner, out var chkM)) chkM.Checked = true;
        }
        else // Standart Kullanıcı
        {
            SetAllPermissions(false);
            if (_permissionBoxes.TryGetValue(UserPermissions.QuickSale, out var chkQ)) chkQ.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ParkedSales, out var chkPark)) chkPark.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.SplitPayment, out var chkSplit)) chkSplit.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ProductVariants, out var chkVar)) chkVar.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.DailyRegister, out var chkDaily)) chkDaily.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.DigitalReceipt, out var chkDig)) chkDig.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.InvoiceEntry, out var chkI)) chkI.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.InvoicePayment, out var chkInvPay)) chkInvPay.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Products, out var chkP)) chkP.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Accounts, out var chkA)) chkA.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.StockMovements, out var chkS)) chkS.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.AccountMovements, out var chkAM)) chkAM.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.StockCount, out var chkSC)) chkSC.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Warehouses, out var chkW)) chkW.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.BarcodeLabels, out var chkB)) chkB.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.MobileScanner, out var chkM)) chkM.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Notifications, out var chkN)) chkN.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Reports, out var chkR)) chkR.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ExcelOperations, out var chkE)) chkE.Checked = true;
        }

        // Tüm kutular düzenlenebilir kalsın
        foreach (var chk in _permissionBoxes.Values)
        {
            chk.Enabled = true;
        }
    }

    private void SetAllPermissions(bool check)
    {
        foreach (var chk in _permissionBoxes.Values)
        {
            chk.Checked = check;
        }
    }

    private void LoadUserData()
    {
        var users = UserService.GetAllUsers();
        _existingUser = users.FirstOrDefault(u => u.Id == _userId);
        if (_existingUser == null)
        {
            MessageBox.Show("Kullanıcı bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        _txtUsername.Text = _existingUser.Username;
        if (_existingUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            _txtUsername.ReadOnly = true;
            // Admin kullanıcısı için de rol ve yetkiler düzenlenebilir olsun
            _cmbRole.Enabled = true;
            _chkIsActive.Enabled = true;
        }

        _txtFullName.Text = _existingUser.FullName;
        _cmbRole.SelectedItem = _existingUser.Role;
        _chkIsActive.Checked = _existingUser.IsActive;

        // Depo Yetkilerini Yükle
        string assignedWh = _existingUser.AssignedWarehouses ?? "ALL";
        if (assignedWh == "ALL" || string.IsNullOrWhiteSpace(assignedWh))
        {
            _chkAllWarehouses.Checked = true;
            _clbWarehouses.Enabled = false;
            for (int i = 0; i < _clbWarehouses.Items.Count; i++)
                _clbWarehouses.SetItemChecked(i, true);
        }
        else
        {
            _chkAllWarehouses.Checked = false;
            _clbWarehouses.Enabled = true;
            var whIds = assignedWh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < _allWarehouses.Count; i++)
            {
                bool isAssigned = whIds.Contains(_allWarehouses[i].Id.ToString());
                _clbWarehouses.SetItemChecked(i, isAssigned);
            }
        }

        // Yetki kutucuklarını her zaman aktif bırak
        foreach (var chk in _permissionBoxes.Values)
        {
            chk.Enabled = true;
        }

        if (_existingUser.Permissions == "ALL")
        {
            SetAllPermissions(true);
        }
        else if (!string.IsNullOrWhiteSpace(_existingUser.Permissions))
        {
            var perms = _existingUser.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var kvp in _permissionBoxes)
            {
                kvp.Value.Checked = perms.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase);
            }
        }
        else
        {
            // Permissions boşsa ve rolde SuperAdmin/Admin ise varsayılan tümü seçili
            if (_existingUser.Role == "Admin" || _existingUser.Role == "SuperAdmin")
            {
                SetAllPermissions(true);
            }
            else
            {
                SetAllPermissions(false);
            }
        }
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        string username = _txtUsername.Text.Trim();
        string fullName = _txtFullName.Text.Trim();
        string role = _cmbRole.SelectedItem?.ToString() ?? "Kullanıcı";
        string password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Kullanıcı adı boş bırakılamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            MessageBox.Show("Ad Soyad alanı boş bırakılamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_userId == 0 && string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Yeni kullanıcı için lütfen şifre belirleyiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Depo Yetkisi Belirleme
        string assignedWarehouses = "ALL";
        if (!_chkAllWarehouses.Checked)
        {
            var selectedWhIds = new List<long>();
            for (int i = 0; i < _allWarehouses.Count; i++)
            {
                if (_clbWarehouses.GetItemChecked(i))
                    selectedWhIds.Add(_allWarehouses[i].Id);
            }

            if (selectedWhIds.Count == 0)
            {
                MessageBox.Show("Lütfen kullanıcı için en az bir yetkili depo seçiniz veya 'Tüm Depolara Yetkili' kutusunu işaretleyiniz.", "Depo Seçimi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            assignedWarehouses = string.Join(",", selectedWhIds);
        }

        // Yetki atamalarını topla (Admin ve Super dahil tüm rollerde seçilen yetkiler geçerlidir)
        var selectedPerms = _permissionBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        string permissions;
        if (selectedPerms.Count == _permissionBoxes.Count)
        {
            permissions = "ALL";
        }
        else
        {
            permissions = string.Join(",", selectedPerms);
        }

        if (_userId == 0)
        {
            var newUser = new User
            {
                Username = username,
                FullName = fullName,
                Role = role,
                Permissions = permissions,
                AssignedWarehouses = assignedWarehouses,
                IsActive = _chkIsActive.Checked
            };

            var res = UserService.AddUser(newUser, password);
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            _existingUser!.Username = username;
            _existingUser.FullName = fullName;
            _existingUser.Role = role;
            _existingUser.Permissions = permissions;
            _existingUser.AssignedWarehouses = assignedWarehouses;
            _existingUser.IsActive = _chkIsActive.Checked;

            var res = UserService.UpdateUser(_existingUser, string.IsNullOrWhiteSpace(password) ? null : password);
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
