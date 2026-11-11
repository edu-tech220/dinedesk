namespace DineDesk.Api.Domain.Enums;

public enum UserRole
{
    Server,
    Kitchen,
    Manager,
    Admin
}

public enum TableStatus
{
    Free,
    Occupied
}

public enum TabStatus
{
    Open,
    Closed
}

public enum OrderItemStatus
{
    Received,
    Preparing,
    Ready,
    Served,
    Voided
}

public enum PaymentMethod
{
    Cash,
    Card
}
