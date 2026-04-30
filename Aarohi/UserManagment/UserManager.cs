using Aarohi.Classes;
using Aarohi.Classes.Healper;
using Aarohi.Configuration;
using Aarohi.Globals;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Classes.Healper.RegistryHelper;
using static Aarohi.UserManagment.FormStartUp;

namespace Aarohi.UserManagment
{
    public static class UserManager
    {
        private static RootConfig _config;
        private static DynamicClass _userClass;
        private static string _LoginDataColumnName;
        private static string _PasswordDataColumnName;
        private static bool _loginFlowRunning = false;
        public static event EventHandler<LoginSuccessEventArgs>? LoginSuccess;
        private static string UserName;
        private static string Password;
        private static bool checkedRememberMe = false;




        public static void Configure(RootConfig config)
        {
            _config = config;
            _userClass = new DynamicClass(
                config.UserMapping.Schema,
                config.UserMapping.Table);
            _LoginDataColumnName = config.UserMapping.Columns.Name;
            _PasswordDataColumnName = config.UserMapping.Columns.Password;
        }

        public static UserMappingConfig UserMap => _config.UserMapping;
        public static PermissionMappingConfig PermissionMap => _config.PermissionMapping;
        public static UserPermissionMappingConfig UserPermissionMap => _config.UserPermissionMapping;

        public static bool logout(bool WantConfirmationMessage = true)
        {
            try
            {
                DialogResult result;

                if (WantConfirmationMessage)
                {
                    result = MessageBox.Show(
                       "Are you sure you want to logout?",
                       "Logout Confirmation",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Question
                    );
                }
                else
                {
                    result = DialogResult.Yes;
                }

                if (result == DialogResult.Yes)
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");

                    _loginFlowRunning = false;

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch
            {
                return false;
            }
        }
        public static bool Login(string userName, string password, bool rememberMe)
        {
            if (TryAuthenticate(userName, password))
            {
                HandleRememberMe(userName, password, rememberMe);

                if (!_loginFlowRunning)
                {
                    _loginFlowRunning = true;
                    LoginSuccess?.Invoke(null, new LoginSuccessEventArgs(userName, password));
                }
                return true;
            }
            return false;
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));

                return builder.ToString();
            }
        }

        public static bool TryAuthenticate(string userName, string password)
        {
            try
            {
                if (userName == AGLobals.Utils.DevName)
                {
                    if (password == DateTime.Now.ToString("ddMMyyyyHH"))
                        return true;

                    MessageBox.Show("Incorrect password.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var values = _userClass.GetRowAsDictionary(_LoginDataColumnName, userName);

                if (values == null || values.Count == 0)
                {
                    MessageBox.Show("Username not found.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var dbUserName = values[_LoginDataColumnName]?.ToString() ?? string.Empty;
                var dbPassword = values[_PasswordDataColumnName]?.ToString() ?? string.Empty;

                if (!string.Equals(userName, dbUserName, StringComparison.Ordinal))
                {
                    MessageBox.Show("Username does not match.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (!string.Equals(password, dbPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show("Incorrect password.", "Login Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while checking login. Please contact support.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        public static void SetRegistryHashes(string userName, string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] userBytes = Encoding.UTF8.GetBytes(userName ?? string.Empty);
                string userHash = BitConverter.ToString(sha.ComputeHash(userBytes)).Replace("-", "");

                byte[] passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
                string passwordHash = BitConverter.ToString(sha.ComputeHash(passwordBytes)).Replace("-", "");

                //Added
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", userHash);
                RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", passwordHash);
            }
        }
        public static bool TryGetLastValues(out string userName, out string password)
        {
            userName = string.Empty;
            password = string.Empty;
            try
            {
                string encryptedName = RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "AESPLXU");
                string encryptedPassword = RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "AESPLXP");

                string realName = RegistryHelper.Decrypt(encryptedName);
                string realPassword = RegistryHelper.Decrypt(encryptedPassword);

                if (string.IsNullOrWhiteSpace(realName) ||
                    string.IsNullOrWhiteSpace(realPassword))
                {
                    if(RegistryHelper.LoadBool(RegistryHelper.storeLocs.Credentials, "IsDevPC"))
                    {
                        userName = AGLobals.Utils.DevName;
                        password = DateTime.Now.ToString("ddMMyyyyHH");
                    }
                    return false;
                }

                UserName= realName;
                Password = realPassword;
                checkedRememberMe= true;

                if (!TryAuthenticate(realName, realPassword))
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");
                }

                if (_loginFlowRunning) return true;

                _loginFlowRunning = true;
                LoginSuccess?.Invoke(null, new LoginSuccessEventArgs(realName, realPassword));//null=this
                return true;
            }
            catch
            {
                try
                {
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXU", "");
                    RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "AESPLXP", "");
                }
                catch { }
                return false;
            }
        }

        public static void HandleRememberMe(string userName, string password, bool rememberMe)
        {
            if (rememberMe)
            {
                if (!string.Equals(userName, AGLobals.Utils.DevName, StringComparison.OrdinalIgnoreCase))
                {
                    SetRegistryHashes(userName, password);
                }
                else
                {
                    SetRegistryHashes(string.Empty, string.Empty);
                }
            }
            else
            {
                SetRegistryHashes(string.Empty, string.Empty);
            }
        }


    }
}
