using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;
using VendorShopOnline.Services;
using VendorShopOnline.ViewModels;

namespace VendorShopOnline.Controllers
{
    /// <summary>
    /// Handles all authentication concerns: registration for both Customer
    /// and Vendor roles, login, logout, and the full forgot-password /
    /// reset-password workflow.
    ///
    /// Password hashing, security stamps, and reset-token generation are
    /// all delegated to ASP.NET Core Identity (UserManager/SignInManager) —
    /// this controller never touches raw passwords beyond the initial
    /// model-bound input, and never stores or logs them.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        // ================= REGISTRATION: CUSTOMER =================

        [HttpGet]
        public IActionResult RegisterCustomer()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            return View(new CustomerRegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCustomer(CustomerRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                FullName = model.FullName,
                DateRegistered = DateTime.UtcNow
            };

            // UserManager.CreateAsync hashes the password internally
            // (PBKDF2 with a per-user salt) — the plain password is never
            // persisted anywhere.
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await EnsureRoleExistsAsync(RoleNames.Customer);
            await _userManager.AddToRoleAsync(user, RoleNames.Customer);

            var customer = new Customer
            {
                UserId = user.Id,
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                DeliveryAddress = model.DeliveryAddress,
                DateRegistered = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New customer account registered: {Email}", model.Email);

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = "Registration successful. Welcome to VendorShop Online!";
            return RedirectToAction("Index", "Home");
        }

        // ================= REGISTRATION: VENDOR =================

        [HttpGet]
        public IActionResult RegisterVendor()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            return View(new VendorRegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterVendor(VendorRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                FullName = model.BusinessName,
                DateRegistered = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await EnsureRoleExistsAsync(RoleNames.Vendor);
            await _userManager.AddToRoleAsync(user, RoleNames.Vendor);

            var vendor = new Vendor
            {
                UserId = user.Id,
                BusinessName = model.BusinessName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                BusinessAddress = model.BusinessAddress,
                Description = model.Description,
                DateRegistered = DateTime.UtcNow,
                IsApproved = false // Vendors require Administrator approval before listing products
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New vendor account registered: {Email}", model.Email);

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = "Registration successful. Your vendor account is pending approval.";
            return RedirectToAction("Index", "Home");
        }

        // ================= LOGIN / LOGOUT =================

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            // SignInManager enforces lockout after repeated failed attempts
            // (configured in Program.cs) to slow down brute-force attacks.
            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} logged in.", model.Email);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Email} account locked out due to repeated failed attempts.", model.Email);
                ModelState.AddModelError(string.Empty, "This account has been temporarily locked due to multiple failed login attempts. Please try again later.");
                return View(model);
            }

            // Deliberately generic message — do not reveal whether the
            // email exists or the password was wrong (enumeration defence).
            ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ================= FORGOT PASSWORD / RESET PASSWORD =================

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            // SECURITY: Always show the same confirmation page whether or
            // not the account exists. This prevents attackers from using
            // this form to enumerate registered email addresses.
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user) || true))
            {
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // Identity generates a cryptographically random, single-use,
            // time-limited token internally (default lifetime configured
            // in Program.cs). It is never stored in plain form by our code.
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var callbackUrl = Url.Action(
                action: nameof(ResetPassword),
                controller: "Account",
                values: new { email = user.Email, token = encodedToken },
                protocol: Request.Scheme);

            var htmlMessage = $@"
<p>Hello {user.FullName ?? "there"}</p>
<p>We received a request to reset your VendorShop Online password.</p>
<p>
<a href='{callbackUrl}'>
Click here to reset your password
</a>
</p>
<p>
This link will expire in 2 hours. If you did not request this, you can safely ignore this email.
</p>";
            try
            {
                await _emailSender.SendEmailAsync(user.Email!, "VendorShop Online - Password Reset", htmlMessage);
            }
            catch (Exception ex)
            {
                // Do not leak SMTP/config failures to the end user; log for
                // the developer and still show the generic confirmation.
                _logger.LogError(ex, "Failed to send password reset email to {Email}.", model.Email);
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet]
        public IActionResult ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction(nameof(Login));
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch
            {
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel { Email = email, Token = decodedToken };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Same generic confirmation regardless of outcome —
                // enumeration defence applies here too.
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            // Identity validates the token's signature and expiry itself;
            // an invalid or expired token simply fails here.
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Password successfully reset for {Email}.", model.Email);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation() => View();

        [HttpGet]
        public IActionResult AccessDenied() => View();

        // ================= PRIVATE HELPERS =================

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
