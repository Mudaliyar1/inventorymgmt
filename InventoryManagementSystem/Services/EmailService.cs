using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Data;
using InventoryManagementSystem.Extensions;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using MongoDB.Driver;
using System;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly BrevoSettings _settings;
        private readonly IBrevoEmailService _brevoApiEmailService;
        private readonly MongoDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<BrevoSettings> settings,
            IBrevoEmailService brevoApiEmailService,
            MongoDbContext context,
            IWebHostEnvironment env,
            ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _brevoApiEmailService = brevoApiEmailService;
            _context = context;
            _env = env;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            await SendEmailWithResultAsync(toEmail, subject, htmlMessage);
        }

        public async Task<(bool Success, string? ErrorMessage)> SendEmailWithResultAsync(string toEmail, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                var emptyErr = "Recipient email address is missing or empty.";
                _logger.LogWarning("[EMAIL] {Error}", emptyErr);
                return (false, emptyErr);
            }

            string? brevoError = null;

            // 1. First attempt Brevo REST API (checks BREVO_API_KEY env var and BrevoApiSettings:ApiKey)
            try
            {
                var (success, messageId, apiResp, error) = await _brevoApiEmailService.SendTransactionalEmailAsync(toEmail.Trim(), subject, htmlMessage);
                if (success)
                {
                    _logger.LogInformation("[EMAIL] Email successfully sent via Brevo REST API to {Email} (MessageId: {MessageId})", toEmail, messageId);
                    return (true, null);
                }
                brevoError = error;
                _logger.LogWarning("[EMAIL] Brevo REST API email attempt to {Email} failed: {Error}. Attempting SMTP fallback.", toEmail, error);
            }
            catch (Exception ex)
            {
                brevoError = ex.Message;
                _logger.LogWarning(ex, "[EMAIL] Brevo REST API exception when sending to {Email}: {Error}. Attempting SMTP fallback.", toEmail, ex.Message);
            }

            // 2. Validate SMTP credentials before attempting SMTP connection
            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                var reason = string.IsNullOrWhiteSpace(brevoError)
                    ? "Brevo API Key is missing and SMTP credentials are not configured in settings."
                    : $"Brevo API failed ({brevoError}) and SMTP credentials are not configured.";

                _logger.LogWarning("[EMAIL] Cannot send email to {Email}: {Reason}", toEmail, reason);
                if (_env.IsDevelopment())
                {
                    Console.WriteLine($"[EMAIL NOTICE] Cannot send email to {toEmail}: {reason}");
                }
                return (false, reason);
            }

            // 3. Send via MailKit SMTP
            try
            {
                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
                emailMessage.To.Add(new MailboxAddress("", toEmail.Trim()));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
                emailMessage.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[EMAIL] Email successfully sent via SMTP to {Email}", toEmail);
                return (true, null);
            }
            catch (Exception ex)
            {
                var smtpErr = $"SMTP delivery failed: {ex.Message}";
                _logger.LogError(ex, "[EMAIL] Email sending failed via SMTP to {Email}: {Message}", toEmail, ex.Message);
                Console.WriteLine($"[EMAIL ERROR] Sending to {toEmail} via SMTP failed: {ex.Message}");
                return (false, smtpErr);
            }
        }

        public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlMessage, byte[] attachmentBytes, string attachmentFileName, string contentType = "application/pdf")
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            string? brevoError = null;

            // 1. Try Brevo REST API with attachment
            try
            {
                var brevoResult = await _brevoApiEmailService.SendTransactionalEmailAsync(
                    toEmail.Trim(), 
                    subject, 
                    htmlMessage, 
                    attachmentFileName: attachmentFileName, 
                    attachmentBytes: attachmentBytes);

                if (brevoResult.Success)
                {
                    _logger.LogInformation("[EMAIL] Email with attachment '{File}' sent via Brevo API to {Email}", attachmentFileName, toEmail);
                    return;
                }
                brevoError = brevoResult.ErrorMessage;
            }
            catch (Exception ex)
            {
                brevoError = ex.Message;
                _logger.LogWarning(ex, "[EMAIL] Brevo API attachment sending failed: {Error}. Trying SMTP fallback.", ex.Message);
            }

            // 2. Validate SMTP credentials
            if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning("[EMAIL] SMTP credentials missing. Could not deliver attachment email to {Email}. Brevo error: {Err}", toEmail, brevoError);
                return;
            }

            // 3. Fallback to MailKit SMTP
            try
            {
                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
                emailMessage.To.Add(new MailboxAddress("", toEmail.Trim()));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
                if (attachmentBytes != null && attachmentBytes.Length > 0)
                {
                    bodyBuilder.Attachments.Add(attachmentFileName, attachmentBytes, ContentType.Parse(contentType));
                }
                emailMessage.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[EMAIL] Email with attachment '{File}' sent via SMTP to {Email}", attachmentFileName, toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL] SMTP delivery of attachment email failed for {Email}: {Msg}", toEmail, ex.Message);
            }
        }

        public async Task SendInvoiceEmailAsync(string toEmail, Sale sale, byte[] pdfBytes)
        {
            var fileName = $"Invoice_{sale.InvoiceNumber}.pdf";
            var subject = $"Invoice #{sale.InvoiceNumber} — Smart Inventory Management System";
            var html = $@"
<p>Dear {System.Net.WebUtility.HtmlEncode(sale.CustomerName ?? "Customer")},</p>
<p>Thank you for your purchase. Please find your invoice attached.</p>
<p><strong>Invoice Number:</strong> {sale.InvoiceNumber}<br/>
<strong>Amount:</strong> ₹ {sale.GrandTotal:N2}</p>
<p>Regards,<br/>SIMS Team</p>";
            await SendEmailWithAttachmentAsync(toEmail, subject, html, pdfBytes, fileName);
        }

        public async Task<(bool Success, string? ErrorMessage)> SendCustomQuoteConfirmationEmailAsync(string toEmail, string contactName, string inquiryReference)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Valued Customer" : contactName.Trim());
            var safeRef = System.Net.WebUtility.HtmlEncode(inquiryReference?.Trim() ?? string.Empty);

            var content = $@"
