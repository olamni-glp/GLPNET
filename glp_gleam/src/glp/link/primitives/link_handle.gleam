//// glp/link/primitives/link_handle — the per-instance state of one established link
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_handle.dart,
//// mirror csharp/glp_link/primitives/LinkHandle.cs).
////
//// One `LinkHandle` per established link, stored in the `LinkRegistry` keyed by
//// `LinkId` so a re-setup with the same identity reuses it (FR-007). Holds the
//// transport endpoint (deferred: `None` until the rendezvous resolves), the closed
//// flag, the outbound sequence counter, and the heap stream cursors the kernels wire
//// during establishment — the writer the host extends as inbound frames arrive
//// (`in_writer`), the reader the host drains as the program writes `Out`
//// (`out_reader`), the fault writer (`faults_writer`), and the live monitor cursors
//// a fault fans out to (FR-008).
////
//// GLEAM MAPPING NOTE: the Dart handle is a MUTABLE object the async pump/egress
//// mutate in place. Here it is an IMMUTABLE record threaded through the (T074) link
//// driver — a re-setup produces a NEW handle value in a NEW registry. The Dart
//// reliability bundle (sequencer / send-window / reassembler / inbound-ordering) is
//// reduced to the monotone `seq` counter for T076; the window + reassembly + dedup
//// land with T077 (`close-link-layer-sequence-dedup`).

import gleam/option.{type Option, None, Some}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}

/// The state of one established link.
pub type LinkHandle {
  LinkHandle(
    id: LinkId,
    options: LinkOptions,
    /// The transport endpoint, DEFERRED until the rendezvous resolves (`None` while
    /// connecting; the egress ship and the inbound pump gate on it — T074).
    endpoint: Option(Endpoint),
    /// Set once the link is being / has been torn down (graceful `[]` close, abrupt
    /// `_link_close`, or a connect failure).
    closed: Bool,
    /// Outbound monotone sequence source (→ frame MessageId). T077 adds the window.
    seq: Int,
    /// The writer the host extends as inbound frames arrive (program reads `In`).
    in_writer: Option(Int),
    /// The reader the host drains as the program writes `Out`. The pump ADVANCES
    /// this as it ships each bound cons head (the moving egress cursor).
    out_reader: Option(Int),
    /// The pump has shipped `Out = []` and closed the send half (graceful eos). No
    /// further egress on this link.
    out_closed: Bool,
    /// The pump has bound `In = []` (peer eos) — the inbound stream is ended. No
    /// further `recv` on this link.
    in_closed: Bool,
    /// The writer the host extends with fault terms (program reads `Faults`).
    faults_writer: Option(Int),
    /// The live per-link MONITOR cursors (T034): one writer cell per independent
    /// fault observer (the establishment `Faults` stream + every later
    /// `link_monitor` stream). A fault fans out to every cursor (FR-008).
    monitor_cursors: List(Int),
  )
}

/// A fresh handle with no endpoint yet (the establish core wires the cursors and,
/// later, `attach_endpoint` publishes the resolved endpoint — T074).
pub fn new(id: LinkId, options: LinkOptions) -> LinkHandle {
  LinkHandle(
    id: id,
    options: options,
    endpoint: None,
    closed: False,
    seq: 0,
    in_writer: None,
    out_reader: None,
    out_closed: False,
    in_closed: False,
    faults_writer: None,
    monitor_cursors: [],
  )
}

/// Publish the resolved transport endpoint (the rendezvous completed).
pub fn attach_endpoint(handle: LinkHandle, ep: Endpoint) -> LinkHandle {
  LinkHandle(..handle, endpoint: Some(ep))
}

/// Take the next outbound sequence number, returning it with the advanced handle
/// (the immutable analogue of the Dart `sequencer.next()` side effect).
pub fn next_seq(handle: LinkHandle) -> #(LinkHandle, Int) {
  #(LinkHandle(..handle, seq: handle.seq + 1), handle.seq)
}

/// Add a live monitor cursor (the establishment `Faults` stream, or a later
/// `link_monitor` observer — FR-008).
pub fn add_monitor_cursor(handle: LinkHandle, writer: Int) -> LinkHandle {
  LinkHandle(..handle, monitor_cursors: [writer, ..handle.monitor_cursors])
}

/// Mark the link torn down (close / permFail).
pub fn mark_closed(handle: LinkHandle) -> LinkHandle {
  LinkHandle(..handle, closed: True)
}
