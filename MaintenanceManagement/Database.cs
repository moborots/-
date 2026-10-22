using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace MaintenanceManagement;

internal static class Database
{
    public static readonly string AppDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOBOROOT");
    public static readonly string DatabasePath = Path.Combine(AppDirectory, "maintenance.db");
    public static readonly string AttachmentsDirectory = Path.Combine(AppDirectory, "Attachments");
    public static readonly string LogsDirectory = Path.Combine(AppDirectory, "Logs");
    public static string BackupDirectory { get; private set; } = Path.Combine(AppDirectory, "Backups");
    private static string ConnectionString => $"Data Source={DatabasePath}";

    public static void Initialize()
    {
        try { Directory.CreateDirectory(@"C:\موبوروت"); BackupDirectory = @"C:\موبوروت\Backups"; Directory.CreateDirectory(BackupDirectory); }
        catch { BackupDirectory = Path.Combine(AppDirectory, "Backups"); Directory.CreateDirectory(BackupDirectory); }
        Directory.CreateDirectory(AppDirectory);
        Directory.CreateDirectory(AttachmentsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS Users(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Salt TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS Inventory(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Category TEXT,
                Unit TEXT NOT NULL DEFAULT 'عدد',
                Quantity REAL NOT NULL DEFAULT 0,
                MinQuantity REAL NOT NULL DEFAULT 0,
                Location TEXT,
                Notes TEXT
            );
            CREATE TABLE IF NOT EXISTS StockMovements(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ItemId INTEGER NOT NULL,
                MovementType TEXT NOT NULL,
                Quantity REAL NOT NULL,
                MovementDate TEXT NOT NULL,
                Description TEXT,
                Username TEXT,
                FOREIGN KEY(ItemId) REFERENCES Inventory(Id)
            );
            CREATE TABLE IF NOT EXISTS Repairs(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AssetName TEXT NOT NULL,
                Reporter TEXT NOT NULL,
                Phone TEXT,
                Problem TEXT NOT NULL,
                Priority TEXT NOT NULL,
                Status TEXT NOT NULL,
                ReceivedAt TEXT NOT NULL,
                CompletedAt TEXT,
                Technician TEXT,
                Cost REAL NOT NULL DEFAULT 0,
                Notes TEXT
            );
            CREATE TABLE IF NOT EXISTS Customers(Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL, Phone TEXT, Email TEXT, Address TEXT, Notes TEXT, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE IF NOT EXISTS RepairStatusHistory(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, OldStatus TEXT, NewStatus TEXT NOT NULL, ChangedAt TEXT NOT NULL, Username TEXT, Notes TEXT, FOREIGN KEY(RepairId) REFERENCES Repairs(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS RepairChecklist(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, Item TEXT NOT NULL, Phase TEXT NOT NULL, IsDone INTEGER NOT NULL DEFAULT 0, Notes TEXT, FOREIGN KEY(RepairId) REFERENCES Repairs(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS RepairAttachments(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, FilePath TEXT NOT NULL, FileName TEXT NOT NULL, AddedAt TEXT NOT NULL, FOREIGN KEY(RepairId) REFERENCES Repairs(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS Estimates(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, Serial TEXT NOT NULL UNIQUE, PartsAmount REAL NOT NULL DEFAULT 0, LaborAmount REAL NOT NULL DEFAULT 0, TotalAmount REAL NOT NULL DEFAULT 0, Approved INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, ApprovedAt TEXT, FOREIGN KEY(RepairId) REFERENCES Repairs(Id));
            CREATE TABLE IF NOT EXISTS Purchases(Id INTEGER PRIMARY KEY AUTOINCREMENT, ItemId INTEGER NOT NULL, Quantity REAL NOT NULL, UnitCost REAL NOT NULL DEFAULT 0, Supplier TEXT, InvoiceNo TEXT, PurchaseDate TEXT NOT NULL, Notes TEXT, FOREIGN KEY(ItemId) REFERENCES Inventory(Id));
            CREATE TABLE IF NOT EXISTS RepairParts(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, ItemId INTEGER NOT NULL, Quantity REAL NOT NULL, UnitCost REAL NOT NULL DEFAULT 0, UsedAt TEXT NOT NULL, FOREIGN KEY(RepairId) REFERENCES Repairs(Id), FOREIGN KEY(ItemId) REFERENCES Inventory(Id));
            CREATE TABLE IF NOT EXISTS Invoices(Id INTEGER PRIMARY KEY AUTOINCREMENT, RepairId INTEGER NOT NULL, Serial TEXT NOT NULL UNIQUE, TotalAmount REAL NOT NULL, CreatedAt TEXT NOT NULL, FOREIGN KEY(RepairId) REFERENCES Repairs(Id));
            CREATE TABLE IF NOT EXISTS FinanceTransactions(Id INTEGER PRIMARY KEY AUTOINCREMENT, TxType TEXT NOT NULL, Amount REAL NOT NULL, Category TEXT NOT NULL, TxDate TEXT NOT NULL, Description TEXT, RelatedPerson TEXT, Username TEXT);
            """;
        cmd.ExecuteNonQuery();

        try
        {
            using var migration = connection.CreateCommand();
            migration.CommandText = "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'مدیر'";
            migration.ExecuteNonQuery();
        }
        catch (SqliteException) { /* Role column already exists. */ }

        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Users";
        if (Convert.ToInt64(count.ExecuteScalar()) == 0)
        {
            var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            using var add = connection.CreateCommand();
            add.CommandText = "INSERT INTO Users(Username, PasswordHash, Salt, Role) VALUES($u,$p,$s,'مدیر')";
            add.Parameters.AddWithValue("$u", "admin");
            add.Parameters.AddWithValue("$p", HashPassword("Admin@123", salt));
            add.Parameters.AddWithValue("$s", salt);
            add.ExecuteNonQuery();
            Log("Created initial admin account.");
        }
    }

    public static string HashPassword(string password, string salt)
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(salt), 120_000,
            HashAlgorithmName.SHA256, 32);
        return Convert.ToHexString(bytes);
    }

    public static bool Authenticate(string username, string password)
    {
        using var c = new SqliteConnection(ConnectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash, Salt, Role FROM Users WHERE Username=$u";
        cmd.Parameters.AddWithValue("$u", username.Trim());
        using var r = cmd.ExecuteReader();
        if (!r.Read()) { Log($"Failed login for username: {username.Trim()}"); return false; }
        var ok = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(r.GetString(0)),
            Convert.FromHexString(HashPassword(password, r.GetString(1))));
        if (ok) { CurrentUsername = username.Trim(); CurrentRole = r.IsDBNull(2) ? "کاربر" : r.GetString(2); }
        Log(ok ? $"Successful login: {username.Trim()}" : $"Failed login for username: {username.Trim()}");
        return ok;
    }

    public static List<Repair> GetRepairs(string search = "")
    {
        var list = new List<Repair>();
        using var c = new SqliteConnection(ConnectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT Id,AssetName,Reporter,Phone,Problem,Priority,Status,ReceivedAt,CompletedAt,Technician,Cost,Notes
            FROM Repairs
            WHERE AssetName LIKE $q OR Reporter LIKE $q OR Problem LIKE $q OR Status LIKE $q
            ORDER BY Id DESC
            """;
        cmd.Parameters.AddWithValue("$q", "%" + search.Trim() + "%");
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Repair {
                Id=r.GetInt32(0), AssetName=r.GetString(1), Reporter=r.GetString(2),
                Phone=r.IsDBNull(3) ? "" : r.GetString(3), Problem=r.GetString(4),
                Priority=r.GetString(5), Status=r.GetString(6), ReceivedAt=r.GetString(7),
                CompletedAt=r.IsDBNull(8) ? "" : r.GetString(8),
                Technician=r.IsDBNull(9) ? "" : r.GetString(9),
                Cost=r.GetDouble(10), Notes=r.IsDBNull(11) ? "" : r.GetString(11)
            });
        return list;
    }

    public static void SaveRepair(Repair x)
    {
        using var c = new SqliteConnection(ConnectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        if (x.Id == 0)
        {
            cmd.CommandText = """
                INSERT INTO Repairs(AssetName,Reporter,Phone,Problem,Priority,Status,ReceivedAt,CompletedAt,Technician,Cost,Notes)
                VALUES($a,$r,$p,$problem,$priority,$status,$received,$completed,$tech,$cost,$notes)
                """;
        }
        else
        {
            cmd.CommandText = """
                UPDATE Repairs SET AssetName=$a,Reporter=$r,Phone=$p,Problem=$problem,Priority=$priority,
                Status=$status,ReceivedAt=$received,CompletedAt=$completed,Technician=$tech,Cost=$cost,Notes=$notes
                WHERE Id=$id
                """;
            cmd.Parameters.AddWithValue("$id", x.Id);
        }
        cmd.Parameters.AddWithValue("$a", x.AssetName.Trim());
        cmd.Parameters.AddWithValue("$r", x.Reporter.Trim());
        cmd.Parameters.AddWithValue("$p", x.Phone.Trim());
        cmd.Parameters.AddWithValue("$problem", x.Problem.Trim());
        cmd.Parameters.AddWithValue("$priority", x.Priority);
        cmd.Parameters.AddWithValue("$status", x.Status);
        cmd.Parameters.AddWithValue("$received", x.ReceivedAt);
        cmd.Parameters.AddWithValue("$completed", string.IsNullOrWhiteSpace(x.CompletedAt) ? DBNull.Value : x.CompletedAt);
        cmd.Parameters.AddWithValue("$tech", x.Technician.Trim());
        cmd.Parameters.AddWithValue("$cost", x.Cost);
        cmd.Parameters.AddWithValue("$notes", x.Notes.Trim());
        var isNew = x.Id == 0;
        cmd.ExecuteNonQuery();
        if (isNew) { using var idCmd = c.CreateCommand(); idCmd.CommandText = "SELECT last_insert_rowid()"; x.Id = Convert.ToInt32(idCmd.ExecuteScalar()); }
        using (var history = c.CreateCommand())
        {
            history.CommandText = "INSERT INTO RepairStatusHistory(RepairId,NewStatus,ChangedAt,Username,Notes) VALUES($id,$status,$date,$user,$notes)";
            history.Parameters.AddWithValue("$id", x.Id); history.Parameters.AddWithValue("$status", x.Status);
            history.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            history.Parameters.AddWithValue("$user", CurrentUsername ?? "system"); history.Parameters.AddWithValue("$notes", "ثبت/ویرایش درخواست"); history.ExecuteNonQuery();
        }
        Log($"{(isNew ? "Created" : "Updated")} repair record #{x.Id} for asset: {x.AssetName}");
    }

    public static void DeleteRepair(int id)
    {
        using var c = new SqliteConnection(ConnectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Repairs WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        Log($"Deleted repair record ID: {id}");
    }

    public static string? CurrentRole { get; set; }
    public static string? CurrentUsername { get; set; }

    public static bool AddUser(string username, string password, string role)
    {
        username = username.Trim();
        if (username.Length < 3 || password.Length < 8) throw new ArgumentException("نام کاربری باید حداقل ۳ و رمز حداقل ۸ نویسه باشد.");
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Users(Username,PasswordHash,Salt,Role) VALUES($u,$p,$s,$r)";
        cmd.Parameters.AddWithValue("$u", username);
        cmd.Parameters.AddWithValue("$p", HashPassword(password, salt));
        cmd.Parameters.AddWithValue("$s", salt);
        cmd.Parameters.AddWithValue("$r", role);
        try { cmd.ExecuteNonQuery(); Log($"User created: {username} ({role})"); return true; }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return false; }
    }

    public static List<(int Id,string Username,string Role,string CreatedAt)> GetUsers()
    {
        var rows = new List<(int,string,string,string)>();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,Username,Role,CreatedAt FROM Users ORDER BY Id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) rows.Add((r.GetInt32(0),r.GetString(1),r.IsDBNull(2)?"کاربر":r.GetString(2),r.GetString(3)));
        return rows;
    }

    public static void DeleteUser(int id)
    {
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id=$id AND Username<>$current";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$current", CurrentUsername ?? "");
        cmd.ExecuteNonQuery();
        Log($"User deletion attempted for ID: {id}");
    }

    public static List<StockItem> GetInventory(string search = "")
    {
        var rows = new List<StockItem>();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,Code,Name,Category,Unit,Quantity,MinQuantity,Location,Notes FROM Inventory WHERE Code LIKE $q OR Name LIKE $q OR Category LIKE $q ORDER BY Name";
        cmd.Parameters.AddWithValue("$q", "%" + search.Trim() + "%");
        using var r = cmd.ExecuteReader();
        while(r.Read()) rows.Add(new StockItem { Id=r.GetInt32(0), Code=r.GetString(1), Name=r.GetString(2),
            Category=r.IsDBNull(3)?"":r.GetString(3), Unit=r.GetString(4), Quantity=r.GetDouble(5),
            MinQuantity=r.GetDouble(6), Location=r.IsDBNull(7)?"":r.GetString(7), Notes=r.IsDBNull(8)?"":r.GetString(8) });
        return rows;
    }

    public static void SaveStockItem(StockItem x)
    {
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        if(x.Id==0) cmd.CommandText = "INSERT INTO Inventory(Code,Name,Category,Unit,Quantity,MinQuantity,Location,Notes) VALUES($code,$name,$cat,$unit,$qty,$min,$loc,$notes)";
        else { cmd.CommandText = "UPDATE Inventory SET Code=$code,Name=$name,Category=$cat,Unit=$unit,MinQuantity=$min,Location=$loc,Notes=$notes WHERE Id=$id"; cmd.Parameters.AddWithValue("$id",x.Id); }
        cmd.Parameters.AddWithValue("$code",x.Code.Trim()); cmd.Parameters.AddWithValue("$name",x.Name.Trim());
        cmd.Parameters.AddWithValue("$cat",x.Category); cmd.Parameters.AddWithValue("$unit",x.Unit);
        cmd.Parameters.AddWithValue("$qty",x.Quantity); cmd.Parameters.AddWithValue("$min",x.MinQuantity);
        cmd.Parameters.AddWithValue("$loc",x.Location); cmd.Parameters.AddWithValue("$notes",x.Notes);
        cmd.ExecuteNonQuery(); Log($"Inventory item saved: {x.Code} / {x.Name}");
    }

    public static void MoveStock(int itemId, double quantity, bool incoming, string description)
    {
        if(quantity<=0) throw new ArgumentException("مقدار باید بیشتر از صفر باشد.");
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var tx = c.BeginTransaction();
        using var check = c.CreateCommand(); check.Transaction=tx;
        check.CommandText = "SELECT Quantity FROM Inventory WHERE Id=$id"; check.Parameters.AddWithValue("$id",itemId);
        var current = Convert.ToDouble(check.ExecuteScalar() ?? 0);
        if(!incoming && quantity>current) throw new ArgumentException("موجودی کافی نیست.");
        using var update = c.CreateCommand(); update.Transaction=tx;
        update.CommandText = "UPDATE Inventory SET Quantity=$q WHERE Id=$id";
        update.Parameters.AddWithValue("$q", incoming ? current+quantity : current-quantity);
        update.Parameters.AddWithValue("$id",itemId); update.ExecuteNonQuery();
        using var move = c.CreateCommand(); move.Transaction=tx;
        move.CommandText = "INSERT INTO StockMovements(ItemId,MovementType,Quantity,MovementDate,Description,Username) VALUES($id,$type,$q,$date,$desc,$user)";
        move.Parameters.AddWithValue("$id",itemId); move.Parameters.AddWithValue("$type",incoming?"ورود":"خروج");
        move.Parameters.AddWithValue("$q",quantity); move.Parameters.AddWithValue("$date",DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        move.Parameters.AddWithValue("$desc",description); move.Parameters.AddWithValue("$user",CurrentUsername ?? "unknown");
        move.ExecuteNonQuery(); tx.Commit(); Log($"Stock {(incoming?"in":"out")}: item={itemId}, qty={quantity}");
    }

    public static List<(string Code,string Name,string Type,double Quantity,string Date,string Description,string User)> GetStockMovements(int itemId)
    {
        var rows = new List<(string,string,string,double,string,string,string)>();
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT i.Code,i.Name,m.MovementType,m.Quantity,m.MovementDate,m.Description,m.Username FROM StockMovements m JOIN Inventory i ON i.Id=m.ItemId WHERE m.ItemId=$id ORDER BY m.Id DESC";
        cmd.Parameters.AddWithValue("$id",itemId);
        using var r=cmd.ExecuteReader();
        while(r.Read()) rows.Add((r.GetString(0),r.GetString(1),r.GetString(2),r.GetDouble(3),r.GetString(4),r.IsDBNull(5)?"":r.GetString(5),r.IsDBNull(6)?"":r.GetString(6)));
        return rows;
    }

    public static void DeleteStockItem(int id)
    {
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText="DELETE FROM Inventory WHERE Id=$id"; cmd.Parameters.AddWithValue("$id",id);
        cmd.ExecuteNonQuery(); Log($"Inventory item deleted: {id}");
    }


    public static void AddFinanceTransaction(string type, double amount, string category, string date, string description, string relatedPerson)
    {
        if (type != "دریافت" && type != "پرداخت") throw new ArgumentException("نوع تراکنش نامعتبر است.");
        if (amount <= 0) throw new ArgumentException("مبلغ باید بیشتر از صفر باشد.");
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO FinanceTransactions(TxType,Amount,Category,TxDate,Description,RelatedPerson,Username) VALUES($t,$a,$c,$d,$n,$p,$u)";
        cmd.Parameters.AddWithValue("$t", type); cmd.Parameters.AddWithValue("$a", amount);
        cmd.Parameters.AddWithValue("$c", category); cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$n", description); cmd.Parameters.AddWithValue("$p", relatedPerson);
        cmd.Parameters.AddWithValue("$u", CurrentUsername ?? ""); cmd.ExecuteNonQuery();
        Log($"Finance transaction: {type}, amount={amount}, category={category}");
    }

    public static List<(int Id,string Type,double Amount,string Category,string Date,string Description,string Person,string User)> GetFinanceTransactions()
    {
        var rows = new List<(int,string,double,string,string,string,string,string)>();
        using var c = new SqliteConnection(ConnectionString); c.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,TxType,Amount,Category,TxDate,COALESCE(Description,''),COALESCE(RelatedPerson,''),COALESCE(Username,'') FROM FinanceTransactions ORDER BY Id DESC";
        using var r = cmd.ExecuteReader(); while(r.Read()) rows.Add((r.GetInt32(0),r.GetString(1),r.GetDouble(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7)));
        return rows;
    }

    public static string CreateBackup(string? destination = null)
    {
        Directory.CreateDirectory(BackupDirectory);
        var target = destination ?? Path.Combine(BackupDirectory, $"MOBOROOT-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        using (var c = new SqliteConnection(ConnectionString))
        {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA wal_checkpoint(FULL);"; cmd.ExecuteNonQuery();
        }
        File.Copy(DatabasePath, target, true);
        Log("Database backup created: " + target);
        return target;
    }

    public static void RestoreBackup(string source)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("فایل پشتیبان پیدا نشد.", source);
        using (var c = new SqliteConnection(ConnectionString))
        { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA wal_checkpoint(FULL);"; cmd.ExecuteNonQuery(); }
        File.Copy(source, DatabasePath, true);
        Log("Database restored from: " + source);
    }

    public static (int repairs, int active, int completed, int lowStock, double revenue) GetDashboard()
    {
        using var c = new SqliteConnection(ConnectionString); c.Open();
        long Scalar(string sql) { using var cmd=c.CreateCommand(); cmd.CommandText=sql; return Convert.ToInt64(cmd.ExecuteScalar() ?? 0); }
        var repairs=(int)Scalar("SELECT COUNT(*) FROM Repairs");
        var active=(int)Scalar("SELECT COUNT(*) FROM Repairs WHERE Status NOT IN ('تکمیل‌شده','لغوشده')");
        var completed=(int)Scalar("SELECT COUNT(*) FROM Repairs WHERE Status='تکمیل‌شده'");
        var low=(int)Scalar("SELECT COUNT(*) FROM Inventory WHERE Quantity<=MinQuantity");
        using var rev=c.CreateCommand(); rev.CommandText="SELECT COALESCE(SUM(Cost),0) FROM Repairs WHERE Status='تکمیل‌شده'";
        return (repairs,active,completed,low,Convert.ToDouble(rev.ExecuteScalar() ?? 0));
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            File.AppendAllText(Path.Combine(LogsDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* Logging failure should not crash the UI. */ }
    }

}

internal sealed class Repair
{
    public int Id { get; set; }
    public string AssetName { get; set; } = "";
    public string Reporter { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Problem { get; set; } = "";
    public string Priority { get; set; } = "متوسط";
    public string Status { get; set; } = "ثبت‌شده";
    public string ReceivedAt { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string CompletedAt { get; set; } = "";
    public string Technician { get; set; } = "";
    public double Cost { get; set; }
    public string Notes { get; set; } = "";
}


internal sealed class StockItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Unit { get; set; } = "عدد";
        public double Quantity { get; set; }
        public double MinQuantity { get; set; }
        public string Location { get; set; } = "";
        public string Notes { get; set; } = "";
    }
