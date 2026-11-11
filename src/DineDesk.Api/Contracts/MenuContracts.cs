namespace DineDesk.Api.Contracts;

public record MenuItemResponse(int Id, string Name, string Category, decimal Price, bool IsAvailable, bool IsHappyHourEligible, string ETag);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record RecipeLineRequest(int IngredientId, decimal QuantityRequired);

public record CreateMenuItemRequest(string Name, string Category, decimal Price, bool IsHappyHourEligible, IReadOnlyList<RecipeLineRequest> Recipe);

public record SetAvailabilityRequest(bool IsAvailable);
