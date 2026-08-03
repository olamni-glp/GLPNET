//// glp/fe/client — the thin Gleam FE REPL loop over the split protocol
//// (feature 064, T027; contracts/febe-split.md "Process roles").
////
//// FE = the existing REPL command surface (glp/repl/commands.parse — load /
//// goals / :trace / :limit / :quit, plus the C# client's :status/:snapshot)
//// speaking glp/split/wire to a BE address. NO engine, no compiler: `load`
//// reads the file's bytes and ships SOURCE TEXT (FR-001); goals ship as text;
//// results render from the 038 envelope through the SAME glp/repl/results path
//// as the single-process REPL, so the T028 equality bar is byte-meaningful.
////
////   gleam run -m glp/fe/client -- --connect 127.0.0.1:7461
////
//// Reference: csharp/glp_repl_client/Program.cs (normative for the exit
//// taxonomy: 64 usage · 69 transport failure · 70 protocol error · 0 quit/EOF).
//// The connect dial rides the Gleam TCP leaf, so the FR-012 norms (D-9
//// dial-retry while the BE is not yet up, {exit_on_close, false}) apply.
////
//// Deviations (recorded; the C# wire is normative and carries no session
//// tunables): `:trace` and `:limit` are accepted with the reference
//// acknowledgment lines but are LOCAL state only — no split-protocol kind
//// exists to carry them, so goals run under the BE's default budget and no
//// trace lines cross the wire (the C# thin client ships neither). `:bytecode`
//// and `:boot` need engine-side state the protocol does not expose — refused
//// loudly, never silently swallowed.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/charlist.{type Charlist}
import gleam/int
import gleam/io
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import glp/codec/result_envelope
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/tcp
import glp/repl/commands
import glp/repl/results
import glp/split/wire

/// A connected FE session: the framed endpoint, the client-monotonic request
/// id (echo-checked, wire rule 2), the outbound FrameCodec message id, and the
/// local-only :trace/:limit state (see the module-doc deviation note).
pub opaque type Client {
  Client(
    ep: Endpoint,
    next_request_id: Int,
    next_message_id: Int,
    trace: Bool,
    limit: Int,
  )
}

/// One executed line's disposition (the loop driver's control flow).
pub type Step {
  /// Keep reading lines.
  Continue
  /// `:quit` — exit 0.
  Quit
  /// Transport/protocol failure — exit with the C# client's code (69/70).
  Fatal(code: Int)
}

// ---------------------------------------------------------------------------
// entrypoint
// ---------------------------------------------------------------------------

