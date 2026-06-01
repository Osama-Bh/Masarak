
//using MailKit.Net.Smtp;
//using MimeKit;

//namespace GoWork.Services.EmailService
//{
//    public class EmailService : IEmailService
//    {
//        private readonly IConfiguration _configuration;
//        public EmailService(IConfiguration configuration)
//        {
//            _configuration = configuration;
//        }
//        public async Task SendEmailAsync(string to, string subject, string body, string userName = "Clinet")
//        {
//            // Retrieve the mail server (SMTP host) from the configuration.
//            string? MailServer = _configuration["EmailSettings:SmtpServer"];

//            // Retrieve the sender email address from the configuration.
//            string? SenderEmail = _configuration["EmailSettings:SenderEmail"];

//            // Retrieve the sender email password from the configuration.
//            string? Password = _configuration["EmailSettings:Password"];

//            // Retrieve the sender's display name from the configuration.
//            string? SenderName = _configuration["EmailSettings:SenderName"];

//            // Retrieve the sender's UserName from the configuration.
//            string? Username = _configuration["EmailSettings:Username"];

//            // Retrieve the SMTP port number from the configuration and convert it to an integer.
//            int Port = Convert.ToInt32(_configuration["EmailSettings:SmtpPort"]);


//            var message = new MimeMessage();

//            var from = new MailboxAddress(SenderName, SenderEmail);
//            message.From.Add(from);

//            var To = new MailboxAddress(userName, to);
//            message.To.Add(To);

//            message.Subject = subject;
//            message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
//            {
//                Text = body
//            };

//            var smtp = new SmtpClient();
//            await smtp.ConnectAsync(MailServer, Port, MailKit.Security.SecureSocketOptions.StartTls);
//            smtp.Authenticate(Username, Password);
//            await smtp.SendAsync(message);
//            smtp.Disconnect(true);
//        }

//        private string BuildEmailTemplate(string message)
//        {
//            return $@"
//                <!DOCTYPE html>
//                <html lang=""ar"" dir=""rtl"">
//                <head>
//                    <meta charset=""UTF-8"">
//                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
//                    <title>Masarak Email</title>
//                </head>

//                <body style=""margin:0; background:#f5f5f5; padding:20px;"">

//                    <div style=""font-family:Tahoma,Arial,sans-serif;
//                                max-width:600px;
//                                margin:auto;
//                                border:1px solid #e0e0e0;
//                                border-radius:12px;
//                                overflow:hidden;
//                                box-shadow:0 4px 15px rgba(0,0,0,0.05);
//                                background:#ffffff;"">

//                        <!-- Header -->
//                        <div style=""background:linear-gradient(135deg, #01bafd 0%, #0199db 100%);
//                                    padding:30px;
//                                    text-align:center;"">

//                            <h1 style=""color:white;
//                                       margin:0;
//                                       font-size:28px;
//                                       font-weight:700;"">
//                                Masarak.
//                            </h1>
//                        </div>

//                        <!-- Content -->
//                        <div style=""padding:30px; text-align:right;"">

//                            <h2 style=""color:#1f2937;
//                                       margin-top:0;
//                                       font-size:22px;
//                                       font-weight:600;"">
//                                مرحباً
//                            </h2>

//                            <div style=""color:#4b5563;
//                                        line-height:1.8;
//                                        font-size:16px;"">

//                                {message}

//                            </div>

//                            <!-- Signature -->
//                            <div style=""margin-top:40px;
//                                        padding-top:25px;
//                                        border-top:1px solid #f3f4f6;
//                                        text-align:center;"">

//                                <p style=""color:#9ca3af;
//                                          font-size:14px;
//                                          margin:0;"">
//                                    مع أطيب التحيات
//                                </p>

//                                <p style=""color:#02b5f1;
//                                          font-weight:700;
//                                          font-size:18px;
//                                          margin:8px 0;"">
//                                    Masarak.
//                                </p>
//                            </div>

//                        </div>

//                        <!-- Footer -->
//                        <div style=""background-color:#f9fafb;
//                                    padding:25px;
//                                    text-align:center;
//                                    border-top:1px solid #f3f4f6;"">

//                            <p style=""color:#9ca3af;
//                                      font-size:13px;
//                                      margin:0;"">
//                                © 2026 Masarak. نبني مستقبل العمل
//                            </p>

//                        </div>

//                    </div>

//                </body>
//                </html>";
//        }
//    }
//}

using MailKit.Net.Smtp;
using MimeKit;

namespace GoWork.Services.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string message,
            string userName = "Client")
        {
            // SMTP Configuration
            string? mailServer = _configuration["EmailSettings:SmtpServer"];
            string? senderEmail = _configuration["EmailSettings:SenderEmail"];
            string? password = _configuration["EmailSettings:Password"];
            string? senderName = _configuration["EmailSettings:SenderName"];
            string? username = _configuration["EmailSettings:Username"];
            int port = Convert.ToInt32(_configuration["EmailSettings:SmtpPort"]);

            // Build Styled HTML
            var styledBody = BuildEmailTemplate(message);

            // Create Email
            var emailMessage = new MimeMessage();

            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));

            emailMessage.To.Add(new MailboxAddress(userName, to));

            emailMessage.Subject = subject;

            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = styledBody
            };

            // Send Email
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                mailServer,
                port,
                MailKit.Security.SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(username, password);

            await smtp.SendAsync(emailMessage);

            await smtp.DisconnectAsync(true);
        }

        private string BuildEmailTemplate(string message)
        {
            return $@"
                                <!doctype html>
                <html lang=""ar"" dir=""rtl"">
                  <head>
                    <meta charset=""UTF-8"" />
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                    <title>Masarak Email</title>
                  </head>

                  <body style=""margin: 0; background: #f5f5f5"">
                    <div
                      style=""
                        font-family: Tahoma, Arial, sans-serif;
                        max-width: 600px;
                        margin: auto;
                        border: 1px solid #e0e0e0;
                        border-radius: 12px;
                        overflow: hidden;
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.05);
                        background: #ffffff;
                      ""
                    >
                      <!-- Header -->
                      <div
                        style=""
                          background: linear-gradient(135deg, #01bafd 0%, #0199db 100%);
                          padding: 30px;
                          text-align: center;
                        ""
                      >
                        <h1 style=""color: white; margin: 0; font-size: 28px; font-weight: 700"">
                          Masarak.
                        </h1>
                      </div>

                        <!-- Content -->

                                {message}
                          <!--  end content

                        <!-- Signature -->
                        <div
                          style=""
                            margin-top: 40px;
                            padding-top: 25px;
                            border-top: 1px solid #f3f4f6;
                            text-align: center;
                          ""
                        >
                          <p style=""color: #9ca3af; font-size: 14px; margin: 0"">
                            مع أطيب التحيات
                          </p>

                          <p
                            style=""
                              color: #02b5f1;
                              font-weight: 700;
                              font-size: 18px;
                              margin: 8px 0;
                            ""
                          >
                            Masarak.
                          </p>
                        </div>
                      </div> 

                      <!-- Footer -->
                      <div
                        style=""
                          background-color: #f9fafb;
                          padding: 25px;
                          text-align: center;
                          border-top: 1px solid #f3f4f6;
                        ""
                      >
                        <p style=""color: #9ca3af; font-size: 13px; margin: 0"">
                          © 2026 Masarak. نبني مستقبل العمل
                        </p>
                      </div>
                    </div>
                  </body>
                </html>
                "
                ;
        }
    }
}