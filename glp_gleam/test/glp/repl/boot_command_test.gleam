//// `:boot` scenario test (064 T033, US5): the REPL command runs a
//// multi-isolate play end-to-end through the wave-3 multiagent boot loader
//// (gap G9) — read the play file, extract the `boot/0` spawn directives,
//// boot one MadEngine per directive over the play merged with the prelude,
//// drive to quiescence, and render each agent's output as `[agentId] line`
//// (the Dart `_runBoot` / `onUIOutput` shape, glp_repl.dart:486).
////
//// The play is a minimal self-contained two-isolate fixture
//// (boot_play_two_agents.glp): alice outputs her identity; bob computes via
//// the prelude's `:=`/2 — proving the per-agent boot path AND the
//// prelude-merge composition. (The full Dart plays under
//// programs/bonds_v2/mad_boot/ are boot files over per-isolate PROJECT
//// loading, which the single-source Gleam boot loader does not model.)

import gleam/list
import gleam/string
import gleeunit/should
import glp/engine
import glp/repl/commands.{Boot, Goal, Session}

// Test cwd is glp_gleam/ (the corpus/prelude convention: engine.new reads
// ../programs/self.glp), so the fixture resolves test-dir-relative from there.
const play_path = "test/glp/repl/boot_play_two_agents.glp"

fn fresh_session() -> commands.Session {
  Session(engine: engine.new(), trace: False, limit: 1_000_000)
}

// The documented outcome: banner + agent list, each agent's own output under
// its `[agentId]` prefix (directive order), quiescence footer — and the
// session comes back READ-ONLY-untouched and usable.
pub fn boot_runs_two_isolate_play_to_quiescence_test() {
  let session = fresh_session()
  let #(session2, output, quit) =
    commands.execute(session, commands.parse(":boot " <> play_path))
  quit |> should.be_false

  let assert [banner, agents, ..rest] = output
  banner |> should.equal("Booting 2 agents from " <> play_path)
  agents |> should.equal("  agents: alice, bob")
  // Per-agent captured output: alice announced herself; bob computed 20+3
  // through the merged prelude's `:=`/2.
  should.be_true(list.contains(rest, "[alice] alice"))
  should.be_true(list.contains(rest, "[bob] 23"))
  let assert Ok(footer) = list.last(rest)
  footer |> should.equal("✓ Play quiesced")

  // `:boot` is read-only w.r.t. the session (contract invariant 6 analogue).
  session2 |> should.equal(session)
  // The session stays usable afterwards.
  let #(_s, out2, _) = commands.execute(session2, Goal("X := 2+2"))
  should.be_true(list.contains(out2, "X = 4"))
}

// A play file that parses but has no boot/0 clause is the loud parse-shaped
// error (extraction happens before any agent boots), session untouched.
pub fn boot_without_boot_clause_errors_test() {
  let #(_session, output, quit) =
    commands.execute(
      fresh_session(),
      Boot("../programs/tests/typed/struct_demo.glp"),
    )
  quit |> should.be_false
  let assert [line] = output
  should.be_true(string.starts_with(line, "Error parsing boot file:"))
  should.be_true(string.contains(line, "boot/0"))
}
