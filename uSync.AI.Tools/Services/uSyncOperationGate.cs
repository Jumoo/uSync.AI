namespace uSync.AI.Tools.Services;

/// <summary>
/// Fail-fast, in-process gate so two agent-driven uSync operations can't overlap. uSync's own
/// import path waits up to 30 minutes on an internal semaphore; a tool call blocking that long
/// would hang the agent's turn. This gate is taken first with no wait, so a second run is
/// refused immediately with a clear message instead.
/// </summary>
/// <remarks>
/// Public and registered with <c>TryAddSingleton</c> so uSync.AI.Complete's publisher tools
/// contend for the same gate as a local import or export.
/// </remarks>
public interface IuSyncOperationGate
{
    /// <summary>A lease to dispose when the operation ends, or null if one is already running.</summary>
    IDisposable? TryAcquire();
}

/// <inheritdoc cref="IuSyncOperationGate"/>
public sealed class uSyncOperationGate : IuSyncOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public IDisposable? TryAcquire() => _semaphore.Wait(0) ? new Lease(_semaphore) : null;

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) semaphore.Release();
        }
    }
}
