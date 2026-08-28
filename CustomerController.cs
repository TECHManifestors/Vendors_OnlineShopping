using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;

namespace VendorShopOnline.Controllers
{
    [Authorize(Roles = RoleNames.Customer)]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var customer = await _context.Customers
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null) return NotFound();

            return View(customer);
        }
    }
}
