//// glp/link/primitives/link_kernels — the effectful link-kernel registry + the
//// runner recognition seam (feature 059, T076 — port of the kernel names/arities in
//// glp_runtime/lib/link/primitives/link_kernels.dart, mirror
//// csharp/glp_link/primitives/LinkKernels.cs).
////
//// The 7 host body kernels the link-layer GLP wrappers (self.glp) call, quote-
//// stripped to their bare label (the standard-kernel convention — `'_link_setup'`
//// compiles to the label `_link_setup`, split on `/` to the bare name at dispatch):
////
////   _link_setup/5   establish/reuse a link by ground LinkId in a role (path A)
////   _link_send/3    ship a ground term to a ground peer over a named link
////   _link_request/5 path-B connector: initiate an in-band link-request handshake
////   _link_listen/3  path-B rendezvous: surface inbound request tokens
////   _link_accept/5  path-B acceptor: adopt a pending connection for a served LinkId
////   _link_monitor/2 obtain the per-link fault monitor stream
////   _link_close/2   abrupt teardown (distributed GC + closed(LinkId,Reason))
////
//// These are EFFECTFUL over the link runtime + external transport I/O (sockets,
//// inbound frames) — parallel to the madGLP `_send` kernel, which is effectful over
//// madGLP state. Like `_send`, they are dispatched at the BODY Spawn label-miss AFTER
//// the pure `kernels.dispatch` misses (runner.gleam). `is_link_kernel` is the
//// recognition the runner consults so a link label is treated as a link kernel
//// awaiting its driver, NOT a generic "unresolved procedure" error.
////
//// DRIVER BOUNDARY (T074 `close-link-inbound-pump`): actually RUNNING a link kernel
//// requires (a) a `LinkRuntime` threaded through the reduction (the analogue of
//// `MadState` — a `step_link`/pump driver in the scheduler, mirroring `step_mad`),
//// (b) the inbound PUMP that binds the `In` stream writer as frames arrive and
//// re-drives the scheduler to quiescence, and (c) the `Out`-stream egress drainer
//// (a `known(Out?)`-guarded runnable goal — the Gleam heap has no `onBind`, so the
//// Dart `heap.onBind` drainer lowers to a goal, exactly as madGLP lowers reader-
//// branch `global_send` spawns). The synchronous T076 trunk (transport selection,
//// idempotent registry, handle + cursor wiring, ground-relay ship framing, fault-as-
//// data lattice) is what that driver composes over. Until the driver is present, a
//// link kernel invoked in the pure `run` path fails NON-FATALLY (the runner
//// recognition below), exactly as a madGLP kernel does outside madGLP mode.

/// `_link_setup/5` — establish/reuse a link (path A).
pub const setup_name = "_link_setup"

pub const setup_arity = 5

/// `_link_send/3` — the LinkId-keyed ground-relay sender.
pub const send_name = "_link_send"

pub const send_arity = 3

/// `_link_request/5` — path-B connector handshake.
pub const request_name = "_link_request"

pub const request_arity = 5

/// `_link_listen/3` — path-B rendezvous listener (surfaces request tokens).
pub const listen_name = "_link_listen"

pub const listen_arity = 3

/// `_link_accept/5` — path-B acceptor (adopts a pending connection).
pub const accept_name = "_link_accept"

pub const accept_arity = 5

/// `_link_monitor/2` — per-link fault monitor stream.
pub const monitor_name = "_link_monitor"

pub const monitor_arity = 2

/// `_link_close/2` — abrupt teardown.
pub const close_name = "_link_close"

pub const close_arity = 2

/// Is `name/arity` one of the 7 effectful link kernels? The runner consults this at
/// the BODY Spawn label-miss (parallel to `mad_kernels.mad_is_kernel`).
pub fn is_link_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_link_setup", 5 -> True
    "_link_send", 3 -> True
    "_link_request", 5 -> True
    "_link_listen", 3 -> True
    "_link_accept", 5 -> True
    "_link_monitor", 2 -> True
    "_link_close", 2 -> True
    _, _ -> False
  }
}
