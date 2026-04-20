using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Globals
{
    public static class AGLobals
    {
        public static class Names
        {
            public const string IPTS = "IPTS";
            public const string IMTS = "IMTS";
            public const string Aarohi_Reports = "Aarohi-Reports";
            public const string Device_Manager = "Device Manager";
            public const string IMTS_Routine = "IMTS Routine";
        }

        public static class PipeNames
        {
            public const string CommunictionPipe = "AarohiCommunicationServicePipe";
        }
        
        public static class ServicesNames
        {
            public const string CommunicationService = "CommunicationService";
        }

        public static class Utils
        {
            public static string DevName = "Dev@Aarohi";
            private const string InstalledSqlPackage =
       @"C:\Program Files (x86)\Aarohi Embedded Systems Pvt. Ltd\sqlpackage\sqlpackage.exe";

            public static string SqlPackagePath => ResolveSqlPackagePath();

            private static string ResolveSqlPackagePath()
            {
                // 1) Installed path (from installer)
                if (File.Exists(InstalledSqlPackage))
                    return InstalledSqlPackage;

                // 2) If your installer installs per-machine but could be Program Files (not x86)
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                string alt1 = Path.Combine(pf86, "Aarohi Embedded Systems Pvt. Ltd", "sqlpackage", "sqlpackage.exe");
                if (File.Exists(alt1)) return alt1;

                string alt2 = Path.Combine(pf, "Aarohi Embedded Systems Pvt. Ltd", "sqlpackage", "sqlpackage.exe");
                if (File.Exists(alt2)) return alt2;

                // 3) Bundled next to EXE (optional fallback)
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string bundled = Path.Combine(baseDir, "sqlpackage", "sqlpackage.exe");
                if (File.Exists(bundled)) return bundled;

                // 4) Optional override for special machines
                string env = Environment.GetEnvironmentVariable("SQLPACKAGE_PATH");
                if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

                // Not found
                throw new FileNotFoundException(
                    "SqlPackage.exe was not found. Please re-install the application or set environment variable SQLPACKAGE_PATH.",
                    alt1
                );
            }
            }
    }
}
