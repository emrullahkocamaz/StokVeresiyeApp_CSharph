using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

/// <summary>
/// Yeni şifre belirleme penceresi: ilk kurulumda yönetici şifresi, varsayılan şifreyle giriş yapanın şifre değişimi
/// ve süper kullanıcı şifresi için kullanılır.
/// </summary>
public class PasswordSetupDialog : Form
{
    private readonly TextBox _txtPassword = new();
    private readonly TextBox _txtConfirm = new();
    private readonly Label _lblError = new();

    public string NewPassword => _txtPassword.Text;

    public PasswordSetupDialog(string title, string message, bool allowCancel)
    {
        Text = title;
        Icon = AppResources.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(440, 300);
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        ControlBox = allowCancel;

        var lblMsg = new Label
        {
            Text = message,
            ForeColor = UITheme.TextSecondary,
            Left = 24, Top = 20, Width = 392, Height = 70
        };

        var lblPass = new Label { Text = "Yeni şifre", ForeColor = UITheme.TextPrimary, Left = 24, Top = 98, Width = 392, Height = 20 };
        _txtPassword.Left = 24; _txtPassword.Top = 120; _txtPassword.Width = 392;
        _txtPassword.UseSystemPasswordChar = true;

        var lblConfirm = new Label { Text = "Yeni şifre (tekrar)", ForeColor = UITheme.TextPrimary, Left = 24, Top = 152, Width = 392, Height = 20 };
        _txtConfirm.Left = 24; _txtConfirm.Top = 174; _txtConfirm.Width = 392;
        _txtConfirm.UseSystemPasswordChar = true;

        _lblError.ForeColor = UITheme.Danger;
        _lblError.Left = 24; _lblError.Top = 206; _lblError.Width = 392; _lblError.Height = 36;

        var btnOk = UITheme.CreateButton("Kaydet", UITheme.Primary, Color.White, (s, e) => Confirm(), 120, 36);
        btnOk.Left = allowCancel ? 188 : 296;
        btnOk.Top = 248;

        Controls.AddRange(new Control[] { lblMsg, lblPass, _txtPassword, lblConfirm, _txtConfirm, _lblError, btnOk });
        AcceptButton = btnOk;

        if (allowCancel)
        {
            var btnCancel = UITheme.CreateButton("Vazgeç", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 100, 36);
            btnCancel.Left = 316;
            btnCancel.Top = 248;
            Controls.Add(btnCancel);
            CancelButton = btnCancel;
        }
    }

    private void Confirm()
    {
        string p = _txtPassword.Text;
        if (UserService.IsWeakPassword(p))
        {
            _lblError.Text = "Şifre en az 6 karakter olmalı ve yaygın bir şifre (123456, admin gibi) olmamalıdır.";
            return;
        }

        if (p != _txtConfirm.Text)
        {
            _lblError.Text = "İki şifre aynı değil. Lütfen tekrar girin.";
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
