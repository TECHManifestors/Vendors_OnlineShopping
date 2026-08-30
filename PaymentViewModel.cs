using System.ComponentModel.DataAnnotations;
using VendorShopOnline.Models;

namespace VendorShopOnline.ViewModels
{
    /// <summary>
    /// Captured on the checkout/payment form. Implements IValidatableObject
    /// to apply conditional validation: a Bank selection is required only
    /// when the customer chooses Bank/EFT, and card-ending digits are
    /// required only when they choose a card method. This keeps the
    /// underlying Payment entity free of UI-specific validation branching.
    /// </summary>
    public class PaymentViewModel : IValidatableObject
    {
        public int OrderId { get; set; }

        [Display(Name = "Order Total")]
        public decimal AmountDue { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        [Display(Name = "Payment Method")]
        public PaymentMethod Method { get; set; }

        [Display(Name = "Select Your Bank")]
        public SouthAfricanBank? Bank { get; set; }

        [Display(Name = "Card Number")]
        [CreditCard(ErrorMessage = "Please enter a valid card number.")]
        public string? CardNumber { get; set; }

        [Display(Name = "Expiry (MM/YY)")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Enter expiry as MM/YY.")]
        public string? CardExpiry { get; set; }

        [Display(Name = "CVV")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
        public string? CardCvv { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Method == PaymentMethod.BankEft && Bank == null)
            {
                yield return new ValidationResult(
                    "Please select which South African bank you are paying from.",
                    new[] { nameof(Bank) });
            }

            if (Method is PaymentMethod.DebitCard or PaymentMethod.CreditCard)
            {
                if (string.IsNullOrWhiteSpace(CardNumber))
                {
                    yield return new ValidationResult("Card number is required.", new[] { nameof(CardNumber) });
                }
                if (string.IsNullOrWhiteSpace(CardExpiry))
                {
                    yield return new ValidationResult("Card expiry date is required.", new[] { nameof(CardExpiry) });
                }
                if (string.IsNullOrWhiteSpace(CardCvv))
                {
                    yield return new ValidationResult("CVV is required.", new[] { nameof(CardCvv) });
                }
            }
        }
    }
}
