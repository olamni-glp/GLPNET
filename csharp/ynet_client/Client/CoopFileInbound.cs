// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;

namespace Ynet.Client;

/// <summary>
/// The CROSS-LANE plane: this lane's M6 mailbox on a shared coop root.
///
/// Until this existed, "run" bound <see cref="LoopbackInbound"/> and the receiver could only ever
/// see messages this process manufactured for itself. That gap was disclosed in this lane's own
/// source rather than hidden, and closing it is the carrier adapter ruled to this lane by
/// Q-glpnetshiras-50 (R-B, 2026-09-05T15:10Z).
///
/// Layout, matching the carrier already proven lane-to-lane on SHIRAS:
///
///     [coopRoot]/[laneDirectory]/inbox/*.frame     inbound frames
///     [coopRoot]/[laneDirectory]/inbox/.taken/     claimed frames, kept for forensics
///
/// Three deliberate choices, each answering a defect the fleet measured today:
///
/// 1. Polling, not FileSystemWatcher. A coop root is an SMB mount that is not always present. A
///    watcher on a network share silently stops delivering after a reconnect; a poll cannot, and
///    its failure to reach the directory is observable rather than silent.
/// 2. A claim is a MOVE, never a read-then-delete. Two receivers on one inbox both attempt to move
///    the same frame; exactly one succeeds and the loser gets a miss, not a double delivery.
///    Rev-3 recorded File.Move(overwrite:false) being cited as atomic when it was not - that was
///    about creating a claim FILE. Moving the SOURCE is atomic in the sense that matters here: a
///    source can only be moved once.
/// 3. A stray file is COUNTED AND NAMED, never invisible. The canonical carrier enumerates *.frame
///    only, so a mis-addressed non-frame file was not refused - it was not seen, and frames_refused
///    stayed 0 while ACK-mandatory traffic sat unread (A3, shiras-glpnet 2026-09-05T15:10Z). This
///    plane reports strays by name, so a mis-addressed send is visible on the RECEIVING side even
///    when the sender never learns of it.
/// </summary>
public sealed class CoopFileInbound : IYnetInbound, IDisposable
{
    /// <summary>Only files with this extension are frames. Everything else is a stray (see class remarks).</summary>
    public const string FrameExtension = ".frame";

    /// <summary>Claimed frames are moved here rather than deleted, so a delivery can be audited after the fact.</summary>
    public const string TakenDirectoryName = ".taken";

    private readonly string _inboxDirectory;
    private readonly string _takenDirectory;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, byte> _strays = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _seenLength = new(StringComparer.OrdinalIgnoreCase);
    private long _strayTotal;

    /// <summary>How many stray NAMES are retained. The total count is exact and unbounded; only names are capped.</summary>
    public const int MaxRetainedStrayNames = 256;

    private CancellationTokenSource? _cts;
    private Task? _pump;

    public CoopFileInbound(string coopRoot, string laneDirectory, TimeSpan? pollInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coopRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(laneDirectory);

        // 🔴 CONFINE the lane under the coop root. Path.Combine does NOT confine: it happily returns
        // C:\victim\inbox for laneDirectory "..\victim", and DISCARDS coopRoot entirely for a rooted
        // laneDirectory like "D:\other". Both values come from environment variables
        // (YNET_CLIENT_COOP / YNET_CLIENT_LANE), so this is reachable configuration, not a theory.
        // codexreview 2026-09-05, P1.
        var root = Path.GetFullPath(coopRoot);
        var combined = Path.GetFullPath(Path.Combine(root, laneDirectory));
        var fence = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(fence, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"lane directory '{laneDirectory}' resolves to '{combined}', which is outside the coop root '{root}'",
                nameof(laneDirectory));
        }

        _inboxDirectory = Path.Combine(combined, "inbox");
        _takenDirectory = Path.Combine(_inboxDirectory, TakenDirectoryName);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>The directory this plane reads. Surfaced so "run" can print where it is actually listening.</summary>
    public string InboxDirectory => _inboxDirectory;

    public string PlaneName => "coop-file-cross-lane";

    /// <summary>
    /// 🔴 FALSE on this plane, and it is a constant rather than a computation so that no caller can
    /// mistake it for a per-message verdict. A message's <c>Origin</c> here comes from a filename the
    /// sender chose, so it is a CLAIM, not proof. See <see cref="OriginFromFrameName"/>.
    /// </summary>
    public bool OriginIsAuthenticated => false;

