//// glp/be/server — the Gleam BE process entrypoint (feature 064, T026;
//// contracts/febe-split.md "Process roles").
////
//// BE = engine + scheduler behind the split protocol (glp/split/wire — the
//// SAME frames the C# split uses) served over the Gleam TCP transport on a
//// configured loopback port. Run it as its own OS process:
////
////   gleam run -m glp/be/server -- --listen 127.0.0.1:7461
////
//// Reference: csharp/glp_engine_host (Program.cs / EngineServer.cs /
//// RequestDispatcher.cs — normative for the wire semantics + exit taxonomy):
////   64 usage error · 66 prelude missing · 65 server (bind) error · 0 SHUTDOWN.
////
//// Serve shape = the C# SingleClient default: one client at a time, requests
//// answered strictly in order (wire rule 2); a client disconnect leaves the
//// engine — and everything loaded into it — alive for the next client (US1
//// AS-3). The listener is the T010 multi_accept leaf, so the port stays bound
//// across clients and every socket carries the FR-012 norms ({exit_on_close,
//// false}, loopback-only bind, D-9 half-close parity) from glp_link_tcp_ffi.
//// Divergence from C# (recorded): a SECOND concurrent client is not refused
//// with a loud PROTOCOL_ERROR frame — it waits in the accept buffer and is
//// served after the current client leaves (the single-accept seam cannot
//// express the mid-serve refusal; C# owns a raw TcpListener for exactly this).
////
//// Scope (T026): the TEXT kinds. SNAPSHOT (no snapshot store on this BE) and
//// the IL kinds are refused loudly with PROTOCOL_ERROR — the engine keeps
//// serving (FR-006); never a silent fallback.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/charlist.{type Charlist}
import gleam/int
import gleam/io
import gleam/list
import gleam/option.{Some}
import gleam/string
import glp/codec/result_envelope.{type ResultEnvelope, ResultEnvelope}
import glp/engine.{type Engine}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/link/transports/multi_accept
import glp/link/transports/tcp
import glp/split/wire

/// Reduction budget per RUN_GOAL (the engine default; there is no :limit wire
/// kind — the split protocol carries no session-tunable, C# parity).
const goal_fuel = 1_000_000

/// Per-`accept` wait slice while parked with no client (the loop re-arms).
const accept_slice_ms = 60_000

// ---------------------------------------------------------------------------
// entrypoint
// ---------------------------------------------------------------------------

