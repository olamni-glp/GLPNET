// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Recorded reversals (feature 102, T042).
//
// Contract federation-config.md G5 / FR-024, FR-025, SC-009.
//
// THE REVERSAL IS DATA, NOT DOCUMENTATION. A runbook that says "and to undo it, remove the rule"
// is a reversal nobody can execute six weeks later on a host they did not configure. Each enabling
// change appends the exact undo alongside itself, and `revert --all` replays them in reverse order.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>One recorded change and the exact action that undoes it.</summary>
public sealed record RecordedChange
{
    [JsonPropertyName("utc")] public string Utc { get; init; } = "";
    [JsonPropertyName("what")] public string What { get; init; } = "";

    /// <summary>How to undo it — a command, or an instruction a person can follow verbatim.</summary>
    [JsonPropertyName("reversal")] public string Reversal { get; init; } = "";

    /// <summary>Prior content, verbatim, when the change overwrote something restorable.</summary>
    [JsonPropertyName("prior")] public string? Prior { get; init; }

    /// <summary>Why the change was made — so a later reader can judge whether to keep it.</summary>
    [JsonPropertyName("rationale")] public string Rationale { get; init; } = "";
}

/// <summary>Append-only ledger of every configuration change made to enable federation.</summary>
public sealed class ChangeLedger
{
    private readonly string _path;

    public ChangeLedger(string path) => _path = path;

    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FederationConfig.DefaultPath())!, "changes.jsonl");

    /// <summary>Record a change together with its reversal. Appending only — never rewritten.</summary>
    public void Record(string what, string reversal, string rationale, string? prior = null)
    {
        var entry = new RecordedChange
        {
            Utc = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            What = what,
            Reversal = reversal,
            Rationale = rationale,
            Prior = prior,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        // CROSS-PROCESS, like the board log. Two documented CLI processes record changes
        // concurrently, and on Windows one of them took a sharing violation AFTER its config or key
        // change had already been applied — leaving a change with no recorded reversal, which is
        // precisely the FR-025 guarantee this file exists to provide.
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>Every recorded change, oldest first.</summary>
    public IReadOnlyList<RecordedChange> All()
    {
        if (!File.Exists(_path)) return Array.Empty<RecordedChange>();
        var outp = new List<RecordedChange>();
        foreach (var line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var e = JsonSerializer.Deserialize<RecordedChange>(line);
            if (e is not null) outp.Add(e);
        }
        return outp;
    }

    /// <summary>
    /// The reversals to run, NEWEST FIRST. Reverse order matters: undoing a config write before the
    /// thing that depended on it would leave the host in a state neither the operator nor the
    /// ledger describes.
    /// </summary>
    public IReadOnlyList<RecordedChange> ReversalPlan() => All().Reverse().ToList();
}
