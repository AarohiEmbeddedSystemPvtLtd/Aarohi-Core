using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    /// <summary>
    /// One-time developer configuration for Zoho OAuth.
    ///
    /// IMPORTANT:
    /// 1. Generate NEW credentials before production because the old
    ///    credentials were exposed during testing.
    /// 2. The USER does not need to do anything at runtime.
    /// 3. Access tokens are generated automatically from RefreshToken.
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

        // ------------------------------------------------------------
        // REPLACE THESE 3 VALUES ONCE WITH YOUR NEW PRODUCTION VALUES
        // ------------------------------------------------------------

        public const string ClientId =
            "1000.NZHQBZCKG6KZPD2Q7CZ0JP26GL5ZRN";

        public const string ClientSecret =
            "13163d55672c09679342f3a4a92a15dc82ee8278ee";

        public const string RefreshToken =
            "1000.e44020d9f9f8676f1604afa3472d4ae7.29dbbb93a93426f43bc77fe4c4def05c";

        // ------------------------------------------------------------
        // Cleanup behavior
        // ------------------------------------------------------------

        // false = move matching IMTS sent copy to Trash.
        // true  = permanently delete matching IMTS sent copy.
        //
        // Keep false while testing.
        public const bool ExpungePermanently =
            false;

        // How many times to wait for Zoho to create the Sent copy.
        public const int CleanupMaxAttempts =
            6;

        public const int CleanupRetryDelayMilliseconds =
            2000;

        // Number of newest Sent messages inspected on each attempt.
        public const int SentMessagesToInspect =
            25;
    }
}
