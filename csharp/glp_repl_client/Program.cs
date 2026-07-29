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
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connect" when i + 1 < args.Length:
                    connect = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"glp_repl_client: unknown argument '{args[i]}'");
                    Console.Error.WriteLine("usage: glp_repl_client --connect <host:port>");
                    return 64;
            }
        }
        if (connect is null)
        {
            Console.Error.WriteLine("glp_repl_client: --connect <host:port> is required");
            return 64;
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
        Console.WriteLine("Input: load <file.glp> to load, goal. to execute; :status, :quit");
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

                    if (trimmed.EndsWith(".glp", StringComparison.Ordinal))
                    {
                        await LoadFileAsync(channel, trimmed);
                        continue;
                    }

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

    private static async Task LoadFileAsync(ClientChannel channel, string trimmed)
    {
        string filename;
        if (trimmed.StartsWith("load ", StringComparison.Ordinal))
            filename = trimmed[5..].Trim();
        else if (!trimmed.Contains(' '))
            filename = trimmed;
        else
            return;

        // Path resolution mirrors the single-process REPL exactly (parity):
        // /, ../, ./ prefixes used as-is, else Path.Combine("glp", name) —
        // where a rooted Windows path also wins over the "glp" prefix.
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
            return;
        }

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
