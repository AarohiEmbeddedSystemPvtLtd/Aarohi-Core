using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.SQL
{
    public partial class FormDatabaseCloner : Form
    {
        private readonly string _appName;

        private Label lblHeader;
        private Label lblTitle;
        private Label lblServer;
        private ComboBox cbServer;
        private Label lblAuth;
        private ComboBox cbAuth;
        private Label lblUsername;
        private TextBox txtUsername;
        private Label lblPassword;
        private TextBox txtPassword;
        private Button btnConnect;
        private Label lblSourceDb;
        private ComboBox cbSourceDb;
        private Label lblTables;
        private CheckedListBox clbTables;
        private Label lblTargetDb;
        private TextBox txtTargetDb;
        private Label lblOutputPath;
        private TextBox txtOutputPath;
        private Button btnClone;
        private Label lblStatus;
        private Button btnCancel;
        private CheckBox chkSelectAll;

        public string? ResultConnectionString { get; private set; }

        public FormDatabaseCloner(string appName)
        {
            _appName = appName;
            InitializeComponent();
            txtTargetDb.Text = _appName;
            txtOutputPath.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbSchema", "BaseDatabase.bak");

            // Register events outside of InitializeComponent to prevent designer parser crashes
            this.cbAuth.SelectedIndexChanged += CbAuth_SelectedIndexChanged;
            this.btnConnect.Click += BtnConnect_Click;
            this.cbSourceDb.SelectedIndexChanged += CbSourceDb_SelectedIndexChanged;
            this.btnClone.Click += BtnClone_Click;
            this.btnCancel.Click += BtnCancel_Click;
            this.chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
        }

        public void SetOutputDirectory(string targetDir)
        {
            txtOutputPath.Text = Path.Combine(targetDir, "DbSchema", "BaseDatabase.bak");
        }

        private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
        {
            bool checkState = chkSelectAll.Checked;
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, checkState);
            }
        }

        private void CbAuth_SelectedIndexChanged(object? sender, EventArgs e)
        {
            bool isSql = cbAuth.SelectedIndex == 1;
            txtUsername.Enabled = isSql;
            txtPassword.Enabled = isSql;
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            await ConnectAndGetDatabasesAsync();
        }

        private async void CbSourceDb_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await LoadTablesAsync();
        }

        private async void BtnClone_Click(object? sender, EventArgs e)
        {
            await PerformCloneAsync();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void InitializeComponent()
        {
            this.lblHeader = new Label();
            this.lblTitle = new Label();
            this.lblServer = new Label();
            this.cbServer = new ComboBox();
            this.lblAuth = new Label();
            this.cbAuth = new ComboBox();
            this.lblUsername = new Label();
            this.txtUsername = new TextBox();
            this.lblPassword = new Label();
            this.txtPassword = new TextBox();
            this.btnConnect = new Button();
            this.lblSourceDb = new Label();
            this.cbSourceDb = new ComboBox();
            this.chkSelectAll = new CheckBox();
            this.lblTables = new Label();
            this.clbTables = new CheckedListBox();
            this.lblTargetDb = new Label();
            this.txtTargetDb = new TextBox();
            this.lblOutputPath = new Label();
            this.txtOutputPath = new TextBox();
            this.btnClone = new Button();
            this.lblStatus = new Label();
            this.btnCancel = new Button();
            this.SuspendLayout();
            // 
            // lblHeader
            // 
            this.lblHeader.AutoSize = true;
            this.lblHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblHeader.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
            this.lblHeader.Location = new Point(25, 20);
            this.lblHeader.Text = "AAROHI DATABASE DEVELOPER WIZARD";
            // 
            // lblTitle
            // 
            this.lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(25, 42);
            this.lblTitle.Size = new Size(580, 25);
            this.lblTitle.Text = "Clone Database, Purge Data, and Package for Distribution";
            // 
            // lblServer
            // 
            this.lblServer.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblServer.Location = new Point(25, 80);
            this.lblServer.Size = new Size(120, 20);
            this.lblServer.Text = "SQL Server Host:";
            // 
            // cbServer
            // 
            this.cbServer.FormattingEnabled = true;
            this.cbServer.Location = new Point(150, 77);
            this.cbServer.Size = new Size(200, 23);
            this.cbServer.Items.AddRange(new object[] { @".\SQLEXPRESS", "(local)", "(local)\\SQLEXPRESS" });
            this.cbServer.SelectedIndex = 0;
            // 
            // lblAuth
            // 
            this.lblAuth.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblAuth.Location = new Point(25, 110);
            this.lblAuth.Size = new Size(120, 20);
            this.lblAuth.Text = "Authentication:";
            // 
            // cbAuth
            // 
            this.cbAuth.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cbAuth.Location = new Point(150, 107);
            this.cbAuth.Size = new Size(200, 23);
            this.cbAuth.Items.AddRange(new object[] { "Windows Authentication", "SQL Server Authentication" });
            this.cbAuth.SelectedIndex = 0;
            // 
            // lblUsername
            // 
            this.lblUsername.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblUsername.Location = new Point(25, 140);
            this.lblUsername.Size = new Size(120, 20);
            this.lblUsername.Text = "Login Username:";
            // 
            // txtUsername
            // 
            this.txtUsername.Enabled = false;
            this.txtUsername.Location = new Point(150, 137);
            this.txtUsername.Size = new Size(200, 23);
            // 
            // lblPassword
            // 
            this.lblPassword.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblPassword.Location = new Point(25, 170);
            this.lblPassword.Size = new Size(120, 20);
            this.lblPassword.Text = "Login Password:";
            // 
            // txtPassword
            // 
            this.txtPassword.Enabled = false;
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Location = new Point(150, 167);
            this.txtPassword.Size = new Size(200, 23);
            // 
            // btnConnect
            // 
            this.btnConnect.BackColor = Color.FromArgb(37, 99, 235);
            this.btnConnect.FlatStyle = FlatStyle.Flat;
            this.btnConnect.ForeColor = Color.White;
            this.btnConnect.Location = new Point(150, 200);
            this.btnConnect.Size = new Size(200, 28);
            this.btnConnect.Text = "Connect and Get Databases";
            this.btnConnect.UseVisualStyleBackColor = false;
            // 
            // lblSourceDb
            // 
            this.lblSourceDb.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblSourceDb.Location = new Point(25, 245);
            this.lblSourceDb.Size = new Size(120, 20);
            this.lblSourceDb.Text = "Source Database:";
            // 
            // cbSourceDb
            // 
            this.cbSourceDb.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cbSourceDb.Enabled = false;
            this.cbSourceDb.Location = new Point(150, 242);
            this.cbSourceDb.Size = new Size(200, 23);
            // 
            // lblTables
            // 
            this.lblTables.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblTables.Location = new Point(380, 80);
            this.lblTables.Size = new Size(220, 20);
            this.lblTables.Text = "Check tables to WIPE (Unchecked will KEEP data):";
            // 
            // clbTables
            // 
            this.clbTables.BackColor = Color.FromArgb(30, 41, 59);
            this.clbTables.ForeColor = Color.White;
            this.clbTables.Location = new Point(380, 105);
            this.clbTables.Size = new Size(245, 184);
            this.clbTables.BorderStyle = BorderStyle.FixedSingle;
            // 
            // chkSelectAll
            // 
            this.chkSelectAll.AutoSize = true;
            this.chkSelectAll.ForeColor = Color.FromArgb(241, 245, 249);
            this.chkSelectAll.Location = new Point(380, 295);
            this.chkSelectAll.Size = new Size(120, 19);
            this.chkSelectAll.Text = "Select All / None";
            this.chkSelectAll.UseVisualStyleBackColor = true;
            this.chkSelectAll.Checked = true;
            // 
            // lblTargetDb
            // 
            this.lblTargetDb.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblTargetDb.Location = new Point(25, 305);
            this.lblTargetDb.Size = new Size(120, 20);
            this.lblTargetDb.Text = "Target DB Name:";
            // 
            // txtTargetDb
            // 
            this.txtTargetDb.Location = new Point(150, 302);
            this.txtTargetDb.Size = new Size(200, 23);
            // 
            // lblOutputPath
            // 
            this.lblOutputPath.ForeColor = Color.FromArgb(241, 245, 249);
            this.lblOutputPath.Location = new Point(25, 335);
            this.lblOutputPath.Size = new Size(120, 20);
            this.lblOutputPath.Text = "Output Bak Path:";
            // 
            // txtOutputPath
            // 
            this.txtOutputPath.Location = new Point(150, 332);
            this.txtOutputPath.Size = new Size(475, 23);
            // 
            // btnClone
            // 
            this.btnClone.BackColor = Color.FromArgb(16, 185, 129); // Green
            this.btnClone.Enabled = false;
            this.btnClone.FlatStyle = FlatStyle.Flat;
            this.btnClone.ForeColor = Color.White;
            this.btnClone.Location = new Point(150, 375);
            this.btnClone.Size = new Size(200, 30);
            this.btnClone.Text = "Clone, Clean & Package";
            this.btnClone.UseVisualStyleBackColor = false;
            // 
            // lblStatus
            // 
            this.lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
            this.lblStatus.Location = new Point(25, 420);
            this.lblStatus.Size = new Size(600, 40);
            this.lblStatus.Text = "Ready. Configure connection settings first.";
            // 
            // btnCancel
            // 
            this.btnCancel.BackColor = Color.FromArgb(71, 85, 105);
            this.btnCancel.FlatStyle = FlatStyle.Flat;
            this.btnCancel.ForeColor = Color.White;
            this.btnCancel.Location = new Point(365, 375);
            this.btnCancel.Size = new Size(100, 30);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = false;
            // 
            // FormDatabaseCloner
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(15, 23, 42); // slate-900
            this.ClientSize = new Size(650, 480);
            this.Controls.Add(this.chkSelectAll);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnClone);
            this.Controls.Add(this.txtOutputPath);
            this.Controls.Add(this.lblOutputPath);
            this.Controls.Add(this.txtTargetDb);
            this.Controls.Add(this.lblTargetDb);
            this.Controls.Add(this.clbTables);
            this.Controls.Add(this.lblTables);
            this.Controls.Add(this.cbSourceDb);
            this.Controls.Add(this.lblSourceDb);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.cbAuth);
            this.Controls.Add(this.lblAuth);
            this.Controls.Add(this.cbServer);
            this.Controls.Add(this.lblServer);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblHeader);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "FormDatabaseCloner";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Database Developer Wizard";
            this.DoubleBuffered = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw a subtle border
            using (var p = new Pen(Color.FromArgb(56, 189, 248), 2))
            {
                e.Graphics.DrawRectangle(p, 1, 1, this.Width - 2, this.Height - 2);
            }
        }

        private string GetConnectionString(bool useMaster = true)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = cbServer.Text.Trim(),
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            if (useMaster)
            {
                builder.InitialCatalog = "master";
            }
            else
            {
                builder.InitialCatalog = cbSourceDb.Text;
            }

            if (cbAuth.SelectedIndex == 0)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = txtUsername.Text.Trim();
                builder.Password = txtPassword.Text;
            }

            return builder.ConnectionString;
        }

        private async Task ConnectAndGetDatabasesAsync()
        {
            btnConnect.Enabled = false;
            lblStatus.Text = "Connecting to SQL Server...";

            try
            {
                string cs = GetConnectionString(useMaster: true);
                List<string> dbs = new List<string>();

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(cs))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name", conn))
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                dbs.Add(r.GetString(0));
                            }
                        }
                    }
                });

                cbSourceDb.Items.Clear();
                cbSourceDb.Items.AddRange(dbs.Cast<object>().ToArray());
                cbSourceDb.Enabled = dbs.Count > 0;

                if (dbs.Count > 0)
                {
                    cbSourceDb.SelectedIndex = 0;
                    lblStatus.Text = "Connected successfully. Select source database to load tables.";
                }
                else
                {
                    lblStatus.Text = "Connected, but no user databases were found on this instance.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect:\n\n" + ex.Message, "SQL Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Connection failed.";
            }
            finally
            {
                btnConnect.Enabled = true;
            }
        }

        private async Task LoadTablesAsync()
        {
            if (string.IsNullOrWhiteSpace(cbSourceDb.Text)) return;

            clbTables.Items.Clear();
            lblStatus.Text = $"Loading tables from {cbSourceDb.Text}...";

            try
            {
                string cs = GetConnectionString(useMaster: false);
                List<string> tables = new List<string>();

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(cs))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(@"
                            SELECT s.name + '.' + t.name 
                            FROM sys.tables t 
                            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id 
                            ORDER BY s.name, t.name", conn))
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                tables.Add(r.GetString(0));
                            }
                        }
                    }
                });

                clbTables.Items.Clear();
                clbTables.Items.AddRange(tables.Cast<object>().ToArray());

                // Auto-check all tables by default (so developer mostly wipes everything except specific metadata tables)
                for (int i = 0; i < clbTables.Items.Count; i++)
                {
                    clbTables.SetItemChecked(i, true);
                }

                btnClone.Enabled = true;
                lblStatus.Text = $"Loaded {tables.Count} tables. Uncheck tables you wish to keep data in.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading tables:\n\n" + ex.Message, "SQL Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error loading tables.";
            }
        }

        private async Task PerformCloneAsync()
        {
            string sourceDb = cbSourceDb.Text;
            string targetDb = txtTargetDb.Text.Trim();
            string outputBak = txtOutputPath.Text.Trim();

            if (string.IsNullOrWhiteSpace(sourceDb) || string.IsNullOrWhiteSpace(targetDb) || string.IsNullOrWhiteSpace(outputBak))
            {
                MessageBox.Show("Please fill all details.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (sourceDb.Equals(targetDb, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Target database name must be different from source database name to protect source data.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnClone.Enabled = false;
            btnCancel.Enabled = false;

            try
            {
                string csMaster = GetConnectionString(useMaster: true);

                // 1. Determine local SQL Server default directory for database files
                lblStatus.Text = "Querying SQL Server data directory...";
                string dataDir = "";
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(csMaster))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(@"
                            SELECT SUBSTRING(physical_name, 1, CHARINDEX('master.mdf', LOWER(physical_name)) - 1) 
                            FROM master.sys.master_files 
                            WHERE database_id = 1 AND file_id = 1", conn))
                        {
                            dataDir = cmd.ExecuteScalar() as string ?? "";
                        }
                    }
                });

                if (string.IsNullOrEmpty(dataDir))
                {
                    throw new Exception("Unable to retrieve SQL Server default directory path.");
                }

                string tempDir = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
                string tempBakPathOnServer = Path.Combine(tempDir, "temp_source_clone.bak");
                string cleanBakPathOnServer = Path.Combine(tempDir, "temp_clean_clone.bak");

                // 2. Backup source developer database to server temp path (SAFE read-only)
                lblStatus.Text = "Creating safe backup of source database...";
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(csMaster))
                    {
                        conn.Open();
                        string sql = $"BACKUP DATABASE [{sourceDb}] TO DISK = @bak WITH FORMAT, INIT, COPY_ONLY";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                            cmd.CommandTimeout = 300;
                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                // 3. Get logical files from backup
                lblStatus.Text = "Reading logical file descriptors...";
                string logicalDataName = "";
                string logicalLogName = "";
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(csMaster))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @bak", conn))
                        {
                            cmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string type = reader["Type"].ToString()?.Trim() ?? "";
                                    string logicalName = reader["LogicalName"].ToString() ?? "";
                                    if (type.Equals("D", StringComparison.OrdinalIgnoreCase)) logicalDataName = logicalName;
                                    else if (type.Equals("L", StringComparison.OrdinalIgnoreCase)) logicalLogName = logicalName;
                                }
                            }
                        }
                    }
                });

                // 4. Kill connections and restore backup as the new target database
                lblStatus.Text = $"Restoring clone database as [{targetDb}]...";
                string targetMdf = Path.Combine(dataDir, $"{targetDb}.mdf");
                string targetLdf = Path.Combine(dataDir, $"{targetDb}_log.ldf");

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(csMaster))
                    {
                        conn.Open();

                        // Kill existing target connections
                        string killSql = $@"
                            IF DB_ID('{targetDb}') IS NOT NULL
                            BEGIN
                                DECLARE @kill NVARCHAR(MAX) = N'';
                                SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N';'
                                FROM sys.dm_exec_sessions
                                WHERE database_id = DB_ID('{targetDb}');
                                IF @kill <> N'' EXEC(@kill);
                            END";
                        using (var killCmd = new SqlCommand(killSql, conn))
                        {
                            killCmd.ExecuteNonQuery();
                        }

                        // Restore
                        string restoreSql = $@"
                            RESTORE DATABASE [{targetDb}] 
                            FROM DISK = @bak 
                            WITH REPLACE, 
                                 MOVE N'{logicalDataName}' TO @dataFile, 
                                 MOVE N'{logicalLogName}' TO @logFile";

                        using (var cmd = new SqlCommand(restoreSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                            cmd.Parameters.AddWithValue("@dataFile", targetMdf);
                            cmd.Parameters.AddWithValue("@logFile", targetLdf);
                            cmd.CommandTimeout = 300;
                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                // 5. Connect to target database and wipe data in checked tables
                lblStatus.Text = "Purging selected tables on clone database...";
                var targetBuilder = new SqlConnectionStringBuilder(csMaster)
                {
                    InitialCatalog = targetDb
                };

                // Get checked tables list
                var tablesToWipe = clbTables.CheckedItems.Cast<string>().ToList();

                if (tablesToWipe.Count > 0)
                {
                    await Task.Run(() =>
                    {
                        using (var conn = new SqlConnection(targetBuilder.ConnectionString))
                        {
                            conn.Open();

                            // 5.1 Disable constraints
                            string disableSql = @"
                                DECLARE @sql NVARCHAR(MAX) = N'';
                                SELECT @sql = @sql + N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] NOCHECK CONSTRAINT ALL; '
                                FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;
                                EXEC sp_executesql @sql;";
                            using (var cmd = new SqlCommand(disableSql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // 5.2 Delete from checked tables
                            foreach (var table in tablesToWipe)
                            {
                                string deleteSql = $"DELETE FROM [{table.Split('.')[0]}].[{table.Split('.')[1]}]";
                                using (var cmd = new SqlCommand(deleteSql, conn))
                                {
                                    cmd.ExecuteNonQuery();
                                }

                                // Reset identity if table has identity column
                                string identitySql = $@"
                                    IF EXISTS(SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('[{table}]'))
                                    DBCC CHECKIDENT ('[{table}]', RESEED, 0);";
                                using (var cmd = new SqlCommand(identitySql, conn))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 5.3 Re-enable constraints
                            string enableSql = @"
                                DECLARE @sql NVARCHAR(MAX) = N'';
                                SELECT @sql = @sql + N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] CHECK CONSTRAINT ALL; '
                                FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;
                                EXEC sp_executesql @sql;";
                            using (var cmd = new SqlCommand(enableSql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                    });
                }

                // 6. Backup cleaned target database to server temp path
                lblStatus.Text = "Creating distribution package...";
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(csMaster))
                    {
                        conn.Open();
                        string sql = $"BACKUP DATABASE [{targetDb}] TO DISK = @bak WITH FORMAT, INIT";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@bak", cleanBakPathOnServer);
                            cmd.CommandTimeout = 300;
                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                // 7. Copy file from SQL Server directory to build folder (bypasses permissions issues)
                lblStatus.Text = "Deploying packaged backup to DbSchema folder...";
                await Task.Run(() =>
                {
                    string outDir = Path.GetDirectoryName(outputBak) ?? "";
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }

                    if (File.Exists(outputBak))
                    {
                        File.Delete(outputBak);
                    }

                    File.Copy(cleanBakPathOnServer, outputBak);
                });

                // 8. Clean up temporary files on SQL server host
                await Task.Run(() =>
                {
                    try { File.Delete(tempBakPathOnServer); } catch { }
                    try { File.Delete(cleanBakPathOnServer); } catch { }
                });

                ResultConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = cbServer.Text.Trim(),
                    InitialCatalog = targetDb,
                    TrustServerCertificate = true,
                    IntegratedSecurity = (cbAuth.SelectedIndex == 0)
                }.ConnectionString;

                lblStatus.Text = "Success! Database cloned and distribution package generated.";
                MessageBox.Show($"Successfully completed:\n\n1. Database cloned as: {targetDb}\n2. Purged {tablesToWipe.Count} tables\n3. Package saved to: {outputBak}", "Developer Setup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred during clone operations:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Clone failed.";
            }
            finally
            {
                btnClone.Enabled = true;
                btnCancel.Enabled = true;
            }
        }
    }
}
