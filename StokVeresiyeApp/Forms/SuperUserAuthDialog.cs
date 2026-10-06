using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class SuperUserAuthDialog : Form
{
    private TextBox _txtPassword = new();
    private Label _lblError = new();

    public SuperUserAuthDialog()
    {
        Text = "Süper Kullanıcı Doğrulama";
        Width = 380;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
    }

    private void BuildUI()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(16, 10, 16, 10)
        };
        var lblTitle = new Label
        {
            Text = "🔐 Süper Kullanıcı Girişi",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill
        };
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 15, 20, 15)
        };

        var lblPrompt = new Label
        {
            Text = "Süper Kullanıcı Şifresini Giriniz:",
            Font = UITheme.TitleFont,
            ForeColor = UITheme.TextPrimary,
            Left = 0,
            Top = 6,
            Width = 320,
            Height = 22
        };

        _txtPassword.Left = 0;
        _txtPassword.Top = 32;
        _txtPassword.Width = 320;
        _txtPassword.Height = 28;
        _txtPassword.UseSystemPasswordChar = true;

        _lblError.Left = 0;
        _lblError.Top = 65;
        _lblError.Width = 320;
        _lblError.Height = 20;
        _lblError.ForeColor = UITheme.Danger;
        _lblError.Font = UITheme.SmallFont;

        var btnOk = UITheme.CreateButton("Giriş Yap", UITheme.Primary, Color.White, VerifyClick, 100, 32);
        btnOk.Left = 110;
        btnOk.Top = 90;
        AcceptButton = btnOk;

        var btnCancel = UITheme.CreateButton("İptal", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 100, 32);
        btnCancel.Left = 220;
        btnCancel.Top = 90;

        pnl.Controls.Add(lblPrompt);
        pnl.Controls.Add(_txtPassword);
        pnl.Controls.Add(_lblError);
        pnl.Controls.Add(btnOk);
        pnl.Controls.Add(btnCancel);

        Controls.Add(pnl);
    }

    private void VerifyClick(object? sender, EventArgs e)
    {
        string pass = _txtPassword.Text.Trim();
        if (pass == LicenseService.SuperUserPassword)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Text = "Hatalı Süper Kullanıcı Şifresi!";
            _txtPassword.SelectAll();
            _txtPassword.Focus();
        }
    }
}
