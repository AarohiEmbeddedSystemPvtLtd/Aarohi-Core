using Aarohi.Classes.Healper;
using Aarohi.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.UserManagment
{
    public static class UserManager
    {
        private static RootConfig _config;

        public static void Configure(RootConfig config)
        {
            _config = config;
        }

        public static UserMappingConfig UserMap => _config.UserMapping;
        public static PermissionMappingConfig PermissionMap => _config.PermissionMapping;
        public static UserPermissionMappingConfig UserPermissionMap => _config.UserPermissionMapping;

        private static readonly string LoginInfoPath = Path.Combine(Environment.GetFolderPath
           (Environment.SpecialFolder.ApplicationData), "Aarohi", "IPTS_Git", "Login.info");

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

                    if (File.Exists(LoginInfoPath))
                    {
                        File.WriteAllText(LoginInfoPath, "" + Environment.NewLine + "");
                    }
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
    }
}
