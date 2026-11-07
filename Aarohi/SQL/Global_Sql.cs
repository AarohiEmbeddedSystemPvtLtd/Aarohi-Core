using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Aarohi.SQL;

public sealed class Global_Sql
{
    private readonly string _appName;

    private readonly string _regPath;

    public string SQL_STRING { get; private set; }

    public Global_Sql(string appName)
    {
        _appName = NormalizeAppName(appName);
        _regPath = "Software\\Aarohi Embedded Systems Pvt Ltd\\" + _appName + "\\Database";
        SQL_STRING = LoadOrPromptUntilValid();
        RunDatabaseMigrations(SQL_STRING);
    }

    private string LoadOrPromptUntilValid()
    {
        bool flag = false;
        using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(_regPath))
        {
            if (registryKey == null)
            {
                flag = true;
            }
        }

        if (flag)
        {
            string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbSchema", "BaseDatabase.bak");
            string text2 = null;
            bool flag2 = false;
            if (File.Exists(text))
            {
                text2 = text;
            }
            else
            {
                string choice = "cancel";
                Form choiceForm = new Form();
                try
                {
                    choiceForm.Text = "Database Setup - Fresh Start";
                    choiceForm.Size = new Size(420, 210);
                    choiceForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    choiceForm.MaximizeBox = false;
                    choiceForm.MinimizeBox = false;
                    choiceForm.StartPosition = FormStartPosition.CenterScreen;
                    choiceForm.BackColor = Color.FromArgb(15, 23, 42);
                    Label value = new Label
                    {
                        Text = "No default database schema package was found.\nHow would you like to set up the database?",
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                        Location = new Point(20, 20),
                        Size = new Size(380, 45)
                    };
                    Button button = new Button
                    {
                        Text = "Select Existing Backup File (.bak)",
                        BackColor = Color.FromArgb(37, 99, 235),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        Location = new Point(20, 80),
                        Size = new Size(365, 30),
                        Cursor = Cursors.Hand
                    };
                    button.Click += delegate
                    {
                        choice = "bak";
                        choiceForm.Close();
                    };
                    Button button2 = new Button
                    {
                        Text = "Open Developer Database Cloner Wizard",
                        BackColor = Color.FromArgb(16, 185, 129),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        Location = new Point(20, 118),
                        Size = new Size(365, 30),
                        Cursor = Cursors.Hand
                    };
                    button2.Click += delegate
                    {
                        choice = "dev";
                        choiceForm.Close();
                    };
                    choiceForm.Controls.Add(value);
                    choiceForm.Controls.Add(button);
                    choiceForm.Controls.Add(button2);
                    choiceForm.ShowDialog();
                }
                finally
                {
                    if (choiceForm != null)
                    {
                        ((IDisposable)choiceForm).Dispose();
                    }
                }

                if (choice == "bak")
                {
                    using OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.Title = "Select Base Database Backup File (.bak)";
                    openFileDialog.Filter = "SQL Server Backup Files (*.bak)|*.bak|All Files (*.*)|*.*";
                    openFileDialog.Multiselect = false;
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        text2 = openFileDialog.FileName;
                    }
                }
                else if (choice == "dev")
                {
                    flag2 = true;
                }
            }

