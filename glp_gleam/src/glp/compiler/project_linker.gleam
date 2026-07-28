//// glp/compiler/project_linker — static linking of multi-module GLP projects
//// (wave-3 T009, FR-008; Section F's mechanism).
////
//// Faithful port of `glp_runtime/lib/compiler/project_linker.dart` +
//// `_detectTopModule` (glp_engine.dart:358) per the T009 dossier (research.md):
//// given a project's parsed `.glp` sources, discover modules (name, ancestor
//// `self.glp` type scope), type-check each independently against its ancestor
//// scope, then link everything into ONE flat module where every inter-module
//// call is resolved to a renamed local procedure (`p/n` → `M:p/n`).
////
//// PURE over parsed sources: the engine facade owns all disk I/O (the FR-009
//// precedent) and hands in `#(path, source)` pairs with `/`-separated paths
//// RELATIVE to the project root. Reference spec:
//// `docs/modules/glp-project-compilation-spec.md`.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/set
import gleam/string
import glp/analysis/type_ast.{type TypeEnvironment, TypeEnvironment}
import glp/analysis/type_checker/param_expansion
import glp/analysis/type_checker/type_checker
import glp/analysis/type_checker/type_environment_builder as teb
import glp/compiler/partial_eval
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser
import glp/runtime/terms

/// A discovered module in the project tree (Dart `DiscoveredModule`).
pub type DiscoveredModule {
  DiscoveredModule(
    /// `/`-separated path relative to the project root (e.g. "ui/mediator.glp").
    file_path: String,
    module_name: String,
    module: ast.SourceModule,
    ancestor_scope: TypeEnvironment,
    is_self_glp: Bool,
  )
}

// ── Discovery (Dart discoverProject, minus the walk — the facade lists) ──────