    public event Action<YnetMessage>? Received;

    /// <summary>
    /// Raised once per stray file, with its full path. A stray is a file in the inbox that is not a
    /// frame - the case the canonical carrier could not see at all.
    /// </summary>
    public event Action<string>? StrayObserved;

    /// <summary>Raised when a poll fails (root unmounted, permission denied). The plane keeps polling.</summary>
    public event Action<Exception>? PollFailed;

    /// <summary>
    /// The most recent stray paths, by full path - named, not merely counted. Capped at
    /// <see cref="MaxRetainedStrayNames"/>; <see cref="StrayCount"/> remains the exact total.
    /// </summary>
    public IReadOnlyCollection<string> Strays => _strays.Keys.ToArray();

    /// <summary>
    /// How many strays have been observed IN TOTAL - exact, never capped. Non-zero means somebody is
    /// mis-addressing this lane. Kept separate from <see cref="Strays"/> so bounding the retained
    /// names can never quietly bound the number that is reported.
    /// </summary>
    public int StrayCount => (int)Interlocked.Read(ref _strayTotal);

    /// <summary>
    /// Create this lane's mailbox without starting to receive. Two reasons this is separate from
    /// <see cref="Open"/>, and the second is a defect the first version of this class had:
    ///
    /// 1. A peer addresses a lane by its directory, so the mailbox must be able to exist before the
    ///    lane ever runs a receiver - otherwise every send to a not-yet-started lane fails closed.
    /// 2. Once the pump is running it is the ONLY caller of <see cref="PollOnce"/> that is safe. An
    ///    operator draining by hand while the pump ran would race it, and each would see a partial
    ///    count. Callers that want deterministic delivery ensure the mailbox and poll themselves.
    /// </summary>
    public void EnsureMailbox()
    {
        Directory.CreateDirectory(_inboxDirectory);
        Directory.CreateDirectory(_takenDirectory);
    }

    // Open/Close mutate a two-field state machine (_cts, _pump). An unguarded `if (_pump is null)`
    // lets two callers both pass the check, both start a pump, and the loser's _cts get overwritten -
    // so Close() cancels only one and the other runs on past disposal. codexreview 2026-09-05, P2.
    private readonly Lock _lifecycle = new();

