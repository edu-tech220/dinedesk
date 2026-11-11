namespace DineDesk.Api.Domain.Entities;

/// <summary>A recurring weekly discount window, e.g. Monday-Friday 16:00-18:00, 20% off eligible items.</summary>
public class HappyHourWindow
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartsAt { get; set; }
    public TimeSpan EndsAt { get; set; }
    public decimal DiscountPercentage { get; set; }
}
