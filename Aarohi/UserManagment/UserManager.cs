using Aarohi.Classes.Healper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.UserManagment
{
    public static class UserManager
    {
        private static readonly string LoginInfoPath = Path.Combine(Environment.GetFolderPath
           (Environment.SpecialFolder.ApplicationData), "Aarohi", "IPTS_Git", "Login.info");

        public static bool logout()
        {

            try
            {
                DialogResult result = MessageBox.Show(
               "Are you sure you want to logout?",
               "Logout Confirmation",
               MessageBoxButtons.YesNo,
               MessageBoxIcon.Question
           );

                if (result == DialogResult.Yes)
                {

                    //RegistryHelper.SaveString((RegistryHelper.storeLocs.Credentials,"AESPLXU", "");
                    //RegistryHelper.SaveString((RegistryHelper.storeLocs.Credentials,"AESPLXP", "");

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

    }
}
