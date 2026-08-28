# VendorShop Online

An ASP.NET Core 8 MVC marketplace application connecting South African
vendors and customers, built with Entity Framework Core and SQLite for the
APDP201 Web Application Project (Sprint One).

## Technology Stack

- ASP.NET Core 8 MVC (C#)
- Entity Framework Core 8 + SQLite
- ASP.NET Core Identity (authentication, password hashing, password reset)
- Razor Views, vanilla CSS3 (responsive, no external UI framework)
- No external product images — category/product cards use CSS/SVG icons

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- The `dotnet-ef` global tool (see below)

Check your SDK version:

```bash
dotnet --version
```

Install the EF Core CLI tool if you don't already have it:

```bash
dotnet tool install --global dotnet-ef
```

## First-Time Setup

1. **Restore packages**

   ```bash
   cd VendorShopOnline
   dotnet restore
   ```

2. **Create the initial migration**

   The project ships without a `Migrations/` folder populated (deliberately —
   migrations should be generated against your actual SDK/EF Core tool
   version rather than committed pre-built). Generate it:

   ```bash
   dotnet ef migrations add InitialCreate
   ```

3. **Apply the migration to create the SQLite database**

   ```bash
   dotnet ef database update
   ```

   This creates `vendorshop.db` in the project root, containing:
   - Identity tables: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`,
     `AspNetUserClaims`, `AspNetUserTokens` (used for password reset tokens),
     `AspNetUserLogins`, `AspNetRoleClaims`
   - Domain tables: `Customers`, `Vendors`, `Categories`, `Products`,
     `Orders`, `OrderItems`, `Payments`

   > Note: `Program.cs` also calls `dbContext.Database.Migrate()` on
   > startup, so `dotnet run` will apply any pending migrations
   > automatically. Running `dotnet ef database update` manually the first
   > time is still recommended so you can see the SQL and confirm the
   > schema before running the app.

4. **Run the application**

   ```bash
   dotnet run
   ```

   By default this serves on `https://localhost:5001` (or check the
   console output for the exact port). Navigate there in your browser.

## Email / Forgot Password in Development

No real SMTP server is required to demonstrate the forgot-password flow.
If `EmailSettings:SmtpHost` is left blank in `appsettings.json` (the
default), the app uses `LoggingEmailSender`, which writes the full reset
email — including the working reset link — to the console/log output
instead of sending real mail. Watch the terminal after submitting the
Forgot Password form, copy the link, and open it in the browser to
complete the reset flow end-to-end.

To send real email (optional, for a live demo), fill in `EmailSettings` in
`appsettings.json` (or better, via `dotnet user-secrets` so credentials
aren't committed):

```bash
dotnet user-secrets init
dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:Username" "your-address@gmail.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password"
dotnet user-secrets set "EmailSettings:FromAddress" "your-address@gmail.com"
```

## Test Accounts (create these yourself)

There is no seeded user data — register through the UI:

- **Customer**: `/Account/RegisterCustomer`
- **Vendor**: `/Account/RegisterVendor`

A newly registered Vendor account is created with `IsApproved = false`.
This is a deliberate marketplace-integrity feature (an Administrator would
approve vendors in a later sprint); it does not block a vendor from adding
products or the demo from working, it's simply visible on their dashboard.

## Payment Feature (South African Banks)

Customers pay for orders via the `/Payment/Checkout` flow, reached after
clicking **Buy Now** on any product (Customer role required). Supported
methods:

- **Bank / EFT** — select from Absa, Capitec, FNB, Nedbank, Standard Bank,
  TymeBank, Discovery Bank, or African Bank
- **Debit Card** / **Credit Card**

**Scope note:** this is a marketplace's own transaction record, not a real
payment gateway integration. No real money moves, and — importantly — no
full card number, CVV, or online banking credentials are ever persisted;
only the last 4 digits of a card are stored for on-screen reference,
consistent with PCI-DSS practice. A production system would redirect to a
licensed South African payment gateway (e.g. PayFast, Ozow, PayGate, Yoco)
at the point this demo simulates a completed payment. This scope and
reasoning is documented in code comments in `Models/Payment.cs` and
`Controllers/PaymentController.cs`.

## New Features (Sprint Two Enhancements)

Five features were added on top of the original Sprint One system without
changing any existing behaviour. Full detail, database changes, and testing
steps are in `FEATURE_SUMMARY.md` at the repository root; short version:

1. **Product Image Upload** — vendors can attach a JPG/PNG/WEBP image
   (≤5 MB) when creating or editing a product. Falls back to the original
   category-icon placeholder when no image is set.
2. **Customer Order Tracking** — `/Order/Track/{orderId}` shows a
   Processing → Shipped → In Transit → Delivered timeline, order contents,
   and (if set) an estimated delivery date. Linked from *My Orders*.
3. **Ordered Products Report** — `/Report/OrderedProducts` (Vendor/
   Administrator only) with date range, product, vendor (admin), and status
   filters; shows per-product sales totals and a customer-orders summary.
4. **Delivery OTP Verification** — when a vendor marks an order "out for
   delivery" a 6-digit one-time code is emailed to the customer (via the
   existing email pipeline); the vendor enters it at `/VendorOrder/
   VerifyDelivery/{orderId}` to mark the order Delivered. Codes are hashed,
   expire after 15 minutes, and lock out after 5 incorrect attempts.
5. **Product Search** — the Products page now has a search box matching
   product name, description, category, and vendor, combinable with the
   existing category filter.

Because Migrations/ is not committed (see "First-Time Setup" above), running
`dotnet ef migrations add <Name>` after pulling these changes will pick up
all of the new columns/tables automatically — no manual SQL is required.

## Database Architecture Summary

```
ApplicationUser (Identity) 1---1 Customer 1---* Order 1---* OrderItem *---1 Product *---1 Category
ApplicationUser (Identity) 1---1 Vendor   1---* Product
Order 1---* Payment *---1 Customer
```

## Common Issues

| Symptom | Fix |
|---|---|
| `dotnet ef` command not found | Run `dotnet tool install --global dotnet-ef`, then restart your terminal |
| `No migrations configuration type was found` | Run migrations commands from inside the `VendorShopOnline` project folder, not the repo root |
| Build error about missing `Microsoft.EntityFrameworkCore.Design` | Confirm `dotnet restore` completed successfully; check your internet connection reached nuget.org |
| SQLite file locked / can't migrate | Stop any running `dotnet run` instance first |
| Blank/unstyled pages | Confirm `wwwroot/css/site.css` exists and `app.UseStaticFiles()` is present in `Program.cs` (it is, by default) |

## Project Structure

```
VendorShopOnline/
├── Controllers/         MVC controllers
├── Models/               Domain entities (Customer, Vendor, Product, Order, Payment, etc.)
├── ViewModels/           Form-bound models (registration, login, payment)
├── Data/                 ApplicationDbContext (EF Core)
├── Services/             IEmailSender + implementations
├── Views/                Razor views, organised by controller
├── wwwroot/
│   ├── css/site.css       Full responsive theme
│   ├── js/site.js         Mobile nav toggle, alert auto-dismiss
│   └── images/dut-logo.jpg
├── Program.cs             App startup / DI / middleware pipeline
└── appsettings.json        Connection string + email settings
```
