//// glp/link/transports/quic_ws — the QUIC-WS transport leaf (feature 050, T055).
////
//// **Genuine QUIC, Windows-native, no fork — Profile A (Gabi's ruling 2026-07-27).**
//// QUIC + RFC 6455 termination is performed by the verified C# `glp_quick_host`, driven here
//// as an OS side-process (`glp_link_quic_ffi`). `quic_termination = side_process`: real, and
//// labelled honestly (constitution II / 036 Decision 8) — nothing is simulated in-runtime.
////
//// **Why not in-process (`quicer`/MsQuic NIF)?** Investigated to the end 2026-07-27 and
//// blocked upstream, not by this host: MsQuic itself builds fine under MSVC here, but
//// `quicer`'s own NIF C sources are POSIX/GCC-only (`#include <dlfcn.h>`,
//// `__attribute__((unused))`) — which is exactly why its `rebar.config` gates the NIF build
//// to `linux|darwin|solaris`. In-process would mean forking and porting a third-party NIF.
//// Recipe + findings: `gleam_quic/profile_c/README.md`. Profile A needs none of it.
////
//// **Framing, and why the wire stays byte-identical to C#:**
////   * IPC leg (this process ↔ host stdio) is line-delimited UTF-8, so a frame rides as
////     BASE64 — raw binary cannot survive it (CRC bytes, length prefixes, newlines).
////   * The host's `--binary` mode base64-DECODES before sending, so the QUIC/WS wire carries
////     the RAW 025 frame — byte-for-byte what `glp_link`'s `QuicTransport` sends. Cross-runtime
////     parity (US5) is preserved; base64 never reaches the wire.
////
//// Seam shape matches every other leaf (T045): `listen`/`connect` return an `Endpoint` whose
//// `recv` blocks. The host's `--role client` dials; `--role server` binds and accepts. The
//// `LinkAddress` supplies host+port; certs come from a directory (mutual-pinned shared cert).

import gleam/erlang/process.{type Pid}
import gleam/int
import gleam/option.{None, Some}
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport, Transport}

/// How to launch the side-process. Supplied at the composition root rather than discovered
/// here: the leaf must not guess where dotnet or the host dll live, and a test may point at
/// a different build. `cert_dir` is the shared-cert directory (`glpquick-cert/`).
pub type HostSpec {
  HostSpec(dotnet: String, dll: String, cert_dir: String)
}

/// The QUIC-WS leaf. `spec` says how to launch the genuine-QUIC host; `connect_timeout_ms`
/// bounds the wait for `LINK_UP`.
pub fn new(spec: HostSpec) -> Transport {
  Transport(
    supported_schemes: [link_scheme.quic()],
    listen: fn(scheme, addr, opts) { open(spec, scheme, addr, opts, "server") },
    connect: fn(scheme, addr, opts) { open(spec, scheme, addr, opts, "client") },
  )
}

fn open(
  spec: HostSpec,
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
  role: String,
) -> Result(Endpoint, LinkFaultSignal) {
  let id = address_id(scheme, addr)
  let port = case addr.port {
    Some(p) -> p
    // A QUIC endpoint is host+port by definition; a bare-string address has no port to dial.
    None -> 0
  }
  case port {
    0 ->
      Error(LinkFaultSignal(
        id,
        Permanent,
        "quic address needs an ep(Host, Port) form — got a bare path",
      ))
    _ -> {
      let args = [
        spec.dll,
        "--role",
        role,
        "--addr",
        addr.host,
        "--port",
        int.to_string(port),
        "--cert",
        spec.cert_dir,
        // Opaque-binary stdio: base64 on this leg, RAW frames on the wire.
        "--binary",
      ]
      case ffi_open(spec.dotnet, args, opts.connect_timeout_ms) {
        Error(reason) -> Error(LinkFaultSignal(id, Transient, reason))
        Ok(pid) -> Ok(endpoint_over(id, pid))
      }
    }
  }
}

/// Wrap the owning process as a seam `Endpoint`. Faults are DATA on the caller-owned subject
/// (FR-043/044) exactly as in the loopback/tcp leaves; a fault returned by `recv` is also
/// published there so an independent monitor sees it.
fn endpoint_over(id: LinkId, pid: Pid) -> Endpoint {
  let faults = process.new_subject()
  Endpoint(
    id: id,
    send: fn(frame) {
      case ffi_send(pid, frame) {
        Ok(Nil) -> Ok(Nil)
        Error(reason) -> {
          let signal = LinkFaultSignal(id, Transient, reason)
          process.send(faults, signal)
          Error(signal)
        }
      }
    },
    recv: fn() {
      // No inactivity deadline: a link with no traffic is not a fault — the pump parks here
      // exactly as it does on the tcp leaf, and teardown (C8) kills it.
      case ffi_recv(pid, 3_600_000) {
        FfiFrame(frame) -> Ok(Some(frame))
        FfiEos -> Ok(None)
        FfiError(reason) -> {
          let signal = LinkFaultSignal(id, Transient, reason)
          process.send(faults, signal)
          Error(signal)
        }
      }
    },
    close: fn() { ffi_close(pid) },
    faults: faults,
  )
}

/// Identity from the dialled address. The nonce is the carrier fact (host:port) — meaningless
/// as logical identity (D-6), and the GLP-visible `LinkId` a program passes is what the
/// registry keys on regardless.
fn address_id(scheme: LinkScheme, addr: LinkAddress) -> LinkId {
  LinkId(
    scheme: scheme,
    endpoint: addr,
    nonce: NonceStr(
      addr.host
      <> ":"
      <> case addr.port {
        Some(p) -> int.to_string(p)
        None -> "-"
      },
    ),
  )
}

// ── FFI ──────────────────────────────────────────────────────────────────────

/// What one `recv` produced (the Erlang side returns a tagged value, not an exception).
pub type FfiRecv {
  FfiFrame(frame: BitArray)
  FfiEos
  FfiError(reason: String)
}

@external(erlang, "glp_link_quic_ffi", "open")
fn ffi_open(
  exe: String,
  args: List(String),
  timeout_ms: Int,
) -> Result(Pid, String)

@external(erlang, "glp_link_quic_ffi", "send")
fn ffi_send(pid: Pid, frame: BitArray) -> Result(Nil, String)

@external(erlang, "glp_link_quic_ffi", "recv")
fn ffi_recv(pid: Pid, timeout_ms: Int) -> FfiRecv

@external(erlang, "glp_link_quic_ffi", "close")
fn ffi_close(pid: Pid) -> Nil
