using System.Drawing;

namespace MaintenanceManagement;

internal sealed class LoginForm : Form
{
    private readonly TextBox username = new() { Width = 240, Text = "admin" };
    private readonly TextBox password = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly bool loginOnly;
    public LoginForm(bool loginOnly = false)
    {
        this.loginOnly = loginOnly;
        if (loginOnly) { Text = "MOBOROOT | ورود / تغییر کاربر (F12)"; }
        Text = "MOBOROOT | ورود به سامانه";
        Width = 420; Height = 280; StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "نام کاربری:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(username, 1, 0);
        layout.Controls.Add(new Label { Text = "رمز عبور:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(password, 1, 1);
        var login = new Button { Text = "ورود", Width = 100, Height = 34 };
        login.Click += (_, _) => SignIn();
        layout.Controls.Add(login, 1, 2);
        layout.Controls.Add(new Label { Text = "ورود اولیه: admin / Admin@123 (پس از ورود رمز را تغییر دهید)", AutoSize = true, ForeColor = Color.DarkSlateGray }, 0, 3);
        layout.SetColumnSpan(layout.GetControlFromPosition(0,3)!, 2);
        Controls.Add(layout);
        AcceptButton = login;
    }

    private void SignIn()
    {
        if (Database.Authenticate(username.Text, password.Text))
        {
            if (loginOnly) { DialogResult = DialogResult.OK; Close(); return; }
            Hide();
            using var main = new MainForm();
            main.ShowDialog();
            Close();
        }
        else MessageBox.Show("نام کاربری یا رمز عبور نادرست است.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
