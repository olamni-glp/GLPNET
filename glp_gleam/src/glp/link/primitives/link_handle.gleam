//// glp/link/primitives/link_handle — the per-instance state of ONE established link
//// (feature 050, T050.C1).
////
//// Port of `csharp/glp_link/primitives/LinkHandle.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_handle.dart`). Stored in the
//// `link_registry` keyed by ground `LinkId`, so a re-setup with the same identity
//// reuses it (FR-007).
////
//// **Immutability difference from the oracles.** The C#/Dart handles are mutable
//// objects whose cursors are assigned in place during establishment and advanced by
//// the pump. Gleam records are immutable, so every mutation here returns a NEW handle
//// and the caller threads it back into the registry (`link_registry.put`). This is a
//// host-mapping choice, not a semantic one — the GLP-visible behaviour is identical.
////
//// **Reliability bundle: deliberately partial (deviation D8).** The C# handle carries
//// `Sequencer`, `SendWindow`, `FrameReassembler` and `InboundOrdering`. In Gleam only
//// the framing half of the reliability sublayer exists today (`link/reliability/`
//// = `crc32` + `frame_codec`); sequence/dedup/reorder/epoch-fencing are **T052**, not
//// T050 (`contracts/link-primitives-port.md` §5 D8). Rather than stub four fields that
//// nothing can honour yet, the handle carries the ONE piece egress genuinely needs now
//// — a monotone outbound sequence counter — and T052 adds the rest. Nothing here
//// pretends to deduplicate or reorder.

import gleam/option.{type Option, None}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}

/// One established link.
///
/// - `id` / `endpoint` / `options`: identity, the open transport end, and the
///   per-link options the leaf was opened with.
/// - `next_seq`: the outbound sequence source (→ frame message id). Monotone,
///   never reused for this link. T052 grows this into the full sequencer.
/// - `in_writer` / `out_reader` / `faults_writer`: the heap stream cursors, wired in
///   during establishment (`link_establish.wire_established_link`). `None` until then,
///   which is why they are `Option` rather than a sentinel address — an unwired cursor
///   is a distinct state, not address 0.
/// - `monitor_cursors`: one writer cell per INDEPENDENT fault observer (FR-008) — the
///   `Faults` stream minted at establishment plus every later `link_monitor/2` stream.
///   A fault is fanned out to every cursor, so observers never alias each other. C7
///   populates these; C1 only carries them.
pub type LinkHandle {
  LinkHandle(
    id: LinkId,
    endpoint: Endpoint,
    options: LinkOptions,
    next_seq: Int,
    in_writer: Option(Int),
    out_reader: Option(Int),
    faults_writer: Option(Int),
    monitor_cursors: List(Int),
  )
}

/// A freshly established handle: identity + open endpoint + options, no cursors wired
/// and no monitors registered yet. Sequence numbering starts at 0 (the first frame on
/// a link is message 0 — matching the C# `LinkSequencer` and the T046 frame codec).
pub fn new(
  id: LinkId,
  endpoint: Endpoint,
  options: LinkOptions,
) -> LinkHandle {
  LinkHandle(
    id: id,
    endpoint: endpoint,
    options: options,
    next_seq: 0,
    in_writer: None,
    out_reader: None,
    faults_writer: None,
    monitor_cursors: [],
  )
}

/// Take the next outbound sequence number, returning it with the advanced handle.
/// Monotone and never reused — the caller MUST thread the returned handle back into
/// the registry, or two frames would share a sequence number.
pub fn take_seq(handle: LinkHandle) -> #(LinkHandle, Int) {
  #(LinkHandle(..handle, next_seq: handle.next_seq + 1), handle.next_seq)
}

/// Wire the heap stream cursors during establishment (the `In` writer the host
/// extends, the `Out` reader it drains, the `Faults` writer it extends).
pub fn with_cursors(
  handle: LinkHandle,
  in_writer: Int,
  out_reader: Int,
  faults_writer: Int,
) -> LinkHandle {
  LinkHandle(
    ..handle,
    in_writer: option.Some(in_writer),
    out_reader: option.Some(out_reader),
    faults_writer: option.Some(faults_writer),
  )
}

/// Register one more independent fault-monitor cursor (C7 `link_monitor/2`).
pub fn add_monitor_cursor(handle: LinkHandle, addr: Int) -> LinkHandle {
  LinkHandle(..handle, monitor_cursors: [addr, ..handle.monitor_cursors])
}