            if (flag2)
            {
                using FormDatabaseCloner formDatabaseCloner = new FormDatabaseCloner(_appName);
                if (formDatabaseCloner.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(formDatabaseCloner.ResultConnectionString))
                {
                    if (!OpenSqlManager("Database cloned and configured successfully.\n\nPlease verify and click Save to confirm connection."))
                    {
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(_regPath, throwOnMissingSubKey: false);
                        }
                        catch
                        {
                        }

                        throw new OperationCanceledException("SQL configuration was cancelled.");
                    }
                }
                else if (!OpenSqlManager("Developer database cloning was cancelled.\n\nPlease configure SQL connection manually."))
                {
                    throw new OperationCanceledException("SQL configuration was cancelled.");
                }
            }
            else if (!string.IsNullOrEmpty(text2))
            {
                using FormDbSplash formDbSplash = new FormDbSplash(_appName, text2);
                if (formDbSplash.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(formDbSplash.ResultConnectionString))
                {
                    string dataSource = new SqlConnectionStringBuilder(formDbSplash.ResultConnectionString).DataSource;
                    SaveSqlSettingsToRegistry(_regPath, dataSource, _appName);
                    if (!OpenSqlManager("Database created successfully from template.\n\nPlease verify and click Save to confirm configuration."))
                    {
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(_regPath, throwOnMissingSubKey: false);
                        }
                        catch
                        {
                        }

                        throw new OperationCanceledException("SQL configuration was cancelled.");
                    }
                }
                else
                {
                    string text3 = formDbSplash.Error?.Message ?? "User cancelled or unknown error.";
                    MessageBox.Show("Database creation failed:\n\n" + text3 + "\n\nRedirecting to SQL Connection Manager for manual setup.", "Database Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    if (!OpenSqlManager("SQL connection failed:\n\n" + text3))
                    {
                        throw new OperationCanceledException("SQL configuration was cancelled.");
                    }
                }
            }
            else if (!OpenSqlManager("No base database file selected.\n\nPlease configure SQL connection manually."))
            {
                throw new OperationCanceledException("SQL configuration was cancelled.");
            }
        }

        string connectionString;
        while (true)
        {
            if (!TryBuildConnectionStringFromRegistry(_regPath, out connectionString, out var error))
            {
                if (!OpenSqlManager(error))
                {
                    throw new OperationCanceledException("SQL configuration was cancelled.");
                }

                continue;
            }

            if (TryTestSqlConnection(connectionString, out var error2))
            {
                break;
            }

            if (OpenSqlManager("SQL connection failed:\n\n" + error2))
            {
                continue;
            }

            throw new OperationCanceledException("SQL configuration was cancelled.");
        }

        return connectionString;
    }

    private static void SaveSqlSettingsToRegistry(string regPath, string server, string database)
    {
        using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(regPath);
        registryKey.SetValue("Server", server);
        registryKey.SetValue("Database", database);
        registryKey.SetValue("AuthMode", "Windows");
        registryKey.SetValue("UserName", "");
        registryKey.SetValue("Password", "");
    }

    private static bool TryBuildConnectionStringFromRegistry(string regPath, out string connectionString, out string error)
    {
        connectionString = "";
        error = "";
        using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(regPath);
        if (registryKey == null)
        {
            error = "SQL connection settings not found for this app.\n\nRegistry:\n" + regPath + "\n\nPlease configure SQL connection.";
            return false;
        }

        string text = (registryKey.GetValue("Server", "") as string) ?? "";
        string text2 = (registryKey.GetValue("Database", "") as string) ?? "";
        string text3 = (registryKey.GetValue("AuthMode", "Windows") as string) ?? "Windows";
        string text4 = (registryKey.GetValue("UserName", "") as string) ?? "";
        string text5 = (registryKey.GetValue("Password", "") as string) ?? "";
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
        {
            error = "SQL connection details are incomplete.\n\nPlease configure SQL connection again.";
            return false;
        }

        bool flag = text3.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder
        {
            DataSource = text,
            InitialCatalog = text2,
            TrustServerCertificate = true,
            ConnectTimeout = 3
        };
        if (flag)
        {
            sqlConnectionStringBuilder.IntegratedSecurity = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(text4))
            {
                error = "User name for SQL authentication is missing.";
                return false;
            }

            sqlConnectionStringBuilder.IntegratedSecurity = false;
            sqlConnectionStringBuilder.UserID = text4;
            sqlConnectionStringBuilder.Password = text5 ?? string.Empty;
        }

        connectionString = sqlConnectionStringBuilder.ConnectionString;
        return true;
    }

    private static bool TryTestSqlConnection(string connectionString, out string error)
    {
        error = "";
        try
        {
            using SqlConnection sqlConnection = OpenStartupConnectionWithRetry(connectionString);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("Global_Sql startup validation failed ({0}): {1}", ex.GetType().Name, ex.Message);
            error = ex is SqlException sqlEx && IsTransientStartupSqlError(sqlEx)
                ? $"SQL Server could not be reached. Check the server and network. (SQL error {sqlEx.Number})"
                : ex.Message;
            return false;
        }
    }

    // Only startup validation and migration connection opening use this helper.
    // The registered connection string and normal runtime pooling remain unchanged.
    private static SqlConnection OpenStartupConnectionWithRetry(string connectionString)
    {
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 4,
            ConnectRetryCount = 0,
            Pooling = false
        };

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            SqlConnection connection = new SqlConnection(builder.ConnectionString);
            bool connected = false;
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                connection.Open();
                connected = true;
                return connection;
            }
            catch (SqlException ex) when (attempt < 3 && IsTransientStartupSqlError(ex))
            {
                Trace.TraceWarning("Global_Sql startup connection to '{0}' failed (attempt {1}/3, SQL {2}, {3} ms). Retrying.",
                    builder.DataSource, attempt, ex.Number, timer.ElapsedMilliseconds);
            }
            finally
            {
                if (!connected)
                    connection.Dispose();
            }

            Thread.Sleep(attempt * 1000); // Delays: 1 s, then 2 s.
        }

        throw new InvalidOperationException("SQL startup connection attempts were exhausted.");
    }

    private static bool IsTransientStartupSqlError(SqlException ex)
    {
        switch (ex.Number)
        {
            case -2:    // Connection timeout
            case -1:    // Network/provider error (including some Error 26 cases)
            case 2:     // Server not found
            case 26:    // Instance discovery failure
            case 40:    // Could not open SQL Server connection
            case 53:    // Network path not found
            case 64:    // Network name unavailable
            case 121:   // Semaphore timeout
            case 233:   // Connection initialization failure
            case 258:   // Wait timeout
            case 10051: // Network unreachable
            case 10052: // Network dropped connection
            case 10053: // Connection aborted
            case 10054: // Connection reset
            case 10060: // Connection timed out
            case 10061: // Connection refused
            case 10065: // Host unreachable
            case 11001: // Host not found
            case 11004: // DNS lookup failure
                return true;
            default:
                return false;
        }
    }

    private bool OpenSqlManager(string message)
    {
        MessageBox.Show(message, _appName + " - SQL Connection", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        using FormSqlConnectionManager formSqlConnectionManager = new FormSqlConnectionManager(_appName);
        return formSqlConnectionManager.ShowDialog() == DialogResult.OK;
    }

    private static string NormalizeAppName(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return "DefaultApp";
        }

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidFileNameChars)
        {
            appName = appName.Replace(c.ToString(), "");
        }

        appName = appName.Replace("\\", "").Replace("/", "").Trim();
        return (appName.Length == 0) ? "DefaultApp" : appName;
    }

    private static bool RestoreDatabaseSilently(string appName, string bakFilePath, out string connString, out string error)
    {
        connString = "";
        error = "";
        string text = "";
        bool flag = false;
        try
        {
            string path = Environment.GetEnvironmentVariable("PUBLIC") ?? "C:\\Users\\Public";
            text = Path.Combine(path, $"temp_restore_{Guid.NewGuid():N}.bak");
            try
            {
                File.Copy(bakFilePath, text, overwrite: true);
                flag = true;
            }
            catch (Exception)
            {
                text = bakFilePath;
            }

            string errorMsg;
            string text2 = FindLocalSqlServer(out errorMsg);
            if (string.IsNullOrEmpty(text2))
            {
                error = "No local SQL Server instance could be connected.\n\nDetails: " + errorMsg;
                return false;
            }

            SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = text2,
                InitialCatalog = "master",
                TrustServerCertificate = true,
                ConnectTimeout = 5,
                IntegratedSecurity = true
            };
            string value = "";
            string value2 = "";
            using (SqlConnection sqlConnection = new SqlConnection(sqlConnectionStringBuilder.ConnectionString))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCommand = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @bak", sqlConnection))
                {
                    sqlCommand.Parameters.AddWithValue("@bak", text);
                    using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
                    while (sqlDataReader.Read())
                    {
                        string text3 = sqlDataReader["Type"].ToString()?.Trim() ?? "";
                        string text4 = sqlDataReader["LogicalName"].ToString() ?? "";
                        if (text3.Equals("D", StringComparison.OrdinalIgnoreCase))
                        {
                            value = text4;
                        }
                        else if (text3.Equals("L", StringComparison.OrdinalIgnoreCase))
                        {
                            value2 = text4;
                        }
                    }
                }

                if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(value2))
                {
                    error = "Could not read logical database file names from the backup file.";
                    return false;
                }

                string text5 = "";
                using (SqlCommand sqlCommand2 = new SqlCommand("\r\n                        SELECT SUBSTRING(physical_name, 1, CHARINDEX('master.mdf', LOWER(physical_name)) - 1) \r\n                        FROM master.sys.master_files \r\n                        WHERE database_id = 1 AND file_id = 1", sqlConnection))
                {
                    text5 = (sqlCommand2.ExecuteScalar() as string) ?? "";
                }

                if (string.IsNullOrEmpty(text5))
                {
                    text5 = Path.GetDirectoryName(text) ?? "C:\\Program Files\\Microsoft SQL Server\\MSSQL15.SQLEXPRESS\\MSSQL\\DATA";
                }

                string value3 = Path.Combine(text5, appName + ".mdf");
                string value4 = Path.Combine(text5, appName + "_log.ldf");
                using (SqlCommand sqlCommand3 = new SqlCommand($"\r\n                        IF DB_ID('{appName}') IS NOT NULL\r\n                        BEGIN\r\n                            DECLARE @kill NVARCHAR(MAX) = N'';\r\n                            SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N';'\r\n                            FROM sys.dm_exec_sessions\r\n                            WHERE database_id = DB_ID('{appName}');\r\n                            IF @kill <> N'' EXEC(@kill);\r\n                        END", sqlConnection))
                {
                    sqlCommand3.ExecuteNonQuery();
                }

                string cmdText = $"\r\n                        RESTORE DATABASE [{appName}]\r\n                        FROM DISK = @bak\r\n                        WITH REPLACE,\r\n                             MOVE N'{value}' TO @dataFile,\r\n                             MOVE N'{value2}' TO @logFile,\r\n                             RECOVERY;";
                using SqlCommand sqlCommand4 = new SqlCommand(cmdText, sqlConnection);
                sqlCommand4.Parameters.AddWithValue("@bak", text);
                sqlCommand4.Parameters.AddWithValue("@dataFile", value3);
                sqlCommand4.Parameters.AddWithValue("@logFile", value4);
                sqlCommand4.CommandTimeout = 300;
                sqlCommand4.ExecuteNonQuery();
            }

            connString = new SqlConnectionStringBuilder
            {
                DataSource = text2,
                InitialCatalog = appName,
                TrustServerCertificate = true,
                IntegratedSecurity = true
            }.ConnectionString;
            return true;
        }
        catch (Exception ex2)
        {
            error = ex2.Message;
            return false;
        }
        finally
        {
            if (flag && !string.IsNullOrEmpty(text))
            {
                try
                {
                    File.Delete(text);
                }
                catch
                {
                }
            }
        }
    }

    private static string? FindLocalSqlServer(out string errorMsg)
    {
        errorMsg = "";
        string machineName = Environment.MachineName;
        string[] array = new string[5]
        {
            machineName + "\\SQLEXPRESS",
            ".\\SQLEXPRESS",
            machineName,
            "(local)",
            "."
        };
        string[] array2 = array;
        foreach (string text in array2)
        {
            SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = text,
                InitialCatalog = "master",
                TrustServerCertificate = true,
                ConnectTimeout = 2,
                IntegratedSecurity = true
            };
            try
            {
                using SqlConnection sqlConnection = new SqlConnection(sqlConnectionStringBuilder.ConnectionString);
                sqlConnection.Open();
                return text;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }
        }

        return null;
    }

    private static void RunDatabaseMigrations(string connectionString)
    {
        Assembly entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
        {
            return;
        }

        List<string> list = (from name in entryAssembly.GetManifestResourceNames()
                             where name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                             orderby name
                             select name).ToList();
        if (list.Count == 0)
        {
            return;
        }

        using SqlConnection sqlConnection = OpenStartupConnectionWithRetry(connectionString);
        string cmdText = "\r\n                    IF OBJECT_ID('dbo.AarohiSchemaVersions', 'U') IS NULL\r\n                    BEGIN\r\n                        CREATE TABLE dbo.AarohiSchemaVersions (\r\n                            Id INT IDENTITY(1,1) PRIMARY KEY,\r\n                            ScriptName NVARCHAR(255) NOT NULL UNIQUE,\r\n                            AppliedAt DATETIME DEFAULT GETDATE()\r\n                        );\r\n                    END";
        using (SqlCommand sqlCommand = new SqlCommand(cmdText, sqlConnection))
        {
            sqlCommand.ExecuteNonQuery();
        }

        HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (SqlCommand sqlCommand2 = new SqlCommand("SELECT ScriptName FROM dbo.AarohiSchemaVersions", sqlConnection))
        {
            using SqlDataReader sqlDataReader = sqlCommand2.ExecuteReader();
            while (sqlDataReader.Read())
            {
                hashSet.Add(sqlDataReader.GetString(0));
            }
        }

        foreach (string item in list)
        {
            string text = item;
            string[] array = item.Split('.');
            if (array.Length >= 2)
            {
                text = array[^2] + "." + array[^1];
            }

            if (hashSet.Contains(text))
            {
                continue;
            }

            string input;
            using (Stream stream = entryAssembly.GetManifestResourceStream(item))
            {
                if (stream == null)
                {
                    continue;
                }

                using (StreamReader streamReader = new StreamReader(stream))
                {
                    input = streamReader.ReadToEnd();
                }

                goto IL_01d8;
            }

        IL_01d8:
            using SqlTransaction sqlTransaction = sqlConnection.BeginTransaction();
            try
            {
                string[] array2 = Regex.Split(input, "^\\s*GO\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                string[] array3 = array2;
                foreach (string text2 in array3)
                {
                    if (!string.IsNullOrWhiteSpace(text2))
                    {
                        using SqlCommand sqlCommand3 = new SqlCommand(text2, sqlConnection, sqlTransaction);
                        sqlCommand3.CommandTimeout = 300;
                        sqlCommand3.ExecuteNonQuery();
                    }
                }

                string cmdText2 = "INSERT INTO dbo.AarohiSchemaVersions (ScriptName) VALUES (@name)";
                using (SqlCommand sqlCommand4 = new SqlCommand(cmdText2, sqlConnection, sqlTransaction))
                {
                    sqlCommand4.Parameters.AddWithValue("@name", text);
                    sqlCommand4.ExecuteNonQuery();
                }

                sqlTransaction.Commit();
            }
            catch (Exception ex)
            {
                sqlTransaction.Rollback();
                throw new Exception("Failed to apply database migration script '" + text + "': " + ex.Message, ex);
            }
        }
    }
}