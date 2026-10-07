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
    public Dictionary<string, bool> ButtonVisibility { get; set; } = new();
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

    private readonly FlowLayoutPanel _flowTabsList = new();
    private readonly FlowLayoutPanel _flowButtonsList = new();
    private readonly Label _lblSelectedTabTitle = new();
    private readonly KryptonCheckBox _chkLock = new();
    private KryptonButton _btnSave = new();

    private KryptonRibbonTab? _selectedTab;
    private readonly Dictionary<string, KryptonCheckBox> _tabCheckBoxes = new();
    private readonly Dictionary<string, KryptonCheckBox> _buttonCheckBoxes = new();

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

            // 1. Sekmelerin Görünürlüğü
            if (config.TabVisibility != null)
            {
                foreach (KryptonRibbonTab tab in ribbon.RibbonTabs)
                {
                    if (config.TabVisibility.TryGetValue(tab.Text, out bool isVisible))
                    {
                        tab.Visible = isVisible;
                    }
                }
            }

            // 2. Butonların Görünürlüğü
            if (config.ButtonVisibility != null)
            {
                foreach (KryptonRibbonTab tab in ribbon.RibbonTabs)
                {
                    foreach (KryptonRibbonGroup grp in tab.Groups)
                    {
                        foreach (var container in grp.Items)
                        {
                            if (container is KryptonRibbonGroupTriple triple)
                            {
                                foreach (var item in triple.Items)
                                {
                                    if (item is KryptonRibbonGroupButton btn)
                                    {
                                        string key = $"{tab.Text}|{btn.TextLine1}";
                                        if (config.ButtonVisibility.TryGetValue(key, out bool btnVis))
                                        {
                                            btn.Visible = btnVis;
                                        }
                                    }
                                }
                            }
                            else if (container is KryptonRibbonGroupLines lines)
                            {
                                foreach (var item in lines.Items)
                                {
                                    if (item is KryptonRibbonGroupButton btn)
                                    {
                                        string key = $"{tab.Text}|{btn.TextLine1}";
                                        if (config.ButtonVisibility.TryGetValue(key, out bool btnVis))
                                        {
                                            btn.Visible = btnVis;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    public RibbonCustomizerDialog(KryptonRibbon ribbon, Action onLayoutChanged)
    {
        _ribbon = ribbon;
        _onLayoutChanged = onLayoutChanged;

        Text = "🧩 Menü & Modül Puzzle Düzenleyicisi (Sekmeler ve İçindeki Butonlar)";
        ClientSize = new Size(920, 640);
        MinimumSize = new Size(860, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
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
            Height = 85,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "🧩 Ribbon Menü & Modül İçi Buton Düzenleyicisi (Puzzle Modu)",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        var lblBanner = new Label
        {
            Text = "Sol taraftan dilediğiniz modülü (sekmeyi) açıp kapatabilir veya seçebilirsiniz. Sağ tarafta ise seçtiğiniz modülün İÇİNDEKİ butonları tek tek kapatıp açabilirsiniz. Yapılan tüm değişiklikler anında canlı olarak uygulanır.",
            Font = UITheme.RegularFont,
            ForeColor = Color.FromArgb(30, 64, 175),
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(8, 4, 8, 4),
            Dock = DockStyle.Fill
        };

        headerPanel.Controls.Add(lblBanner);
        headerPanel.Controls.Add(lblTitle);

        // 2. Alt İşlem Paneli
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

        var btnReset = UITheme.CreateKryptonButton("🔄 Tümünü Aç", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) => ResetAll(), 130, 36);
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

        // 3. Orta Çift Panelli Bölüm (Sol: Sekmeler, Sağ: Seçili Sekmenin Butonları)
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 340,
            SplitterWidth = 8,
            BackColor = UITheme.BorderColor
        };

        // --- SOL PANEL: MODÜLLER (SEKMELER) ---
        var pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.Background, Padding = new Padding(12) };
        var lblLeftHeader = new Label
        {
            Text = "📁 MODÜLLER (ANA SEKMELER)",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        var scrollTabs = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _flowTabsList.Dock = DockStyle.Top;
        _flowTabsList.AutoSize = true;
        _flowTabsList.FlowDirection = FlowDirection.TopDown;
        _flowTabsList.WrapContents = false;
        scrollTabs.Controls.Add(_flowTabsList);

        pnlLeft.Controls.Add(scrollTabs);
        pnlLeft.Controls.Add(lblLeftHeader);
        splitContainer.Panel1.Controls.Add(pnlLeft);

        // --- SAĞ PANEL: MODÜLÜN İÇİNDEKİ BUTONLAR ---
        var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.Background, Padding = new Padding(12) };
        
        var pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        _lblSelectedTabTitle.Text = "🔘 SEÇİLİ MODÜLÜN İÇİNDEKİ BUTONLAR";
        _lblSelectedTabTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblSelectedTabTitle.ForeColor = UITheme.Primary;
        _lblSelectedTabTitle.Dock = DockStyle.Left;
        _lblSelectedTabTitle.AutoSize = true;

        var btnOpenAllButtonsInTab = UITheme.CreateButton("Bu Sekmedeki Tüm Butonları Aç", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) => ResetButtonsForCurrentTab(), 220, 28);
        btnOpenAllButtonsInTab.Dock = DockStyle.Right;

        pnlRightHeader.Controls.Add(_lblSelectedTabTitle);
        pnlRightHeader.Controls.Add(btnOpenAllButtonsInTab);

        var scrollButtons = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _flowButtonsList.Dock = DockStyle.Top;
        _flowButtonsList.AutoSize = true;
        _flowButtonsList.FlowDirection = FlowDirection.TopDown;
        _flowButtonsList.WrapContents = false;
        scrollButtons.Controls.Add(_flowButtonsList);

        pnlRight.Controls.Add(scrollButtons);
        pnlRight.Controls.Add(pnlRightHeader);
        splitContainer.Panel2.Controls.Add(pnlRight);

        Controls.Add(splitContainer);
        Controls.Add(bottomPanel);
        Controls.Add(headerPanel);

        headerPanel.SendToBack();
        bottomPanel.SendToBack();
        splitContainer.BringToFront();
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

        _flowTabsList.Controls.Clear();
        _tabCheckBoxes.Clear();
        _buttonCheckBoxes.Clear();

        if (_ribbon.RibbonTabs.Count == 0) return;

        foreach (KryptonRibbonTab tab in _ribbon.RibbonTabs)
        {
            bool isTabVisible = tab.Visible;
            if (config.TabVisibility != null && config.TabVisibility.TryGetValue(tab.Text, out bool savedVis))
            {
                isTabVisible = savedVis;
            }

            var card = new CardPanel
            {
                Width = 310,
                Height = 48,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var chk = new KryptonCheckBox
            {
                Text = tab.Text,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Checked = isTabVisible,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            chk.CheckedChanged += (s, e) =>
            {
                tab.Visible = chk.Checked;
                _onLayoutChanged?.Invoke();
            };

            _tabCheckBoxes[tab.Text] = chk;

            card.Controls.Add(chk);

            // Karta tıklandığında sağ tarafta o sekmenin butonları listelensin
            card.Click += (s, e) => SelectTab(tab, card);
            chk.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(241, 245, 249);
            chk.MouseLeave += (s, e) => card.BackColor = UITheme.CardBg;

            _flowTabsList.Controls.Add(card);
        }

        // İlk sekmeyi seç
        if (_ribbon.RibbonTabs.Count > 0)
        {
            SelectTab(_ribbon.RibbonTabs[0], _flowTabsList.Controls[0] as CardPanel);
        }
    }

    private void SelectTab(KryptonRibbonTab tab, CardPanel? activeCard)
    {
        _selectedTab = tab;
        _lblSelectedTabTitle.Text = $"🔘 '{tab.Text}' MODÜLÜ İÇİNDEKİ BUTONLAR";

        // Kart vurgusu
        foreach (Control c in _flowTabsList.Controls)
        {
            if (c is CardPanel cp)
            {
                cp.BackColor = (cp == activeCard) ? Color.FromArgb(224, 242, 254) : UITheme.CardBg;
            }
        }

        // Sağ paneldeki butonları doldur
        _flowButtonsList.Controls.Clear();
        var config = LoadConfig();

        var buttons = GetButtonsInTab(tab);
        if (buttons.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "Bu modül içerisinde gizlenebilir buton bulunamadı.",
                Font = UITheme.RegularFont,
                ForeColor = UITheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(10)
            };
            _flowButtonsList.Controls.Add(lblEmpty);
            return;
        }

        foreach (var (grpName, btn) in buttons)
        {
            string key = $"{tab.Text}|{btn.TextLine1}";
            bool isBtnVisible = btn.Visible;
            if (config.ButtonVisibility != null && config.ButtonVisibility.TryGetValue(key, out bool savedVis))
            {
                isBtnVisible = savedVis;
            }

            var cardBtn = new CardPanel
            {
                Width = 510,
                Height = 50,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 0, 6)
            };

            var chkBtn = new KryptonCheckBox
            {
                Text = $"  {btn.TextLine1} {(string.IsNullOrWhiteSpace(btn.TextLine2) ? "" : $"({btn.TextLine2})")}",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Checked = isBtnVisible,
                Dock = DockStyle.Left,
                AutoSize = true
            };

            var lblGrp = new Label
            {
                Text = $"Grup: {grpName}",
                Font = UITheme.SmallFont,
                ForeColor = UITheme.TextMuted,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 140
            };

            chkBtn.CheckedChanged += (s, e) =>
            {
                btn.Visible = chkBtn.Checked;
                _onLayoutChanged?.Invoke();
            };

            _buttonCheckBoxes[key] = chkBtn;

            cardBtn.Controls.Add(lblGrp);
            cardBtn.Controls.Add(chkBtn);
            _flowButtonsList.Controls.Add(cardBtn);
        }
    }

    private List<(string GroupName, KryptonRibbonGroupButton Button)> GetButtonsInTab(KryptonRibbonTab tab)
    {
        var list = new List<(string GroupName, KryptonRibbonGroupButton Button)>();

        foreach (KryptonRibbonGroup grp in tab.Groups)
        {
            string grpTitle = grp.TextLine1 ?? "Genel";

            foreach (var container in grp.Items)
            {
                if (container is KryptonRibbonGroupTriple triple && triple.Items != null)
                {
                    foreach (var item in triple.Items)
                    {
                        if (item is KryptonRibbonGroupButton btn && !string.IsNullOrWhiteSpace(btn.TextLine1))
                        {
                            list.Add((grpTitle, btn));
                        }
                    }
                }
                else if (container is KryptonRibbonGroupLines lines && lines.Items != null)
                {
                    foreach (var item in lines.Items)
                    {
                        if (item is KryptonRibbonGroupButton btn && !string.IsNullOrWhiteSpace(btn.TextLine1))
                        {
                            list.Add((grpTitle, btn));
                        }
                    }
                }
            }
        }

        return list;
    }

    private void ResetButtonsForCurrentTab()
    {
        if (_selectedTab == null) return;
        var buttons = GetButtonsInTab(_selectedTab);
        foreach (var b in buttons)
        {
            string key = $"{_selectedTab.Text}|{b.Button.TextLine1}";
            if (_buttonCheckBoxes.TryGetValue(key, out var chk))
            {
                chk.Checked = true;
            }
            b.Button.Visible = true;
        }
        _onLayoutChanged?.Invoke();
    }

    private void ResetAll()
    {
        foreach (var chk in _tabCheckBoxes.Values) chk.Checked = true;
        foreach (var chk in _buttonCheckBoxes.Values) chk.Checked = true;

        foreach (KryptonRibbonTab tab in _ribbon.RibbonTabs)
        {
            tab.Visible = true;
            foreach (var b in GetButtonsInTab(tab))
            {
                b.Button.Visible = true;
            }
        }

        _onLayoutChanged?.Invoke();
    }

    private void SaveAndApply()
    {
        var config = new RibbonTabConfig
        {
            IsLocked = _chkLock.Checked,
            TabVisibility = new Dictionary<string, bool>(),
            ButtonVisibility = new Dictionary<string, bool>()
        };

        // Sekmeler
        foreach (var kvp in _tabCheckBoxes)
        {
            config.TabVisibility[kvp.Key] = kvp.Value.Checked;
        }

        // Butonlar
        foreach (KryptonRibbonTab tab in _ribbon.RibbonTabs)
        {
            foreach (var b in GetButtonsInTab(tab))
            {
                string key = $"{tab.Text}|{b.Button.TextLine1}";
                if (_buttonCheckBoxes.TryGetValue(key, out var chk))
                {
                    config.ButtonVisibility[key] = chk.Checked;
                }
                else
                {
                    config.ButtonVisibility[key] = b.Button.Visible;
                }
            }
        }

        SaveConfig(config);
        ApplyToRibbon(_ribbon);
        _onLayoutChanged?.Invoke();

        string lockMessage = config.IsLocked 
            ? "Menü ve buton düzeni başarıyla kilitlendi ve kaydedildi!" 
            : "Menü ve buton düzeni başarıyla kaydedildi!";

        MessageBox.Show(lockMessage, "Menü Düzeni Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
