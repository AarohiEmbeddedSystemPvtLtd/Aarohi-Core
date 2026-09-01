using DnsClient;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    public static class MailHelpers
    {
        private const string SenderEmail =
            "panel@aarohies.in";

        private const string AppPassword =
            "mDfrnvMPXpaM";

        private const string SmtpHost =
            "smtp.zoho.in";

        private const int SmtpPort =
            587;


        private static readonly LookupClient _lookup = new LookupClient(
     new LookupClientOptions(
         IPAddress.Parse("8.8.8.8"),
         IPAddress.Parse("1.1.1.1"))
     {
         Timeout = TimeSpan.FromSeconds(3),
         Retries = 1,
         UseCache = true
     });

                    UseCache =
                        true
                });

        // ============================================================
        // DOMAIN VALIDATION
        // ============================================================

        public static async Task<bool> HasValidEmailDomain(
            string email)
        {
            if (string.IsNullOrWhiteSpace(
                    email))
            {
                return false;
            }

            int atIndex =
                email.LastIndexOf('@');

            if (atIndex <= 0 ||
                atIndex >= email.Length - 1)
            {
                return false;
            }

            string domain =
                email[(atIndex + 1)..]
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    domain))
            {
                return false;
            }

            try
            {
                var result =
                    await _lookup.QueryAsync(
                        domain,
                        QueryType.MX);

                return result
                    .Answers
                    .MxRecords()
                    .Any();
            }
            catch
            {
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Unable to verify the email domain due to a DNS/network problem.\n" +
                    "Please check the internet and DNS connection.",
                    ex);
            }
        }

        public static async Task SendOtpEmail(
            string email,
            string fromName,
            string subject,
            string body)
        {
            var from =
                new MailAddress(
                    SenderEmail,
                    fromName);

            var to =
                new MailAddress(
                    email);

            using var smtp =
                CreateSmtpClient();

            using var message =
                new MailMessage(
                    from,
                    to)
                {
                    Subject =
                        subject,

                    IsBodyHtml =
                        true,

                    Body =
                        body
                };

            await smtp.SendMailAsync(
                message);
        }

        // ============================================================
        // REPORT EMAIL
        //
        // Final automatic flow:
        //
        // 1. Create unique hidden cleanup marker.
        // 2. Send report normally through existing SMTP.
        // 3. SMTP success = MAIL SUCCESS.
        // 4. Zoho OAuth automatically creates access token.
        // 5. Zoho API automatically discovers Account ID.
        // 6. Zoho API automatically discovers Sent Folder ID.
        // 7. Read newest Sent messages.
        // 8. Find exact message by subject + sender + hidden marker.
        // 9. Move ONLY that exact message to Trash.
        //
        // Manual IMTS Send Mail and Auto Mail both get this behavior
        // because both use this same method.
        // ============================================================

        public static async Task SendReportEmail(
      string toEmails,
      string? ccEmails,
      string? bccEmails,
      string subject,
      string? body,
      string? attachmentFilePath)
        {
            if (string.IsNullOrWhiteSpace(toEmails))
            {
                throw new ArgumentException(
                    "At least one To email is required.",
                    nameof(toEmails));
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException(
                    "Email subject is required.",
                    nameof(subject));
            }

            if (!string.IsNullOrWhiteSpace(attachmentFilePath) &&
                !File.Exists(attachmentFilePath))
            {
                throw new FileNotFoundException(
                    "Report attachment was not found.",
                    attachmentFilePath);
            }

            string cleanupMarker =
                "AES-IMTS-CLEANUP-" +
                Guid.NewGuid().ToString("N");

            string htmlBody =
                body ?? string.Empty;

            htmlBody +=
                "<div style=\"display:none !important;" +
                "max-height:0;overflow:hidden;opacity:0;" +
                "font-size:1px;line-height:1px;\">" +
                WebUtility.HtmlEncode(cleanupMarker) +
                "</div>";

            using var message =
                new MailMessage();

            message.From =
                new MailAddress(
                    SenderEmail,
                    "Aarohi");

            AddValidAddresses(
                message.To,
                toEmails);

            AddValidAddresses(
                message.CC,
                ccEmails);

            AddValidAddresses(
                message.Bcc,
                bccEmails);

            if (message.To.Count == 0)
            {
                throw new InvalidOperationException(
                    "No valid To email address is available.");
            }

            message.Subject =
                subject.Trim();

            message.Body =
                htmlBody;

            message.IsBodyHtml =
                true;

            if (!string.IsNullOrWhiteSpace(attachmentFilePath))
            {
                message.Attachments.Add(
                    new Attachment(
                        attachmentFilePath));
            }

            using var smtp =
                CreateSmtpClient();

            Debug.WriteLine(
                $"IMTS MAIL: Sending. CleanupMarker={cleanupMarker}");

            await smtp.SendMailAsync(
                message);

            Debug.WriteLine(
                "IMTS MAIL: SMTP send successful.");

            try
            {
                ZohoSentCleanupResult cleanupResult =
                    await ZohoMailCleanupService
                        .DeleteExactSentReportAsync(
                            cleanupMarker,
                            message.Subject);

                if (cleanupResult.Deleted)
                {
                    Debug.WriteLine(
                        $"IMTS MAIL: Sent copy cleaned successfully. MessageId={cleanupResult.MessageId}");
                }
                else
                {
                    Debug.WriteLine(
                        "IMTS MAIL: Report sent successfully, but Sent cleanup did not complete. " +
                        cleanupResult.ErrorMessage);
                }
            }
            catch (Exception cleanupException)
            {
                Debug.WriteLine(
                    "IMTS MAIL: SMTP was successful, but Zoho Sent cleanup failed: " +
                    cleanupException);
            }
        }

        // ============================================================
        // SMTP CLIENT
        // ============================================================

        private static SmtpClient CreateSmtpClient()
        {
            return new SmtpClient
            {
                Host =
                    SmtpHost,

                Port =
                    SmtpPort,

                EnableSsl =
                    true,

                UseDefaultCredentials =
                    false,

                DeliveryMethod =
                    SmtpDeliveryMethod.Network,

                Credentials =
                    new NetworkCredential(
                        SenderEmail,
                        AppPassword),

                Timeout =
                    30000
            };
        }

        // ============================================================
        // RECIPIENTS
        //
        // An invalid recipient does not prevent all other valid
        // recipients from receiving the report.
        // ============================================================

        private static void AddValidAddresses(
            MailAddressCollection addressList,
            string? emails)
        {
            if (string.IsNullOrWhiteSpace(
                    emails))
            {
                return;
            }

            string[] addresses =
                emails.Split(
                    new[]
                    {
                        ',',
                        ';',
                        '\r',
                        '\n'
                    },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            foreach (string rawAddress in
                     addresses)
            {
                string address =
                    rawAddress.Trim();

                if (string.IsNullOrWhiteSpace(
                        address))
                {
                    continue;
                }

                try
                {
                    MailAddress parsed =
                        new MailAddress(
                            address);

                    bool exists =
                        addressList
                            .Cast<MailAddress>()
                            .Any(
                                item =>
                                    string.Equals(
                                        item.Address,
                                        parsed.Address,
                                        StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        addressList.Add(
                            parsed);
                    }
                }
                catch
                {
                    Debug.WriteLine(
                        $"IMTS MAIL: Invalid recipient skipped: {address}");
                }
            }
        }
    }
}












