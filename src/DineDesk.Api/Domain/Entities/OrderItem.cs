using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Domain.Entities;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Unit price captured at order time (after any happy-hour discount), so later menu price changes don't retroactively change the bill.</summary>
    public decimal UnitPrice { get; set; }

    public OrderItemStatus Status { get; set; } = OrderItemStatus.Received;

    /// <summary>Guest label for itemized split billing, e.g. "Guest 1". Null when the item is shared.</summary>
    public string? GuestLabel { get; set; }

    public string? VoidReason { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}
