using DineDesk.Api.Data;
using DineDesk.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DineDesk.Api.Services;

public interface IHappyHourPricingService
{
    /// <summary>Computes the price to charge for one serving of a menu item right now, applying
    /// any active happy-hour discount. Pricing is always derived server-side, never trusted from the client.</summary>
    Task<decimal> GetCurrentUnitPriceAsync(MenuItem menuItem, DateTime nowLocal);
}

public class HappyHourPricingService : IHappyHourPricingService
{
    private readonly DineDeskDbContext _db;

    public HappyHourPricingService(DineDeskDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetCurrentUnitPriceAsync(MenuItem menuItem, DateTime nowLocal)
    {
        if (!menuItem.IsHappyHourEligible)
        {
            return menuItem.Price;
        }

        var dayOfWeek = nowLocal.DayOfWeek;
        var timeOfDay = nowLocal.TimeOfDay;

        var activeWindow = await _db.HappyHourWindows
            .Where(w => w.DayOfWeek == dayOfWeek && w.StartsAt <= timeOfDay && w.EndsAt > timeOfDay)
            .OrderByDescending(w => w.DiscountPercentage)
            .FirstOrDefaultAsync();

        if (activeWindow is null)
        {
            return menuItem.Price;
        }

        var discounted = menuItem.Price * (1 - activeWindow.DiscountPercentage);
        return decimal.Round(discounted, 2, MidpointRounding.AwayFromZero);
    }
}
