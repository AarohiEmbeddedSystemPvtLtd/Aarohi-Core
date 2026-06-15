using Microsoft.Data.SqlClient;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.SQL
{
    public partial class FormDbSplash : Form
    {
        private readonly string _appName;
        private readonly string _bakFilePath;

        private Label lblHeader;
        private Label lblTitle;
        private Label lblStatus;
        private Label lblDetail;
        private Label lblPercent;

        private int progressPercent = 0;

        public string? ServerInstance { get; private set; }
        public string? ResultConnectionString { get; private set; }
        public Exception? Error { get; private set; }

        public FormDbSplash(string appName, string bakFilePath)
        {
            _appName = appName;
            _bakFilePath = bakFilePath;
            InitializeComponent();
            lblTitle.Text = $"Creating database for: {_appName}";
        }

        private void InitializeComponent()
        {
            this.lblHeader = new Label();
            this.lblTitle = new Label();
            this.lblStatus = new Label();
            this.lblDetail = new Label();
            this.lblPercent = new Label();
            this.SuspendLayout();
            // 
            // lblHeader
            // 
            this.lblHeader.AutoSize = true;
            this.lblHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblHeader.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
            this.lblHeader.Location = new Point(30, 25);
            this.lblHeader.Name = "lblHeader";
            this.lblHeader.Size = new Size(161, 15);
            this.lblHeader.Text = "AAROHI DATABASE BUILDER";
            // 
            // lblTitle
            // 
            this.lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(30, 50);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(440, 55);
            this.lblTitle.Text = "Creating database for app...";
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.lblStatus.ForeColor = Color.FromArgb(241, 245, 249); // slate-50
            this.lblStatus.Location = new Point(30, 115);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(440, 23);
            this.lblStatus.Text = "Initializing...";
            // 
            // lblDetail
            // 
            this.lblDetail.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
            this.lblDetail.ForeColor = Color.FromArgb(148, 163, 184); // slate-400
            this.lblDetail.Location = new Point(30, 142);
            this.lblDetail.Name = "lblDetail";
            this.lblDetail.Size = new Size(440, 40);
            this.lblDetail.Text = "Starting process...";
            // 
            // lblPercent
            // 
            this.lblPercent.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblPercent.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
            this.lblPercent.Location = new Point(410, 210);
            this.lblPercent.Name = "lblPercent";
            this.lblPercent.Size = new Size(60, 23);
            this.lblPercent.Text = "0%";
            this.lblPercent.TextAlign = ContentAlignment.TopRight;
            // 
            // FormDbSplash
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(15, 23, 42); // Dark slate
            this.ClientSize = new Size(500, 260);
            this.Controls.Add(this.lblPercent);
            this.Controls.Add(this.lblDetail);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblHeader);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "FormDbSplash";
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Aarohi Database Setup";
            this.DoubleBuffered = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Run creation task on background thread
            _ = Task.Run(PerformDatabaseCreationAsync);
        }

        private void UpdateStatus(int percent, string status, string detail)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateStatus(percent, status, detail)));
                return;
            }

            progressPercent = percent;
            lblStatus.Text = status;
            lblDetail.Text = detail;
            lblPercent.Text = $"{percent}%";
            this.Invalidate(); // Redraw custom progress bar
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Draw custom border
            using (var borderPen = new Pen(Color.FromArgb(56, 189, 248), 2)) // Cyan
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 2, this.Height - 2);
            }

            // 2. Draw Progress Bar Track
            int barX = 30;
            int barY = 190;
            int barWidth = this.Width - 60;
            int barHeight = 12;
            int radius = 6;

            using (var trackBrush = new SolidBrush(Color.FromArgb(30, 41, 59))) // Slate-800
            {
                FillRoundedRectangle(g, trackBrush, barX, barY, barWidth, barHeight, radius);
            }

            // 3. Draw Progress Bar Fill
            if (progressPercent > 0)
            {
                int fillWidth = (int)(barWidth * (progressPercent / 100.0));
                if (fillWidth > 0)
                {
                    int drawWidth = Math.Max(fillWidth, radius * 2);
                    using (var fillBrush = new LinearGradientBrush(
                        new Point(barX, barY),
                        new Point(barX + drawWidth, barY),
                        Color.FromArgb(37, 99, 235), // Royal Blue
                        Color.FromArgb(56, 189, 248)  // Cyan
                    ))
                    {
                        FillRoundedRectangle(g, fillBrush, barX, barY, fillWidth, barHeight, radius);
                    }
                }
            }
        }

        private void FillRoundedRectangle(Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.StartFigure();
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + width - d, y, d, d, 270, 90);
                path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
                path.AddArc(x, y + height - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        private async Task PerformDatabaseCreationAsync()
        {
            string tempBakPathOnServer = "";
            bool copiedTemp = false;
            try
            {
                // To bypass SQL Server service account permission issues reading from private user directories,
                // copy the backup file to the public folder first.
                string tempDir = System.Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
                tempBakPathOnServer = Path.Combine(tempDir, $"temp_restore_{Guid.NewGuid():N}.bak");
                
                try
                {
                    UpdateStatus(2, "Preparing backup file...", "Copying template to public folder...");
                    await Task.Run(() => File.Copy(_bakFilePath, tempBakPathOnServer, true));
                    copiedTemp = true;
                }
                catch (Exception)
                {
                    // Fallback to original file if copy fails for any reason
                    tempBakPathOnServer = _bakFilePath;
                }

                UpdateStatus(5, "Scanning for local SQL Server...", "Testing connections on common instances...");
                string? server = FindLocalSqlServer(out string scanError);
                if (string.IsNullOrEmpty(server))
                {
                    throw new Exception("No local SQL Server instance could be connected using Windows Authentication.\n\nError: " + scanError);
                }

                ServerInstance = server;
                UpdateStatus(20, $"Connected to local SQL Server: {server}", "Reading file list from backup template...");

                var csb = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = "master",
                    TrustServerCertificate = true,
                    ConnectTimeout = 10,
                    IntegratedSecurity = true
                };

                string logicalDataName = "";
                string logicalLogName = "";

                using (var con = new SqlConnection(csb.ConnectionString))
                {
                    await con.OpenAsync();

                    // 1. Get logical files from backup
                    using (var fileListCmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @bak", con))
                    {
                        fileListCmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                        using (var reader = await fileListCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string type = reader["Type"].ToString()?.Trim() ?? "";
                                string logicalName = reader["LogicalName"].ToString() ?? "";
                                if (type.Equals("D", StringComparison.OrdinalIgnoreCase))
                                {
                                    logicalDataName = logicalName;
                                }
                                else if (type.Equals("L", StringComparison.OrdinalIgnoreCase))
                                {
                                    logicalLogName = logicalName;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(logicalDataName) || string.IsNullOrEmpty(logicalLogName))
                    {
                        throw new InvalidOperationException("Could not read logical database file names from the backup file.");
                    }

                    // 2. Query default SQL Server data path (prevents file system access permission issues)
                    string dataDir = "";
                    using (var pathCmd = new SqlCommand(@"
                        SELECT SUBSTRING(physical_name, 1, CHARINDEX('master.mdf', LOWER(physical_name)) - 1) 
                        FROM master.sys.master_files 
                        WHERE database_id = 1 AND file_id = 1", con))
                    {
                        dataDir = (await pathCmd.ExecuteScalarAsync()) as string ?? "";
                    }

                    if (string.IsNullOrEmpty(dataDir))
                    {
                        dataDir = Path.GetDirectoryName(tempBakPathOnServer) ?? @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\DATA";
                    }

                    string dataFilePath = Path.Combine(dataDir, $"{_appName}.mdf");
                    string logFilePath = Path.Combine(dataDir, $"{_appName}_log.ldf");

                    UpdateStatus(35, "Preparing database files...", $"Path: {dataDir}");

                    // 3. Kill existing connections to the target DB if it exists
                    using (var killCmd = new SqlCommand(@"
                        IF DB_ID(@db) IS NOT NULL
                        BEGIN
                            DECLARE @kill NVARCHAR(MAX) = N'';
                            SELECT @kill = @kill + N'KILL ' + CAST(session_id AS NVARCHAR(10)) + N';'
                            FROM sys.dm_exec_sessions
                            WHERE database_id = DB_ID(@db);
                            IF @kill <> N'' EXEC(@kill);
                        END", con))
                    {
                        killCmd.Parameters.AddWithValue("@db", _appName);
                        await killCmd.ExecuteNonQueryAsync();
                    }

                    // 4. Run Restore Database with progress tracking
                    string restoreSql = $@"
                        RESTORE DATABASE [{_appName}]
                        FROM DISK = @bak
                        WITH REPLACE,
                             MOVE N'{logicalDataName}' TO @dataFile,
                             MOVE N'{logicalLogName}' TO @logFile,
                             RECOVERY,
                             STATS = 10;";

                    UpdateStatus(45, "Restoring database from template...", "Starting SQL Server restore engine...");

                    con.InfoMessage += (s, args) =>
                    {
                        foreach (SqlError err in args.Errors)
                        {
                            if (err.Message != null && err.Message.Contains("percent processed"))
                            {
                                var parts = err.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 0 && int.TryParse(parts[0], out int pct))
                                {
                                    // Map 0-100% restore progress to 45-98% overall progress
                                    int overallPct = 45 + (int)(pct * 0.53);
                                    UpdateStatus(overallPct, "Restoring database from template...", $"{pct}% completed.");
                                }
                            }
                        }
                    };

                    using (var restoreCmd = new SqlCommand(restoreSql, con))
                    {
                        restoreCmd.Parameters.AddWithValue("@bak", tempBakPathOnServer);
                        restoreCmd.Parameters.AddWithValue("@dataFile", dataFilePath);
                        restoreCmd.Parameters.AddWithValue("@logFile", logFilePath);
                        restoreCmd.CommandTimeout = 600; // 10 minutes

                        await restoreCmd.ExecuteNonQueryAsync();
                    }
                }

                UpdateStatus(100, "Database creation complete!", "Ready to configure.");
                await Task.Delay(500); // Brief delay for smooth UI feedback

                ResultConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = _appName,
                    TrustServerCertificate = true,
                    IntegratedSecurity = true
                }.ConnectionString;

                this.Invoke(new Action(() =>
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }));
            }
            catch (Exception ex)
            {
                Error = ex;
                this.Invoke(new Action(() =>
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }));
            }
            finally
            {
                if (copiedTemp && !string.IsNullOrEmpty(tempBakPathOnServer))
                {
                    try { File.Delete(tempBakPathOnServer); } catch { }
                }
            }
        }

        private string? FindLocalSqlServer(out string errorMsg)
        {
            errorMsg = "";
            string pcName = Environment.MachineName;
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
                    ConnectTimeout = 2, // 2 seconds timeout to scan quickly
                    IntegratedSecurity = true
                };

                try
                {
                    using (var con = new SqlConnection(csb.ConnectionString))
                    {
                        con.Open();
                        return server; // Connected!
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            }
            return null;
        }
    }
}
