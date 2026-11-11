namespace DineDesk.Api.Domain.Entities;

/// <summary>A recipe line: how much of an ingredient one serving of a menu item consumes.</summary>
public class MenuItemIngredient
{
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal QuantityRequired { get; set; }
}
