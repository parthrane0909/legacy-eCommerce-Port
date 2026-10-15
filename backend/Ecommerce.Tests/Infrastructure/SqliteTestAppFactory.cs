namespace Ecommerce.Tests.Infrastructure;

/// <summary>
/// Test host backed by a SQLite database, as permitted by the assignment.
/// </summary>
/// <remarks>
/// A temporary <b>file</b> database is used rather than SQLite in-memory. SQLite's shared-cache
/// in-memory mode loses its database as soon as the last connection closes, and EF's DbContext pool
/// closes connections during host start-up and disposal - which produced intermittent
/// "no such table" failures. A per-factory temp file gives each factory an isolated, durable
/// database for its whole lifetime. The file is deleted on dispose.
/// </remarks>
public class SqliteTestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"shophub-test-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Rate limiting is disabled for the suite by default. A few tests exercise the limiter
    /// explicitly, and tests within a class share an IP partition, so leaving it on makes
    /// otherwise unrelated tests fail with 429.
    /// </summary>
    public const string DisableRateLimitingConfig = "RateLimiting:Enabled";

    public const string AdminEmail = "admin@shophub.test";
    public const string AdminPassword = "Admin@Test12345";
    public const string CustomerEmail = "customer@shophub.test";
    public const string CustomerPassword = "Customer@Test12345";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "ShopHubApiTests",
                ["Jwt:Audience"] = "ShopHubClientTests",

                // Deterministic test signing key. Not a secret: it never leaves the test process.
                ["Jwt:SigningKey"] = "test-signing-key-not-a-secret-0123456789abcdef",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",

                // Tests build the schema with EnsureCreated from the model rather than replaying the
                // SQL Server migration script. AutoMigrate must stay off, otherwise the host would
                // run Migrate() and fail the provider-specific pending-model-changes check.
                ["Database:AutoMigrate"] = "false",
                [DisableRateLimitingConfig] = "false",
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Seed:CustomerEmail"] = CustomerEmail,
                ["Seed:CustomerPassword"] = CustomerPassword,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the SQL Server DbContext registration with SQLite. Every EF-owned
            // registration must go, otherwise two providers end up registered and EF refuses to start.
            var efRegistrations = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<EcommerceDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)
                            || d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in efRegistrations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<EcommerceDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    /// <summary>
    /// Forces the host to start and the schema to be created. Called by every helper so that no
    /// test touches the database before migrations and seeding have run.
    /// </summary>
    public async Task EnsureInitialisedAsync()
    {
        await EnsureDatabaseAsync();
        await SeedAsync();
    }

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _databaseReady;
    private volatile bool _seeded;

    private async Task EnsureDatabaseAsync()
    {
        if (_databaseReady)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_databaseReady)
            {
                return;
            }

            // Resolve the DbContext to force host start-up, then create the schema.
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
            await db.Database.EnsureCreatedAsync();

            _databaseReady = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task SeedAsync()
    {
        if (_seeded)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var seeder = scope.ServiceProvider
                .GetRequiredService<Ecommerce.Infrastructure.Seeding.DatabaseSeeder>();
            await seeder.SeedAsync();

            _seeded = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Seeds the catalogue plus the admin and sample customer accounts.</summary>
    public Task SeedTestUsersAsync() => EnsureInitialisedAsync();

    /// <summary>Runs a mutation against the test database.</summary>
    public async Task<T> WithDbAsync<T>(Func<EcommerceDbContext, Task<T>> action)
    {
        await EnsureInitialisedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        return await action(db);
    }

    public async Task WithDbAsync(Func<EcommerceDbContext, Task> action)
    {
        await EnsureInitialisedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        await action(db);
    }

    /// <summary>Reads from the test database.</summary>
    public async Task<T> ReadDbAsync<T>(Func<EcommerceDbContext, Task<T>> action)
    {
        await EnsureInitialisedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        return await action(db);
    }

    /// <summary>Returns a client that already carries a valid Bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        await EnsureInitialisedAsync();

        var client = CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password), JsonOptions);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>
    /// Returns a client with cookie handling enabled. Blocks briefly on first call so the schema
    /// and seed data exist before the caller issues a request.
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        EnsureInitialisedAsync().GetAwaiter().GetResult();

        return CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _initLock.Dispose();

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test run over.
        }
    }
}