    public void Open()
    {
        lock (_lifecycle)
        {
            if (_pump is not null) return; // idempotent

            EnsureMailbox();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pump = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    PollOnce();
                    try { await Task.Delay(_pollInterval, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }
            }, token);
        }
    }

    public void Close()
    {
        Task? pump;
        CancellationTokenSource? cts;
        lock (_lifecycle)
        {
            if (_pump is null) return; // idempotent
            pump = _pump;
            cts = _cts;
            _pump = null;
            _cts = null;
        }
        // Wait OUTSIDE the lock: the pump calls PollOnce, and holding _lifecycle while waiting for it
        // would deadlock a concurrent Open().
        cts!.Cancel();
        try { pump!.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { /* cancellation */ }
        cts.Dispose();
    }

    /// <summary>
    /// One sweep of the inbox. Public so a test - and an operator - can drive delivery
    /// deterministically instead of sleeping for a poll interval. Returns frames delivered.
    ///
    /// Call this EITHER from the pump (via <see cref="Open"/>) or by hand after
    /// <see cref="EnsureMailbox"/> - never both at once, or the two sweeps split the frames between
    /// them and each returns a partial count.
    /// </summary>
    public int PollOnce()
    {
        string[] entries;
        try
        {
            entries = Directory.GetFiles(_inboxDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PollFailed?.Invoke(ex); // the root went away; report it and keep polling
            return 0;
        }

        var delivered = 0;
        foreach (var path in entries)
        {
            if (!path.EndsWith(FrameExtension, StringComparison.OrdinalIgnoreCase))
            {
                NoteStray(path);
                continue;
            }
            // A frame that is still being written is left for the next sweep, not guessed at.
            if (!LengthIsStable(path)) continue;
            if (TryDeliver(path)) delivered++;
        }
        return delivered;
    }

    private void NoteStray(string path)
    {
        // First observation only - a stray that is never removed must not re-fire on every poll.
        if (!_strays.TryAdd(path, 0)) return;

        // BOUNDED. A long-running receiver on a shared lane sees unique stray names for days, and an
        // unbounded set is a leak that grows with someone else's mistakes. The TOTAL is kept exactly
        // (it is the number that matters); only the retained NAMES are capped. codexreview P2.
        Interlocked.Increment(ref _strayTotal);
        if (_strays.Count > MaxRetainedStrayNames)
        {
            foreach (var k in _strays.Keys)
            {
                if (_strays.Count <= MaxRetainedStrayNames) break;
                _strays.TryRemove(k, out _);
            }
        }
        StrayObserved?.Invoke(path);
    }

    /// <summary>
    /// Is this file finished being written?
    ///
    /// 🔴 A CLAIM IS NOT ENOUGH ON ITS OWN. `File.Move` can succeed on a file a peer is still writing
    /// on shares that permit renaming an open handle, and `ReadAllBytes` would then deliver a
    /// TRUNCATED body as if it were complete - a silent wrong answer, not an error (codexreview P1).
    ///
    /// The writer's contract is temp-file + rename into `*.frame`, which makes publication atomic and
    /// makes this check unnecessary. This exists because a receiver cannot enforce another lane's
    /// writer, and a truncated delivery is exactly the class of silent corruption this fleet has been
    /// paying for. Two observations of the same length, separated by a poll, is a cheap gate that
    /// costs one extra interval and refuses to guess.
    /// </summary>
    private bool LengthIsStable(string path)
    {
        long length;
        try { length = new FileInfo(path).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }

        if (_seenLength.TryGetValue(path, out var previous) && previous == length)
        {
            _seenLength.TryRemove(path, out _);
            return true;
        }
        _seenLength[path] = length;   // first sighting, or still growing - look again next poll
        return false;
    }

    private bool TryDeliver(string path)
    {
        // Claim by MOVE. A unique destination means two receivers racing the same frame produce one
        // winner and one miss, never two deliveries.
        var claimed = Path.Combine(_takenDirectory, $"{Path.GetFileName(path)}.{Guid.NewGuid():n}");
        try
        {
            File.Move(path, claimed);
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false; // lost the race, or the frame is still being written - the next poll retries
        }

        byte[] body;
        try
        {
            body = File.ReadAllBytes(claimed);
        }
        catch (IOException ex)
        {
            PollFailed?.Invoke(ex);
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        Received?.Invoke(new YnetMessage(
            MessageId: name,
            Origin: OriginFromFrameName(name),
            Summary: name,
            Body: body));
        return true;
    }

    /// <summary>
    /// The canonical carrier names a frame "[origin]--[id].frame". A name that carries no origin
    /// yields "unknown" rather than a guess: an unattributed message is reported as unattributed,
    /// never silently attributed to whoever happens to be nearby.
    ///
    /// 🔴 THE RESULT IS UNAUTHENTICATED AND MUST NOT BE TREATED AS PROOF OF SENDER.
    /// It is derived from a FILENAME, and any party that can write into this inbox chooses that
    /// filename. A peer can name its frame "shiras-glpnet--urgent.frame" and this method will
    /// return "shiras-glpnet" (codexreview 2026-09-05, P1).
    ///
    /// This is DISCLOSED rather than fixed, deliberately, and the reason is scope, not convenience:
    /// authenticating a sender needs a SIGNED FRAME ENVELOPE, and the envelope belongs to the
    /// canonical client (Q-glpnetshiras-50 ruled YngeniOS.Ynet.Client canonical and this one a
    /// contributor). Inventing a rival envelope here is precisely the mistake that produced three
    /// M6 clients in one morning.
    ///
    /// What this lane DOES do about it: <see cref="OriginIsAuthenticated"/> reports false, the
    /// limitation is pinned by a test that demonstrates the spoof rather than hiding it, and the
    /// gap is carried as a named requirement for the canonical envelope.
    /// </summary>
    internal static string OriginFromFrameName(string frameName)
    {
        var sep = frameName.IndexOf("--", StringComparison.Ordinal);
        return sep > 0 ? frameName[..sep] : "unknown";
    }

    public void Dispose() => Close();
}
