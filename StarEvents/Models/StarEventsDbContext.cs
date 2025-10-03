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
    }
}
