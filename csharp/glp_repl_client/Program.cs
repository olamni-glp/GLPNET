// glp_repl_client — the thin terminal REPL over the split protocol (T012, R7).
//
//   glp_repl_client --connect 127.0.0.1:7461
//
// Holds NO language context (FR-003): `load` reads the file's bytes and ships
// the SOURCE TEXT to the engine (FR-001); goals ship as text; results render
// from the 038 envelope's pre-rendered display strings (R6) — no local heap,
// no local prelude. Rendering mirrors the single-process REPL line formats
// (out/csharp/bin/glp_repl.cs) so the SC-001 parity diff is meaningful:
//   <output lines>                 (captured '_output' text, R3 blob)
//   Name = <rendered>              (bindings; <unbound> when unbound)
//   → succeeds | → failed | → suspended
//   Error: <error>                 (when the envelope carries one)
// Transport failure renders as "!! transport failure: …" — never confusable
// with a goal failure (FR-007).

using System.Text;

using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.ReplClient;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string? connect = null;
        string path = "text";
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connect" when i + 1 < args.Length:
                    connect = args[++i];
                    break;
                // 064 US3 (T022): the load/run path is chosen PER SESSION and
                // never mixed per module (contracts/il-request-kind.md rule 3).
                // "il" compiles locally and ships CompiledIlEnvelope frames.
                case "--path" when i + 1 < args.Length:
                    path = args[++i];
                    if (path is not ("text" or "il"))
                    {
                        Console.Error.WriteLine($"glp_repl_client: --path expects text|il, got '{path}'");
                        return 64;
                    }
                    break;
                default:
                    Console.Error.WriteLine($"glp_repl_client: unknown argument '{args[i]}'");
                    Console.Error.WriteLine("usage: glp_repl_client --connect <host:port> [--path text|il]");
                    return 64;
            }
        }
        if (connect is null)
        {
            Console.Error.WriteLine("glp_repl_client: --connect <host:port> is required");
            return 64;
        }

        IlSession? il = null;
        if (path == "il")
        {
            try
            {
                il = IlSession.Create();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"glp_repl_client: cannot start IL session: {ex.Message}");
                return 66;
            }
        }

        var idx = connect.LastIndexOf(':');
        if (idx <= 0 || !int.TryParse(connect[(idx + 1)..], out var port))
        {
            Console.Error.WriteLine($"glp_repl_client: --connect expects <host:port>, got '{connect}'");
            return 64;
        }
        var host = connect[..idx];

        ClientChannel channel;
        try
        {
            channel = await ClientChannel.ConnectAsync(host, port, TimeSpan.FromSeconds(10));
        }
        catch (ClientTransportException ex)
        {
            Console.Error.WriteLine($"!! transport failure: {ex.Message}");
            return 69;
        }

        Console.WriteLine($"Connected to engine at {host}:{port}");
        Console.WriteLine("Input: load <file.glp> to load, goal. to execute; :status, :snapshot, :quit");
        if (il is not null)
        {
            Console.WriteLine("IL session: modules compile LOCALLY and ship as IL; goals run by " +
                              "reference (bindings are not rendered on the IL path — use a text " +
                              "session when bindings matter).");
            // The engine cannot compile the prelude on its IL path — ship it first.
            try
            {
                var preludeResponse = await channel.RoundTripAsync(new RequestFrame(
                    channel.NextRequestId(), RequestKind.LoadIl, il.PreludeEnvelope()));
                if (!RenderIlLoadResponse(preludeResponse, IlSession.PreludeModuleRef))
                    return 70;
            }
            catch (ClientTransportException ex)
            {
                Console.Error.WriteLine($"!! transport failure: {ex.Message}");
                return 69;
            }
        }
        Console.WriteLine();

        await using (channel)
        {
            while (true)
            {
                Console.Write("GLP> ");
                var input = Console.ReadLine();
                if (input is null)
                    break;

                var trimmed = input.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (trimmed.EndsWith('.') && !trimmed.EndsWith(".glp", StringComparison.Ordinal))
                    trimmed = trimmed[..^1].Trim();

                if (trimmed is ":quit" or ":q")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                try
                {
                    if (trimmed is ":status")
                    {
                        var response = await channel.RoundTripAsync(
                            RequestFrame.Empty(channel.NextRequestId(), RequestKind.Status));
                        Console.WriteLine(response.BodyText());
                        continue;
                    }

                    if (trimmed is ":snapshot")
                    {
                        // T022: on-demand snapshot. ACK carries "snapshot seq=N"
                        // (plus any loud store-degradation note, US2/AS-4);
                        // DEFERRED means parked pending quiescence (wire rule 5) —
                        // check :status for the pending/last-seq state.
                        var response = await channel.RoundTripAsync(
                            RequestFrame.Empty(channel.NextRequestId(), RequestKind.Snapshot));
                        Console.WriteLine(response.Kind switch
                        {
                            ResponseKind.Ack => response.BodyText(),
                            ResponseKind.Deferred =>
                                "snapshot deferred (engine busy) — it fires at the next quiescence; see :status",
                            ResponseKind.EngineBusy => "Engine busy (restoring) — try again shortly",
                            _ => $"!! protocol error: {response.BodyText()}",
                        });
                        continue;
                    }

                    if (trimmed.EndsWith(".glp", StringComparison.Ordinal))
                    {
                        if (il is not null)
                            await LoadFileIlAsync(channel, il, trimmed);
                        else
                            await LoadFileAsync(channel, trimmed);
                        continue;
                    }

                    if (il is not null)
                        await RunGoalIlAsync(channel, il, trimmed);
                    else
                        await RunGoalAsync(channel, trimmed);
                }
                catch (ClientTransportException ex)
                {
                    // FR-007: transport failure, rendered distinctly from any goal failure.
                    Console.WriteLine($"!! transport failure: {ex.Message}");
                    return 69;
                }
                catch (SplitProtocolException ex)
                {
                    Console.WriteLine($"!! protocol error: {ex.Message}");
                    return 70;
                }
            }
        }
        return 0;
    }

    /// <summary>Filename + path resolution shared by the text and IL load paths.
    /// Mirrors the single-process REPL exactly (parity): /, ../, ./ prefixes
    /// used as-is, else Path.Combine("glp", name) — where a rooted Windows path
    /// also wins over the "glp" prefix. Null = not a load line / missing file
    /// (already reported).</summary>
    private static (string Filename, string SourcePath)? ResolveLoad(string trimmed)
    {
        string filename;
        if (trimmed.StartsWith("load ", StringComparison.Ordinal))
            filename = trimmed[5..].Trim();
        else if (!trimmed.Contains(' '))
            filename = trimmed;
        else
            return null;

        string sourcePath;
        if (filename.StartsWith("/", StringComparison.Ordinal) ||
            filename.StartsWith("../", StringComparison.Ordinal) ||
            filename.StartsWith("./", StringComparison.Ordinal))
        {
            sourcePath = filename;
        }
        else
        {
            sourcePath = Path.Combine("glp", filename);
        }

        if (!File.Exists(sourcePath))
        {
            Console.WriteLine($"Error: File not found: {sourcePath}");
            return null;
        }
        return (filename, sourcePath);
    }

    private static async Task LoadFileAsync(ClientChannel channel, string trimmed)
    {
        var resolved = ResolveLoad(trimmed);
        if (resolved is null)
            return;
        var (filename, sourcePath) = resolved.Value;

        // FR-001: the client ships SOURCE TEXT; the full pipeline runs engine-side.
        var source = await File.ReadAllTextAsync(sourcePath);
        var response = await channel.RoundTripAsync(
            RequestFrame.Text(channel.NextRequestId(), RequestKind.LoadSource, source));

        switch (response.Kind)
        {
            case ResponseKind.Ack:
                Console.WriteLine($"✓ Loaded: {filename}");
                break;
            case ResponseKind.Result:
                var envelope = ResultEnvelopeCodec.Decode(response.Body);
                Console.WriteLine($"Error loading {filename}: {envelope.Error}");
                break;
            case ResponseKind.EngineBusy:
                Console.WriteLine("Engine busy (restoring) — try again shortly");
                break;
            default:
                Console.WriteLine($"!! protocol error: {response.BodyText()}");
                break;
        }
    }

    private static async Task RunGoalAsync(ClientChannel channel, string goal)
    {
        var response = await channel.RoundTripAsync(
            RequestFrame.Text(channel.NextRequestId(), RequestKind.RunGoal, goal));

        switch (response.Kind)
        {
            case ResponseKind.Result:
                RenderEnvelope(ResultEnvelopeCodec.Decode(response.Body));
                break;
            case ResponseKind.EngineBusy:
                Console.WriteLine("Engine busy (restoring) — try again shortly");
                break;
            default:
                Console.WriteLine($"!! protocol error: {response.BodyText()}");
                break;
        }
        Console.WriteLine();
    }

    // ---- 064 US3: the IL session's load/run half (compile local, ship IL) ----

    private static async Task LoadFileIlAsync(ClientChannel channel, IlSession il, string trimmed)
    {
        var resolved = ResolveLoad(trimmed);
        if (resolved is null)
            return;
        var (filename, sourcePath) = resolved.Value;

        byte[] envelope;
        try
        {
            var source = await File.ReadAllTextAsync(sourcePath);
            // The full pipeline runs HERE (contract rule 5) — compile/type errors
            // render exactly as loudly as the engine's text path would.
            envelope = il.CompileUnit(source, filename);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading {filename}: {ex.Message}");
            return;
        }

        var response = await channel.RoundTripAsync(new RequestFrame(
            channel.NextRequestId(), RequestKind.LoadIl, envelope));
        if (RenderIlLoadResponse(response, filename))
            Console.WriteLine($"✓ Loaded (IL): {filename}");
    }

    private static async Task RunGoalIlAsync(ClientChannel channel, IlSession il, string goal)
    {
        RunGoalIlBody body;
        try
        {
            body = il.GoalBody(goal);
        }
        catch (Exception ex)
        {
            // A goal that does not compile is a LOUD local error — never a
            // silent retry over the text path (contract rule 3).
            Console.WriteLine($"Error compiling goal: {ex.Message}");
            return;
        }

        var response = await channel.RoundTripAsync(new RequestFrame(
            channel.NextRequestId(), RequestKind.RunGoalIl, body.Encode()));
        switch (response.Kind)
        {
            case ResponseKind.Result:
                RenderEnvelope(ResultEnvelopeCodec.Decode(response.Body));
                break;
            case ResponseKind.IlRefused:
                var refusal = IlRefusal.Decode(response.Body);
                Console.WriteLine($"!! IL refused ({RefusalWord(refusal.Code)}): {refusal.Reason}");
                break;
            case ResponseKind.EngineBusy:
                Console.WriteLine("Engine busy (restoring) — try again shortly");
                break;
            default:
                Console.WriteLine($"!! protocol error: {response.BodyText()}");
                break;
        }
        Console.WriteLine();
    }

    /// <summary>Render a LOAD_IL response; true = loaded (caller prints the ✓ line).</summary>
    private static bool RenderIlLoadResponse(ResponseFrame response, string moduleRef)
    {
        switch (response.Kind)
        {
            case ResponseKind.Ack:
                return true;
            case ResponseKind.IlRefused:
                var refusal = IlRefusal.Decode(response.Body);
                Console.WriteLine(
                    $"Error loading {moduleRef} — IL refused ({RefusalWord(refusal.Code)}): {refusal.Reason}");
                return false;
            case ResponseKind.EngineBusy:
                Console.WriteLine("Engine busy (restoring) — try again shortly");
                return false;
            default:
                Console.WriteLine($"!! protocol error: {response.BodyText()}");
                return false;
        }
    }

    private static string RefusalWord(IlRefusalCode code) => code switch
    {
        IlRefusalCode.Malformed => "malformed",
        IlRefusalCode.IlVersionMismatch => "il_version_mismatch",
        IlRefusalCode.DigestMismatch => "digest_mismatch",
        IlRefusalCode.MidTransferTruncation => "mid_transfer_truncation",
        _ => $"0x{(byte)code:X2}",
    };

    private static void RenderEnvelope(ResultEnvelope envelope)
    {
        // R3 output blob first — the REPL prints program output during execution,
        // before bindings and status.
        if (envelope.Captured.Length > 0)
        {
            var lines = Encoding.UTF8.GetString(envelope.Captured).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && lines[i].Length == 0)
                    break; // trailing empty segment from the final '\n'
                Console.WriteLine(lines[i]);
            }
        }

        // R6: bindings arrive pre-rendered as display strings.
        foreach (var kv in envelope.ResolvedBindings)
        {
            var rendered = kv.Value is ConstTerm { Value: ConstString s }
                ? s.Value
                : kv.Value.ToString();
            Console.WriteLine($"{kv.Key} = {rendered}");
        }

        Console.WriteLine(envelope.Status switch
        {
            ExecutionStatus.Success => "→ succeeds",
            ExecutionStatus.Failed => "→ failed",
            ExecutionStatus.Suspended => "→ suspended",
            _ => throw new ArgumentOutOfRangeException(nameof(envelope.Status)),
        });

        if (envelope.Error is not null)
            Console.WriteLine($"Error: {envelope.Error}");
    }
}
