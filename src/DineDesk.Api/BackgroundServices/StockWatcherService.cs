using DineDesk.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DineDesk.Api.BackgroundServices;

/// <summary>Periodically reconciles menu item availability against current ingredient stock.
/// Order placement already 86's items it depletes, but this worker catches the other direction:
/// re-enabling items once a manual restock makes them fulfillable again.</summary>
public class StockWatcherService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockWatcherService> _logger;

    public StockWatcherService(IServiceScopeFactory scopeFactory, ILogger<StockWatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Stock reconciliation pass failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DineDeskDbContext>();

        var menuItems = await db.MenuItems
            .Include(m => m.Recipe)
            .ThenInclude(r => r.Ingredient)
            .Where(m => m.Recipe.Any())
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var menuItem in menuItems)
        {
            var canFulfillOne = menuItem.Recipe.All(r => r.Ingredient.QuantityInStock >= r.QuantityRequired);
            if (canFulfillOne != menuItem.IsAvailable)
            {
                menuItem.IsAvailable = canFulfillOne;
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Stock watcher updated availability for {Count} menu item(s).", changed);
        }
    }
}
