using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;
using VendorShopOnline.ViewModels;

namespace VendorShopOnline.Controllers
{
    /// <summary>
    /// Handles the payment step of checkout. IMPORTANT SCOPE NOTE (also
    /// documented on the Payment model): this controller does not perform
    /// real money movement or talk to a bank/card network. It records the
    /// customer's stated payment method and marks the order paid, which is
    /// the correct and safe scope for an academic marketplace project.
    /// A production deployment would replace the "processing" step with a
    /// redirect to a PCI-DSS compliant payment gateway (e.g. PayFast, Ozow,
    /// PayGate, or Yoco) and would never collect raw card numbers directly
    /// as this demo form illustratively does.
    /// </summary>
    [Authorize(Roles = RoleNames.Customer)]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<PaymentController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int orderId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Forbid();

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customer.CustomerId);

            if (order == null) return NotFound();

            if (order.IsPaid)
            {
                return RedirectToAction(nameof(Confirmation), new { orderId = order.OrderId });
            }

            ViewBag.Order = order;
            var model = new PaymentViewModel
            {
                OrderId = order.OrderId,
                AmountDue = order.TotalAmount
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(PaymentViewModel model)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Forbid();

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == model.OrderId && o.CustomerId == customer.CustomerId);

            if (order == null) return NotFound();

            if (order.IsPaid)
            {
                return RedirectToAction(nameof(Confirmation), new { orderId = order.OrderId });
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Order = order;
                model.AmountDue = order.TotalAmount;
                return View(model);
            }

            var payment = new Payment
            {
                OrderId = order.OrderId,
                CustomerId = customer.CustomerId,
                Method = model.Method,
                Bank = model.Method == PaymentMethod.BankEft ? model.Bank : null,
                CardLast4Digits = model.Method is PaymentMethod.DebitCard or PaymentMethod.CreditCard && !string.IsNullOrEmpty(model.CardNumber)
                    ? model.CardNumber.Replace(" ", "").Substring(Math.Max(0, model.CardNumber.Replace(" ", "").Length - 4))
                    : null,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Completed, // Simulated success for demo/academic purposes — see class summary
                ReferenceNumber = GenerateReferenceNumber(order.OrderId),
                PaymentDate = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            order.Status = OrderStatus.Confirmed;

            // Deduct stock now that payment has been recorded.
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                if (product != null)
                {
                    product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                }
            }

            // Order-tracking feature: record this as the first entry on the
            // order's tracking timeline. Purely additive — does not change
            // the existing payment/order/stock logic above in any way.
            _context.OrderTrackingEvents.Add(new OrderTrackingEvent
            {
                OrderId = order.OrderId,
                Status = OrderStatus.Confirmed,
                Timestamp = DateTime.UtcNow,
                Notes = "Payment received. Your order is being processed."
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment {Reference} recorded for Order {OrderId} via {Method}.",
                payment.ReferenceNumber, order.OrderId, payment.Method);

            TempData["SuccessMessage"] = "Payment successful! Your order has been confirmed.";
            return RedirectToAction(nameof(Confirmation), new { orderId = order.OrderId });
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(int orderId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Forbid();

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customer.CustomerId);

            if (order == null) return NotFound();

            return View(order);
        }

        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;
            return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        private static string GenerateReferenceNumber(int orderId)
        {
            // Simple, human-readable, reasonably unique reference for
            // demo/reconciliation purposes: VSO-{orderId}-{short timestamp}.
            return $"VSO-{orderId:D6}-{DateTime.UtcNow:yyMMddHHmmss}";
        }
    }
}
