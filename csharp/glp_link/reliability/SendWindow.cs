namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Per-link bounded send window for backpressure (FR-025, SC-013). The egress
/// drainer acquires one credit per in-flight frame and releases it when the frame
/// is acked/consumed by the peer. When all <see cref="Capacity"/> credits are
/// out, the producer SUSPENDS (awaits <see cref="AcquireAsync"/>) rather than
/// buffering unboundedly — the host-side reflection of FCP producer suspension, so
/// the outbound queue stays bounded (no OOM).
/// </summary>
/// <remarks>
/// The default window is N=8 (<see cref="Seam.LinkOptions.BackpressureWindow"/>),
/// scheme-overridable below the seam. Each link owns its own window, so a full
/// window on one link never blocks an independent link (no head-of-line blocking
/// across links — FR-025). Over-releasing (acking a frame that was never in
/// flight) throws, surfacing the bug at its cause rather than corrupting the count.
/// </remarks>
public sealed class SendWindow : IDisposable
{
    private readonly SemaphoreSlim _credits;

    /// <summary>The window size N — the maximum number of in-flight frames.</summary>
    public int Capacity { get; }

    /// <param name="window">Window size N (default 8). Must be ≥ 1.</param>
    public SendWindow(int window = 8)
    {
        if (window < 1) throw new ArgumentOutOfRangeException(nameof(window), window, "window must be >= 1");
        Capacity = window;
        _credits = new SemaphoreSlim(window, window);
    }

    /// <summary>Credits currently free (frames that may be sent without suspending).</summary>
    public int Available => _credits.CurrentCount;

    /// <summary>Frames currently in flight (credits acquired, not yet released).</summary>
    public int InFlight => Capacity - _credits.CurrentCount;

    /// <summary>
    /// Try to acquire a credit without suspending. Returns <c>false</c> when the
    /// window is full (the caller must then <see cref="AcquireAsync"/> or back off).
    /// </summary>
    public bool TryAcquire() => _credits.Wait(0);

    /// <summary>
    /// Acquire a credit, SUSPENDING until one is free. This is the backpressure
    /// point: a producer outrunning the consumer parks here until an ack releases a
    /// credit.
    /// </summary>
    public Task AcquireAsync(CancellationToken ct = default) => _credits.WaitAsync(ct);

    /// <summary>
    /// Release a credit on ack/consume of one in-flight frame. Throws
    /// <see cref="SemaphoreFullException"/> if the window is already full (an ack
    /// for a frame that was never in flight — a double-ack bug).
    /// </summary>
    public void Release() => _credits.Release();

    public void Dispose() => _credits.Dispose();
}
