using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Krypton.Ribbon;
using Krypton.Toolkit;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class RibbonTabConfig
{
    public bool IsLocked { get; set; } = false;
    public Dictionary<string, bool> TabVisibility { get; set; } = new();
}

public class RibbonCustomizerDialog : KryptonForm
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp",
        "ribbon_tabs_config.json"
    );

    private readonly KryptonRibbon _ribbon;
    private readonly Action _onLayoutChanged;
    private readonly FlowLayoutPanel _flowTabs = new();
    private readonly KryptonCheckBox _chkLock = new();
    private KryptonButton _btnSave = new();
    private readonly Dictionary<string, KryptonCheckBox> _tabCheckBoxes = new();

    public static RibbonTabConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<RibbonTabConfig>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { }

        return new RibbonTabConfig { IsLocked = false };
    }

    public static void SaveConfig(RibbonTabConfig config)
    {
        try
        {
            string dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static void ApplyToRibbon(KryptonRibbon ribbon)
    {
        try
        {
            var config = LoadConfig();
            if (config.TabVisibility == null || config.TabVisibility.Count == 0) return;

            foreach (KryptonRibbonTab tab in ribbon.RibbonTabs)
            {
                if (config.TabVisibility.TryGetValue(tab.Text, out bool isVisible))
                {
                    tab.Visible = isVisible;
                }
            }
        }
        catch { }
    }

    public RibbonCustomizerDialog(KryptonRibbon ribbon, Action onLayoutChanged)
    {
        _ribbon = ribbon;
        _onLayoutChanged = onLayoutChanged;

        Text = "🧩 Menü & Sekme Düzenleyici (Puzzle Modu)";
        ClientSize = new Size(760, 580);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        BuildInterface();
        LoadCurrentState();
    }

    private void BuildInterface()
    {
        // 1. Üst Başlık & Açıklama Paneli
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 12, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "🧩 Ribbon Menü & Modül Düzenleyicisi (Puzzle Modu)",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };

        var lblBanner = new Label
        {
            Text = "İşletmenizde kullanmadığınız menü sekmelerini kapatabilir veya ihtiyacınız olanları açabilirsiniz. Seçim yaptığınız anda üst menü canlı olarak güncellenir. Düzenlemeniz bittiğinde 'Kilitle ve Kaydet' butonuna basarak yanlışlıkla menülerin gizlenmesini önleyebilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = Color.FromArgb(30, 64, 175),
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(10, 6, 10, 6),
            Dock = DockStyle.Fill
        };

        headerPanel.Controls.Add(lblBanner);
        headerPanel.Controls.Add(lblTitle);

        // 2. Alt Butonlar & Kilitleme Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 14, 20, 14)
        };

        _chkLock.Text = "🔒 Menü Düzenini Kilitle (Yetkisizler değiştiremesin)";
        _chkLock.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        _chkLock.Dock = DockStyle.Left;
        _chkLock.AutoSize = true;

        var btnCancel = UITheme.CreateKryptonButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 90, 36);
        btnCancel.Dock = DockStyle.Right;

        var btnReset = UITheme.CreateKryptonButton("🔄 Tümünü Aç", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) => ResetAllTabs(), 125, 36);
        btnReset.Dock = DockStyle.Right;

        _btnSave = UITheme.CreateKryptonButton("💾 Kaydet", UITheme.Primary, Color.White, (s, e) => SaveAndApply(), 160, 36);
        _btnSave.Dock = DockStyle.Right;

        _chkLock.CheckedChanged += (s, e) =>
        {
            _btnSave.Text = _chkLock.Checked ? "🔒 Kilitle ve Kaydet" : "💾 Değişiklikleri Kaydet";
            _btnSave.StateCommon.Back.Color1 = _chkLock.Checked ? Color.FromArgb(234, 88, 12) : UITheme.Primary;
            _btnSave.StateCommon.Back.Color2 = _btnSave.StateCommon.Back.Color1;
        };

        bottomPanel.Controls.Add(_chkLock);
        bottomPanel.Controls.Add(btnCancel);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        bottomPanel.Controls.Add(btnReset);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        bottomPanel.Controls.Add(_btnSave);

        // 3. Orta Kaydırılabilir Liste Alanı
        var listContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 12, 20, 12)
        };

        _flowTabs.Dock = DockStyle.Top;
        _flowTabs.AutoSize = true;
        _flowTabs.FlowDirection = FlowDirection.TopDown;
        _flowTabs.WrapContents = false;
        _flowTabs.Padding = new Padding(0);

        listContainer.Controls.Add(_flowTabs);

        Controls.Add(listContainer);
        Controls.Add(bottomPanel);
        Controls.Add(headerPanel);

        headerPanel.SendToBack();
        bottomPanel.SendToBack();
        listContainer.BringToFront();
    }

    private void LoadCurrentState()
    {
        var config = LoadConfig();
        _chkLock.Checked = config.IsLocked;
        _btnSave.Text = config.IsLocked ? "🔒 Kilitle ve Kaydet" : "💾 Değişiklikleri Kaydet";
        if (config.IsLocked)
        {
            _btnSave.StateCommon.Back.Color1 = Color.FromArgb(234, 88, 12);
            _btnSave.StateCommon.Back.Color2 = Color.FromArgb(234, 88, 12);
        }

        _flowTabs.Controls.Clear();
        _tabCheckBoxes.Clear();

        if (_ribbon.RibbonTabs.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "Görüntülenecek menü sekmesi bulunamadı.",
                Font = UITheme.RegularFont,
                ForeColor = UITheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(10)
            };
            _flowTabs.Controls.Add(lblEmpty);
            return;
        }

        foreach (KryptonRibbonTab tab in _ribbon.RibbonTabs)
        {
            bool isVisible = tab.Visible;
            if (config.TabVisibility != null && config.TabVisibility.TryGetValue(tab.Text, out bool savedVis))
            {
                isVisible = savedVis;
            }

            var card = new CardPanel
            {
                Width = 700,
                Height = 54,
                Padding = new Padding(15, 8, 15, 8),
                Margin = new Padding(0, 0, 0, 8)
            };

            var chk = new KryptonCheckBox
            {
                Text = $"  {tab.Text}",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Checked = isVisible,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            var lblDesc = new Label
            {
                Text = isVisible ? "✅ Açık (Menüde Görünüyor)" : "❌ Gizli (Menüde Kapalı)",
                Font = UITheme.SmallFont,
                ForeColor = isVisible ? UITheme.Success : UITheme.TextMuted,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 200
            };

            // Canlı önizleme: Kullanıcı tıkladıkça ekranda canlı değişsin (Puzzle gibi)
            chk.CheckedChanged += (s, e) =>
            {
                tab.Visible = chk.Checked;
                lblDesc.Text = chk.Checked ? "✅ Açık (Menüde Görünüyor)" : "❌ Gizli (Menüde Kapalı)";
                lblDesc.ForeColor = chk.Checked ? UITheme.Success : UITheme.TextMuted;
                _onLayoutChanged?.Invoke();
            };

            _tabCheckBoxes[tab.Text] = chk;

            card.Controls.Add(lblDesc);
            card.Controls.Add(chk);
            _flowTabs.Controls.Add(card);
        }
    }

    private void ResetAllTabs()
    {
        foreach (var kvp in _tabCheckBoxes)
        {
            kvp.Value.Checked = true;
        }
    }

    private void SaveAndApply()
    {
        var config = new RibbonTabConfig
        {
            IsLocked = _chkLock.Checked,
            TabVisibility = new Dictionary<string, bool>()
        };

        foreach (var kvp in _tabCheckBoxes)
        {
            config.TabVisibility[kvp.Key] = kvp.Value.Checked;
        }

        SaveConfig(config);
        ApplyToRibbon(_ribbon);
        _onLayoutChanged?.Invoke();

        string lockMessage = config.IsLocked 
            ? "Menü düzeni başarıyla kilitlendi ve kaydedildi!" 
            : "Menü düzeni başarıyla kaydedildi!";

        MessageBox.Show(lockMessage, "Menü Düzeni Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