<p style='font-size:16px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for your interest in SIMS!
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    We have received your custom package inquiry. Our team will review your requirements and contact you regarding the next steps.
</p>
<div style='background-color:#1e293b;border-left:4px solid #3b82f6;padding:16px;border-radius:6px;margin:24px 0;'>
    <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;font-weight:700;letter-spacing:0.05em;'>Inquiry Reference</div>
    <div style='font-size:18px;font-weight:800;color:#38bdf8;font-family:monospace;margin-top:4px;'>{safeRef}</div>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    If you have any additional information to share, please reply to this email or contact our support team.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:24px;'>
    Thank you for considering SIMS for your business.
</p>
<p style='color:#94a3b8;font-size:14px;line-height:1.6;margin-top:20px;border-top:1px solid #334155;padding-top:16px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            var html = GetStandardHtmlTemplate("We Received Your Custom SIMS Request", content);
            return await SendEmailWithResultAsync(toEmail, "We Received Your Custom SIMS Request", html);
        }

        public async Task<(bool Success, string? ErrorMessage)> SendContactInquiryConfirmationEmailAsync(string toEmail, string customerName, string inquiryReference)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(customerName) ? "Valued Customer" : customerName.Trim());
            var safeRef = System.Net.WebUtility.HtmlEncode(inquiryReference?.Trim() ?? string.Empty);

            var content = $@"
<p style='font-size:16px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for contacting SIMS.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    We have successfully received your message and our team will review your inquiry. We will get back to you as soon as possible.
</p>
<div style='background-color:#1e293b;border-left:4px solid #3b82f6;padding:16px;border-radius:6px;margin:24px 0;'>
    <div style='font-size:11px;color:#94a3b8;text-transform:uppercase;font-weight:700;letter-spacing:0.05em;'>Inquiry Reference</div>
    <div style='font-size:18px;font-weight:800;color:#38bdf8;font-family:monospace;margin-top:4px;'>{safeRef}</div>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:24px;'>
    Thank you for reaching out to SIMS.
</p>
<p style='color:#94a3b8;font-size:14px;line-height:1.6;margin-top:20px;border-top:1px solid #334155;padding-top:16px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Support Team</strong>
</p>";

            var html = GetStandardHtmlTemplate("We Received Your Message — SIMS Support", content);
            return await SendEmailWithResultAsync(toEmail, "We Received Your Message — SIMS Support", html);
        }

        public async Task<(bool Success, string? ErrorMessage)> SendCustomQuoteReplyEmailAsync(string toEmail, string contactName, string inquiryReference, string status, string message)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Valued Customer" : contactName.Trim());
            var safeRef = System.Net.WebUtility.HtmlEncode(inquiryReference?.Trim() ?? string.Empty);
            var safeStatus = System.Net.WebUtility.HtmlEncode(status?.Trim() ?? "In Review");
            var safeMsg = System.Net.WebUtility.HtmlEncode(message?.Trim() ?? string.Empty).Replace("\n", "<br/>");

            var content = $@"
