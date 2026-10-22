namespace MaintenanceManagement;

internal sealed class StockForm : Form
{
    private readonly TextBox code=new(), nameBox=new(), category=new(), unit=new(), quantity=new(), minQty=new(), location=new(), notes=new();
    private readonly bool isNew;
    private readonly StockItem item;
    public StockForm(StockItem x,bool newItem)
    {
        item=x;isNew=newItem;Text=newItem?"تعریف کالای جدید":"ویرایش مشخصات کالا";Width=520;Height=510;
        StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=9,AutoScroll=true};
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,125));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Add(p,"کد کالا *",code,0);Add(p,"نام کالا *",nameBox,1);Add(p,"گروه کالا",category,2);Add(p,"واحد",unit,3);
        Add(p,"موجودی اولیه",quantity,4);Add(p,"حداقل موجودی",minQty,5);Add(p,"محل نگهداری",location,6);Add(p,"توضیحات",notes,7);
        var b=new Button{Text="ذخیره",Height=36,BackColor=Color.FromArgb(30,130,76),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        b.Click+=(_,_)=>Save();p.Controls.Add(b,1,8);Controls.Add(p);
        code.Text=x.Code;nameBox.Text=x.Name;category.Text=x.Category;unit.Text=x.Unit;quantity.Text=x.Quantity.ToString();minQty.Text=x.MinQuantity.ToString();location.Text=x.Location;notes.Text=x.Notes;
        code.Enabled=newItem;quantity.Enabled=newItem;
    }
    private static void Add(TableLayoutPanel p,string label,Control c,int row){c.Dock=DockStyle.Fill;p.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);p.Controls.Add(c,1,row);}
    private void Save()
    {
        if(string.IsNullOrWhiteSpace(code.Text)||string.IsNullOrWhiteSpace(nameBox.Text)){MessageBox.Show("کد و نام کالا الزامی است.");return;}
        if(!double.TryParse(quantity.Text,out var q)||q<0||!double.TryParse(minQty.Text,out var m)||m<0){MessageBox.Show("موجودی و حداقل موجودی باید عدد مثبت یا صفر باشند.");return;}
        item.Code=code.Text.Trim();item.Name=nameBox.Text.Trim();item.Category=category.Text.Trim();item.Unit=string.IsNullOrWhiteSpace(unit.Text)?"عدد":unit.Text.Trim();
        item.Quantity=q;item.MinQuantity=m;item.Location=location.Text.Trim();item.Notes=notes.Text.Trim();DialogResult=DialogResult.OK;Close();
    }
}
internal sealed class StockMovementForm : Form
{
    private readonly NumericUpDown amount=new(){Minimum=0.01M,Maximum=100000000,DecimalPlaces=2,Value=1,Width=220};
    private readonly TextBox description=new(){Width=220,Multiline=true,Height=70};
    public double Quantity=> (double)amount.Value;
    public string Description=>description.Text;
    public StockMovementForm(StockItem item,bool incoming)
    {
        Text=incoming?"ورود کالا به انبار":"خروج کالا از انبار";Width=430;Height=260;StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=3};
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        p.Controls.Add(new Label{Text=$"{item.Name} ({item.Unit})",AutoSize=true},0,0);p.Controls.Add(amount,1,0);
        p.Controls.Add(new Label{Text="شرح گردش",AutoSize=true},0,1);p.Controls.Add(description,1,1);
        var b=new Button{Text="ثبت گردش",Height=35,BackColor=incoming?Color.SeaGreen:Color.DarkOrange,ForeColor=Color.White};
        b.Click+=(_,_)=>{DialogResult=DialogResult.OK;Close();};p.Controls.Add(b,1,2);Controls.Add(p);
    }
}
internal sealed class AddUserForm : Form
{
    private readonly TextBox userBox=new(),passBox=new(){UseSystemPasswordChar=true};
    private readonly ComboBox roleBox=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    public string Username=>userBox.Text.Trim(); public string Password=>passBox.Text; public string Role=>roleBox.Text;
    public AddUserForm(string initialRole = "مدیر")
    {
        Text="افزودن کاربر";Width=460;Height=300;StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;
        roleBox.Items.AddRange(new object[]{"مدیر","پذیرش","تعمیرکار","انباردار"});roleBox.SelectedItem=initialRole; if(roleBox.SelectedIndex<0) roleBox.SelectedIndex=0;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,RowCount=4};
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Add(p,"نام کاربری",userBox,0);Add(p,"رمز عبور",passBox,1);Add(p,"نقش",roleBox,2);
        var b=new Button{Text="ایجاد کاربر",Height=36,BackColor=Color.SeaGreen,ForeColor=Color.White};
        b.Click+=(_,_)=>{if(Username.Length<3||Password.Length<8){MessageBox.Show("نام کاربری حداقل ۳ و رمز عبور حداقل ۸ نویسه باشد.");return;}DialogResult=DialogResult.OK;Close();};
        p.Controls.Add(b,1,3);Controls.Add(p);
    }
    private static void Add(TableLayoutPanel p,string label,Control c,int row){c.Dock=DockStyle.Fill;p.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);p.Controls.Add(c,1,row);}
}


