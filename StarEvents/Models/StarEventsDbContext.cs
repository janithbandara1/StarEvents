using Microsoft.EntityFrameworkCore;

namespace StarEvents.Models
{
    public class StarEventsDbContext : DbContext
    {
        public StarEventsDbContext(DbContextOptions<StarEventsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Discount> Discounts { get; set; }
        public DbSet<LoyaltyPoint> LoyaltyPoints { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure all DateTime properties to use 'datetime' instead of 'datetime2'
            
            // User entity DateTime properties
            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasColumnType("datetime");

            // Event entity DateTime properties
            modelBuilder.Entity<Event>()
                .Property(e => e.EventDate)
                .HasColumnType("datetime");

            modelBuilder.Entity<Event>()
                .Property(e => e.CreatedAt)
                .HasColumnType("datetime");

            // Ticket entity DateTime properties
            modelBuilder.Entity<Ticket>()
                .Property(t => t.PurchaseDate)
                .HasColumnType("datetime");

            // Payment entity DateTime properties
            modelBuilder.Entity<Payment>()
                .Property(p => p.PaymentDate)
                .HasColumnType("datetime");

            // Discount entity DateTime properties
            modelBuilder.Entity<Discount>()
                .Property(d => d.ValidFrom)
                .HasColumnType("datetime");

            modelBuilder.Entity<Discount>()
                .Property(d => d.ValidTo)
                .HasColumnType("datetime");

            // LoyaltyPoint entity DateTime properties
            modelBuilder.Entity<LoyaltyPoint>()
                .Property(l => l.LastUpdated)
                .HasColumnType("datetime");
        }
    }
}
