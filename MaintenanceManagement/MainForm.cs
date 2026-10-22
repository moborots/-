using System.Drawing.Printing;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace MaintenanceManagement;

internal sealed class MainForm : Form
{
    private readonly DataGridView repairsGrid = MakeGrid();
    private readonly DataGridView stockGrid = MakeGrid();
    private readonly DataGridView usersGrid = MakeGrid();
    private readonly TextBox repairSearch = new() { Width=190 };
    private readonly TextBox stockSearch = new() { Width=190 };
    private readonly Label userLabel = new() { Dock=DockStyle.Left, Width=320, ForeColor=Color.WhiteSmoke, Font=new Font("Segoe UI",10), TextAlign=ContentAlignment.MiddleLeft };
    private readonly Label footer = new() { Dock=DockStyle.Bottom, Height=30, TextAlign=ContentAlignment.MiddleRight, Padding=new Padding(10,0,10,0), BackColor=Color.FromArgb(235,241,247), ForeColor=Color.FromArgb(22,52,82) };
    private List<Repair> repairs = new();
    private List<StockItem> stock = new();
    private List<(int Id,string Username,string Role,string CreatedAt)> users = new();

    private static DataGridView MakeGrid() => new() { Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false,
        SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor=Color.White, BorderStyle=BorderStyle.None, RowHeadersVisible=false, AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,
        DefaultCellStyle={ Font=new Font("Segoe UI",10), SelectionBackColor=Color.FromArgb(210,230,250), SelectionForeColor=Color.Black },
        ColumnHeadersDefaultCellStyle={ Font=new Font("Segoe UI",10,FontStyle.Bold), BackColor=Color.FromArgb(31,78,121), ForeColor=Color.White },
        EnableHeadersVisualStyles=false, GridColor=Color.FromArgb(225,230,235) };

