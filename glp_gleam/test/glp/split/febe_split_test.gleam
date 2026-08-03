//// Two-process FE↔BE split test (feature 064, T028; contracts/febe-split.md
//// acceptance 1).
////
//// Standard REPL scenarios driven through the split — the thin FE
//// (glp/fe/client) speaking glp/split/wire over REAL loopback TCP sockets to
//// the BE (glp/be/server) — must produce output equal to the single-process
//// REPL (glp/repl/commands over one in-process engine) for a representative
//// program set: loads (success + missing file), arithmetic, conjunction
//// producer/consumer (succeeds + suspended-latch), missing predicate,
//// captured `_output/1` program output, and the :trace/:limit echoes.
////
//// Deviation (recorded, sanctioned by the task): the BE runs as a spawned
//// BEAM process inside this suite — a two-actor test over the real TCP socket
//// path in one OS process — not as a separate OS process. The wire, framing,
//// and role separation are identical; only the OS process boundary is
//// simulated. A true two-OS-process smoke is the T029/T031 shell layer.
////
//// Convention (repl_test.gleam): semantics are pinned deterministically
//// in-process; distinct ports per test avoid intra-run collisions.

import gleam/erlang/process
import gleam/list
import gleam/option.{Some}
import gleam/string
import gleeunit/should
import glp/be/server
import glp/engine
import glp/fe/client.{Continue, Quit}
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/tcp
import glp/repl/commands.{Session}
import glp/split/wire

// ── harness ──────────────────────────────────────────────────────────────────

/// Spawn a BE serving `port`; the returned subject delivers its exit code.
fn start_be(port: Int) -> process.Subject(Int) {
  let done = process.new_subject()
  process.spawn(fn() { process.send(done, server.serve(port)) })
  done
}

/// Drive the script through the single-process REPL (the reference).
fn run_single_process(script: List(String)) -> List(String) {
  let session =
    Session(engine: engine.new(), trace: False, limit: 1_000_000)
  let #(_session, output) =
    list.fold(script, #(session, []), fn(acc, line) {
      let #(session, output) = acc
      let #(session, lines, _quit) =
        commands.execute(session, commands.parse(line))
      #(session, list.append(output, lines))
    })
  output
}

/// Drive the same script through FE↔BE over the split wire.
fn run_split(script: List(String), port: Int) -> List(String) {
  let assert Ok(fe) = client.connect("127.0.0.1", port)
  let #(fe, output) =
    list.fold(script, #(fe, []), fn(acc, line) {
      let #(fe, output) = acc
      let #(fe, lines, step) = client.execute_line(fe, line)
      // Every scripted line must leave the session serving (no transport or
      // protocol fatality mid-corpus).
      case step {
        Continue | Quit -> Nil
        client.Fatal(_) ->
          panic as { "split FE fatality on line: " <> line }
      }
      #(fe, list.append(output, lines))
    })
  let _ = client.shutdown(fe)
  client.close(fe)
  output
}

/// The BE, once shut down, must exit with the C# taxonomy's code 0.
fn assert_clean_exit(done: process.Subject(Int)) -> Nil {
  let assert Ok(code) = process.receive(done, 10_000)
  code |> should.equal(0)
}

// ── acceptance 1: standard scenarios, split == single-process ────────────────

const struct_demo = "../programs/tests/typed/struct_demo.glp"

const output_fixture = "test/glp_split_output_fixture.glp"

pub fn standard_scenarios_split_equals_single_process_test() {
  let script = [
    // loads: success + missing file
    "load " <> struct_demo,
    "load " <> output_fixture,
    "load does/not/exist.glp",
    // prelude arithmetic
    "X := 2+3.",
    // conjunction producer→consumer (succeeds, shared-var bindings)
    "build_person(P), get_age(P?, A).",
    // consumer-first conjunction (suspended-latch parity)
    "get_age(P?, A), build_person(P).",
    // missing predicate (loud reference failure)
    "no_such_pred(1).",
    // captured `_output/1` program output (the R3 blob across the wire)
    "emit(hello).",
    // command-surface echoes (FE-local by design — same visible lines)
    ":trace",
    ":trace",
    ":limit 42",
    "",
  ]
  let done = start_be(34_771)
  let split_output = run_split(script, 34_771)
  let single_output = run_single_process(script)
  split_output |> should.equal(single_output)
  assert_clean_exit(done)
}

// ── BE surface: STATUS / PING-adjacent + loud refusals ───────────────────────

