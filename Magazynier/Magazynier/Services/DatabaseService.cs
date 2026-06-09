using System;
using System.Collections.Generic;
using System.IO;
using Magazynier.Models;
using Microsoft.Data.Sqlite;

namespace Magazynier
{
    /// <summary>
    /// Handles all SQLite database operations for Magazynier.
    /// Database file is stored in the Data subfolder next to the executable.
    /// </summary>
    public static class DatabaseService
    {
        private static string DbPath =>
            Path.Combine(AppContext.BaseDirectory, AppConstants.DATA_FOLDER, AppConstants.DATABASE_FILE);

        private static string ConnectionString => $"Data Source={DbPath}";

        // ==================== INITIALIZATION ====================

        public static void Initialize()
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, AppConstants.DATA_FOLDER);
            Directory.CreateDirectory(dataDir);

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            // Enable WAL mode for better performance
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }

            CreateTables(conn);
            SeedDefaultCategories(conn);
        }

        private static void CreateTables(SqliteConnection conn)
        {
            var statements = new[]
            {
                @"CREATE TABLE IF NOT EXISTS AssetCategories (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name        TEXT NOT NULL,
                    Description TEXT,
                    IconGlyph   TEXT NOT NULL DEFAULT '\uE7F4'
                )",
                @"CREATE TABLE IF NOT EXISTS AppUsers (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName   TEXT NOT NULL,
                    LastName    TEXT NOT NULL,
                    Department  TEXT,
                    Email       TEXT,
                    Phone       TEXT,
                    IsActive    INTEGER NOT NULL DEFAULT 1
                )",
                @"CREATE TABLE IF NOT EXISTS Assets (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name            TEXT NOT NULL,
                    SerialNumber    TEXT,
                    InventoryNumber TEXT,
                    CategoryId      INTEGER NOT NULL REFERENCES AssetCategories(Id),
                    Manufacturer    TEXT,
                    Model           TEXT,
                    Description     TEXT,
                    Status          INTEGER NOT NULL DEFAULT 0,
                    PurchaseDate    TEXT,
                    CreatedAt       TEXT NOT NULL,
                    UpdatedAt       TEXT NOT NULL
                )",
                @"CREATE TABLE IF NOT EXISTS Assignments (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    AssetId         INTEGER NOT NULL REFERENCES Assets(Id),
                    UserId          INTEGER NOT NULL REFERENCES AppUsers(Id),
                    DecisionNumber  TEXT NOT NULL,
                    AssignedAt      TEXT NOT NULL,
                    ReturnedAt      TEXT,
                    Notes           TEXT
                )"
            };

            foreach (var sql in statements)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static void SeedDefaultCategories(SqliteConnection conn)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM AssetCategories;";
            var count = (long)(checkCmd.ExecuteScalar() ?? 0L);
            if (count > 0) return;

            var defaults = new[]
            {
                ("Komputer", "Komputery stacjonarne i laptopy", "\uE7F8"),
                ("Monitor", "Monitory i wyświetlacze", "\uE7F4"),
                ("Klawiatura", "Klawiatury", "\uE765"),
                ("Myszka", "Myszy komputerowe", "\uE962"),
                ("Telefon VoIP", "Telefony IP i stacjonarne", "\uE717"),
                ("Drukarka", "Drukarki i urządzenia wielofunkcyjne", "\uE749"),
                ("Inne", "Pozostały sprzęt", "\uE7C3"),
            };

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO AssetCategories (Name, Description, IconGlyph) VALUES (@n, @d, @g);";
            var pName = insertCmd.Parameters.Add("@n", SqliteType.Text);
            var pDesc = insertCmd.Parameters.Add("@d", SqliteType.Text);
            var pGlyph = insertCmd.Parameters.Add("@g", SqliteType.Text);

            foreach (var (name, desc, glyph) in defaults)
            {
                pName.Value = name;
                pDesc.Value = desc;
                pGlyph.Value = glyph;
                insertCmd.ExecuteNonQuery();
            }
        }

        // ==================== ASSET CATEGORIES ====================

        public static List<AssetCategory> GetCategories()
        {
            var list = new List<AssetCategory>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Description, IconGlyph FROM AssetCategories ORDER BY Name;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AssetCategory
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IconGlyph = reader.GetString(3),
                });
            }
            return list;
        }

        public static void SaveCategory(AssetCategory cat)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            if (cat.Id == 0)
            {
                cmd.CommandText = "INSERT INTO AssetCategories (Name, Description, IconGlyph) VALUES (@n, @d, @g);";
            }
            else
            {
                cmd.CommandText = "UPDATE AssetCategories SET Name=@n, Description=@d, IconGlyph=@g WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", cat.Id);
            }
            cmd.Parameters.AddWithValue("@n", cat.Name);
            cmd.Parameters.AddWithValue("@d", (object?)cat.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@g", cat.IconGlyph);
            cmd.ExecuteNonQuery();

            if (cat.Id == 0)
            {
                using var idCmd = conn.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                cat.Id = (int)(long)(idCmd.ExecuteScalar() ?? 0L);
            }
        }

        public static void DeleteCategory(int id)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AssetCategories WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static bool CategoryHasAssets(int categoryId)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Assets WHERE CategoryId=@id;";
            cmd.Parameters.AddWithValue("@id", categoryId);
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }

        // ==================== USERS ====================

        public static List<AppUser> GetUsers(bool activeOnly = false)
        {
            var list = new List<AppUser>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = activeOnly
                ? "SELECT Id, FirstName, LastName, Department, Email, Phone, IsActive FROM AppUsers WHERE IsActive=1 ORDER BY LastName, FirstName;"
                : "SELECT Id, FirstName, LastName, Department, Email, Phone, IsActive FROM AppUsers ORDER BY LastName, FirstName;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AppUser
                {
                    Id = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Department = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Phone = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsActive = reader.GetInt32(6) == 1,
                });
            }
            return list;
        }

        public static void SaveUser(AppUser user)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            if (user.Id == 0)
            {
                cmd.CommandText = @"INSERT INTO AppUsers (FirstName, LastName, Department, Email, Phone, IsActive)
                                    VALUES (@fn, @ln, @dep, @email, @phone, @active);";
            }
            else
            {
                cmd.CommandText = @"UPDATE AppUsers SET FirstName=@fn, LastName=@ln, Department=@dep,
                                    Email=@email, Phone=@phone, IsActive=@active WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", user.Id);
            }
            cmd.Parameters.AddWithValue("@fn", user.FirstName);
            cmd.Parameters.AddWithValue("@ln", user.LastName);
            cmd.Parameters.AddWithValue("@dep", (object?)user.Department ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone", (object?)user.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@active", user.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();

            if (user.Id == 0)
            {
                using var idCmd = conn.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                user.Id = (int)(long)(idCmd.ExecuteScalar() ?? 0L);
            }
        }

        public static void DeleteUser(int id)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AppUsers WHERE Id=@id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static bool UserHasActiveAssignments(int userId)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE UserId=@id AND ReturnedAt IS NULL;";
            cmd.Parameters.AddWithValue("@id", userId);
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }

        // ==================== ASSETS ====================

        public static List<Asset> GetAssets(string? searchQuery = null, int? categoryId = null, AssetStatus? status = null)
        {
            var list = new List<Asset>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT a.Id, a.Name, a.SerialNumber, a.InventoryNumber, a.CategoryId, c.Name,
                       a.Manufacturer, a.Model, a.Description, a.Status,
                       a.PurchaseDate, a.CreatedAt, a.UpdatedAt,
                       u.Id, (u.FirstName || ' ' || u.LastName), asn.DecisionNumber, asn.AssignedAt
                FROM Assets a
                LEFT JOIN AssetCategories c ON a.CategoryId = c.Id
                LEFT JOIN Assignments asn ON asn.AssetId = a.Id AND asn.ReturnedAt IS NULL
                LEFT JOIN AppUsers u ON asn.UserId = u.Id
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                cmd.CommandText += " AND (a.Name LIKE @q OR a.SerialNumber LIKE @q OR a.InventoryNumber LIKE @q OR a.Manufacturer LIKE @q OR a.Model LIKE @q)";
                cmd.Parameters.AddWithValue("@q", $"%{searchQuery}%");
            }
            if (categoryId.HasValue)
            {
                cmd.CommandText += " AND a.CategoryId = @cat";
                cmd.Parameters.AddWithValue("@cat", categoryId.Value);
            }
            if (status.HasValue)
            {
                cmd.CommandText += " AND a.Status = @status";
                cmd.Parameters.AddWithValue("@status", (int)status.Value);
            }

            cmd.CommandText += " ORDER BY a.Name;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Asset
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    SerialNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                    InventoryNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CategoryId = reader.GetInt32(4),
                    CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Manufacturer = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Model = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Description = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Status = (AssetStatus)reader.GetInt32(9),
                    PurchaseDate = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
                    CreatedAt = DateTime.Parse(reader.GetString(11)),
                    UpdatedAt = DateTime.Parse(reader.GetString(12)),
                    AssignedUserId = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    AssignedUserName = reader.IsDBNull(14) ? null : reader.GetString(14),
                    AssignmentDecisionNumber = reader.IsDBNull(15) ? null : reader.GetString(15),
                    AssignmentDate = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16)),
                });
            }
            return list;
        }

        public static void SaveAsset(Asset asset)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            asset.UpdatedAt = DateTime.Now;

            if (asset.Id == 0)
            {
                asset.CreatedAt = DateTime.Now;
                cmd.CommandText = @"INSERT INTO Assets (Name, SerialNumber, InventoryNumber, CategoryId, Manufacturer, Model, Description, Status, PurchaseDate, CreatedAt, UpdatedAt)
                                    VALUES (@n, @sn, @inv, @cat, @mfr, @mdl, @desc, @status, @pd, @ca, @ua);";
            }
            else
            {
                cmd.CommandText = @"UPDATE Assets SET Name=@n, SerialNumber=@sn, InventoryNumber=@inv, CategoryId=@cat,
                                    Manufacturer=@mfr, Model=@mdl, Description=@desc, Status=@status, PurchaseDate=@pd, UpdatedAt=@ua
                                    WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", asset.Id);
            }
            cmd.Parameters.AddWithValue("@n", asset.Name);
            cmd.Parameters.AddWithValue("@sn", (object?)asset.SerialNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inv", (object?)asset.InventoryNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cat", asset.CategoryId);
            cmd.Parameters.AddWithValue("@mfr", (object?)asset.Manufacturer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mdl", (object?)asset.Model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", (object?)asset.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (int)asset.Status);
            cmd.Parameters.AddWithValue("@pd", asset.PurchaseDate.HasValue ? (object)asset.PurchaseDate.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@ca", asset.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@ua", asset.UpdatedAt.ToString("o"));
            cmd.ExecuteNonQuery();

            if (asset.Id == 0)
            {
                using var idCmd = conn.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                asset.Id = (int)(long)(idCmd.ExecuteScalar() ?? 0L);
            }
        }

        public static void DeleteAsset(int id)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                using var delAssignCmd = conn.CreateCommand();
                delAssignCmd.Transaction = transaction;
                delAssignCmd.CommandText = "DELETE FROM Assignments WHERE AssetId=@id;";
                delAssignCmd.Parameters.AddWithValue("@id", id);
                delAssignCmd.ExecuteNonQuery();

                using var delAssetCmd = conn.CreateCommand();
                delAssetCmd.Transaction = transaction;
                delAssetCmd.CommandText = "DELETE FROM Assets WHERE Id=@id;";
                delAssetCmd.Parameters.AddWithValue("@id", id);
                delAssetCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== ASSIGNMENTS ====================

        public static List<Assignment> GetAssignments(int? assetId = null, int? userId = null, bool activeOnly = false)
        {
            var list = new List<Assignment>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT asn.Id, asn.AssetId, a.Name, a.SerialNumber,
                       asn.UserId, (u.FirstName || ' ' || u.LastName),
                       asn.DecisionNumber, asn.AssignedAt, asn.ReturnedAt, asn.Notes
                FROM Assignments asn
                LEFT JOIN Assets a ON asn.AssetId = a.Id
                LEFT JOIN AppUsers u ON asn.UserId = u.Id
                WHERE 1=1";

            if (assetId.HasValue)
            {
                cmd.CommandText += " AND asn.AssetId=@assetId";
                cmd.Parameters.AddWithValue("@assetId", assetId.Value);
            }
            if (userId.HasValue)
            {
                cmd.CommandText += " AND asn.UserId=@userId";
                cmd.Parameters.AddWithValue("@userId", userId.Value);
            }
            if (activeOnly)
            {
                cmd.CommandText += " AND asn.ReturnedAt IS NULL";
            }

            cmd.CommandText += " ORDER BY asn.AssignedAt DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Assignment
                {
                    Id = reader.GetInt32(0),
                    AssetId = reader.GetInt32(1),
                    AssetName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    AssetSerial = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UserId = reader.GetInt32(4),
                    UserName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DecisionNumber = reader.GetString(6),
                    AssignedAt = DateTime.Parse(reader.GetString(7)),
                    ReturnedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                });
            }
            return list;
        }

        public static void AssignAsset(Assignment assignment)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Close any existing active assignment for this asset
                using var closeCmd = conn.CreateCommand();
                closeCmd.Transaction = transaction;
                closeCmd.CommandText = "UPDATE Assignments SET ReturnedAt=@now WHERE AssetId=@assetId AND ReturnedAt IS NULL;";
                closeCmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                closeCmd.Parameters.AddWithValue("@assetId", assignment.AssetId);
                closeCmd.ExecuteNonQuery();

                // Insert new assignment
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"INSERT INTO Assignments (AssetId, UserId, DecisionNumber, AssignedAt, Notes)
                                          VALUES (@assetId, @userId, @dn, @at, @notes);";
                insertCmd.Parameters.AddWithValue("@assetId", assignment.AssetId);
                insertCmd.Parameters.AddWithValue("@userId", assignment.UserId);
                insertCmd.Parameters.AddWithValue("@dn", assignment.DecisionNumber);
                insertCmd.Parameters.AddWithValue("@at", assignment.AssignedAt.ToString("o"));
                insertCmd.Parameters.AddWithValue("@notes", (object?)assignment.Notes ?? DBNull.Value);
                insertCmd.ExecuteNonQuery();

                // Update asset status
                using var statusCmd = conn.CreateCommand();
                statusCmd.Transaction = transaction;
                statusCmd.CommandText = "UPDATE Assets SET Status=@status, UpdatedAt=@now WHERE Id=@id;";
                statusCmd.Parameters.AddWithValue("@status", (int)AssetStatus.Assigned);
                statusCmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                statusCmd.Parameters.AddWithValue("@id", assignment.AssetId);
                statusCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static void ReturnAsset(int assignmentId, int assetId)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                using var returnCmd = conn.CreateCommand();
                returnCmd.Transaction = transaction;
                returnCmd.CommandText = "UPDATE Assignments SET ReturnedAt=@now WHERE Id=@id;";
                returnCmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                returnCmd.Parameters.AddWithValue("@id", assignmentId);
                returnCmd.ExecuteNonQuery();

                using var statusCmd = conn.CreateCommand();
                statusCmd.Transaction = transaction;
                statusCmd.CommandText = "UPDATE Assets SET Status=@status, UpdatedAt=@now WHERE Id=@assetId;";
                statusCmd.Parameters.AddWithValue("@status", (int)AssetStatus.Available);
                statusCmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
                statusCmd.Parameters.AddWithValue("@assetId", assetId);
                statusCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ==================== STATISTICS ====================

        public static (int Total, int Available, int Assigned, int InRepair, int Retired) GetAssetStats()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COUNT(*),
                    SUM(CASE WHEN Status=0 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Status=1 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Status=2 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Status=3 THEN 1 ELSE 0 END)
                FROM Assets;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return (
                    reader.IsDBNull(0) ? 0 : (int)(long)reader.GetValue(0),
                    reader.IsDBNull(1) ? 0 : (int)(long)reader.GetValue(1),
                    reader.IsDBNull(2) ? 0 : (int)(long)reader.GetValue(2),
                    reader.IsDBNull(3) ? 0 : (int)(long)reader.GetValue(3),
                    reader.IsDBNull(4) ? 0 : (int)(long)reader.GetValue(4)
                );
            }
            return (0, 0, 0, 0, 0);
        }
    }
}
