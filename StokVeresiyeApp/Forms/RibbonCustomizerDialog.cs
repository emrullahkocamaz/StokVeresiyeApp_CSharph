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
    public bool IsLocked { get; set; } = true;
    public Dictionary<string, bool> TabVisibility { get; set; } = new();
}

public class RibbonCustomizerDialog : BaseModernForm
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

        return new RibbonTabConfig { IsLocked = true };
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

    public RibbonCustomizerDialog(KryptonRibbon ribbon, Action onLayoutChanged) 
        : base("🧩 Menü & Sekme Düzenleyici (Puzzle Modu)", 620, 520)
    {
        _ribbon = ribbon;
        _onLayoutChanged = onLayoutChanged;

        BuildInterface();
        LoadCurrentState();
    }

    private void BuildInterface()
    {
        BtnSave.Text = "🔒 Kilitle ve Kaydet";
        BtnSave.Click += (s, e) => SaveAndApply();

        var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

        var lblBanner = new Label
        {
            Text = "🧩 Ribbon Menü Puzzle Düzenleyicisi:\nKullanmadığınız menü sekmelerini kapatabilir veya ihtiyacınız olanları açabilirsiniz. Düzenlemeniz bittiğinde 'Kilitle ve Kaydet' butonuna basarak yanlışlıkla menülerin silinmesini veya değişmesini engelleyebilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.Primary,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(10),
            Dock = DockStyle.Top,
            Height = 70
        };

        _flowTabs.Dock = DockStyle.Fill;
        _flowTabs.FlowDirection = FlowDirection.TopDown;
        _flowTabs.WrapContents = false;
        _flowTabs.AutoScroll = true;
        _flowTabs.Padding = new Padding(0, 15, 0, 10);

        var pnlLockBar = new Panel { Dock = DockStyle.Bottom, Height = 42 };
        _chkLock.Text = "🔒 Menü Düzenini Kilitle (Yetkisiz kullanıcılar değiştiremesin)";
        _chkLock.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        _chkLock.Dock = DockStyle.Left;
        _chkLock.Checked = true;
        _chkLock.CheckedChanged += (s, e) => UpdateLockState();

        var btnReset = UITheme.CreateButton("🔄 Tüm Menüleri Aç", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => ResetAllTabs(), 150, 32);
        btnReset.Dock = DockStyle.Right;

        pnlLockBar.Controls.Add(_chkLock);
        pnlLockBar.Controls.Add(btnReset);

        container.Controls.Add(_flowTabs);
        container.Controls.Add(pnlLockBar);
        container.Controls.Add(lblBanner);

        ContentTable.Visible = false;
        Controls.Add(container);
        container.BringToFront();
    }

    private void LoadCurrentState()
    {
        var config = LoadConfig();
        _chkLock.Checked = config.IsLocked;

        _flowTabs.Controls.Clear();
        _tabCheckBoxes.Clear();

        foreach (KryptonRibbonTab tab in _ribbon.RibbonTabs)
        {
            bool isVisible = tab.Visible;
            if (config.TabVisibility != null && config.TabVisibility.TryGetValue(tab.Text, out bool savedVis))
            {
                isVisible = savedVis;
            }

            var card = new CardPanel
            {
                Width = 560,
                Height = 44,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 0, 8)
            };

            var chk = new KryptonCheckBox
            {
                Text = $"{tab.Text} Sekmesi",
                Font = new Font(UITheme.RegularFont, FontStyle.Bold),
                Checked = isVisible,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            // Canlı önizleme: Kullanıcı tıkladıkça ekranda canlı değişsin (Puzzle gibi)
            chk.CheckedChanged += (s, e) =>
            {
                tab.Visible = chk.Checked;
                _onLayoutChanged?.Invoke();
            };

            _tabCheckBoxes[tab.Text] = chk;

            var lblDesc = new Label
            {
                Text = chk.Checked ? "Açık (Görünür)" : "Kapalı (Gizli)",
                Font = UITheme.SmallFont,
                ForeColor = chk.Checked ? UITheme.Success : UITheme.TextMuted,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 120
            };

            chk.CheckedChanged += (s, e) =>
            {
                lblDesc.Text = chk.Checked ? "Açık (Görünür)" : "Kapalı (Gizli)";
                lblDesc.ForeColor = chk.Checked ? UITheme.Success : UITheme.TextMuted;
            };

            card.Controls.Add(lblDesc);
            card.Controls.Add(chk);
            _flowTabs.Controls.Add(card);
        }

        UpdateLockState();
    }

    private void UpdateLockState()
    {
        bool isLocked = _chkLock.Checked;
        foreach (var chk in _tabCheckBoxes.Values)
        {
            chk.Enabled = !isLocked;
        }
        BtnSave.Text = isLocked ? "🔒 Kilitli Olarak Kaydet" : "💾 Değişiklikleri Kaydet";
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
            if (_ribbon.RibbonTabs.FirstOrDefault(t => t.Text == kvp.Key) is KryptonRibbonTab tab)
            {
                tab.Visible = kvp.Value.Checked;
            }
        }

        SaveConfig(config);
        _onLayoutChanged?.Invoke();

        MessageBox.Show("Menü düzeni başarıyla kilitlendi ve kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
