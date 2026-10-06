namespace StokVeresiyeApp.Helpers;

public static class PromptDialog
{
    public static string Show(string prompt, string title, string defaultValue = "")
    {
        using var form = new Form
        {
            Width = 440,
            Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UITheme.Background,
            Font = UITheme.RegularFont
        };

        var lblText = new Label
        {
            Left = 20,
            Top = 15,
            Width = 380,
            Text = prompt,
            Font = UITheme.TitleFont,
            ForeColor = UITheme.TextPrimary
        };

        var textBox = new TextBox
        {
            Left = 20,
            Top = 45,
            Width = 380,
            Text = defaultValue,
            Font = UITheme.RegularFont
        };

        var btnOk = UITheme.CreateButton("Tamam", UITheme.Primary, Color.White, (s, e) =>
        {
            form.DialogResult = DialogResult.OK;
            form.Close();
        }, 90, 32);
        btnOk.Left = 210;
        btnOk.Top = 90;

        var btnCancel = UITheme.CreateButton("İptal", UITheme.Secondary, Color.White, (s, e) =>
        {
            form.DialogResult = DialogResult.Cancel;
            form.Close();
        }, 90, 32);
        btnCancel.Left = 310;
        btnCancel.Top = 90;

        form.Controls.Add(lblText);
        form.Controls.Add(textBox);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        textBox.SelectAll();
        return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : "";
    }
}