pub fn main() -> Nil {
  let args = list.map(plain_args(), charlist.to_string)
  case parse_connect(args) {
    Error(usage) -> {
      io.println_error("glp_fe: " <> usage)
      io.println_error("usage: gleam run -m glp/fe/client -- --connect <host:port>")
      halt(64)
    }
    Ok(#(host, port)) ->
      case connect(host, port) {
        Error(reason) -> {
          io.println_error("!! transport failure: " <> reason)
          halt(69)
        }
        Ok(client) -> {
          io.println(
            "Connected to engine at " <> host <> ":" <> int.to_string(port),
          )
          io.println(
            "Input: load <file.glp> to load, goal. to execute; :status, :quit",
          )
          io.println("")
          halt(loop(client))
        }
      }
  }
}

fn parse_connect(args: List(String)) -> Result(#(String, Int), String) {
  case args {
    ["--connect", hostport] ->
      case string.split(hostport, ":") {
        [host, port_str] ->
          case int.parse(port_str) {
            Ok(port) if port >= 1 && port <= 65_535 -> Ok(#(host, port))
            _ ->
              Error("--connect expects <host:port>, got '" <> hostport <> "'")
          }
        _ -> Error("--connect expects <host:port>, got '" <> hostport <> "'")
      }
    [] -> Error("--connect <host:port> is required")
    [other, ..] -> Error("unknown argument '" <> other <> "'")
  }
}

fn loop(client: Client) -> Int {
  io.print("GLP> ")
  case read_line() {
    // EOF → non-interactive exit 0 (scripted stdin, the REPL convention).
    Error(_) -> 0
    Ok(line) -> {
      let #(client, output, step) = execute_line(client, line)
      list.each(output, io.println)
      case step {
        Continue -> loop(client)
        Quit -> 0
        Fatal(code) -> code
      }
    }
  }
}

// ---------------------------------------------------------------------------
// connect / round trip
// ---------------------------------------------------------------------------

/// Dial the BE (the tcp leaf retries while the listener is not yet up — D-9 /
/// FR-012 role-order independence).
pub fn connect(host: String, port: Int) -> Result(Client, String) {
  let addr = link_address.endpoint(host, port)
  let leaf = tcp.new()
  case leaf.connect(link_scheme.tcp(), addr, link_options.default()) {
    Error(fault) -> Error(fault.reason)
    Ok(ep) ->
      Ok(Client(ep: ep, next_request_id: 1, next_message_id: 1, trace: False, limit: 1_000_000))
  }
}

/// Close the FE's end of the wire (the BE's engine survives — US1 AS-3).
pub fn close(client: Client) -> Nil {
  let ep = client.ep
  ep.close()
}

/// Test/supervisor seam: ship a SHUTDOWN request and return the ACK body.
pub fn shutdown(client: Client) -> Result(#(Client, String), String) {
  case round_trip(client, wire.request_empty(client.next_request_id, wire.Shutdown)) {
    Error(#(_code, reason)) -> Error(reason)
    Ok(#(client, response)) -> Ok(#(client, text_of(response.body)))
  }
}

/// One request → its one terminal response (wire rule 2: the echoed request id
/// is verified). `Error(#(exit_code, reason))` distinguishes transport failure
/// (69) from a protocol violation (70) — C# ClientTransportException vs
/// SplitProtocolException.
fn round_trip(
  client: Client,
  request: wire.RequestFrame,
) -> Result(#(Client, wire.ResponseFrame), #(Int, String)) {
  let client =
    Client(
      ..client,
      next_request_id: client.next_request_id + 1,
      next_message_id: client.next_message_id + 1,
    )
  let ep = client.ep
  case wire.encode_request_frame(request, client.next_message_id) {
    Error(wire.SplitError(msg)) -> Error(#(70, msg))
    Ok(frame) ->
      case ep.send(frame) {
        Error(fault) -> Error(#(69, fault.reason))
        Ok(Nil) ->
          case ep.recv() {
            Error(fault) -> Error(#(69, fault.reason))
            Ok(None) -> Error(#(69, "connection closed by the engine"))
            Ok(Some(bytes)) ->
              case wire.decode_response_frame(bytes) {
                Error(wire.SplitError(msg)) -> Error(#(70, msg))
                Ok(response) ->
                  // Wire rule 2: the response echoes the request's id —
                  // except a pre-decode PROTOCOL_ERROR, which carries id 0
                  // (the BE could not read the id).
                  case
                    response.request_id == request.request_id
                    || response.request_id == 0
                  {
                    True -> Ok(#(client, response))
                    False ->
                      Error(#(
                        70,
                        "response id "
                          <> int.to_string(response.request_id)
                          <> " does not echo request id "
                          <> int.to_string(request.request_id),
                      ))
                  }
              }
          }
      }
  }
}

// ---------------------------------------------------------------------------
// line execution — the REPL command surface over the wire
// ---------------------------------------------------------------------------

/// Execute one input line against the BE. Mirrors the single-process
/// `commands.execute` shape (#(state, output lines, disposition)) so the T028
/// two-process test drives it without a live stdin.
pub fn execute_line(client: Client, line: String) -> #(Client, List(String), Step) {
  // The C# thin client's extra surface, pre-empting the goal fall-through.
  case string.trim(line) {
    ":status" -> status(client)
    ":snapshot" -> snapshot(client)
    _ -> execute_command(client, commands.parse(line))
  }
}

fn execute_command(
  client: Client,
  command: commands.Command,
) -> #(Client, List(String), Step) {
  case command {
    commands.Blank -> #(client, [], Continue)
    commands.Quit -> #(client, ["Goodbye!"], Quit)
    commands.ToggleTrace -> {
      let trace = !client.trace
      let msg = case trace {
        True -> "Trace enabled"
        False -> "Trace disabled"
      }
      // LOCAL only — no wire kind carries the flag (module-doc deviation).
      #(Client(..client, trace: trace), [msg], Continue)
    }
    commands.SetLimit(n) -> #(
      Client(..client, limit: n),
      ["Goal reduction limit set to " <> int.to_string(n)],
      Continue,
    )
    commands.LimitUsage(msg) -> #(client, [msg], Continue)
    commands.Bytecode -> #(
      client,
      ["!! :bytecode is not available over the split protocol (engine state is BE-side)"],
      Continue,
    )
    commands.Boot(_) | commands.BootUsage -> #(
      client,
      ["!! :boot is not available over the split protocol (engine state is BE-side)"],
      Continue,
    )
    commands.Load(path) -> load(client, path)
    commands.Goal(text) -> goal(client, text)
  }
}

/// `load <path>`: read the file LOCALLY, ship SOURCE TEXT (FR-001). Wording
/// matches the single-process REPL (`commands.execute` Load) line for line —
/// the BE's failure envelope carries the same `string.inspect(staged)` text.
fn load(client: Client, path: String) -> #(Client, List(String), Step) {
  case read_source(path) {
    Error(_) -> #(
      client,
      ["Error loading " <> path <> ": File not found"],
      Continue,
    )
    Ok(source) ->
      case
        round_trip(
          client,
          wire.request_text(client.next_request_id, wire.LoadSource, source),
        )
      {
        Error(fail) -> transport_fail(client, fail)
        Ok(#(client, response)) ->
          case response.kind {
            wire.Ack -> #(client, ["✓ Loaded: " <> path], Continue)
            wire.ResultKind ->
              case result_envelope.decode(response.body) {
                Ok(env) -> #(
                  client,
                  [
                    "Error loading "
                    <> path
                    <> ": "
                    <> option.unwrap(env.error, "load failed"),
                  ],
                  Continue,
                )
                Error(e) -> #(
                  client,
                  ["!! protocol error: bad RESULT envelope: " <> string.inspect(e)],
                  Fatal(70),
                )
              }
            wire.EngineBusy -> #(
              client,
              ["Engine busy (restoring) — try again shortly"],
              Continue,
            )
            _ -> #(
              client,
              ["!! protocol error: " <> text_of(response.body)],
              Continue,
            )
          }
      }
  }
}