<p style='font-size:16px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Our team has reviewed your custom package request and has the following update regarding your inquiry:
</p>
<div style='background-color:#1e293b;border-left:4px solid #38bdf8;padding:16px;border-radius:6px;margin:20px 0;color:#f1f5f9;line-height:1.7;'>
    {safeMsg}
</div>
<div style='background-color:#0f172a;border:1px solid #334155;padding:12px 16px;border-radius:6px;margin:20px 0;font-size:13px;color:#94a3b8;'>
    <div><strong>Inquiry Reference:</strong> <span style='color:#38bdf8;font-family:monospace;'>{safeRef}</span></div>
    <div style='margin-top:4px;'><strong>Status:</strong> <span style='color:#f8fafc;'>{safeStatus}</span></div>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:20px;'>
    If you have any questions or would like to proceed with the next steps, simply reply directly to this email.
</p>
<p style='color:#94a3b8;font-size:14px;line-height:1.6;margin-top:20px;border-top:1px solid #334155;padding-top:16px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            var subject = $"Update on Your Custom SIMS Package Inquiry [{safeRef}]";
            var html = GetStandardHtmlTemplate(subject, content);
            return await SendEmailWithResultAsync(toEmail, subject, html);
        }

        public async Task<(bool Success, string? ErrorMessage)> SendContactInquiryReplyEmailAsync(string toEmail, string customerName, string inquiryReference, string status, string message)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(customerName) ? "Valued Customer" : customerName.Trim());
            var safeRef = System.Net.WebUtility.HtmlEncode(inquiryReference?.Trim() ?? string.Empty);
            var safeStatus = System.Net.WebUtility.HtmlEncode(status?.Trim() ?? "In Progress");
            var safeMsg = System.Net.WebUtility.HtmlEncode(message?.Trim() ?? string.Empty).Replace("\n", "<br/>");

            var content = $@"
<p style='font-size:16px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for contacting SIMS Support. Here is our response regarding your inquiry:
</p>
<div style='background-color:#1e293b;border-left:4px solid #38bdf8;padding:16px;border-radius:6px;margin:20px 0;color:#f1f5f9;line-height:1.7;'>
    {safeMsg}
</div>
<div style='background-color:#0f172a;border:1px solid #334155;padding:12px 16px;border-radius:6px;margin:20px 0;font-size:13px;color:#94a3b8;'>
    <div><strong>Inquiry Reference:</strong> <span style='color:#38bdf8;font-family:monospace;'>{safeRef}</span></div>
    <div style='margin-top:4px;'><strong>Status:</strong> <span style='color:#f8fafc;'>{safeStatus}</span></div>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:20px;'>
    If you need further assistance, simply reply directly to this email.
</p>
<p style='color:#94a3b8;font-size:14px;line-height:1.6;margin-top:20px;border-top:1px solid #334155;padding-top:16px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Support Team</strong>
</p>";

            var subject = $"Update on Your SIMS Support Inquiry [{safeRef}]";
            var html = GetStandardHtmlTemplate(subject, content);
            return await SendEmailWithResultAsync(toEmail, subject, html);
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
            await SendPasswordResetEmailAsync(toEmail, "User", resetLink, "Account");
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetLink, string accountType)
        {
            if (_env.IsDevelopment())
            {
                Console.WriteLine("=================================================");
                Console.WriteLine($"[SIMS DEV RESET LINK] Password reset link for {toEmail} ({accountType}): {resetLink}");
                Console.WriteLine("=================================================");
            }

            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName);
            var safeAccountType = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(accountType) ? "SIMS" : accountType);

            var content = $@"
<p>Hello <strong>{safeName}</strong>,</p>
<p>A password reset has been requested for your <strong>SIMS {safeAccountType}</strong> account.</p>
<p>Click the button below to create a new secure password:</p>
<div style='text-align: center; margin: 30px 0;'>
    <a href='{resetLink}' class='btn' style='display:inline-block;background-color:#0d6efd;color:#ffffff!important;padding:14px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:16px;'>Reset Password</a>
