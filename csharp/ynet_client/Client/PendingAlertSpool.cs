// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace Ynet.Client;

/// <summary>One notification waiting for the agent to decide what to do with it.</summary>
public sealed record PendingAlert(
    string AlertId,
    string MessageId,
    string Origin,
    string Summary,
    DateTimeOffset RaisedUtc,
    int Presentations);

/// <summary>
/// The durable half of M6-f. An alert that interrupts is a preemption; an alert that waits to be
/// asked for is a poll. M6-f is neither: the notification is DELIVERED and DURABLE, and the moment
/// it is consumed is the agent's choice.
///
/// That only works if the notification outlives the instant it was raised, so every alert is a
/// file on disk before the raiser is told it succeeded. An alert raised while the agent was
/// mid-task, asleep, compacting or absent is still there afterwards, and is re-presented with an
/// incremented count rather than quietly re-raised as if new.
///
/// Draining is explicit and idempotent: the agent drains an alert by id when it has actually
/// handled it. Nothing here ever drains on the agent's behalf, because "the agent saw it" and
/// "the agent handled it" are different facts and only the second one may delete the record.
/// </summary>
public sealed class PendingAlertSpool
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _gate = new();

    public PendingAlertSpool(string directory)
    {
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        System.IO.Directory.CreateDirectory(Directory);
    }

    /// <summary>Where the spool lives. Outside any repo, so a clone or clean cannot destroy it.</summary>
    public string Directory { get; }

    /// <summary>The default per-user spool: %LOCALAPPDATA%\glpnet\ynet-client\alerts (or XDG equivalent).</summary>
    public static string DefaultDirectory
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(baseDir, "glpnet", "ynet-client", "alerts");
        }
    }

    /// <summary>
    /// Record an alert durably and return it. Written to a temp file and moved into place, so a
    /// crash mid-write leaves either the previous state or the complete new one, never a torn file.
    /// Re-raising the same MessageId re-presents the existing alert instead of creating a second.
    /// </summary>
    public PendingAlert Raise(string messageId, string origin, string summary, DateTimeOffset? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        var now = nowUtc ?? DateTimeOffset.UtcNow;

        lock (_gate)
        {
            var existing = ReadAll().FirstOrDefault(a => a.MessageId == messageId);
            var alert = existing is null
                ? new PendingAlert(NewAlertId(messageId), messageId, origin, summary, now, 1)
                : existing with { Presentations = existing.Presentations + 1 };

            Write(alert);
            return alert;
        }
    }

    /// <summary>Every undrained alert, oldest first. Survives a restart of the client and of the agent.</summary>
    public IReadOnlyList<PendingAlert> Undrained()
    {
        lock (_gate)
        {
            return ReadAll().OrderBy(a => a.RaisedUtc).ThenBy(a => a.AlertId, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>
    /// Mark one alert handled. Returns true when this call removed it, false when there was
    /// nothing to remove — so a repeated drain is a recorded no-op rather than an error.
    /// </summary>
    public bool Drain(string alertId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alertId);
        lock (_gate)
        {
            var path = PathFor(alertId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    /// <summary>Count of alerts still waiting for the agent.</summary>
    public int Count => Undrained().Count;

    private List<PendingAlert> ReadAll()
    {
        var result = new List<PendingAlert>();
        foreach (var f in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            try
            {
                var a = JsonSerializer.Deserialize<PendingAlert>(File.ReadAllText(f));
                if (a is not null) result.Add(a);
            }
            catch (JsonException)
            {
                // An unreadable spool entry is quarantined, never deleted: losing an alert to make
                // a listing tidy is the one failure this class exists to prevent.
                var quarantine = f + ".unreadable";
                if (!File.Exists(quarantine)) File.Move(f, quarantine);
            }
        }
        return result;
    }

    private void Write(PendingAlert alert)
    {
        var path = PathFor(alert.AlertId);

        // Two corrections adopted from @shiras-glpnet's TOCTOU finding (commit cd085e3c,
        // 2026-09-05T11:58Z), after running their grep against this file rather than assuming a
        // brand-new class was exempt. Their third point — that File.Move(overwrite: false) is not
        // an atomic exclusive claim — does not apply here, because this writer claims nothing and
        // replaces deliberately.
        //
        // 1. A UNIQUE temp name. "path + .tmp" is shared by every concurrent writer of the same
        //    alert, so two processes (a running receiver and a one-shot inject) could interleave
        //    a half-written file into the rename.
        // 2. FLUSH TO DISK before the rename. File.WriteAllText returns once the bytes are with
        //    the OS, not once they are on the medium — so a power loss could rename a durable-
        //    looking name onto content that was never written. This class exists precisely to
        //    promise "durable before the agent is told", and that promise is what the flush buys.
        var tmp = $"{path}.tmp-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}";
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(alert, Json));
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDeleteQuietly(tmp);
            throw;
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* a leftover temp file must never mask the original failure */ }
        catch (UnauthorizedAccessException) { /* likewise */ }
    }

    private string PathFor(string alertId) => Path.Combine(Directory, alertId + ".json");

    private static string NewAlertId(string messageId)
    {
        var safe = new string(messageId.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').Take(48).ToArray());
        if (safe.Length == 0) safe = "msg";
        return $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{safe}";
    }
}
