using DineDesk.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DineDesk.Api.Tests.Infrastructure;

/// <summary>Boots the real Program.cs pipeline against an isolated in-memory SQLite database
/// per instance, so each test class gets its own fresh schema and seed data with no external
/// SQL Server dependency. Rate limiting is relaxed so a burst of test requests never trips it.</summary>
public class DineDeskApiFactory : WebApplicationFactory<Program>
{
    // A named shared-cache in-memory database (instead of a single shared SqliteConnection)
    // so concurrent requests each open their own ADO.NET connection - required for the
    // concurrency tests, since one SqliteConnection object can't run commands from multiple
    // threads at once. "Default Timeout" makes SQLite wait out writer contention (SQLITE_BUSY)
    // instead of throwing, mirroring how a real database would just block briefly.
    private readonly string _connectionString = $"DataSource=file:{Guid.NewGuid()}?mode=memory&cache=shared;Default Timeout=5";
    private readonly SqliteConnection _keepAliveConnection;

    public DineDeskApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "100000",
                ["RateLimiting:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DineDeskDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DineDeskDbContext>(options => options.UseSqlite(_connectionString));

            // Surface the real exception in the ProblemDetails body on a 500 instead of a
            // generic message, so a failing test's output tells you what actually broke.
            services.PostConfigure<ProblemDetailsOptions>(o =>
            {
                var previous = o.CustomizeProblemDetails;
                o.CustomizeProblemDetails = ctx =>
                {
                    previous?.Invoke(ctx);
                    if (ctx.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error is { } ex)
                    {
                        ctx.ProblemDetails.Extensions["exception"] = ex.ToString();
                    }
                };
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
