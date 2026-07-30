// CrashLog — append-only crash-record persistence + operator queries (T026;
// FR-024, data-model.md "CrashRecord").
//
// One JSON line per COMPLETED crash record under
// <store_root>/supervisor/crash-log.jsonl — appended once the restart outcome
// is known (restored(seq) | unrecoverable(reason)), never rewritten. The
// heartbeat/status surface is a separate small status.json the supervisor
// overwrites in place (derived state, not history).

using System.Text.Json;

namespace GlpRuntime.Supervisor;

/// <summary>How the death was detected (contracts/supervision.md).</summary>
public enum CrashDetection { Exit, PingTimeout }

/// <summary>One completed crash record (data-model.md; append-only, FR-024).</summary>
public sealed record CrashRecord(
    DateTimeOffset TimestampUtc,
    string EngineIdentity,
    int? ExitCode,
    CrashDetection Detection,
    string RestartOutcome,       // "restored(seq)" | "unrecoverable(reason)"
    double BackoffAppliedMs);

/// <summary>The supervisor's derived live-status surface (overwritten in place).</summary>
public sealed record SupervisorStatus(
    DateTimeOffset UpdatedUtc,
    string EngineIdentity,
    string EngineState,          // starting | healthy | restarting | stopped(<reason>)
    int? EnginePid,
    DateTimeOffset? LastHeartbeatUtc,
    ulong? LastSnapshotSeq,
    int CrashCount);

public sealed class CrashLog
{
    private readonly string _dir;
    private readonly string _logPath;
    private readonly string _statusPath;
    private readonly object _lock = new();

    public CrashLog(string storeRoot)
    {
        _dir = Path.Combine(storeRoot, "supervisor");
        Directory.CreateDirectory(_dir);
        _logPath = Path.Combine(_dir, "crash-log.jsonl");
        _statusPath = Path.Combine(_dir, "status.json");
    }

    /// <summary>Append one completed record (append-only — never rewritten).</summary>
    public void Append(CrashRecord record)
    {
        lock (_lock)
            File.AppendAllText(_logPath, JsonSerializer.Serialize(record) + Environment.NewLine);
    }

    /// <summary>All records, oldest first (FR-024 history query).</summary>
    public IReadOnlyList<CrashRecord> History()
    {
        lock (_lock)
        {
            if (!File.Exists(_logPath))
                return Array.Empty<CrashRecord>();
            return File.ReadAllLines(_logPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<CrashRecord>(l)
                    ?? throw new InvalidOperationException($"corrupt crash-log line: {l}"))
                .ToList();
        }
    }

    /// <summary>Overwrite the derived status surface (not history — FR-024's status query).</summary>
    public void WriteStatus(SupervisorStatus status)
    {
        lock (_lock)
            File.WriteAllText(_statusPath,
                JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The current status, or null when the supervisor never ran here.</summary>
    public SupervisorStatus? ReadStatus()
    {
        lock (_lock)
        {
            if (!File.Exists(_statusPath)) return null;
            return JsonSerializer.Deserialize<SupervisorStatus>(File.ReadAllText(_statusPath));
        }
    }
}
