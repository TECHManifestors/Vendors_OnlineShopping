using Microsoft.AspNetCore.Identity;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// Extends the built-in ASP.NET Core Identity user with VendorShop-specific
    /// fields. Identity already provides secure password hashing, lockout,
    /// two-factor scaffolding and token generation (used for password reset),
    /// so we do not reimplement any of that here.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Business role for this account. Kept as a simple string constant
        /// rather than a free-text field so it can double as an Identity Role
        /// name (see RoleNames) and be checked with User.IsInRole(...).
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

        // Navigation properties — exactly one of these will be populated
        // depending on which role the account was registered under.
        public Customer? Customer { get; set; }
        public Vendor? Vendor { get; set; }
    }

    /// <summary>
    /// Centralised role name constants to avoid "magic strings" scattered
    /// across controllers and views.
    /// </summary>
    public static class RoleNames
    {
        public const string Customer = "Customer";
        public const string Vendor = "Vendor";
        public const string Administrator = "Administrator";
    }
}