pub fn main() -> Nil {
  let args = list.map(plain_args(), charlist.to_string)
  case parse_listen(args) {
    Error(usage) -> {
      io.println_error("glp_be: " <> usage)
      io.println_error("usage: gleam run -m glp/be/server -- --listen <host:port>")
      halt(64)
    }
    Ok(#(host, port)) ->
      case host == "127.0.0.1" || host == "localhost" {
        False -> {
          // The Gleam TCP leaf binds IPv4 loopback ONLY (glp_link_tcp_ffi
          // tcp_listen pins {ip, {127,0,0,1}}) — refuse a non-loopback ask
          // loudly instead of silently serving loopback (FR-012 trust boundary).
          io.println_error(
            "glp_be: --listen host must be loopback (127.0.0.1/localhost); "
            <> "the Gleam TCP leaf binds IPv4 loopback only",
          )
          halt(64)
        }
        True ->
          // C# exit 66: prelude unlocatable. The Gleam engine reads
          // ../programs/self.glp relative to the CWD (the glp_gleam package
          // root convention) — check it BEFORE engine.new() panics.
          case read_file("../programs/self.glp") {
            Error(_) -> {
              io.println_error(
                "glp_be: cannot read ../programs/self.glp — run from the "
                <> "glp_gleam/ package root of a glpnet checkout",
              )
              halt(66)
            }
            Ok(_) -> {
              io.println("glp_be: serving on " <> host <> ":" <> int.to_string(port) <> " (one client at a time)")
              halt(serve(port))
            }
          }
      }
  }
}

/// `--listen <host:port>` (the only argument, C# Program.cs shape).
fn parse_listen(args: List(String)) -> Result(#(String, Int), String) {
  case args {
    ["--listen", hostport] ->
      case string.split(hostport, ":") {
        [host, port_str] ->
          case int.parse(port_str) {
            Ok(port) if port >= 1 && port <= 65_535 -> Ok(#(host, port))
            _ -> Error("--listen expects <host:port>, got '" <> hostport <> "'")
          }
        _ -> Error("--listen expects <host:port>, got '" <> hostport <> "'")
      }
    [] -> Error("--listen <host:port> is required")
    [other, ..] -> Error("unknown argument '" <> other <> "'")
  }
}

// ---------------------------------------------------------------------------
// serve loop
// ---------------------------------------------------------------------------

/// BE state threaded through the serve loop: the live engine (loads survive
/// client disconnects — US1 AS-3), the load counter (C# `_loadCounter`), the
/// outbound FrameCodec message-id counter, and the shutdown latch (wire rule 6).
type Be {
  Be(engine: Engine, port: Int, loads: Int, message_id: Int, shutdown: Bool)
}

/// Bind `127.0.0.1:port` and serve split-protocol clients one at a time until
/// SHUTDOWN. Returns the process exit code (0 = graceful SHUTDOWN, 65 = bind
/// failure — the C# EngineServerException path). Runnable in-process (the T028
/// two-actor test spawns it) or via `main` as its own OS process.
pub fn serve(port: Int) -> Int {
  let addr = link_address.endpoint("127.0.0.1", port)
  case multi_accept.listen(link_scheme.tcp(), addr, link_options.default()) {
    Error(fault) -> {
      // Busy port ⇒ loud bind-time refusal, never a silent queue (C# parity).
      io.println_error(
        "glp_be: cannot listen on 127.0.0.1:"
        <> int.to_string(port)
        <> ": "
        <> fault.reason,
      )
      65
    }
    Ok(listener) -> {
      // Composition root (the repl.run pattern): transports armed for
      // GLP-level links the ENGINE may hold; the split wire itself rides the
      // listener above.
      let be =
        Be(
          engine: engine.new()
            |> engine.with_transports([loopback.new(), tcp.new()]),
          port: port,
          loads: 0,
          message_id: 0,
          shutdown: False,
        )
      let be = accept_loop(be, listener)
      multi_accept.stop(listener)
      case be.shutdown {
        True -> {
          io.println("glp_be: shutdown complete")
          0
        }
        False -> 65
      }
    }
  }
}

fn accept_loop(be: Be, listener: multi_accept.Listener) -> Be {
  case be.shutdown {
    True -> be
    False ->
      case multi_accept.accept(listener, accept_slice_ms) {
        // No client within the slice — park again (the listener stays bound).
        Error(_) -> accept_loop(be, listener)
        Ok(ep) -> {
          let be = serve_client(be, ep)
          ep.close()
          accept_loop(be, listener)
        }
      }
  }
}

/// Serve one client to disconnect or SHUTDOWN: recv one frame, dispatch, send
/// the one terminal response (wire rule 2), repeat. A malformed frame gets a
/// structured PROTOCOL_ERROR (request id 0 — undecodable) and the engine keeps
/// serving (FR-006, wire rule 3). A vanished client ends only this session.
fn serve_client(be: Be, ep: Endpoint) -> Be {
  case ep.recv() {
    // Clean disconnect / transport fault — engine survives (US1 AS-3).
    Ok(option.None) -> be
    Error(_) -> be
    Ok(Some(frame)) -> {
      let #(be, response) = case wire.decode_request_frame(frame) {
        Error(wire.SplitError(msg)) -> #(
          be,
          wire.response_text(0, wire.ProtocolError, msg),
        )
        Ok(request) -> dispatch(be, request)
      }
      let be = Be(..be, message_id: be.message_id + 1)
      case wire.encode_response_frame(response, be.message_id) {
        // An unencodable response is an internal invariant break — drop the
        // client rather than mis-frame (FrameCodec loud-fail).
        Error(_) -> be
        Ok(bytes) ->
          case ep.send(bytes) {
            // Client vanished before the response — crash boundary is the
            // client's (wire rule 8).
            Error(_) -> be
            Ok(Nil) ->
              case be.shutdown {
                True -> be
                False -> serve_client(be, ep)
              }
          }
      }
    }
  }
}

// ---------------------------------------------------------------------------
// dispatch — one request in, one terminal response out (C# RequestDispatcher)
// ---------------------------------------------------------------------------

