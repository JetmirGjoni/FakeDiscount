using FakeDiscountDetector.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FakeDiscountDetector.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<PriceRecord> PriceRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Url)
                .IsUnique();

            modelBuilder.Entity<PriceRecord>()
                .HasOne(pr => pr.Product)
                .WithMany(p => p.PriceHistory)
                .HasForeignKey(pr => pr.ProductId);
        }
    }
}
