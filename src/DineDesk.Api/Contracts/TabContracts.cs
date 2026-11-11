using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Contracts;

public record TabItemResponse(int Id, string MenuItemName, int Quantity, decimal UnitPrice, OrderItemStatus Status, string? GuestLabel, string ETag);

public record TabOrderResponse(int OrderId, DateTime PlacedAtUtc, IReadOnlyList<TabItemResponse> Items);

public record TabResponse(int Id, int DiningTableId, string TableNumber, int GuestCount, TabStatus Status, DateTime OpenedAtUtc, IReadOnlyList<TabOrderResponse> Orders);

public record BillResponse(decimal Subtotal, decimal TaxRate, decimal Tax, decimal Tip, decimal Total, decimal AmountPaid, decimal BalanceDue);

public record OrderLineRequest(int MenuItemId, int Quantity, string? GuestLabel);

public record PlaceOrderRequest(IReadOnlyList<OrderLineRequest> Items);

public record SplitPreviewRequest(int SplitCount);

public record AddPaymentRequest(string? GuestLabel, decimal Amount, PaymentMethod Method);
