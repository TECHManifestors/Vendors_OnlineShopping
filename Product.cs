using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace VendorShopOnline.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(150, MinimumLength = 2)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 100000, ErrorMessage = "Price must be between R0.01 and R100 000.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Stock quantity is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [Required]
        [ForeignKey(nameof(Category))]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required]
        [ForeignKey(nameof(Vendor))]
        public int VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Relative web path (e.g. "/images/products/{guid}.jpg") to the
        /// uploaded product image, or null if the vendor has not uploaded
        /// one — in which case views fall back to the existing category
        /// icon placeholder, exactly as before this feature was added.
        /// Nullable so existing rows created before this column existed
        /// remain valid (backward compatible schema change).
        /// </summary>
        [StringLength(260)]
        public string? ImagePath { get; set; }

        /// <summary>
        /// Not persisted — this is the form-bound file the vendor selects
        /// on Create/Edit. Kept on the entity (rather than a separate view
        /// model) to avoid touching the existing Create/Edit action
        /// signatures and views, matching the project's existing pattern of
        /// [NotMapped] computed/UI-only members (see Order.IsPaid).
        /// Validated and saved by IProductImageService; never mapped to a
        /// database column.
        /// </summary>
        [NotMapped]
        [Display(Name = "Product Image")]
        public IFormFile? ImageFile { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
