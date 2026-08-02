// EngineSession — the engine host's lifecycle state machine (T011;
// data-model.md "EngineSession"). One session per host process; one client
// connection at a time (FR-002).

namespace GlpRuntime.EngineHost;

/// <summary>Engine lifecycle states (data-model.md).</summary>
public enum EngineState
{
    Starting,
    Restoring,
    Serving,
    Quiescing,
    Snapshotting,
    ShuttingDown,
}

/// <summary>
/// The engine host's session: stable identity (store scoping + snapshot seq
/// namespace) plus the lifecycle state. Transitions are checked loudly — an
/// illegal transition is a host bug, never silently absorbed (data-model.md:
/// starting→(restoring|serving); restoring→serving only after full restore
/// (FR-030); serving→snapshotting only when quiescent (FR-014);
/// snapshotting→serving; serving→shutting_down).
/// </summary>
public sealed class EngineSession
{
    private static readonly Dictionary<EngineState, EngineState[]> Allowed = new()
    {
        [EngineState.Starting] = new[] { EngineState.Restoring, EngineState.Serving },
        [EngineState.Restoring] = new[] { EngineState.Serving },
        [EngineState.Serving] = new[] { EngineState.Quiescing, EngineState.Snapshotting, EngineState.ShuttingDown },
        [EngineState.Quiescing] = new[] { EngineState.Snapshotting, EngineState.Serving },
        [EngineState.Snapshotting] = new[] { EngineState.Serving, EngineState.ShuttingDown },
        [EngineState.ShuttingDown] = Array.Empty<EngineState>(),
    };

    public string EngineIdentity { get; }

    public EngineState State { get; private set; }

    public EngineSession(string engineIdentity)
    {
        EngineIdentity = engineIdentity;
        State = EngineState.Starting;
    }

    /// <summary>Move to <paramref name="next"/>; throws on an illegal transition.</summary>
    public void TransitionTo(EngineState next)
    {
        if (!Allowed[State].Contains(next))
            throw new InvalidOperationException(
                $"illegal EngineSession transition {State} → {next} (engine '{EngineIdentity}')");
        State = next;
    }

    /// <summary>Lower-snake state word for STATUS bodies (data-model naming).</summary>
    public string StateWord => State switch
    {
        EngineState.Starting => "starting",
        EngineState.Restoring => "restoring",
        EngineState.Serving => "serving",
        EngineState.Quiescing => "quiescing",
        EngineState.Snapshotting => "snapshotting",
        EngineState.ShuttingDown => "shutting_down",
        _ => throw new ArgumentOutOfRangeException(nameof(State)),
    };
}
