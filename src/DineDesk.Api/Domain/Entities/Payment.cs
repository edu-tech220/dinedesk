using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Domain.Entities;

public class Payment
{
    public int Id { get; set; }

    public int TabId { get; set; }
    public Tab Tab { get; set; } = null!;

    public string? GuestLabel { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaidAtUtc { get; set; }
}
