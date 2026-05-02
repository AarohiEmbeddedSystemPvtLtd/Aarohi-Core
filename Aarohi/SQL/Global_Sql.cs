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
    }
}
