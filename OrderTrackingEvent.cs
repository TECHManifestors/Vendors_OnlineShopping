using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// One entry in an order's delivery timeline (e.g. "Order confirmed",
    /// "Shipped from vendor", "Out for delivery", "Delivered"). A new,
    /// purely additive table — it does not alter any existing table, and
    /// an order with no tracking events simply shows an empty timeline,
    /// so this never breaks orders created before this feature existed.
    /// </summary>
    public class OrderTrackingEvent
    {
        [Key]
        public int OrderTrackingEventId { get; set; }

        [Required]
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        public OrderStatus Status { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Short human-readable note shown on the tracking timeline, e.g.
        /// "Package handed to courier" or "Awaiting OTP confirmation from
        /// customer". Optional — a status change is still valid without one.
        /// </summary>
        [StringLength(300)]
        public string? Notes { get; set; }
    }
}
