//// glp/link/seam/endpoint — one established, bilateral link end
//// (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/i_link_endpoint.dart` (mirror
//// `csharp/glp_link/seam/ILinkEndpoint.cs`). Carries opaque, self-delimiting frames
//// in FIFO order; the reliability sublayer (FrameCodec, sequence/dedup — T046) sits
//// ABOVE this and the engine above that. The endpoint itself knows nothing about
//// terms or SRSW (architecture-context.md §3).
////
//// Gleam shape: the Dart/C# interface is expressed as a record-of-functions vtable
//// so each transport leaf (loopback T048 / tcp T049 / quic T055) supplies its own
//// closures. This is a HOST seam BELOW GLP, so the synchronous-`Result` +
//// `gleam_erlang` `Subject` mapping is a language-mapping choice, not a GLP-semantic
//// deviation (the frozen 025 semantics live above the seam, in the primitives —
//// T050). No `gleam_otp` (AtomVM subset): faults arrive out-of-band on a Subject.

import gleam/erlang/process.{type Subject}
import gleam/option.{type Option}
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/link/seam/link_id.{type LinkId}

/// One established bilateral link end.
///
/// - `id`: the stable, never-reused link identity (FR-007); both establishment
///   paths (listen/connect and request/accept) yield endpoints with an equivalent
///   id (FR-002).
/// - `send`: send exactly one self-delimiting frame (an opaque payload blob); a
///   leaf MUST deliver accepted frames in submission order or report a fault
///   (FR-018).
/// - `recv`: receive the next self-delimiting frame, awaiting one if none is
///   buffered. `Ok(Some(frame))` = one complete frame; `Ok(None)` = the peer
///   cleanly ended the stream (→ `closed(LinkId, eos)` upstream); `Error(_)` = a
///   transport fault.
/// - `close`: tear this end down; graceful close drains in-flight frames and ends
///   the peer's `recv` with `Ok(None)`; the sublayer emits the terminal
///   `closed(LinkId, Reason)` term and runs distributed GC (FR-024).
/// - `faults`: out-of-band transport faults (FR-008). A fault here never becomes a
///   unification verdict (FR-043) and never fails the reader's data goal (FR-044).
pub type Endpoint {
  Endpoint(
    id: LinkId,
    send: fn(BitArray) -> Result(Nil, LinkFaultSignal),
    recv: fn() -> Result(Option(BitArray), LinkFaultSignal),
    close: fn() -> Nil,
    faults: Subject(LinkFaultSignal),
  )
}
