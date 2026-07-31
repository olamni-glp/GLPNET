// T023 — snapshot blob + capture/restore tests (US2):
//   - blob round-trip: decode(encode(state)) == state, byte-stable;
//   - restore-then-recapture reproduces the same section bytes (FR-010/011);
//   - non-quiescent deferral + deferred-fire at next quiescence (FR-014);
//   - empty-engine snapshot/restore yields a healthy empty engine (edge case);
//   - in-flight/parked snapshot requests coalesce — one snapshot, monotonic
//     seq (edge case);
//   - loud-fail on unknown section tag / trailing bytes (contract).

using GlpRuntime.Engine;
using GlpRuntime.EngineHost;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.Runtime;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class SnapshotTests : IDisposable
{
    private const string DoubleSource =
        "procedure double_it(Integer?, Integer).\n" +
        "double_it(X, Y?) :- Y := X? * 2.\n";

    private const string HangSource =
        "procedure hang(Integer?, Integer).\n" +
        "hang(X, Y?) :- Y := X? + 1.\n";

    private readonly string _rootSelfGlp = Program.ResolveRootSelfGlpPath();
    private readonly string _storeDir =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t023-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_storeDir, recursive: true); } catch (IOException) { }
    }

    private string RootSelfSource => File.ReadAllText(_rootSelfGlp);

    /// <summary>Build an engine with loaded programs and a persistent suspended goal.</summary>
    private async Task<(GlpEngine Engine, List<LoadedUnit> Units)> BuildStatefulEngineAsync()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var units = new List<LoadedUnit>();
        engine.LoadSource(DoubleSource, filename: "_client_load_1");
        units.Add(new LoadedUnit("_client_load_1", DoubleSource));
        engine.LoadSource(HangSource, filename: "_client_load_2");
        units.Add(new LoadedUnit("_client_load_2", HangSource));

        var done = await engine.RunGoalAsync("double_it(5, Y)");
        Assert.True(done.Succeeded);
        var hung = await engine.RunGoalAsync("hang(A, B)"); // A never bound — stays suspended
        Assert.True(hung.Suspended);
        Assert.NotEmpty(engine.Runtime.Suspended);
        return (engine, units);
    }

    private SnapshotBlob CaptureNow(GlpEngine engine, IReadOnlyList<LoadedUnit> units, ulong seq)
    {
        var quiescence = new Quiescence(engine);
        Assert.True(quiescence.IsQuiescent);
        var disarmed = quiescence.DisarmTimersForCapture();
        Assert.NotNull(disarmed);
        try
        {
            return SnapshotCapture.Capture(
                engine, null, units, RootSelfSource, disarmed!, "engine-test", seq);
        }
        finally
        {
            quiescence.RearmTimers(disarmed!);
        }
    }

    // ---------------------------------------------------------- blob round-trip

    [Fact]
    public async Task Blob_EncodeDecode_RoundTripsByteIdentically()
    {
        var (engine, units) = await BuildStatefulEngineAsync();
        var blob = CaptureNow(engine, units, seq: 1);

        var encoded = blob.Encode();
        var decoded = SnapshotBlob.Decode(encoded);

        Assert.Equal(blob.FormatVersion, decoded.FormatVersion);
        Assert.Equal(blob.EngineIdentity, decoded.EngineIdentity);
        Assert.Equal(blob.CreatedUtcMs, decoded.CreatedUtcMs);
        Assert.Equal(blob.Seq, decoded.Seq);
        Assert.Equal(blob.Sections.Count, decoded.Sections.Count);
        foreach (var tag in SnapshotSection.All)
            Assert.Equal(blob.Section(tag), decoded.Section(tag));

        // decode(encode(state)) == state at the byte level too.
        Assert.Equal(encoded, decoded.Encode());
    }

    [Fact]
    public async Task Restore_ThenRecapture_ReproducesEverySectionByteForByte()
    {
        var (engineA, units) = await BuildStatefulEngineAsync();
        var blobA = CaptureNow(engineA, units, seq: 7);

        var restored = SnapshotRestore.Restore(
            SnapshotBlob.Decode(blobA.Encode()), _rootSelfGlp);
        var blobB = CaptureNow(restored.Engine, restored.Units.ToList(), seq: 7);

        // FR-010/FR-011: the restored engine's complete resumable state is the
        // snapshotted state — every section reproduces byte-for-byte (heap
        // verbatim, same suspension records, same tables, same counters).
        foreach (var tag in SnapshotSection.All)
            Assert.Equal(blobA.Section(tag), blobB.Section(tag));
    }

    [Fact]
    public async Task RestoredEngine_AnswersProbesIdentically()
    {
        var (engineA, units) = await BuildStatefulEngineAsync();
        var blob = CaptureNow(engineA, units, seq: 1);
        var restored = SnapshotRestore.Restore(
            SnapshotBlob.Decode(blob.Encode()), _rootSelfGlp);
        var engineB = restored.Engine;

        // Same probe from the same state on both engines → identical answers.
        var a = await engineA.RunGoalAsync("double_it(21, Y)");
        var b = await engineB.RunGoalAsync("double_it(21, Y)");
        Assert.True(a.Succeeded);
        Assert.True(b.Succeeded);
        Assert.Equal(
            a.Bindings.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()),
            b.Bindings.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()));

        // The suspended goal survived the round-trip.
        Assert.Equal(engineA.Runtime.Suspended.Count, engineB.Runtime.Suspended.Count);
    }

    // ------------------------------------------------- FR-014 deferral + parking

    [Fact]
    public async Task NonQuiescentEngine_DefersSnapshot_ThenFiresAtNextQuiescence()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Serving);
        var quiescence = new Quiescence(engine);
        var store = new SnapshotStore(
            null, new FileSnapshotStore(_storeDir, "engine-test"), _ => { });
        var dispatcher = new RequestDispatcher(
            engine, session, quiescence, store, null, RootSelfSource);

        // Make the engine non-quiescent: park a goal ref in the queue.
        engine.Runtime.Gq.Enqueue(new GoalRef(999_999, 0));

        var deferred = await dispatcher.DispatchAsync(
            RequestFrame.Empty(1, RequestKind.Snapshot));
        Assert.Equal(ResponseKind.Deferred, deferred.Kind);
        Assert.True(quiescence.SnapshotPending);
        Assert.Empty(store.List()); // never an inconsistent snapshot (FR-014)

        // STATUS reports the parked state (FR-014 "pending state reported").
        var status = await dispatcher.DispatchAsync(RequestFrame.Empty(2, RequestKind.Status));
        Assert.Contains("pending_snapshot=deferred_since=", status.BodyText());

        // Drain the queue → quiescent again; the parked snapshot fires after the
        // next dispatched request.
        engine.Runtime.Gq.Dequeue();
        var ping = await dispatcher.DispatchAsync(RequestFrame.Empty(3, RequestKind.Ping));
        Assert.Equal(ResponseKind.Ack, ping.Kind);

        Assert.False(quiescence.SnapshotPending);
        Assert.Single(store.List());
        Assert.Equal(1UL, dispatcher.LastSnapshotSeq);
    }

    [Fact]
    public async Task ParkedSnapshotRequests_Coalesce_SeqStaysMonotonic()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Serving);
        var quiescence = new Quiescence(engine);
        var store = new SnapshotStore(
            null, new FileSnapshotStore(_storeDir, "engine-test"), _ => { });
        var dispatcher = new RequestDispatcher(
            engine, session, quiescence, store, null, RootSelfSource);

        engine.Runtime.Gq.Enqueue(new GoalRef(999_999, 0));

        // Two snapshot requests while busy: both DEFERRED, coalesced into one
        // parked request (edge case: "second request refused or coalesced").
        var d1 = await dispatcher.DispatchAsync(RequestFrame.Empty(1, RequestKind.Snapshot));
        var d2 = await dispatcher.DispatchAsync(RequestFrame.Empty(2, RequestKind.Snapshot));
        Assert.Equal(ResponseKind.Deferred, d1.Kind);
        Assert.Equal(ResponseKind.Deferred, d2.Kind);

        engine.Runtime.Gq.Dequeue();
        await dispatcher.DispatchAsync(RequestFrame.Empty(3, RequestKind.Ping));

        // ONE snapshot, seq 1 — no interleaved partial writes, monotonic seq.
        Assert.Single(store.List());
        Assert.Equal(1UL, store.List()[0].Seq);

        // An explicit snapshot afterwards continues the sequence.
        var ack = await dispatcher.DispatchAsync(RequestFrame.Empty(4, RequestKind.Snapshot));
        Assert.Equal(ResponseKind.Ack, ack.Kind);
        Assert.Contains("snapshot seq=2", ack.BodyText());
    }

    // ---------------------------------------------------- empty-engine edge case

    [Fact]
    public async Task EmptyEngine_SnapshotRestore_YieldsHealthyEmptyEngine()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var blob = CaptureNow(engine, Array.Empty<LoadedUnit>(), seq: 1);

        var restored = SnapshotRestore.Restore(
            SnapshotBlob.Decode(blob.Encode()), _rootSelfGlp);

        Assert.Empty(restored.Units);
        Assert.Equal(0, restored.Engine.Runtime.Heap.Hp);

        // Healthy: the restored empty engine loads and runs normally.
        restored.Engine.LoadSource(DoubleSource, filename: "_after_restore");
        var probe = await restored.Engine.RunGoalAsync("double_it(4, Y)");
        Assert.True(probe.Succeeded);
    }

    // ------------------------------------------------------- loud-fail contract

    [Fact]
    public async Task UnknownSectionTag_And_TrailingBytes_FailLoudly()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var blob = CaptureNow(engine, Array.Empty<LoadedUnit>(), seq: 1);
        var encoded = blob.Encode();

        // Unknown section tag appended.
        var unknownTag = encoded.Concat(new byte[] { 0x7F, 0x00 }).ToArray();
        var ex1 = Assert.Throws<SnapshotException>(() => SnapshotBlob.Decode(unknownTag));
        Assert.Contains("unknown section tag", ex1.Message);

        // Truncated: a section header whose payload is cut off.
        var truncated = encoded.Concat(new byte[] { SnapshotSection.HeapCells }).ToArray();
        Assert.Throws<SnapshotException>(() => SnapshotBlob.Decode(truncated));

        await Task.CompletedTask;
    }
}
