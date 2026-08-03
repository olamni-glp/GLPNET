//// glp/mad/boot_loader — the multiagent boot loader (wave-3 US4, T039;
//// gap G9).
////
//// Port of `glp_runtime/lib/multiagent/boot_loader.dart` + the
//// `isolate_manager` spawn/drive discipline (docs/ma/isolate-boot-spec.md
//// v0.4): a play's `boot/0` clause is a conjunction of SPAWN DIRECTIVES
//// `goal(agentId, consts…, _)@agentId`; each spawns one agent running `goal`
//// with arg0 = the agent constant, the middle constants verbatim, and the
//// final `_` slot materialized as the agent's network-input reader (the
//// serializer's cold-call stream, spec §4.1).
////
//// Gleam mapping: Dart extracts directives by REGEX and strips the boot
//// clause because its parser does not know `@`; the Gleam parser models
//// `Goal@AgentId` natively (`ast.SpawnGoal`), so directives are read from the
//// parsed AST and nothing is stripped. Dart spawns OS isolates with a
//// `MadEngine` each and routes messages over ports; here each agent is a
//// `MadEngine` value and `drive` is the router — step every agent, deliver
//// each drained message to its destination's Receive, repeat until every
//// agent is idle and no message is in flight. A malformed directive, an
//// unknown goal label, a step fault, or an undeliverable message is a LOUD
//// error, never a silent drop.

import gleam/int
import gleam/list
import gleam/option.{Some}
import gleam/string
import glp/bytecode/program.{type BytecodeProgram}
import glp/engine/scheduler.{StepErrored, StepIdle}
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/parser/ast
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, VarRef}

/// One spawn directive `goal(agentId, consts…, _)@agentId` (Dart
/// `SpawnDirective`).
pub type SpawnDirective {
  SpawnDirective(agent_id: String, functor: String, args: List(ast.Term))
}

