using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    public sealed class ZohoSentCleanupResult
    {
        public bool Success { get; set; }

        public bool Found { get; set; }

        public bool Deleted { get; set; }

        public string MessageId { get; set; } =
            string.Empty;

        public string ErrorMessage { get; set; } =
            string.Empty;

        public static ZohoSentCleanupResult DeletedSuccessfully(
            string messageId)
        {
            return new ZohoSentCleanupResult
            {
                Success = true,
                Found = true,
                Deleted = true,
                MessageId =
                    messageId ?? string.Empty
            };
        }

        public static ZohoSentCleanupResult NotFound()
        {
            return new ZohoSentCleanupResult
            {
                Success = false,
                Found = false,
                Deleted = false,
                ErrorMessage =
                    "The matching IMTS report was not found in Zoho Sent."
            };
        }

        public static ZohoSentCleanupResult Failed(
            string? errorMessage)
        {
            return new ZohoSentCleanupResult
            {
                Success = false,
                Found = false,
                Deleted = false,
                ErrorMessage =
                    errorMessage ?? string.Empty
            };
        }
    }
}