/// Discover the project's modules from its `.glp` sources. `files` are
/// `#(relative_path, source)` pairs (the facade already excluded
/// `boot_direct.glp` / `mad_boot.glp` / `mad_boot/`); `root_name` is the
/// project directory's own name (the module name of a root-level `self.glp` —
/// Dart `_moduleNameFromDirPath`); `prelude_source` is the root
/// `programs/self.glp` the prelude environment is built from.
pub fn discover(
  files: List(#(String, String)),
  root_name: String,
  prelude_source: String,
) -> Result(List(DiscoveredModule), String) {
  use prelude_env <- result.try(
    teb.build_prelude_environment(prelude_source)
    |> result.map_error(fn(e) {
      "project discover: prelude environment: " <> teb.error_to_string(e)
    }),
  )
  // Parse every file once.
  use parsed <- result.try(
    list.try_map(files, fn(file) {
      let #(path, source) = file
      use module <- result.try(parse_source(path, source))
      Ok(#(path, module))
    }),
  )
  // The self.glp modules, needed for every file's ancestor chain.
  let self_glps =
    list.filter(parsed, fn(p) { basename(p.0) == "self.glp" })
  list.try_map(parsed, fn(p) {
    let #(path, module) = p
    let is_self = basename(path) == "self.glp"
    let module_name = case ast.module_name(module) {
      Some(name) -> name
      None ->
        case is_self {
          True -> dir_module_name(dir_of(path), root_name)
          False -> strip_glp(basename(path))
        }
    }
    // Ancestor chain: self.glp files from the file's directory up to the root,
    // root-first; a self.glp's own chain starts at its parent's parent (Dart
    // discoverSelfChain).
    let chain =
      self_chain(path, is_self, self_glps)
      |> list.map(fn(s) { s.1 })
    use scope <- result.try(build_ancestor_scope(chain, prelude_env))
    Ok(DiscoveredModule(path, module_name, module, scope, is_self))
  })
}

fn parse_source(
  path: String,
  source: String,
) -> Result(ast.SourceModule, String) {
  case lexer.tokenize(source) {
    Error(lexer.LexError(message, line, _)) ->
      Error(
        "project parse: "
        <> path
        <> ": lex error at line "
        <> int.to_string(line)
        <> ": "
        <> message,
      )
    Ok(tokens) ->
      case parser.parse_module(tokens) {
        Error(parser.ParseError(message, line, _)) ->
          Error(
            "project parse: "
            <> path
            <> ": parse error at line "
            <> int.to_string(line)
            <> ": "
            <> message,
          )
        Ok(module) -> Ok(module)
      }
  }
}

/// The self.glp entries on the path from `path`'s directory up to the root,
/// ROOT-FIRST (Dart discoverSelfChain reverses its bottom-up walk).
fn self_chain(
  path: String,
  is_self: Bool,
  self_glps: List(#(String, ast.SourceModule)),
) -> List(#(String, ast.SourceModule)) {
  let start_dir = case is_self {
    // The file IS a self.glp — its chain starts at the parent's parent.
    True -> parent_dir(dir_of(path))
    False -> dir_of(path)
  }
  walk_up(start_dir, [])
  |> list.filter_map(fn(dir) {
    let self_path = join(dir, "self.glp")
    list.find(self_glps, fn(s) { s.0 == self_path })
  })
}

/// Directories from `dir` up to the root, ROOT-FIRST ("" last in the walk, so
/// first after the accumulating prepend).
fn walk_up(dir: String, acc: List(String)) -> List(String) {
  case dir {
    "" -> [dir, ..acc]
    _ -> walk_up(parent_dir(dir), [dir, ..acc])
  }
}

/// Layer the ancestor `self.glp` scopes over the prelude environment
/// (Dart `_buildAncestorScope`): each self.glp's parameterized templates are
/// extracted before expansion and threaded to descendants; children shadow
/// parents (`merge`'s other-wins rule over the root-first chain).
fn build_ancestor_scope(
  chain: List(ast.SourceModule),
  prelude_env: TypeEnvironment,
) -> Result(TypeEnvironment, String) {
  list.try_fold(chain, prelude_env, fn(env, self_module) {
    let self_templates =
      list.fold(self_module.type_defs, dict.new(), fn(acc, td) {
        case type_ast.is_parameterized_def(td) {
          True -> dict.insert(acc, td.name, td)
          False -> acc
        }
      })
    let expanded =
      param_expansion.expand_parameterized_types(
        self_module,
        set.from_list(dict.keys(env.types)),
        env.type_templates,
      )
    use self_env <- result.try(
      teb.build_environment_from_module(expanded, False, True)
      |> result.map_error(fn(e) {
        "project ancestor scope: " <> teb.error_to_string(e)
      }),
    )
    Ok(type_ast.merge(
      env,
      TypeEnvironment(
        self_env.types,
        self_env.procedures,
        self_env.param_proc_decls,
        self_templates,
      ),
    ))
  })
}

// ── Per-module type check (Dart typeCheckProject) ────────────────────────────

/// Type-check each module independently against its ancestor scope. Modules
/// with no own (non-imported) declarations are skipped (untyped orchestration).
/// The first failing module rejects with its name, path, and errors.
pub fn type_check_project(
  modules: List(DiscoveredModule),
  prelude_units: Dict(String, List(ast.Term)),
) -> Result(Nil, String) {
  list.try_each(modules, fn(mod) {
    let has_own_decls =
      list.any(mod.module.proc_declarations, fn(d) { !d.imported })
    case mod.module.proc_declarations == [] || !has_own_decls {
      True -> Ok(Nil)
      False -> {
        // Same pipeline as the compiler: PE before type check.
        use transformed <- result.try(
          case
            partial_eval.transform_defined_guards_analyzer(
              mod.module.procedures,
              prelude_units,
            )
          {
            Ok(t) -> Ok(t)
            Error(partial_eval.DefinedGuardError(message, _))
            | Error(partial_eval.GuardAdmissionError(message, _)) ->
              Error(
                "project PE failed for "
                <> mod.module_name
                <> " ("
                <> mod.file_path
                <> "): "
                <> message,
              )
          },
        )
        case
          type_checker.check_module(
            mod.module,
            Some(transformed),
            Some(mod.ancestor_scope),
          )
        {
          Error(env_error) ->
            Error(
              "project type env failed for "
              <> mod.module_name
              <> ": "
              <> teb.error_to_string(env_error),
            )
          Ok(res) ->
            case res.errors {
              [] -> Ok(Nil)
              errors ->
                Error(
                  "Type checking failed for "
                  <> mod.module_name
                  <> " ("
                  <> mod.file_path
                  <> "):\n"
                  <> string.join(
                    list.map(errors, fn(e) { "  " <> e.message }),
                    "\n",
                  ),
                )
            }
        }
      }
    }
  })
}

// ── Top-module detection (Dart _detectTopModule, glp_engine.dart:358) ────────

/// The orchestrator: the single module with imported declarations, else the
/// module with the most procedures.
pub fn detect_top_module(modules: List(DiscoveredModule)) -> String {
  let with_imports =
    list.filter(modules, fn(m) {
      list.any(m.module.proc_declarations, fn(d) { d.imported })
    })
  case with_imports {
    [only] -> only.module_name
    _ ->
      modules
      |> list.sort(fn(a, b) {
        int.compare(
          list.length(b.module.procedures),
          list.length(a.module.procedures),
        )
      })
      |> list.first
      |> result.map(fn(m) { m.module_name })
      |> result.unwrap("")
  }
}

// ── Linking (Dart linkProject) ───────────────────────────────────────────────

/// Link all modules into one flat module: rename every procedure `p/n` →
/// `M:p/n`, resolve body calls (local → ancestor self.glp chain → leave for
/// prelude/kernel; static `M # p(...)` → direct `M:p` call), generate entry
/// aliases (top module: ALL procedures; others: EXPORTED only; first wins) with
/// mode-aware argument forwarding, and carry the renamed non-imported
/// declarations. The result compiles in system mode with the SRSW stage elided
/// (Dart `compileProgram`'s `skipGlobalSRSW: true` — modules were already
/// checked individually).
pub fn link_project(
  modules: List(DiscoveredModule),
  top_module_name: String,
) -> ast.SourceModule {
  // Procedure registry: module name → its "name/arity" signatures.
  let registry =
    list.fold(modules, dict.new(), fn(acc, mod) {
      dict.insert(
        acc,
        mod.module_name,
        list.fold(mod.module.procedures, set.new(), fn(sigs, proc) {
          set.insert(sigs, ast.signature(proc))
        }),
      )
    })
  // Per-module ancestor self.glp procedure map: sig → owning ancestor module,
  // innermost ancestor wins (Dart: sort by path length desc + putIfAbsent).
  let self_glps = list.filter(modules, fn(m) { m.is_self_glp })
  let ancestor_procs =
    list.fold(modules, dict.new(), fn(acc, mod) {
      let mod_dir = dir_of(mod.file_path)
      let ancestors =
        self_glps
        |> list.filter(fn(s) {
          s.file_path != mod.file_path
          && dir_prefixes(dir_of(s.file_path), mod_dir)
        })
        |> list.sort(fn(a, b) {
          int.compare(string.length(b.file_path), string.length(a.file_path))
        })
      let procs =
        list.fold(ancestors, dict.new(), fn(procs, self_mod) {
          list.fold(self_mod.module.procedures, procs, fn(procs, proc) {
            let sig = ast.signature(proc)
            case dict.has_key(procs, sig) {
              True -> procs
              False -> dict.insert(procs, sig, self_mod.module_name)
            }
          })
        })
      dict.insert(acc, mod.module_name, procs)
    })

  // Rename every procedure and resolve its bodies.
  let renamed =
    list.flat_map(modules, fn(mod) {
      let local_sigs =
        dict.get(registry, mod.module_name) |> result.unwrap(set.new())
      let mod_ancestors =
        dict.get(ancestor_procs, mod.module_name) |> result.unwrap(dict.new())
      list.map(mod.module.procedures, fn(proc) {
        ast.Procedure(
          mod.module_name <> ":" <> proc.name,
          proc.arity,
          list.map(proc.clauses, fn(clause) {
            ast.Clause(
              ast.Atom(
                mod.module_name <> ":" <> clause.head.functor,
                clause.head.args,
                clause.head.pos,
              ),
              clause.guards,
              option.map(clause.body, fn(goals) {
                list.map(goals, fn(g) {
                  resolve_goal(g, mod.module_name, local_sigs, mod_ancestors)
                })
              }),
              clause.pos,
            )
          }),
          proc.pos,
        )
      })
    })

  // Project-wide declaration index for mode-aware aliases (first decl wins).
  let decl_index =
    list.fold(modules, dict.new(), fn(acc, mod) {
      list.fold(mod.module.proc_declarations, acc, fn(acc, d) {
        case d.imported || dict.has_key(acc, type_ast.key(d)) {
          True -> acc
          False -> dict.insert(acc, type_ast.key(d), d)
        }
      })
    })

  // Entry-point aliases: top module aliases ALL its procedures (REPL
  // invocation); others only EXPORTED ones; first alias per sig wins.
  let #(aliases, _aliased) =
    list.fold(modules, #([], set.new()), fn(acc, mod) {
      let is_top = mod.module_name == top_module_name
      list.fold(mod.module.procedures, acc, fn(acc, proc) {
        let #(aliases, aliased) = acc
        let sig = ast.signature(proc)
        let wanted = case is_top {
          True -> True
          False ->
            list.any(mod.module.proc_declarations, fn(d) {
              d.exported
              && d.name == proc.name
              && list.length(d.arg_types) == proc.arity
            })
        }
        case !wanted || set.contains(aliased, sig) {
          True -> acc
          False -> {
            let decl =
              case
                list.find(mod.module.proc_declarations, fn(d) {
                  !d.imported
                  && d.name == proc.name
                  && list.length(d.arg_types) == proc.arity
                })
              {
                Ok(d) -> Some(d)
                Error(_) ->
                  dict.get(decl_index, sig) |> option.from_result
              }
            let alias =
              make_alias(
                proc.name,
                proc.arity,
                mod.module_name <> ":" <> proc.name,
                decl,
              )
            #([alias, ..aliases], set.insert(aliased, sig))
          }
        }
      })
    })

  // Renamed non-imported declarations (SRSW type-based relaxation downstream).
  let renamed_decls =
    list.flat_map(modules, fn(mod) {
      mod.module.proc_declarations
      |> list.filter(fn(d) { !d.imported })
      |> list.map(fn(d) {
        type_ast.ProcDecl(..d, name: mod.module_name <> ":" <> d.name)
      })
    })

  ast.SourceModule(
    declaration: None,
    type_defs: [],
    proc_declarations: renamed_decls,
    param_proc_decls: [],
    procedures: list.append(renamed, list.reverse(aliases)),
    compile_mode: ast.System,
    pos: type_ast.Pos(0, 0),
  )
}

/// Resolve one body goal (Dart `_resolveGoal`): static remote → direct `M':p`
/// call; local → `M:p`; ancestor self.glp → `A:p`; else leave (prelude /
/// stdlib / body kernel). Dynamic remotes and nested remotes stay as-is.
fn resolve_goal(
  goal: ast.Goal,
  module_name: String,
  local_sigs: set.Set(String),
  ancestors: Dict(String, String),
) -> ast.Goal {
  case goal {
    ast.RemoteGoal(module_term, inner, pos) ->
      case module_term, inner {
        // Static dispatch: M' # p(...) → M':p(...).
        ast.ConstTerm(terms.ConstAtom(target), _), ast.Goal(f, args, _) ->
          ast.Goal(target <> ":" <> f, args, pos)
        _, _ -> goal
      }
    ast.SpawnGoal(ast.InnerGoal(f, args, ipos), agent_id, pos) -> {
      let sig = f <> "/" <> int.to_string(list.length(args))
      case resolve_name(f, sig, module_name, local_sigs, ancestors) {
        Some(resolved) ->
          ast.SpawnGoal(ast.InnerGoal(resolved, args, ipos), agent_id, pos)
        None -> goal
      }
    }
    ast.Goal(f, args, pos) -> {
      let sig = f <> "/" <> int.to_string(list.length(args))
      case resolve_name(f, sig, module_name, local_sigs, ancestors) {
        Some(resolved) -> ast.Goal(resolved, args, pos)
        None -> goal
      }
    }
  }
}

fn resolve_name(
  functor: String,
  sig: String,
  module_name: String,
  local_sigs: set.Set(String),
  ancestors: Dict(String, String),
) -> Option(String) {
  case set.contains(local_sigs, sig) {
    True -> Some(module_name <> ":" <> functor)
    False ->
      case dict.get(ancestors, sig) {
        Ok(ancestor) -> Some(ancestor <> ":" <> functor)
        Error(_) -> None
      }
  }
}

/// A mode-aware alias `p(V0, …) :- M:p(V0?, V1, …)` (Dart `_makeAliasClause`):
/// declared input args forward as readers, output args as writers; without a
/// declaration every body arg falls back to reader.
fn make_alias(
  name: String,
  arity: Int,
  target: String,
  declaration: Option(type_ast.ProcDecl),
) -> ast.Procedure {
  let pos = type_ast.Pos(0, 0)
  let clause = case arity {
    0 ->
      ast.Clause(
        ast.Atom(name, [], pos),
        None,
        Some([ast.Goal(target, [], pos)]),
        pos,
      )
    _ -> {
      let indexes = list.index_map(list.repeat(0, arity), fn(_, i) { i })
      let head_args =
        list.map(indexes, fn(i) {
          ast.VarTerm("V" <> int.to_string(i), False, pos)
        })
      let body_args =
        list.map(indexes, fn(i) {
          let is_input = case declaration {
            Some(decl) ->
              case i < list.length(decl.arg_types) {
                True -> type_ast.is_input_arg(decl, i)
                False -> True
              }
            None -> True
          }
          ast.VarTerm("V" <> int.to_string(i), is_input, pos)
        })
      ast.Clause(
        ast.Atom(name, head_args, pos),
        None,
        Some([ast.Goal(target, body_args, pos)]),
        pos,
      )
    }
  }
  ast.Procedure(name, arity, [clause], pos)
}

// ── Path helpers (`/`-separated, relative to the project root; "" = root) ────

fn basename(path: String) -> String {
  case list.last(string.split(path, "/")) {
    Ok(base) -> base
    Error(_) -> path
  }
}

fn dir_of(path: String) -> String {
  case string.split(path, "/") |> list.reverse {
    [_file, ..dirs] -> dirs |> list.reverse |> string.join("/")
    [] -> ""
  }
}

fn parent_dir(dir: String) -> String {
  dir_of(dir)
}

fn join(dir: String, file: String) -> String {
  case dir {
    "" -> file
    _ -> dir <> "/" <> file
  }
}

fn strip_glp(filename: String) -> String {
  case string.ends_with(filename, ".glp") {
    True -> string.drop_end(filename, 4)
    False -> filename
  }
}

/// Module name of a self.glp from its directory: the last path component, or
/// the project root's own name at the root (Dart `_moduleNameFromDirPath`).
fn dir_module_name(dir: String, root_name: String) -> String {
  case dir {
    "" -> root_name
    _ -> basename(dir)
  }
}

/// Is `ancestor_dir` an ancestor of (or equal to) `dir`? Component-aware
/// (Dart uses raw `startsWith` on absolute paths; "" is the root and prefixes
/// everything).
fn dir_prefixes(ancestor_dir: String, dir: String) -> Bool {
  ancestor_dir == ""
  || ancestor_dir == dir
  || string.starts_with(dir, ancestor_dir <> "/")
}
