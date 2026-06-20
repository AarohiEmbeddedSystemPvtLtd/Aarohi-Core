using System;
using System.IO;
using System.Windows.Forms;

namespace Aarohi.DbPackager
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var result = MessageBox.Show(
                "Would you like to generate and package the database template (.bak) for this build?\n\n(Clicking Yes will clone the developer database, clear transactions, and place it in the output DbSchema folder.)",
                "Aarohi Database Packager",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                string targetDir = AppDomain.CurrentDomain.BaseDirectory;
                string appName = "IPTS";

                if (args.Length > 0)
                {
                    // Detect if trailing backslash in $(TargetDir) escaped the quote and merged arguments
                    if (args[0].Contains("\""))
                    {
                        var parts = args[0].Split('\"');
                        if (parts.Length > 0) targetDir = parts[0].Trim();
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) appName = parts[1].Trim();
                    }
                    else
                    {
                        targetDir = args[0];
                        if (args.Length > 1) appName = args[1];
                    }
                }

                targetDir = targetDir.Trim('\"', ' ');
                try
                {
                    targetDir = Path.GetFullPath(targetDir);
                }
                catch { }
                
                // Open the cloner form using the dynamic app name
                using (var cloner = new Aarohi.SQL.FormDatabaseCloner(appName))
                {
                    cloner.SetOutputDirectory(targetDir);
                    cloner.ShowDialog();
                }
            }
        }
    }
}
