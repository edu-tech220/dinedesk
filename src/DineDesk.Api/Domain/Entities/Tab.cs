using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Domain.Entities;

/// <summary>An open table session. Orders (rounds) and payments accumulate against a tab until it closes.</summary>
public class Tab
{
    public int Id { get; set; }

    public int DiningTableId { get; set; }
    public DiningTable DiningTable { get; set; } = null!;

    public int GuestCount { get; set; }
    public int OpenedByUserId { get; set; }
    public User OpenedByUser { get; set; } = null!;

    public TabStatus Status { get; set; } = TabStatus.Open;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public decimal TipAmount { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
