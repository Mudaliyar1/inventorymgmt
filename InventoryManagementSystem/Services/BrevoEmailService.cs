using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class BrevoEmailService : IBrevoEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly BrevoApiSettings _settings;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(
            HttpClient httpClient,
            IOptions<BrevoApiSettings> options,
            ILogger<BrevoEmailService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<(bool Success, string MessageId, string ApiResponse, string ErrorMessage)> SendTransactionalEmailAsync(
            string recipientEmail,
            string subject,
            string htmlContent,
            List<string>? ccRecipients = null)
        {
            var apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = _settings.ApiKey;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var err = "Brevo API Key is missing. Please configure BREVO_API_KEY in environment variables or appsettings.json.";
                _logger.LogError(err);
                return (false, string.Empty, string.Empty, err);
            }

            var senderEmail = Environment.GetEnvironmentVariable("BREVO_FROM_EMAIL");
            if (string.IsNullOrWhiteSpace(senderEmail) || senderEmail.EndsWith("@sims.com", StringComparison.OrdinalIgnoreCase))
            {
                senderEmail = !string.IsNullOrWhiteSpace(_settings.SenderEmail) && !_settings.SenderEmail.EndsWith("@sims.com", StringComparison.OrdinalIgnoreCase)
                    ? _settings.SenderEmail
                    : "vijaymudaliyar224@gmail.com";
            }
            var senderName = !string.IsNullOrWhiteSpace(_settings.SenderName) ? _settings.SenderName : "SIMS Inventory System";

            var toList = new List<object> { new { email = recipientEmail.Trim() } };

            if (ccRecipients != null && ccRecipients.Any())
            {
                foreach (var cc in ccRecipients.Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c.Trim(), recipientEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    toList.Add(new { email = cc.Trim() });
                }
            }

            var payload = new
            {
                sender = new { name = senderName, email = senderEmail },
                to = toList,
                subject = subject,
                htmlContent = htmlContent
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            const int maxRetries = 3;
            int delayMs = 1000;
            string lastResponse = string.Empty;
            string lastError = string.Empty;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                    request.Headers.Add("api-key", apiKey.Trim());
                    request.Headers.Add("accept", "application/json");
                    request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    lastResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        string messageId = string.Empty;
                        try
                        {
                            using var doc = JsonDocument.Parse(lastResponse);
                            if (doc.RootElement.TryGetProperty("messageId", out var msgIdProp))
                            {
                                messageId = msgIdProp.GetString() ?? string.Empty;
                            }
                        }
                        catch {}

                        _logger.LogInformation("Brevo Email sent successfully to {Recipient}. MessageID: {MessageId}", recipientEmail, messageId);
                        return (true, messageId, lastResponse, string.Empty);
                    }

                    lastError = $"Brevo API returned HTTP {(int)response.StatusCode}: {lastResponse}";
                    _logger.LogWarning("Brevo Email send attempt {Attempt} failed: {Error}", attempt, lastError);
                }
                catch (Exception ex)
                {
                    lastError = $"Exception on attempt {attempt}: {ex.Message}";
                    _logger.LogWarning(ex, "Exception during Brevo Email send attempt {Attempt}", attempt);
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff: 1s, 2s, 4s
                }
            }

            return (false, string.Empty, lastResponse, lastError);
        }
    }
}
