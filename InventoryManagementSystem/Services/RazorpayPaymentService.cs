using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventoryManagementSystem.Configuration;
using InventoryManagementSystem.Interfaces;
using Razorpay.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class RazorpayPaymentService : IRazorpayPaymentService
    {
        private readonly RazorpaySettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RazorpayPaymentService> _logger;

        public RazorpayPaymentService(
            IOptions<RazorpaySettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<RazorpayPaymentService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string GetKeyId() => _settings.KeyId ?? string.Empty;
        public string GetCurrency() => !string.IsNullOrWhiteSpace(_settings.Currency) ? _settings.Currency : "INR";
        public bool IsConfigured() => _settings.IsEnabled;

        public Task<(bool Success, string OrderId, string ErrorMessage)> CreateOrderAsync(
            string transactionRef, 
            decimal amountInInr, 
            Dictionary<string, string>? notes = null)
        {
            if (amountInInr <= 0)
            {
                return Task.FromResult((false, string.Empty, "Payment amount must be greater than zero."));
            }

            // Always calculate in integer paise on the server
            long amountInPaise = (long)Math.Round(amountInInr * 100m, MidpointRounding.AwayFromZero);

            if (!_settings.IsEnabled)
            {
                // Fallback for development/test mode if keys not yet supplied in environment
                var mockOrderId = $"order_mock_{Guid.NewGuid():N}".Substring(0, 24);
                _logger.LogWarning("[RAZORPAY] Razorpay credentials not configured. Generated test mock order: {OrderId}", mockOrderId);
                return Task.FromResult((true, mockOrderId, string.Empty));
            }

            try
            {
                var client = new RazorpayClient(_settings.KeyId, _settings.KeySecret);

                var orderData = new Dictionary<string, object>
                {
                    { "amount", amountInPaise },
                    { "currency", GetCurrency() },
                    { "receipt", transactionRef },
                    { "payment_capture", 1 }
                };

                if (notes != null && notes.Count > 0)
                {
                    orderData.Add("notes", notes);
                }

                Order order = client.Order.Create(orderData);
                string orderId = order["id"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(orderId))
                {
                    return Task.FromResult((false, string.Empty, "Razorpay API did not return an order ID."));
                }

                _logger.LogInformation("[RAZORPAY] Created Razorpay order {OrderId} for transaction {TxnRef} with amount {Paise} paise.", 
                    orderId, transactionRef, amountInPaise);

                return Task.FromResult((true, orderId, string.Empty));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RAZORPAY] Failed to create order for {TxnRef}: {Message}", transactionRef, ex.Message);
                return Task.FromResult((false, string.Empty, $"Razorpay order generation failed: {ex.Message}"));
            }
        }

        public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
        {
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            // In dev mode when using mock order
            if (!_settings.IsEnabled && orderId.StartsWith("order_mock_"))
            {
                return true;
            }

            try
            {
                string payload = $"{orderId}|{paymentId}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.KeySecret));
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                string generatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                byte[] expectedBytes = Encoding.UTF8.GetBytes(generatedSignature);
                byte[] actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());

                return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RAZORPAY] Error verifying payment signature for order {OrderId}: {Message}", orderId, ex.Message);
                return false;
            }
        }

        public bool VerifyWebhookSignature(string rawBody, string signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            var secretsToTry = new List<string>();
            if (!string.IsNullOrWhiteSpace(_settings.WebhookSecret))
            {
                secretsToTry.Add(_settings.WebhookSecret.Trim());
            }
            if (!string.IsNullOrWhiteSpace(_settings.KeySecret) && !secretsToTry.Contains(_settings.KeySecret.Trim()))
            {
                secretsToTry.Add(_settings.KeySecret.Trim());
            }

            if (secretsToTry.Count == 0)
            {
                _logger.LogWarning("[RAZORPAY] Cannot verify webhook signature: No WebhookSecret or KeySecret is configured.");
                return false;
            }

            byte[] actualBytes = Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant());

            foreach (var sec in secretsToTry)
            {
                try
                {
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sec));
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
                    string generatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    byte[] expectedBytes = Encoding.UTF8.GetBytes(generatedSignature);
                    if (CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RAZORPAY] Error verifying webhook signature with secret: {Message}", ex.Message);
                }
            }

            return false;
        }

        public Task<(bool Success, string Status, decimal AmountInInr, string ErrorMessage)> FetchPaymentDetailsAsync(string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                return Task.FromResult((false, string.Empty, 0m, "Payment ID is required."));
            }

            if (!_settings.IsEnabled && paymentId.StartsWith("pay_mock_"))
            {
                return Task.FromResult((true, "captured", 0m, string.Empty));
            }

            try
            {
                var client = new RazorpayClient(_settings.KeyId, _settings.KeySecret);
                Payment payment = client.Payment.Fetch(paymentId);

                string status = payment["status"]?.ToString() ?? "unknown";
                long amountPaise = 0;
                var amountObj = payment["amount"];
                if (amountObj != null)
                {
                    long.TryParse(amountObj.ToString(), out amountPaise);
                }
                decimal amountInr = amountPaise / 100m;

                return Task.FromResult((true, status, amountInr, string.Empty));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RAZORPAY] Failed to fetch payment details for {PaymentId}: {Message}", paymentId, ex.Message);
                return Task.FromResult((false, string.Empty, 0m, ex.Message));
            }
        }

        public async Task<(bool Success, string PaymentId, string Status, decimal AmountInInr, string Currency, string ErrorMessage)> FetchOrderPaymentsAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return (false, string.Empty, string.Empty, 0m, string.Empty, "Order ID is required.");
            }

            if (!_settings.IsEnabled && orderId.StartsWith("order_mock_"))
            {
                return (true, "pay_mock_" + Guid.NewGuid().ToString("N").Substring(0, 14), "captured", 1m, "INR", string.Empty);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.KeyId}:{_settings.KeySecret}"));
                var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"https://api.razorpay.com/v1/orders/{orderId}/payments");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[RAZORPAY] Orders payments API returned HTTP {StatusCode} for {OrderId}: {Body}", response.StatusCode, orderId, errBody);
                    return (false, string.Empty, string.Empty, 0m, string.Empty, $"Razorpay API returned {response.StatusCode}: {errBody}");
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    JsonElement? capturedPayment = null;
                    JsonElement? anyPayment = null;

                    foreach (var item in items.EnumerateArray())
                    {
                        var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                        if (status.Equals("captured", StringComparison.OrdinalIgnoreCase) || status.Equals("paid", StringComparison.OrdinalIgnoreCase))
                        {
                            capturedPayment = item;
                            break;
                        }
                        if (capturedPayment == null && status.Equals("authorized", StringComparison.OrdinalIgnoreCase))
                        {
                            capturedPayment = item;
                        }
                        anyPayment ??= item;
                    }

                    var selected = capturedPayment ?? anyPayment;
                    if (selected.HasValue)
                    {
                        var pay = selected.Value;
                        string payId = pay.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string payStatus = pay.TryGetProperty("status", out var stProp) ? stProp.GetString() ?? "" : "";
                        string currency = pay.TryGetProperty("currency", out var curProp) ? curProp.GetString() ?? "INR" : "INR";
                        long amountPaise = 0;
                        if (pay.TryGetProperty("amount", out var amtProp))
                        {
                            if (amtProp.ValueKind == JsonValueKind.Number)
                            {
                                amtProp.TryGetInt64(out amountPaise);
                            }
                            else if (amtProp.ValueKind == JsonValueKind.String)
                            {
                                long.TryParse(amtProp.GetString(), out amountPaise);
                            }
                        }
                        decimal amountInr = amountPaise / 100m;

                        return (true, payId, payStatus, amountInr, currency, string.Empty);
                    }
                }

                return (false, string.Empty, "no_payments_found", 0m, string.Empty, "No payments found for this order on Razorpay.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RAZORPAY] Failed to fetch order payments for {OrderId}: {Message}", orderId, ex.Message);
                return (false, string.Empty, string.Empty, 0m, string.Empty, ex.Message);
            }
        }
    }
}
