using DineDesk.Api.Data;
using DineDesk.Api.Domain.Entities;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DineDesk.Api.Services;

public record PlaceOrderLine(int MenuItemId, int Quantity, string? GuestLabel);

public record PlacedOrderItem(int OrderItemId, int MenuItemId, string MenuItemName, int Quantity, decimal UnitPrice, OrderItemStatus Status);

public record PlacedOrder(int OrderId, int TabId, DateTime PlacedAtUtc, IReadOnlyList<PlacedOrderItem> Items);

public record KitchenQueueItem(int Id, string MenuItemName, int Quantity, OrderItemStatus Status, string TableNumber, DateTime PlacedAtUtc, byte[] RowVersion);

public interface IOrderService
{
    Task<ServiceResult<PlacedOrder>> PlaceOrderAsync(int tabId, IReadOnlyList<PlaceOrderLine> lines);

    Task<PagedResult<KitchenQueueItem>> GetKitchenQueueAsync(int page, int pageSize, OrderItemStatus? status);

    Task<ServiceResult<bool>> UpdateItemStatusAsync(int orderItemId, OrderItemStatus newStatus, byte[] expectedRowVersion);

    Task<ServiceResult<bool>> VoidItemAsync(int orderItemId, string reason);
}

public class OrderService : IOrderService
{
    private static readonly Dictionary<OrderItemStatus, OrderItemStatus[]> AllowedTransitions = new()
    {
        [OrderItemStatus.Received] = new[] { OrderItemStatus.Preparing },
        [OrderItemStatus.Preparing] = new[] { OrderItemStatus.Ready },
        [OrderItemStatus.Ready] = new[] { OrderItemStatus.Served },
        [OrderItemStatus.Served] = Array.Empty<OrderItemStatus>(),
        [OrderItemStatus.Voided] = Array.Empty<OrderItemStatus>(),
    };

    private readonly DineDeskDbContext _db;
    private readonly IHappyHourPricingService _pricing;
    private readonly BillingOptions _billingOptions;

    public OrderService(DineDeskDbContext db, IHappyHourPricingService pricing, IOptions<BillingOptions> billingOptions)
    {
        _db = db;
        _pricing = pricing;
        _billingOptions = billingOptions.Value;
    }