internal sealed class ManagementFinanceForm : Form
{
    private readonly DataGridView grid = new() { Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor=Color.White, RowHeadersVisible=false };
    private readonly Label totals = new() { Dock=DockStyle.Bottom, Height=38, TextAlign=ContentAlignment.MiddleRight, Padding=new Padding(10), Font=new Font("Segoe UI",10,FontStyle.Bold) };
    public ManagementFinanceForm(bool financeOnly = false, bool techniciansOnly = false)
    {
        Text=financeOnly ? "MOBOROOT | دریافت و پرداخت" : techniciansOnly ? "MOBOROOT | مدیریت تعمیرکاران" : "MOBOROOT | امور مالی و تعمیرکاران"; Width=1100; Height=650; MinimumSize=new Size(850,500); StartPosition=FormStartPosition.CenterParent; RightToLeft=RightToLeft.Yes; RightToLeftLayout=true; BackColor=Color.White;
        var bar=new FlowLayoutPanel { Dock=DockStyle.Top, Height=55, Padding=new Padding(8), FlowDirection=FlowDirection.RightToLeft, WrapContents=false, AutoScroll=true, BackColor=Color.FromArgb(245,247,250) };
        if (!techniciansOnly)
        {
            bar.Controls.Add(MakeButton("＋ ثبت دریافت",Color.SeaGreen,()=>AddTransaction("دریافت")));
            bar.Controls.Add(MakeButton("－ ثبت پرداخت",Color.DarkOrange,()=>AddTransaction("پرداخت")));
            bar.Controls.Add(MakeButton("↻ به‌روزرسانی",Color.SteelBlue,LoadTransactions));
        }
        if (!financeOnly)
        {
            bar.Controls.Add(MakeButton("＋ تعریف تعمیرکار",Color.FromArgb(100,75,160),AddTechnician));
            bar.Controls.Add(MakeButton("مدیریت کاربران",Color.FromArgb(60,80,100),ManageUsers));
        }
        Controls.Add(grid); if(!techniciansOnly) Controls.Add(totals); Controls.Add(bar);
        if(techniciansOnly) LoadTechnicians(); else LoadTransactions();
    }
    private static Button MakeButton(string text,Color color,Action action)
    {
        var b=new Button{Text=text,AutoSize=true,Height=36,BackColor=color,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(4)}; b.FlatAppearance.BorderSize=0; b.Click+=(_,_)=>action(); return b;
    }
    private void LoadTechnicians()
    {
        var rows=Database.GetUsers().Where(x=>x.Role=="تعمیرکار").ToList();
        grid.DataSource=rows.Select(x=>new{شناسه=x.Id,نام_کاربری=x.Username,نقش=x.Role,تاریخ_ایجاد=x.CreatedAt}).ToList();
        totals.Text=$"تعداد تعمیرکاران: {rows.Count}";
    }
    private void LoadTransactions()
    {
        var rows=Database.GetFinanceTransactions();
        grid.DataSource=rows.Select(x=>new{شناسه=x.Id,نوع=x.Type,مبلغ_ریال=x.Amount.ToString("N0"),دسته=x.Category,تاریخ=x.Date,طرف_حساب=x.Person,شرح=x.Description,کاربر=x.User}).ToList();
        var income=rows.Where(x=>x.Type=="دریافت").Sum(x=>x.Amount); var expense=rows.Where(x=>x.Type=="پرداخت").Sum(x=>x.Amount);
        totals.Text=$"جمع دریافت: {income:N0} ریال     |     جمع پرداخت: {expense:N0} ریال     |     مانده: {income-expense:N0} ریال";
    }
    private void AddTransaction(string type)
    {
        using var f=new FinanceTransactionForm(type); if(f.ShowDialog(this)!=DialogResult.OK)return;
        try { Database.AddFinanceTransaction(type,f.Amount,f.Category,f.TxDate,f.Description,f.Person); LoadTransactions(); }
        catch(Exception ex) { MessageBox.Show("ثبت تراکنش ناموفق بود: "+ex.Message); }
    }
    private void AddTechnician()
    {
        using var f=new AddUserForm("تعمیرکار"); if(f.ShowDialog(this)!=DialogResult.OK)return;
        try { if(!Database.AddUser(f.Username,f.Password,"تعمیرکار")) MessageBox.Show("این نام کاربری قبلاً وجود دارد."); else MessageBox.Show("حساب تعمیرکار ایجاد شد."); }
        catch(Exception ex) { MessageBox.Show(ex.Message); }
    }
    private void ManageUsers()
    {
        var rows=Database.GetUsers();
        using var f=new Form{Text="فهرست کاربران و تعمیرکاران",Width=800,Height=450,StartPosition=FormStartPosition.CenterParent,RightToLeft=RightToLeft.Yes};
        var g=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows.Select(x=>new{شناسه=x.Id,نام_کاربری=x.Username,نقش=x.Role,تاریخ_ایجاد=x.CreatedAt}).ToList()}; f.Controls.Add(g); f.ShowDialog(this);
    }
}