//using DnsClient;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Mail;
//using System.Threading.Tasks;

//namespace Aarohi.Networking
//{
//    public static class MailHelpers
//    {
//        private const string SenderEmail =
//            "panel@aarohies.in";

//        // IMPORTANT:
//        // Keep your existing Zoho application-specific password here.
//        // Do not replace it with your normal Zoho/Windows password.
//        private const string AppPassword =
//            "mDfrnvMPXpaM";

//        // Zoho India Data Center
//        private const string SmtpHost =
//            "smtp.zoho.in";

//        private const int SmtpPort =
//            587;

//        private static readonly LookupClient _lookup =
//            new LookupClient(
//                new LookupClientOptions
//                {
//                    Timeout = TimeSpan.FromSeconds(3),
//                    Retries = 1,
//                    UseCache = true
//                });

//        // ============================================================
//        // EMAIL DOMAIN CHECK
//        // ============================================================

//        public static async Task<bool> HasValidEmailDomain(
//            string email)
//        {
//            if (string.IsNullOrWhiteSpace(email))
//            {
//                return false;
//            }

//            int atIndex =
//                email.LastIndexOf('@');

//            if (atIndex <= 0 ||
//                atIndex >= email.Length - 1)
//            {
//                return false;
//            }

//            string domain =
//                email[(atIndex + 1)..]
//                    .Trim();