    public MainForm()
    {
        Text="MOBOROOT | مدیریت تعمیرات و خدمات";
        KeyPreview=true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F12) { e.Handled=true; OpenLoginForRoleSwitch(); } };
        Width=1400; Height=800; MinimumSize=new Size(1050,650); StartPosition=FormStartPosition.CenterScreen;
        RightToLeft=RightToLeft.Yes; RightToLeftLayout=true; BackColor=Color.FromArgb(245,247,250);
        var header=new Panel { Dock=DockStyle.Top, Height=76, BackColor=Color.FromArgb(22,52,82), Padding=new Padding(20,12,20,8) };
        header.Controls.Add(new Label { Text="MOBOROOT | مدیریت تعمیرات و خدمات", Dock=DockStyle.Right, Width=420, ForeColor=Color.White,
            Font=new Font("Segoe UI",18,FontStyle.Bold), TextAlign=ContentAlignment.MiddleRight });
        userLabel.Text=$"کاربر: {Database.CurrentUsername}  |  نقش: {Database.CurrentRole}";
        header.Controls.Add(userLabel);
        var topActions = new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=48, FlowDirection=FlowDirection.RightToLeft, BackColor=Color.FromArgb(245,247,250), Padding=new Padding(8,4,8,4) };
        topActions.Controls.Add(Btn("ورود / تغییر کاربر (F12)", Color.FromArgb(110,75,150), OpenLoginForRoleSwitch));
        topActions.Controls.Add(Btn("دریافت و پرداخت", Color.FromArgb(0,130,100), OpenFinance));
        topActions.Controls.Add(Btn("مدیریت تعمیرکاران", Color.FromArgb(100,75,150), OpenTechnicians));
        topActions.Controls.Add(Btn("داشبورد آماری", Color.FromArgb(22,100,140), ShowDashboard));
        topActions.Controls.Add(Btn("پشتیبان‌گیری", Color.FromArgb(30,130,76), BackupDatabase));
        topActions.Controls.Add(Btn("بازیابی پشتیبان", Color.FromArgb(190,100,30), RestoreDatabase));
        var tabs=new TabControl { Dock=DockStyle.Fill, Font=new Font("Segoe UI",10), Padding=new Point(18,8) };
        var repairTab=new TabPage("درخواست‌های تعمیر") { BackColor=Color.White };
        var stockTab=new TabPage("کاردکس کالا و انبار") { BackColor=Color.White };
        var usersTab=new TabPage("مدیریت کاربران") { BackColor=Color.White };
        repairTab.Controls.Add(BuildRepairsPanel()); stockTab.Controls.Add(BuildStockPanel());
        if (Database.CurrentRole=="مدیر") { usersTab.Controls.Add(BuildUsersPanel()); tabs.TabPages.Add(usersTab); }
        tabs.TabPages.Add(repairTab); tabs.TabPages.Add(stockTab);
        Controls.Add(tabs); Controls.Add(topActions); Controls.Add(header); Controls.Add(footer);
        LoadRepairs(); LoadStock(); if(Database.CurrentRole=="مدیر") LoadUsers();
    }

    private static Button Btn(string text, Color color, Action action)
    {
        var b=new Button { Text=text, AutoSize=true, Height=38, MinimumSize=new Size(105,38), FlatStyle=FlatStyle.Flat,
            BackColor=color, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold), Cursor=Cursors.Hand, Margin=new Padding(4) };
        b.FlatAppearance.BorderSize=0; b.Click+=(_,_)=>action(); return b;
    }
    private static FlowLayoutPanel Toolbar() => new() { Dock=DockStyle.Top, Height=58, Padding=new Padding(8), WrapContents=false, AutoScroll=true, BackColor=Color.FromArgb(248,250,252) };

    private Control BuildRepairsPanel()
    {
        var panel=new Panel { Dock=DockStyle.Fill };
        var bar=Toolbar();
        bar.Controls.Add(Btn("＋ ثبت تعمیر",Color.FromArgb(30,130,76),()=>EditRepair(null)));
        bar.Controls.Add(Btn("✎ ویرایش",Color.FromArgb(36,100,170),EditSelectedRepair));
        bar.Controls.Add(Btn("🗑 حذف",Color.FromArgb(190,55,55),DeleteRepair));
        bar.Controls.Add(Btn("▤ صدور فاکتور PDF",Color.FromArgb(100,75,160),ExportRepairInvoicePdf));
        bar.Controls.Add(repairSearch);
        bar.Controls.Add(Btn("⌕ جست‌وجو",Color.FromArgb(70,85,100),LoadRepairs));
        panel.Controls.Add(repairsGrid); panel.Controls.Add(bar); return panel;
    }
    private Control BuildStockPanel()
    {
        var panel=new Panel { Dock=DockStyle.Fill }; var bar=Toolbar();
        bar.Controls.Add(Btn("＋ تعریف کالا",Color.FromArgb(30,130,76),()=>EditStock(null)));
        bar.Controls.Add(Btn("✎ ویرایش کالا",Color.FromArgb(36,100,170),EditSelectedStock));
        bar.Controls.Add(Btn("⬆ ورود به انبار",Color.FromArgb(0,130,130),()=>MoveStock(true)));
        bar.Controls.Add(Btn("⬇ خروج از انبار",Color.FromArgb(190,100,30),()=>MoveStock(false)));
        bar.Controls.Add(Btn("▤ کاردکس",Color.FromArgb(100,75,160),ShowCardex));
        bar.Controls.Add(Btn("🗑 حذف کالا",Color.FromArgb(190,55,55),DeleteStock));
        bar.Controls.Add(stockSearch); bar.Controls.Add(Btn("⌕ جست‌وجو",Color.FromArgb(70,85,100),LoadStock));
        panel.Controls.Add(stockGrid); panel.Controls.Add(bar); return panel;
    }
    private Control BuildUsersPanel()
    {
        var panel=new Panel { Dock=DockStyle.Fill }; var bar=Toolbar();
        bar.Controls.Add(Btn("＋ افزودن کاربر",Color.FromArgb(30,130,76),AddUser));
        bar.Controls.Add(Btn("🗑 حذف کاربر",Color.FromArgb(190,55,55),DeleteUser));
        panel.Controls.Add(usersGrid); panel.Controls.Add(bar); return panel;
    }

    private void OpenLoginForRoleSwitch()
    {
        using var f = new LoginForm(loginOnly: true);
        if (f.ShowDialog(this) == DialogResult.OK)
        {
            userLabel.Text = $"کاربر: {Database.CurrentUsername}  |  نقش: {Database.CurrentRole}";
            MessageBox.Show($"ورود انجام شد. کاربر: {Database.CurrentUsername}، نقش: {Database.CurrentRole}\nبرای اعمال کامل دسترسی‌های نقش جدید، برنامه را ببندید و دوباره با این کاربر وارد شوید.", "MOBOROOT");
        }
    }
    private void OpenFinance()
    {
        using var f = new ManagementFinanceForm(financeOnly: true);
        f.ShowDialog(this);
    }
    private void OpenTechnicians()
    {
        using var f = new ManagementFinanceForm(techniciansOnly: true);
        f.ShowDialog(this);
    }

    private void BackupDatabase()
    {
        try { var path=Database.CreateBackup(); MessageBox.Show("پشتیبان با موفقیت ذخیره شد:\n"+path,"MOBOROOT",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        catch(Exception ex) { MessageBox.Show("پشتیبان‌گیری ناموفق بود: "+ex.Message); }
    }
    private void RestoreDatabase()
    {
        using var d=new OpenFileDialog { Title="انتخاب فایل پشتیبان MOBOROOT", Filter="SQLite backup (*.db;*.sqlite)|*.db;*.sqlite|All files (*.*)|*.*", InitialDirectory=Database.BackupDirectory };
        if(d.ShowDialog(this)!=DialogResult.OK) return;
        if(MessageBox.Show("بازیابی، اطلاعات فعلی را با نسخه پشتیبان جایگزین می‌کند. ابتدا پشتیبان فعلی گرفته شود؟","تأیید بازیابی",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
        try { Database.CreateBackup(); Database.RestoreBackup(d.FileName); MessageBox.Show("بازیابی انجام شد. برنامه را ببندید و دوباره اجرا کنید."); }
        catch(Exception ex) { MessageBox.Show("بازیابی ناموفق بود: "+ex.Message); }
    }
    private void ShowDashboard()
    {
        try { var d=Database.GetDashboard();
            using var f=new Form { Text="داشبورد مدیریتی MOBOROOT", Width=600, Height=360, StartPosition=FormStartPosition.CenterParent, RightToLeft=RightToLeft.Yes, BackColor=Color.White };
            var p=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(24), ColumnCount=2, RowCount=5 };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
            void Add(string name,string value,int row) { p.Controls.Add(new Label { Text=name, AutoSize=true, Font=new Font("Segoe UI",11), Padding=new Padding(0,8,0,8) },0,row); p.Controls.Add(new Label { Text=value, AutoSize=true, Font=new Font("Segoe UI",11,FontStyle.Bold), ForeColor=Color.FromArgb(22,100,140), Padding=new Padding(0,8,0,8) },1,row); }
            Add("کل پذیرش‌ها",d.repairs.ToString(),0); Add("در حال انجام",d.active.ToString(),1); Add("تکمیل‌شده",d.completed.ToString(),2); Add("اقلام نیازمند تأمین",d.lowStock.ToString(),3); Add("درآمد تعمیرات تکمیل‌شده",d.revenue.ToString("N0")+" ریال",4);
            f.Controls.Add(p); f.ShowDialog(this);
        } catch(Exception ex) { MessageBox.Show("نمایش داشبورد ممکن نیست: "+ex.Message); }
    }

    private void LoadRepairs()
    {
        repairs=Database.GetRepairs(repairSearch.Text);
        repairsGrid.DataSource=repairs.Select(x=>new { x.Id, تجهیز=x.AssetName, درخواست_دهنده=x.Reporter, تلفن=x.Phone,
            شرح_خرابی=x.Problem, اولویت=x.Priority, وضعیت=x.Status, تاریخ_ثبت=x.ReceivedAt, تاریخ_اتمام=x.CompletedAt,
            تعمیرکار=x.Technician, هزینه=x.Cost, توضیحات=x.Notes }).ToList();
        footer.Text=$"تعداد درخواست‌ها: {repairs.Count}   |   کاربر فعال: {Database.CurrentUsername}";
    }
    private Repair? SelectedRepair()
    {
        if(repairsGrid.CurrentRow==null) return null;
        var v=repairsGrid.CurrentRow.Cells[0].Value;
        return v==null?null:repairs.FirstOrDefault(x=>x.Id==Convert.ToInt32(v));
    }
    private void EditSelectedRepair(){var x=SelectedRepair(); if(x==null){MessageBox.Show("یک درخواست را انتخاب کنید.");return;} EditRepair(x);}
    private void EditRepair(Repair? original)
    {
        var x=original==null?new Repair():new Repair { Id=original.Id,AssetName=original.AssetName,Reporter=original.Reporter,Phone=original.Phone,
            Problem=original.Problem,Priority=original.Priority,Status=original.Status,ReceivedAt=original.ReceivedAt,CompletedAt=original.CompletedAt,
            Technician=original.Technician,Cost=original.Cost,Notes=original.Notes };
        using var f=new RepairForm(x); if(f.ShowDialog(this)==DialogResult.OK){try{Database.SaveRepair(x);LoadRepairs();}catch(Exception ex){MessageBox.Show("ذخیره انجام نشد: "+ex.Message);}}
    }
    private void DeleteRepair(){var x=SelectedRepair();if(x==null){MessageBox.Show("یک درخواست را انتخاب کنید.");return;}
        if(MessageBox.Show("این درخواست حذف شود؟","تأیید",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes){Database.DeleteRepair(x.Id);LoadRepairs();}}
    private void ExportRepairInvoicePdf()
    {
        var x=SelectedRepair();
        if(x==null){MessageBox.Show("برای صدور فاکتور، ابتدا یک درخواست تعمیر را از جدول انتخاب کنید.","موبوروت",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var dialog=new SaveFileDialog { Filter="PDF (*.pdf)|*.pdf", FileName=$"Moboroot-Repair-Invoice-{x.Id}-{DateTime.Now:yyyyMMdd}.pdf", Title="ذخیره فاکتور موبوروت به صورت PDF", DefaultExt="pdf", AddExtension=true };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            using var document=new System.Drawing.Printing.PrintDocument();
            var pdfPrinter=PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault(n=>n.Contains("Microsoft Print to PDF",StringComparison.OrdinalIgnoreCase));
            if(string.IsNullOrWhiteSpace(pdfPrinter))
            {
                MessageBox.Show("چاپگر Microsoft Print to PDF در ویندوز فعال نیست. از Settings > Optional features > More Windows features آن را فعال کنید.","موبوروت",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;
            }
            document.PrinterSettings.PrinterName=pdfPrinter;
            document.PrinterSettings.PrintToFile=true;
            document.PrinterSettings.PrintFileName=dialog.FileName;
            document.DefaultPageSettings.Landscape=false;
            document.DefaultPageSettings.Margins=new System.Drawing.Printing.Margins(55,55,55,55);
            document.PrintPage += (_,e) =>
            {
                var g=e.Graphics!; var bounds=e.MarginBounds; float y=bounds.Top;
                using var titleFont=new Font("Segoe UI",22,FontStyle.Bold);
                using var headingFont=new Font("Segoe UI",13,FontStyle.Bold);
                using var bodyFont=new Font("Segoe UI",10,FontStyle.Regular);
                using var smallFont=new Font("Segoe UI",8,FontStyle.Regular);
                using var blue=new SolidBrush(Color.FromArgb(22,52,82));
                using var ink=new SolidBrush(Color.FromArgb(35,45,55));
                using var linePen=new Pen(Color.FromArgb(205,215,225),1);
                using var rtl=new StringFormat { Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.DirectionRightToLeft };
                using var center=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.DirectionRightToLeft };
                g.FillRectangle(blue,bounds.Left,y,bounds.Width,78);
                g.DrawString("موبوروت | MOBOROOT",titleFont,Brushes.White,new RectangleF(bounds.Left+14,y+7,bounds.Width-28,38),rtl);
                g.DrawString("فاکتور خدمات و تعمیرات",headingFont,Brushes.White,new RectangleF(bounds.Left+14,y+43,bounds.Width-28,27),rtl);
                y+=98;
                g.DrawString($"شماره فاکتور: MR-{x.Id:00000}",headingFont,blue,new RectangleF(bounds.Left,y,bounds.Width,28),rtl); y+=36;
                g.DrawLine(linePen,bounds.Left,y,bounds.Right,y); y+=12;
                void Field(string label,string value)
                {
                    g.DrawString(label,headingFont,blue,new RectangleF(bounds.Left,y,150,28),rtl);
                    g.DrawString(string.IsNullOrWhiteSpace(value)?"—":value,bodyFont,ink,new RectangleF(bounds.Left+155,y,bounds.Width-155,28),rtl); y+=34;
                }
                Field("تاریخ صدور:",DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                Field("نام تجهیز:",x.AssetName); Field("درخواست‌دهنده:",x.Reporter); Field("تلفن:",x.Phone);
                Field("تعمیرکار:",x.Technician); Field("اولویت:",x.Priority); Field("وضعیت:",x.Status);
                y+=5; g.DrawString("شرح خرابی / خدمات",headingFont,blue,new RectangleF(bounds.Left,y,bounds.Width,28),rtl); y+=32;
                var problem=string.IsNullOrWhiteSpace(x.Problem)?"—":x.Problem;
                var problemSize=g.MeasureString(problem,bodyFont,bounds.Width-24,new StringFormat(StringFormatFlags.DirectionRightToLeft));
                g.DrawRectangle(linePen,bounds.Left,y,bounds.Width,Math.Max(54,problemSize.Height+20));
                g.DrawString(problem,bodyFont,ink,new RectangleF(bounds.Left+10,y+8,bounds.Width-20,Math.Max(38,problemSize.Height+4)),rtl); y+=Math.Max(66,problemSize.Height+32);
                Field("تاریخ ثبت:",x.ReceivedAt); Field("تاریخ اتمام:",x.CompletedAt);
                y+=12; g.FillRectangle(new SolidBrush(Color.FromArgb(235,242,248)),bounds.Left,y,bounds.Width,52);
                g.DrawString("مبلغ خدمات / هزینه تعمیر",headingFont,blue,new RectangleF(bounds.Left+10,y+4,bounds.Width-20,22),rtl);
                g.DrawString(x.Cost.ToString("N0")+" ریال",headingFont,blue,new RectangleF(bounds.Left+10,y+27,bounds.Width-20,22),rtl); y+=68;
                if(!string.IsNullOrWhiteSpace(x.Notes)){g.DrawString("توضیحات: "+x.Notes,bodyFont,ink,new RectangleF(bounds.Left,y,bounds.Width,55),rtl);y+=60;}
                g.DrawLine(linePen,bounds.Left,bounds.Bottom-35,bounds.Right,bounds.Bottom-35);
                g.DrawString("از انتخاب شما سپاسگزاریم | موبوروت",smallFont,ink,new RectangleF(bounds.Left,bounds.Bottom-30,bounds.Width,22),center);
                e.HasMorePages=false;
            };
            document.Print();
            MessageBox.Show("فاکتور PDF موبوروت ذخیره شد.\n"+dialog.FileName,"موبوروت",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex){MessageBox.Show("صدور PDF انجام نشد: "+ex.Message,"موبوروت",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }

    private void LoadStock()
    {
        stock=Database.GetInventory(stockSearch.Text);
        stockGrid.DataSource=stock.Select(x=>new { x.Id, کد_کالا=x.Code, نام_کالا=x.Name, گروه=x.Category, واحد=x.Unit,
            موجودی=x.Quantity, حداقل_موجودی=x.MinQuantity, محل_نگهداری=x.Location, توضیحات=x.Notes,
            وضعیت=x.Quantity<=x.MinQuantity?"نیاز به تأمین":"موجود" }).ToList();
        if(stockGrid.Columns["موجودی"]!=null) foreach(DataGridViewRow row in stockGrid.Rows)
            if(Convert.ToDouble(row.Cells["موجودی"].Value)<=Convert.ToDouble(row.Cells["حداقل_موجودی"].Value)) row.DefaultCellStyle.BackColor=Color.MistyRose;
        footer.Text=$"تعداد اقلام: {stock.Count}   |   اقلام کم‌موجود: {stock.Count(x=>x.Quantity<=x.MinQuantity)}";
    }
    private StockItem? SelectedStock(){if(stockGrid.CurrentRow==null)return null;var v=stockGrid.CurrentRow.Cells[0].Value;return v==null?null:stock.FirstOrDefault(x=>x.Id==Convert.ToInt32(v));}
    private void EditSelectedStock(){var x=SelectedStock();if(x==null){MessageBox.Show("یک کالا را انتخاب کنید.");return;}EditStock(x);}
    private void EditStock(StockItem? original)
    {
        var x=original==null?new StockItem():new StockItem{Id=original.Id,Code=original.Code,Name=original.Name,Category=original.Category,
            Unit=original.Unit,Quantity=original.Quantity,MinQuantity=original.MinQuantity,Location=original.Location,Notes=original.Notes};
        using var f=new StockForm(x,original==null); if(f.ShowDialog(this)==DialogResult.OK)
            try{Database.SaveStockItem(x);LoadStock();}catch(SqliteException ex){MessageBox.Show(ex.SqliteErrorCode==19?"کد کالا تکراری است.":ex.Message);}
    }
    private void MoveStock(bool incoming)
    {
        var x=SelectedStock();if(x==null){MessageBox.Show("یک کالا را انتخاب کنید.");return;}
        using var f=new StockMovementForm(x,incoming);if(f.ShowDialog(this)==DialogResult.OK)
            try{Database.MoveStock(x.Id,f.Quantity,incoming,f.Description);LoadStock();}catch(Exception ex){MessageBox.Show(ex.Message);}
    }
    private void DeleteStock()
    {
        var x=SelectedStock();if(x==null){MessageBox.Show("یک کالا را انتخاب کنید.");return;}
        if(MessageBox.Show($"کالای «{x.Name}» حذف شود؟","تأیید حذف",MessageBoxButtons.YesNo)==DialogResult.Yes)
            try{Database.DeleteStockItem(x.Id);LoadStock();}catch(Exception ex){MessageBox.Show("به دلیل وجود سابقه کاردکس، حذف ممکن نیست. "+ex.Message);}
    }
    private void ShowCardex()
    {
        var x=SelectedStock();if(x==null){MessageBox.Show("یک کالا را انتخاب کنید.");return;}
        var data=Database.GetStockMovements(x.Id).Select(m=>new{کد=m.Code,کالا=m.Name,نوع_گردش=m.Type,مقدار=m.Quantity,تاریخ=m.Date,شرح=m.Description,کاربر=m.User}).ToList();
        using var f=new Form{Text=$"کاردکس کالا: {x.Name}",Width=900,Height=500,StartPosition=FormStartPosition.CenterParent,RightToLeft=RightToLeft.Yes};
        var g=MakeGrid();g.DataSource=data;f.Controls.Add(g);f.ShowDialog(this);
    }
    private void LoadUsers(){users=Database.GetUsers();usersGrid.DataSource=users.Select(x=>new{شناسه=x.Id,نام_کاربری=x.Username,نقش=x.Role,تاریخ_ایجاد=x.CreatedAt}).ToList();}
    private void AddUser()
    {
        using var f=new AddUserForm();if(f.ShowDialog(this)!=DialogResult.OK)return;
        try{if(!Database.AddUser(f.Username,f.Password,f.Role))MessageBox.Show("این نام کاربری قبلاً وجود دارد.");else{LoadUsers();MessageBox.Show("کاربر با موفقیت اضافه شد.");}}
        catch(Exception ex){MessageBox.Show(ex.Message);}
    }
    private void DeleteUser()
    {
        if(usersGrid.CurrentRow==null){MessageBox.Show("یک کاربر را انتخاب کنید.");return;}
        var id=Convert.ToInt32(usersGrid.CurrentRow.Cells[0].Value);var name=Convert.ToString(usersGrid.CurrentRow.Cells[1].Value);
        if(name==Database.CurrentUsername){MessageBox.Show("نمی‌توانید حساب واردشده را حذف کنید.");return;}
        if(MessageBox.Show($"کاربر {name} حذف شود؟","تأیید",MessageBoxButtons.YesNo)==DialogResult.Yes){Database.DeleteUser(id);LoadUsers();}
    }
}
