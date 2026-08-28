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
    /// Ordered-products reporting dashboard. New controller — there is no
    /// pre-existing reporting feature in this project to extend, so this
    /// is a pure addition alongside the existing Vendor/Customer/Order
    /// controllers, following the same "own data only" scoping already
    /// used elsewhere: a Vendor only ever sees their own products' sales;
    /// an Administrator (if one exists in the system) sees everything and
    /// can filter by vendor.
    /// </summary>
    [Authorize(Roles = RoleNames.Vendor + "," + RoleNames.Administrator)]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> OrderedProducts(
            DateTime? startDate, DateTime? endDate, int? productId, int? vendorId, OrderStatus? status)
        {
            var isAdmin = User.IsInRole(RoleNames.Administrator);
            int? scopedVendorId = null;

            if (!isAdmin)
            {
                var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == _userManager.GetUserId(User));
                if (vendor == null) return Forbid();
                scopedVendorId = vendor.VendorId; // vendors can never see another vendor's data
            }
            else if (vendorId.HasValue)
            {
                scopedVendorId = vendorId; // admin optionally narrows to one vendor
            }

            var itemsQuery = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product).ThenInclude(p => p!.Vendor)
                .Include(oi => oi.Product).ThenInclude(p => p!.Category)
                .Where(oi => oi.Order != null && oi.Product != null)
                .AsQueryable();

            if (scopedVendorId.HasValue)
                itemsQuery = itemsQuery.Where(oi => oi.Product!.VendorId == scopedVendorId.Value);

            if (startDate.HasValue)
                itemsQuery = itemsQuery.Where(oi => oi.Order!.OrderDate >= startDate.Value.Date);

            if (endDate.HasValue)
                itemsQuery = itemsQuery.Where(oi => oi.Order!.OrderDate < endDate.Value.Date.AddDays(1));

            if (productId.HasValue)
                itemsQuery = itemsQuery.Where(oi => oi.ProductId == productId.Value);

            if (status.HasValue)
                itemsQuery = itemsQuery.Where(oi => oi.Order!.Status == status.Value);

            var items = await itemsQuery.ToListAsync();

            var productRows = items
                .GroupBy(oi => oi.Product!)
                .Select(g => new ProductReportRow
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    VendorName = g.Key.Vendor?.BusinessName,
                    CategoryName = g.Key.Category?.Name,
                    QuantityOrdered = g.Sum(oi => oi.Quantity),
                    NumberOfSales = g.Select(oi => oi.OrderId).Distinct().Count(),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                    FirstOrderDate = g.Min(oi => oi.Order!.OrderDate),
                    LastOrderDate = g.Max(oi => oi.Order!.OrderDate)
                })
                .OrderByDescending(r => r.QuantityOrdered)
                .ToList();

            var customerRows = items
                .GroupBy(oi => oi.Order!.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderIds = g.Select(oi => oi.OrderId).Distinct(),
                    Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                    LastOrderDate = g.Max(oi => oi.Order!.OrderDate)
                })
                .ToList();

            var customerIds = customerRows.Select(c => c.CustomerId).ToList();
            var customerNames = await _context.Customers
                .Where(c => customerIds.Contains(c.CustomerId))
                .ToDictionaryAsync(c => c.CustomerId, c => c.FullName);

            var customerSummaries = customerRows
                .Select(c => new CustomerOrderSummaryRow
                {
                    CustomerId = c.CustomerId,
                    CustomerName = customerNames.TryGetValue(c.CustomerId, out var name) ? name : $"Customer #{c.CustomerId}",
                    OrderCount = c.OrderIds.Count(),
                    TotalSpent = c.Revenue,
                    LastOrderDate = c.LastOrderDate
                })
                .OrderByDescending(c => c.TotalSpent)
                .ToList();

            var availableProductsQuery = _context.Products.AsQueryable();
            if (scopedVendorId.HasValue)
                availableProductsQuery = availableProductsQuery.Where(p => p.VendorId == scopedVendorId.Value);

            var model = new OrderedProductsReportViewModel
            {
                Products = productRows,
                CustomerSummaries = customerSummaries,
                StartDate = startDate,
                EndDate = endDate,
                ProductId = productId,
                VendorId = vendorId,
                Status = status,
                IsAdministratorView = isAdmin,
                AvailableProducts = await availableProductsQuery.OrderBy(p => p.ProductName).ToListAsync(),
                AvailableVendors = isAdmin
                    ? await _context.Vendors.OrderBy(v => v.BusinessName).ToListAsync()
                    : new List<Vendor>()
            };

            return View(model);
        }
    }
}
