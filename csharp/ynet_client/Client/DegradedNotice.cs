// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace Ynet.Client;

/// <summary>
/// Writes the fleet-visible record that a host is running on a plane it did not ask for.
///
/// <para>
/// <b>Why this exists</b> (engineer ruling Q-G34-02 → C, 2026-09-06). Loud fallback alone makes
/// every host individually honest and leaves the fleet blind: four hosts each correctly reporting
/// "running on file, wire unavailable" look, from the outside, exactly like four healthy hosts,
/// and the fleet can sit entirely on the shared volume for a week with nobody counting. The engineer
/// chose this option over plain loud fallback for that reason.
/// </para>
///
/// <para>
/// 🔴 <b>Additive only.</b> One file per degradation event, never an overwrite. The COOP tree has
/// already lost 2990 lines once to a hand-written fan-out that clobbered an existing file
/// (2026-08-16), and the rule that came out of that incident is enforced here rather than
/// documented: an existing destination is left alone.
/// </para>
///
/// <para>
/// 🔴 <b>Best-effort by construction.</b> Every failure path returns quietly. The entire purpose of
/// degrading is to keep a damaged host receiving; a notice that cannot be written must never be the
/// reason it stops.
/// </para>
/// </summary>
public sealed class DegradedNotice : IDegradedNotifier
{
    /// <summary>Where degraded notices collect. A dedicated directory, not the broadcast root:
    /// these are machine-written telemetry and must not be mistaken for lane broadcasts.</summary>
    public const string DirectoryName = "_ynet-degraded";

    private readonly string? _coopRoot;
    private readonly string _lane;
    private readonly string _host;
    private readonly Func<DateTimeOffset> _now;

    public DegradedNotice(string? coopRoot, string lane, string? host = null, Func<DateTimeOffset>? now = null)
    {
        _coopRoot = coopRoot;
        _lane = string.IsNullOrWhiteSpace(lane) ? "unknown-lane" : lane;
        _host = host ?? Environment.MachineName;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The last path written, or null. Exposed for tests and for the operator, so a
    /// notice that WAS written can be pointed at rather than assumed.</summary>
    public string? LastNoticePath { get; private set; }

    /// <summary>True when this notifier has nowhere to write. A client with no COOP root has no
    /// fleet to notify, which is a legitimate state and not an error.</summary>
    public bool CanNotify => !string.IsNullOrWhiteSpace(_coopRoot);

    public void Degraded(PlaneCatalog.Plane requested, PlaneCatalog.Plane live, string reason)
    {
        if (!CanNotify) return;

        try
        {
            var dir = Path.Combine(_coopRoot!, DirectoryName);
            Directory.CreateDirectory(dir);

            var now = _now();
            var stamp = now.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
            var name = $"DEGRADED-{stamp}-{Sanitize(_host)}-{Sanitize(_lane)}.json";
            var path = Path.Combine(dir, name);

            // Never overwrite (2026-08-16 incident). A same-second second degradation gets a
            // suffix rather than clobbering the first — losing the earlier event would hide
            // exactly the flapping this record exists to make visible.
            var attempt = 0;
            while (File.Exists(path) && attempt < 100)
            {
                attempt++;
                path = Path.Combine(dir, $"DEGRADED-{stamp}-{Sanitize(_host)}-{Sanitize(_lane)}-{attempt}.json");
            }
            if (File.Exists(path)) return;

            var payload = JsonSerializer.Serialize(new
            {
                kind = "ynet-plane-degraded",
                utc = now.ToUniversalTime().ToString("O"),
                host = _host,
                lane = _lane,
                requested = requested.ToString(),
                live = live.ToString(),
                reason,
            });

            File.WriteAllText(path, payload);
            LastNoticePath = path;
        }
        catch
        {
            // Intentionally total. See the class doc: the notice is a convenience, the receiving is
            // the product. There is no error path here that should be allowed to stop a client that
            // is otherwise able to run.
        }
    }

    private static string Sanitize(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        for (var i = 0; i < s.Length; i++)
            buf[i] = char.IsLetterOrDigit(s[i]) || s[i] is '-' or '_' or '.' ? s[i] : '-';
        return new string(buf);
    }
}
