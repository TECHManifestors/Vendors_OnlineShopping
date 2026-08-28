# VendorShop Online — Sprint Two Feature Summary

This document describes the five features added to the existing VendorShop
Online system, exactly as requested: product image upload, customer order
tracking, an ordered-products report, delivery OTP verification, and
product search. **No existing feature, controller action, route, or view
was removed or renamed**, and all database changes are additive
(new nullable columns / new tables), so existing data and workflows keep
working unmodified.

---

## 1. Safest-approach analysis (performed before writing any code)

The codebase was fully inspected first: `Program.cs`, `ApplicationDbContext`,
every model, controller, view, and the CSS/layout conventions. Key
constraints that shaped every design decision below:

- **EF Core stores enums as `int`.** `OrderStatus` already had 5 values in
  use (`Confirmed` is set by the payment flow). Any new status had to be
  *appended*, never inserted in the middle, or every previously-stored
  order's status would silently change meaning.
- **`Migrations/` is intentionally empty** (per the existing README) —
  migrations are generated locally against the developer's own EF tool
  version, not committed. So no migration files are included here; you
  generate one the same way you always have (see §7).
- **Existing patterns were reused, not replaced**: the new image and OTP
  services follow the exact same interface/DI pattern as the existing
  `IEmailSender`; the OTP hashing reuses ASP.NET Core Identity's
  `PasswordHasher`, the same primitive already trusted for account
  passwords; new "own data only" authorization scoping mirrors what
  `ProductController`/`VendorController` already do.
- **Every new field is nullable or defaulted**, and every new table is a
  separate table with a foreign key back to `Orders`/`Products` — nothing
  required backfilling or touching existing rows.

---

## 2. Feature 1 — Product Image Upload

**What changed:**
- `Product.ImagePath` (nullable `string`) — the saved image's relative web
  path (e.g. `/images/products/{guid}.jpg`), or `null` if the vendor hasn't
  uploaded one.
- `Product.ImageFile` (`[NotMapped] IFormFile?`) — the form-bound upload
  field, kept on the existing `Product` model (like `Order.IsPaid` already
  is) so the existing `Create(Product model)` / `Edit(Product model)`
  controller signatures didn't need to change.
- New `IProductImageService` / `LocalProductImageService`
  (`Services/`): validates file **extension AND binary signature** (magic
  bytes) — not just the browser-supplied content type, which is trivially
  spoofable — enforces a 5 MB limit, and saves to
  `wwwroot/images/products/`.
- `ProductController.Create`/`Edit` now call the image service when a file
  is supplied; validation failures surface as a normal `ModelState` error
  and re-display the form, exactly like every other field.
- `ProductController.Edit`/`Delete` clean up the old image file when it's
  replaced or the product is removed.
- Views updated to show the uploaded image (falling back to the original
  CSS/SVG category icon when there isn't one): `Product/Index`,
  `Product/Details`, `Product/MyProducts`, and file inputs added to
  `Product/Create` and `Product/Edit`.

**Not affected:** product creation without an image works exactly as
before (the field is optional); all existing validation rules on
`Product` are untouched.

---

## 3. Feature 2 — Customer Order Tracking

**What changed:**
- `OrderStatus.InTransit` — a new value **appended** to the end of the
  enum (see §1) representing "out for delivery".
- `Order.EstimatedDeliveryDate` (nullable `DateTime`).
- New `OrderTrackingEvent` table — one row per status change on an order
  (status, timestamp, optional note), giving the timeline its data.
- `Order.TrackingLabel` / `Order.TrackingStageIndex` — computed,
  `[NotMapped]` helpers that translate the underlying status into the
  customer-facing 4-step label (Processing / Shipped / In Transit /
  Delivered) without ever renaming the stored enum values.
- New `OrderController.Track(int orderId)` action + `Views/Order/Track.cshtml`
  — a timeline view, linked from *My Orders*.
- `PaymentController.Checkout` (POST) now also writes one
  `OrderTrackingEvent` when payment completes — the **only** change to
  that method; the existing payment/stock-deduction logic is untouched.
- `VendorOrderController` (new, see Feature 4) writes further tracking
  events as a vendor moves an order to Shipped / In Transit / Delivered.

**Not affected:** orders that existed before this feature simply have an
empty timeline and show "Processing" until a vendor acts on them — no
special-casing was needed.

---

## 4. Feature 3 — Ordered Products Report

**What changed:**
- New `ReportController.OrderedProducts` action
  (`[Authorize(Roles = "Vendor,Administrator")]`) — there was no existing
  reporting feature to extend, so this is a clean addition.
- A Vendor only ever sees their own products' sales (scoped by
  `VendorId`, identical to how `ProductController.MyProducts` already
  scopes data); an Administrator sees everything and can filter by vendor.
