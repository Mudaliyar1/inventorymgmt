using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagementSystem.Interfaces;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Data;
using MongoDB.Driver;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.Extensions;

namespace InventoryManagementSystem.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly ILicenseService _licenseService;
        private readonly MongoDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IFeatureCatalogService _featureCatalogService;
        private readonly IPlatformSettingsService _platformSettingsService;
        private readonly ICustomPricingService _customPricingService;
        private readonly IContactInquiryService _contactInquiryService;
        private readonly IPolicyService _policyService;
        private readonly ISaaSPaymentService _saasPaymentService;
        private readonly IRazorpayPaymentService _razorpayPaymentService;

        public LandingController(
            ILicenseService licenseService,
            MongoDbContext context,
            IEmailService emailService,
            IFeatureCatalogService featureCatalogService,
            IPlatformSettingsService platformSettingsService,
            ICustomPricingService customPricingService,
            IContactInquiryService contactInquiryService,
            IPolicyService policyService,
            ISaaSPaymentService saasPaymentService,
            IRazorpayPaymentService razorpayPaymentService)
        {
            _licenseService = licenseService;
            _context = context;
            _emailService = emailService;
            _featureCatalogService = featureCatalogService;
            _platformSettingsService = platformSettingsService;
            _customPricingService = customPricingService;
            _contactInquiryService = contactInquiryService;
            _policyService = policyService;
            _saasPaymentService = saasPaymentService;
            _razorpayPaymentService = razorpayPaymentService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(Role.SuperAdmin))
                {
                    return RedirectToAction("Dashboard", "PlatformAdmin");
                }
                if (User.IsInRole(Role.Supplier))
                {
                    return RedirectToAction("Index", "SupplierDashboard");
                }
                return RedirectToAction("Index", "Home");
            }

            var packages = await _licenseService.GetPublicPackagesAsync();
            var settings = await _platformSettingsService.GetSettingsAsync();
            ViewBag.Packages = packages;
            ViewBag.Settings = settings;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Pricing()
        {
            var packages = await _licenseService.GetPublicPackagesAsync();
            var settings = await _platformSettingsService.GetSettingsAsync();
            var customPricingConfig = await _customPricingService.GetConfigurationAsync();

            ViewBag.GroupedFeatures = await _featureCatalogService.GetGroupedFeaturesAsync();
            ViewBag.AllFeatures = await _featureCatalogService.GetActiveFeaturesAsync();
            ViewBag.Settings = settings;
            ViewBag.CustomPricingConfig = customPricingConfig;
            return View(packages);
        }

        [HttpGet]
        public async Task<IActionResult> Features()
        {
            var settings = await _platformSettingsService.GetSettingsAsync();
            ViewBag.Settings = settings;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Contact()
        {
            var settings = await _platformSettingsService.GetSettingsAsync();
            ViewBag.Settings = settings;
            return View();
        }

        [HttpGet("policy/{slug}")]
        public async Task<IActionResult> Policy(string slug)
        {
            var settings = await _platformSettingsService.GetSettingsAsync();
            ViewBag.Settings = settings;

            var policy = await _policyService.GetBySlugAsync(slug, mustBePublishedAndActive: true);
            if (policy == null)
            {
                return NotFound();
            }

            var allPublic = await _policyService.GetPublicPoliciesAsync();
            ViewBag.AllPublicPolicies = allPublic;

            return View("Policy", policy);
        }

        [HttpGet]
        public async Task<IActionResult> PrivacyPolicy()
        {
            return await Policy("privacy-policy");
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return RedirectToAction(nameof(Policy), new { slug = "privacy-policy" });
        }

        [HttpGet]
        public async Task<IActionResult> Terms()
        {
            return await Policy("terms-and-conditions");
        }

        [HttpGet]
        public IActionResult TermsAndConditions()
        {
            return RedirectToAction(nameof(Policy), new { slug = "terms-and-conditions" });
        }

        [HttpGet]
        public async Task<IActionResult> RefundPolicy()
        {
            return await Policy("refund-policy");
        }

        [HttpGet]
        public async Task<IActionResult> SubscriptionPolicy()
        {
            return await Policy("subscription-policy");
        }

        [HttpGet]
        public async Task<IActionResult> CookiePolicy()
        {
            return await Policy("cookie-policy");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitCustomQuote([FromForm] InventoryManagementSystem.ViewModels.CustomQuoteInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            try
            {
                var config = await _customPricingService.GetConfigurationAsync();
                if (!config.IsEnabled || !config.AllowCustomQuotes)
                {
                    return Json(new { success = false, message = "Custom quote calculations are currently undergoing scheduled maintenance. Please reach out via our Contact desk." });
                }

                var allFeatures = await _featureCatalogService.GetActiveFeaturesAsync();
                var validKeys = new List<string>();
                var validNames = new List<string>();

                if (model.SelectedFeatureKeys != null)
                {
                    foreach (var key in model.SelectedFeatureKeys)
                    {
                        var feat = allFeatures.FirstOrDefault(f => string.Equals(f.FeatureKey, key, StringComparison.OrdinalIgnoreCase));
                        if (feat != null)
                        {
                            validKeys.Add(feat.FeatureKey);
                            validNames.Add(feat.FeatureName);
                        }
                    }
                }

                var (monthly, yearly, snapshotJson) = _customPricingService.CalculatePrice(
                    config,
                    model.AdminCount,
                    model.EmployeeCount,
                    model.ProductCount,
                    model.SupplierCount,
                    model.DeviceCount,
                    validKeys);

                var inquiry = new CustomPackageInquiry
                {
                    ContactName = model.ContactName.Trim(),
                    BusinessName = model.BusinessName.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    Phone = model.Phone?.Trim() ?? string.Empty,
                    Message = model.Message?.Trim() ?? string.Empty,
                    AdminCount = model.AdminCount,
                    EmployeeCount = model.EmployeeCount,
                    ProductCount = model.ProductCount,
                    SupplierCount = model.SupplierCount,
                    DeviceCount = model.DeviceCount,
                    SelectedFeatureKeys = validKeys,
                    SelectedFeatureNames = validNames,
                    EstimatedMonthlyPrice = monthly,
                    EstimatedYearlyPrice = yearly,
                    PricingConfigSnapshotJson = snapshotJson
                };

                var savedInquiry = await _customPricingService.CreateInquiryAsync(inquiry);

                // 1. Send Customer Confirmation Email
                try
                {
                    var (custSuccess, custErr) = await _emailService.SendCustomQuoteConfirmationEmailAsync(
                        savedInquiry.Email,
                        savedInquiry.ContactName,
                        savedInquiry.InquiryNumber);

                    await _customPricingService.UpdateConfirmationEmailStatusAsync(
                        savedInquiry.Id,
                        custSuccess ? "Sent" : "Failed",
                        custErr);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL NOTICE] Customer confirmation email exception: {ex.Message}");
                    await _customPricingService.UpdateConfirmationEmailStatusAsync(
                        savedInquiry.Id,
                        "Failed",
                        ex.Message);
                }

                // 2. Notify Platform Admin / Support Desk
                var settings = await _platformSettingsService.GetSettingsAsync();
                try
                {
                    var adminEmail = !string.IsNullOrWhiteSpace(settings.ContactEmail) ? settings.ContactEmail : "support@sims.com";
                    var subject = $"New Custom Package Quote Request — {savedInquiry.BusinessName} ({savedInquiry.InquiryNumber})";
                    var body = $@"
                        <div style='font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 25px; border-radius: 8px;'>
                            <h2 style='color: #38bdf8; margin-top:0;'>New Custom Package Inquiry Received</h2>
                            <p><strong>Reference:</strong> {savedInquiry.InquiryNumber}</p>
                            <p><strong>Business:</strong> {savedInquiry.BusinessName} | <strong>Contact:</strong> {savedInquiry.ContactName}</p>
                            <p><strong>Email:</strong> {savedInquiry.Email} | <strong>Phone:</strong> {savedInquiry.Phone}</p>
                            <hr style='border-color: #334155;'/>
                            <h4>Configured Quantities:</h4>
                            <ul>
                                <li>Administrators: {savedInquiry.AdminCount}</li>
                                <li>Employees: {savedInquiry.EmployeeCount}</li>
                                <li>Products: {savedInquiry.ProductCount:N0}</li>
                                <li>Suppliers: {savedInquiry.SupplierCount}</li>
                            </ul>
                            <h4>Selected Features ({validNames.Count}):</h4>
                            <p>{string.Join(", ", validNames)}</p>
                            <h4>Server-Calculated Baseline Estimate:</h4>
                            <p style='font-size: 18px; color: #4ade80;'><strong>Monthly:</strong> ₹{savedInquiry.EstimatedMonthlyPrice:N0} / mo | <strong>Yearly:</strong> ₹{savedInquiry.EstimatedYearlyPrice:N0} / yr</p>
                            <p><strong>Customer Message:</strong> {savedInquiry.Message}</p>
                        </div>";

                    await _emailService.SendEmailAsync(adminEmail, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL NOTICE] Custom quote admin notification email failed: {ex.Message}");
                }

                return Json(new
                {
                    success = true,
                    inquiryNumber = savedInquiry.InquiryNumber,
                    estimatedMonthly = savedInquiry.EstimatedMonthlyPrice,
                    estimatedYearly = savedInquiry.EstimatedYearlyPrice,
                    message = $"Thank you, {savedInquiry.ContactName}! We have received your custom package inquiry. Our team will review your requirements and contact you regarding the next steps."
                });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while saving your quote request. Please try again or contact support." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitContact([FromForm] InventoryManagementSystem.ViewModels.ContactInquiryInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            try
            {
                var inquiry = new ContactInquiry
                {
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    Phone = model.Phone?.Trim() ?? string.Empty,
                    BusinessName = model.BusinessName?.Trim() ?? string.Empty,
                    Subject = model.Subject.Trim(),
                    Message = model.Message.Trim()
                };

                var saved = await _contactInquiryService.CreateInquiryAsync(inquiry);

                // 1. Send Customer Confirmation Email
                try
                {
                    var (custSuccess, custErr) = await _emailService.SendContactInquiryConfirmationEmailAsync(
                        saved.Email,
                        saved.FullName,
                        saved.ReferenceNumber);

                    await _contactInquiryService.UpdateConfirmationEmailStatusAsync(
                        saved.Id,
                        custSuccess ? "Sent" : "Failed",
                        custErr);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL NOTICE] Contact customer confirmation email exception: {ex.Message}");
                    await _contactInquiryService.UpdateConfirmationEmailStatusAsync(
                        saved.Id,
                        "Failed",
                        ex.Message);
                }

                // 2. Notify support email
                var settings = await _platformSettingsService.GetSettingsAsync();
                try
                {
                    var supportEmail = !string.IsNullOrWhiteSpace(settings.ContactEmail) ? settings.ContactEmail : "support@sims.com";
                    var subject = $"New Contact Inquiry [{saved.ReferenceNumber}]: {saved.Subject}";
                    var body = $@"
                        <div style='font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; padding: 25px; border-radius: 8px;'>
                            <h2 style='color: #38bdf8; margin-top:0;'>New Support / Contact Desk Inquiry</h2>
                            <p><strong>Reference:</strong> {saved.ReferenceNumber}</p>
                            <p><strong>Sender:</strong> {saved.FullName} | <strong>Business:</strong> {saved.BusinessName}</p>
                            <p><strong>Email:</strong> {saved.Email} | <strong>Phone:</strong> {saved.Phone}</p>
                            <p><strong>Subject:</strong> {saved.Subject}</p>
                            <hr style='border-color: #334155;'/>
                            <p style='white-space: pre-wrap;'>{saved.Message}</p>
                        </div>";

                    await _emailService.SendEmailAsync(supportEmail, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL NOTICE] Contact admin notification email failed: {ex.Message}");
                }

                return Json(new
                {
                    success = true,
                    reference = saved.ReferenceNumber,
                    message = $"Thank you, {saved.FullName}! We have received your message. Our team will review your inquiry and get back to you as soon as possible."
                });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while saving your inquiry. Please try again." });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Maintenance()
        {
            var (isMaint, msg, endTime) = await _platformSettingsService.CheckWebsiteMaintenanceAsync("HomePage");
            if (!isMaint)
            {
                return RedirectToAction("Index", "Landing");
            }

            var settings = await _platformSettingsService.GetSettingsAsync();
            ViewBag.Settings = settings;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> MaintenanceStatus()
        {
            var (isMaint, msg, endTime) = await _platformSettingsService.CheckWebsiteMaintenanceAsync("HomePage");
            return Json(new { isInMaintenance = isMaint });
        }

        [HttpGet]
        public async Task<IActionResult> Register(string? packageId = null, string? mode = null, string? billingCycle = null)
        {
            ViewBag.Packages = await _licenseService.GetPublicPackagesAsync();
            ViewBag.SelectedPackageId = packageId;
            ViewBag.SelectedMode = mode ?? "trial";
            ViewBag.SelectedBillingCycle = billingCycle ?? "Monthly";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterShop(
            string shopName, string ownerName, string contactEmail, string phone,
            string address, string gstin, string packageId, string password,
            string? mode = "trial", string? billingCycle = "Monthly")
        {
            if (string.IsNullOrWhiteSpace(shopName) || string.IsNullOrWhiteSpace(contactEmail) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(ownerName))
            {
                TempData["ToastMessage"] = "Please fill in all required shop registration fields.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register), new { packageId });
            }

            contactEmail = contactEmail.Trim().ToLowerInvariant();

            // Check if user email already exists
            var existingUser = await _context.Users.Find(u => u.Email == contactEmail || u.Username == contactEmail).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                TempData["ToastMessage"] = "An account with this email address already exists. Please login instead.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("Login", "Account");
            }

            // Generate cryptographically secure 6-digit OTP
            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var hashedOtp = HashOtp(otp);

            // Clean up any stale pending registration for this email
            await _context.PendingRegistrations.DeleteManyAsync(p => p.ContactEmail == contactEmail);

            var pending = new PendingRegistration
            {
                RegistrationToken = Guid.NewGuid().ToString("N"),
                ContactEmail = contactEmail,
                HashedOtp = hashedOtp,
                ShopName = shopName.Trim(),
                OwnerName = ownerName.Trim(),
                Phone = phone?.Trim() ?? string.Empty,
                Address = address?.Trim() ?? string.Empty,
                GSTIN = gstin?.Trim() ?? string.Empty,
                PackageId = packageId ?? string.Empty,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RegistrationMode = mode ?? "trial",
                BillingCycle = billingCycle ?? "Monthly",
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
                AttemptCount = 0,
                MaxAttempts = 5,
                ResendCooldownUtc = DateTime.UtcNow.AddSeconds(60)
            };

            await _context.PendingRegistrations.InsertOneAsync(pending);

            // Send OTP verification email
            try
            {
                var (sent, error) = await _emailService.SendRegistrationOtpEmailAsync(contactEmail, ownerName, otp, 10);
                if (!sent)
                {
                    Console.WriteLine($"[OTP EMAIL WARNING] Email dispatch notice: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OTP EMAIL ERROR] {ex.Message}");
            }

            return RedirectToAction(nameof(VerifyRegistrationOtp), new { token = pending.RegistrationToken });
        }

        [HttpGet]
        public async Task<IActionResult> VerifyRegistrationOtp(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ToastMessage"] = "Invalid verification request. Please register again.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(Register));
            }

            var pending = await _context.PendingRegistrations.Find(p => p.RegistrationToken == token).FirstOrDefaultAsync();
            if (pending == null)
            {
                TempData["ToastMessage"] = "Verification session expired or not found. Please submit your registration details again.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register));
            }

            if (pending.ExpiresAtUtc < DateTime.UtcNow)
            {
                TempData["ToastMessage"] = "Your verification code has expired. Please request a new code.";
                TempData["ToastType"] = "warning";
            }

            var secondsUntilResend = (int)Math.Max(0, (pending.ResendCooldownUtc - DateTime.UtcNow).TotalSeconds);
            ViewBag.Token = token;
            ViewBag.ContactEmail = pending.ContactEmail;
            ViewBag.OwnerName = pending.OwnerName;
            ViewBag.ResendCooldownSeconds = secondsUntilResend;
            ViewBag.ExpiresAtUtc = pending.ExpiresAtUtc;

            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmRegistrationOtp(string token, string otp)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(otp))
            {
                TempData["ToastMessage"] = "Please enter the 6-digit verification code.";
                TempData["ToastType"] = "warning";
                return RedirectToAction(nameof(VerifyRegistrationOtp), new { token });
            }

            var pending = await _context.PendingRegistrations.Find(p => p.RegistrationToken == token).FirstOrDefaultAsync();
            if (pending == null)
            {
                TempData["ToastMessage"] = "Registration session expired. Please start over.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register));
            }

            if (pending.ExpiresAtUtc < DateTime.UtcNow)
            {
                TempData["ToastMessage"] = "Your verification code has expired. Please click 'Resend Code' below.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(VerifyRegistrationOtp), new { token });
            }

            if (pending.AttemptCount >= pending.MaxAttempts)
            {
                await _context.PendingRegistrations.DeleteOneAsync(p => p.Id == pending.Id);
                TempData["ToastMessage"] = "Too many failed attempts. For your security, this registration session has been closed. Please register again.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register));
            }

            var submittedHashedOtp = HashOtp(otp);
            if (!string.Equals(submittedHashedOtp, pending.HashedOtp, StringComparison.OrdinalIgnoreCase))
            {
                pending.AttemptCount++;
                await _context.PendingRegistrations.ReplaceOneAsync(p => p.Id == pending.Id, pending);

                var remaining = pending.MaxAttempts - pending.AttemptCount;
                TempData["ToastMessage"] = $"Incorrect verification code. Please check your email. {remaining} attempt(s) remaining.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(VerifyRegistrationOtp), new { token });
            }

            // OTP is Valid! Check once more if email was registered in the interim
            var existingUser = await _context.Users.Find(u => u.Email == pending.ContactEmail || u.Username == pending.ContactEmail).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                await _context.PendingRegistrations.DeleteOneAsync(p => p.Id == pending.Id);
                TempData["ToastMessage"] = "An account with this email address is already active. Please sign in.";
                TempData["ToastType"] = "info";
                return RedirectToAction("Login", "Account");
            }

            var package = await _context.SubscriptionPackages.Find(p => p.Id == pending.PackageId).FirstOrDefaultAsync();
            bool isDirectPaid = (pending.RegistrationMode == "paid") || (package != null && !package.HasTrial);

            if (isDirectPaid)
            {
                // Customer chose direct paid package — mark as verified and proceed straight to Razorpay Checkout
                pending.IsVerified = true;
                await _context.PendingRegistrations.ReplaceOneAsync(p => p.Id == pending.Id, pending);

                return RedirectToAction(nameof(Checkout), new { token = pending.RegistrationToken });
            }

            // Free-Trial Approval Workflow:
            // 1. Generate unique TenantCode
            var tenantCount = await _context.Tenants.CountDocumentsAsync(_ => true);
            var tenantCode = $"SHOP-{100 + tenantCount + 1}";

            var tenant = new Tenant
            {
                TenantCode = tenantCode,
                ShopName = pending.ShopName,
                OwnerName = pending.OwnerName,
                ContactEmail = pending.ContactEmail,
                Phone = pending.Phone,
                Address = pending.Address,
                GSTIN = pending.GSTIN,
                PackageId = pending.PackageId,
                Status = "PendingApproval", // Free trial requires admin approval before starting
                IsPaidCustomer = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Tenants.InsertOneAsync(tenant);

            // 2. Create Clean Shop Admin User
            var shopAdmin = new User
            {
                TenantId = tenant.Id,
                Username = pending.ContactEmail,
                Email = pending.ContactEmail,
                FullName = pending.OwnerName,
                PhoneNumber = pending.Phone,
                Role = Role.Admin,
                EmployeeId = "EMP-0001",
                IsLocked = false,
                PasswordHash = pending.PasswordHash,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            await _context.Users.InsertOneAsync(shopAdmin);

            // 3. Remove pending registration
            await _context.PendingRegistrations.DeleteOneAsync(p => p.Id == pending.Id);

            // 4. Send "Registration Pending Approval" Email
            try
            {
                await _emailService.SendTrialRegistrationPendingEmailAsync(tenant.ContactEmail, tenant.OwnerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL NOTICE] Registration pending approval email failed: {ex.Message}");
            }

            // 5. Sign in user so they are guided to UnderReview
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, shopAdmin.Id),
                new Claim(ClaimTypes.Name, shopAdmin.Username),
                new Claim(ClaimTypes.Email, shopAdmin.Email),
                new Claim(ClaimTypes.Role, shopAdmin.Role),
                new Claim("TenantId", tenant.Id),
                new Claim(ClaimTypes.GroupSid, tenant.Id),
                new Claim("FullName", shopAdmin.FullName),
                new Claim("EmployeeId", shopAdmin.EmployeeId)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            TempData["ToastMessage"] = "Your free trial registration has been received! Our onboarding team will review and approve your workspace.";
            TempData["ToastType"] = "info";

            return RedirectToAction("UnderReview", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegistrationOtp(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Json(new { success = false, message = "Invalid token." });
            }

            var pending = await _context.PendingRegistrations.Find(p => p.RegistrationToken == token).FirstOrDefaultAsync();
            if (pending == null)
            {
                return Json(new { success = false, message = "Registration session not found. Please register again." });
            }

            if (pending.ResendCooldownUtc > DateTime.UtcNow)
            {
                var remaining = (int)Math.Ceiling((pending.ResendCooldownUtc - DateTime.UtcNow).TotalSeconds);
                return Json(new { success = false, message = $"Please wait {remaining} seconds before requesting another code." });
            }

            // Generate new OTP
            var newOtp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            pending.HashedOtp = HashOtp(newOtp);
            pending.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
            pending.ResendCooldownUtc = DateTime.UtcNow.AddSeconds(60);
            pending.AttemptCount = 0; // reset attempt count on fresh OTP

            await _context.PendingRegistrations.ReplaceOneAsync(p => p.Id == pending.Id, pending);

            try
            {
                await _emailService.SendRegistrationOtpEmailAsync(pending.ContactEmail, pending.OwnerName, newOtp, 10);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OTP RESEND ERROR] {ex.Message}");
            }

            return Json(new { success = true, message = "A fresh 6-digit verification code has been dispatched to your email." });
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(string token, string? cycle = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction(nameof(Register));
            }

            var pending = await _context.PendingRegistrations.Find(p => p.RegistrationToken == token).FirstOrDefaultAsync();
            if (pending == null)
            {
                TempData["ToastMessage"] = "Registration session not found or expired. Please submit your registration details again.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register));
            }

            if (!pending.IsVerified)
            {
                return RedirectToAction(nameof(VerifyRegistrationOtp), new { token });
            }

            var package = await _context.SubscriptionPackages.Find(p => p.Id == pending.PackageId).FirstOrDefaultAsync();
            if (package == null)
            {
                TempData["ToastMessage"] = "The selected subscription package could not be found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register));
            }

            var billingCycle = !string.IsNullOrWhiteSpace(cycle) ? cycle : (pending.BillingCycle ?? "Monthly");
            pending.BillingCycle = billingCycle;
            await _context.PendingRegistrations.ReplaceOneAsync(p => p.Id == pending.Id, pending);

            // Initiate Razorpay Order via SaaSPaymentService
            var (initSuccess, txn, orderId, rzpKey, errorMsg) = await _saasPaymentService.InitiatePurchaseAsync(
                package.Id,
                billingCycle,
                tenantId: null,
                customerName: pending.OwnerName,
                customerEmail: pending.ContactEmail,
                customerPhone: pending.Phone,
                shopName: pending.ShopName,
                purpose: "NewPurchase",
                registrationToken: pending.RegistrationToken);

            if (!initSuccess || txn == null)
            {
                TempData["ToastMessage"] = $"Could not initiate payment: {errorMsg}";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Register), new { packageId = package.Id });
            }

            ViewBag.Transaction = txn;
            ViewBag.Package = package;
            ViewBag.Pending = pending;
            ViewBag.RazorpayKey = rzpKey;
            ViewBag.RazorpayOrderId = orderId;
            ViewBag.BillingCycle = billingCycle;

            return View(txn);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PaymentCallback(
            string razorpay_order_id,
            string razorpay_payment_id,
            string razorpay_signature,
            string registration_token)
        {
            if (string.IsNullOrWhiteSpace(razorpay_order_id) || string.IsNullOrWhiteSpace(razorpay_payment_id) || string.IsNullOrWhiteSpace(razorpay_signature))
            {
                TempData["ToastMessage"] = "Payment verification failed: missing payment signature details.";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Checkout), new { token = registration_token });
            }

            var (success, txn, errorMsg) = await _saasPaymentService.ProcessPaymentSuccessAsync(
                razorpay_order_id,
                razorpay_payment_id,
                razorpay_signature);

            if (!success || txn == null)
            {
                TempData["ToastMessage"] = $"Payment verification error: {errorMsg}";
                TempData["ToastType"] = "danger";
                return RedirectToAction(nameof(Checkout), new { token = registration_token });
            }

            // Find newly activated tenant or user
            User? shopAdmin = null;
            if (!string.IsNullOrEmpty(txn.TenantId))
            {
                shopAdmin = await _context.Users.Find(u => u.TenantId == txn.TenantId && u.Role == Role.Admin).FirstOrDefaultAsync();
            }

            if (shopAdmin == null && !string.IsNullOrEmpty(txn.CustomerEmail))
            {
                shopAdmin = await _context.Users.Find(u => (u.Email == txn.CustomerEmail || u.Username == txn.CustomerEmail) && u.Role == Role.Admin).FirstOrDefaultAsync();
            }

            if (shopAdmin != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, shopAdmin.Id),
                    new Claim(ClaimTypes.Name, shopAdmin.Username),
                    new Claim(ClaimTypes.Email, shopAdmin.Email),
                    new Claim(ClaimTypes.Role, shopAdmin.Role),
                    new Claim("TenantId", shopAdmin.TenantId ?? ""),
                    new Claim(ClaimTypes.GroupSid, shopAdmin.TenantId ?? ""),
                    new Claim("FullName", shopAdmin.FullName),
                    new Claim("EmployeeId", shopAdmin.EmployeeId ?? "EMP-0001"),
                    new Claim("ProfilePictureUrl", string.IsNullOrEmpty(shopAdmin.ProfilePictureUrl) ? "/images/default-avatar.png" : shopAdmin.ProfilePictureUrl)
                };

                if (shopAdmin.Permissions != null)
                {
                    foreach (var perm in shopAdmin.Permissions)
                    {
                        claims.Add(new Claim("Permission", perm));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(120)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                TempData["ToastMessage"] = "Payment verified successfully! Welcome to SIMS SaaS. Your subscription is active.";
                TempData["ToastType"] = "success";

                return RedirectToAction("Index", "Home");
            }

            TempData["ToastMessage"] = "Payment verified successfully! Your subscription is active. Please sign in.";
            TempData["ToastType"] = "success";

            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Landing/RazorpayWebhook")]
        [Route("api/payments/webhook")]
        [Route("api/razorpay/webhook")]
        [Route("razorpay/webhook")]
        public async Task<IActionResult> RazorpayWebhook()
        {
            try
            {
                using var reader = new System.IO.StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();
                var signatureHeader = Request.Headers["X-Razorpay-Signature"].ToString();

                var (success, message) = await _saasPaymentService.ProcessWebhookAsync(rawBody, signatureHeader);
                if (success)
                {
                    return Ok(new { status = "success", message });
                }
                return BadRequest(new { status = "error", message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RecordCheckoutCancellation([FromForm] string orderId, [FromForm] string? reason)
        {
            var (success, msg) = await _saasPaymentService.RecordCustomerCheckoutCancellationAsync(orderId, reason);
            return Json(new { success, message = msg });
        }

        private static string HashOtp(string otp)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(otp.Trim());
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
