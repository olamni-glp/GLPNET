//// glp/repl/commands — REPL command surface + semantics (feature 050, T032; US2).
////
//// The reference REPL's command handling (Dart `glp_runtime/bin/glp_repl.dart`),
//// for the contract's named set (contracts/gleam-instance-surface.md §"REPL
//// commands"): `load <path>` / bare `<path>.glp`, `<goal>.`, `:trace`, `:limit
//// <n>`, `:quit`. `parse` is pure (the parse surface); `execute` threads a
//// `Session` (the immutable engine + trace flag + reduction limit) and returns the
//// output lines plus a quit flag. Goal results are rendered from the 038
//// `ResultEnvelope` (glp/repl/results) — the ED-1 in-process seam.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/list
import gleam/string
import glp/bytecode/program
import glp/compiler/loader
import glp/engine.{type Engine}
import glp/engine/scheduler
import glp/mad/boot_loader
import glp/mad/mad_engine
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser
import glp/repl/results
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm}

/// A parsed REPL line (the contract's command set; unrecognized input is a Goal,
/// matching the Dart fall-through to `runGoal`).
pub type Command {
  Quit
  ToggleTrace
  SetLimit(Int)
  /// A malformed `:limit` — carries the exact reference message to print.
  LimitUsage(String)
  /// A `.glp` load — path with any `load ` prefix and surrounding quotes stripped.
  Load(String)
  /// A goal to execute (trailing `.` already stripped).
  Goal(String)
  /// `:bytecode` / `:bc` — dump every loaded program's bytecode (wave-3 T019;
  /// Dart glp_repl.dart:203 — no argument, iterates `engine.loadedPrograms`).
  Bytecode
  /// `:boot <playfile.glp> [timeoutSec]` — run a multi-isolate play (Dart
  /// glp_repl.dart:189 `_runBoot` via IsolateManager). On this instance the
  /// isolate machinery is the wave-3 multiagent boot loader (gap G9, US4;
  /// glp/mad/boot_loader): read the play file, extract the `boot/0` spawn
  /// directives from the parsed AST, boot one `MadEngine` per directive over
  /// the play program merged with the prelude (the Dart isolates' self.glp +
  /// play composition), and `drive` synchronously to quiescence. The optional
  /// `timeoutSec` is accepted for surface compatibility but unused: the
  /// synchronous drive terminates at quiescence (or loudly on fuel
  /// exhaustion) — there is no wall-clock to bound (064 T033).
  Boot(String)
  /// A `:boot` with no play path — carries the reference usage line.
  BootUsage
  /// An empty line — a no-op (Dart `continue`).
  Blank
}

/// The mutable REPL state threaded across lines: the immutable engine, the trace
/// toggle, and the current reduction limit (Dart `engine.maxCycles`).
pub type Session {
  Session(engine: Engine, trace: Bool, limit: Int)
}

/// Parse one input line into a `Command`. Mirrors the Dart pre-processing: trim,
/// then strip a single trailing `.` UNLESS the line ends in `.glp` (so `foo(X).`
/// becomes the goal `foo(X)` but a bare `merge.glp` load is untouched).
pub fn parse(line: String) -> Command {
  let trimmed = string.trim(line)
  case trimmed {
    "" -> Blank
    _ -> {
      let dedotted = case
        string.ends_with(trimmed, ".") && !string.ends_with(trimmed, ".glp")
      {
        True -> string.trim(string.drop_end(trimmed, 1))
        False -> trimmed
      }
      classify(dedotted)
    }
  }
}

fn classify(t: String) -> Command {
  case t {
    ":quit" | ":q" -> Quit
    ":trace" | ":t" -> ToggleTrace
    ":bytecode" | ":bc" -> Bytecode
    _ ->
      case string.starts_with(t, ":limit") {
        True -> parse_limit(t)
        False ->
          case string.starts_with(t, ":boot") {
            True -> parse_boot(t)
            False -> classify_load_or_goal(t)
          }
      }
  }
}

fn parse_limit(t: String) -> Command {
  let parts = t |> string.split(" ") |> list.filter(fn(s) { s != "" })
  case parts {
    [":limit", n_str] ->
      case int.parse(n_str) {
        Ok(n) if n > 0 -> SetLimit(n)
        _ -> LimitUsage("Error: limit must be a positive integer")
      }
    _ -> LimitUsage("Usage: :limit <number>")
  }
}

