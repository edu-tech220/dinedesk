using DineDesk.Api.Data;
using DineDesk.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DineDesk.Api.Services;

public record IngredientSummary(int Id, string Name, string UnitOfMeasure, decimal QuantityInStock, decimal ReorderThreshold);

public interface IIngredientService
{
    Task<IReadOnlyList<IngredientSummary>> GetAllAsync();

    Task<ServiceResult<bool>> RestockAsync(int ingredientId, decimal quantityToAdd);
}

public class IngredientService : IIngredientService
{
    private readonly DineDeskDbContext _db;
    private readonly BillingOptions _billingOptions;

    public IngredientService(DineDeskDbContext db, IOptions<BillingOptions> billingOptions)
    {
        _db = db;
        _billingOptions = billingOptions.Value;
    }

    public async Task<IReadOnlyList<IngredientSummary>> GetAllAsync()
    {
        return await _db.Ingredients
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new IngredientSummary(i.Id, i.Name, i.UnitOfMeasure, i.QuantityInStock, i.ReorderThreshold))
            .ToListAsync();
    }

    public async Task<ServiceResult<bool>> RestockAsync(int ingredientId, decimal quantityToAdd)
    {
        if (quantityToAdd <= 0)
        {
            return ServiceResult<bool>.Failure("Quantity to add must be positive.");
        }

        for (var attempt = 1; attempt <= _billingOptions.MaxConcurrencyRetries; attempt++)
        {
            var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.Id == ingredientId);
            if (ingredient is null)
            {
                return ServiceResult<bool>.Failure("Ingredient not found.", ServiceErrorType.NotFound);
            }

            ingredient.QuantityInStock += quantityToAdd;

            try
            {
                await _db.SaveChangesAsync();
                return ServiceResult<bool>.Success(true);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync();
                }
            }
        }

        return ServiceResult<bool>.Failure("Restock failed due to concurrent updates. Please try again.", ServiceErrorType.Conflict);
    }
}
