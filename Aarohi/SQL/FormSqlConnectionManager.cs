using Aarohi.Classes.Healper;
using Aarohi.ExtendedUI;
using DocumentFormat.OpenXml.Math;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace Aarohi.SQL
{
    public partial class FormSqlConnectionManager : Form
    {
        private readonly string _appName;
        private readonly string _regPath;

        private bool isClosedFromSave = false;
        public string? SavedConnectionString { get; private set; }

        private bool _isDatabaseLoading = false;
        private string _lastDatabaseLoadKey = "";

        public FormSqlConnectionManager(string appName)
        {
            InitializeComponent();
            _appName = string.IsNullOrWhiteSpace(appName) ? "IPTS" : appName.Trim();
            LabelHeader.Text += $" ({_appName})";
            _regPath = $@"Software\Aarohi Embedded Systems Pvt Ltd\{_appName}\Database";
        }

        private void comboBoxAuth_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxAuth.SelectedIndex == 0)
            {
                // Windows Authentication
                extendedPanel4.Enabled = false;
                extendedPanel3.Enabled = false;
            }
            else if (comboBoxAuth.SelectedIndex == 1)
            {
                // SQL Server Authentication
                extendedPanel4.Enabled = true;
                extendedPanel3.Enabled = true;
            }
            else
            {
                MessageBox.Show("Invalid Selection!");
            }
        }
        private static bool TryLoadSqlSettings(string regPath,
    out string server, out string database, out bool useWindowsAuth,
    out string userName, out string password)
        {
            server = database = userName = password = "";
            useWindowsAuth = true;

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath);
            if (key == null) return false;

            server = key.GetValue("Server", "") as string ?? "";
            database = key.GetValue("Database", "") as string ?? "";
            string authMode = key.GetValue("AuthMode", "Windows") as string ?? "Windows";
            userName = key.GetValue("UserName", "") as string ?? "";
            password = key.GetValue("Password", "") as string ?? "";

            useWindowsAuth = authMode.Equals("Windows", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static void SaveSqlSettings(string regPath,
            string server, string database, bool useWindowsAuth,
            string userName, string password)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath);
            key.SetValue("Server", server);
            key.SetValue("Database", database);
            key.SetValue("AuthMode", useWindowsAuth ? "Windows" : "SQL");
            key.SetValue("UserName", userName ?? "");
            key.SetValue("Password", password ?? "");
        }


        private async void FormSqlConnectionManager_Load(object sender, EventArgs e)
        {
            isClosedFromSave = false;
            PanelDataHolderWrapper.Enabled = false;
            ButtonSave.Enabled = false;

            comboBoxHostname.DropDownStyle = ComboBoxStyle.DropDown;
            ComboboxDatabaseName.DropDownStyle = ComboBoxStyle.DropDownList;

            await LoadSqlServersAsync();

            try
            {
                if (TryLoadSqlSettings(_regPath, out string server, out string database, out bool useWindowsAuth, out string userName, out string password))
                {
                    // Hostname
                    if (!string.IsNullOrWhiteSpace(server))
                    {
                        int idx = comboBoxHostname.Items.IndexOf(server);
                        if (idx >= 0)
                            comboBoxHostname.SelectedIndex = idx;
                        else
                            comboBoxHostname.Text = server;
                    }

                    comboBoxAuth.SelectedIndex = useWindowsAuth ? 0 : 1;

                    if (!string.IsNullOrWhiteSpace(database))
                        ComboboxDatabaseName.Text = database;

                    textBoxUserName.Text = userName ?? string.Empty;
                    textBoxPassword.Text = password ?? string.Empty;
                }
                else
                {
                    comboBoxAuth.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error loading SQL settings from Registry:\n" + ex.Message,
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                comboBoxAuth.SelectedIndex = 0;
            }

            PanelDataHolderWrapper.Enabled = true;
        }

        private async Task LoadSqlServersAsync()
        {
            comboBoxHostname.Enabled = false;

            // Save the currently selected or typed hostname to restore it later
            string currentHost = comboBoxHostname.Text;

            comboBoxHostname.Items.Clear();
            comboBoxHostname.Items.Add("Loading...");
            comboBoxHostname.SelectedIndex = 0;

            List<string> servers = new List<string>();
            bool enableNetworkDiscovery = toggleNetworkDiscovery != null && toggleNetworkDiscovery.Checked;

            try
            {
                servers = await Task.Run(() =>
                {
                    List<string> list = new List<string>();

                    if (enableNetworkDiscovery)
                    {
                        try
                        {
                            DataTable dt = SqlDataSourceEnumerator.Instance.GetDataSources();

                            foreach (DataRow row in dt.Rows)
                            {
                                string server = row["ServerName"]?.ToString();
                                string instance = row["InstanceName"]?.ToString();

                                if (string.IsNullOrWhiteSpace(server))
                                    continue;

                                string full = string.IsNullOrWhiteSpace(instance)
                                    ? server
                                    : $@"{server}\{instance}";

                                if (!list.Contains(full))
                                    list.Add(full);
                            }
                        }
                        catch
                        {
                            // ignore scan errors
                        }

                        string pc = Environment.MachineName;

                        // most common local instance
                        list.Add($@"{pc}\SQLEXPRESS");
                    }
                    else
                    {
                        string pc = Environment.MachineName;
                        list.Add(pc);
                        list.Add($@"{pc}\SQLEXPRESS");
                    }

                    return list.Distinct().OrderBy(x => x).ToList();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error scanning SQL Servers:\n" + ex.Message);
            }

            comboBoxHostname.Items.Clear();
            comboBoxHostname.Items.AddRange(servers.Cast<object>().ToArray());

            bool assigned = false;

            // Try to restore the previous selection or text
            if (!string.IsNullOrWhiteSpace(currentHost) && currentHost != "Loading...")
            {
                int idx = comboBoxHostname.Items.IndexOf(currentHost);
                if (idx >= 0)
                {
                    comboBoxHostname.SelectedIndex = idx;
                    assigned = true;
                }
                else
                {
                    comboBoxHostname.Text = currentHost;
                    assigned = true;
                }
            }

            if (!assigned)
            {
                // ---- AUTO SELECT CURRENT PC SERVER ----
                string pcName = Environment.MachineName;

                string[] preferred =
                {
                    $@"{pcName}\SQLEXPRESS",
                    @".\SQLEXPRESS",
                    $@"{pcName}",
                    @"(local)",
                    @"."
                };

                foreach (var p in preferred)
                {
                    if (servers.Contains(p))
                    {
                        comboBoxHostname.SelectedItem = p;
                        assigned = true;
                        break;
                    }
                }

                if (!assigned && comboBoxHostname.Items.Count > 0)
                    comboBoxHostname.SelectedIndex = 0;
            }

            comboBoxHostname.Enabled = true;
            PanelDataHolderWrapper.Enabled = true;
            comboBoxHostname.SelectAll();
        }

        //private async void ComboboxDatabaseName_DropDown(object sender, EventArgs e)
        //{
        //    string server = comboBoxHostname.Text?.Trim();

        //    if (string.IsNullOrEmpty(server))
        //    {
        //        MessageBox.Show("Please select or enter SQL Server host first.");
        //        return;
        //    }

        //    bool useWindowsAuth = comboBoxAuth.SelectedIndex == 0;

        //    string userName = textBoxUserName.Text.Trim();
        //    string password = textBoxPassword.Text;

        //    if (!useWindowsAuth)
        //    {
        //        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        //        {
        //            MessageBox.Show("Please enter SQL Server user name and password.");
        //            return;
        //        }
        //    }

        //    ComboboxDatabaseName.Enabled = false;
        //    ComboboxDatabaseName.Items.Clear();
        //    ComboboxDatabaseName.Items.Add("Loading...");
        //    ComboboxDatabaseName.SelectedIndex = 0;

        //    List<string> dbs;

        //    try
        //    {
        //        dbs = await GetDatabaseListAsync(server, useWindowsAuth, userName, password);
        //    }
        //    catch (Exception ex)
        //    {
        //        ComboboxDatabaseName.Items.Clear();
        //        ComboboxDatabaseName.Items.Add("Error");
        //        ComboboxDatabaseName.SelectedIndex = 0;
        //        ComboboxDatabaseName.Enabled = true;

        //        MessageBox.Show(
        //            "Unable to read databases from server:\n" + ex.Message,
        //            "SQL Error",
        //            MessageBoxButtons.OK,
        //            MessageBoxIcon.Error);

        //        return;
        //    }

        //    ComboboxDatabaseName.Items.Clear();

        //    if (dbs.Count == 0)
        //    {
        //        ComboboxDatabaseName.Items.Add("(No databases found)");
        //        ComboboxDatabaseName.SelectedIndex = 0;
        //    }
        //    else
        //    {
        //        ComboboxDatabaseName.Items.AddRange(dbs.Cast<object>().ToArray());
        //        ComboboxDatabaseName.SelectedIndex = 0;
        //    }

        //    ComboboxDatabaseName.Enabled = true;
        //}

        private async void ComboboxDatabaseName_DropDown(object sender, EventArgs e)
        {
            if (_isDatabaseLoading)
                return;

            string server = comboBoxHostname.Text?.Trim();

            if (string.IsNullOrEmpty(server))
            {
                MessageBox.Show("Please select or enter SQL Server host first.");
                return;
            }

            bool useWindowsAuth = comboBoxAuth.SelectedIndex == 0;
            string userName = textBoxUserName.Text.Trim();
            string password = textBoxPassword.Text;

            if (!useWindowsAuth)
            {
                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Please enter SQL Server user name and password.");
                    return;
                }
            }

            string loadKey = $"{server}|{useWindowsAuth}|{userName}";

            // Important:
            // If database list is already loaded for same server/auth,
            // do not reload again. Let user select normally.
            if (ComboboxDatabaseName.Items.Count > 0 && _lastDatabaseLoadKey == loadKey)
                return;

            string previousSelection = ComboboxDatabaseName.SelectedItem?.ToString();

            try
            {
                _isDatabaseLoading = true;

                ComboboxDatabaseName.Items.Clear();
                ComboboxDatabaseName.Items.Add("Loading...");
                ComboboxDatabaseName.SelectedIndex = 0;

                List<string> dbs = await GetDatabaseListAsync(server, useWindowsAuth, userName, password);

                ComboboxDatabaseName.Items.Clear();

                if (dbs.Count == 0)
                {
                    ComboboxDatabaseName.Items.Add("(No databases found)");
                    ComboboxDatabaseName.SelectedIndex = 0;
                    return;
                }

                ComboboxDatabaseName.Items.AddRange(dbs.Cast<object>().ToArray());

                // Do not force index 0 always
                if (!string.IsNullOrWhiteSpace(previousSelection) && dbs.Contains(previousSelection))
                {
                    ComboboxDatabaseName.SelectedItem = previousSelection;
                }
                else
                {
                    ComboboxDatabaseName.SelectedIndex = -1; // user can select manually
                }

                _lastDatabaseLoadKey = loadKey;

                // Open dropdown again after loading
                BeginInvoke(new Action(() =>
                {
                    if (!ComboboxDatabaseName.IsDisposed)
                        ComboboxDatabaseName.DroppedDown = true;
                }));
            }
            catch (Exception ex)
            {
                ComboboxDatabaseName.Items.Clear();
                ComboboxDatabaseName.Items.Add("Error");
                ComboboxDatabaseName.SelectedIndex = 0;

                MessageBox.Show(
                    "Unable to read databases from server:\n" + ex.Message,
                    "SQL Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isDatabaseLoading = false;
            }
        }


        private Task<List<string>> GetDatabaseListAsync(string server,
                                                        bool useWindowsAuth,
                                                        string userName,
                                                        string password)
        {
            return Task.Run(() =>
            {
                var result = new List<string>();

                var csBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = "master",
                    TrustServerCertificate = true,
                    ConnectTimeout = 30
                };

                if (useWindowsAuth)
                {
                    csBuilder.IntegratedSecurity = true;
                }
                else
                {
                    csBuilder.IntegratedSecurity = false;
                    csBuilder.UserID = userName;
                    csBuilder.Password = password;
                }

                //using (var conn = new SqlConnection("Server=61.0.160.172;Database=master;User Id=panel;Password=Aespl@2037;"))
                using (var conn = new SqlConnection(csBuilder.ConnectionString))
                {
                    try
                    {
                        conn.Open();
                    }
                    catch (SqlException ex)
                    {
                        throw new Exception(
                            "Failed to connect to SQL Server.\n\n" +
                            "Reason: " + ex.Message + "\n\n" +
                            "Possible Causes:\n" +
                            "• Windows Authentication cannot be used for remote SQL Servers.\n" +
                            "• SQL Server service not running.\n" +
                            "• SQL Browser disabled.\n" +
                            "• Wrong credentials (if SQL Auth).\n" +
                            "• Firewall blocking SQL Server port (1433).");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            "Unexpected error while connecting:\n" + ex.Message);
                    }


                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name FROM sys.databases ORDER BY name";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string name = reader.GetString(0);
                                result.Add(name);
                            }
                        }
                    }
                }

                return result;
            });
        }

        private string BuildConnectionString(bool useMasterDb = false)
        {
            string server = comboBoxHostname.Text.Trim();
            string database = ComboboxDatabaseName.Text.Trim();
            bool useWindowsAuth = comboBoxAuth.SelectedIndex == 0;

            string userName = textBoxUserName.Text.Trim();
            string password = textBoxPassword.Text;

            if (string.IsNullOrWhiteSpace(server))
                throw new Exception("SQL Server host is empty.");

            var csBuilder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                TrustServerCertificate = true,
                ConnectTimeout = 30
            };

            csBuilder.InitialCatalog = useMasterDb ? "master" : (string.IsNullOrWhiteSpace(database) ? "master" : database);

            if (useWindowsAuth)
            {
                csBuilder.IntegratedSecurity = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(userName))
                    throw new Exception("SQL user name is empty.");

                csBuilder.IntegratedSecurity = false;
                csBuilder.UserID = userName;
                csBuilder.Password = password;
            }

            return csBuilder.ConnectionString;
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            string server = comboBoxHostname.Text.Trim();
            string database = ComboboxDatabaseName.Text.Trim();
            bool useWindowsAuth = comboBoxAuth.SelectedIndex == 0;
            string userName = textBoxUserName.Text.Trim();
            string password = textBoxPassword.Text;

            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show("Please select or enter SQL Server host.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(database))
            {
                MessageBox.Show("Please select or enter database name.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!useWindowsAuth && string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("Please enter SQL user name for SQL Authentication.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SaveSqlSettings(_regPath, server, database, useWindowsAuth, userName, password);
                SavedConnectionString = BuildConnectionString(useMasterDb: false);

                //this.DialogResult = DialogResult.OK;
                isClosedFromSave = true;
                this.Close();
            }
            catch (Exception ex)
            {
                isClosedFromSave = false;
                MessageBox.Show("Failed to save settings to Registry:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static bool TryConfigure(IWin32Window owner, string appName, out string connectionString)
        {
            using var frm = new FormSqlConnectionManager(appName);
            var result = frm.ShowDialog(owner);

            connectionString = frm.SavedConnectionString ?? "";
            return result == DialogResult.OK && !string.IsNullOrWhiteSpace(connectionString);
        }

        private async void ButtonTestConnection_Click(object sender, EventArgs e)
        {
            ButtonTestConnection.Enabled = false;
            ButtonSave.Enabled = false;

            string connString;
            string DBName = ComboboxDatabaseName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(DBName))
            {
                try
                {
                    connString = BuildConnectionString(useMasterDb: false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    ButtonTestConnection.Enabled = true;
                    return;
                }

                try
                {
                    using var con = new SqlConnection(connString);
                    await con.OpenAsync();

                    MessageBox.Show("Connection successful!", "SQL Connection",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    ButtonSave.Enabled = true;
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Failed to connect to SQL Server.\n\n" +
                        "Message: " + ex.Message,
                        "SQL Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unexpected error while testing connection:\n" + ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    ButtonTestConnection.Enabled = true;
                }
            }
            else
                MessageBox.Show("Please select or enter SQL Server Database first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void FormSqlConnectionManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isClosedFromSave == true)
            {
                isClosedFromSave = false;
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        private async void toggleNetworkDiscovery_CheckedChanged(object sender, EventArgs e)
        {
            await LoadSqlServersAsync();
        }
    }

    public class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Size = new Size(250, 40);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaintBackground(pevent);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int width = 50;
            int height = 26;
            int toggleSize = 20;

            // Draw background capsule
            Rectangle rect = new Rectangle(0, (this.Height - height) / 2, width, height);
            using (var path = GetRoundPath(rect, height))
            {
                Color bgColor = Checked ? Color.MediumBlue : Color.LightGray;
                using (Brush brush = new SolidBrush(bgColor))
                {
                    pevent.Graphics.FillPath(brush, path);
                }
            }

            // Draw switch ellipse
            int left = Checked ? (width - toggleSize - 3) : 3;
            Rectangle toggleRect = new Rectangle(left, (this.Height - toggleSize) / 2, toggleSize, toggleSize);
            using (Brush brush = new SolidBrush(Color.White))
            {
                pevent.Graphics.FillEllipse(brush, toggleRect);
            }

            // Draw text
            if (!string.IsNullOrEmpty(Text))
            {
                Rectangle textRect = new Rectangle(width + 8, 0, this.Width - width - 8, this.Height);
                TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        private GraphicsPath GetRoundPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

