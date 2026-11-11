using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Contracts;

public record DiningTableResponse(int Id, string Number, int Capacity, TableStatus Status);

public record OpenTabRequest(int GuestCount);

public record OpenTabResponse(int TabId, int DiningTableId, DateTime OpenedAtUtc);