</div>
<p style='font-size:13px;color:#94a3b8;'>This link is temporary and can only be used for a limited time (<strong>60 minutes</strong>). It is single-use and will be invalidated after resetting.</p>
<p style='font-size:13px;color:#94a3b8;'>If you did not expect this request, you can safely ignore this email. Your current password remains secure.</p>
<p style='margin-top: 25px; font-size:13px; color:#64748b;'>Regards,<br /><strong>Smart Inventory Management System (SIMS)</strong></p>";

            await SendEmailAsync(toEmail, "Reset Your SIMS Account Password", GetStandardHtmlTemplate("Reset Your SIMS Account Password", content));
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

        public async Task SendLicenseRenewalApprovedEmailAsync(
            string toEmail, 
            string shopName, 
            string ownerName, 
            string packageName, 
            int extensionDays, 
            DateTime newExpiryDate, 
            string? adminNotes)
        {
            var safeShop = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(shopName) ? "Your Shop" : shopName);
            var safeOwner = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ownerName) ? "Store Owner" : ownerName);
            var safePackage = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(packageName) ? "Standard Package" : packageName);
            var safeNotes = !string.IsNullOrWhiteSpace(adminNotes) ? System.Net.WebUtility.HtmlEncode(adminNotes) : null;
            var istExpiry = newExpiryDate.AddMinutes(330);

            var notesHtml = safeNotes != null ? $@"
<div style='background-color:#1e293b;border-left:4px solid #10b981;padding:12px 16px;border-radius:4px;margin:20px 0;'>
    <strong style='color:#10b981;font-size:13px;display:block;margin-bottom:4px;'>Admin Notes / Message:</strong>
    <span style='color:#e2e8f0;font-size:14px;line-height:1.5;'>{safeNotes}</span>
</div>" : "";

            var content = $@"
<div style='text-align:center;margin-bottom:20px;'>
    <div style='display:inline-block;background-color:rgba(16,185,129,0.15);color:#10b981;padding:8px 18px;border-radius:20px;font-weight:700;font-size:14px;border:1px solid rgba(16,185,129,0.3);'>
        ✓ License Renewal Approved
    </div>
</div>
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeOwner}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Great news! Your license renewal request for <strong>{safeShop}</strong> has been officially approved by the platform administrator. Your shop's subscription is now active and extended.
</p>

<table style='width:100%;border-collapse:collapse;margin:20px 0;background-color:#0f172a;border-radius:8px;overflow:hidden;border:1px solid #334155;'>
    <tr style='border-bottom:1px solid #334155;'>
        <td style='padding:12px 16px;color:#94a3b8;font-size:13px;font-weight:600;'>Shop Name</td>
        <td style='padding:12px 16px;color:#f8fafc;font-weight:700;font-size:14px;'>{safeShop}</td>
    </tr>
    <tr style='border-bottom:1px solid #334155;'>
        <td style='padding:12px 16px;color:#94a3b8;font-size:13px;font-weight:600;'>Package / Plan</td>
        <td style='padding:12px 16px;color:#38bdf8;font-weight:700;font-size:14px;'>{safePackage}</td>
    </tr>
    <tr style='border-bottom:1px solid #334155;'>
        <td style='padding:12px 16px;color:#94a3b8;font-size:13px;font-weight:600;'>Extension Period</td>
        <td style='padding:12px 16px;color:#10b981;font-weight:700;font-size:14px;'>+{extensionDays} Days</td>
    </tr>
    <tr>
        <td style='padding:12px 16px;color:#94a3b8;font-size:13px;font-weight:600;'>New Expiry Date (IST)</td>
        <td style='padding:12px 16px;color:#fbbf24;font-family:monospace;font-size:13px;'>{istExpiry:dd MMM yyyy, hh:mm tt IST}</td>
    </tr>
</table>

{notesHtml}

<div style='text-align:center;margin:30px 0;'>
    <a href='#' class='btn' style='display:inline-block;background-color:#10b981;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        Log In to SIMS Dashboard
    </a>
