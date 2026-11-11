using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DineDesk.Api.Data;

/// <summary>
/// Generates a new concurrency token on every insert and update. SQL Server uses a native
/// rowversion column instead, but this keeps the same byte[] concurrency token portable to
/// providers (like SQLite in tests) that have no equivalent computed column type.
/// </summary>
public class RowVersionValueGenerator : ValueGenerator<byte[]>
{
    public override bool GeneratesTemporaryValues => false;

    public override byte[] Next(EntityEntry entry) => Guid.NewGuid().ToByteArray();
}
