using System.ComponentModel.DataAnnotations;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// Product category. Kept as its own table (rather than a hard-coded
    /// enum) so categories can be managed and extended without a code
    /// change or migration — standard normalised design.
    /// </summary>
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// CSS class name used to render a category-appropriate icon/colour
        /// on a placeholder product card (see wwwroot/css/site.css).
        /// No external image is required.
        /// </summary>
        [StringLength(50)]
        public string IconClass { get; set; } = "icon-generic";

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
