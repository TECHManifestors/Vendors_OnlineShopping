using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Data;
using VendorShopOnline.Models;
using VendorShopOnline.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Database (SQLite via EF Core) ----------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=vendorshop.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ---------------- ASP.NET Core Identity ----------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password policy — enforced server-side by Identity in addition
        // to the client-side [RegularExpression] annotations, so validation
        // cannot be bypassed by disabling JavaScript.
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Account lockout to slow brute-force login attempts.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Emails must be unique across the system.
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Password reset tokens (DataProtectorTokenProvider, the default) expire
// after this window. Kept explicit here so the security control is visible
// and easy to reference in documentation.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(2));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// ---------------- Email service ----------------
// Uses real SMTP if configured in appsettings/user-secrets, otherwise falls
// back to logging the email content — see Services/SmtpEmailSender.cs.
var smtpHostConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["EmailSettings:SmtpHost"]);
if (smtpHostConfigured)
{
    builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, LoggingEmailSender>();
}

// ---------------- New feature services ----------------
// Product image upload (local disk storage under wwwroot/images/products)
// and delivery OTP generation/verification. Registered the same way as
// the existing IEmailSender abstraction just above.
builder.Services.AddScoped<IProductImageService,LocalProductImageService>();
builder.Services.AddScoped<IDeliveryOtpService,DeliveryOtpService>();

// ---------------- MVC ----------------
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ---------------- Apply migrations & seed roles at startup ----------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { RoleNames.Customer, RoleNames.Vendor, RoleNames.Administrator })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

// ---------------- HTTP request pipeline ----------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