pub fn status_reflects_be_state_test() {
  let done = start_be(34_772)
  let assert Ok(fe) = client.connect("127.0.0.1", 34_772)

  let #(fe, lines, step) = client.execute_line(fe, ":status")
  step |> should.equal(Continue)
  let assert [status_line] = lines
  string.contains(status_line, "state=serving") |> should.be_true
  string.contains(status_line, "loaded_programs=0") |> should.be_true

  // A successful load is visible in the next STATUS (BE-side session state).
  let #(fe, load_lines, _) =
    client.execute_line(fe, "load " <> output_fixture)
  load_lines |> should.equal(["✓ Loaded: " <> output_fixture])
  let #(fe, lines2, _) = client.execute_line(fe, ":status")
  let assert [status_line2] = lines2
  string.contains(status_line2, "loaded_programs=1") |> should.be_true

  // SNAPSHOT has no store on this BE — refused loudly, session keeps serving.
  let #(fe, snap_lines, snap_step) = client.execute_line(fe, ":snapshot")
  snap_step |> should.equal(Continue)
  let assert [snap_line] = snap_lines
  string.starts_with(snap_line, "!! protocol error:") |> should.be_true

  let assert Ok(#(fe, ack)) = client.shutdown(fe)
  string.contains(ack, "shutting_down") |> should.be_true
  client.close(fe)
  assert_clean_exit(done)
}

// ── US1 AS-3: the engine (and its loads) survive a client disconnect ─────────

pub fn engine_survives_client_disconnect_test() {
  let done = start_be(34_773)

  // Client 1 loads the fixture, then vanishes without SHUTDOWN.
  let assert Ok(fe1) = client.connect("127.0.0.1", 34_773)
  let #(fe1, load_lines, _) =
    client.execute_line(fe1, "load " <> output_fixture)
  load_lines |> should.equal(["✓ Loaded: " <> output_fixture])
  client.close(fe1)

  // Client 2 finds the load still in place and runs its predicate un-reloaded.
  let assert Ok(fe2) = client.connect("127.0.0.1", 34_773)
  let #(fe2, lines, _) = client.execute_line(fe2, ":status")
  let assert [status_line] = lines
  string.contains(status_line, "loaded_programs=1") |> should.be_true
  let #(fe2, goal_lines, _) = client.execute_line(fe2, "emit(hello).")
  goal_lines |> should.equal(["hello", "→ succeeds", ""])

  let _ = client.shutdown(fe2)
  client.close(fe2)
  assert_clean_exit(done)
}

// ── FR-006: a load error is a structured RESULT, engine keeps serving ────────

pub fn load_error_is_structured_result_test() {
  let done = start_be(34_774)
  let assert Ok(fe) = client.connect("127.0.0.1", 34_774)

  // A load error must come back as a structured Failed RESULT (FR-006) and
  // leave the BE serving — exercised through a source that fails the pipeline.
  let #(fe, err_lines, err_step) = client.execute_line(fe, "load " <> bad_source_path())
  err_step |> should.equal(Continue)
  case err_lines {
    [line] -> string.starts_with(line, "Error loading ") |> should.be_true
    _ -> panic as "expected exactly one load-error line"
  }

  // …and the session still serves goals afterwards.
  let #(fe, goal_lines, _) = client.execute_line(fe, "X := 1+1.")
  goal_lines |> should.equal(["X = 2", "→ succeeds", ""])

  let _ = client.shutdown(fe)
  client.close(fe)
  assert_clean_exit(done)
}

/// A path that exists but fails the load pipeline: this very test module (a
/// .gleam file is not GLP source — the lexer/parser rejects it loudly).
fn bad_source_path() -> String {
  "test/glp/split/febe_split_test.gleam"
}

// ── raw wire: PING ACK + loud IL/snapshot refusals (no silent fallback) ──────

pub fn raw_wire_ping_and_il_refusal_test() {
  let done = start_be(34_775)
  let leaf = tcp.new()
  let assert Ok(ep) =
    leaf.connect(
      link_scheme.tcp(),
      link_address.endpoint("127.0.0.1", 34_775),
      link_options.default(),
    )

  // PING → ACK "pong" (wire rule 7), request id echoed.
  let assert Ok(ping) =
    wire.encode_request_frame(wire.request_empty(11, wire.Ping), 1)
  let assert Ok(Nil) = ep.send(ping)
  let assert Ok(Some(reply1)) = ep.recv()
  let assert Ok(wire.ResponseFrame(11, wire.Ack, body1)) =
    wire.decode_response_frame(reply1)
  body1 |> should.equal(<<"pong":utf8>>)

  // LOAD_IL → loud PROTOCOL_ERROR (T026 text-kind scope), engine keeps serving.
  let assert Ok(load_il) =
    wire.encode_request_frame(wire.RequestFrame(12, wire.LoadIl, <<0xAA>>), 2)
  let assert Ok(Nil) = ep.send(load_il)
  let assert Ok(Some(reply2)) = ep.recv()
  let assert Ok(wire.ResponseFrame(12, wire.ProtocolError, _)) =
    wire.decode_response_frame(reply2)

  // A malformed (non-FrameCodec) blob → PROTOCOL_ERROR with id 0 (undecodable).
  let assert Ok(Nil) = ep.send(<<0xDE, 0xAD>>)
  let assert Ok(Some(reply3)) = ep.recv()
  let assert Ok(wire.ResponseFrame(0, wire.ProtocolError, _)) =
    wire.decode_response_frame(reply3)

  // …and the session still answers a healthy request afterwards (FR-006).
  let assert Ok(shutdown) =
    wire.encode_request_frame(wire.request_empty(13, wire.Shutdown), 3)
  let assert Ok(Nil) = ep.send(shutdown)
  let assert Ok(Some(reply4)) = ep.recv()
  let assert Ok(wire.ResponseFrame(13, wire.Ack, _)) =
    wire.decode_response_frame(reply4)
  ep.close()
  assert_clean_exit(done)
}