//            if (string.IsNullOrWhiteSpace(domain))
//            {
//                return false;
//            }

//            try
//            {
//                var result =
//                    await _lookup.QueryAsync(
//                        domain,
//                        QueryType.MX);

//                return result
//                    .Answers
//                    .MxRecords()
//                    .Any();
//            }
//            catch (DnsResponseException)
//            {
//                return false;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        // ============================================================
//        // OTP EMAIL
//        // Existing OTP behavior remains HTML.
//        // ============================================================

//        public static async Task SendOtpEmail(
//            string email,
//            string fromName,
//            string subject,
//            string body)
//        {
//            var from =
//                new MailAddress(
//                    SenderEmail,
//                    fromName);

//            var to =
//                new MailAddress(
//                    email);

//            using var smtp =
//                CreateSmtpClient();

//            using var message =
//                new MailMessage(
//                    from,
//                    to)
//                {
//                    Subject = subject,
//                    IsBodyHtml = true,
//                    Body = body
//                };

//            await smtp.SendMailAsync(
//                message);
//        }

//        // ============================================================
//        // REPORT EMAIL
//        //
//        // Changes:
//        // 1. HTML body is rendered properly.
//        // 2. Invalid-format To/CC/BCC addresses are skipped.
//        // 3. Remaining valid recipients still receive the report.
//        // 4. At least one valid To recipient is mandatory.
//        // ============================================================

//        public static async Task SendReportEmail(
//            string toEmails,
//            string? ccEmails,
//            string? bccEmails,
//            string subject,
//            string? body,
//            string attachmentFilePath)
//        {
//            if (string.IsNullOrWhiteSpace(
//                    toEmails))
//            {
//                throw new ArgumentException(
//                    "At least one To email is required.",
//                    nameof(toEmails));
//            }

//            if (string.IsNullOrWhiteSpace(
//                    subject))
//            {
//                throw new ArgumentException(
//                    "Email subject is required.",
//                    nameof(subject));
//            }

//            if (string.IsNullOrWhiteSpace(
//                    attachmentFilePath))
//            {
//                throw new ArgumentException(
//                    "Report attachment path is required.",
//                    nameof(attachmentFilePath));
//            }

//            if (!File.Exists(
//                    attachmentFilePath))
//            {
//                throw new FileNotFoundException(
//                    "Report attachment was not found.",
//                    attachmentFilePath);
//            }

//            using var message =
//                new MailMessage();

//            message.From =
//                new MailAddress(
//                    SenderEmail,
//                    "AES Auto Mail");

//            // --------------------------------------------------------
//            // Add recipients independently.
//            // Invalid-format addresses are skipped.
//            // --------------------------------------------------------

//            List<string> skippedAddresses =
//                new List<string>();

//            AddValidAddresses(
//                message.To,
//                toEmails,
//                "To",
//                skippedAddresses);

//            AddValidAddresses(
//                message.CC,
//                ccEmails,
//                "CC",
//                skippedAddresses);

//            AddValidAddresses(
//                message.Bcc,
//                bccEmails,
//                "BCC",
//                skippedAddresses);

//            // At least one valid primary recipient is required.
//            if (message.To.Count == 0)
//            {
//                string details =
//                    skippedAddresses.Count > 0
//                        ? "\nSkipped: " +
//                          string.Join(
//                              ", ",
//                              skippedAddresses)
//                        : string.Empty;

//                throw new InvalidOperationException(
//                    "No valid To email address is available." +
//                    details);
//            }

//            message.Subject =
//                subject.Trim();

//            message.Body =
//                body ?? string.Empty;

//            // IMPORTANT:
//            // This makes the professional HTML report template
//            // render correctly in Gmail/Outlook.
//            message.IsBodyHtml =
//                true;

//            message.Attachments.Add(
//                new Attachment(
//                    attachmentFilePath));

//            using var smtp =
//                CreateSmtpClient();

//            Console.WriteLine(
//                "==========================================");

//            Console.WriteLine(
//                "AUTO MAIL SMTP CONFIGURATION");

//            Console.WriteLine(
//                "==========================================");

//            Console.WriteLine(
//                $"Host            : {smtp.Host}");

