// T024a — restore-equivalence probe test (US2 independent test, SC-004):
// load programs + run goals to a known state through the split dispatcher,
// SNAPSHOT, boot a SECOND engine --from-snapshot (same store, latest), run the
// state-revealing probe set against both engines and assert IDENTICAL answers
// (byte-identical RESULT envelope bodies — the ground-only 038 subset).
//
// A second fact proves the FR-015 remaining-time timer re-arm end-to-end: a
// goal suspended on wait(...) is snapshotted mid-wait; the restored engine
// re-arms with the remaining duration and the timer fires post-restore,
// observable through the next goal's captured output blob.

using GlpRuntime.Engine;
using GlpRuntime.EngineHost;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class RestoreEquivalenceTests : IDisposable
{
    private const string DoubleSource =
        "procedure double_it(Integer?, Integer).\n" +
        "double_it(X, Y?) :- Y := X? * 2.\n";

    private const string HangSource =
        "procedure hang(Integer?, Integer).\n" +
        "hang(X, Y?) :- Y := X? + 1.\n";

    private readonly string _rootSelfGlp = Program.ResolveRootSelfGlpPath();
    private readonly string _storeDir =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t024a-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_storeDir, recursive: true); } catch (IOException) { }
    }

    private (RequestDispatcher Dispatcher, SnapshotStore Store, GlpEngine Engine) NewHost(
        GlpEngine? engine = null, IReadOnlyList<LoadedUnit>? restoredUnits = null)
    {
        engine ??= new GlpEngine(_rootSelfGlp);
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Serving);
        var store = new SnapshotStore(
            null, new FileSnapshotStore(_storeDir, "engine-test"), _ => { });
        var dispatcher = new RequestDispatcher(
            engine, session, new Quiescence(engine), store,
            null, File.ReadAllText(_rootSelfGlp), restoredUnits);
        return (dispatcher, store, engine);
    }

    private static async Task<ResponseFrame> SendAsync(
        RequestDispatcher d, ulong id, RequestKind kind, string? body = null) =>
        await d.DispatchAsync(body is null
            ? RequestFrame.Empty(id, kind)
            : RequestFrame.Text(id, kind, body));

    [Fact]
    public async Task SecondEngineFromSnapshot_AnswersEveryProbeIdentically()
    {
        // ---- engine A: known state through the split path ----
        var (a, store, _) = NewHost();
        Assert.Equal(ResponseKind.Ack, (await SendAsync(a, 1, RequestKind.LoadSource, DoubleSource)).Kind);
        Assert.Equal(ResponseKind.Ack, (await SendAsync(a, 2, RequestKind.LoadSource, HangSource)).Kind);

        var ran = await SendAsync(a, 3, RequestKind.RunGoal, "double_it(5, Y)");
        Assert.Equal(ResponseKind.Result, ran.Kind);
        var hung = await SendAsync(a, 4, RequestKind.RunGoal, "hang(A, B)"); // stays suspended
        Assert.Equal(ExecutionStatus.Suspended, ResultEnvelopeCodec.Decode(hung.Body).Status);

        var ack = await SendAsync(a, 5, RequestKind.Snapshot);
        Assert.Equal(ResponseKind.Ack, ack.Kind);
        Assert.Contains("snapshot seq=1", ack.BodyText());

        // ---- engine B: boot --from-snapshot latest (the Program.cs path) ----
        var latest = store.Latest();
        Assert.NotNull(latest);
        var restored = SnapshotRestore.Restore(SnapshotBlob.Decode(latest!.Value.Blob), _rootSelfGlp);
        var (b, _, _) = NewHost(restored.Engine, restored.Units);

        // ---- SC-004: every state-revealing probe answers identically ----
        var probes = new[]
        {
            "double_it(21, Y)",   // loaded program + deterministic fresh allocation
            "hang(P, Q)",         // a new suspension from the same heap frontier
            "Z := 3 + 4",         // arithmetic through the prelude
            "double_it(0, Y)",
        };
        ulong id = 100;
        foreach (var probe in probes)
        {
            var ra = await SendAsync(a, id++, RequestKind.RunGoal, probe);
            var rb = await SendAsync(b, id++, RequestKind.RunGoal, probe);
            Assert.Equal(ResponseKind.Result, ra.Kind);
            Assert.Equal(ResponseKind.Result, rb.Kind);
            // Byte-identical envelope bodies: status + pre-rendered bindings +
            // output blob all agree (100% of probes, SC-004).
            Assert.Equal(ra.Body, rb.Body);
        }

        // The loaded-program surface agrees too.
        var sa = (await SendAsync(a, id++, RequestKind.Status)).BodyText();
        var sb = (await SendAsync(b, id++, RequestKind.Status)).BodyText();
        Assert.Equal(
            sa.Split(' ').First(p => p.StartsWith("loaded_programs=")),
            sb.Split(' ').First(p => p.StartsWith("loaded_programs=")));
    }

    [Fact]
    public async Task ArmedTimer_CapturedAsRemainingDuration_RearmsAndFiresAfterRestore()
    {
        // An armed timer at quiescence cannot be produced through the wire in the
        // MVP: the engine's async drain WAITS for pending timers before RUN_GOAL
        // returns (scheduler.cs DrainAsyncWithStatus's timer poll), so a wait()
        // goal completes inside its own request. Armed-at-quiescence arises only
        // when a drain exhausts MaxCycles with a timer still pending. This test
        // therefore arms the state exactly as the runner's wait guard does
        // (runner.cs 'wait': allocate pair → SetWaitReader → IncrementPendingTimers
        // → StartGlpTimer → suspend on the reader) and proves the FR-015 contract:
        // capture records the REMAINING duration, restore re-arms with it, and the
        // fired timer binds the writer and reactivates the suspended goal.
        var engine = new GlpEngine(_rootSelfGlp);
        var rt = engine.Runtime;

        var (writerAddr, readerAddr) = rt.Heap.AllocateVariable();
        const int goalId = 4242;
        const int kappa = 17;
        rt.SetGoalEnv(goalId, new GlpRuntime.Bytecode.CallEnv());
        rt.SetGoalProgram(goalId, "main");
        rt.SetWaitReader(goalId, readerAddr);
        rt.IncrementPendingTimers();
        GlpRuntime.Bytecode.BytecodeRunner.StartGlpTimer(1200, rt, writerAddr);
        rt.SuspendGoalFCP(goalId, kappa, new HashSet<int> { readerAddr });

        // Capture: the disarm records remaining ∈ (0, 1200].
        var quiescence = new Quiescence(engine);
        var disarmed = quiescence.DisarmTimersForCapture();
        Assert.NotNull(disarmed);
        var timer = Assert.Single(disarmed!);
        Assert.Equal(writerAddr, timer.WriterAddr);
        Assert.InRange(timer.RemainingMs, 0, 1200);

        var blob = SnapshotCapture.Capture(
            engine, null, Array.Empty<LoadedUnit>(), File.ReadAllText(_rootSelfGlp),
            disarmed!, "engine-test", 1);
        quiescence.RearmTimers(disarmed!); // engine A keeps running undisturbed

        // Restore: the timer re-arms with the REMAINING duration (FR-015) —
        // independent of downtime, no expired-timer storm semantics to violate
        // here because the deadline is re-based at restore.
        var restored = SnapshotRestore.Restore(SnapshotBlob.Decode(blob.Encode()), _rootSelfGlp);
        var rtB = restored.Engine.Runtime;
        Assert.Equal(1, rtB.PendingTimers);
        Assert.Equal(readerAddr, rtB.WaitReadersView[goalId]);
        Assert.False(rtB.Heap.IsFullyBound(writerAddr));

        // The re-armed timer fires within the remaining window: writer bound,
        // suspended goal reactivated into the queue, bookkeeping drained.
        await Task.Delay(2500);
        Assert.Equal(0, rtB.PendingTimers);
        Assert.True(rtB.Heap.IsFullyBound(writerAddr));
        Assert.Contains(rtB.Gq.Items, g => g.Id == goalId && g.Pc == kappa);
        Assert.Empty(rtB.Suspended);
    }
}