- Filters: date range, product, vendor (admin only), order status.
- Two tables are produced: a **per-product** breakdown (quantity ordered,
  number of distinct sales, revenue, first/last order date) and a
  **customer-orders summary** (order count, total spent, last order date).
- New `Views/Report/OrderedProducts.cshtml` — filter bar, summary cards,
  and two data tables, styled with new (additive) CSS rules in
  `site.css` (`.report-filter-bar`, `.report-table`, `.report-summary-cards`).
- New "Reports" link added to the nav for Vendor and Administrator roles
  in `_Layout.cshtml`.

---

## 5. Feature 4 — Customer Delivery OTP Verification

**What changed:**
- New `DeliveryOtp` table — one row per order, storing only a **salted
  hash** of the 6-digit code (via `PasswordHasher<DeliveryOtp>`), its
  expiry time, and an attempt counter. The plain code is never persisted.
- New `IDeliveryOtpService` / `DeliveryOtpService`:
  - Generates a cryptographically random 6-digit code
    (`RandomNumberGenerator`, not `Random`).
  - Default validity: **15 minutes**.
  - Locks after **5** incorrect attempts (`DeliveryOtp.MaxAttempts`).
  - Regenerating an OTP for the same order overwrites/invalidates the
    previous one.
- New `VendorOrderController` (Vendor-only, see Feature 2) handles the
  fulfilment workflow:
  - `ManageOrders` — lists paid orders containing the vendor's products.
  - `UpdateStatus` — advances Confirmed → Shipped → In Transit. Moving to
    **In Transit automatically generates and emails the OTP** to the
    customer via the existing `IEmailSender` pipeline (so it also appears
    in the console log in development, exactly like the password-reset
    email already does).
  - `ResendOtp` — issues a fresh code if the original expired.
  - `VerifyDelivery` (GET/POST) — the vendor/delivery person enters the
    code; on success the order is marked **Delivered** and a tracking
    event is logged. Invalid/expired/locked codes show a clear,
    actionable error instead of silently failing.
- New views: `Views/VendorOrder/ManageOrders.cshtml`,
  `Views/VendorOrder/VerifyDelivery.cshtml`.

**Security notes:** OTP is never shown in any vendor-facing UI or log —
only the customer's email receives it. Delivered status can now **only**
be reached through a verified OTP, never by directly editing an order.

---

## 6. Feature 5 — Product Search

**What changed:**
- `ProductController.Index(int? categoryId, string? searchTerm)` — the
  `searchTerm` parameter is new and additive; the existing `categoryId`
  filter still works exactly as before and can be combined with a search.
- Matches (case-insensitively, via `EF.Functions.Like`) against product
  name, description, category name, and vendor business name.
- A non-unique index on `Product.ProductName` was added in
  `ApplicationDbContext` to keep this fast as the catalogue grows. (SQLite
  full-text search (FTS5) would be a good next step if the catalogue grows
  into the thousands of products — noted here as a future improvement
  rather than implemented now, to keep this change minimal and safe.)
- `Views/Product/Index.cshtml` — added a search box that combines with the
  existing category pills; search term round-trips through both.

---

## 7. Files changed vs. created

### Modified (existing files — all changes additive)
- `Controllers/AccountController.cs` — **unrelated bug fix**: the
  forgot-password email's greeting name was built with a malformed C#
  string interpolation (an unparenthesized ternary and nested quotes
  inside a verbatim interpolated string), which failed to compile. This
  was already present in the uploaded project and unrelated to any of the
  five new features; it's now a simple `greetingName` variable computed
  before the string. No behaviour changed — the email still greets the
  user by their full name, or "there" if unset.
- `Models/Product.cs` — added `ImagePath`, `ImageFile`
- `Models/Order.cs` — added `EstimatedDeliveryDate`, `OrderStatus.InTransit`,
  `TrackingEvents`, `DeliveryOtp` nav, `TrackingLabel`, `TrackingStageIndex`
