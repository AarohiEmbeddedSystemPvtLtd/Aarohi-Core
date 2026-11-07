
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    /// <summary>
    /// Zoho OAuth configuration.
    /// Sensitive credentials are loaded from IMTS Routine .env.
    /// All existing non-sensitive configuration remains unchanged.
    /// </summary>
    public static class ZohoApiConfiguration
    {
        public const string SenderEmail =
            "panel@aarohies.in";

        // India data center
        public const string OAuthTokenUrl =
            "https://accounts.zoho.in/oauth/v2/token";

        public const string MailApiBaseUrl =
            "https://mail.zoho.in/api";


        public static string ClientId =>
            GetEnvValue("ZOHO_CLIENT_ID");

        public static string ClientSecret =>
            GetEnvValue("ZOHO_CLIENT_SECRET");

        public static string RefreshToken =>
            GetEnvValue("ZOHO_REFRESH_TOKEN");

        // ============================================================
        // EXISTING CONFIGURATION - UNCHANGED
        // ============================================================

        public const bool ExpungePermanently =
            false;

        public const int CleanupMaxAttempts =
            6;

        public const int CleanupRetryDelayMilliseconds =
            2000;

     
        public const int SentMessagesToInspect =
            25;

     

        private static string GetEnvValue(string key)
        {
            string envPath = Path.Combine(
                AppContext.BaseDirectory,
                ".env");

            if (!File.Exists(envPath))
            {
                throw new FileNotFoundException(
                    "Zoho configuration file (.env) not found.",
                    envPath);
            }

            foreach (string line in File.ReadLines(envPath))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("#"))
                {
                    continue;
                }

                int separatorIndex = trimmed.IndexOf('=');

                if (separatorIndex <= 0)
                    continue;

                string envKey = trimmed
                    .Substring(0, separatorIndex)
                    .Trim();

                if (!envKey.Equals(
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = trimmed
                    .Substring(separatorIndex + 1)
                    .Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"{key} is empty in .env.");
                }

                return value;
            }

            throw new InvalidOperationException(
                $"{key} not found in .env.");
        }
    }
}
