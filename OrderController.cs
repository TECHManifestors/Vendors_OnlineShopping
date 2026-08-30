using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;

namespace VendorShopOnline.Controllers
{
    /// <summary>
    /// Minimal order flow: a signed-in Customer buys a single product
    /// directly ("Buy Now"), which creates a Pending Order and immediately
    /// routes to the Payment checkout screen. A multi-item shopping cart is
    /// a natural future extension but is out of scope for this sprint —
    /// this keeps the Order -> Payment relationship fully demonstrable
    /// end-to-end without adding an entire cart subsystem.
    /// </summary>
    [Authorize(Roles = RoleNames.Customer)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Buy(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive);

            if (product == null) return NotFound();
            if (product.StockQuantity < 1)
            {
                TempData["ErrorMessage"] = "This product is currently out of stock.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive);
            if (product == null) return NotFound();

            if (quantity > product.StockQuantity)
            {
                TempData["ErrorMessage"] = $"Only {product.StockQuantity} unit(s) of this product are available.";
                return RedirectToAction(nameof(Buy), new { productId });
            }

            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Forbid();

            var order = new Order
            {
                CustomerId = customer.CustomerId,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                DeliveryAddress = customer.DeliveryAddress,
                TotalAmount = product.Price * quantity
            };

            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.ProductId,
                Quantity = quantity,
                UnitPrice = product.Price
            });

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("Checkout", "Payment", new { orderId = order.OrderId });
        }

        public async Task<IActionResult> MyOrders()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Forbid();

            var orders = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .Where(o => o.CustomerId == customer.CustomerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;
            return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        }
    }
}
