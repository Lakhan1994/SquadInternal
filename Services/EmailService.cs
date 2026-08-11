
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Linq;

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

        // ============================================================
        // SEND EMAIL
        // ============================================================

        public void SendEmail(string toEmail, string subject, string body)
        {
            using var smtpClient = new SmtpClient(_config["Email:Smtp"])
            {
                Port = int.Parse(_config["Email:Port"]),
                Credentials = new NetworkCredential(
                    _config["Email:Username"],
                    _config["Email:Password"]),
                EnableSsl = true
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    _config["Email:Username"],
                    "Squad Logic HR"),
                Subject = subject,
                IsBodyHtml = true,
                Body = body
            };

            mail.To.Add(toEmail);

            // --------------------------------------------------------
            // Company Logo
            // --------------------------------------------------------

            var htmlView = AlternateView.CreateAlternateViewFromString(
                body,
                null,
                MediaTypeNames.Text.Html);

            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "company-logo.png");

            if (File.Exists(logoPath))
            {
                var logo = new LinkedResource(
                    logoPath,
                    MediaTypeNames.Image.Png)
                {
                    ContentId = "companylogo",
                    ContentType =
                    {
                        Name = "company-logo.png"
                    },
                    TransferEncoding = TransferEncoding.Base64
                };

                htmlView.LinkedResources.Add(logo);
            }

            mail.AlternateViews.Add(htmlView);

            smtpClient.Send(mail);
        }

        // ============================================================
        // COMMON EMAIL STYLES
        // ============================================================

        private string GetEmailHeader(string title, string subtitle)
        {
            return $@"
<tr>
    <td style='
        background: linear-gradient(135deg,#6674f6 0%,#4d5bd4 100%);
        padding:32px 25px;
        text-align:center;
        border-radius:14px 14px 0 0;
    '>

        <div style='
            font-family:Arial,Helvetica,sans-serif;
            font-size:30px;
            line-height:36px;
            font-weight:800;
            letter-spacing:2px;
            color:#ffffff;
        '>
            SQUAD LOGIC
        </div>

        <div style='
            margin-top:6px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:12px;
            line-height:18px;
            font-weight:600;
            letter-spacing:5px;
            color:#e8ebff;
        '>
            TREAT WITH TECHNOLOGY
        </div>

        <div style='
            width:55px;
            height:3px;
            background:#ffffff;
            margin:18px auto;
            border-radius:5px;
        '></div>

        <div style='
            font-family:Arial,Helvetica,sans-serif;
            font-size:22px;
            line-height:30px;
            font-weight:700;
            color:#ffffff;
        '>
            {title}
        </div>

        <div style='
            margin-top:5px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            line-height:20px;
            color:#e9ebff;
        '>
            {subtitle}
        </div>

    </td>
</tr>";
        }

        private string GetEmailFooter()
        {
            return $@"
<tr>
    <td style='
        background:#f7f8fc;
        padding:24px 20px;
        text-align:center;
        border-top:1px solid #e8eaf2;
    '>

        <div style='
            font-family:Arial,Helvetica,sans-serif;
            font-size:14px;
            font-weight:700;
            color:#4d5bd4;
        '>
            SQUAD LOGIC
        </div>

        <div style='
            margin-top:5px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:11px;
            letter-spacing:3px;
            color:#777f95;
        '>
            TREAT WITH TECHNOLOGY
        </div>

        <div style='
            margin-top:15px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:11px;
            line-height:18px;
            color:#8a8f9f;
        '>
            © {DateTime.Now.Year} SquadInternal HR System
        </div>

        <div style='
            margin-top:5px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:10px;
            color:#a0a5b2;
        '>
            This is an automated email. Please do not reply to this message.
        </div>

    </td>
</tr>";
        }

        private string GetLeaveDetailsTable(
            string leaveType,
            DateTime from,
            DateTime to,
            string reason)
        {
            var duration = (to - from).Days + 1;

            return $@"
<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='
            border-collapse:separate;
            border-spacing:0;
            background:#f8f9fd;
            border:1px solid #e8eaf2;
            border-radius:10px;
            overflow:hidden;
       '>

    <tr>
        <td style='
            padding:14px 16px;
            width:38%;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#72798a;
        '>
            <strong>Leave Type</strong>
        </td>

        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            font-weight:600;
            color:#252a38;
        '>
            {leaveType}
        </td>
    </tr>

    <tr>
        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#72798a;
        '>
            <strong>From</strong>
        </td>

        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#252a38;
        '>
            {from:dddd, dd MMM yyyy}
        </td>
    </tr>

    <tr>
        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#72798a;
        '>
            <strong>To</strong>
        </td>

        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#252a38;
        '>
            {to:dddd, dd MMM yyyy}
        </td>
    </tr>

    <tr>
        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#72798a;
        '>
            <strong>Duration</strong>
        </td>

        <td style='
            padding:14px 16px;
            border-bottom:1px solid #e8eaf2;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            font-weight:600;
            color:#252a38;
        '>
            {duration} day{(duration == 1 ? "" : "s")}
        </td>
    </tr>

    <tr>
        <td style='
            padding:14px 16px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            color:#72798a;
            vertical-align:top;
        '>
            <strong>Reason</strong>
        </td>

        <td style='
            padding:14px 16px;
            font-family:Arial,Helvetica,sans-serif;
            font-size:13px;
            line-height:20px;
            color:#252a38;
        '>
            {reason}
        </td>
    </tr>

</table>";
        }

        // ============================================================
        // OTP - PASSWORD RESET
        // ============================================================

        public string GetOtpTemplate(string userName, string otp, int validMinutes = 5)
        {
            var otpDigitsHtml = string.Join("", otp.Select(d => $@"
                <td style='
                    width:44px;
                    height:54px;
                    background:#eef1ff;
                    border:1px solid #d9deff;
                    border-radius:8px;
                    text-align:center;
                    font-family:Arial,Helvetica,sans-serif;
                    font-size:26px;
                    font-weight:800;
                    color:#4d5bd4;
                '>{d}</td>
                <td style='width:8px;'></td>"));

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Password Reset OTP - Squad Logic</title>
</head>

<body style='
    margin:0;
    padding:0;
    background:#eef1f8;
    font-family:Arial,Helvetica,sans-serif;
'>

<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='background:#eef1f8;padding:35px 15px;'>

<tr>
<td align='center'>

<table width='620'
       cellpadding='0'
       cellspacing='0'
       style='
            width:100%;
            max-width:620px;
            background:#ffffff;
            border-radius:14px;
            overflow:hidden;
            box-shadow:0 8px 30px rgba(35,45,80,0.10);
       '>

{GetEmailHeader(
    "Password Reset Request",
    "Use the OTP below to reset your account password")}

<tr>
<td style='padding:35px 32px;'>

<p style='
    margin:0 0 20px 0;
    font-family:Arial,Helvetica,sans-serif;
    font-size:15px;
    color:#333a4d;
'>
    Hello, <strong>{userName}</strong>,
</p>

<p style='
    margin:0 0 25px 0;
    font-family:Arial,Helvetica,sans-serif;
    font-size:14px;
    line-height:21px;
    color:#555c6d;
'>
    We received a request to reset your password. Please use the
    One-Time Password (OTP) below to proceed. This code is valid for
    <strong>{validMinutes} minutes</strong>.
</p>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center' style='padding:10px 0 25px 0;'>
    <table cellpadding='0' cellspacing='0'>
    <tr>
        {otpDigitsHtml}
    </tr>
    </table>
</td>
</tr>
</table>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#fff8e8;
    border:1px solid #ffe5a8;
    border-radius:10px;
    padding:14px 16px;
'>

<div style='
    font-size:13px;
    line-height:19px;
    color:#8a6d00;
    font-family:Arial,Helvetica,sans-serif;
'>
    ⚠️ For your security, do not share this OTP with anyone, including
    Squad Logic staff.
</div>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

<p style='
    margin:0;
    font-size:13px;
    line-height:21px;
    color:#666d7d;
'>
    If you did not request a password reset, you can safely ignore this
    email — your password will remain unchanged.
</p>

<p style='
    margin:22px 0 0 0;
    font-size:13px;
    color:#555c6d;
'>
    Regards,<br>
    <strong style='color:#4d5bd4;'>Squad Logic HR Team</strong>
</p>

</td>
</tr>

{GetEmailFooter()}

</table>

</td>
</tr>

</table>

</body>
</html>";
        }

        // ============================================================
        // 1. LEAVE APPROVED
        // ============================================================

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
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Leave Approved - Squad Logic</title>
</head>

<body style='
    margin:0;
    padding:0;
    background:#eef1f8;
    font-family:Arial,Helvetica,sans-serif;
'>

<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='background:#eef1f8;padding:35px 15px;'>

<tr>
<td align='center'>

<table width='620'
       cellpadding='0'
       cellspacing='0'
       style='
            width:100%;
            max-width:620px;
            background:#ffffff;
            border-radius:14px;
            overflow:hidden;
            box-shadow:0 8px 30px rgba(35,45,80,0.10);
       '>

{GetEmailHeader(
    "Leave Request Approved",
    "Your leave request has been successfully approved")}

<tr>
<td style='padding:35px 32px;'>

<p style='
    margin:0 0 20px 0;
    font-family:Arial,Helvetica,sans-serif;
    font-size:15px;
    color:#333a4d;
'>
    Dear <strong>{employeeName}</strong>,
</p>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#ecfdf3;
    border:1px solid #c8f0d8;
    border-radius:10px;
    padding:16px 18px;
'>

<div style='
    font-size:14px;
    font-weight:700;
    color:#198754;
'>
    ✓ Leave Approved
</div>

<div style='
    margin-top:5px;
    font-size:12px;
    line-height:18px;
    color:#4f6f5b;
'>
    Your leave request has been reviewed and approved by the management.
</div>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

{GetLeaveDetailsTable(leaveType, from, to, reason)}

<div style='height:25px;line-height:25px;'>&nbsp;</div>

<p style='
    margin:0;
    font-size:13px;
    line-height:21px;
    color:#666d7d;
'>
    Please make sure your leave dates are reflected correctly in your
    work schedule. We wish you a pleasant and restful time off.
</p>

<p style='
    margin:22px 0 0 0;
    font-size:13px;
    color:#555c6d;
'>
    Regards,<br>
    <strong style='color:#4d5bd4;'>Squad Logic HR Team</strong>
</p>

</td>
</tr>

{GetEmailFooter()}

</table>

</td>
</tr>

</table>

</body>
</html>";
        }

        // ============================================================
        // 2. LEAVE REJECTED
        // ============================================================

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
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Leave Request Update - Squad Logic</title>
</head>

<body style='
    margin:0;
    padding:0;
    background:#eef1f8;
    font-family:Arial,Helvetica,sans-serif;
'>

<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='background:#eef1f8;padding:35px 15px;'>

<tr>
<td align='center'>

<table width='620'
       cellpadding='0'
       cellspacing='0'
       style='
            width:100%;
            max-width:620px;
            background:#ffffff;
            border-radius:14px;
            overflow:hidden;
            box-shadow:0 8px 30px rgba(35,45,80,0.10);
       '>

{GetEmailHeader(
    "Leave Request Update",
    "An update has been made to your leave request")}

<tr>
<td style='padding:35px 32px;'>

<p style='
    margin:0 0 20px 0;
    font-size:15px;
    color:#333a4d;
'>
    Dear <strong>{employeeName}</strong>,
</p>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#fff1f2;
    border:1px solid #ffd1d5;
    border-radius:10px;
    padding:16px 18px;
'>

<div style='
    font-size:14px;
    font-weight:700;
    color:#dc3545;
'>
    ✕ Leave Request Rejected
</div>

<div style='
    margin-top:5px;
    font-size:12px;
    line-height:18px;
    color:#79535a;
'>
    Unfortunately, your leave request has not been approved.
</div>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

{GetLeaveDetailsTable(leaveType, from, to, reason)}

<div style='height:25px;line-height:25px;'>&nbsp;</div>

<p style='
    margin:0;
    font-size:13px;
    line-height:21px;
    color:#666d7d;
'>
    If you need clarification regarding this decision, please contact
    your reporting manager or the HR team.
</p>

<p style='
    margin:22px 0 0 0;
    font-size:13px;
    color:#555c6d;
'>
    Regards,<br>
    <strong style='color:#4d5bd4;'>Squad Logic HR Team</strong>
</p>

</td>
</tr>

{GetEmailFooter()}

</table>

</td>
</tr>

</table>

</body>
</html>";
        }

        // ============================================================
        // 3. ADMIN LEAVE REQUEST
        // ============================================================

        public string GetAdminLeaveTemplate(
            string employeeName,
            DateTime from,
            DateTime to,
            string leaveType,
            string reason,
            int leaveId,
            string baseUrl,
            string adminName = "Admin")
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>New Leave Request - Squad Logic</title>
</head>

<body style='
    margin:0;
    padding:0;
    background:#eef1f8;
    font-family:Arial,Helvetica,sans-serif;
'>

<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='background:#eef1f8;padding:35px 15px;'>

<tr>
<td align='center'>

<table width='620'
       cellpadding='0'
       cellspacing='0'
       style='
            width:100%;
            max-width:620px;
            background:#ffffff;
            border-radius:14px;
            overflow:hidden;
            box-shadow:0 8px 30px rgba(35,45,80,0.10);
       '>

{GetEmailHeader(
    "New Leave Request",
    "A new leave request requires your attention")}

<tr>
<td style='padding:35px 32px;'>

<p style='
    margin:0 0 20px 0;
    font-size:15px;
    color:#333a4d;
'>
    Dear <strong>{adminName}</strong>,
</p>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#eef2ff;
    border:1px solid #d9deff;
    border-radius:10px;
    padding:16px 18px;
'>

<div style='
    font-size:14px;
    font-weight:700;
    color:#4d5bd4;
'>
    🔔 Action Required
</div>

<div style='
    margin-top:5px;
    font-size:12px;
    line-height:18px;
    color:#626a82;
'>
    <strong>{employeeName}</strong> has submitted a leave request
    that is waiting for approval.
</div>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

{GetLeaveDetailsTable(leaveType, from, to, reason)}

<div style='height:28px;line-height:28px;'>&nbsp;</div>

<div style='
    text-align:center;
    font-family:Arial,Helvetica,sans-serif;
    font-size:14px;
    font-weight:600;
    color:#454b5c;
'>
    Please review this request and take appropriate action.
</div>

<div style='height:20px;line-height:20px;'>&nbsp;</div>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td align='center'>

<a href='{baseUrl}/Admin/ApproveFromEmail?id={leaveId}'
   style='
        display:inline-block;
        background:#28a745;
        color:#ffffff;
        text-decoration:none;
        padding:12px 25px;
        border-radius:7px;
        font-family:Arial,Helvetica,sans-serif;
        font-size:13px;
        font-weight:700;
        margin-right:8px;
   '>
    ✓ Approve Leave
</a>

<a href='{baseUrl}/Admin/RejectFromEmail?id={leaveId}'
   style='
        display:inline-block;
        background:#dc3545;
        color:#ffffff;
        text-decoration:none;
        padding:12px 25px;
        border-radius:7px;
        font-family:Arial,Helvetica,sans-serif;
        font-size:13px;
        font-weight:700;
        margin-left:8px;
   '>
    ✕ Reject Leave
</a>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

<p style='
    margin:0;
    text-align:center;
    font-size:11px;
    line-height:18px;
    color:#9096a5;
'>
    Please verify the request before taking action.
</p>

</td>
</tr>

{GetEmailFooter()}

</table>

</td>
</tr>

</table>

</body>
</html>";
        }

        // ============================================================
        // 4. LEAVE CANCELLED
        // ============================================================

        public string GetLeaveCancelledTemplate(
            string employeeName,
            DateTime from,
            DateTime to,
            string leaveType,
            string reason,
            string adminName = "Admin")
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Leave Cancelled - Squad Logic</title>
</head>