fn dispatch(be: Be, request: wire.RequestFrame) -> #(Be, wire.ResponseFrame) {
  let id = request.request_id
  case request.kind {
    wire.LoadSource -> load_source(be, id, request.body)
    wire.RunGoal -> run_goal(be, id, request.body)
    wire.Status -> #(be, wire.response_text(id, wire.Ack, status_body(be)))
    wire.Ping -> #(be, wire.response_text(id, wire.Ack, "pong"))
    wire.Shutdown -> #(
      Be(..be, shutdown: True),
      wire.response_text(
        id,
        wire.Ack,
        "shutting_down final_snapshot=skipped(no snapshot store on the Gleam BE)",
      ),
    )
    // No snapshot machinery on this BE (T026 scope) — refuse loudly, keep
    // serving (FR-006); never a silent no-op ACK.
    wire.Snapshot -> #(
      be,
      wire.response_text(
        id,
        wire.ProtocolError,
        "engine error: SNAPSHOT is not served by the Gleam BE (no snapshot store; 064 T026 text-kind scope)",
      ),
    )
    // IL kinds: decoded (single wire table) but not served here — loud typed
    // refusal, never a silent fallback to the text path.
    wire.LoadIl | wire.RunGoalIl -> #(
      be,
      wire.response_text(
        id,
        wire.ProtocolError,
        "engine error: IL request kinds are not served by the Gleam BE (text kinds only; 064 T026 scope)",
      ),
    )
  }
}

/// LOAD_SOURCE → engine full pipeline; ACK "loaded" on success, RESULT(Failed
/// envelope) on compile/type errors — the engine stays serving (FR-006). The
/// envelope error is `string.inspect(staged)`, exactly the single-process
/// REPL's load-failure text, so the FE renders identically (T028 parity).
fn load_source(be: Be, id: Int, body: BitArray) -> #(Be, wire.ResponseFrame) {
  // C# `++_loadCounter`: the counter advances even when the load fails.
  let be = Be(..be, loads: be.loads + 1)
  case bit_array.to_string(body) {
    Error(_) -> #(
      be,
      wire.response_text(
        id,
        wire.ProtocolError,
        "engine error: LOAD_SOURCE body is not valid UTF-8",
      ),
    )
    Ok(source) -> {
      let name = "_client_load_" <> int.to_string(be.loads)
      case engine.load(be.engine, name, source) {
        Ok(eng) -> #(
          Be(..be, engine: eng),
          wire.response_text(id, wire.Ack, "loaded"),
        )
        Error(staged) -> #(
          be,
          wire.ResponseFrame(
            id,
            wire.ResultKind,
            result_envelope.encode(failed_envelope(string.inspect(staged))),
          ),
        )
      }
    }
  }
}

/// RUN_GOAL → execute; RESULT = 038 ResultEnvelope with the captured
/// `_output/1` program output riding as the envelope's UTF-8 blob (R3 — the
/// FE holds no engine to capture from). Bindings travel as deep-resolved codec
/// terms (already heap-independent), which the Gleam FE renders through the
/// same glp/repl/results path as the single-process REPL.
fn run_goal(be: Be, id: Int, body: BitArray) -> #(Be, wire.ResponseFrame) {
  case bit_array.to_string(body) {
    Error(_) -> #(
      be,
      wire.response_text(
        id,
        wire.ProtocolError,
        "engine error: RUN_GOAL body is not valid UTF-8",
      ),
    )
    Ok(goal) -> {
      let #(eng, env, output) =
        engine.run_with_limit_capturing(be.engine, goal, goal_fuel)
      let captured = case output {
        [] -> <<>>
        lines -> bit_array.from_string(string.join(lines, "\n") <> "\n")
      }
      let env = ResultEnvelope(..env, captured: captured)
      #(
        Be(..be, engine: eng),
        wire.ResponseFrame(id, wire.ResultKind, result_envelope.encode(env)),
      )
    }
  }
}

/// STATUS ACK body — the C# field shape (state/engine/loaded_programs/
/// pending_snapshot/last_snapshot_seq) with this BE's values; no snapshot
/// store ⇒ the pending/last fields are permanently `none`.
fn status_body(be: Be) -> String {
  "state=serving engine=engine-"
  <> int.to_string(be.port)
  <> " loaded_programs="
  <> int.to_string(list.length(engine.loaded_programs(be.engine)))
  <> " pending_snapshot=none last_snapshot_seq=none"
}

fn failed_envelope(reason: String) -> ResultEnvelope {
  ResultEnvelope(
    status: result_envelope.Failed,
    resolved_bindings: [],
    var_to_writer: [],
    suspended: [],
    captured: <<>>,
    error: Some(reason),
  )
}

// ---------------------------------------------------------------------------
// FFI
// ---------------------------------------------------------------------------

@external(erlang, "init", "get_plain_arguments")
fn plain_args() -> List(Charlist)

@external(erlang, "erlang", "halt")
fn halt(code: Int) -> Nil

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)
