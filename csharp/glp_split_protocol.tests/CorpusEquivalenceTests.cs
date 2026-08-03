// T024 — the SC-003 corpus equivalence gate (contracts/il-request-kind.md
// rule 4/Acceptance): for a representative set of programs from
// programs/tests/typed (12 programs incl. multi_client_control.glp), results
// via the IL path == results via the text path.
//
// Per case, the SAME source unit (program file + a nullary `run_case` driver
// that '_output's its observable result) runs through:
//   TEXT: LOAD_SOURCE(unit) → RUN_GOAL "run_case"          (engine compiles)
//   IL:   client-compile(unit) → LOAD_IL(prelude) → LOAD_IL(unit)
//         → RUN_GOAL_IL "run_case/0"                        (engine executes only)
// and the two 038 ResultEnvelopes must agree on status, error, the captured
// '_output' blob, and (both-empty) bindings. Nullary drivers make the
// comparison exact: neither path renders query-variable bindings, and the
// observable value rides the captured blob. Statuses are additionally anchored
// per case so equivalence cannot pass vacuously.
//
// The driver rides `-mode(system).` (the embedded madGLP-predicates precedent)
// because it calls the reserved '_output'/1 kernel after a ground/1 guard —
// the exact send_to_user/1 shape from GlpEngine's embedded source.

using System.Text;

using GlpRuntime.ReplClient;
using GlpRuntime.ResultCodec;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;

namespace GlpRuntime.SplitProtocol.Tests;

public class CorpusEquivalenceTests
{
    /// <summary>One shared client-side compiler front-end (each case compiles
    /// its unit under a distinct module_ref).</summary>
    private static readonly Lazy<IlSession> Client = new(IlSession.Create);

    private const string DriverTemplate =
        "\n\n%% 064 US3 corpus driver (nullary; observable result rides '_output')\n" +
        "procedure run_case.\n" +
        "run_case :- {0}.\n" +
        "procedure out_term(_?).\n" +
        "out_term(T) :- ground(T?) | '_output'(T?).\n";

    // (program file, driver body or null = program defines run_case-equivalent itself,
    //  goal label, expected status, expected output substring or null)
    public static IEnumerable<object[]> Corpus()
    {
        static object[] Case(string file, string? driver, RcExecutionStatus expected,
            string? outputContains) => new object[] { file, driver!, expected, outputContains! };

        yield return Case("constant_ground_test.glp",
            "test_constant(foo, R), out_term(R?)", RcExecutionStatus.Success, "foo");
        yield return Case("gethead_test.glp",
            "test_gethead(R), out_term(R?)", RcExecutionStatus.Success, "a");
        yield return Case("arith_comparison.glp",
            "test_lt(3, 5, R), out_term(R?)", RcExecutionStatus.Success, "yes");
        yield return Case("otherwise_guard.glp",
            "classify(0, R), out_term(R?)", RcExecutionStatus.Success, "zero");
        yield return Case("multiply.glp",
            "multiply(3, [1,2,3,4], Ys), out_term(Ys?)", RcExecutionStatus.Success, "[3, 6, 9, 12]");
        yield return Case("arithmetic_fixed.glp",
            "compute(Z), out_term(Z?)", RcExecutionStatus.Success, "10");
        yield return Case("struct_demo.glp",
            "build_person(P), out_term(P?)", RcExecutionStatus.Success, "person(alice");
        yield return Case("depth_test.glp",
            "ter_all(a, b, c, T), out_term(T?)", RcExecutionStatus.Success, "triple(a, b, c)");
        yield return Case("merge_standalone.glp",
            "merge2([c,d], Out), out_term(Out?)", RcExecutionStatus.Success, null);
        yield return Case("compound_suspend.glp",
            "run_eq_wake", RcExecutionStatus.Success, null); // suspend → reactivate → succeed
        yield return Case("multi_client_control.glp",
            "control_demo(Out), out_term(Out?)", RcExecutionStatus.Success, "pong(c1)");
        yield return Case("p.glp",
            "p(c)", RcExecutionStatus.Failed, null); // no clause for c → FAIL on both paths
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task IlPath_Equals_TextPath(
        string file, string driver, RcExecutionStatus expected, string? outputContains)
    {
        var programSource = await File.ReadAllTextAsync(TestRepo.Typed(file));
        var unit = "-mode(system).\n" + programSource +
                   string.Format(DriverTemplate, driver);

        var text = await RunTextPathAsync(unit);
        var il = await RunIlPathAsync(unit, file);

        // Rule 4: IL-path results == text-path results (full envelope surface).
        Assert.Equal(text.Status, il.Status);
        Assert.Equal(text.Error, il.Error);
        Assert.Equal(
            Encoding.UTF8.GetString(text.Captured),
            Encoding.UTF8.GetString(il.Captured));
        Assert.Empty(text.ResolvedBindings); // nullary driver — neither path renders bindings
        Assert.Empty(il.ResolvedBindings);

        // Anchors: equivalence must not pass vacuously.
        Assert.Equal(expected, il.Status);
        if (outputContains is not null)
            Assert.Contains(outputContains, Encoding.UTF8.GetString(il.Captured));
    }

    [Fact]
    public async Task UnknownGoal_RendersTheSameFailure_OnBothPaths()
    {
        var text = await RunTextPathAsync(unit: null, goal: "nope");
        var il = await RunIlPathAsync(unit: null, moduleRef: null, goalRef: "nope/0");
        Assert.Equal(RcExecutionStatus.Failed, text.Status);
        Assert.Equal(RcExecutionStatus.Failed, il.Status);
        Assert.Equal("Predicate nope/0 not found", text.Error);
        Assert.Equal(text.Error, il.Error);
    }

    // ---- the two paths ----

    private static async Task<ResultEnvelope> RunTextPathAsync(
        string? unit, string goal = "run_case")
    {
        using var host = new DispatcherFixture();
        ulong id = 0;
        if (unit is not null)
        {
            var load = await host.SendAsync(RequestFrame.Text(++id, RequestKind.LoadSource, unit));
            Assert.True(load.Kind == ResponseKind.Ack,
                $"text load failed: {(load.Kind == ResponseKind.Result ? ResultEnvelopeCodec.Decode(load.Body).Error : load.BodyText())}");
        }
        var run = await host.SendAsync(RequestFrame.Text(++id, RequestKind.RunGoal, goal));
        Assert.Equal(ResponseKind.Result, run.Kind);
        return ResultEnvelopeCodec.Decode(run.Body);
    }

    private static async Task<ResultEnvelope> RunIlPathAsync(
        string? unit, string? moduleRef, string goalRef = "run_case/0")
    {
        using var host = new DispatcherFixture();
        ulong id = 0;

        // The engine cannot compile the prelude on the IL path — the client
        // ships it first (IlSession's session-start behaviour).
        var prelude = await host.SendAsync(new RequestFrame(
            ++id, RequestKind.LoadIl, Client.Value.PreludeEnvelope()));
        Assert.Equal(ResponseKind.Ack, prelude.Kind);

        if (unit is not null)
        {
            var envelope = Client.Value.CompileUnit(unit, $"corpus/{moduleRef}");
            var load = await host.SendAsync(new RequestFrame(++id, RequestKind.LoadIl, envelope));
            Assert.True(load.Kind == ResponseKind.Ack, $"IL load refused: {load.BodyText()}");
        }

        var run = await host.SendAsync(new RequestFrame(++id, RequestKind.RunGoalIl,
            new RunGoalIlBody(goalRef, null).Encode()));
        Assert.Equal(ResponseKind.Result, run.Kind);
        return ResultEnvelopeCodec.Decode(run.Body);
    }
}
