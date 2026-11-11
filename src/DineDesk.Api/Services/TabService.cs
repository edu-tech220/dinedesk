using DineDesk.Api.Data;
using DineDesk.Api.Domain.Entities;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DineDesk.Api.Services;

public record BillSummary(decimal Subtotal, decimal TaxRate, decimal Tax, decimal Tip, decimal Total, decimal AmountPaid, decimal BalanceDue);

public interface ITabService
{
    Task<ServiceResult<Tab>> OpenTabAsync(int tableId, int guestCount, int openedByUserId);

    Task<ServiceResult<BillSummary>> GetBillAsync(int tabId);

    Task<ServiceResult<IReadOnlyList<decimal>>> PreviewEvenSplitAsync(int tabId, int splitCount);

    Task<ServiceResult<bool>> AddPaymentAsync(int tabId, string? guestLabel, decimal amount, PaymentMethod method);

    Task<ServiceResult<bool>> CloseTabAsync(int tabId);
}

public class TabService : ITabService
{
    private const decimal BalanceTolerance = 0.01m;

    private readonly DineDeskDbContext _db;
    private readonly BillingOptions _billingOptions;

    public TabService(DineDeskDbContext db, IOptions<BillingOptions> billingOptions)
    {
        _db = db;
        _billingOptions = billingOptions.Value;
    }

    public async Task<ServiceResult<Tab>> OpenTabAsync(int tableId, int guestCount, int openedByUserId)
    {
        if (guestCount <= 0)
        {
            return ServiceResult<Tab>.Failure("Guest count must be at least 1.");
        }

        var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == tableId);
        if (table is null)
        {
            return ServiceResult<Tab>.Failure("Table not found.", ServiceErrorType.NotFound);
        }

        if (table.Status != TableStatus.Free)
        {
            return ServiceResult<Tab>.Failure("Table is already occupied.", ServiceErrorType.Conflict);
        }

        var tab = new Tab
        {
            DiningTableId = tableId,
            GuestCount = guestCount,
            OpenedByUserId = openedByUserId,
            Status = TabStatus.Open,
            OpenedAtUtc = DateTime.UtcNow,
        };

        table.Status = TableStatus.Occupied;
        _db.Tabs.Add(tab);

        await _db.SaveChangesAsync();
        return ServiceResult<Tab>.Success(tab);
    }

    public async Task<ServiceResult<BillSummary>> GetBillAsync(int tabId)
    {
        var tab = await _db.Tabs
            .Include(t => t.Orders)
            .ThenInclude(o => o.Items)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == tabId);

        if (tab is null)
        {
            return ServiceResult<BillSummary>.Failure("Tab not found.", ServiceErrorType.NotFound);
        }

        var summary = BuildBillSummary(tab);
        return ServiceResult<BillSummary>.Success(summary);
    }

    public async Task<ServiceResult<IReadOnlyList<decimal>>> PreviewEvenSplitAsync(int tabId, int splitCount)
    {
        if (splitCount <= 0)
        {
            return ServiceResult<IReadOnlyList<decimal>>.Failure("Split count must be at least 1.");
        }

        var billResult = await GetBillAsync(tabId);
        if (!billResult.Succeeded)
        {
            return ServiceResult<IReadOnlyList<decimal>>.Failure(billResult.Error!, billResult.ErrorType);
        }

        var shares = SplitEvenly(billResult.Value!.Total, splitCount);
        return ServiceResult<IReadOnlyList<decimal>>.Success(shares);
    }

    public async Task<ServiceResult<bool>> AddPaymentAsync(int tabId, string? guestLabel, decimal amount, PaymentMethod method)
    {
        if (amount <= 0)
        {
            return ServiceResult<bool>.Failure("Payment amount must be positive.");
        }

        var tab = await _db.Tabs
            .Include(t => t.Orders)
            .ThenInclude(o => o.Items)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == tabId);

        if (tab is null)
        {
            return ServiceResult<bool>.Failure("Tab not found.", ServiceErrorType.NotFound);
        }

        if (tab.Status != TabStatus.Open)
        {
            return ServiceResult<bool>.Failure("Tab is already closed.", ServiceErrorType.Conflict);
        }

        var summary = BuildBillSummary(tab);
        if (amount > summary.BalanceDue + BalanceTolerance)
        {
            return ServiceResult<bool>.Failure(
                $"Payment of {amount:C} exceeds the remaining balance of {summary.BalanceDue:C}.",
                ServiceErrorType.Validation);
        }

        _db.Payments.Add(new Payment
        {
            TabId = tabId,
            GuestLabel = guestLabel,
            Amount = amount,
            Method = method,
            PaidAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CloseTabAsync(int tabId)
    {
        var tab = await _db.Tabs
            .Include(t => t.DiningTable)
            .Include(t => t.Orders)
            .ThenInclude(o => o.Items)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == tabId);

        if (tab is null)
        {
            return ServiceResult<bool>.Failure("Tab not found.", ServiceErrorType.NotFound);
        }

        if (tab.Status != TabStatus.Open)
        {
            return ServiceResult<bool>.Failure("Tab is already closed.", ServiceErrorType.Conflict);
        }

        var summary = BuildBillSummary(tab);
        if (summary.BalanceDue > BalanceTolerance)
        {
            return ServiceResult<bool>.Failure(
                $"Tab is not fully paid yet, balance due: {summary.BalanceDue:C}.",
                ServiceErrorType.Conflict);
        }

        tab.Status = TabStatus.Closed;
        tab.ClosedAtUtc = DateTime.UtcNow;
        tab.DiningTable.Status = TableStatus.Free;

        try
        {
            await _db.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<bool>.Failure("Tab was modified concurrently, please retry.", ServiceErrorType.Conflict);
        }
    }

    private BillSummary BuildBillSummary(Tab tab)
    {
        var subtotal = tab.Orders
            .SelectMany(o => o.Items)
            .Where(i => i.Status != OrderItemStatus.Voided)
            .Sum(i => i.UnitPrice * i.Quantity);

        var tax = decimal.Round(subtotal * _billingOptions.TaxRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax + tab.TipAmount;
        var amountPaid = tab.Payments.Sum(p => p.Amount);
        var balanceDue = decimal.Round(total - amountPaid, 2, MidpointRounding.AwayFromZero);

        return new BillSummary(subtotal, _billingOptions.TaxRate, tax, tab.TipAmount, total, amountPaid, Math.Max(balanceDue, 0));
    }

    private static IReadOnlyList<decimal> SplitEvenly(decimal total, int splitCount)
    {
        var totalCents = (long)Math.Round(total * 100, MidpointRounding.AwayFromZero);
        var baseCents = totalCents / splitCount;
        var remainderCents = totalCents % splitCount;

        var shares = new decimal[splitCount];
        for (var i = 0; i < splitCount; i++)
        {
            var cents = baseCents + (i < remainderCents ? 1 : 0);
            shares[i] = cents / 100m;
        }

        return shares;
    }
}
