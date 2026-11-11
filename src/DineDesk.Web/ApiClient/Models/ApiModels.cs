namespace DineDesk.Web.ApiClient.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Username, string Role);

public record DiningTableResponse(int Id, string Number, int Capacity, string Status);

public record OpenTabRequest(int GuestCount);

public record OpenTabResponse(int TabId, int DiningTableId, DateTime OpenedAtUtc);

public record OpenTabIdResponse(int TabId);

public record TabItemResponse(int Id, string MenuItemName, int Quantity, decimal UnitPrice, string Status, string? GuestLabel, string ETag);

public record TabOrderResponse(int OrderId, DateTime PlacedAtUtc, IReadOnlyList<TabItemResponse> Items);

public record TabResponse(int Id, int DiningTableId, string TableNumber, int GuestCount, string Status, DateTime OpenedAtUtc, IReadOnlyList<TabOrderResponse> Orders);

public record BillResponse(decimal Subtotal, decimal TaxRate, decimal Tax, decimal Tip, decimal Total, decimal AmountPaid, decimal BalanceDue);

public record OrderLineRequest(int MenuItemId, int Quantity, string? GuestLabel);

public record PlaceOrderRequest(IReadOnlyList<OrderLineRequest> Items);

public record SplitPreviewRequest(int SplitCount);

public record AddPaymentRequest(string? GuestLabel, decimal Amount, string Method);

public record MenuItemResponse(int Id, string Name, string Category, decimal Price, bool IsAvailable, bool IsHappyHourEligible, string ETag);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record RecipeLineRequest(int IngredientId, decimal QuantityRequired);

public record CreateMenuItemRequest(string Name, string Category, decimal Price, bool IsHappyHourEligible, IReadOnlyList<RecipeLineRequest> Recipe);

public record SetAvailabilityRequest(bool IsAvailable);

public record IngredientResponse(int Id, string Name, string UnitOfMeasure, decimal QuantityInStock, decimal ReorderThreshold);

public record RestockRequest(decimal QuantityToAdd);

public record KitchenQueueItemResponse(int Id, string MenuItemName, int Quantity, string Status, string TableNumber, DateTime PlacedAtUtc, string ETag);

public record UpdateItemStatusRequest(string NewStatus);

public record VoidItemRequest(string Reason);

public record ApiProblem(string? Title, string? Detail, int? Status);
