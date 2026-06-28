//// glp_quick_gleam — the Gleam data-plane stack (feature 036, US3 Profile A).
////
//// The Gleam/BEAM process performs the channel-link ROLE (lifecycle, control/data demux over the
//// uniform stdio seam) and delegates the genuine QUIC + RFC 6455 WebSocket termination to a
//// **native side-process** — the verified C# `glp_quick_host` — over line-delimited local IPC.
//// This is honest per Decision 8 / constitution II: QUIC is real (the side-process performs it);
//// `quic_termination` is `side_process`, never simulated in-runtime.
////
//// Invocation (by the Python `gleam` StackAdapter):
////   gleam run -- <dotnet> <host-dll> --role <server|client> --addr A --port P --cert DIR ...
//// The first two args are the side-process launcher (dotnet + the host dll); the rest are the
//// identical host CLI args the C# stack uses — so the wire/CLI contract is unchanged (FR-010).

import argv
import gleam/io

/// The line relay between our stdio (facing the Python control plane) and the QUIC side-process:
///  - our stdin            -> side-process stdin   (L5 envelopes to send)
///  - side-process stdout  -> our stdout           (received L5 envelopes; lines starting with `{`)
///  - side-process stderr  -> our stderr           (control: READY / LINK_UP / ERR / FAULT / ...)
/// Implemented in Erlang FFI (`glpq_ffi:relay/3`) using an OS port; the demux keeps the observable
/// stdio behaviour byte-identical to the C# stack so the two are interchangeable (SC-006).
@external(erlang, "glpq_ffi", "relay")
fn relay(dotnet: String, dll: String, host_args: List(String)) -> Int

pub fn main() {
  case argv.load().arguments {
    [dotnet, dll, ..host_args] -> {
      let status = relay(dotnet, dll, host_args)
      halt(status)
    }
    _ -> {
      io.println_error(
        "usage: glp_quick_gleam <dotnet> <host-dll> --role <server|client> --addr A --port P --cert DIR ...",
      )
      halt(2)
    }
  }
}

@external(erlang, "erlang", "halt")
fn halt(status: Int) -> a
