using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;

namespace VendorShopOnline.Controllers
{
    [Authorize(Roles = RoleNames.Vendor)]
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VendorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var vendor = await _context.Vendors
                .Include(v => v.Products)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null) return NotFound();

            return View(vendor);
        }
    }
}
