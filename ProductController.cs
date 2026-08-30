using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;

namespace VendorShopOnline.Controllers
{
    /// <summary>
    /// Public product browsing plus Vendor-only CRUD for their own listings.
    /// Demonstrates full Create / Read / Update / Delete against SQLite via
    /// EF Core, as required by the assessment rubric.
    /// </summary>
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ---------- Public browsing (Read) ----------

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.SelectedCategoryId = categoryId;

            var products = await query.OrderByDescending(p => p.DateCreated).ToListAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();
            return View(product);
        }

        // ---------- Vendor-only CRUD ----------

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(new Product());
        }

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            ModelState.Remove(nameof(Product.Vendor));
            ModelState.Remove(nameof(Product.Category));

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                return View(model);
            }

            model.VendorId = vendor.VendorId;
            model.DateCreated = DateTime.UtcNow;
            model.IsActive = true;

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{model.ProductName}' was created successfully.";
            return RedirectToAction(nameof(MyProducts));
        }

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpGet]
        public async Task<IActionResult> MyProducts()
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.VendorId == vendor.VendorId)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();

            return View(products);
        }

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.VendorId == vendor.VendorId);
            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(product);
        }

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model)
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.VendorId == vendor.VendorId);
            if (product == null) return NotFound();

            ModelState.Remove(nameof(Product.Vendor));
            ModelState.Remove(nameof(Product.Category));

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                return View(model);
            }

            product.ProductName = model.ProductName;
            product.Description = model.Description;
            product.Price = model.Price;
            product.StockQuantity = model.StockQuantity;
            product.CategoryId = model.CategoryId;
            product.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Product '{product.ProductName}' was updated successfully.";
            return RedirectToAction(nameof(MyProducts));
        }

        [Authorize(Roles = RoleNames.Vendor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var vendor = await GetCurrentVendorAsync();
            if (vendor == null) return Forbid();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.VendorId == vendor.VendorId);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' was deleted.";
            return RedirectToAction(nameof(MyProducts));
        }

        private async Task<Vendor?> GetCurrentVendorAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;
            return await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        }
    }
}
