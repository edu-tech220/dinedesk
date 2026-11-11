namespace DineDesk.Api.Domain.Entities;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>True when the kitchen can no longer serve this item (auto-86'd or manually disabled).</summary>
    public bool IsAvailable { get; set; } = true;

    public bool IsHappyHourEligible { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<MenuItemIngredient> Recipe { get; set; } = new List<MenuItemIngredient>();
}