- `Data/ApplicationDbContext.cs` — new `DbSet`s, relationships, index
- `Program.cs` — registered two new services
- `Controllers/ProductController.cs` — image upload + search
- `Controllers/OrderController.cs` — added `Track` action
- `Controllers/PaymentController.cs` — added one tracking-event write
- `Views/Product/Create.cshtml`, `Edit.cshtml`, `Index.cshtml`,
  `Details.cshtml`, `MyProducts.cshtml` — image display/upload, search box
- `Views/Order/MyOrders.cshtml` — added "Track Order" link
- `Views/Shared/_Layout.cshtml` — new nav links (Manage Orders, Reports)
- `wwwroot/css/site.css` — new, additive rules only (timeline, report
  tables, OTP input, product image)
- `README.md` — documented the new features

### Created (new files)
- `Models/OrderTrackingEvent.cs`
- `Models/DeliveryOtp.cs`
- `Services/IProductImageService.cs`, `LocalProductImageService.cs`
- `Services/IDeliveryOtpService.cs`, `DeliveryOtpService.cs`
- `Controllers/VendorOrderController.cs`
- `Controllers/ReportController.cs`
- `ViewModels/ReportViewModels.cs`
- `Views/Order/Track.cshtml`
- `Views/VendorOrder/ManageOrders.cshtml`, `VerifyDelivery.cshtml`
- `Views/Report/OrderedProducts.cshtml`
- `wwwroot/images/products/` (upload target folder)
- This file, `FEATURE_SUMMARY.md`

---

## 8. Database changes required

No committed migration is included, consistent with how this project was
already set up (see the README's "First-Time Setup"). After pulling these
changes:

```bash
cd VendorShopOnline
dotnet ef migrations add AddImageTrackingOtpAndSearch
dotnet ef database update
```

This single migration will pick up automatically:
- `Products.ImagePath` (nullable `TEXT`)
- `Orders.EstimatedDeliveryDate` (nullable `DATETIME`)
- New table `OrderTrackingEvents`
- New table `DeliveryOtps`
- New index on `Products.ProductName`

All changes are backward compatible — existing rows get `NULL` for the new
nullable columns and simply have no rows in the two new tables until acted
on, which every new controller/view already treats as the normal case.

---

## 9. Testing instructions

1. **Image upload** — Log in as a Vendor → *My Dashboard* → *Add Product*.
   Upload a `.jpg`/`.png`/`.webp` under 5 MB → save → confirm it shows on
   *My Products*, the public *Products* page, and *Product Details*. Try
   uploading a `.txt` file renamed to `.jpg` — it should be rejected (the
   binary-signature check catches this, not just the extension). Try a
   file over 5 MB — it should be rejected. Edit a product and leave the
   image field blank — the existing image should be kept.

2. **Order tracking** — As a Customer, buy a product and pay (existing
   flow, unchanged). Go to *My Orders* → *Track Order*. You should see
   "Processing" as reached and the rest pending. Then, as the Vendor who
   owns that product, go to *Manage Orders* and advance the order through
   Shipped → Out for Delivery → (see Feature 4 test below for Delivered).
   Refresh the customer's Track page after each step to see the timeline
   update.

3. **Ordered products report** — As a Vendor with at least one paid order,
   go to *Reports*. Confirm the totals match your test orders. Try the
   date-range, product, and status filters. Confirm a second vendor
   account only ever sees their own products' data.

4. **Delivery OTP** — As the Vendor, from *Manage Orders*, click "Mark Out
   for Delivery" on a Shipped order. If no real SMTP is configured (the
   project's default), watch the application console/log — the OTP email,
   including the 6-digit code, is printed there exactly like the
   password-reset email already is (see the existing "Email / Forgot
   Password in Development" section of the README). Copy the code, click
   "Confirm Delivery", and enter it. Try entering a wrong code 5 times to
   confirm the lockout message appears, then use "Resend OTP" to get a
   fresh code. Confirm the order shows "Delivered" on both the vendor and
   customer tracking views afterward.

5. **Product search** — On the public *Products* page, search for a
   product name, a category name, and a vendor's business name — each
   should return matching results. Combine a search term with a category
   filter pill and confirm both apply together. Search for nonsense text
   and confirm the "No products found" message appears without an error.

## 10. Additional configuration needed

None. No new NuGet packages, connection strings, or `appsettings.json`
keys were introduced — everything reuses infrastructure already present
(EF Core/SQLite, ASP.NET Identity's password hasher, the existing
`IEmailSender` pipeline, and static file serving from `wwwroot`).