/// `:boot <bootfile.glp> [timeoutSec]` (Dart glp_repl.dart:189-201): the first
/// token after `:boot` is the play path; a trailing numeric token (Dart's
/// wall-clock timeout) is accepted and ignored — the Gleam drive is synchronous
/// to quiescence.
fn parse_boot(t: String) -> Command {
  let parts = t |> string.split(" ") |> list.filter(fn(s) { s != "" })
  case parts {
    [":boot", path, ..] -> Boot(strip_quotes(path))
    _ -> BootUsage
  }
}

fn classify_load_or_goal(t: String) -> Command {
  case string.starts_with(t, "load ") {
    True -> Load(strip_quotes(string.trim(string.drop_start(t, 5))))
    False ->
      case string.ends_with(t, ".glp") {
        True -> Load(strip_quotes(t))
        False -> Goal(t)
      }
  }
}

fn strip_quotes(s: String) -> String {
  let n = string.length(s)
  case n >= 2 {
    False -> s
    True ->
      case
        { string.starts_with(s, "\"") && string.ends_with(s, "\"") }
        || { string.starts_with(s, "'") && string.ends_with(s, "'") }
      {
        True -> string.slice(s, 1, n - 2)
        False -> s
      }
  }
}

/// Execute a command against the session. Returns the updated session, the output
/// lines to print, and whether the REPL should exit (Dart `break`). Goal output
/// carries a trailing blank line (Dart prints `''` after each goal block).
pub fn execute(
  session: Session,
  command: Command,
) -> #(Session, List(String), Bool) {
  case command {
    Blank -> #(session, [], False)
    Quit -> #(session, ["Goodbye!"], True)
    ToggleTrace -> {
      let trace = !session.trace
      let msg = case trace {
        True -> "Trace enabled"
        False -> "Trace disabled"
      }
      #(Session(..session, trace: trace), [msg], False)
    }
    SetLimit(n) -> #(
      Session(..session, limit: n),
      ["Goal reduction limit set to " <> int.to_string(n)],
      False,
    )
    LimitUsage(msg) -> #(session, [msg], False)
    // `:bytecode` is READ-ONLY (contract invariant 6): it renders from the
    // engine value and returns the session unchanged — no heap, no program,
    // no session mutation.
    Bytecode -> #(session, render_bytecode(session.engine), False)
    // `:boot` is READ-ONLY w.r.t. the session (like `:bytecode`): the play runs
    // in its own per-agent MadEngines over its own program; the REPL engine,
    // trace flag, and limit come back untouched.
    Boot(path) -> #(session, execute_boot(session, path), False)
    BootUsage -> #(session, ["Usage: :boot <bootfile.glp> [timeoutSec]"], False)
    Load(path) -> execute_load(session, path)
    Goal(text) -> {
      let #(_engine, env, output, traces) =
        engine.run_with_limit_traced(
          session.engine,
          text,
          session.limit,
          session.trace,
        )
      // Reference order: reduction-trace lines (`:trace`) during the run, then any
      // `_output/1` program output, then the bindings/status block, then a blank.
      #(
        session,
        list.flatten([traces, output, results.render_outcome(env), [""]]),
        False,
      )
    }
  }
}

/// Render every loaded program's bytecode (Dart glp_repl.dart:203-213: "No
/// programs loaded" when empty; else a `Bytecode for <name>:` header, a 60-char
/// rule, and the per-PC op lines — display format is not a parity surface,
/// v2.16 §0.5).
fn render_bytecode(eng: Engine) -> List(String) {
  case engine.loaded_programs(eng) {
    [] -> ["No programs loaded"]
    loaded ->
      list.flat_map(loaded, fn(entry) {
        let #(name, prog) = entry
        [
          "",
          "Bytecode for " <> name <> ":",
          string.repeat("=", 60),
          ..string.split(program.to_disassembly(prog), "\n")
        ]
      })
  }
}

/// Total drive-loop step iterations a play may take before the boot loader's
/// loud non-quiescence error (the boot-loader test suite's drive fuel; the
/// per-step reduction budget is the session's `:limit`).
const boot_drive_fuel = 10_000

