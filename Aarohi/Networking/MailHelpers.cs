using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using DnsClient;

namespace Aarohi.Networking
{
    public static class MailHelpers
    {
        public async static Task<bool> HasValidEmailDomain(string email)
        {
            try
            {
                var domain = email.Split('@')[1];

                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(domain, QueryType.MX);

                var mxRecords = result.Answers.MxRecords();

                return mxRecords != null && mxRecords.Any();
            }
            catch
            {
                return false;
            }
        }

        public static async Task SendOtpEmail(string email, string fromName, string subject, string body)
        {
            var from = new MailAddress("panel@aarohies.in", fromName);
            var to = new MailAddress(email);

            const string appPassword = "7CHit0C2vuCy";
            var smtp = new SmtpClient("smtppro.zoho.in", 587)
            {
                EnableSsl = true,
                Credentials = new System.Net.NetworkCredential(from.Address, appPassword)
            };
            var message = new MailMessage(from, to)
            {
                Subject = subject,
                IsBodyHtml = true,
                Body = body
            };
            await smtp.SendMailAsync(message);
        }
    }
}
