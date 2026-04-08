using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Aarohi.Classes.Healper
{
    public static class RegistryHelper
    {
        private const string RootRegistryPath = @"Software\Aarohi Embedded Systems Pvt Ltd\IPTS";

        public enum storeLocs
        {
            Database,
            Settings,
            Credentials,
            Miscellaneous,
            root
        }

        private static string GetRegistryPath(storeLocs location)
        {
            switch (location)
            {
                case storeLocs.Database:
                    return $@"{RootRegistryPath}\Database";

                case storeLocs.Settings:
                    return $@"{RootRegistryPath}\Settings";

                case storeLocs.Credentials:
                    return $@"{RootRegistryPath}\Credentials";

                case storeLocs.Miscellaneous:
                    return $@"{RootRegistryPath}\Miscellaneous";

                case storeLocs.root:
                default:
                    return RootRegistryPath;
            }
        }

        public static void Save(
            storeLocs location,
            string server,
            string database,
            bool useWindowsAuth,
            string userName,
            string password)
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (key == null)
                    throw new Exception("Unable to create/read registry key: " + registryPath);

                key.SetValue("Server", server ?? string.Empty, RegistryValueKind.String);
                key.SetValue("Database", database ?? string.Empty, RegistryValueKind.String);
                key.SetValue("AuthMode", useWindowsAuth ? "Windows" : "Sql", RegistryValueKind.String);
                key.SetValue("UserName", userName ?? string.Empty, RegistryValueKind.String);

                string encryptedPassword = Encrypt(password);
                key.SetValue("Password", encryptedPassword ?? string.Empty, RegistryValueKind.String);
            }
        }

        public static bool Load(
            storeLocs location,
            out string server,
            out string database,
            out bool useWindowsAuth,
            out string userName,
            out string password)
        {
            string registryPath = GetRegistryPath(location);

            server = database = userName = password = string.Empty;
            useWindowsAuth = true;

            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key == null)
                    return false;

                server = key.GetValue("Server", string.Empty) as string ?? string.Empty;
                database = key.GetValue("Database", string.Empty) as string ?? string.Empty;

                string authMode = key.GetValue("AuthMode", "Windows") as string ?? "Windows";
                useWindowsAuth = authMode.Equals("Windows", StringComparison.OrdinalIgnoreCase);

                userName = key.GetValue("UserName", string.Empty) as string ?? string.Empty;

                string encryptedPassword = key.GetValue("Password", string.Empty) as string ?? string.Empty;
                password = Decrypt(encryptedPassword);

                return true;
            }
        }

        public static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return string.Empty;

            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] protectedData = ProtectedData.Protect(
                data,
                null,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(protectedData);
        }

        public static string Decrypt(string cipher)
        {
            if (string.IsNullOrEmpty(cipher))
                return string.Empty;

            try
            {
                byte[] protectedData = Convert.FromBase64String(cipher);
                byte[] data = ProtectedData.Unprotect(
                    protectedData,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveString(storeLocs location, string name, string value)
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (key == null)
                    throw new Exception("Unable to create/read registry key: " + registryPath);

                key.SetValue(name, value ?? string.Empty, RegistryValueKind.String);
            }
        }

        public static string LoadString(storeLocs location, string name, string defaultValue = "")
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key == null)
                    return defaultValue;

                return key.GetValue(name, defaultValue) as string ?? defaultValue;
            }
        }

        public static void SaveBool(storeLocs location, string name, bool value)
        {
            SaveString(location, name, value ? "1" : "0");
        }

        public static bool LoadBool(storeLocs location, string name, bool defaultValue = false)
        {
            string def = defaultValue ? "1" : "0";
            string val = LoadString(location, name, def);
            return val == "1";
        }

        public static void SaveInt(storeLocs location, string name, int value)
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (key == null)
                    throw new Exception("Unable to create/read registry key: " + registryPath);

                key.SetValue(name, value, RegistryValueKind.DWord);
            }
        }

        public static int LoadInt(storeLocs location, string name, int defaultValue = 0)
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key == null)
                    return defaultValue;

                object raw = key.GetValue(name, defaultValue);
                if (raw is int i)
                    return i;

                if (int.TryParse(raw?.ToString(), out int parsed))
                    return parsed;

                return defaultValue;
            }
        }

        public static void SaveEncrypted(storeLocs location, string name, string plainValue)
        {
            string encrypted = Encrypt(plainValue);
            SaveString(location, name, encrypted);
        }

        public static string LoadEncrypted(storeLocs location, string name, string defaultValue = "")
        {
            string cipher = LoadString(location, name, string.Empty);
            if (string.IsNullOrEmpty(cipher))
                return defaultValue;

            string plain = Decrypt(cipher);
            return string.IsNullOrEmpty(plain) ? defaultValue : plain;
        }

        public static void DeleteValue(storeLocs location, string name)
        {
            string registryPath = GetRegistryPath(location);

            using (var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true))
            {
                if (key == null)
                    return;

                key.DeleteValue(name, false);
            }
        }
    }
}


























//using System;
//using System.Security.Cryptography;
//using System.Text;
//using Microsoft.Win32;

//namespace Aarohi.Classes.Healper
//{
//    public static class RegistryHelper
//    {
//        private const string RegistryPath = @"Software\Aarohi Embedded Systems Pvt Ltd\IPTS";


//        public enum storeLocs
//        {
//            Database,
//            Settings,
//            Credentials,
//            Miscellaneous,
//            root
//        }