    public async Task<ServiceResult<PlacedOrder>> PlaceOrderAsync(int tabId, IReadOnlyList<PlaceOrderLine> lines)
    {
        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0))
        {
            return ServiceResult<PlacedOrder>.Failure("Each order line needs a positive quantity.");
        }

        var tab = await _db.Tabs.FirstOrDefaultAsync(t => t.Id == tabId);
        if (tab is null)
        {
            return ServiceResult<PlacedOrder>.Failure("Tab not found.", ServiceErrorType.NotFound);
        }

        if (tab.Status != TabStatus.Open)
        {
            return ServiceResult<PlacedOrder>.Failure("Tab is not open.", ServiceErrorType.Conflict);
        }

        var menuItemIds = lines.Select(l => l.MenuItemId).Distinct().ToList();
        var menuItemsReadOnly = await _db.MenuItems
            .AsNoTracking()
            .Include(m => m.Recipe)
            .Where(m => menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var missing = menuItemIds.Where(id => !menuItemsReadOnly.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return ServiceResult<PlacedOrder>.Failure($"Unknown menu item id(s): {string.Join(", ", missing)}.");
        }

        var unavailable = menuItemIds.Where(id => !menuItemsReadOnly[id].IsAvailable).Select(id => menuItemsReadOnly[id].Name).ToList();
        if (unavailable.Count > 0)
        {
            return ServiceResult<PlacedOrder>.Failure($"Currently 86'd and cannot be ordered: {string.Join(", ", unavailable)}.", ServiceErrorType.Conflict);
        }

        var requiredByIngredient = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            foreach (var recipeLine in menuItemsReadOnly[line.MenuItemId].Recipe)
            {
                requiredByIngredient.TryGetValue(recipeLine.IngredientId, out var current);
                requiredByIngredient[recipeLine.IngredientId] = current + recipeLine.QuantityRequired * line.Quantity;
            }
        }

        var now = DateTime.Now;
        var unitPrices = new Dictionary<int, decimal>();
        foreach (var menuItemId in menuItemIds)
        {
            unitPrices[menuItemId] = await _pricing.GetCurrentUnitPriceAsync(menuItemsReadOnly[menuItemId], now);
        }

        for (var attempt = 1; attempt <= _billingOptions.MaxConcurrencyRetries; attempt++)
        {
            // Drop any entities left tracked by a previous, conflicting attempt so every retry
            // re-reads the current database state instead of compounding stale in-memory edits.
            _db.ChangeTracker.Clear();

            var ingredients = await _db.Ingredients
                .Where(i => requiredByIngredient.Keys.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            var trackedMenuItems = await _db.MenuItems
                .Include(m => m.Recipe)
                .ThenInclude(r => r.Ingredient)
                .Where(m => menuItemIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            var shortages = requiredByIngredient
                .Where(kv => ingredients[kv.Key].QuantityInStock < kv.Value)
                .Select(kv => ingredients[kv.Key].Name)
                .ToList();

            if (shortages.Count > 0)
            {
                await Mark86dItemsAsync(trackedMenuItems.Values, ingredients);

                try
                {
                    await _db.SaveChangesAsync();
                    return ServiceResult<PlacedOrder>.Failure(
                        $"Not enough stock for: {string.Join(", ", shortages)}. The affected item(s) were marked unavailable.",
                        ServiceErrorType.Conflict);
                }
                catch (DbUpdateConcurrencyException)
                {
                    continue;
                }
            }

            foreach (var (ingredientId, quantity) in requiredByIngredient)
            {
                ingredients[ingredientId].QuantityInStock -= quantity;
            }

            await Mark86dItemsAsync(trackedMenuItems.Values, ingredients);

            var order = new Order { TabId = tabId, PlacedAtUtc = DateTime.UtcNow };
            foreach (var line in lines)
            {
                order.Items.Add(new OrderItem
                {
                    MenuItemId = line.MenuItemId,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrices[line.MenuItemId],
                    GuestLabel = line.GuestLabel,
                    Status = OrderItemStatus.Received,
                });
            }

            _db.Orders.Add(order);

            try
            {
                await _db.SaveChangesAsync();

                var placedItems = order.Items
                    .Select(i => new PlacedOrderItem(i.Id, i.MenuItemId, menuItemsReadOnly[i.MenuItemId].Name, i.Quantity, i.UnitPrice, i.Status))
                    .ToList();

                return ServiceResult<PlacedOrder>.Success(new PlacedOrder(order.Id, tabId, order.PlacedAtUtc, placedItems));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Loop again; ChangeTracker.Clear() at the top of the next attempt discards
                // this attempt's in-memory edits so we re-read fresh stock levels.
            }
        }

        return ServiceResult<PlacedOrder>.Failure(
            "The order could not be placed due to concurrent stock updates. Please try again.",
            ServiceErrorType.Conflict);
    }

    public async Task<PagedResult<KitchenQueueItem>> GetKitchenQueueAsync(int page, int pageSize, OrderItemStatus? status)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.OrderItems
            .AsNoTracking()
            .Include(i => i.MenuItem)
            .Include(i => i.Order)
            .ThenInclude(o => o.Tab)
            .ThenInclude(t => t.DiningTable)
            .Where(i => status.HasValue ? i.Status == status.Value : i.Status != OrderItemStatus.Served && i.Status != OrderItemStatus.Voided)
            .AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(i => i.Order.PlacedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new KitchenQueueItem(i.Id, i.MenuItem.Name, i.Quantity, i.Status, i.Order.Tab.DiningTable.Number, i.Order.PlacedAtUtc, i.RowVersion))
            .ToListAsync();

        return new PagedResult<KitchenQueueItem>(items, page, pageSize, totalCount);
    }

    public async Task<ServiceResult<bool>> UpdateItemStatusAsync(int orderItemId, OrderItemStatus newStatus, byte[] expectedRowVersion)
    {
        var item = await _db.OrderItems.FirstOrDefaultAsync(i => i.Id == orderItemId);
        if (item is null)
        {
            return ServiceResult<bool>.Failure("Order item not found.", ServiceErrorType.NotFound);
        }

        if (!AllowedTransitions[item.Status].Contains(newStatus))
        {
            return ServiceResult<bool>.Failure($"Cannot move an item from {item.Status} to {newStatus}.", ServiceErrorType.Validation);
        }

        _db.Entry(item).Property(i => i.RowVersion).OriginalValue = expectedRowVersion;
        item.Status = newStatus;

        try
        {
            await _db.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<bool>.Failure(
                "This item was already updated by someone else. Refresh and try again.",
                ServiceErrorType.Conflict);
        }
    }

    public async Task<ServiceResult<bool>> VoidItemAsync(int orderItemId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<bool>.Failure("A void reason is required.");
        }

        for (var attempt = 1; attempt <= _billingOptions.MaxConcurrencyRetries; attempt++)
        {
            _db.ChangeTracker.Clear();

            var item = await _db.OrderItems
                .Include(i => i.MenuItem)
                .ThenInclude(m => m.Recipe)
                .ThenInclude(r => r.Ingredient)
                .FirstOrDefaultAsync(i => i.Id == orderItemId);

            if (item is null)
            {
                return ServiceResult<bool>.Failure("Order item not found.", ServiceErrorType.NotFound);
            }

            if (item.Status == OrderItemStatus.Voided)
            {
                return ServiceResult<bool>.Failure("Item is already voided.", ServiceErrorType.Conflict);
            }

            if (item.Status == OrderItemStatus.Served)
            {
                return ServiceResult<bool>.Failure("Cannot void an item that has already been served.", ServiceErrorType.Validation);
            }

            item.Status = OrderItemStatus.Voided;
            item.VoidReason = reason;

            foreach (var recipeLine in item.MenuItem.Recipe)
            {
                recipeLine.Ingredient.QuantityInStock += recipeLine.QuantityRequired * item.Quantity;
            }

            if (!item.MenuItem.IsAvailable && item.MenuItem.Recipe.All(r => r.Ingredient.QuantityInStock >= r.QuantityRequired))
            {
                item.MenuItem.IsAvailable = true;
            }

            try
            {
                await _db.SaveChangesAsync();
                return ServiceResult<bool>.Success(true);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Loop again; ChangeTracker.Clear() at the top of the next attempt re-reads fresh state.
            }
        }

        return ServiceResult<bool>.Failure(
            "The item could not be voided due to concurrent stock updates. Please try again.",
            ServiceErrorType.Conflict);
    }

    private Task Mark86dItemsAsync(IEnumerable<MenuItem> candidateMenuItems, Dictionary<int, Ingredient> loadedIngredients)
    {
        foreach (var menuItem in candidateMenuItems)
        {
            if (menuItem.Recipe.Count == 0)
            {
                continue;
            }

            var hasEnoughForOneServing = menuItem.Recipe.All(r =>
                !loadedIngredients.TryGetValue(r.IngredientId, out var ingredient) || ingredient.QuantityInStock >= r.QuantityRequired);

            if (!hasEnoughForOneServing)
            {
                menuItem.IsAvailable = false;
            }
        }

        return Task.CompletedTask;
    }
}
