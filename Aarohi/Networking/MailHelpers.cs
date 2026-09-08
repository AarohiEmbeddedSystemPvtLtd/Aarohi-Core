using DnsClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    public static class MailHelpers
    {
        //public async static Task<bool> HasValidEmailDomain(string email)
        //{
        //    try
        //    {
        //        var domain = email.Split('@')[1];

        //        var lookup = new LookupClient();
        //        var result = await lookup.QueryAsync(domain, QueryType.MX);

        //        var mxRecords = result.Answers.MxRecords();

        //        return mxRecords != null && mxRecords.Any();
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        //private static readonly LookupClient _lookup = new LookupClient(new LookupClientOptions
        //{
        //    Timeout = TimeSpan.FromSeconds(3),
        //    Retries = 1,
        //    UseCache = true
        //});

        //public static async Task<bool> HasValidEmailDomain(string email)
        //{
        //    string domain = email[(email.IndexOf('@') + 1)..];

        //    try
        //    {
        //        var result = await _lookup.QueryAsync(domain, QueryType.MX);

        //        return result.Answers.MxRecords().Any();
        //    }
        //    catch (DnsResponseException)
        //    {
        //        // Domain genuinely doesn't exist
        //        return false;
        //    }
        //}

        private static readonly LookupClient _lookup = new LookupClient(
     new LookupClientOptions(
         IPAddress.Parse("8.8.8.8"),
         IPAddress.Parse("1.1.1.1"))
     {
         Timeout = TimeSpan.FromSeconds(3),
         Retries = 1,
         UseCache = true
     });

        public static async Task<bool> HasValidEmailDomain(string email)
        {
            try
            {
                if (!MailAddress.TryCreate(email, out var mailAddress))
                    return false;

                string domain = mailAddress.Host;

                var result = await _lookup.QueryAsync(domain, QueryType.MX);

                return result.Answers.MxRecords().Any();
            }
            catch (DnsResponseException)
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



        //public static async Task SendOtpEmail(string email, string otp)
        //{
        //    var fromEmail = "panel@aarohies.in";
        //    var fromName = "Aarohi Support";

        //    var subject = "Email Verification code";

        //    var plainTextBody = $"Your OTP is {otp}. It is valid for 5 minutes. Do not share it with anyone.";

        //    var from = new MailAddress(fromEmail, fromName);
        //    var to = new MailAddress(email);

        //    using var smtp = new SmtpClient("smtppro.zoho.in", 587)
        //    {
        //        EnableSsl = true,
        //        Credentials = new NetworkCredential(fromEmail, GetAppPassword()) //  secure
        //    };

        //    using var message = new MailMessage(from, to)
        //    {
        //        Subject = subject,
        //        Body = MailTemplates.OtpEmailBody(otp),
        //        IsBodyHtml = true
        //    };

        //    //  Add plain-text version (VERY IMPORTANT)
        //    message.AlternateViews.Add(
        //        AlternateView.CreateAlternateViewFromString(
        //            plainTextBody, null, "text/plain"));

        //    // Add helpful headers (reduce spam chance)
        //    message.Headers.Add("X-Mailer", "AarohiApp");
        //    message.Headers.Add("X-Priority", "3");

        //    try
        //    {
        //        await smtp.SendMailAsync(message);
        //    }
        //    catch (Exception ex)
        //    {
        //        // log properly
        //        Console.WriteLine("Email failed: " + ex.Message);
        //    }
        //}
    }
}
