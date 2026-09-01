using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aarohi.Configuration;

public static class MachineDbConfiguration
{
    private static readonly string FolderPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "Aarohi Embedded Systems Pvt Ltd",
            "IMTS_Routine");

    private static readonly string FilePath =
        Path.Combine(
            FolderPath,
            "dbsettings.dat");

    public static void Save(
        string centralConnectionString,
        string panelConnectionString)
    {
        if (string.IsNullOrWhiteSpace(
                centralConnectionString))
        {
            throw new ArgumentException(
                "Central connection string is required.");
        }

        if (string.IsNullOrWhiteSpace(
                panelConnectionString))
        {
            throw new ArgumentException(
                "Panel connection string is required.");
        }

        Directory.CreateDirectory(
            FolderPath);

        MachineDbSettings settings =
            new MachineDbSettings
            {
                CentralConnectionString =
                    centralConnectionString.Trim(),

                PanelConnectionString =
                    panelConnectionString.Trim()
            };

        string json =
            JsonSerializer.Serialize(
                settings);

        byte[] raw =
            Encoding.UTF8.GetBytes(
                json);

        byte[] encrypted =
            ProtectedData.Protect(
                raw,
                null,
                DataProtectionScope.LocalMachine);

        File.WriteAllBytes(
            FilePath,
            encrypted);
    }

    public static bool TryLoad(
        out string centralConnectionString,
        out string panelConnectionString)
    {
        centralConnectionString =
            string.Empty;

        panelConnectionString =
            string.Empty;

        try
        {
            if (!File.Exists(
                    FilePath))
            {
                return false;
            }

            byte[] encrypted =
                File.ReadAllBytes(
                    FilePath);

            byte[] raw =
                ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.LocalMachine);

            string json =
                Encoding.UTF8.GetString(
                    raw);

            MachineDbSettings? settings =
                JsonSerializer.Deserialize<MachineDbSettings>(
                    json);

            if (settings == null)
                return false;

            centralConnectionString =
                settings.CentralConnectionString
                ?.Trim()
                ?? string.Empty;

            panelConnectionString =
                settings.PanelConnectionString
                ?.Trim()
                ?? string.Empty;

            return
                !string.IsNullOrWhiteSpace(
                    centralConnectionString) &&
                !string.IsNullOrWhiteSpace(
                    panelConnectionString);
        }
        catch
        {
            centralConnectionString =
                string.Empty;

            panelConnectionString =
                string.Empty;

            return false;
        }
    }

    private sealed class MachineDbSettings
    {
        public string CentralConnectionString
        {
            get;
            set;
        } = string.Empty;

        public string PanelConnectionString
        {
            get;
            set;
        } = string.Empty;
    }
}