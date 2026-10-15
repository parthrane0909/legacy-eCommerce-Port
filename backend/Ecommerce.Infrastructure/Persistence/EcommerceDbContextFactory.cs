using Microsoft.EntityFrameworkCore.Design;

namespace Ecommerce.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> and <c>database update</c>.
/// It reads the connection string from configuration (or the
/// <c>SHOPHUB_ConnectionStrings__DefaultConnection</c> environment variable) and never
/// hardcodes a password.
/// </summary>
public sealed class EcommerceDbContextFactory : IDesignTimeDbContextFactory<EcommerceDbContext>
{
    public EcommerceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set it in appsettings.json "
                + "or export SHOPHUB_ConnectionStrings__DefaultConnection.");

        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(EcommerceDbContext).Assembly.FullName))
            .Options;

        return new EcommerceDbContext(options);
    }
}