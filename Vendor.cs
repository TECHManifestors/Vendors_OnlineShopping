using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// Vendor profile data, linked 1:1 with an ApplicationUser (Identity) row.
    /// </summary>
    public class Vendor
    {
        [Key]
        public int VendorId { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Business name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 150 characters.")]
        [Display(Name = "Business Name")]
        public string BusinessName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^(\+27|0)[6-8][0-9]{8}$",
            ErrorMessage = "Please enter a valid South African phone number, e.g. 0821234567 or +27821234567.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Business address is required.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "Business address must be between 5 and 300 characters.")]
        [Display(Name = "Business Address")]
        public string BusinessAddress { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Business Description")]
        public string? Description { get; set; }

        public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Simple vendor approval workflow so an Administrator can vet new
        /// vendors before their products go live — a common, easy-to-explain
        /// requirement for a marketplace academic project.
        /// </summary>
        public bool IsApproved { get; set; } = false;

        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