//            Console.WriteLine(
//                $"Port            : {smtp.Port}");

//            Console.WriteLine(
//                $"SSL/TLS Enabled : {smtp.EnableSsl}");

//            Console.WriteLine(
//                $"Default Creds   : {smtp.UseDefaultCredentials}");

//            Console.WriteLine(
//                $"Sender/User     : {SenderEmail}");

//            Console.WriteLine(
//                $"To              : {string.Join(", ", message.To.Cast<MailAddress>().Select(x => x.Address))}");

//            Console.WriteLine(
//                $"CC              : {string.Join(", ", message.CC.Cast<MailAddress>().Select(x => x.Address))}");

//            Console.WriteLine(
//                $"BCC             : {string.Join(", ", message.Bcc.Cast<MailAddress>().Select(x => x.Address))}");

//            if (skippedAddresses.Count > 0)
//            {
//                Console.WriteLine(
//                    $"Skipped Invalid : {string.Join(", ", skippedAddresses)}");
//            }

//            Console.WriteLine(
//                $"Attachment      : {attachmentFilePath}");

//            Console.WriteLine(
//                "==========================================");

//            await smtp.SendMailAsync(
//                message);

//            Console.WriteLine(
//                "AUTO MAIL SENT SUCCESSFULLY.");
//        }

//        // ============================================================
//        // SMTP CLIENT
//        // ============================================================

//        private static SmtpClient CreateSmtpClient()
//        {
//            var smtp =
//                new SmtpClient
//                {
//                    Host =
//                        SmtpHost,

//                    Port =
//                        SmtpPort,

//                    EnableSsl =
//                        true,

//                    UseDefaultCredentials =
//                        false,

//                    DeliveryMethod =
//                        SmtpDeliveryMethod.Network,

//                    Credentials =
//                        new NetworkCredential(
//                            SenderEmail,
//                            AppPassword),

//                    Timeout =
//                        30000
//                };

//            return smtp;
//        }



//        private static void AddValidAddresses(
//            MailAddressCollection addressList,
//            string? emails,
//            string recipientType,
//            List<string> skippedAddresses)
//        {
//            if (string.IsNullOrWhiteSpace(
//                    emails))
//            {
//                return;
//            }

//            string[] addresses =
//                emails.Split(
//                    new[]
//                    {
//                        ',',
//                        ';',
//                        '\r',
//                        '\n'
//                    },
//                    StringSplitOptions.RemoveEmptyEntries);

//            HashSet<string> alreadyAdded =
//                new HashSet<string>(
//                    StringComparer.OrdinalIgnoreCase);

//            foreach (string value in addresses)
//            {
//                string address =
//                    value.Trim();

//                if (string.IsNullOrWhiteSpace(
//                        address))
//                {
//                    continue;
//                }

//                if (alreadyAdded.Contains(
//                        address))
//                {
//                    continue;
//                }

//                try
//                {
//                    MailAddress mailAddress =
//                        new MailAddress(
//                            address);

//                    string normalizedAddress =
//                        mailAddress.Address.Trim();

//                    // Additional simple sanity check.
//                    int atIndex =
//                        normalizedAddress.LastIndexOf('@');

//                    if (atIndex <= 0 ||
//                        atIndex >= normalizedAddress.Length - 1)
//                    {
//                        skippedAddresses.Add(
//                            $"{recipientType}: {address}");

//                        continue;
//                    }

//                    string domain =
//                        normalizedAddress[(atIndex + 1)..];

//                    if (string.IsNullOrWhiteSpace(
//                            domain) ||
//                        domain.StartsWith(
//                            ".",
//                            StringComparison.Ordinal) ||
//                        domain.EndsWith(
//                            ".",
//                            StringComparison.Ordinal) ||
//                        domain.Contains(
//                            "..",
//                            StringComparison.Ordinal))
//                    {
//                        skippedAddresses.Add(
//                            $"{recipientType}: {address}");

//                        continue;
//                    }

//                    addressList.Add(
//                        mailAddress);

//                    alreadyAdded.Add(
//                        normalizedAddress);
//                }
//                catch (FormatException)
//                {
//                    skippedAddresses.Add(
//                        $"{recipientType}: {address}");
//                }
//                catch
//                {
//                    skippedAddresses.Add(
//                        $"{recipientType}: {address}");
//                }
//            }
//        }
//    }
//}







