using System.ComponentModel.DataAnnotations;

namespace StarEvents.Models
{
    public class PurchaseTicketViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; }
        public DateTime EventDate { get; set; }
        public string Location { get; set; }
        public decimal TicketPrice { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; }

        public string OrganizerName { get; set; }
    }

    public class TicketDetailsViewModel
    {
        public int TicketId { get; set; }
        public string EventTitle { get; set; }
        public DateTime EventDate { get; set; }
        public string Location { get; set; }
        public decimal PricePaid { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string Status { get; set; }
        public string QRCode { get; set; }
        public string OrganizerName { get; set; }
        public string PaymentMethod { get; set; }
    }

    public class ValidateTicketViewModel
    {
        public string QRCodeData { get; set; }
    }
}