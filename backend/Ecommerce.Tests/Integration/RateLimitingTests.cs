using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ecommerce.Tests.Infrastructure;

/// <summary>
/// Test host with rate limiting left enabled, used only by the tests that assert throttling
/// behaviour. Separate from <see cref="SqliteTestAppFactory"/> so that suite-wide 429s do not
/// leak into unrelated tests.
/// </summary>
public class RateLimitedTestAppFactory : SqliteTestAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DisableRateLimitingConfig] = "true",
            }));
    }
}

/// <summary>
/// Asserts that the authentication endpoints are rate limited per client IP.
/// The whole budget is consumed inside a single test because the limiter partitions on IP,
/// which is identical for every request the test host makes.
/// </summary>
public class RateLimitingTests : IClassFixture<RateLimitedTestAppFactory>
{
    private readonly RateLimitedTestAppFactory _factory;

    public RateLimitingTests(RateLimitedTestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_attempts_are_answered_normally_then_throttled()
    {
        var client = _factory.CreateAnonymousClient();
        await _factory.SeedTestUsersAsync();

        const int permitLimit = 10;
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < permitLimit + 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "flood@shophub.test",
                password = "Wr0ng!Password",
            }, SqliteTestAppFactory.JsonOptions);

            statuses.Add(response.StatusCode);
        }

        statuses.Take(permitLimit).Should().AllSatisfy(
            s => s.Should().Be(HttpStatusCode.Unauthorized,
                "requests within the budget are handled normally"));

        statuses.Skip(permitLimit).Should().AllSatisfy(
            s => s.Should().Be(HttpStatusCode.TooManyRequests,
                "requests beyond the budget are rejected with 429"));
    }
}