/// Run a multi-isolate play (Dart `_runBoot`, glp_repl.dart:486): read the boot
/// file, extract the `boot/0` spawn directives, compile the play over the
/// prelude, boot one agent per directive, drive to quiescence, and render the
/// per-agent captured output as `[agentId] line` (the Dart `onUIOutput`
/// rendering). Error wording follows the Dart reference: a missing file and a
/// play that fails to parse report `Error: boot file not found:` /
/// `Error parsing boot file:`; failures past parsing report `Boot failed:`.
fn execute_boot(session: Session, path: String) -> List(String) {
  case read_source(path) {
    Error(_) -> ["Error: boot file not found: " <> path]
    Ok(source) -> run_boot(session, path, source)
  }
}

fn run_boot(session: Session, path: String, source: String) -> List(String) {
  case parse_play(source) {
    Error(detail) -> ["Error parsing boot file: " <> detail]
    Ok(module) ->
      case boot_loader.extract_directives(module) {
        Error(reason) -> ["Error parsing boot file: " <> reason]
        Ok(directives) ->
          case compile_play(session, source) {
            Error(detail) -> ["Boot failed: " <> detail]
            Ok(prog) ->
              case boot_loader.boot_agents(prog, directives) {
                Error(reason) -> ["Boot failed: " <> reason]
                Ok(play) ->
                  case
                    boot_loader.drive(play, session.limit, boot_drive_fuel)
                  {
                    Error(reason) -> ["Boot failed: " <> reason]
                    Ok(play) -> render_play(path, directives, play)
                  }
              }
          }
      }
  }
}

fn parse_play(source: String) -> Result(ast.SourceModule, String) {
  case lexer.tokenize(source) {
    Error(e) -> Error(string.inspect(e))
    Ok(tokens) ->
      case parser.parse_module(tokens) {
        Error(e) -> Error(string.inspect(e))
        Ok(module) -> Ok(module)
      }
  }
}

/// The play program each agent runs: the play source compiled on the boot
/// loader's stage set (parse → SRSW → PE → codegen, no fatal type check — the
/// T039 boot path), merged over the compiled prelude so prelude procedures
/// (`:=`/2 …) resolve — the Gleam composition of the Dart isolates' "self.glp
/// then the play project" load order (prelude ops first, first-occurrence-wins).
fn compile_play(
  session: Session,
  source: String,
) -> Result(program.BytecodeProgram, String) {
  case loader.compile_prelude(source) {
    Error(staged) -> Error(string.inspect(staged))
    Ok(play_prog) ->
      case loader.compile_prelude(engine.prelude_source(session.engine)) {
        Error(staged) -> Error(string.inspect(staged))
        Ok(prelude_prog) -> Ok(program.merge(play_prog, prelude_prog))
      }
  }
}

/// The success block: the Dart boot banner (`Booting N … from <path>`, the
/// agent list), each agent's captured `_output/1` lines as `[agentId] line`
/// (directive order — the Dart per-isolate `onUIOutput` prefix), and a
/// quiescence footer (the synchronous analogue of Dart's `✓ Shutdown
/// complete`). Display format is not a parity surface.
fn render_play(
  path: String,
  directives: List(boot_loader.SpawnDirective),
  play: boot_loader.Play,
) -> List(String) {
  let ids = list.map(directives, fn(d) { d.agent_id })
  let agent_lines =
    list.flat_map(boot_loader.agents(play), fn(entry) {
      let #(agent, me) = entry
      let id = agent_label(agent)
      scheduler.captured_output(mad_engine.engine(me))
      |> list.map(fn(line) { "[" <> id <> "] " <> line })
    })
  list.flatten([
    [
      "Booting "
        <> int.to_string(list.length(ids))
        <> " agents from "
        <> path,
      "  agents: " <> string.join(ids, ", "),
    ],
    agent_lines,
    ["✓ Play quiesced"],
  ])
}

fn agent_label(agent: Term) -> String {
  case agent {
    ConstTerm(ConstAtom(id)) -> id
    other -> string.inspect(other)
  }
}

fn execute_load(
  session: Session,
  path: String,
) -> #(Session, List(String), Bool) {
  case read_source(path) {
    Error(_) -> #(
      session,
      ["Error loading " <> path <> ": File not found"],
      False,
    )
    Ok(source) ->
      case engine.load(session.engine, path, source) {
        Ok(engine) -> #(
          Session(..session, engine: engine),
          ["✓ Loaded: " <> path],
          False,
        )
        Error(staged) -> #(
          session,
          ["Error loading " <> path <> ": " <> string.inspect(staged)],
          False,
        )
      }
  }
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits)
    Error(_) -> Error(Nil)
  }
}