//        // Based on The Locations
//        //private static string GetRegistryPath(storeLocs location)
//        //{
//        //    switch (location)
//        //    {
//        //        case storeLocs.Database:
//        //            return $@"{RegistryPath}\Database";

//        //        case storeLocs.Settings:
//        //            return $@"{RegistryPath}\Settings";

//        //        case storeLocs.Credentials:
//        //            return $@"{RegistryPath}\Credentials";

//        //        case storeLocs.Miscellaneous:
//        //            return $@"{RegistryPath}\Miscellaneous";

//        //        case storeLocs.root:
//        //        default:
//        //            return RegistryPath;
//        //    }
//        //}


//        public static void Save(
//            string server,
//            string database,
//            bool useWindowsAuth,
//            string userName,
//            string password)
//        {
//            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
//            {
//                if (key == null)
//                    throw new Exception("Unable to create/read registry key: " + RegistryPath);

//                key.SetValue("Server", server ?? string.Empty, RegistryValueKind.String);
//                key.SetValue("Database", database ?? string.Empty, RegistryValueKind.String);
//                key.SetValue("AuthMode", useWindowsAuth ? "Windows" : "Sql", RegistryValueKind.String);
//                key.SetValue("UserName", userName ?? string.Empty, RegistryValueKind.String);

//                string encryptedPassword = Encrypt(password);
//                key.SetValue("Password", encryptedPassword ?? string.Empty, RegistryValueKind.String);
//            }
//        }

//        public static bool Load(
//            out string server,
//            out string database,
//            out bool useWindowsAuth,
//            out string userName,
//            out string password)
//        {
//            server = database = userName = password = string.Empty;
//            useWindowsAuth = true;

//            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
//            {
//                if (key == null)
//                    return false;

//                server = key.GetValue("Server", string.Empty) as string ?? string.Empty;
//                database = key.GetValue("Database", string.Empty) as string ?? string.Empty;

//                string authMode = key.GetValue("AuthMode", "Windows") as string ?? "Windows";
//                useWindowsAuth = authMode.Equals("Windows", StringComparison.OrdinalIgnoreCase);

//                userName = key.GetValue("UserName", string.Empty) as string ?? string.Empty;

//                string encryptedPassword = key.GetValue("Password", string.Empty) as string ?? string.Empty;
//                password = Decrypt(encryptedPassword);

//                return true;
//            }
//        }

//        public static string Encrypt(string plain)
//        {
//            if (string.IsNullOrEmpty(plain))
//                return string.Empty;

//            byte[] data = Encoding.UTF8.GetBytes(plain);
//            byte[] protectedData = ProtectedData.Protect(
//                data,
//                null,
//                DataProtectionScope.CurrentUser);

//            return Convert.ToBase64String(protectedData);
//        }

//        public static string Decrypt(string cipher)
//        {
//            if (string.IsNullOrEmpty(cipher))
//                return string.Empty;

//            try
//            {
//                byte[] protectedData = Convert.FromBase64String(cipher);
//                byte[] data = ProtectedData.Unprotect(
//                    protectedData,
//                    null,
//                    DataProtectionScope.CurrentUser);

//                return Encoding.UTF8.GetString(data);
//            }
//            catch
//            {
//                return string.Empty;
//            }
//        }

//        public static void SaveString(string name, string value)
//        {
//            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
//            {
//                if (key == null)
//                    throw new Exception("Unable to create/read registry key: " + RegistryPath);

//                key.SetValue(name, value ?? string.Empty, RegistryValueKind.String);
//            }
//        }

//        public static string LoadString(string name, string defaultValue = "")
//        {
//            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
//            {
//                if (key == null)
//                    return defaultValue;

//                return key.GetValue(name, defaultValue) as string ?? defaultValue;
//            }
//        }

//        public static void SaveBool(string name, bool value)
//        {
//            SaveString(name, value ? "1" : "0");
//        }

//        public static bool LoadBool(string name, bool defaultValue = false)
//        {
//            string def = defaultValue ? "1" : "0";
//            string val = LoadString(name, def);
//            return val == "1";
//        }

//        public static void SaveInt(string name, int value)
//        {
//            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
//            {
//                if (key == null)
//                    throw new Exception("Unable to create/read registry key: " + RegistryPath);

//                key.SetValue(name, value, RegistryValueKind.DWord);
//            }
//        }

//        public static int LoadInt(string name, int defaultValue = 0)
//        {
//            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
//            {
//                if (key == null)
//                    return defaultValue;

//                object raw = key.GetValue(name, defaultValue);
//                if (raw is int i)
//                    return i;

//                if (int.TryParse(raw?.ToString(), out int parsed))
//                    return parsed;

//                return defaultValue;
//            }
//        }





//        public static void SaveEncrypted(string name, string plainValue)
//        {
//            string encrypted = Encrypt(plainValue);
//            SaveString(name, encrypted);
//        }

//        public static string LoadEncrypted(string name, string defaultValue = "")
//        {
//            string cipher = LoadString(name, string.Empty);
//            if (string.IsNullOrEmpty(cipher))
//                return defaultValue;

//            string plain = Decrypt(cipher);
//            return string.IsNullOrEmpty(plain) ? defaultValue : plain;
//        }

//        public static void DeleteValue(string name)
//        {
//            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true))
//            {
//                if (key == null)
//                    return;

//                key.DeleteValue(name, false);
//            }
//        }
//    }
//}
