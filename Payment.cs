using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VendorShopOnline.Models
{
    /// <summary>
    /// South African banks supported for EFT-style payment on this
    /// marketplace. Stored as an enum (not free text) so the value is
    /// constrained to a known set — avoids typos/invalid banks being
    /// persisted and keeps reporting/filtering simple.
    /// </summary>
    public enum SouthAfricanBank
    {
        [Display(Name = "Absa Bank")]
        Absa,

        [Display(Name = "Capitec Bank")]
        Capitec,

        [Display(Name = "First National Bank (FNB)")]
        Fnb,

        [Display(Name = "Nedbank")]
        Nedbank,

        [Display(Name = "Standard Bank")]
        StandardBank,

        [Display(Name = "TymeBank")]
        TymeBank,

        [Display(Name = "Discovery Bank")]
        DiscoveryBank,

        [Display(Name = "African Bank")]
        AfricanBank
    }

    public enum PaymentMethod
    {
        [Display(Name = "Bank / EFT")]
        BankEft,

        [Display(Name = "Debit Card")]
        DebitCard,

        [Display(Name = "Credit Card")]
        CreditCard
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded
    }

    /// <summary>
    /// Represents a payment made by a Customer against a specific Order.
    /// This model deliberately does NOT store full card numbers, CVVs, or
    /// online banking credentials — see the security note on CardLast4Digits
    /// below. In a real production system, actual payment processing would
    /// be delegated to a PCI-DSS compliant payment gateway (e.g. PayFast,
    /// Ozow, PayGate, or a bank's own hosted payment page); this model
    /// represents the marketplace's own record of that transaction, which
    /// is the academically correct and legally appropriate scope for a
    /// student project to implement.
    /// </summary>
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        [Display(Name = "Payment Method")]
        public PaymentMethod Method { get; set; }

        /// <summary>
        /// Only populated when Method == BankEft. Which South African bank
        /// the customer is paying from, for reference/reconciliation.
        /// </summary>
        [Display(Name = "Bank")]
        public SouthAfricanBank? Bank { get; set; }

        /// <summary>
        /// SECURITY: We store only the last 4 digits for on-screen
        /// reference (e.g. "Card ending 4321"), matching standard industry
        /// practice (PCI-DSS forbids storing full PANs, CVVs, or track
        /// data on merchant systems). The full card number is never
        /// captured or persisted by this application.
        /// </summary>
        [StringLength(4, MinimumLength = 4)]
        [Display(Name = "Card Ending In")]
        public string? CardLast4Digits { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than R0.00.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        /// <summary>
        /// Reference number shown to the customer and used to reconcile
        /// against bank statements — generated server-side, never editable.
        /// </summary>
        [StringLength(40)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}
