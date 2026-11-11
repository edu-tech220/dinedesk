using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Contracts;

public record KitchenQueueItemResponse(int Id, string MenuItemName, int Quantity, OrderItemStatus Status, string TableNumber, DateTime PlacedAtUtc, string ETag);

public record UpdateItemStatusRequest(OrderItemStatus NewStatus);

public record VoidItemRequest(string Reason);
