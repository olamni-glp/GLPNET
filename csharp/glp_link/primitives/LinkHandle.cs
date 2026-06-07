using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The per-instance state of one established link: its transport endpoint plus the
/// Phase-2 reliability bundle (sequencer, send window, reassembler, inbound
/// ordering). Stored in the <see cref="LinkRegistry"/> keyed by <see cref="LinkId"/>
/// so a re-setup with the same identity reuses it (FR-007). The heap stream cursors
/// (the In/Faults writers the host extends and the Out reader it drains) are wired
/// in by the <c>'_link_setup'</c> kernel during establishment.
/// </summary>
public sealed class LinkHandle
{
    public LinkId Id { get; }
    public ILinkEndpoint Endpoint { get; }
    public LinkOptions Options { get; }

    /// <summary>Outbound sequence source (→ frame MessageId).</summary>
    public LinkSequencer Sequencer { get; }

    /// <summary>Bounded backpressure window (N from <see cref="LinkOptions.BackpressureWindow"/>).</summary>
    public SendWindow Window { get; }

    /// <summary>Inbound fragment reassembly.</summary>
    public FrameReassembler Reassembler { get; }

    /// <summary>Inbound FIFO reconstruction + transport-level dedup.</summary>
    public InboundOrdering Ordering { get; }

    // --- heap stream cursors, set by '_link_setup' during establishment ---

    /// <summary>The writer the host extends as inbound frames arrive (the program reads <c>In</c>).</summary>
    public int? InWriterAddr { get; set; }

    /// <summary>The reader the host drains as the program writes <c>Out</c>.</summary>
    public int? OutReaderAddr { get; set; }

    /// <summary>The writer the host extends with fault terms (the program reads <c>Faults</c>).</summary>
    public int? FaultsWriterAddr { get; set; }

    /// <summary>
    /// The live per-link MONITOR cursors (feature 025, T034): one writer cell per
    /// independent fault observer. A fault is fanned out to every cursor — the
    /// <c>Faults</c> stream minted at establishment AND every later <c>link_monitor</c>
    /// stream — so faults are independently observable from the data path and from each
    /// other (FR-008). Each entry advances to a fresh writer as its stream is extended;
    /// mutated only on the runner thread (the kernel and the pump's apply step).
    /// </summary>
    public List<int> MonitorCursors { get; } = new();

    public LinkHandle(LinkId id, ILinkEndpoint endpoint, LinkOptions options)
    {
        Id = id;
        Endpoint = endpoint;
        Options = options;
        Sequencer = new LinkSequencer();
        Window = new SendWindow(options.BackpressureWindow);
        Reassembler = new FrameReassembler();
        Ordering = new InboundOrdering();
    }
}
