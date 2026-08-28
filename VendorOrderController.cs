using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;
using VendorShopOnline.Services;

namespace VendorShopOnline.Controllers
{
    /// <summary>
    /// Vendor-side order fulfilment: view orders containing this vendor's
    /// products, advance their status along the delivery lifecycle, and
    /// confirm final hand-over to the customer via OTP.
    ///
    /// New controller — it does not modify OrderController (which remains
    /// entirely Customer-facing) or any existing vendor workflow. A vendor
    /// only ever sees/affects orders that include at least one of their
    /// own products, mirroring the existing "own data only" pattern used
    /// throughout ProductController and VendorController.
    /// </summary>
    [Authorize(Roles = RoleNames.Vendor)]
    public class VendorOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDeliveryOtpService _otpService;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<VendorOrderController> _logger;

        public VendorOrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IDeliveryOtpService otpService,
            IEmailSender emailSender,
            ILogger<VendorOrderController> logger)
        {
            _context = context;
            _userManager = userManager;
            _otpService = otpService;
            _emailSender = emailSender;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ManageOrders()
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            // Only orders that are paid and contain at least one of this
            // vendor's products are actionable for fulfilment.
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.VendorId == vendor.VendorId)
                            && o.Payments.Any(p => p.Status == PaymentStatus.Completed))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        /// <summary>
        /// Advances an order to Shipped or InTransit. Delivered is
        /// deliberately excluded from this generic action — it can only be
        /// reached via OTP verification (see VerifyDelivery), so a
        /// completed delivery always has a customer-confirmed record.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus newStatus)
        {
            if (newStatus != OrderStatus.Shipped && newStatus != OrderStatus.InTransit)
            {
                TempData["ErrorMessage"] = "That status can't be set directly.";
                return RedirectToAction(nameof(ManageOrders));
            }

            var order = await GetVendorOrderAsync(orderId);
            if (order == null) return NotFound();

            order.Status = newStatus;

            var note = newStatus == OrderStatus.Shipped
                ? "Order has been shipped by the vendor."
                : "Order is out for delivery.";

            _context.OrderTrackingEvents.Add(new OrderTrackingEvent
            {
                OrderId = order.OrderId,
                Status = newStatus,
                Timestamp = DateTime.UtcNow,
                Notes = note
            });

            await _context.SaveChangesAsync();

            // When the order goes out for delivery, generate and email the
            // delivery OTP the customer will need to hand to the courier.
            if (newStatus == OrderStatus.InTransit)
            {
                await GenerateAndSendOtpAsync(order);
            }

            TempData["SuccessMessage"] = $"Order #{order.OrderId} marked as {order.TrackingLabel}.";
            return RedirectToAction(nameof(ManageOrders));
        }

        /// <summary>
        /// Resends a fresh OTP for an order already InTransit — useful if
        /// the original expired or the customer didn't receive it. This
        /// invalidates the previous OTP (DeliveryOtpService overwrites it).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(int orderId)
        {
            var order = await GetVendorOrderAsync(orderId);
            if (order == null) return NotFound();

            if (order.Status != OrderStatus.InTransit)
            {
                TempData["ErrorMessage"] = "An OTP can only be (re)sent while the order is out for delivery.";
                return RedirectToAction(nameof(ManageOrders));
            }

            await GenerateAndSendOtpAsync(order);
            TempData["SuccessMessage"] = $"A new delivery OTP has been sent for Order #{order.OrderId}.";
            return RedirectToAction(nameof(ManageOrders));
        }

        [HttpGet]
        public async Task<IActionResult> VerifyDelivery(int orderId)
        {
            var order = await GetVendorOrderAsync(orderId);
            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyDelivery(int orderId, string code)
        {
            var order = await GetVendorOrderAsync(orderId);
            if (order == null) return NotFound();

            if (string.IsNullOrWhiteSpace(code))
            {
                ModelState.AddModelError(string.Empty, "Please enter the OTP provided by the customer.");
                return View(order);
            }

            var result = await _otpService.VerifyAsync(orderId, code);

            switch (result)
            {
                case OtpVerificationResult.Success:
                    order.Status = OrderStatus.Delivered;
                    _context.OrderTrackingEvents.Add(new OrderTrackingEvent
                    {
                        OrderId = order.OrderId,
                        Status = OrderStatus.Delivered,
                        Timestamp = DateTime.UtcNow,
                        Notes = "Delivery confirmed by OTP."
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Order #{order.OrderId} confirmed as delivered.";
                    return RedirectToAction(nameof(ManageOrders));

                case OtpVerificationResult.InvalidCode:
                    ModelState.AddModelError(string.Empty, "That code is incorrect. Please check with the customer and try again.");
                    break;
                case OtpVerificationResult.Expired:
                    ModelState.AddModelError(string.Empty, "This OTP has expired. Use \"Resend OTP\" from Manage Orders to issue a new one.");
                    break;
                case OtpVerificationResult.Locked:
                    ModelState.AddModelError(string.Empty, "Too many incorrect attempts. Use \"Resend OTP\" from Manage Orders to issue a new one.");
                    break;
                case OtpVerificationResult.AlreadyVerified:
                    ModelState.AddModelError(string.Empty, "This order's delivery has already been confirmed.");
                    break;
                case OtpVerificationResult.NotFound:
                default:
                    ModelState.AddModelError(string.Empty, "No active OTP was found for this order. Mark it as \"Out for delivery\" first.");
                    break;
            }

            return View(order);
        }

        // ================= PRIVATE HELPERS =================

        private async Task GenerateAndSendOtpAsync(Order order)
        {
            var customerEmail = order.Customer?.Email;
            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                // Order was loaded without Customer included somewhere — reload defensively.
                var reloaded = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderId == order.OrderId);
                customerEmail = reloaded?.Customer?.Email;
            }

            var code = await _otpService.GenerateAsync(order);

            if (!string.IsNullOrWhiteSpace(customerEmail))
            {
                var html = $@"
                    <p>Hello,</p>
                    <p>Your VendorShop Online order <strong>#{order.OrderId}</strong> is out for delivery.</p>
                    <p>Please give the delivery person this one-time code to confirm you have received your order:</p>
                    <p style='font-size:1.6rem; font-weight:bold; letter-spacing:0.2rem;'>{code}</p>
                    <p>This code expires in 15 minutes and can only be used once.</p>
                    <p>Do not share this code with anyone except the person delivering your order.</p>";

                try
                {
                    await _emailSender.SendEmailAsync(customerEmail, "VendorShop Online - Your Delivery Code", html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send delivery OTP email for Order {OrderId}.", order.OrderId);
                }
            }
            else
            {
                _logger.LogWarning("No customer email on file for Order {OrderId}; delivery OTP was generated but not emailed.", order.OrderId);
            }
        }

        private async Task<Vendor?> GetCurrentVendorAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;
            return await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        }

        /// <summary>
        /// Loads an order, enforcing that it is paid and contains at least
        /// one product belonging to the currently signed-in vendor.
        /// </summary>
        private async Task<Order?> GetVendorOrderAsync(int orderId)
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return null;

            return await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId
                    && o.OrderItems.Any(oi => oi.Product != null && oi.Product.VendorId == vendor.VendorId)
                    && o.Payments.Any(p => p.Status == PaymentStatus.Completed));
        }
    }
}
