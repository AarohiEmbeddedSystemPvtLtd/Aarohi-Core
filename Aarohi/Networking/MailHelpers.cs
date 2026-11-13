using Aarohi.Classes.Healper;
using DnsClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    public static class MailHelpers
    {
        public static string SenderEmail =>
            RegistryHelper.LoadString(RegistryHelper.storeLocs.Credentials, "SmtpSenderEmail", "panel@aarohies.in");

        public static string SmtpHost =>
            RegistryHelper.LoadString(RegistryHelper.storeLocs.Settings, "SmtpHost", "smtp.zoho.in");

        public static int SmtpPort =>
            RegistryHelper.LoadInt(RegistryHelper.storeLocs.Settings, "SmtpPort", 587);

        public static string AppPassword =>
            RegistryHelper.LoadEncrypted(RegistryHelper.storeLocs.Credentials, "SmtpAppPassword", string.Empty);

        /// <summary>
        /// Saves Zoho SMTP credentials with the application password encrypted using Windows DPAPI.
        /// </summary>
        public static void SaveSmtpCredentials(string appPassword, string senderEmail = "panel@aarohies.in", string host = "smtp.zoho.in", int port = 587)
        {
            RegistryHelper.SaveString(RegistryHelper.storeLocs.Credentials, "SmtpSenderEmail", senderEmail);
            RegistryHelper.SaveString(RegistryHelper.storeLocs.Settings, "SmtpHost", host);
            RegistryHelper.SaveInt(RegistryHelper.storeLocs.Settings, "SmtpPort", port);
            RegistryHelper.SaveEncrypted(RegistryHelper.storeLocs.Credentials, "SmtpAppPassword", appPassword);
        }

        private static readonly LookupClient _lookup = new LookupClient(
            new LookupClientOptions
            {
                Timeout = TimeSpan.FromSeconds(3),
                Retries = 1,
                UseCache = true
            });

        public enum EmailValidationResult
        {
            Valid,
            InvalidSyntax,
            DomainNotFound,
            LookupUnavailable
        }

        public static async Task<EmailValidationResult> ValidateEmailDomainAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return EmailValidationResult.InvalidSyntax;

            string trimmed = email.Trim();
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            if (!regex.IsMatch(trimmed))
                return EmailValidationResult.InvalidSyntax;

            string[] parts = trimmed.Split('@');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                return EmailValidationResult.InvalidSyntax;

            string domain = parts[1];
            try
            {
                var result = await _lookup.QueryAsync(domain, QueryType.MX);
                if (result != null && result.Answers.MxRecords().Any())
                    return EmailValidationResult.Valid;

                var aResult = await _lookup.QueryAsync(domain, QueryType.A);
                if (aResult != null && aResult.Answers.ARecords().Any())
                    return EmailValidationResult.Valid;

                return EmailValidationResult.DomainNotFound;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MailHelpers] DNS verification failed: {ex.Message}");
                return EmailValidationResult.LookupUnavailable;
            }
        }

        public static async Task<bool> HasValidEmailDomain(string email)
        {
            var status = await ValidateEmailDomainAsync(email);
            return status == EmailValidationResult.Valid;
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
        // EXISTING SIGNATURE (single attachment)
        // Routine app na junu calls break na thay etle rakhyu che.
        // Have aa fakt navi list valu method ne call kare che.
        // ============================================================
        public static Task SendReportEmail(
            string toEmails,
            string? ccEmails,
            string? bccEmails,
            string subject,
            string? body,
            string? attachmentFilePath)
        {
            List<string> attachments = new();

            if (!string.IsNullOrWhiteSpace(attachmentFilePath))
                attachments.Add(attachmentFilePath);

            return SendReportEmail(
                toEmails,
                ccEmails,
                bccEmails,
                subject,
                body,
                attachments);
        }

        // ============================================================
        // NEW: multiple attachments
        // ============================================================
        public static async Task SendReportEmail(
            string toEmails,
            string? ccEmails,
            string? bccEmails,
            string subject,
            string? body,
            IEnumerable<string>? attachmentFilePaths)
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

            List<string> attachmentPaths =
                (attachmentFilePaths ?? Enumerable.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

            foreach (string path in attachmentPaths)
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "Report attachment was not found.",
                        path);
                }
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

            AddValidAddresses(message.To, toEmails);
            AddValidAddresses(message.CC, ccEmails);
            AddValidAddresses(message.Bcc, bccEmails);

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

            foreach (string path in attachmentPaths)
            {
                message.Attachments.Add(
                    new Attachment(path));
            }

            using var smtp =
                CreateSmtpClient();

            Debug.WriteLine(
                $"IMTS MAIL: Sending. CleanupMarker={cleanupMarker}, Attachments={attachmentPaths.Count}");

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


        private static SmtpClient CreateSmtpClient()
        {
            string password = AppPassword;
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "SMTP application password is not configured or cannot be decrypted. " +
                    "Please configure SMTP credentials in Secure Settings.");
            }

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
                        password),

                Timeout =
                    20000
            };
        }

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