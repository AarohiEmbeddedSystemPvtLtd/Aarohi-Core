using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    public class MailTemplates
    {
        public static string OtpEmailBody(string otp)
        {
            return $@"
         <!DOCTYPE html>
         <html>
         <head>
           <meta charset='UTF-8'>
         </head>
         <body style='margin:0; padding:0; background:#f4f7fb; font-family:Segoe UI, Arial, sans-serif;'>

          <div style='max-width:520px; margin:40px auto; background:#ffffff; border-radius:18px; overflow:hidden; box-shadow:0 10px 30px rgba(15,23,42,0.12);'>

         <div style='background:linear-gradient(135deg,#2563eb,#7c3aed); padding:28px 30px; text-align:center; color:white;'>
            <h1 style='margin:0; font-size:24px; font-weight:700;'>Email Verification</h1>
            <p style='margin:8px 0 0; font-size:14px; opacity:0.9;'>Secure access to your account</p>
         </div>
 
         <div style='padding:32px 30px; color:#1e293b; text-align:center;'>

            <p style='font-size:16px; margin:0 0 14px;'>
                Hello,
            </p>

            <p style='font-size:15px; line-height:1.6; color:#475569; margin:0 0 24px;'>
                Please use the verification code below to complete your email verification.
            </p>

            <div style='display:inline-block; padding:16px 30px; background:#f1f5ff; border:1px dashed #2563eb; border-radius:14px; margin-bottom:24px;'>
                <span style='font-size:34px; letter-spacing:8px; font-weight:800; color:#2563eb;'>
                    {otp}
                </span>
            </div>

            <p style='font-size:14px; color:#64748b; line-height:1.6; margin:0 0 20px;'>
                This OTP is valid for a limited time. Please do not share this code with anyone.
            </p>

            <div style='height:1px; background:#e2e8f0; margin:24px 0;'></div>

            <p style='font-size:13px; color:#94a3b8; margin:0;'>
                If you did not request this verification, you can safely ignore this email.
            </p>

          </div>

          <div style='background:#f8fafc; padding:18px 30px; text-align:center;'>
            <p style='font-size:12px; color:#94a3b8; margin:0;'>
                © Aarohi Embedded Systems Pvt. Ltd.
            </p>
          </div>

           </div>
          </body>
          </html>";
        }
    }
}