</div>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Thank you for choosing SIMS for your mobile shop inventory management. If you have any questions or require custom assistance, reply directly to this email or contact support.
</p>";

            await SendEmailAsync(toEmail, $"[SIMS] License Renewal Approved - {safeShop}", GetStandardHtmlTemplate("License Renewal Approved", content));
        }

        public async Task SendLicenseRenewalRejectedEmailAsync(
            string toEmail, 
            string shopName, 
            string ownerName, 
            string packageName, 
            string? rejectionReason)
        {
            var safeShop = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(shopName) ? "Your Shop" : shopName);
            var safeOwner = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ownerName) ? "Store Owner" : ownerName);
            var safePackage = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(packageName) ? "Standard Package" : packageName);
            var safeReason = !string.IsNullOrWhiteSpace(rejectionReason) 
                ? System.Net.WebUtility.HtmlEncode(rejectionReason) 
                : "Your license renewal request could not be approved at this time. Please contact SaaS support or submit a new request with updated details.";

            var content = $@"
<div style='text-align:center;margin-bottom:20px;'>
    <div style='display:inline-block;background-color:rgba(239,68,68,0.15);color:#ef4444;padding:8px 18px;border-radius:20px;font-weight:700;font-size:14px;border:1px solid rgba(239,68,68,0.3);'>
        ✕ Renewal Request Update
    </div>
</div>
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeOwner}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Thank you for reaching out to us. We have reviewed the license renewal request for <strong>{safeShop}</strong> regarding the package <strong>{safePackage}</strong>.
</p>

<div style='background-color:#1e293b;border-left:4px solid #ef4444;padding:16px;border-radius:4px;margin:20px 0;'>
    <strong style='color:#ef4444;font-size:13px;display:block;margin-bottom:6px;'>Reason for Decision / Administrator Notes:</strong>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;font-family:sans-serif;'>{safeReason}</p>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    If you have any questions, need clarification, or would like to discuss alternative subscription plans or payment verification, you may submit a revised renewal request from your dashboard or reach out to our support team.
</p>

<div style='text-align:center;margin:30px 0;'>
    <a href='#' class='btn' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        View Subscription Plans
    </a>
</div>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Smart Inventory Management System (SIMS) Team
</p>";

            await SendEmailAsync(toEmail, $"[SIMS] License Renewal Request Update - {safeShop}", GetStandardHtmlTemplate("License Renewal Request Update", content));
        }

        public async Task SendShopSuspendedEmailAsync(string toEmail, string shopName, string ownerName, string? reason = null)
        {
            var safeShop = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(shopName) ? "Your Shop" : shopName);
            var safeOwner = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ownerName) ? "Store Owner" : ownerName);
            var safeReason = !string.IsNullOrWhiteSpace(reason)
                ? System.Net.WebUtility.HtmlEncode(reason)
                : "Your shop account has been placed on hold by the SaaS Platform Administrator. Operational modules, staff portals, and vendor access are temporarily paused.";

            var content = $@"
<div style='text-align:center;margin-bottom:20px;'>
    <div style='display:inline-block;background-color:rgba(239,68,68,0.15);color:#ef4444;padding:8px 18px;border-radius:20px;font-weight:700;font-size:14px;border:1px solid rgba(239,68,68,0.3);'>
        ⚠ Shop Account Suspended
    </div>
</div>
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeOwner}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    This is an important notification regarding your shop <strong>{safeShop}</strong> on the Smart Inventory Management System (SIMS). Your shop dashboard access has been suspended.
</p>

<div style='background-color:#1e293b;border-left:4px solid #ef4444;padding:16px;border-radius:4px;margin:20px 0;'>
    <strong style='color:#ef4444;font-size:13px;display:block;margin-bottom:6px;'>Reason / Notice:</strong>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;'>{safeReason}</p>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    While suspended, staff members, store employees, and associated suppliers will not be able to process sales, perform inventory updates, or view internal store records.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    To reactivate your shop account and resume full access, please contact our Platform Support Team immediately.
</p>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    SIMS SaaS Platform Administrator
</p>";

            await SendEmailAsync(toEmail, $"[SIMS] Urgent: Shop Account Suspended - {safeShop}", GetStandardHtmlTemplate("Shop Account Suspended", content));
        }

        public async Task SendLicenseExpiredEmailAsync(string toEmail, string shopName, string ownerName, DateTime expiryDate)
        {
            var safeShop = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(shopName) ? "Your Shop" : shopName);
            var safeOwner = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ownerName) ? "Store Owner" : ownerName);
            var istExpiry = expiryDate.AddMinutes(330).ToString("dd-MMM-yyyy, hh:mm tt IST");

            var content = $@"
