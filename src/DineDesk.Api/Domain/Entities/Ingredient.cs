namespace DineDesk.Api.Domain.Entities;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal QuantityInStock { get; set; }
    public decimal ReorderThreshold { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MenuItemIngredient> UsedIn { get; set; } = new List<MenuItemIngredient>();
}
