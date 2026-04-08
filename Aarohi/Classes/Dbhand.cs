using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    public static class Dbhand
    {
        private static string? _connectionString;

        public static void Configure(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.");

            _connectionString = connectionString;
        }

        public static string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                    throw new InvalidOperationException("Dbhand not configured. Call Dbhand.Configure() first.");

                return _connectionString;
            }
        }

        public static IReadOnlyList<TableInfo> GetTables()
        {
            if (_connectionString == null)
                throw new InvalidOperationException("Dbhand not configured.");

            var list = new List<TableInfo>();

            const string sql = @"
                SELECT 
                    s.name AS SchemaName,
                    t.name AS TableName
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name;";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new TableInfo(
                            r.GetString(0),
                            r.GetString(1)
                        ));
                    }
                }
            }

            return list;
        }

        public readonly struct TableInfo
        {
            public string Schema { get; }
            public string Name { get; }

            public TableInfo(string schema, string name)
            {
                Schema = schema;
                Name = name;
            }

            public override string ToString() => $"{Schema}.{Name}";
        }
        #region Full Backup

        public static void BackupDatabaseToBak(
        string folderPath,
        int daysToKeep = 0)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Dbhand not configured. Call Dbhand.Configure() first.");

            // Create folder if not exists
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string databaseName = "";
            var builder = new SqlConnectionStringBuilder(_connectionString);
            databaseName = builder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("Database name missing in connection string.");

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string bakFileName = $"{databaseName}_FULL_{timestamp}.bak";
            string bakFilePath = Path.Combine(folderPath, bakFileName);

            if (daysToKeep > 0)
            {
                var files = Directory.GetFiles(folderPath, $"{databaseName}_FULL_*.bak");
                var limit = DateTime.Now.AddDays(-daysToKeep);

                foreach (var file in files)
                {
                    try
                    {
                        if (File.GetCreationTime(file) < limit)
                            File.Delete(file);
                    }
                    catch { /* ignore errors */ }
                }
            }

            string sql = $@"
            BACKUP DATABASE [{databaseName}]
            TO DISK = @path
            WITH FORMAT,
                 INIT,
                 STATS = 10;";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@path", bakFilePath);
                con.Open();

                cmd.CommandTimeout = 60 * 10;
                cmd.ExecuteNonQuery();
            }
        }

        public static void RestoreDatabaseFromBak(
    string bakFilePath,
    string targetDatabaseName)
        {
            if (string.IsNullOrWhiteSpace(bakFilePath))
                throw new ArgumentException("Backup file path is required.", nameof(bakFilePath));

            if (!File.Exists(bakFilePath))
                throw new FileNotFoundException("Backup file not found.", bakFilePath);

            if (string.IsNullOrWhiteSpace(targetDatabaseName))
                throw new ArgumentException("Target database name is required.", nameof(targetDatabaseName));

            // 1) Always connect to master, never to the DB being restored
            var csb = new SqlConnectionStringBuilder(_connectionString);
            csb.InitialCatalog = "master";

            using (var con = new SqlConnection(csb.ConnectionString))
            {
                con.Open();

                string logicalDataName = "";
                string logicalLogName = "";
                string dataDir = "";
                string logDir = "";

                using (var fileListCmd = new SqlCommand(
                    "RESTORE FILELISTONLY FROM DISK = @bak", con))
                {
                    fileListCmd.Parameters.AddWithValue("@bak", bakFilePath);

                    using (var reader = fileListCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string type = reader["Type"].ToString()?.Trim() ?? "";
                            string logicalName = reader["LogicalName"].ToString() ?? "";
                            string physicalName = reader["PhysicalName"].ToString() ?? "";

                            string dir = Path.GetDirectoryName(physicalName) ?? "";

                            if (type.Equals("D", StringComparison.OrdinalIgnoreCase))
                            {
                                logicalDataName = logicalName;
                                dataDir = dir;
                            }
                            else if (type.Equals("L", StringComparison.OrdinalIgnoreCase))
                            {
                                logicalLogName = logicalName;
                                logDir = dir;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(logicalDataName) || string.IsNullOrEmpty(logicalLogName))
                    throw new InvalidOperationException("Could not read logical names from backup file.");

                // Fallback if dirs are empty
                if (string.IsNullOrEmpty(dataDir))
                    dataDir = Path.GetDirectoryName(bakFilePath) ?? @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA";

                if (string.IsNullOrEmpty(logDir))
                    logDir = dataDir;

                string dataFilePath = Path.Combine(dataDir, $"{targetDatabaseName}.mdf");
                string logFilePath = Path.Combine(logDir, $"{targetDatabaseName}_log.ldf");

                using (var killCmd = new SqlCommand(@"
            IF DB_ID(@db) IS NOT NULL
            BEGIN
                DECLARE @kill NVARCHAR(MAX) = N'';

                SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N';'
                FROM sys.dm_exec_sessions
                WHERE database_id = DB_ID(@db);

                IF @kill <> N''
                    EXEC(@kill);
            END
        ", con))
                {
                    killCmd.Parameters.AddWithValue("@db", targetDatabaseName);
                    killCmd.ExecuteNonQuery();
                }

                string restoreSql = $@"
RESTORE DATABASE [{targetDatabaseName}]
FROM DISK = @bak
WITH REPLACE,
     MOVE N'{logicalDataName}' TO @dataFile,
     MOVE N'{logicalLogName}' TO @logFile,
     RECOVERY,
     STATS = 10;
";

                using (var restoreCmd = new SqlCommand(restoreSql, con))
                {
                    restoreCmd.Parameters.AddWithValue("@bak", bakFilePath);
                    restoreCmd.Parameters.AddWithValue("@dataFile", dataFilePath);
                    restoreCmd.Parameters.AddWithValue("@logFile", logFilePath);

                    restoreCmd.CommandTimeout = 60 * 10; // 10 minutes
                    restoreCmd.ExecuteNonQuery();
                }

                con.Close();
            }
        }


        #endregion

        #region CSV
        public static void ExportTableToCsv(
    string schemaName,
    string tableName,
    string csvFilePath)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Dbhand not configured. Call Dbhand.Configure() first.");

            string sql = $"SELECT * FROM [{schemaName}].[{tableName}]";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();

                using (var reader = cmd.ExecuteReader())
                using (var writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
                {
                    int fieldCount = reader.FieldCount;

                    for (int i = 0; i < fieldCount; i++)
                    {
                        if (i > 0) writer.Write(",");
                        WriteCsvField(writer, reader.GetName(i));
                    }
                    writer.WriteLine();

                    while (reader.Read())
                    {
                        for (int i = 0; i < fieldCount; i++)
                        {
                            if (i > 0) writer.Write(",");

                            if (reader.IsDBNull(i))
                            {
                                writer.Write("");
                            }
                            else
                            {
                                string val = reader.GetValue(i)?.ToString() ?? "";
                                WriteCsvField(writer, val);
                            }
                        }
                        writer.WriteLine();
                    }
                }
            }
        }

        public static void RestoreTableFromCsvWithMerge(
    string schemaName,
    string tableName,
    string csvFilePath)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Dbhand not configured. Call Dbhand.Configure() first.");

            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException("CSV file not found.", csvFilePath);

            string fullTableName = $"[{schemaName}].[{tableName}]";

            // -----------------------------
            // 1) Read CSV header + rows
            // -----------------------------
            List<string> headers;
            List<string[]> rows = new List<string[]>();

            using (var reader = new StreamReader(csvFilePath, Encoding.UTF8))
            {
                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new InvalidOperationException("CSV file is empty.");

                headers = ParseCsvLine(headerLine);
                if (headers.Count == 0)
                    throw new InvalidOperationException("No columns found in CSV header.");

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = ParseCsvLine(line);
                    rows.Add(fields.ToArray());
                }
            }

            // Column name -> index map
            var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
                headerIndex[headers[i]] = i;

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // ---------------------------------------------
                // 2) Find PRIMARY KEY columns from database
                // ---------------------------------------------
                List<string> pkColumns = new List<string>();

                using (var pkCmd = new SqlCommand(@"
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
               AND tc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
               AND tc.TABLE_NAME = kcu.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
              AND tc.TABLE_SCHEMA = @schema
              AND tc.TABLE_NAME = @table
            ORDER BY kcu.ORDINAL_POSITION;
        ", con))
                {
                    pkCmd.Parameters.AddWithValue("@schema", schemaName);
                    pkCmd.Parameters.AddWithValue("@table", tableName);

                    using (var r = pkCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            pkColumns.Add(r.GetString(0));
                        }
                    }
                }

                // =====================================================
                // CASE 1: NO PRIMARY KEY → TRUNCATE + BULK INSERT
                // =====================================================
                if (pkColumns.Count == 0)
                {
                    // Build DataTable from CSV
                    var dt = new DataTable();
                    foreach (var col in headers)
                    {
                        dt.Columns.Add(col, typeof(string));
                    }

                    foreach (var row in rows)
                    {
                        var dr = dt.NewRow();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            string val = i < row.Length ? row[i] : string.Empty;
                            dr[i] = string.IsNullOrEmpty(val) ? DBNull.Value : val;
                        }
                        dt.Rows.Add(dr);
                    }

                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            // TRUNCATE TABLE
                            using (var truncateCmd = new SqlCommand($"TRUNCATE TABLE {fullTableName};", con, tx))
                            {
                                truncateCmd.ExecuteNonQuery();
                            }

                            // Bulk insert all data
                            using (var bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.CheckConstraints, tx))
                            {
                                bulk.DestinationTableName = fullTableName;

                                foreach (DataColumn col in dt.Columns)
                                {
                                    bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                                }

                                bulk.WriteToServer(dt);
                            }

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }

                    // Done for no-PK case
                    return;
                }

                // =====================================================
                // CASE 2: PK FOUND → DO UPSERT (UPDATE + INSERT)
                // =====================================================

                // Check that all PK columns exist in CSV
                foreach (var pk in pkColumns)
                {
                    if (!headerIndex.ContainsKey(pk))
                        throw new InvalidOperationException(
                            $"CSV does not contain primary key column '{pk}'. Cannot match rows.");
                }

                var allColumns = headers.ToList();
                var nonPkColumns = allColumns
                    .Where(c => !pkColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                // Create safe param names
                string CleanParamIdentifier(string name)
                {
                    var sb = new StringBuilder();
                    foreach (char ch in name)
                    {
                        if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                        else sb.Append('_');
                    }
                    return sb.ToString();
                }

                var paramNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in allColumns)
                {
                    paramNames[col] = "@p_" + CleanParamIdentifier(col);
                }

                string pkWhere = string.Join(" AND ",
                    pkColumns.Select(c => $"[{c}] = {paramNames[c]}"));

                string setClause = nonPkColumns.Count == 0
                    ? ""
                    : string.Join(", ", nonPkColumns.Select(c => $"[{c}] = {paramNames[c]}"));

                string insertCols = string.Join(", ", allColumns.Select(c => $"[{c}]"));
                string insertParams = string.Join(", ", allColumns.Select(c => paramNames[c]));

                var sbSql = new StringBuilder();
                sbSql.AppendLine($"IF EXISTS (SELECT 1 FROM {fullTableName} WHERE {pkWhere})");
                sbSql.AppendLine("BEGIN");
                if (!string.IsNullOrEmpty(setClause))
                {
                    sbSql.AppendLine($"    UPDATE {fullTableName}");
                    sbSql.AppendLine($"    SET {setClause}");
                    sbSql.AppendLine($"    WHERE {pkWhere};");
                }
                else
                {
                    sbSql.AppendLine("    -- Row exists, but no non-PK columns to update.");
                }
                sbSql.AppendLine("END");
                sbSql.AppendLine("ELSE");
                sbSql.AppendLine("BEGIN");
                sbSql.AppendLine($"    INSERT INTO {fullTableName} ({insertCols})");
                sbSql.AppendLine($"    VALUES ({insertParams});");
                sbSql.AppendLine("END;");

                string upsertSql = sbSql.ToString();

                using (var tx = con.BeginTransaction())
                using (var cmd = new SqlCommand(upsertSql, con, tx))
                {
                    // Create parameters once
                    foreach (var col in allColumns)
                    {
                        var p = cmd.Parameters.Add(paramNames[col], SqlDbType.NVarChar);
                        p.IsNullable = true;
                    }

                    try
                    {
                        foreach (var row in rows)
                        {
                            foreach (var col in allColumns)
                            {
                                int idx = headerIndex[col];
                                string value = idx < row.Length ? row[idx] : "";

                                var p = cmd.Parameters[paramNames[col]];
                                if (string.IsNullOrEmpty(value))
                                    p.Value = DBNull.Value;
                                else
                                    p.Value = value;
                            }

                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion

        #region Tables

        public static void ExportTablesToXlsx(
     string schemaName,
     IEnumerable<string> tableNames,
     string xlsxFilePath)
        {
            using var con = new SqlConnection(_connectionString);
            con.Open();

            using var wb = new XLWorkbook();

            foreach (var table in tableNames)
            {
                string sql = $"SELECT * FROM [{schemaName}].[{table}]";
                using var cmd = new SqlCommand(sql, con);
                using var da = new SqlDataAdapter(cmd);
                var dt = new DataTable(table);
                da.Fill(dt);

                wb.Worksheets.Add(dt, table);
            }

            wb.SaveAs(xlsxFilePath);
        }


        public static void RestoreAllTablesFromXlsx(string schemaName, string xlsxFilePath)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Dbhand not configured.");

            if (!File.Exists(xlsxFilePath))
                throw new FileNotFoundException("XLSX file not found.", xlsxFilePath);

            using var wb = new XLWorkbook(xlsxFilePath);
            using var con = new SqlConnection(_connectionString);
            con.Open();

            foreach (var ws in wb.Worksheets)
            {
                string tableName = ws.Name.Trim();
                string fullTableName = $"[{schemaName}].[{tableName}]";

                // ----------------------
                // 1) Read header
                // ----------------------
                var headers = new List<string>();
                int colIndex = 1;
                while (true)
                {
                    string header = ws.Cell(1, colIndex).GetValue<string>().Trim();
                    if (string.IsNullOrWhiteSpace(header))
                        break;

                    headers.Add(header);
                    colIndex++;
                }

                if (headers.Count == 0)
                    continue;

                // ----------------------
                // 2) Read rows
                // ----------------------
                var rows = new List<string[]>();
                int rowIndex = 2;

                while (true)
                {
                    bool empty = true;
                    var values = new string[headers.Count];

                    for (int c = 0; c < headers.Count; c++)
                    {
                        string val = ws.Cell(rowIndex, c + 1).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(val)) empty = false;
                        values[c] = val;
                    }

                    if (empty)
                        break;

                    rows.Add(values);
                    rowIndex++;
                }

                if (rows.Count == 0)
                    continue;

                // Map header -> index
                var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Count; i++)
                    headerIndex[headers[i]] = i;

                // ----------------------
                // 3) Find primary key
                // ----------------------
                var pkColumns = new List<string>();

                using (var pkCmd = new SqlCommand(@"
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
               AND tc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
               AND tc.TABLE_NAME = kcu.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
              AND tc.TABLE_SCHEMA = @schema
              AND tc.TABLE_NAME = @table
            ORDER BY kcu.ORDINAL_POSITION;
        ", con))
                {
                    pkCmd.Parameters.AddWithValue("@schema", schemaName);
                    pkCmd.Parameters.AddWithValue("@table", tableName);

                    using var rdr = pkCmd.ExecuteReader();
                    while (rdr.Read())
                        pkColumns.Add(rdr.GetString(0));
                }

                // =====================================================
                // CASE A: NO PRIMARY KEY → TRUNCATE + BULK INSERT
                // =====================================================
                if (pkColumns.Count == 0)
                {
                    var dt = new DataTable();
                    foreach (var col in headers)
                        dt.Columns.Add(col, typeof(string));

                    foreach (var r in rows)
                    {
                        var dr = dt.NewRow();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            dr[i] = string.IsNullOrWhiteSpace(r[i]) ? DBNull.Value : r[i];
                        }
                        dt.Rows.Add(dr);
                    }

                    using var tx = con.BeginTransaction();
                    try
                    {
                        using var truncate = new SqlCommand($"TRUNCATE TABLE {fullTableName}", con, tx);
                        truncate.ExecuteNonQuery();

                        using var bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.CheckConstraints, tx)
                        {
                            DestinationTableName = fullTableName
                        };

                        foreach (DataColumn col in dt.Columns)
                            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

                        bulk.WriteToServer(dt);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }

                    continue; // next table/sheet
                }

                // =====================================================
                // CASE B: PK FOUND → UPSERT (UPDATE + INSERT)
                // =====================================================
                foreach (var pk in pkColumns)
                {
                    if (!headerIndex.ContainsKey(pk))
                        throw new Exception($"Sheet '{tableName}' missing primary key column '{pk}'");
                }

                var allColumns = headers;
                var nonPkColumns = allColumns.Where(c => !pkColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

                string Clean(string name)
                {
                    var sb = new StringBuilder();
                    foreach (char ch in name)
                    {
                        if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                        else sb.Append('_');
                    }
                    return sb.ToString();
                }

                var paramNames = allColumns.ToDictionary(c => c, c => "@p_" + Clean(c), StringComparer.OrdinalIgnoreCase);

                string pkWhere = string.Join(" AND ", pkColumns.Select(c => $"[{c}] = {paramNames[c]}"));
                string setClause = string.Join(", ", nonPkColumns.Select(c => $"[{c}] = {paramNames[c]}"));

                string insertCols = string.Join(", ", allColumns.Select(c => $"[{c}]"));
                string insertParams = string.Join(", ", allColumns.Select(c => paramNames[c]));

                string sql = $@"
IF EXISTS (SELECT 1 FROM {fullTableName} WHERE {pkWhere})
BEGIN
    {(string.IsNullOrEmpty(setClause) ? "/* nothing to update */" : $"UPDATE {fullTableName} SET {setClause} WHERE {pkWhere}")}
END
ELSE
BEGIN
    INSERT INTO {fullTableName} ({insertCols}) VALUES ({insertParams});
END";

                using var tx2 = con.BeginTransaction();
                using var cmd = new SqlCommand(sql, con, tx2);

                foreach (var col in allColumns)
                {
                    cmd.Parameters.Add(paramNames[col], SqlDbType.NVarChar).IsNullable = true;
                }

                try
                {
                    foreach (var r in rows)
                    {
                        foreach (var col in allColumns)
                        {
                            int idx = headerIndex[col];
                            string val = r[idx];

                            cmd.Parameters[paramNames[col]].Value =
                                string.IsNullOrWhiteSpace(val) ? DBNull.Value : val;
                        }

                        cmd.ExecuteNonQuery();
                    }

                    tx2.Commit();
                }
                catch
                {
                    tx2.Rollback();
                    throw;
                }
            }
        }

        #endregion

        #region Healper

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
                return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Escaped quote ("")
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }


        private static void WriteCsvField(StreamWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write("");
                return;
            }

            bool mustQuote =
                value.Contains(",") ||
                value.Contains("\"") ||
                value.Contains("\r") ||
                value.Contains("\n");

            if (value.Contains("\""))
                value = value.Replace("\"", "\"\"");

            if (mustQuote)
            {
                writer.Write("\"");
                writer.Write(value);
                writer.Write("\"");
            }
            else
            {
                writer.Write(value);
            }
        }

        #endregion


    }
}
