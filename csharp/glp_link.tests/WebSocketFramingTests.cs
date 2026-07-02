using System.IO;

using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Transports;

using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Regression for the 036 code-review fix (finding #1, 2026-07-02): the RFC 6455 decoder must
/// bound the frame length so a corrupt/huge length from an (authenticated) peer surfaces as a
/// clean <see cref="FrameException"/> — never an <c>OverflowException</c>/OOM crash (FR-019).
/// </summary>
public class WebSocketFramingTests
{
    [Fact]
    public async Task OversizedDataFrame_ThrowsFrameException_NotOverflowOrOom()
    {
        // FIN|binary (0x82), len7=127 → 8-byte length = 256 MiB (> the 16 MiB bound), unmasked.
        var frame = new byte[] { 0x82, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00 };
        using var ms = new MemoryStream(frame);
        var ws = new WebSocketOverQuic(ms);
        await Assert.ThrowsAsync<FrameException>(() => ws.ReceiveFrameAsync());
    }

    [Fact]
    public async Task ControlFrameOver125_ThrowsFrameException()
    {
        // FIN|ping (0x89), len7=126 → 16-bit length = 126 (> the RFC 6455 §5.5 control-frame max of 125).
        var frame = new byte[] { 0x89, 126, 0x00, 0x7E };
        using var ms = new MemoryStream(frame);
        var ws = new WebSocketOverQuic(ms);
        await Assert.ThrowsAsync<FrameException>(() => ws.ReceiveFrameAsync());
    }
}
