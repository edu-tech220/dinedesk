using DineDesk.Api.Data;
using DineDesk.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DineDesk.Api.Services;

public record MenuItemSummary(int Id, string Name, string Category, decimal Price, bool IsAvailable, bool IsHappyHourEligible);

public record MenuItemRecipeLine(int IngredientId, decimal QuantityRequired);

public interface IMenuService
{
    Task<PagedResult<MenuItemSummary>> GetMenuAsync(int page, int pageSize, string? category);

    Task<ServiceResult<MenuItem>> CreateMenuItemAsync(string name, string category, decimal price, bool isHappyHourEligible, IReadOnlyList<MenuItemRecipeLine> recipe);

    Task<ServiceResult<bool>> SetAvailabilityAsync(int menuItemId, bool isAvailable, byte[] expectedRowVersion);
}

public class MenuService : IMenuService
{
    private const int MaxPageSize = 100;

    private readonly DineDeskDbContext _db;

    public MenuService(DineDeskDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<MenuItemSummary>> GetMenuAsync(int page, int pageSize, string? category)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.MenuItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MenuItemSummary(m.Id, m.Name, m.Category, m.Price, m.IsAvailable, m.IsHappyHourEligible))
            .ToListAsync();

        return new PagedResult<MenuItemSummary>(items, page, pageSize, totalCount);
    }

    public async Task<ServiceResult<MenuItem>> CreateMenuItemAsync(
        string name, string category, decimal price, bool isHappyHourEligible, IReadOnlyList<MenuItemRecipeLine> recipe)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
        {
            return ServiceResult<MenuItem>.Failure("Name and category are required.");
        }

        if (price <= 0)
        {
            return ServiceResult<MenuItem>.Failure("Price must be positive.");
        }

        if (recipe.Count == 0)
        {
            return ServiceResult<MenuItem>.Failure("A menu item needs at least one recipe ingredient.");
        }

        var ingredientIds = recipe.Select(r => r.IngredientId).Distinct().ToList();
        var existingCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (existingCount != ingredientIds.Count)
        {
            return ServiceResult<MenuItem>.Failure("One or more ingredients do not exist.");
        }

        var menuItem = new MenuItem
        {
            Name = name,
            Category = category,
            Price = price,
            IsHappyHourEligible = isHappyHourEligible,
        };

        foreach (var line in recipe)
        {
            menuItem.Recipe.Add(new MenuItemIngredient { IngredientId = line.IngredientId, QuantityRequired = line.QuantityRequired });
        }

        _db.MenuItems.Add(menuItem);
        await _db.SaveChangesAsync();

        return ServiceResult<MenuItem>.Success(menuItem);
    }

    public async Task<ServiceResult<bool>> SetAvailabilityAsync(int menuItemId, bool isAvailable, byte[] expectedRowVersion)
    {
        var menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == menuItemId);
        if (menuItem is null)
        {
            return ServiceResult<bool>.Failure("Menu item not found.", ServiceErrorType.NotFound);
        }

        _db.Entry(menuItem).Property(m => m.RowVersion).OriginalValue = expectedRowVersion;
        menuItem.IsAvailable = isAvailable;

        try
        {
            await _db.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<bool>.Failure("Menu item was modified concurrently, please retry.", ServiceErrorType.Conflict);
        }
    }
}