/// `<goal>.`: ship goal text; render the RESULT envelope exactly as the
/// single-process REPL renders a finished run — captured `_output/1` lines
/// first (R3 blob), then the bindings/status block, then a blank line.
fn goal(client: Client, text: String) -> #(Client, List(String), Step) {
  case
    round_trip(
      client,
      wire.request_text(client.next_request_id, wire.RunGoal, text),
    )
  {
    Error(fail) -> transport_fail(client, fail)
    Ok(#(client, response)) ->
      case response.kind {
        wire.ResultKind ->
          case result_envelope.decode(response.body) {
            Ok(env) -> #(
              client,
              list.flatten([
                captured_lines(env.captured),
                results.render_outcome(env),
                [""],
              ]),
              Continue,
            )
            Error(e) -> #(
              client,
              ["!! protocol error: bad RESULT envelope: " <> string.inspect(e)],
              Fatal(70),
            )
          }
        wire.EngineBusy -> #(
          client,
          ["Engine busy (restoring) — try again shortly", ""],
          Continue,
        )
        _ -> #(
          client,
          ["!! protocol error: " <> text_of(response.body), ""],
          Continue,
        )
      }
  }
}

fn status(client: Client) -> #(Client, List(String), Step) {
  case
    round_trip(client, wire.request_empty(client.next_request_id, wire.Status))
  {
    Error(fail) -> transport_fail(client, fail)
    Ok(#(client, response)) -> #(client, [text_of(response.body)], Continue)
  }
}

fn snapshot(client: Client) -> #(Client, List(String), Step) {
  case
    round_trip(client, wire.request_empty(client.next_request_id, wire.Snapshot))
  {
    Error(fail) -> transport_fail(client, fail)
    Ok(#(client, response)) ->
      case response.kind {
        wire.Ack -> #(client, [text_of(response.body)], Continue)
        wire.Deferred -> #(
          client,
          [
            "snapshot deferred (engine busy) — it fires at the next quiescence; see :status",
          ],
          Continue,
        )
        wire.EngineBusy -> #(
          client,
          ["Engine busy (restoring) — try again shortly"],
          Continue,
        )
        _ -> #(
          client,
          ["!! protocol error: " <> text_of(response.body)],
          Continue,
        )
      }
  }
}

// ---------------------------------------------------------------------------
// rendering helpers
// ---------------------------------------------------------------------------

/// The envelope's R3 captured-output blob back into per-line strings (the BE
/// joins lines with '\n' and appends a trailing '\n' — drop the final empty
/// segment, the C# RenderEnvelope convention).
fn captured_lines(captured: BitArray) -> List(String) {
  case bit_array.byte_size(captured) {
    0 -> []
    _ ->
      case bit_array.to_string(captured) {
        Error(_) -> ["!! captured output is not valid UTF-8"]
        Ok(text) ->
          case string.split(text, "\n") {
            [] -> []
            parts ->
              case list.reverse(parts) {
                ["", ..rest] -> list.reverse(rest)
                _ -> parts
              }
          }
      }
  }
}

/// FR-007: a transport/protocol failure renders distinctly from any goal
/// failure — "!! transport failure: …" is never confusable with `→ failed`.
fn transport_fail(
  client: Client,
  fail: #(Int, String),
) -> #(Client, List(String), Step) {
  let #(code, reason) = fail
  let prefix = case code {
    70 -> "!! protocol error: "
    _ -> "!! transport failure: "
  }
  #(client, [prefix <> reason], Fatal(code))
}

fn text_of(body: BitArray) -> String {
  case bit_array.to_string(body) {
    Ok(s) -> s
    Error(_) -> "<non-UTF-8 body>"
  }
}

fn read_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) ->
      case bit_array.to_string(bits) {
        Ok(s) -> Ok(s)
        Error(_) -> Error(Nil)
      }
    Error(_) -> Error(Nil)
  }
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

@external(erlang, "glp_repl_ffi", "read_line")
fn read_line() -> Result(String, Nil)
