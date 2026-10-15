namespace Ecommerce.Api.Infrastructure;

/// <summary>
/// Serialises database access for the duration of a request.
/// </summary>
/// <remarks>
/// EF Core registers <c>EcommerceDbContext</c> as a scoped service, so a single context instance is
/// shared by every service in one request. EF Core forbids two concurrent operations on the same
/// context ("A second operation was started on this context instance"). Rather than relying on
/// every endpoint author remembering not to use <c>Task.WhenAll</c> over queries, this gate makes
/// it safe by construction.
/// <para>
/// Cost is negligible: the queries are small and indexed, so serialising them adds little wall-clock
/// time, and correctness beats micro-optimisation here.
/// </para>
/// </remarks>
public sealed class ConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();
}