<div style='text-align:center;margin-bottom:20px;'>
    <div style='display:inline-block;background-color:rgba(245,158,11,0.15);color:#f59e0b;padding:8px 18px;border-radius:20px;font-weight:700;font-size:14px;border:1px solid rgba(245,158,11,0.3);'>
        ⌛ Subscription License Expired
    </div>
</div>
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeOwner}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    The active subscription license for <strong>{safeShop}</strong> expired on <strong style='color:#f59e0b;'>{istExpiry}</strong>.
</p>

<div style='background-color:#1e293b;border-left:4px solid #f59e0b;padding:16px;border-radius:4px;margin:20px 0;'>
    <strong style='color:#f59e0b;font-size:13px;display:block;margin-bottom:6px;'>Service Impact:</strong>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;'>
        Store sales, product editing, stock transfers, and invoice creation are temporarily paused until your subscription is renewed or extended. You can still log in to view and submit a license renewal request.
    </p>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    Please renew your subscription plan immediately to retain continuous access to all your shop features and data.
</p>

<div style='text-align:center;margin:30px 0;'>
    <a href='#' class='btn' style='display:inline-block;background-color:#f59e0b;color:#111827!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        Renew License Now
    </a>
</div>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Smart Inventory Management System (SIMS) Team
</p>";

            await SendEmailAsync(toEmail, $"[SIMS] Subscription License Expired - {safeShop}", GetStandardHtmlTemplate("Subscription License Expired", content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendRegistrationOtpEmailAsync(string toEmail, string contactName, string otp, int expiryMinutes)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName);
            var safeOtp = System.Net.WebUtility.HtmlEncode(otp);
            var subject = "Verify Your Email to Start Your SIMS Free Trial";
            var content = $@"
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeName}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Thank you for choosing SIMS.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Please use the verification code below to verify your email address and continue your registration.
</p>

<div style='text-align:center;margin:30px 0;'>
    <div style='display:inline-block;background-color:#1e293b;border:2px dashed #38bdf8;padding:16px 36px;border-radius:10px;'>
        <span style='font-size:13px;color:#94a3b8;display:block;margin-bottom:6px;text-transform:uppercase;letter-spacing:1px;'>Your verification code</span>
        <span style='font-size:32px;font-weight:bold;letter-spacing:6px;color:#38bdf8;font-family:monospace;'>{safeOtp}</span>
    </div>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    This code expires in <strong>{expiryMinutes} minutes</strong>. If you did not request this code, you can safely ignore this email.
</p>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br />
    <strong>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate("Email Verification", content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrialReminderEmailAsync(string toEmail, string contactName, DateTime expiryUtc, string reminderType, string pricingUrl)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName);
            var expiryIst = expiryUtc.ToPolicyIstDisplayString();
            var safePricingUrl = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(pricingUrl) ? "#" : pricingUrl);
            var subject = "Your SIMS Free Trial Is Ending Soon";
            var content = $@"
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeName}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    This is a reminder that your SIMS free trial is scheduled to end on <strong>{expiryIst}</strong>.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    To continue using the features included in your subscription, please choose a suitable plan before your trial expires.
</p>

<div style='background-color:#1e293b;border-left:4px solid #38bdf8;padding:16px;border-radius:4px;margin:20px 0;'>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;'>
        <strong>Your trial expires:</strong> {expiryIst}
    </p>
</div>

<div style='text-align:center;margin:30px 0;'>
    <a href='{safePricingUrl}' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        Choose Your Plan
    </a>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    Thank you for trying SIMS.
</p>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br />
    <strong>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate("Trial Ending Soon", content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrialExpiredEmailAsync(string toEmail, string contactName, DateTime expiryUtc, string pricingUrl)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName);
            var expiryIst = expiryUtc.ToPolicyIstDisplayString();
            var safePricingUrl = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(pricingUrl) ? "#" : pricingUrl);
            var subject = "Your SIMS Free Trial Has Expired";
            var content = $@"
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeName}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Your SIMS free trial ended on <strong>{expiryIst}</strong>.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Your trial subscription is now marked as expired. To continue using subscription-protected features, please choose a suitable plan and complete the subscription process.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Your business data has not been automatically deleted because your trial expired.
</p>

