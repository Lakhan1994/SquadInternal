using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Mime;

namespace SquadInternal.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly string _logoUrl;

        public EmailService(IConfiguration config)
        {
            _config = config;
            _logoUrl = _config["Email:LogoUrl"];
        }



        // ===================================
        // 📩 SEND EMAIL METHOD
        // ===================================

        public void SendEmail(string toEmail, string subject, string body)
        {
            var smtpClient = new SmtpClient(_config["Email:Smtp"])
            {
                Port = int.Parse(_config["Email:Port"]),
                Credentials = new NetworkCredential(
                    _config["Email:Username"],
                    _config["Email:Password"]),
                EnableSsl = true,
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_config["Email:Username"]),
                Subject = subject,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            // Create HTML view
            var htmlView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);

            // Absolute path to logo
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "company-logo.png"
            );

            if (File.Exists(logoPath))
            {
                LinkedResource logo = new LinkedResource(logoPath, MediaTypeNames.Image.Png);
                logo.ContentId = "companylogo";
                logo.ContentType.Name = "company-logo.png";
                logo.TransferEncoding = TransferEncoding.Base64;

                htmlView.LinkedResources.Add(logo);
            }

            mail.AlternateViews.Add(htmlView);

            smtpClient.Send(mail);
        }


        // ===================================
        // ✅ Leave Approved Template (Professional)
        // ===================================
        public string GetLeaveApprovedTemplate(
     string employeeName,
     DateTime from,
     DateTime to,
     string leaveType,
     string reason)
        {
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Arial, sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:8px;overflow:hidden;'>

<!-- HEADER -->
<tr>
<td style='background-color:#4e73df;padding:20px;text-align:center;'>

<img src=""cid:companylogo"" style=""height:60px;margin-bottom:15px;"" />

<h2 style='color:#ffffff;margin:0;'>Leave Request Approved</h2>

</td>
</tr>

<!-- BODY -->
<tr>
<td style='padding:30px;color:#333333;font-size:14px;'>

<p>Dear <strong>{employeeName}</strong>,</p>

<p style='color:#28a745;font-weight:bold;'>
Your leave request has been approved.
</p>

<table width='100%' cellpadding='8' cellspacing='0' style='background-color:#f8f9fc;border-radius:5px;'>

<tr>
<td style='color:#555555;width:150px;'><strong>Leave Type:</strong></td>
<td>{leaveType}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>From:</strong></td>
<td>{from:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>To:</strong></td>
<td>{to:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Duration:</strong></td>
<td>{(to - from).Days + 1} day(s)</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Reason:</strong></td>
<td>{reason}</td>
</tr>

</table>

</td>
</tr>

<!-- FOOTER -->
<tr>
<td style='background-color:#f8f9fc;text-align:center;padding:15px;font-size:12px;color:#777777;'>

© {DateTime.Now.Year} SquadInternal HR System

</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }

        // ===================================
        // ❌ Leave Rejected Template (Professional)
        // ===================================
        public string GetLeaveRejectedTemplate(
    string employeeName,
    DateTime from,
    DateTime to,
    string leaveType,
    string reason)
        {
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Arial, sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:8px;overflow:hidden;'>

<!-- HEADER -->
<tr>
<td style='background-color:#4e73df;padding:20px;text-align:center;'>

<img src=""cid:companylogo"" style=""height:60px;margin-bottom:15px;"" />
<h2 style='color:#ffffff;margin:0;'>Leave Request Update</h2>

</td>
</tr>

<!-- BODY -->
<tr>
<td style='padding:30px;color:#333333;font-size:14px;'>

<p>Dear <strong>{employeeName}</strong>,</p>

<p style='color:#dc3545;font-weight:bold;'>
Your leave request has been rejected.
</p>

<table width='100%' cellpadding='8' cellspacing='0' style='background-color:#f8f9fc;border-radius:5px;'>

<tr>
<td style='color:#555555;width:150px;'><strong>Leave Type:</strong></td>
<td>{leaveType}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>From:</strong></td>
<td>{from:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>To:</strong></td>
<td>{to:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Duration:</strong></td>
<td>{(to - from).Days + 1} day(s)</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Reason:</strong></td>
<td>{reason}</td>
</tr>

</table>

</td>
</tr>

<!-- FOOTER -->
<tr>
<td style='background-color:#f8f9fc;text-align:center;padding:15px;font-size:12px;color:#777777;'>

© {DateTime.Now.Year} SquadInternal HR System

</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }
        // ===================================
        // 📩 Admin Leave Request Template
        // ===================================
        public string GetAdminLeaveTemplate(
     string employeeName,
     DateTime from,
     DateTime to,
     string leaveType,
     string reason,
     int leaveId,
     string baseUrl)
        {
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Arial, sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:8px;overflow:hidden;'>

<!-- HEADER -->
<tr>
<td style='background-color:#4e73df;padding:20px;text-align:center;'>

<img src=""cid:companylogo"" style=""height:60px;margin-bottom:15px;"" />
<h2 style='color:#ffffff;margin:0;'>New Leave Request</h2>

</td>
</tr>

<!-- BODY -->
<tr>
<td style='padding:30px;color:#333333;font-size:14px;'>

<p>Dear Admin,</p>

<p><strong>{employeeName}</strong> has submitted a leave request for approval.</p>

<table width='100%' cellpadding='8' cellspacing='0' style='background-color:#f8f9fc;border-radius:5px;'>

<tr>
<td style='color:#555555;width:150px;'><strong>Leave Type:</strong></td>
<td>{leaveType}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>From:</strong></td>
<td>{from:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>To:</strong></td>
<td>{to:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Reason:</strong></td>
<td>{reason}</td>
</tr>

</table>

<br/>

<p>Please review and take necessary action.</p>

<!-- BUTTONS -->
<div style='text-align:center;margin-top:25px;'>

<a href='{baseUrl}/Admin/ApproveFromEmail?id={leaveId}'
   style='background-color:#28a745;color:#ffffff;padding:10px 20px;
          text-decoration:none;border-radius:4px;margin-right:10px;
          display:inline-block;'>
Approve
</a>

<a href='{baseUrl}/Admin/RejectFromEmail?id={leaveId}'
   style='background-color:#dc3545;color:#ffffff;padding:10px 20px;
          text-decoration:none;border-radius:4px;
          display:inline-block;'>
Reject
</a>

</div>

</td>
</tr>

<!-- FOOTER -->
<tr>
<td style='background-color:#f8f9fc;text-align:center;padding:15px;font-size:12px;color:#777777;'>

© {DateTime.Now.Year} SquadInternal HR System

</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }

        public string GetLeaveCancelledTemplate(
    string employeeName,
    DateTime from,
    DateTime to,
    string leaveType,
    string reason)
        {
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Arial, sans-serif;'>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center'>

<table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:8px;overflow:hidden;'>

<!-- HEADER -->
<tr>
<td style='background-color:#4e73df;padding:20px;text-align:center;'>

<img src=""cid:companylogo"" style=""height:60px;margin-bottom:15px;"" />

<h2 style='color:#ffffff;margin:0;'>Leave Request Update</h2>

</td>
</tr>

<!-- BODY -->
<tr>
<td style='padding:30px;color:#333333;font-size:14px;'>

<p>Dear Admin,</p>

<p><strong>{employeeName}</strong> has cancelled a leave request.</p>

<table width='100%' cellpadding='8' cellspacing='0' style='background-color:#f8f9fc;border-radius:5px;'>

<tr>
<td style='color:#555555;width:150px;'><strong>Leave Type:</strong></td>
<td>{leaveType}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>From:</strong></td>
<td>{from:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>To:</strong></td>
<td>{to:dddd, dd MMM yyyy}</td>
</tr>

<tr>
<td style='color:#555555;'><strong>Reason:</strong></td>
<td>{reason}</td>
</tr>

</table>

<br/>

<p style='color:#dc3545;font-weight:bold;'>
No further action is required.
</p>

</td>
</tr>

<!-- FOOTER -->
<tr>
<td style='background-color:#f8f9fc;text-align:center;padding:15px;font-size:12px;color:#777777;'>

© {DateTime.Now.Year} SquadInternal HR System

</td>
</tr>

</table>

</td>
</tr>
</table>

</body>
</html>";
        }

    }
}