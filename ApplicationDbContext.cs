using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VendorShopOnline.Models;

namespace VendorShopOnline.Data
{
    /// <summary>
    /// Application database context. Inherits IdentityDbContext so that the
    /// standard Identity tables (AspNetUsers, AspNetRoles, AspNetUserRoles,
    /// AspNetUserTokens — used for password reset tokens — etc.) are created
    /// automatically alongside our own domain tables.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Vendor> Vendors { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // --- Customer (1:1 with ApplicationUser) ---
            builder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne(u => u.Customer)
                .HasForeignKey<Customer>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Customer>()
                .HasIndex(c => c.Email)
                .IsUnique();

            // --- Vendor (1:1 with ApplicationUser) ---
            builder.Entity<Vendor>()
                .HasOne(v => v.User)
                .WithOne(u => u.Vendor)
                .HasForeignKey<Vendor>(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Vendor>()
                .HasIndex(v => v.Email)
                .IsUnique();

            // --- Product -> Vendor (many-to-one) ---
            builder.Entity<Product>()
                .HasOne(p => p.Vendor)
                .WithMany(v => v.Products)
                .HasForeignKey(p => p.VendorId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- Product -> Category (many-to-one) ---
            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Order -> Customer (many-to-one) ---
            builder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- OrderItem -> Order (many-to-one) ---
            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- OrderItem -> Product (many-to-one) ---
            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Payment -> Order (many-to-one) ---
            builder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- Payment -> Customer (many-to-one) ---
            builder.Entity<Payment>()
                .HasOne(p => p.Customer)
                .WithMany()
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasIndex(p => p.ReferenceNumber)
                .IsUnique();

            // --- Seed data: categories only (no user/product seed data,
            // which keeps the database honestly empty until real
            // registration/CRUD testing is performed for the demo). ---
            builder.Entity<Category>().HasData(
                new Category { CategoryId = 1, Name = "Maize Meal", IconClass = "icon-maize" },
                new Category { CategoryId = 2, Name = "Rice", IconClass = "icon-rice" },
                new Category { CategoryId = 3, Name = "Bread & Bakery", IconClass = "icon-bread" },
                new Category { CategoryId = 4, Name = "Cooking Oil", IconClass = "icon-oil" },
                new Category { CategoryId = 5, Name = "Dairy Products", IconClass = "icon-dairy" },
                new Category { CategoryId = 6, Name = "Snacks", IconClass = "icon-snacks" },
                new Category { CategoryId = 7, Name = "Biscuits", IconClass = "icon-biscuits" },
                new Category { CategoryId = 8, Name = "Vegetables", IconClass = "icon-vegetables" },
                new Category { CategoryId = 9, Name = "Canned Goods", IconClass = "icon-canned" }
            );
        }
    }
}
