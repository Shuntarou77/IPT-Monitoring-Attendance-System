using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IPT102monitoringAttendance.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"]?.Replace(" ", ""); // Remove spaces from app password
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;
            var fromName = _configuration["EmailSettings:FromName"] ?? "Attendance Monitoring System";

            if (string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
            {
                _logger.LogError("Email settings are not configured properly. SmtpUsername or SmtpPassword is empty.");
                throw new InvalidOperationException("Email settings are not configured. Please configure SMTP settings in appsettings.json");
            }

            _logger.LogInformation($"Attempting to send email to {email} using SMTP server {smtpHost}:{smtpPort}");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Password Reset Request - Attendance Monitoring System";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; border-radius: 10px;'>
                            <h2 style='color: #6b40e3;'>Password Reset Request</h2>
                            <p>Hello,</p>
                            <p>You have requested to reset your password for the Attendance Monitoring System.</p>
                            <p>Please click the link below to reset your password:</p>
                            <p style='margin: 20px 0;'>
                                <a href='{resetUrl}' style='display: inline-block; padding: 12px 24px; background-color: #6b40e3; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Reset Password</a>
                            </p>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; color: #666;'>{resetUrl}</p>
                            <p style='margin-top: 30px; font-size: 12px; color: #999;'>
                                <strong>Note:</strong> This link will expire in 1 hour. If you did not request a password reset, please ignore this email.
                            </p>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;' />
                            <p style='font-size: 12px; color: #999; text-align: center;'>
                                This is an automated message. Please do not reply to this email.
                            </p>
                        </div>
                    </body>
                    </html>
                "
            };

            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                try
                {
                    _logger.LogInformation($"Connecting to SMTP server {smtpHost}:{smtpPort}...");
                    await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                    
                    _logger.LogInformation($"Authenticating with username: {smtpUsername}...");
                    await client.AuthenticateAsync(smtpUsername, smtpPassword);
                    
                    _logger.LogInformation($"Sending email to {email}...");
                    await client.SendAsync(message);
                    
                    _logger.LogInformation($"Email sent successfully to {email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error sending email to {email}: {ex.Message}");
                    throw new Exception($"Failed to send email: {ex.Message}. Please verify your Gmail App Password is correct.", ex);
                }
                finally
                {
                    if (client.IsConnected)
                    {
                        await client.DisconnectAsync(true);
                    }
                }
            }
        }
    }
}

