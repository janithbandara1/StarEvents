using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StarEvents.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // Customer, Organizer, Admin
        public string? ProfileImage { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Event> Events { get; set; }
        public ICollection<Ticket> Tickets { get; set; }
        public LoyaltyPoint LoyaltyPoint { get; set; }
    }

    public class Event
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Location { get; set; }
        public DateTime EventDate { get; set; }
        public decimal TicketPrice { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public User Organizer { get; set; }
        public ICollection<Ticket> Tickets { get; set; }
    }

    public class Ticket
    {
        public int TicketId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string? QRCode { get; set; }
        public decimal PricePaid { get; set; }
        public string Status { get; set; }

        public Event Event { get; set; }
        public User User { get; set; }
        public Payment Payment { get; set; }
    }

    public class Payment
    {
        public int PaymentId { get; set; }
        public int TicketId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string Status { get; set; }

        public Ticket Ticket { get; set; }
    }

    public class Discount
    {
        public int DiscountId { get; set; }
        public string Code { get; set; }
        public string? Description { get; set; }
        public int Percentage { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsActive { get; set; }
    }

    public class LoyaltyPoint
    {
        [Key]
        public int LoyaltyId { get; set; }
        public int UserId { get; set; }
        public int Points { get; set; }
        public DateTime LastUpdated { get; set; }

        public User User { get; set; }
    }
}
