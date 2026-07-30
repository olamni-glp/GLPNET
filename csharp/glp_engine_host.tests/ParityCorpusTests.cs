// T014 — SC-001 parity harness (research D11): the same load+goal script runs
// through (a) the single-process REPL (the converted GlpRuntime.Repl.Program,
// in-process with redirected console) and (b) the split client against a live
// engine host, and the rendered binding + status lines must agree 100% on the
// ground-only envelope subset.
//
// Corpus: Section-A programs from test/run_all_tests.sh that run on the C#
// runtime (A2 append/reverse/copy, A3 quicksort incl. suspension, A15 :=
// arithmetic). The full Section-A sweep stays the Dart suite's job; this
// corpus is the C#-compatible representative subset — cases beyond it are NOT
// covered here (no silent-cap: the corpus is the explicit list below). The
// compile-error/engine-stays-usable path is covered by EngineServerTests
// (structured RESULT + FR-006), not this corpus; the comparison here is the
// binding + status lines of the ground-only envelope subset (captured-output
// parity is implied by the R3 blob riding the same envelope both sides render).

using System.Text;

using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class ParityCorpusTests
{
    private static readonly string RepoRoot =
        Path.GetDirectoryName(Path.GetDirectoryName(
            GlpRuntime.EngineHost.Program.ResolveRootSelfGlpPath()))!;

    private static string Book(string rel) =>
        Path.Combine(RepoRoot, "programs", "typed_book", rel).Replace('\\', '/');

    private static string Typed(string rel) =>
        Path.Combine(RepoRoot, "programs", "tests", "typed", rel).Replace('\\', '/');

    public static IEnumerable<object[]> Corpus()
    {
        yield return new object[]
        {
            "append-reverse-copy",
            new[]
            {
                Book("recursive/list_processing/append.glp"),
                Book("recursive/list_processing/reverse.glp"),
                Book("recursive/list_processing/copy.glp"),
            },
            new[]
            {
                "append([a,b], [c,d], Zs).",
                "append([], [x,y], Zs2).",
                "append([a,b], [], Zs3).",
                "reverse([a,b,c], Ys).",
                "reverse([], Ys2).",
                "copy([a,b,c], Yc).",
            },
        };
        yield return new object[]
        {
            "quicksort-with-suspension",
            new[] { Book("recursive/list_processing/quicksort.glp") },
            new[]
            {
                "quicksort([],Xq1).",
                "quicksort([1,6,4,2,7,4,2,6],Xq4).",
                "quicksort([1,3,4,2,5],Xq5).",
                "quicksort([1|X?],Xq7).",
            },
        };
        yield return new object[]
        {
            "assign-arithmetic",
            Array.Empty<string>(),
            new[]
            {
                "Xa1 := 3 + 4.",
                "Xa2 := 10 - 4.",
                "Xa3 := 4 * 7.",
                "Xa4 := 25 / 5.",
                "Xa5 := 3 + 4 * 2.",
                "Xa6 := (3 + 5) * 2.",
                "Xa7 := 0 - 5.",
            },
        };
        yield return new object[]
        {
            "arithmetic-fixed-program",
            new[] { Typed("arithmetic_fixed.glp") },
            new[]
            {
                "add(5, 3, Xadd).",
                "multiply(4, 7, Ymul).",
                "compute(Zcomp).",
                "subtract(10, 3, Xsub).",
            },
        };
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task SplitClient_MatchesSingleProcessRepl(string label, string[] files, string[] goals)
    {
        var script = new StringBuilder();
        foreach (var f in files)
        {
            Assert.True(File.Exists(f), $"corpus file missing: {f}");
            script.Append(f).Append('\n');
        }
        foreach (var g in goals)
            script.Append(g).Append('\n');
        script.Append(":quit\n");

        var reference = await RunSingleProcessReplAsync(script.ToString());
        var split = await RunSplitAsync(script.ToString());

        Assert.True(reference.Count > 0, $"[{label}] reference produced no result lines");
        Assert.Equal(reference, split);
    }

    /// <summary>The lines SC-001 compares: bindings and status verdicts.</summary>
    private static List<string> ExtractResultLines(string output)
    {
        var lines = new List<string>();
        foreach (var raw in output.Split('\n'))
        {
            // The interactive prompt prefixes result lines when stdin is piped
            // ("GLP> Xs = …"); strip every leading prompt token first.
            var line = raw.Replace("\r", "");
            while (line.StartsWith("GLP> ", StringComparison.Ordinal))
                line = line["GLP> ".Length..];
            if (line.StartsWith("→ ", StringComparison.Ordinal) ||
                System.Text.RegularExpressions.Regex.IsMatch(line, @"^\S+ = "))
            {
                lines.Add(line.TrimEnd());
            }
        }
        return lines;
    }

    private static async Task<List<string>> RunSingleProcessReplAsync(string script)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetIn(new StringReader(script));
            Console.SetOut(output);
            await GlpRuntime.Repl.Program.Main(Array.Empty<string>());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
        return ExtractResultLines(output.ToString());
    }

    private static async Task<List<string>> RunSplitAsync(string script)
    {
        await using var host = new EngineHostFixture();
        await using var client = await host.ConnectAsync();

        var output = new StringWriter();
        foreach (var raw in script.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line == ":quit")
                continue;

            if (line.EndsWith(".glp", StringComparison.Ordinal))
            {
                // FR-001: the client ships source text; load result renders like the REPL.
                var source = await File.ReadAllTextAsync(line);
                var response = await client.RoundTripAsync(
                    RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, source));
                if (response.Kind == ResponseKind.Result)
                {
                    var env = GlpRuntime.ResultCodec.ResultEnvelopeCodec.Decode(response.Body);
                    output.WriteLine($"Error loading {line}: {env.Error}");
                }
                continue;
            }

            var goal = line.EndsWith('.') ? line[..^1].Trim() : line;
            var goalResponse = await client.RoundTripAsync(
                RequestFrame.Text(client.NextRequestId(), RequestKind.RunGoal, goal));
            Assert.Equal(ResponseKind.Result, goalResponse.Kind);
            var envelope = GlpRuntime.ResultCodec.ResultEnvelopeCodec.Decode(goalResponse.Body);
            RenderLikeClient(envelope, output);
        }
        return ExtractResultLines(output.ToString());
    }

    /// <summary>Mirror of the client's RenderEnvelope (glp_repl_client/Program.cs).</summary>
    private static void RenderLikeClient(GlpRuntime.ResultCodec.ResultEnvelope envelope, StringWriter output)
    {
        if (envelope.Captured.Length > 0)
        {
            var lines = Encoding.UTF8.GetString(envelope.Captured).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && lines[i].Length == 0)
                    break;
                output.WriteLine(lines[i]);
            }
        }

        foreach (var kv in envelope.ResolvedBindings)
        {
            var rendered = kv.Value is GlpRuntime.ResultCodec.ConstTerm { Value: GlpRuntime.ResultCodec.ConstString s }
                ? s.Value
                : kv.Value.ToString();
            output.WriteLine($"{kv.Key} = {rendered}");
        }

        output.WriteLine(envelope.Status switch
        {
            GlpRuntime.ResultCodec.ExecutionStatus.Success => "→ succeeds",
            GlpRuntime.ResultCodec.ExecutionStatus.Failed => "→ failed",
            GlpRuntime.ResultCodec.ExecutionStatus.Suspended => "→ suspended",
            _ => throw new ArgumentOutOfRangeException(),
        });

        if (envelope.Error is not null)
            output.WriteLine($"Error: {envelope.Error}");
    }
}
