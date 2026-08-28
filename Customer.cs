using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// Customer profile data. This table is linked 1:1 with an
    /// ApplicationUser (Identity) row via UserId. Identity owns the
    /// email/password/security-stamp concerns; this table owns the
    /// customer-specific business data.
    /// </summary>
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [RegularExpression(@"^(\+27|0)[6-8][0-9]{8}$",
            ErrorMessage = "Please enter a valid South African phone number, e.g. 0821234567 or +27821234567.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery address is required.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Delivery address must be between 5 and 300 characters.")]
        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; } = string.Empty;

        public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
