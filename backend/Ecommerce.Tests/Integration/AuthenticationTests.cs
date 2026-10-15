using Ecommerce.Core.Dtos.Auth;
using Ecommerce.Tests.Infrastructure;

namespace Ecommerce.Tests.Integration;

/// <summary>
/// Integration tests for the authentication endpoints, using the real HTTP pipeline through
/// <c>WebApplicationFactory</c> against a SQLite in-memory database.
/// </summary>
public class AuthenticationTests : IClassFixture<SqliteTestAppFactory>
{
    private readonly SqliteTestAppFactory _factory;

    public AuthenticationTests(SqliteTestAppFactory factory) => _factory = factory;

    private static object RegisterBody(string email = "new.user@shophub.test") => new
    {
        email,
        password = "Str0ng!Password",
        firstName = "Asha",
        lastName = "Nair",
    };

    [Fact]
    public async Task Register_creates_a_customer_and_returns_an_access_token()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            RegisterBody(), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(SqliteTestAppFactory.JsonOptions);

        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.TokenType.Should().Be("Bearer");
        auth.ExpiresInSeconds.Should().BeGreaterThan(0);
        auth.User.Email.Should().Be("new.user@shophub.test");
    }

    /// <summary>
    /// The assignment requires that a customer can never self-assign the Admin role. The request DTO
    /// has no role field at all, and the service hardcodes Customer.
    /// </summary>
    [Fact]
    public async Task Register_never_grants_the_admin_role()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not.admin@shophub.test",
            password = "Str0ng!Password",
            firstName = "Mallory",
            lastName = "Admin",
            role = "Admin", // Ignored: not part of the contract.
        }, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(SqliteTestAppFactory.JsonOptions);
        auth!.User.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Register_rejects_a_weak_password()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "weak@shophub.test",
            password = "abc", // Rejected by FluentValidation.
            firstName = "Weak",
            lastName = "Password",
        }, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_409()
    {
        var client = _factory.CreateAnonymousClient();

        await client.PostAsJsonAsync("/api/auth/register", RegisterBody("dupe@shophub.test"),
            SqliteTestAppFactory.JsonOptions);

        var second = await client.PostAsJsonAsync("/api/auth/register", RegisterBody("dupe@shophub.test"),
            SqliteTestAppFactory.JsonOptions);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_valid_credentials_succeeds()
    {
        var client = _factory.CreateAnonymousClient();
        await SeedAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(SqliteTestAppFactory.JsonOptions);
        auth!.User.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_401()
    {
        var client = _factory.CreateAnonymousClient();
        await SeedAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, "Wr0ng!Password"),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A generic failure message avoids disclosing whether the account exists.</summary>
    [Fact]
    public async Task Login_failure_does_not_reveal_whether_the_account_exists()
    {
        var client = _factory.CreateAnonymousClient();
        await SeedAsync();

        var unknownUser = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody@shophub.test", "Wr0ng!Password"), SqliteTestAppFactory.JsonOptions);

        var knownUser = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, "Wr0ng!Password"), SqliteTestAppFactory.JsonOptions);

        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        knownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Compare the meaningful parts only: each response carries its own traceId and timestamp.
        var unknown = await unknownUser.Content.ReadFromJsonAsync<ProblemShape>(SqliteTestAppFactory.JsonOptions);
        var known = await knownUser.Content.ReadFromJsonAsync<ProblemShape>(SqliteTestAppFactory.JsonOptions);

        known!.Title.Should().Be(unknown!.Title);
        known.Detail.Should().Be(unknown.Detail);
        known.Status.Should().Be(unknown.Status);
    }

    private sealed record ProblemShape(string? Type, string? Title, int? Status, string? Detail);

    [Fact]
    public async Task Me_returns_the_current_user_for_a_valid_token()
    {
        await SeedAsync();
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>(SqliteTestAppFactory.JsonOptions);
        user!.Email.Should().Be(SqliteTestAppFactory.CustomerEmail);
    }

    [Fact]
    public async Task Me_rejects_an_anonymous_caller_with_401()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_issues_a_new_access_token_and_rotates_the_cookie()
    {
        await SeedAsync();
        var client = _factory.CreateAnonymousClient();

        await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword),
            SqliteTestAppFactory.JsonOptions);

        var response = await client.PostAsJsonAsync<RefreshRequest?>("/api/auth/refresh", null, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(SqliteTestAppFactory.JsonOptions);
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_without_a_token_is_401()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync<RefreshRequest?>("/api/auth/refresh", null, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Refresh-token rotation: replaying a consumed token is treated as reuse and rejected.
    /// </summary>
    [Fact]
    public async Task Replaying_a_rotated_refresh_token_is_rejected()
    {
        await SeedAsync();
        var client = _factory.CreateAnonymousClient();

        await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword),
            SqliteTestAppFactory.JsonOptions);

        // Read the refresh cookie out of the login response's Set-Cookie header, so the test
        // does not depend on the test client's cookie container implementation.
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword),
            SqliteTestAppFactory.JsonOptions);

        var stale = ExtractRefreshCookie(login);
        stale.Should().NotBeNullOrWhiteSpace("login issues the refresh cookie");

        // Rotate using the stale token explicitly; the token is now consumed.
        var rotate = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(stale), SqliteTestAppFactory.JsonOptions);

        rotate.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = ExtractRefreshCookie(rotate);
        rotated.Should().NotBe(stale, "rotation must issue a different token");

        // Replay the consumed token on a client with an empty cookie jar. A cookie takes
        // precedence over the body value, so reusing the original client would silently
        // resend the rotated cookie instead of the stale token.
        var freshClient = _factory.CreateAnonymousClient();

        var replay = await freshClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(stale), SqliteTestAppFactory.JsonOptions);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads shophub_refresh from a response's Set-Cookie headers.</summary>
    private static string? ExtractRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("shophub_refresh=", StringComparison.Ordinal))
                ?.Split(';')[0]["shophub_refresh=".Length..]
            : null;

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        await SeedAsync();
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private Task SeedAsync() => _factory.SeedTestUsersAsync();
}