/// A booted play: the agents in directive order, each `#(agent_term, engine)`.
pub type Play {
  Play(agents: List(#(Term, MadEngine)))
}

/// Extract the spawn directives from a parsed module's `boot/0` clause (Dart
/// `BootLoader.parse`). Loud on a missing/empty `boot/0` or a non-spawn goal
/// in its body — a boot clause is directives only.
pub fn extract_directives(
  module: ast.SourceModule,
) -> Result(List(SpawnDirective), String) {
  case list.find(module.procedures, fn(p) { p.name == "boot" && p.arity == 0 }) {
    Error(_) -> Error("boot loader: no boot/0 clause in the play source")
    Ok(procedure) ->
      case procedure.clauses {
        [clause] ->
          case clause.body {
            Some([_, ..] as goals) ->
              list.try_map(goals, fn(goal) {
                case goal {
                  ast.SpawnGoal(ast.InnerGoal(functor, args, _), agent_id, _) ->
                    Ok(SpawnDirective(agent_id, functor, args))
                  other ->
                    Error(
                      "boot loader: boot/0 may contain only spawn directives"
                      <> " (goal@agent); found "
                      <> ast.goal_functor(other),
                    )
                }
              })
            _ -> Error("boot loader: boot/0 has an empty body")
          }
        _ -> Error("boot loader: boot/0 must have exactly one clause")
      }
  }
}

/// Boot one `MadEngine` per directive over `prog` (Dart `IsolateManager.spawn`
/// per agent): arg registers are the directive's constants with the final `_`
/// slot materialized as the agent's network-input READER (spec §4.1 — the
/// isolate entry's `netInReader`).
pub fn boot_agents(
  prog: BytecodeProgram,
  directives: List(SpawnDirective),
) -> Result(Play, String) {
  use agents <- try(
    list.try_map(directives, fn(directive) {
      let agent = ConstTerm(ConstAtom(directive.agent_id))
      let arity = list.length(directive.args)
      let label = directive.functor <> "/" <> int.to_string(arity)
      case program.label_pc(prog, label) {
        Error(_) ->
          Error(
            "boot loader: spawn goal "
            <> label
            <> " not found in the play program",
          )
        Ok(entry) -> {
          let me = mad_engine.new(prog, agent)
          use regs <- try(build_regs(me, directive))
          let #(me, _goal_id) = mad_engine.boot(me, label, entry, regs)
          Ok(#(agent, me))
        }
      }
    }),
  )
  Ok(Play(agents))
}

/// The isolate entry registers (Dart boot spec v0.4): constants pass verbatim;
/// a variable slot (the trailing `_`) becomes the agent's net-in reader. Any
/// other term shape in a directive is malformed — surfaced, never guessed.
fn build_regs(
  me: MadEngine,
  directive: SpawnDirective,
) -> Result(program.XRegs, String) {
  list.index_fold(directive.args, Ok(program.new_regs()), fn(acc, arg, i) {
    use regs <- try(acc)
    case arg {
      ast.ConstTerm(value, _) ->
        Ok(program.set_reg(regs, i, ConstTerm(value)))
      ast.VarTerm(_, _, _) | ast.UnderscoreTerm(_, _) ->
        Ok(program.set_reg(
          regs,
          i,
          VarRef(mad_engine.net_in_reader(me)),
        ))
      _ ->
        Error(
          "boot loader: directive "
          <> directive.functor
          <> "@"
          <> directive.agent_id
          <> " argument "
          <> int.to_string(i)
          <> " must be a constant or the net-in `_` slot",
        )
    }
  })
}

/// Drive the play to quiescence (the isolate-manager loop): step every agent
/// to idle, route each drained message to its destination agent's Receive,
/// and repeat until a full round moves no messages and every agent is idle.
/// `fuel` bounds the total steps (a non-terminating play is a loud error,
/// never a hang).
pub fn drive(play: Play, reduction_budget: Int, fuel: Int) -> Result(Play, String) {
  use #(agents, outbox, fuel) <- try(step_round(
    play.agents,
    reduction_budget,
    fuel,
  ))
  case outbox {
    [] -> Ok(Play(agents))
    _ -> {
      use agents <- try(deliver(agents, outbox))
      drive(Play(agents), reduction_budget, fuel)
    }
  }
}

fn step_round(
  agents: List(#(Term, MadEngine)),
  reduction_budget: Int,
  fuel: Int,
) -> Result(#(List(#(Term, MadEngine)), List(Message), Int), String) {
  list.try_fold(agents, #([], [], fuel), fn(acc, entry) {
    let #(done, outbox, fuel) = acc
    let #(agent, me) = entry
    use #(me, msgs, fuel) <- try(run_agent(me, reduction_budget, fuel))
    Ok(#(
      list.append(done, [#(agent, me)]),
      list.append(outbox, msgs),
      fuel,
    ))
  })
}

fn run_agent(
  me: MadEngine,
  reduction_budget: Int,
  fuel: Int,
) -> Result(#(MadEngine, List(Message), Int), String) {
  case fuel <= 0 {
    True -> Error("boot loader: play did not quiesce within fuel")
    False -> {
      let #(me, outcome, msgs) = mad_engine.step(me, reduction_budget)
      case outcome {
        StepIdle -> Ok(#(me, msgs, fuel))
        StepErrored(fault) ->
          Error("boot loader: agent step errored: " <> string.inspect(fault))
        _ -> {
          use #(me, more, fuel) <- try(run_agent(me, reduction_budget, fuel - 1))
          Ok(#(me, list.append(msgs, more), fuel))
        }
      }
    }
  }
}

/// Deliver each message to its destination agent's Receive (the port router).
/// An unknown destination or a Receive rejection is loud.
fn deliver(
  agents: List(#(Term, MadEngine)),
  outbox: List(Message),
) -> Result(List(#(Term, MadEngine)), String) {
  list.try_fold(outbox, agents, fn(agents, msg) {
    let Message(name, term, dest) = msg
    case list.any(agents, fn(entry) { entry.0 == dest }) {
      False ->
        Error(
          "boot loader: message destined for unknown agent "
          <> string.inspect(dest),
        )
      True ->
        list.try_map(agents, fn(entry) {
          let #(agent, me) = entry
          case agent == dest {
            False -> Ok(entry)
            True ->
              case mad_engine.receive(me, name, term) {
                Ok(me) -> Ok(#(agent, me))
                Error(reason) -> Error("boot loader: " <> reason)
              }
          }
        })
    }
  })
}

/// The play's agents (inspection: per-agent engines for output/status reads).
pub fn agents(play: Play) -> List(#(Term, MadEngine)) {
  play.agents
}

fn try(
  result: Result(a, String),
  continue: fn(a) -> Result(b, String),
) -> Result(b, String) {
  case result {
    Ok(value) -> continue(value)
    Error(reason) -> Error(reason)
  }
}
