using DineDesk.Api.Domain.Enums;

namespace DineDesk.Api.Domain.Entities;

public class DiningTable
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Free;

    public ICollection<Tab> Tabs { get; set; } = new List<Tab>();
}
