using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Shipped,
        Delivered,
        Cancelled,

        /// <summary>
        /// Added for the customer order-tracking feature. Deliberately
        /// appended at the end of the enum (rather than inserted in
        /// "logical" position between Shipped and Delivered) because EF
        /// Core stores enums as their underlying int by default — inserting
        /// a value in the middle would silently shift the stored meaning of
        /// every OrderStatus value after it for existing rows. Appending
        /// keeps every previously-persisted Status value valid.
        /// Represents "out for delivery / in transit to the customer",
        /// between Shipped and Delivered.
        /// </summary>
        InTransit
    }

    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(300)]
        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; } = string.Empty;

        /// <summary>
        /// Optional estimated delivery date shown to the customer on the
        /// order-tracking page. Nullable — existing orders (and any order
        /// where the vendor hasn't set an estimate) simply omit this in
        /// the UI, so this is a purely additive, backward-compatible column.
        /// </summary>
        [Display(Name = "Estimated Delivery")]
        public DateTime? EstimatedDeliveryDate { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        /// <summary>
        /// An order may have more than one payment attempt recorded
        /// (e.g. a failed attempt followed by a successful one), so this
        /// is a collection rather than a single nullable reference.
        /// </summary>
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        /// <summary>
        /// Chronological timeline of status changes for the customer order
        /// tracking page (Processing -> Shipped -> In Transit -> Delivered).
        /// New table, additive only — see OrderTrackingEvent.
        /// </summary>
        public ICollection<OrderTrackingEvent> TrackingEvents { get; set; } = new List<OrderTrackingEvent>();

        /// <summary>
        /// The delivery OTP most recently issued for this order (if any),
        /// used to confirm hand-over to the customer. See DeliveryOtp.
        /// </summary>
        public DeliveryOtp? DeliveryOtp { get; set; }

        /// <summary>
        /// Convenience flag: true once at least one Payment against this
        /// order has Status == Completed. Not mapped to a column — it is
        /// computed from the Payments collection at read time.
        /// </summary>
        [NotMapped]
        public bool IsPaid => Payments.Any(p => p.Status == PaymentStatus.Completed);

        /// <summary>
        /// Customer-friendly tracking label. Kept separate from the raw
        /// enum so the underlying OrderStatus values (and their stored
        /// integers) never need to change to satisfy new wording — both
        /// Pending and Confirmed read as "Processing" to a customer, since
        /// neither represents a physical hand-off yet.
        /// </summary>
        [NotMapped]
        public string TrackingLabel => Status switch
        {
            OrderStatus.Pending => "Processing",
            OrderStatus.Confirmed => "Processing",
            OrderStatus.Shipped => "Shipped",
            OrderStatus.InTransit => "In Transit",
            OrderStatus.Delivered => "Delivered",
            OrderStatus.Cancelled => "Cancelled",
            _ => Status.ToString()
        };

        /// <summary>
        /// Position of the current status along the 4-step customer-facing
        /// tracking timeline (0=Processing, 1=Shipped, 2=In Transit,
        /// 3=Delivered; -1=Cancelled, shown separately). Deliberately not
        /// the same as the enum's underlying int (see the comment on
        /// OrderStatus.InTransit) — this is presentation-only ordering.
        /// </summary>
        [NotMapped]
        public int TrackingStageIndex => Status switch
        {
            OrderStatus.Pending => 0,
            OrderStatus.Confirmed => 0,
            OrderStatus.Shipped => 1,
            OrderStatus.InTransit => 2,
            OrderStatus.Delivered => 3,
            OrderStatus.Cancelled => -1,
            _ => 0
        };
    }

    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }

        [Required]
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
    }
}
