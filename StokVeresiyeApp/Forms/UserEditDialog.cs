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
    private readonly Dictionary<string, CheckBox> _permissionBoxes = new();

    // Depo Yetkilendirme
    private CheckBox _chkAllWarehouses = new() { Text = "Tüm Depolara Yetkili (Kısıtlama Yok)", Checked = true, AutoSize = true };
    private CheckedListBox _clbWarehouses = new() { Height = 75, CheckOnClick = true };
    private List<Warehouse> _allWarehouses = new();

    public UserEditDialog(long userId = 0)
    {
        _userId = userId;
        Text = _userId > 0 ? "Kullanıcı Düzenle & Detaylı Yetkilendirme" : "Yeni Kullanıcı Tanımla & Detaylı Yetkilendirme";
        Width = 870;
        Height = 880;
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
            Padding = new Padding(24, 12, 24, 12)
        };
        var lblTitle = new Label
        {
            Text = _userId > 0 ? "✏️ Kullanıcı Bilgilerini & Modül İçi Yetkileri Düzenle" : "👤 Yeni Kullanıcı & Modül İçi Yetki Yönetimi",
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
            Height = 60,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(24, 12, 24, 12)
        };

        var btnCancel = UITheme.CreateButton("İptal", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 105, 36);
        btnCancel.Dock = DockStyle.Right;

        var btnSave = UITheme.CreateButton("💾 Kaydet ve Yetkileri Uygula", UITheme.Primary, Color.White, SaveClick, 220, 36);
        btnSave.Dock = DockStyle.Right;
        AcceptButton = btnSave;

        bottom.Controls.Add(btnSave);
        bottom.Controls.Add(btnCancel);
        Controls.Add(bottom);

        // 3. İçerik Alanı
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 14, 24, 14),
            AutoScroll = true
        };

        int top = 8;
        const int col1Left = 0;
        const int col1Width = 380;
        const int col2Left = 405;
        const int col2Width = 390;

        // Kullanıcı Adı & Tam Adı
        var lblUser = new Label { Text = "Kullanıcı Adı (Giriş Adı):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = col1Left, Top = top, Width = col1Width, Height = 20 };
        var lblFullName = new Label { Text = "Adı Soyadı / Unvanı:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = col2Left, Top = top, Width = col2Width, Height = 20 };
        top += 22;

        _txtUsername.Left = col1Left;
        _txtUsername.Top = top;
        _txtUsername.Width = col1Width;
        _txtUsername.Height = 28;

        _txtFullName.Left = col2Left;
        _txtFullName.Top = top;
        _txtFullName.Width = col2Width;
        _txtFullName.Height = 28;

        content.Controls.Add(lblUser);
        content.Controls.Add(_txtUsername);
        content.Controls.Add(lblFullName);
        content.Controls.Add(_txtFullName);

        top += 38;

        // Rol ve Şifre
        var lblRole = new Label { Text = "Kullanıcı Rolü (Şablon):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = col1Left, Top = top, Width = col1Width, Height = 20 };
        var lblPass = new Label { Text = _userId > 0 ? "Yeni Şifre (Değişmeyecekse Boş Bırakınız):" : "Giriş Şifresi:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = col2Left, Top = top, Width = col2Width, Height = 20 };
        top += 22;

        _cmbRole.Left = col1Left;
        _cmbRole.Top = top;
        _cmbRole.Width = col1Width;
        _cmbRole.Height = 28;
        _cmbRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbRole.Items.AddRange(new object[] { "SuperAdmin", "Admin", "Yönetici", "Kullanıcı", "Kasiyer" });
        _cmbRole.SelectedIndex = 3; // Kullanıcı
        _cmbRole.SelectedIndexChanged += RoleChanged;

        _txtPassword.Left = col2Left;
        _txtPassword.Top = top;
        _txtPassword.Width = col2Width;
        _txtPassword.Height = 28;
        _txtPassword.UseSystemPasswordChar = true;

        content.Controls.Add(lblRole);
        content.Controls.Add(_cmbRole);
        content.Controls.Add(lblPass);
        content.Controls.Add(_txtPassword);

        top += 38;

        // Aktiflik Kutusu
        _chkIsActive.Left = col1Left;
        _chkIsActive.Top = top;
        _chkIsActive.Font = UITheme.RegularFont;
        content.Controls.Add(_chkIsActive);

        top += 32;

        // Depo Yetkilendirme Paneli
        var pnlWarehouseBox = new Panel
        {
            Left = 0,
            Top = top,
            Width = 800,
            Height = 115,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblWarehouseHeader = new Label
        {
            Text = "🏬 Yetkili Depo Tanımı (İşlem Yapabileceği Depolar):",
            Font = UITheme.TitleFont,
            ForeColor = UITheme.PrimaryDark,
            Dock = DockStyle.Top,
            Height = 20
        };
        pnlWarehouseBox.Controls.Add(lblWarehouseHeader);

        _chkAllWarehouses.Dock = DockStyle.Top;
        _chkAllWarehouses.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        _chkAllWarehouses.ForeColor = Color.FromArgb(2, 132, 199);
        _chkAllWarehouses.Height = 26;
        _chkAllWarehouses.CheckedChanged += (s, e) =>
        {
            _clbWarehouses.Enabled = !_chkAllWarehouses.Checked;
            if (_chkAllWarehouses.Checked)
            {
                for (int i = 0; i < _clbWarehouses.Items.Count; i++)
                    _clbWarehouses.SetItemChecked(i, true);
            }
        };
        pnlWarehouseBox.Controls.Add(_chkAllWarehouses);

        _clbWarehouses.Dock = DockStyle.Fill;
        _clbWarehouses.BorderStyle = BorderStyle.None;
        _clbWarehouses.Font = UITheme.RegularFont;
        _clbWarehouses.Enabled = false;

        _allWarehouses = WarehouseService.GetActiveWarehouses();
        foreach (var w in _allWarehouses)
        {
            _clbWarehouses.Items.Add($"{w.Name} (Depo ID: {w.Id})", true);
        }
        pnlWarehouseBox.Controls.Add(_clbWarehouses);

        content.Controls.Add(pnlWarehouseBox);
        top += 125;

        // Yetkiler Üst Başlığı ve Hızlı İşlem Butonları
        var permHeaderPanel = new Panel { Left = 0, Top = top, Width = 800, Height = 34 };
        var lblPerms = new Label
        {
            Text = "🔑 Modüller ve Modül İçi Detaylı Yetkiler (Ekleme / Düzenleme / Silme):",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Left = 0,
            Top = 6,
            Width = 470,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var btnClearAll = UITheme.CreateButton("❌ Temizle", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => SetAllPermissions(false), 80, 26);
        btnClearAll.Left = 715;
        btnClearAll.Top = 3;

        var btnSafeMode = UITheme.CreateButton("🛡️ Silme Yetkilerini Kaldır", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9), (s, e) => RemoveDeletePermissions(), 180, 26);
        btnSafeMode.Left = 525;
        btnSafeMode.Top = 3;

        var btnSelectAll = UITheme.CreateButton("🌟 Tümünü Seç", Color.FromArgb(224, 231, 255), Color.FromArgb(67, 56, 202), (s, e) => SetAllPermissions(true), 100, 26);
        btnSelectAll.Left = 415;
        btnSelectAll.Top = 3;

        permHeaderPanel.Controls.Add(lblPerms);
        permHeaderPanel.Controls.Add(btnSelectAll);
        permHeaderPanel.Controls.Add(btnSafeMode);
        permHeaderPanel.Controls.Add(btnClearAll);
        content.Controls.Add(permHeaderPanel);

        top += 40;

        // Kategori Bazlı Kart Paneli (Scrollable Container)
        var pnlCategories = new Panel
        {
            Left = 0,
            Top = top,
            Width = 800,
            Height = 440,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        int catTop = 8;
        var categories = UserPermissions.AllPermissions.GroupBy(p => p.Category).ToList();

        var tt = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 200 };

        foreach (var group in categories)
        {
            var catItems = group.ToList();
            int rowCount = (int)Math.Ceiling(catItems.Count / 2.0);
            int catCardHeight = 36 + (rowCount * 30) + 10;

            var card = new Panel
            {
                Left = 8,
                Top = catTop,
                Width = 760,
                Height = catCardHeight,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 6, 10, 8)
            };

            // Başlık şeridi
            var titlePanel = new Panel { Dock = DockStyle.Top, Height = 28 };
            var lblCatTitle = new Label
            {
                Text = group.Key,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnCatToggle = new LinkLabel
            {
                Text = "Tümünü Seç / Kaldır",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                LinkColor = Color.FromArgb(2, 132, 199),
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };

            var currentCategoryBoxes = new List<CheckBox>();

            titlePanel.Controls.Add(lblCatTitle);
            titlePanel.Controls.Add(btnCatToggle);
            card.Controls.Add(titlePanel);

            // Yetki kutucukları (2 Sütunlu)
            int itemIndex = 0;
            foreach (var item in catItems)
            {
                int cCol = itemIndex % 2;
                int cRow = itemIndex / 2;
                int itemLeft = cCol == 0 ? 12 : 380;
                int itemTop = 32 + (cRow * 30);

                string prefix = item.IsDeleteOrCritical ? "🗑️ [SİLME/ÇIKARMA] " : "";
                var chk = new CheckBox
                {
                    Text = prefix + item.Title,
                    Tag = item.Key,
                    Left = itemLeft,
                    Top = itemTop,
                    Width = 355,
                    Height = 26,
                    Font = item.IsDeleteOrCritical 
                        ? new Font(UITheme.RegularFont.FontFamily, 8.5f, FontStyle.Bold) 
                        : new Font(UITheme.RegularFont.FontFamily, 8.5f, FontStyle.Regular),
                    ForeColor = item.IsDeleteOrCritical ? Color.FromArgb(185, 28, 28) : UITheme.TextPrimary,
                    AutoEllipsis = true
                };

                tt.SetToolTip(chk, $"{item.Title} ({item.Key})\n{item.Description}");

                _permissionBoxes[item.Key] = chk;
                currentCategoryBoxes.Add(chk);
                card.Controls.Add(chk);

                itemIndex++;
            }

            btnCatToggle.LinkClicked += (s, e) =>
            {
                bool anyUnchecked = currentCategoryBoxes.Any(b => !b.Checked);
                foreach (var b in currentCategoryBoxes)
                {
                    b.Checked = anyUnchecked;
                }
            };

            pnlCategories.Controls.Add(card);
            catTop += catCardHeight + 10;
        }

        content.Controls.Add(pnlCategories);
        Controls.Add(content);

        Controls.SetChildIndex(header, 0);
        Controls.SetChildIndex(bottom, 1);
        Controls.SetChildIndex(content, 2);
        content.BringToFront();
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
            // Yönetici: Kullanıcı yönetimi, Lisans ve Sistem Sıfırlama hariç tüm modüller ve silmeler açık
            SetAllPermissions(true);
            if (_permissionBoxes.TryGetValue(UserPermissions.Users, out var chkU)) chkU.Checked = false;
            if (_permissionBoxes.TryGetValue(UserPermissions.License, out var chkL)) chkL.Checked = false;
            if (_permissionBoxes.TryGetValue(UserPermissions.SystemReset, out var chkSR)) chkSR.Checked = false;
        }
        else if (role == "Kasiyer")
        {
            // Kasiyer: Sadece Hızlı Satış, Fiş Bekletme, Parçalı Tahsilat, Dijital Fiş, Çift Ekran, Terazi, Ürün & Cari Görüntüleme
            SetAllPermissions(false);
            if (_permissionBoxes.TryGetValue(UserPermissions.QuickSale, out var chkQ)) chkQ.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ParkedSales, out var chkPark)) chkPark.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.SplitPayment, out var chkSplit)) chkSplit.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.DigitalReceipt, out var chkDig)) chkDig.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.CustomerDisplay, out var chkCD)) chkCD.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.ScaleBarcode, out var chkSB)) chkSB.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Products, out var chkP)) chkP.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.Accounts, out var chkA)) chkA.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.BarcodeLabels, out var chkB)) chkB.Checked = true;
            if (_permissionBoxes.TryGetValue(UserPermissions.MobileScanner, out var chkM)) chkM.Checked = true;
        }
        else // Standart Kullanıcı (Personel)
        {
            // Standart Kullanıcı: İşlem yapabilir (Ekleme, Düzenleme, Raporlama vb.) ANCAK SİLME/ÇIKARMA YETKİLERİ KAPALI!
            SetAllPermissions(false);
            foreach (var perm in UserPermissions.AllPermissions)
            {
                // Silme / Kritik yetkiler ve Yönetim modülleri standart kullanıcıda kapalı olsun
                if (perm.IsDeleteOrCritical) continue;
                if (perm.Key == UserPermissions.Users || perm.Key == UserPermissions.License || perm.Key == UserPermissions.SettingsBackup || perm.Key == UserPermissions.CloudBackup) continue;

                if (_permissionBoxes.TryGetValue(perm.Key, out var chk))
                {
                    chk.Checked = true;
                }
            }
        }

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

    private void RemoveDeletePermissions()
    {
        int count = 0;
        foreach (var perm in UserPermissions.AllPermissions)
        {
            if (perm.IsDeleteOrCritical && _permissionBoxes.TryGetValue(perm.Key, out var chk))
            {
                if (chk.Checked)
                {
                    chk.Checked = false;
                    count++;
                }
            }
        }
        MessageBox.Show($"Tüm silme, pasife alma ve kritik sıfırlama yetkileri ({count} adet) bu kullanıcı için kaldırıldı.\nPersonel veri silemeyecek şekilde güvene alındı.", "Güvenli Mod", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                // Birebir eşleşme veya ana yetki varsa (Örn: eskiden "Products" verilmişse "Products.Create", "Products.Delete" vb. de işaretlensin)
                bool hasPerm = perms.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase);
                if (!hasPerm && kvp.Key.Contains('.'))
                {
                    var parent = kvp.Key.Split('.')[0];
                    if (perms.Contains(parent, StringComparer.OrdinalIgnoreCase))
                    {
                        hasPerm = true;
                    }
                }
                kvp.Value.Checked = hasPerm;
            }
        }
        else
        {
            if (_existingUser.Role == "Admin" || _existingUser.Role == "SuperAdmin")
            {
                SetAllPermissions(true);
            }
            else
            {
                RoleChanged(null, EventArgs.Empty);
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

        // Yetki atamalarını topla
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