<div style='text-align:center;margin:30px 0;'>
    <a href='{safePricingUrl}' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        View Subscription Plans
    </a>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    If you have already purchased a subscription, please sign in or contact our support team if your access has not been updated.
</p>

<p style='color:#cbd5e1;line-height:1.6;'>
    Thank you for trying SIMS.
</p>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br />
    <strong>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate("Free Trial Expired", content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendSubscriptionReminderEmailAsync(string toEmail, string contactName, DateTime expiryUtc, string reminderType, string renewalUrl)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName);
            var expiryIst = expiryUtc.ToPolicyIstDisplayString();
            var safeRenewalUrl = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(renewalUrl) ? "#" : renewalUrl);
            bool is7Day = reminderType.Contains("7Day", StringComparison.OrdinalIgnoreCase);

            string subject = is7Day ? "Your SIMS Subscription Expires in 7 Days" : "Your SIMS Subscription Expires Tomorrow";
            string bodyParagraph = is7Day
                ? $"Your SIMS subscription is scheduled to expire on <strong>{expiryIst}</strong>.<br/><br/>There are 7 days remaining on your current subscription. To continue using your subscribed features without interruption, please renew your plan before it expires."
                : $"Your SIMS subscription is scheduled to expire on <strong>{expiryIst}</strong>, which is approximately one day away.<br/><br/>Please renew your subscription to continue accessing the features included in your plan.";

            var content = $@"
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeName}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    {bodyParagraph}
</p>

{(is7Day ? $@"
<div style='background-color:#1e293b;border-left:4px solid #38bdf8;padding:16px;border-radius:4px;margin:20px 0;'>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;'>
        <strong>Subscription expiry:</strong> {expiryIst}
    </p>
</div>" : "")}

<div style='text-align:center;margin:30px 0;'>
    <a href='{safeRenewalUrl}' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        Renew Your Subscription
    </a>
</div>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br />
    <strong>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate(subject, content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendSubscriptionExpiredEmailAsync(string toEmail, string contactName, DateTime expiryUtc, string pricingUrl)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName);
            var expiryIst = expiryUtc.ToPolicyIstDisplayString();
            var safePricingUrl = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(pricingUrl) ? "#" : pricingUrl);
            var subject = "Your SIMS Subscription Has Expired";
            var content = $@"
<p style='font-size:15px;color:#e2e8f0;'>Hello <strong>{safeName}</strong>,</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Your SIMS subscription expired on <strong>{expiryIst}</strong>.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Subscription-protected features may no longer be available until an eligible subscription is activated.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    Your business data has not been automatically deleted because the subscription expired.
</p>
<p style='color:#cbd5e1;line-height:1.6;'>
    You can review the available plans and renew your subscription here:
</p>

<div style='text-align:center;margin:30px 0;'>
    <a href='{safePricingUrl}' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        View Subscription Plans
    </a>
</div>

<p style='color:#cbd5e1;line-height:1.6;'>
    If you have already renewed, please sign in or contact our support team if your access has not been restored.
</p>

<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br />
    <strong>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate("Subscription Expired", content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrialRegistrationPendingEmailAsync(string toEmail, string contactName)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName.Trim());
            var subject = "Your SIMS Registration Is Awaiting Approval";

            var content = $@"
<p style='font-size:15px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for registering with SIMS.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Your email has been verified, and your shop registration has been submitted for review.
</p>
<div style='background-color:#1e293b;border-left:4px solid #f59e0b;padding:16px;border-radius:6px;margin:20px 0;'>
    <p style='color:#f8fafc;font-size:14px;margin:0;line-height:1.6;'>
        <strong>Status:</strong> Under Review (Awaiting Super Admin Approval)
    </p>
    <p style='color:#94a3b8;font-size:13px;margin:6px 0 0 0;'>
        Your free trial will begin only once your registration is approved. We will notify you by email when your account is ready.
    </p>
</div>
<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate(subject, content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrialApprovedEmailAsync(string toEmail, string contactName, DateTime trialStartUtc, DateTime trialExpiryUtc)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName.Trim());
            var trialStartIst = trialStartUtc.AddMinutes(330).ToString("dd MMM yyyy, hh:mm tt") + " IST";
            var trialExpiryIst = trialExpiryUtc.AddMinutes(330).ToString("dd MMM yyyy, hh:mm tt") + " IST";
            var subject = "Your SIMS Account Has Been Approved";

            var content = $@"
<p style='font-size:15px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Good news! Your SIMS shop registration has been approved.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Your free trial is now active.
</p>
<div style='background-color:#1e293b;border-left:4px solid #10b981;padding:16px;border-radius:6px;margin:20px 0;'>
    <p style='color:#f8fafc;font-size:14px;margin:0 0 6px 0;'>
        <strong>Trial starts:</strong> {trialStartIst}
    </p>
    <p style='color:#f8fafc;font-size:14px;margin:0;'>
        <strong>Trial expires:</strong> {trialExpiryIst}
    </p>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:20px;'>
    You can now sign in and begin using SIMS.
</p>
<div style='text-align:center;margin:30px 0;'>
    <a href='/Account/Login' style='display:inline-block;background-color:#0284c7;color:#ffffff!important;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px;'>
        Sign In to Your Dashboard
    </a>
</div>
<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate(subject, content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrialRejectedEmailAsync(string toEmail, string contactName, string? rejectionReason)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName.Trim());
            var subject = "Update on Your SIMS Registration";

            var reasonBlock = !string.IsNullOrWhiteSpace(rejectionReason)
                ? $@"<div style='background-color:#1e293b;border-left:4px solid #ef4444;padding:16px;border-radius:6px;margin:20px 0;color:#f8fafc;font-size:14px;'>
                        <strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(rejectionReason.Trim())}
                     </div>"
                : string.Empty;

            var content = $@"
