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