<body style='
    margin:0;
    padding:0;
    background:#eef1f8;
    font-family:Arial,Helvetica,sans-serif;
'>

<table width='100%'
       cellpadding='0'
       cellspacing='0'
       style='background:#eef1f8;padding:35px 15px;'>

<tr>
<td align='center'>

<table width='620'
       cellpadding='0'
       cellspacing='0'
       style='
            width:100%;
            max-width:620px;
            background:#ffffff;
            border-radius:14px;
            overflow:hidden;
            box-shadow:0 8px 30px rgba(35,45,80,0.10);
       '>

{GetEmailHeader(
    "Leave Request Cancelled",
    "A leave request has been cancelled")}

<tr>
<td style='padding:35px 32px;'>

<p style='
    margin:0 0 20px 0;
    font-size:15px;
    color:#333a4d;
'>
    Dear <strong>{adminName}</strong>,
</p>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#fff8e8;
    border:1px solid #ffe5a8;
    border-radius:10px;
    padding:16px 18px;
'>

<div style='
    font-size:14px;
    font-weight:700;
    color:#b77900;
'>
    ↩ Leave Request Cancelled
</div>

<div style='
    margin-top:5px;
    font-size:12px;
    line-height:18px;
    color:#776342;
'>
    <strong>{employeeName}</strong> has cancelled their leave request.
</div>

</td>
</tr>
</table>

<div style='height:25px;line-height:25px;'>&nbsp;</div>

{GetLeaveDetailsTable(leaveType, from, to, reason)}

<div style='height:25px;line-height:25px;'>&nbsp;</div>

<table width='100%' cellpadding='0' cellspacing='0'>
<tr>
<td style='
    background:#f8f9fd;
    border-radius:8px;
    padding:14px 16px;
    text-align:center;
    font-family:Arial,Helvetica,sans-serif;
    font-size:12px;
    line-height:19px;
    color:#6f7585;
'>
    No further action is required for this request.
</td>
</tr>
</table>

<p style='
    margin:22px 0 0 0;
    font-size:13px;
    color:#555c6d;
'>
    Regards,<br>
    <strong style='color:#4d5bd4;'>Squad Logic HR Team</strong>
</p>

</td>
</tr>

{GetEmailFooter()}

</table>

</td>
</tr>

</table>

</body>
</html>";
        }
    }
}