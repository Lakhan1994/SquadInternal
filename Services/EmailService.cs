using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SquadInternal.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

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
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            smtpClient.Send(mail);
        }

        // ✅ Leave Approved Template
        public string GetLeaveApprovedTemplate(string employeeName, DateTime from, DateTime to)
        {
            return $@"
            <div style='font-family: Arial; padding:20px;'>
                <h2 style='color:green;'>Leave Approved ✅</h2>
                <p>Hi {employeeName},</p>
                <p>Your leave request has been approved.</p>

                <table style='margin-top:10px;'>
                    <tr>
                        <td><strong>From:</strong></td>
                        <td>{from:dd MMM yyyy}</td>
                    </tr>
                    <tr>
                        <td><strong>To:</strong></td>
                        <td>{to:dd MMM yyyy}</td>
                    </tr>
                </table>

                <p style='margin-top:20px;'>Enjoy your time off 😊</p>
                <hr />
                <small>SquadInternal HR System</small>
            </div>";
        }

        // ✅ Leave Rejected Template
        public string GetLeaveRejectedTemplate(string employeeName, DateTime from, DateTime to)
        {
            return $@"
            <div style='font-family: Arial; padding:20px;'>
                <h2 style='color:red;'>Leave Rejected ❌</h2>
                <p>Hi {employeeName},</p>
                <p>Your leave request has been rejected.</p>

                <table style='margin-top:10px;'>
                    <tr>
                        <td><strong>From:</strong></td>
                        <td>{from:dd MMM yyyy}</td>
                    </tr>
                    <tr>
                        <td><strong>To:</strong></td>
                        <td>{to:dd MMM yyyy}</td>
                    </tr>
                </table>

                <p style='margin-top:20px;'>Please contact HR for more details.</p>
                <hr />
                <small>SquadInternal HR System</small>
            </div>";
        }

        public string GetAdminLeaveTemplate(string employeeName, DateTime from, DateTime to, int leaveId, string baseUrl)
        {
            return $@"
    <div style='font-family: Arial; padding:20px;'>
        <h2>New Leave Application</h2>

        <p><strong>Employee:</strong> {employeeName}</p>
        <p><strong>From:</strong> {from:dd MMM yyyy}</p>
        <p><strong>To:</strong> {to:dd MMM yyyy}</p>

        <div style='margin-top:20px;'>

            <a href='{baseUrl}/Admin/ApproveFromEmail?id={leaveId}'
               style='background-color:green;
                      color:white;
                      padding:10px 15px;
                      text-decoration:none;
                      border-radius:5px;'>
               Approve
            </a>

            &nbsp;&nbsp;

            <a href='{baseUrl}/Admin/RejectFromEmail?id={leaveId}'
               style='background-color:red;
                      color:white;
                      padding:10px 15px;
                      text-decoration:none;
                      border-radius:5px;'>
               Reject
            </a>

        </div>

        <hr />
        <small>SquadInternal HR System</small>
    </div>";
        }
    }
}