namespace MaintenanceManagement;

internal sealed class RepairForm : Form
{
    private readonly Repair item;
    private readonly TextBox asset = new(), reporter = new(), phone = new(), problem = new(), technician = new(), cost = new(), notes = new(), received = new(), completed = new();
    private readonly ComboBox priority = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox status = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    public RepairForm(Repair repair)
    {
        item = repair;
        Text = repair.Id == 0 ? "ثبت تعمیر جدید" : $"ویرایش درخواست تعمیر شماره {repair.Id}";
        Width = 700; Height = 610; StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 12, AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        priority.Items.AddRange(new object[] { "کم", "متوسط", "زیاد", "بحرانی" });
        status.Items.AddRange(new object[] { "ثبت‌شده", "در حال بررسی", "در حال تعمیر", "تکمیل‌شده", "لغوشده" });
        Add(panel, "نام تجهیز *", asset, 0); Add(panel, "درخواست‌دهنده *", reporter, 1); Add(panel, "تلفن", phone, 2);
        Add(panel, "شرح خرابی *", problem, 3); Add(panel, "اولویت", priority, 4); Add(panel, "وضعیت", status, 5);
        Add(panel, "تاریخ ثبت (YYYY-MM-DD)", received, 6); Add(panel, "تاریخ اتمام", completed, 7);
        Add(panel, "تعمیرکار", technician, 8); Add(panel, "هزینه", cost, 9); Add(panel, "توضیحات", notes, 10);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var save = new Button { Text = "ذخیره", Width = 100 };
        save.Click += (_, _) => Save(); buttons.Controls.Add(save);
        var cancel = new Button { Text = "انصراف", Width = 100, DialogResult = DialogResult.Cancel }; buttons.Controls.Add(cancel);
        panel.Controls.Add(buttons, 1, 11); Controls.Add(panel);
        asset.Text=repair.AssetName; reporter.Text=repair.Reporter; phone.Text=repair.Phone; problem.Text=repair.Problem;
        priority.SelectedItem=repair.Priority; if (priority.SelectedIndex < 0) priority.SelectedItem="متوسط";
        status.SelectedItem=repair.Status; if (status.SelectedIndex < 0) status.SelectedItem="ثبت‌شده";
        received.Text=repair.ReceivedAt; completed.Text=repair.CompletedAt; technician.Text=repair.Technician;
        cost.Text=repair.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture); notes.Text=repair.Notes;
        AcceptButton = save; CancelButton = cancel;
    }

    private static void Add(TableLayoutPanel p, string label, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        if (control is TextBox tb) tb.Margin = new Padding(3, 4, 3, 4);
        p.Controls.Add(new Label { Text=label, AutoSize=true, Anchor=AnchorStyles.Left, Padding=new Padding(0,6,0,0) }, 0, row);
        p.Controls.Add(control, 1, row);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(asset.Text) || string.IsNullOrWhiteSpace(reporter.Text) || string.IsNullOrWhiteSpace(problem.Text))
        { MessageBox.Show("فیلدهای ستاره‌دار را تکمیل کنید."); return; }
        if (!double.TryParse(cost.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount)
            && !double.TryParse(cost.Text, out amount))
        { MessageBox.Show("هزینه باید عددی باشد."); return; }
        if (!DateTime.TryParse(received.Text, out var d)) { MessageBox.Show("تاریخ ثبت معتبر نیست."); return; }
        if (!string.IsNullOrWhiteSpace(completed.Text) && !DateTime.TryParse(completed.Text, out _))
        { MessageBox.Show("تاریخ اتمام معتبر نیست."); return; }
        item.AssetName=asset.Text; item.Reporter=reporter.Text; item.Phone=phone.Text; item.Problem=problem.Text;
        item.Priority=priority.Text; item.Status=status.Text; item.ReceivedAt=d.ToString("yyyy-MM-dd");
        item.CompletedAt=string.IsNullOrWhiteSpace(completed.Text) ? "" : DateTime.Parse(completed.Text).ToString("yyyy-MM-dd");
        item.Technician=technician.Text; item.Cost=amount; item.Notes=notes.Text;
        DialogResult=DialogResult.OK; Close();
    }
}