<p style='font-size:15px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for your interest in SIMS.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    After reviewing your registration, we are unable to approve your free-trial request at this time.
</p>
{reasonBlock}
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:20px;'>
    If you believe this decision needs further review, please contact our support team.
</p>
<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            return await SendEmailWithResultAsync(toEmail, subject, GetStandardHtmlTemplate(subject, content));
        }

        public async Task<(bool Success, string? ErrorMessage)> SendPaymentReceiptEmailAsync(
            string toEmail, 
            string contactName, 
            string packageName, 
            string receiptNumber, 
            string amountPaidFormatted, 
            string paymentReference, 
            byte[] receiptPdfBytes)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contactName) ? "Customer" : contactName.Trim());
            var safePkg = System.Net.WebUtility.HtmlEncode(packageName);
            var safeReceiptNo = System.Net.WebUtility.HtmlEncode(receiptNumber);
            var safeAmount = System.Net.WebUtility.HtmlEncode(amountPaidFormatted);
            var safeRef = System.Net.WebUtility.HtmlEncode(paymentReference);
            var subject = $"Your SIMS Payment Receipt — {safeReceiptNo}";

            var content = $@"
<p style='font-size:15px;color:#f8fafc;margin-bottom:16px;'>Hello {safeName},</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Thank you for your payment.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    We have verified your payment for the SIMS <strong>{safePkg}</strong> subscription.
</p>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:16px;'>
    Your payment receipt is attached to this email for your records.
</p>
<div style='background-color:#1e293b;border-left:4px solid #10b981;padding:16px;border-radius:6px;margin:20px 0;'>
    <p style='color:#f8fafc;font-size:14px;margin:0 0 6px 0;'>
        <strong>Receipt Number:</strong> {safeReceiptNo}
    </p>
    <p style='color:#f8fafc;font-size:14px;margin:0 0 6px 0;'>
        <strong>Amount Paid:</strong> {safeAmount}
    </p>
    <p style='color:#f8fafc;font-size:14px;margin:0;'>
        <strong>Payment Reference:</strong> {safeRef}
    </p>
</div>
<p style='color:#cbd5e1;line-height:1.7;margin-bottom:20px;'>
    If you have any questions about this transaction, please contact our support team.
</p>
<p style='color:#94a3b8;font-size:13px;line-height:1.5;margin-top:25px;'>
    Regards,<br/>
    <strong style='color:#f8fafc;'>SIMS Team</strong>
</p>";

            var html = GetStandardHtmlTemplate("Payment Receipt", content);
            var fileName = $"Receipt_{receiptNumber}.pdf";

            try
            {
                await SendEmailWithAttachmentAsync(toEmail, subject, html, receiptPdfBytes, fileName);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL] Failed to send payment receipt email to {Email}: {Msg}", toEmail, ex.Message);
                return (false, ex.Message);
            }
        }
    }
}
