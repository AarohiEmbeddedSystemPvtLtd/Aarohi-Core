using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aarohi.Globals;

namespace Aarohi.Configuration;

public static class MachineDbConfiguration
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Aarohi.MachineDbConfiguration.Entropy.V1");

    private static string FolderPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "Aarohi Embedded Systems Pvt Ltd",
            AGLobals.ValueHolders.currentApp);

    private static string FilePath =>
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
                Entropy,
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
            if (!File.Exists(FilePath))
            {
                return false;
            }

            byte[] encrypted =
                File.ReadAllBytes(
                    FilePath);

            byte[] raw;

            try
            {
                // 1. Try with new V1 Entropy
                raw = ProtectedData.Unprotect(
                    encrypted,
                    Entropy,
                    DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException)
            {
                // 2. Fallback to legacy unprotect without entropy for existing deployed files
                raw = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.LocalMachine);

                // Transparently migrate file to use new Entropy
                try
                {
                    Directory.CreateDirectory(FolderPath);
                    byte[] reEncrypted = ProtectedData.Protect(
                        raw,
                        Entropy,
                        DataProtectionScope.LocalMachine);

                    File.WriteAllBytes(FilePath, reEncrypted);
                }
                catch
                {
                    // Ignore write failures during read migration
                }
            }

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
