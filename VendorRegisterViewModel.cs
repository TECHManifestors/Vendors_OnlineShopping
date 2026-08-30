using System.ComponentModel.DataAnnotations;

namespace VendorShopOnline.ViewModels
{
    public class VendorRegisterViewModel
    {
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

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
