using Aarohi.Classes.Healper;
using System;

namespace Aarohi.Networking
{
    /// <summary>
    /// Configuration provider for Zoho OAuth and Mail API.
    /// Loads client credentials dynamically at runtime from Windows DPAPI storage.
    /// Credentials are never stored as plaintext in source code or compiled binaries.
    /// </summary>
    public static class ZohoApiConfiguration
    {
        public static string SenderEmail =>
            RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "ZohoSenderEmail", "panel@aarohies.in");

        // India data center
        public const string OAuthTokenUrl =
            "https://accounts.zoho.in/oauth/v2/token";

        public const string MailApiBaseUrl =
            "https://mail.zoho.in/api";

        // ------------------------------------------------------------
        // Dynamic DPAPI-backed OAuth Credentials (SEC-C-002)
        // ------------------------------------------------------------

        public static string ClientId =>
            RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "ZohoClientId", string.Empty);

        public static string ClientSecret =>
            RegistryHelper.LoadEncrypted(RegistryHelper.storeLocs.Credentials, "ZohoClientSecret", string.Empty);

        public static string RefreshToken =>
            RegistryHelper.LoadEncrypted(RegistryHelper.storeLocs.Credentials, "ZohoRefreshToken", string.Empty);

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(RefreshToken);

        /// <summary>
        /// Saves Zoho OAuth API credentials encrypted using Windows DPAPI.
        /// </summary>
        public static void SaveCredentials(string clientId, string clientSecret, string refreshToken, string senderEmail = "panel@aarohies.in")
        {
            RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "ZohoSenderEmail", senderEmail);
            RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "ZohoClientId", clientId);
            RegistryHelper.SaveEncrypted(RegistryHelper.storeLocs.Credentials, "ZohoClientSecret", clientSecret);
            RegistryHelper.SaveEncrypted(RegistryHelper.storeLocs.Credentials, "ZohoRefreshToken", refreshToken);
        }

        // ------------------------------------------------------------
        // Cleanup behavior (SEC-L-002: Runtime Configurable)
        // ------------------------------------------------------------

        // false = move matching IMTS sent copy to Trash.
        // true  = permanently delete matching IMTS sent copy.
        // Default is conservative: false (move to Trash)
        public static bool ExpungePermanently
        {
            get => RegistryHelper.LoadBool(RegistryHelper.storeLocs.Settings, "ZohoExpungePermanently", false);
            set
            {
                bool current = ExpungePermanently;
                if (current != value)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"[SECURITY AUDIT] ZohoExpungePermanently changed from {current} to {value} by user '{Environment.UserName}' at {DateTime.UtcNow:O}");
                    RegistryHelper.SaveBool(RegistryHelper.storeLocs.Settings, "ZohoExpungePermanently", value);
                }
            }
        }

        // How many times to wait for Zoho to create the Sent copy.
        public const int CleanupMaxAttempts = 6;

        public const int CleanupRetryDelayMilliseconds = 2000;

        // Number of newest Sent messages inspected on each attempt.
        public const int SentMessagesToInspect = 25;
    }
}
