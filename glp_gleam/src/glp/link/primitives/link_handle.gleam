//// glp/link/primitives/link_handle — the per-instance state of one established
//// link (wave-3 US4, T035).
////
//// Port of `glp_runtime/lib/link/primitives/link_handle.dart` (mirror C#
//// `LinkHandle`): the transport endpoint plus the reliability bundle
//// (sequencer, send window, reassembler, inbound ordering). Stored in the
//// `LinkRegistry` keyed by `LinkId` so a re-setup with the same identity is
//// refused/reused (FR-007-025).
////
//// Gleam mapping: the Dart deferred-endpoint machinery (`endpointReady`,
//// `attachEndpoint`, the egress readiness gate) exists because Dart's single
//// isolate cannot block on the connect. The Gleam seam's `listen`/`connect`
//// are SYNCHRONOUS — establishment returns only once the rendezvous resolved —
//// so a handle ALWAYS holds a ready endpoint and the amended contract's "no
//// program traffic before establishment completes" is discharged by
//// construction. Immutable — reliability ops return the advanced handle.

import glp/link/reliability/frame_reassembler.{type FrameReassembler}
import glp/link/reliability/inbound_ordering.{type InboundOrdering}
import glp/link/reliability/link_sequencer.{type LinkSequencer}
import glp/link/reliability/send_window.{type SendWindow}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}

pub type LinkHandle {
  LinkHandle(
    id: LinkId,
    options: LinkOptions,
    endpoint: Endpoint,
    /// Outbound sequence source (→ frame `MessageId`).
    sequencer: LinkSequencer,
    /// Bounded backpressure window.
    window: SendWindow,
    /// Inbound fragment reassembly.
    reassembler: FrameReassembler,
    /// Inbound FIFO reconstruction + transport-level dedup.
    ordering: InboundOrdering,
    /// Set once the link is being / has been torn down.
    closed: Bool,
  )
}

/// A fresh handle over an established endpoint (the reference's field
/// initializers: sequencer 0, window from options, default reassembler and
/// ordering bounds).
pub fn new(
  id: LinkId,
  options: LinkOptions,
  endpoint: Endpoint,
) -> Result(LinkHandle, String) {
  case send_window.new(options.backpressure_window) {
    Error(reason) -> Error(reason)
    Ok(window) ->
      Ok(LinkHandle(
        id: id,
        options: options,
        endpoint: endpoint,
        sequencer: link_sequencer.new(0),
        window: window,
        reassembler: frame_reassembler.default(),
        ordering: inbound_ordering.default(),
        closed: False,
      ))
  }
}