internal sealed class FinanceTransactionForm : Form
{
    private readonly TextBox amountBox=new(){Width=250};
    private readonly TextBox categoryBox=new(){Width=250};
    private readonly TextBox personBox=new(){Width=250};
    private readonly TextBox descriptionBox=new(){Width=250,Multiline=true,Height=75};
    private readonly DateTimePicker dateBox=new(){Width=250,Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm"};
    public double Amount=>double.Parse(amountBox.Text.Trim(),System.Globalization.NumberStyles.Number);
    public string Category=>categoryBox.Text.Trim(); public string Person=>personBox.Text.Trim(); public string Description=>descriptionBox.Text.Trim(); public string TxDate=>dateBox.Value.ToString("yyyy-MM-dd HH:mm");
    public FinanceTransactionForm(string type)
    {
        Text="ثبت "+type; Width=500; Height=390; StartPosition=FormStartPosition.CenterParent; RightToLeft=RightToLeft.Yes; RightToLeftLayout=true;
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=6}; p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        void Add(string label,Control c,int row){c.Dock=DockStyle.Fill;p.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);p.Controls.Add(c,1,row);}
        Add("مبلغ (ریال) *",amountBox,0); Add("دسته‌بندی *",categoryBox,1); Add("تاریخ",dateBox,2); Add("طرف حساب",personBox,3); Add("شرح",descriptionBox,4);
        var b=new Button{Text="ثبت "+type,Height=36,BackColor=type=="دریافت"?Color.SeaGreen:Color.DarkOrange,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        b.Click+=(_,_)=>{if(!double.TryParse(amountBox.Text.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.CurrentCulture,out var a) && !double.TryParse(amountBox.Text.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out a)){MessageBox.Show("مبلغ را به‌صورت عدد وارد کنید.");return;} if(a<=0||string.IsNullOrWhiteSpace(categoryBox.Text)){MessageBox.Show("مبلغ مثبت و دسته‌بندی الزامی است.");return;} DialogResult=DialogResult.OK;Close();}; p.Controls.Add(b,1,5); Controls.Add(p);
    }
}
