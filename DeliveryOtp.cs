using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// One-time password used to confirm hand-over of an order to the
    /// customer at the point of delivery. A new, additive table — it has
    /// no effect on any existing order, payment, or product data.
    ///
    /// SECURITY: the plain-text OTP is never persisted. Only a salted hash
    /// (produced by ASP.NET Core Identity's PasswordHasher, the same
    /// primitive already used elsewhere in this project for account
    /// passwords) is stored, together with an expiry time and a bounded
    /// attempt counter, so the OTP cannot be brute-forced or recovered
    /// from the database. Exactly one row exists per Order (OrderId is
    /// unique) — requesting a new OTP for the same order overwrites the
    /// previous one, invalidating it immediately.
    /// </summary>
    public class DeliveryOtp
    {
        [Key]
        public int DeliveryOtpId { get; set; }

        [Required]
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        /// <summary>
        /// Salted hash of the 6-digit OTP (never the plain code).
        /// </summary>
        [Required]
        [StringLength(400)]
        public string CodeHash { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The OTP is rejected once this time has passed, even if the
        /// digits are otherwise correct.
        /// </summary>
        [Required]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Incremented on every failed verification attempt. Once this
        /// reaches MaxAttempts, the OTP is locked and a new one must be
        /// generated — this bounds brute-force guessing of the 6-digit code.
        /// </summary>
        public int AttemptsMade { get; set; } = 0;

        public const int MaxAttempts = 5;

        public bool IsVerified { get; set; } = false;

        public DateTime? VerifiedAt { get; set; }

        [NotMapped]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        [NotMapped]
        public bool IsLocked => AttemptsMade >= MaxAttempts;
    }
}
