namespace DineDesk.Api.Domain.Entities;

/// <summary>A single round placed against a tab. A tab can have many orders over its lifetime.</summary>
public class Order
{
    public int Id { get; set; }

    public int TabId { get; set; }
    public Tab Tab { get; set; } = null!;

    public DateTime PlacedAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
