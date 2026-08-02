using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// T021 (feature 062 US3) — end-to-end over the REAL ZeroMQ transport: compile-on-A →
/// <see cref="CompiledIlEnvelope"/> → <see cref="FrameCodec"/> → ZMQ PAIR wire →
/// reassemble-on-B → verify+decode → execute, with B's result EQUAL to local. Ties the
/// compiled-IL envelope (T016), the receiver path (T017), and the ZMQ base (T020)
/// together over a genuine cross-socket link, across every runnable status.
/// </summary>
[Trait("Category", "ZmqExecute")]
public class ZmqEnvelopeExecuteTests
{
    private static CancellationToken Timeout15 => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public static IEnumerable<object[]> RunnableCases =>
        Corpus.Runnable().Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(RunnableCases))]
    public async Task Compiled_IL_over_zmq_executes_equally_to_local(string name)
    {
        var c = Corpus.ByName(name);
        var ct = Timeout15;

        // A: compile → wrap → frame for the wire (small MTU → exercise fragment reassembly on B).
        byte[] envelope = CompiledIlEnvelopeCodec.Wrap(c.Program, "zmq/" + c.Name, c.VariableMap);
        var frames = FrameCodec.Encode(envelope, messageId: 0x7A00, maxFrameBytes: FrameCodec.HeaderSize + 32);

        int port = FreeTcpPort();
        var transport = new ZmqTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        var server = await transport.ListenAsync(LinkScheme.Zmq, addr, LinkOptions.Default, ct);
        var client = await transport.ConnectAsync(LinkScheme.Zmq, addr, LinkOptions.Default, ct);

        try
        {
            foreach (var f in frames)
                await client.SendBytesAsync(f, ct);

            // B: pull frames off the wire and reassemble the whole envelope.
            var reasm = new FrameReassembler();
            byte[]? whole = null;
            while (whole is null)
            {
                var frameBytes = await server.RecvBytesAsync(ct);
                Assert.NotNull(frameBytes); // the stream must not end before the envelope is whole
                whole = reasm.Accept(FrameCodec.ParseFrame(frameBytes!));
            }

            var (decoded, _) = CompiledIlEnvelopeCodec.Unwrap(whole);
            var onB = await GlpExecutor.RunNullaryAsync(decoded.Program, c.Goal!);
            var local = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);

            Assert.Equal(local, onB);
            Assert.Equal(c.ExpectedStatus, onB);
        }
        finally
        {
            await client.DisposeAsync();
            await server.DisposeAsync();
        }
    }
}
