using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Interfaces;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly BrevoSettings _settings;
        private readonly IBrevoEmailService _brevoApiEmailService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<BrevoSettings> settings,
            IBrevoEmailService brevoApiEmailService,
            IWebHostEnvironment env,
            ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _brevoApiEmailService = brevoApiEmailService;
            _env = env;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            // 1. First check if Brevo REST API can be used (if ApiKey is set)
            var apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var (success, _, _, error) = await _brevoApiEmailService.SendTransactionalEmailAsync(toEmail, subject, htmlMessage);
                if (success) return;
                _logger.LogWarning("Brevo REST API email attempt failed: {Error}. Trying SMTP fallback.", error);
            }

            // 2. Validate SMTP credentials before attempting connection
            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning("[EMAIL] SMTP credentials (Host/Username/Password) are not configured in appsettings.json. Skipping SMTP attempt.");
                if (_env.IsDevelopment())
                {
                    Console.WriteLine($"[EMAIL NOTICE] Cannot send email to {toEmail} via SMTP: Username or Password is empty in appsettings.json -> BrevoSettings.");
                }
                return;
            }

            // 3. Send via MailKit SMTP
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
                await client.SendAsync(emailMessage);
                _logger.LogInformation("[EMAIL] Email successfully sent via SMTP to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email sending failed: {Message}", ex.Message);
                Console.WriteLine($"Email sending failed: {ex.Message}");
            }
            finally
            {
                try
                {
                    await client.DisconnectAsync(true);
                }
                catch { }
            }
        }

        private string GetStandardHtmlTemplate(string title, string content)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>{title}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #1a1e29;
            color: #ffffff;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #0f121d;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0,0,0,0.5);
            border: 1px solid #232a3b;
        }}
        .header {{
            background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%);
            padding: 30px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            color: #ffffff;
            font-size: 24px;
            font-weight: 600;
        }}
        .body {{
            padding: 40px 30px;
            line-height: 1.6;
            color: #e2e8f0;
        }}
        .footer {{
            background-color: #0c0e18;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #64748b;
            border-top: 1px solid #232a3b;
        }}
        .btn {{
            display: inline-block;
            background-color: #0d6efd;
            color: #ffffff !important;
            padding: 12px 25px;
            border-radius: 5px;
            text-decoration: none;
            font-weight: 600;
            margin-top: 20px;
        }}
        .warning {{
            color: #ffc107;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{title}</h1>
        </div>
        <div class='body'>
            {content}
        </div>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} Smart Inventory Management System (SIMS). All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string username)
        {
            var content = $@"
<p>Hello <strong>{username}</strong>,</p>
<p>Welcome to the <strong>Smart Inventory Management System (SIMS)</strong>! Your account has been successfully set up and is active.</p>
<p>You can now log in using your credentials to manage inventory, track products, and generate reports.</p>
<a href='#' class='btn'>Access Dashboard</a>
<p style='margin-top: 20px; font-size: 13px; color: #64748b;'>If you did not request this account, please notify the system administrator immediately.</p>";
            await SendEmailAsync(toEmail, "Welcome to SIMS!", GetStandardHtmlTemplate("Welcome to SIMS", content));
        }

        public async Task SendForgotPasswordEmailAsync(string toEmail, string resetLink)
        {
            var content = $@"
<p>Hello,</p>
<p>We received a request to reset your password for the Smart Inventory Management System (SIMS).</p>
<p>Please click the button below to choose a new password. This link is valid for 1 hour.</p>
<a href='{resetLink}' class='btn'>Reset Password</a>
<p style='margin-top: 25px;'>If you did not request a password reset, please ignore this email. Your password will remain unchanged.</p>";
            await SendEmailAsync(toEmail, "Reset Your Password - SIMS", GetStandardHtmlTemplate("Reset Your Password", content));
        }

        public async Task SendPasswordResetOtpEmailAsync(string toEmail, string otpCode, string recipientName)
        {
            if (_env.IsDevelopment())
            {
                Console.WriteLine("=================================================");
                Console.WriteLine($"[SIMS DEV OTP] Verification OTP for {toEmail}: {otpCode}");
                Console.WriteLine("=================================================");
            }

            var content = $@"
<p>Hello <strong>{System.Net.WebUtility.HtmlEncode(recipientName)}</strong>,</p>
<p>We received a request to reset your password for your <strong>Smart Inventory Management System (SIMS)</strong> account.</p>
<p>Please use the following 6-digit One-Time Password (OTP) to reset your password:</p>

<div style='background-color: #1e2538; border: 2px dashed #3b82f6; border-radius: 10px; padding: 18px 24px; text-align: center; margin: 24px 0;'>
    <div style='font-size: 12px; color: #94a3b8; text-transform: uppercase; font-weight: 700; letter-spacing: 1px; margin-bottom: 6px;'>Your Verification OTP</div>
    <div style='font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #38bdf8; font-family: monospace;'>{otpCode}</div>
    <div style='font-size: 12px; color: #f59e0b; margin-top: 8px; font-weight: 600;'>Valid for 15 minutes only</div>
</div>

<p style='font-size: 13px; color: #94a3b8;'>Enter this OTP on the password reset screen along with your new password (minimum 8 characters).</p>
<p style='margin-top: 20px; font-size: 12px; color: #64748b;'>If you did not request a password reset, please ignore this email or notify your system administrator immediately. Your password will remain unchanged.</p>";

            await SendEmailAsync(toEmail, $"SIMS Password Reset OTP: {otpCode}", GetStandardHtmlTemplate("Password Reset OTP", content));
        }

        public async Task SendPasswordChangedEmailAsync(string toEmail, string username)
        {
            var content = $@"
<p>Hello <strong>{username}</strong>,</p>
<p>This is to confirm that the password for your SIMS account has been successfully changed.</p>
<p class='warning'>If you did not make this change, please contact the administrator immediately to secure your account.</p>
<a href='#' class='btn'>Log In to SIMS</a>";
            await SendEmailAsync(toEmail, "Password Changed - SIMS", GetStandardHtmlTemplate("Password Changed", content));
        }

        public async Task SendLowStockAlertEmailAsync(string toEmail, string productName, int currentStock, int minStock)
        {
            var content = $@"
<p>Hello,</p>
<p class='warning'>This is an automated inventory alert from SIMS.</p>
<p>The product <strong>{productName}</strong> has fallen below its minimum stock threshold.</p>
<table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
    <tr style='background-color: #232a3b; border: 1px solid #4a5568;'>
        <th style='padding: 8px; text-align: left; color: #a0aec0;'>Metric</th>
        <th style='padding: 8px; text-align: left; color: #a0aec0;'>Value</th>
    </tr>
    <tr style='border: 1px solid #4a5568;'>
        <td style='padding: 8px;'>Current Stock</td>
        <td style='padding: 8px; color: #f56565; font-weight: bold;'>{currentStock}</td>
    </tr>
    <tr style='border: 1px solid #4a5568;'>
        <td style='padding: 8px;'>Minimum Required</td>
        <td style='padding: 8px;'>{minStock}</td>
    </tr>
</table>
<p style='margin-top: 20px;'>Please log in to increase the stock level for this item.</p>
<a href='#' class='btn'>View Inventory</a>";
            await SendEmailAsync(toEmail, $"Low Stock Alert: {productName} - SIMS", GetStandardHtmlTemplate("Low Stock Alert", content));
        }

        public async Task SendNewUserCreatedEmailAsync(string toEmail, string username, string role, string tempPassword)
        {
            var content = $@"
<p>Hello <strong>{username}</strong>,</p>
<p>Your user profile has been created by the Administrator under the role <strong>{role}</strong>.</p>
<p>You can use the temporary details below to log in for the first time. You will be prompted to change your password immediately.</p>
<table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
    <tr style='border: 1px solid #4a5568;'>
        <td style='padding: 8px; font-weight: bold;'>Username / Email</td>
        <td style='padding: 8px;'>{toEmail}</td>
    </tr>
    <tr style='border: 1px solid #4a5568;'>
        <td style='padding: 8px; font-weight: bold;'>Temporary Password</td>
        <td style='padding: 8px; font-family: monospace; background-color: #232a3b;'>{tempPassword}</td>
    </tr>
</table>
<a href='#' class='btn'>Log In to System</a>";
            await SendEmailAsync(toEmail, "Account Created - SIMS", GetStandardHtmlTemplate("SIMS Account Created", content));
        }
    }
}
