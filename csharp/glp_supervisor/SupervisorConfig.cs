// SupervisorConfig — the supervision knobs (T025; data-model.md "SupervisorConfig",
// contracts/supervision.md).

namespace GlpRuntime.Supervisor;

public sealed record SupervisorConfig
{
    /// <summary>Path to the engine host binary (glp_engine_host.exe).</summary>
    public required string EngineBinary { get; init; }

    /// <summary>The engine's --listen endpoint (host:port); also the ping target.</summary>
    public required string Listen { get; init; }

    /// <summary>Snapshot store root (the engine's --store; also read for the taxonomy).</summary>
    public required string StoreRoot { get; init; }

    /// <summary>Liveness ping cadence (contracts/supervision.md).</summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Missing-ACK budget: no ACK within this after a ping ⇒ death detected.</summary>
    public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How long a freshly started engine may take to answer its first ping.</summary>
    public TimeSpan StartupBudget { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Restart backoff: initial delay.</summary>
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Restart backoff: multiplier per consecutive crash.</summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>Restart backoff: ceiling.</summary>
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>repeated_immediate_crash window (DEF-F2 taxonomy).</summary>
    public TimeSpan CrashWindow { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Crashes within the window that classify repeated_immediate_crash.</summary>
    public int CrashThreshold { get; init; } = 3;

    /// <summary>The engine identity the store scopes to (mirrors the engine's own derivation).</summary>
    public string EngineIdentity =>
        $"engine-{Listen[(Listen.LastIndexOf(':') + 1)..]}";
}
