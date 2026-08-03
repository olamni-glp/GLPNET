//// T039 boot-loader tests (wave-3 US4, gap G9): `boot/0` spawn-directive
//// extraction from the parsed AST (the Gleam parser models `goal@agent`
//// natively — no Dart-style regex/strip), per-agent MadEngine boot with the
//// trailing `_` slot materialized as the net-in reader, and the drive loop to
//// quiescence.

import gleam/list
import gleeunit/should
import glp/compiler/loader
import glp/engine/scheduler
import glp/mad/boot_loader
import glp/mad/mad_engine
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser

const play_source = "-mode(system).
procedure agent_init(_?, _?).
agent_init(A, _) :- '_output'(A?).
boot :- agent_init(alice, _)@alice, agent_init(bob, _)@bob."

fn parse(source: String) -> ast.SourceModule {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  module
}

// Directive extraction: two spawns, agent ids and goal shapes preserved.
pub fn extract_directives_test() {
  let assert Ok([d1, d2]) =
    boot_loader.extract_directives(parse(play_source))
  d1.agent_id |> should.equal("alice")
  d1.functor |> should.equal("agent_init")
  list.length(d1.args) |> should.equal(2)
  d2.agent_id |> should.equal("bob")
}

// A missing boot/0 is a loud error, not an empty play.
pub fn missing_boot_clause_errors_test() {
  let assert Error(_) =
    boot_loader.extract_directives(parse(
      "-mode(system).
procedure lone(_?).
lone(x).",
    ))
}

// End-to-end: compile the play, boot one MadEngine per directive, drive to
// quiescence; each agent ran ITS OWN init (per-agent captured output).
pub fn boot_and_drive_play_test() {
  let assert Ok(prog) = loader.compile_prelude(play_source)
  let assert Ok(directives) =
    boot_loader.extract_directives(parse(play_source))
  let assert Ok(play) = boot_loader.boot_agents(prog, directives)
  let assert Ok(play) = boot_loader.drive(play, 5000, 10_000)
  let assert [#(_, alice), #(_, bob)] = boot_loader.agents(play)
  scheduler.captured_output(mad_engine.engine(alice))
  |> should.equal(["alice"])
  scheduler.captured_output(mad_engine.engine(bob))
  |> should.equal(["bob"])
}

// An unknown spawn goal is a loud boot error (never a silent no-op agent).
pub fn unknown_spawn_goal_errors_test() {
  let assert Ok(prog) = loader.compile_prelude(play_source)
  let assert Error(_) =
    boot_loader.boot_agents(prog, [
      boot_loader.SpawnDirective("carol", "no_such_goal", []),
    ])
}
