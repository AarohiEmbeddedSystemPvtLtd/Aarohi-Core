using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.SQL
{
    public sealed class Global_Sql
    {
        public string SQL_STRING { get; private set; }

        private readonly string _appName;
        private readonly string _regPath;

        public Global_Sql(string appName)
        {
            _appName = NormalizeAppName(appName);
            _regPath = $@"Software\Aarohi Embedded Systems Pvt Ltd\{_appName}\Database";

            SQL_STRING = LoadOrPromptUntilValid();
        }

        private string LoadOrPromptUntilValid()
        {
            // Check if it is a fresh start (registry key does not exist at all)
            bool isFreshStart = false;
            using (var key = Registry.CurrentUser.OpenSubKey(_regPath))
            {
                if (key == null)
                {
                    isFreshStart = true;
                }
            }

            if (isFreshStart)
            {
                string defaultBakPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbSchema", "BaseDatabase.bak");
                string? bakFilePath = null;
                bool launchCloner = false;

                if (System.IO.File.Exists(defaultBakPath))
                {
                    bakFilePath = defaultBakPath;
                }
                else
                {
                    // Prompt Choice Dialog for developers vs. clients when default package is missing
                    string choice = "cancel";
                    using (var choiceForm = new Form())
                    {
                        choiceForm.Text = "Database Setup - Fresh Start";
                        choiceForm.Size = new System.Drawing.Size(420, 210);
                        choiceForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                        choiceForm.MaximizeBox = false;
                        choiceForm.MinimizeBox = false;
                        choiceForm.StartPosition = FormStartPosition.CenterScreen;
                        choiceForm.BackColor = System.Drawing.Color.FromArgb(15, 23, 42); // slate-900

                        var lbl = new Label
                        {
                            Text = "No default database schema package was found.\nHow would you like to set up the database?",
                            ForeColor = System.Drawing.Color.White,
                            Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular),
                            Location = new System.Drawing.Point(20, 20),
                            Size = new System.Drawing.Size(380, 45)
                        };

                        var btnSelectBak = new Button
                        {
                            Text = "Select Existing Backup File (.bak)",
                            BackColor = System.Drawing.Color.FromArgb(37, 99, 235),
                            ForeColor = System.Drawing.Color.White,
                            FlatStyle = FlatStyle.Flat,
                            Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                            Location = new System.Drawing.Point(20, 80),
                            Size = new System.Drawing.Size(365, 30),
                            Cursor = Cursors.Hand
                        };
                        btnSelectBak.Click += (s, e) => { choice = "bak"; choiceForm.Close(); };

                        var btnDevWizard = new Button
                        {
                            Text = "Open Developer Database Cloner Wizard",
                            BackColor = System.Drawing.Color.FromArgb(16, 185, 129),
                            ForeColor = System.Drawing.Color.White,
                            FlatStyle = FlatStyle.Flat,
                            Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                            Location = new System.Drawing.Point(20, 118),
                            Size = new System.Drawing.Size(365, 30),
                            Cursor = Cursors.Hand
                        };
                        btnDevWizard.Click += (s, e) => { choice = "dev"; choiceForm.Close(); };

                        choiceForm.Controls.Add(lbl);
                        choiceForm.Controls.Add(btnSelectBak);
                        choiceForm.Controls.Add(btnDevWizard);

                        choiceForm.ShowDialog();
                    }

                    if (choice == "bak")
                    {
                        using (var ofd = new OpenFileDialog())
                        {
                            ofd.Title = "Select Base Database Backup File (.bak)";
                            ofd.Filter = "SQL Server Backup Files (*.bak)|*.bak|All Files (*.*)|*.*";
                            ofd.Multiselect = false;

                            if (ofd.ShowDialog() == DialogResult.OK)
                            {
                                bakFilePath = ofd.FileName;
                            }
                        }
                    }
                    else if (choice == "dev")
                    {
                        launchCloner = true;
                    }
                }

                if (launchCloner)
                {
                    using (var cloner = new FormDatabaseCloner(_appName))
                    {
                        if (cloner.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(cloner.ResultConnectionString))
                        {
                            // Success! Registry details are saved by the cloner itself.
                            if (!OpenSqlManager("Database cloned and configured successfully.\n\nPlease verify and click Save to confirm connection."))
                            {
                                try { Registry.CurrentUser.DeleteSubKeyTree(_regPath, false); } catch { }
                                throw new OperationCanceledException("SQL configuration was cancelled.");
                            }
                        }
                        else
                        {
                            if (!OpenSqlManager("Developer database cloning was cancelled.\n\nPlease configure SQL connection manually."))
                                throw new OperationCanceledException("SQL configuration was cancelled.");
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(bakFilePath))
                {
                    using (var splash = new FormDbSplash(_appName, bakFilePath))
                    {
                        if (splash.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(splash.ResultConnectionString))
                        {
                            // Success! Write target database configurations to registry so the manager loads them automatically
                            string serverName = new SqlConnectionStringBuilder(splash.ResultConnectionString).DataSource;
                            SaveSqlSettingsToRegistry(_regPath, serverName, _appName);

                            if (!OpenSqlManager("Database created successfully from template.\n\nPlease verify and click Save to confirm configuration."))
                            {
                                try
                                {
                                    Registry.CurrentUser.DeleteSubKeyTree(_regPath, false);
                                }
                                catch { }
                                throw new OperationCanceledException("SQL configuration was cancelled.");
                            }
                        }
                        else
                        {
                            string restoreError = splash.Error?.Message ?? "User cancelled or unknown error.";
                            MessageBox.Show($"Database creation failed:\n\n{restoreError}\n\nRedirecting to SQL Connection Manager for manual setup.", 
                                "Database Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                            if (!OpenSqlManager("SQL connection failed:\n\n" + restoreError))
                                throw new OperationCanceledException("SQL configuration was cancelled.");
                        }
                    }
                }
                else
                {
                    if (!OpenSqlManager("No base database file selected.\n\nPlease configure SQL connection manually."))
                        throw new OperationCanceledException("SQL configuration was cancelled.");
                }
            }

            while (true)
            {
                // 1) Build connection string from registry
                if (!TryBuildConnectionStringFromRegistry(_regPath, out string cs, out string buildError))
                {
                    if (!OpenSqlManager(buildError))
                        throw new OperationCanceledException("SQL configuration was cancelled.");

                    continue;
                }

                // 2) Test SQL connection
                if (!TryTestSqlConnection(cs, out string connError))
                {
                    if (!OpenSqlManager("SQL connection failed:\n\n" + connError))
                        throw new OperationCanceledException("SQL configuration was cancelled.");

                    continue;
                }

                return cs;
            }
        }

        private static void SaveSqlSettingsToRegistry(string regPath, string server, string database)
        {
            using var key = Registry.CurrentUser.CreateSubKey(regPath);
            key.SetValue("Server", server);
            key.SetValue("Database", database);
            key.SetValue("AuthMode", "Windows");
            key.SetValue("UserName", "");
            key.SetValue("Password", "");
        }

        private static bool TryBuildConnectionStringFromRegistry(
            string regPath,
            out string connectionString,
            out string error)
        {
            connectionString = "";
            error = "";

            using var key = Registry.CurrentUser.OpenSubKey(regPath);
            if (key == null)
            {
                error = $"SQL connection settings not found for this app.\n\nRegistry:\n{regPath}\n\nPlease configure SQL connection.";
                return false;
            }

            string server = key.GetValue("Server", "") as string ?? "";
            string database = key.GetValue("Database", "") as string ?? "";
            string authMode = key.GetValue("AuthMode", "Windows") as string ?? "Windows";
            string userName = key.GetValue("UserName", "") as string ?? "";
            string password = key.GetValue("Password", "") as string ?? "";

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            {
                error = "SQL connection details are incomplete.\n\nPlease configure SQL connection again.";
                return false;
            }

            bool useWindowsAuth = authMode.Equals("Windows", StringComparison.OrdinalIgnoreCase);

            var csBuilder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                TrustServerCertificate = true,
                ConnectTimeout = 3
            };

            if (useWindowsAuth)
            {
                csBuilder.IntegratedSecurity = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    error = "User name for SQL authentication is missing.";
                    return false;
                }

                csBuilder.IntegratedSecurity = false;
                csBuilder.UserID = userName;
                csBuilder.Password = password ?? string.Empty;
            }

            connectionString = csBuilder.ConnectionString;
            return true;
        }

        private static bool TryTestSqlConnection(string connectionString, out string error)
        {
            error = "";
            try
            {
                using var con = new SqlConnection(connectionString);
                con.Open();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool OpenSqlManager(string message)
        {
            MessageBox.Show(message, $"{_appName} - SQL Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            using var frm = new FormSqlConnectionManager(_appName);
            return frm.ShowDialog() == DialogResult.OK;
        }

        private static string NormalizeAppName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return "DefaultApp";

            // Avoid registry path breaking chars
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                appName = appName.Replace(c.ToString(), "");

            appName = appName.Replace("\\", "").Replace("/", "").Trim();
            return appName.Length == 0 ? "DefaultApp" : appName;
        }

        private static bool RestoreDatabaseSilently(string appName, string bakFilePath, out string connString, out string error)
        {
            connString = "";
            error = "";
            string tempBakPathOnServer = "";
            bool copiedTemp = false;
            try
            {
                // To bypass SQL Server service account permission issues reading from private user directories,
                // copy the backup file to the public folder first.
                string tempDir = System.Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
                tempBakPathOnServer = System.IO.Path.Combine(tempDir, $"temp_restore_{System.Guid.NewGuid():N}.bak");
                
                try
                {
                    System.IO.File.Copy(bakFilePath, tempBakPathOnServer, true);
                    copiedTemp = true;
                }
                catch (System.Exception)
                {
                    // Fallback to original file if copy fails for any reason
                    tempBakPathOnServer = bakFilePath;
                }

                string? server = FindLocalSqlServer(out string scanError);
                if (string.IsNullOrEmpty(server))
                {
                    error = "No local SQL Server instance could be connected.\n\nDetails: " + scanError;
                    return false;
                }

                var csb = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = "master",
                    TrustServerCertificate = true,
                    ConnectTimeout = 5,
                    IntegratedSecurity = true
                };

                string logicalDataName = "";
                string logicalLogName = "";

                using (var con = new SqlConnection(csb.ConnectionString))
                {
                    con.Open();

                    using (var fileListCmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @bak", con))
                    {
                        fileListCmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                        using (var reader = fileListCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string type = reader["Type"].ToString()?.Trim() ?? "";
                                string logicalName = reader["LogicalName"].ToString() ?? "";
                                if (type.Equals("D", System.StringComparison.OrdinalIgnoreCase)) logicalDataName = logicalName;
                                else if (type.Equals("L", System.StringComparison.OrdinalIgnoreCase)) logicalLogName = logicalName;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(logicalDataName) || string.IsNullOrEmpty(logicalLogName))
                    {
                        error = "Could not read logical database file names from the backup file.";
                        return false;
                    }

                    string dataDir = "";
                    using (var pathCmd = new SqlCommand(@"
                        SELECT SUBSTRING(physical_name, 1, CHARINDEX('master.mdf', LOWER(physical_name)) - 1) 
                        FROM master.sys.master_files 
                        WHERE database_id = 1 AND file_id = 1", con))
                    {
                        dataDir = pathCmd.ExecuteScalar() as string ?? "";
                    }

                    if (string.IsNullOrEmpty(dataDir))
                    {
                        dataDir = System.IO.Path.GetDirectoryName(tempBakPathOnServer) ?? @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA";
                    }

                    string dataFilePath = System.IO.Path.Combine(dataDir, $"{appName}.mdf");
                    string logFilePath = System.IO.Path.Combine(dataDir, $"{appName}_log.ldf");

                    using (var killCmd = new SqlCommand($@"
                        IF DB_ID('{appName}') IS NOT NULL
                        BEGIN
                            DECLARE @kill NVARCHAR(MAX) = N'';
                            SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N';'
                            FROM sys.dm_exec_sessions
                            WHERE database_id = DB_ID('{appName}');
                            IF @kill <> N'' EXEC(@kill);
                        END", con))
                    {
                        killCmd.ExecuteNonQuery();
                    }

                    string restoreSql = $@"
                        RESTORE DATABASE [{appName}]
                        FROM DISK = @bak
                        WITH REPLACE,
                             MOVE N'{logicalDataName}' TO @dataFile,
                             MOVE N'{logicalLogName}' TO @logFile,
                             RECOVERY;";

                    using (var restoreCmd = new SqlCommand(restoreSql, con))
                    {
                        restoreCmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                        restoreCmd.Parameters.AddWithValue("@dataFile", dataFilePath);
                        restoreCmd.Parameters.AddWithValue("@logFile", logFilePath);
                        restoreCmd.CommandTimeout = 300;
                        restoreCmd.ExecuteNonQuery();
                    }
                }

                connString = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = appName,
                    TrustServerCertificate = true,
                    IntegratedSecurity = true
                }.ConnectionString;

                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (copiedTemp && !string.IsNullOrEmpty(tempBakPathOnServer))
                {
                    try { System.IO.File.Delete(tempBakPathOnServer); } catch { }
                }
            }
        }

        private static string? FindLocalSqlServer(out string errorMsg)
        {
            errorMsg = "";
            string pcName = System.Environment.MachineName;
            string[] preferred =
            {
                $@"{pcName}\SQLEXPRESS",
                @".\SQLEXPRESS",
                pcName,
                "(local)",
                "."
            };

            foreach (var server in preferred)
            {
                var csb = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = "master",
                    TrustServerCertificate = true,
                    ConnectTimeout = 2,
                    IntegratedSecurity = true
                };

                try
                {
                    using (var con = new SqlConnection(csb.ConnectionString))
                    {
                        con.Open();
                        return server;
                    }
                }
                catch (System.Exception ex)
                {
                    errorMsg = ex.Message;
                }
            }
            return null;
        }
    }
}
