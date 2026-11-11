namespace DineDesk.Api.Contracts;

public record IngredientResponse(int Id, string Name, string UnitOfMeasure, decimal QuantityInStock, decimal ReorderThreshold);

public record RestockRequest(decimal QuantityToAdd);
