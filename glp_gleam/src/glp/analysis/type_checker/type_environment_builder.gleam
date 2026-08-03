//// glp/analysis/type_checker/type_environment_builder — builds a
//// `TypeEnvironment` from a parsed module (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/type_environment_builder.dart
//// (spec: docs/modules/type-environment.md v0.8). Loads the prelude, merges user
//// definitions, resolves type aliases (simple + union) at preprocessing time, and
//// checks type determinism.
////
//// Port notes:
////   * Dart's module-global mutable `_preludeEnvironmentSource` + setter are
////     replaced by an explicit `source` parameter on `build_prelude_environment`
////     (the engine threads `programs/self.glp` in; the Dart `typePrelude` constant
////     is the empty string in this port, so the default is an empty environment).
////   * Dart throws `RedefinitionError` / `CircularAliasError` /
////     `NonDeterministicTypeError` / `AliasExpansionError` (uncaught, propagate to
////     the engine); the port returns them as a `Result` error (`TypeEnvError`).
////   * Dart mutates the `types`/`procedures` maps in place across the alias-
////     resolution steps; the port threads new dicts through each step.
////   * Constant keys (determinism + labels) use the BARE `value.toString()` (Dart
////     `Object.toString`), like program_dfa's `const_label_symbol` — atoms and
////     strings both unquoted.

import gleam/dict.{type Dict}
import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/set
import gleam/string
import glp/analysis/prelude
import glp/analysis/type_ast.{
  type ProcDecl, type TypeDef, type TypeEnvironment, type TypeExpr, ConstantAlt,
  CvAtom, CvInt, CvReal, CvString, DiffListAlt, ListConsAlt, ListNilAlt, Pos,
  PrimitiveModeAlt, ProcDecl, StructAlt, TypeDef, TypeEnvironment, TypeRef,
}
import glp/analysis/type_checker/param_expansion
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser

// =============================================================================
// Errors
// =============================================================================

/// A type-environment construction error (Dart's four exception classes). Each
/// carries the offending node's position.
pub type TypeEnvError {
  /// Illegal redefinition of a predefined type/procedure (Dart `RedefinitionError`).
  RedefinitionError(message: String, line: Int, column: Int)
  /// Circular alias chain (Dart `CircularAliasError`).
  CircularAliasError(message: String, line: Int, column: Int)
  /// Overlapping type alternatives (Dart `NonDeterministicTypeError`).
  NonDeterministicTypeError(message: String, line: Int, column: Int)
  /// Union alias expansion failure (Dart `AliasExpansionError`).
  AliasExpansionError(message: String, line: Int, column: Int)
}

/// Dart `Exception.toString()` — `"$message at line $line, column $column"`.
pub fn error_to_string(error: TypeEnvError) -> String {
  case error {
    RedefinitionError(message, line, column)
    | CircularAliasError(message, line, column)
    | NonDeterministicTypeError(message, line, column)
    | AliasExpansionError(message, line, column) ->
      message <> " at line " <> int.to_string(line) <> ", column " <> int.to_string(column)
  }
}

// =============================================================================
// Public builders
// =============================================================================

/// Build a `TypeEnvironment` from the prelude source (Dart `buildPreludeEnvironment`).
/// An empty source (the Dart `typePrelude` default) yields an empty environment.
pub fn build_prelude_environment(
  source: String,
) -> Result(TypeEnvironment, TypeEnvError) {
  case source == "" {
    True -> Ok(type_ast.empty_environment())
    False -> {
      // The prelude source is trusted; a lex/parse failure is an invariant.
      let tokens = case lexer.tokenize(source) {
        Ok(t) -> t
        Error(e) ->
          panic as { "build_prelude_environment: lex error: " <> string.inspect(e) }
      }
      let module = case parser.parse_module(tokens) {
        Ok(m) -> m
        Error(e) ->
          panic as { "build_prelude_environment: parse error: " <> string.inspect(e) }
      }
      // Extract parameterized templates before expansion removes them.
      let prelude_templates =
        list.fold(module.type_defs, dict.new(), fn(acc, td) {
          case type_ast.is_parameterized_def(td) {
            True -> dict.insert(acc, td.name, td)
            False -> acc
          }
        })
      let expanded =
        param_expansion.expand_parameterized_types(module, set.new(), dict.new())
      use env <- result.try(build_environment_from_module(expanded, False, True))
      Ok(TypeEnvironment(
        env.types,
        env.procedures,
        env.param_proc_decls,
        prelude_templates,
      ))
    }
  }
}

/// Build a `TypeEnvironment` from a parsed module (Dart `buildTypeEnvironment`).
/// The base environment is `ancestor_scope` if provided, else an empty prelude;
/// the module's own definitions merge on top (shadowing), then aliases resolve on
/// the merged environment.
pub fn build_type_environment(
  module: ast.SourceModule,
  ancestor_scope: Option(TypeEnvironment),
) -> Result(TypeEnvironment, TypeEnvError) {
  use base_env <- result.try(case ancestor_scope {
    Some(e) -> Ok(e)
    None -> build_prelude_environment("")
  })
  let check_redefinitions = case ancestor_scope {
    None -> True
    Some(_) -> False
  }
  use user_env <- result.try(build_environment_from_module(
    module,
    check_redefinitions,
    False,
  ))
  let merged = type_ast.merge(base_env, user_env)
  // Resolve aliases on the merged environment (user aliases can reference prelude).
  use #(types, procedures) <- result.try(resolve_aliases(
    merged.types,
    merged.procedures,
  ))
  Ok(TypeEnvironment(types, procedures, merged.param_proc_decls, dict.new()))
}

/// Extract all clauses from a module's procedures (Dart `extractClauses`).
pub fn extract_clauses(module: ast.SourceModule) -> List(ast.Clause) {
  list.flat_map(module.procedures, fn(p) { p.clauses })
}

// =============================================================================
// Environment construction from a module
// =============================================================================

/// Build a `TypeEnvironment` from a module's type definitions and procedure
/// declarations (Dart `_buildEnvironmentFromModule`; public because the
/// project linker's ancestor-scope layering builds per-self.glp scopes with it,
/// as Dart `_buildAncestorScope` does via `buildScopeFromModule`).
pub fn build_environment_from_module(
  module: ast.SourceModule,
  check_redefinitions: Bool,
  resolve_aliases_now: Bool,
) -> Result(TypeEnvironment, TypeEnvError) {
  use types <- result.try(
    list.try_fold(module.type_defs, dict.new(), fn(types, type_def) {
      case check_redefinitions && prelude.is_predefined_type(type_def.name) {
        True ->
          Error(RedefinitionError(
            "Cannot redefine predefined type: " <> type_def.name,
            type_def.pos.line,
            type_def.pos.column,
          ))
        False -> {
          // Aliases are allowed (v0.7) — determinism check skipped for them.
          use _ <- result.try(case is_type_alias(type_def) {
            True -> Ok(Nil)
            False -> check_determinism(type_def)
          })
          Ok(dict.insert(types, type_def.name, type_def))
        }
      }
    }),
  )

  use procedures <- result.try(
    list.try_fold(module.proc_declarations, dict.new(), fn(procs, proc_decl) {
      case check_redefinitions && prelude.is_predefined_procedure(proc_decl.name) {
        True ->
          Error(RedefinitionError(
            "Cannot redefine predefined procedure: "
              <> proc_decl.name
              <> "/"
              <> int.to_string(type_ast.arity(proc_decl)),
            proc_decl.pos.line,
            proc_decl.pos.column,
          ))
        False -> {
          // Mark as builtin if it is a true builtin (implemented natively).
          let is_builtin = prelude.is_builtin_procedure(type_ast.key(proc_decl))
          let decl = case is_builtin && !proc_decl.is_builtin {
            True ->
              ProcDecl(
                name: proc_decl.name,
                arg_types: proc_decl.arg_types,
                type_params: [],
                pos: proc_decl.pos,
                is_builtin: True,
                exported: proc_decl.exported,
                imported: proc_decl.imported,
                module_path: proc_decl.module_path,
              )
            False -> proc_decl
          }
          Ok(dict.insert(procs, type_ast.qualified_key(proc_decl), decl))
        }
      }
    }),
  )

  let param_proc_decls =
    list.fold(module.param_proc_decls, dict.new(), fn(acc, param_decl) {
      dict.insert(acc, type_ast.qualified_key(param_decl), param_decl)
    })

  case resolve_aliases_now {
    True -> {
      use #(types2, procedures2) <- result.try(resolve_aliases(types, procedures))
      Ok(TypeEnvironment(types2, procedures2, param_proc_decls, dict.new()))
    }
    False -> Ok(TypeEnvironment(types, procedures, param_proc_decls, dict.new()))
  }
}

// =============================================================================
// Alias classification
// =============================================================================

/// A simple alias: a single alternative that is a `TypeRef` or `PrimitiveModeAlt`
/// (Dart `_isSimpleAlias`).
fn is_simple_alias(def: TypeDef) -> Bool {
  case def.alternatives {
    [PrimitiveModeAlt(_, _)] -> True
    [TypeRef(_, _, _, _)] -> True
    _ -> False
  }
}

/// A union alias: two or more alternatives, all `TypeRef`s to non-predefined
/// types (Dart `_isUnionAlias`).
fn is_union_alias(def: TypeDef) -> Bool {
  case list.length(def.alternatives) >= 2 {
    False -> False
    True ->
      list.all(def.alternatives, fn(alt) {
        case alt {
          TypeRef(name, _, _, _) -> !prelude.is_predefined_type(name)
          _ -> False
        }
      })
  }
}

fn is_type_alias(def: TypeDef) -> Bool {
  is_simple_alias(def) || is_union_alias(def)
}

// =============================================================================
// Alias resolution (Dart `_resolveAliases`)
// =============================================================================

fn resolve_aliases(
  types: Dict(String, TypeDef),
  procedures: Dict(String, ProcDecl),
) -> Result(#(Dict(String, TypeDef), Dict(String, ProcDecl)), TypeEnvError) {
  // Step 1: identify simple and union aliases.
  let simple_aliases = dict.filter(types, fn(_k, v) { is_simple_alias(v) })
  let union_aliases = dict.filter(types, fn(_k, v) { is_union_alias(v) })

  case dict.is_empty(simple_aliases) && dict.is_empty(union_aliases) {
    True -> Ok(#(types, procedures))
    False -> {
      // Step 2: resolve simple aliases transitively (with cycle detection).
      use resolved <- result.try(resolve_all_simple(simple_aliases))
      // Step 3: expand union aliases (updates the types map).
      use types_u <- result.try(expand_union_aliases(
        union_aliases,
        simple_aliases,
        types,
      ))
      // Step 4: replace simple alias references in non-simple-alias type defs.
      let types_r =
        dict.map_values(types_u, fn(k, v) {
          case dict.has_key(simple_aliases, k) {
            True -> v
            False ->
              TypeDef(
                v.name,
                [],
                list.map(v.alternatives, replace_alias_references(_, resolved)),
                v.pos,
              )
          }
        })
      // Step 5: replace alias references in procedure declarations.
      let procedures_r =
        dict.map_values(procedures, fn(_k, v) {
          ProcDecl(
            name: v.name,
            arg_types: list.map(v.arg_types, replace_alias_references(_, resolved)),
            type_params: [],
            pos: v.pos,
            is_builtin: v.is_builtin,
            exported: v.exported,
            imported: v.imported,
            module_path: v.module_path,
          )
        })
      // Step 6: remove simple alias definitions.
      let types_final = dict.drop(types_r, dict.keys(simple_aliases))
      Ok(#(types_final, procedures_r))
    }
  }
}

/// Resolve every simple alias, accumulating a `name -> resolved TypeExpr` memo
/// (Dart's `for name in simpleAliases.keys resolveSimpleAlias(name)`).
fn resolve_all_simple(
  simple_aliases: Dict(String, TypeDef),
) -> Result(Dict(String, TypeExpr), TypeEnvError) {
  list.try_fold(dict.keys(simple_aliases), dict.new(), fn(resolved, name) {
    case resolve_simple_alias(name, simple_aliases, resolved, set.new()) {
      Ok(#(_expr, resolved2)) -> Ok(resolved2)
      Error(e) -> Error(e)
    }
  })
}

/// Resolve one simple alias transitively (Dart nested `resolveSimpleAlias`).
/// `visiting` is the current DFS path (cycle detection); `resolved` is the memo.
fn resolve_simple_alias(
  name: String,
  simple_aliases: Dict(String, TypeDef),
  resolved: Dict(String, TypeExpr),
  visiting: set.Set(String),
) -> Result(#(TypeExpr, Dict(String, TypeExpr)), TypeEnvError) {
  case dict.get(resolved, name) {
    Ok(r) -> Ok(#(r, resolved))
    Error(_) ->
      case dict.get(simple_aliases, name) {
        // Not a simple alias — return a plain TypeRef to it.
        Error(_) -> Ok(#(TypeRef(name, False, [], Pos(0, 0)), resolved))
        Ok(alias_def) ->
          case set.contains(visiting, name) {
            True ->
              Error(CircularAliasError(
                "Circular alias chain detected: " <> name,
                alias_def.pos.line,
                alias_def.pos.column,
              ))
            False -> {
              let visiting2 = set.insert(visiting, name)
              case alias_def.alternatives {
                [TypeRef(target_name, is_input, _, target_pos)] ->
                  case dict.has_key(simple_aliases, target_name) {
                    // Target is also an alias — resolve transitively.
                    True -> {
                      use #(resolved_target, resolved2) <- result.try(
                        resolve_simple_alias(
                          target_name,
                          simple_aliases,
                          resolved,
                          visiting2,
                        ),
                      )
                      let res =
                        apply_complement(
                          resolved_target,
                          is_input,
                          target_pos.line,
                          target_pos.column,
                        )
                      Ok(#(res, dict.insert(resolved2, name, res)))
                    }
                    // Target is a real type — keep as TypeRef.
                    False -> {
                      let res = TypeRef(target_name, is_input, [], target_pos)
                      Ok(#(res, dict.insert(resolved, name, res)))
                    }
                  }
                // Target is `_`/`_?` or (defensively) anything else.
                [target] -> Ok(#(target, dict.insert(resolved, name, target)))
                _ ->
                  // _isSimpleAlias guarantees exactly one alternative.
                  panic as "type_environment_builder: simple alias without one alternative"
              }
            }
          }
      }
  }
}

/// Expand union aliases in place (Dart `_resolveAliases` step 3).
fn expand_union_aliases(
  union_aliases: Dict(String, TypeDef),
  simple_aliases: Dict(String, TypeDef),
  types: Dict(String, TypeDef),
) -> Result(Dict(String, TypeDef), TypeEnvError) {
  list.try_fold(dict.to_list(union_aliases), types, fn(types_acc, entry) {
    let #(name, def) = entry
    use expanded_alts <- result.try(
      list.try_fold(def.alternatives, [], fn(alts_acc, alt) {
        case alt {
          TypeRef(ref_name, ref_is_input, _, _) ->
            case
              dict.has_key(simple_aliases, ref_name)
              || dict.has_key(union_aliases, ref_name)
            {
              True ->
                Error(AliasExpansionError(
                  "Union alias cannot reference another alias: " <> ref_name,
                  def.pos.line,
                  def.pos.column,
                ))
              False ->
                case prelude.is_predefined_type(ref_name) {
                  // Predefined type: keep the TypeRef as an alternative.
                  True -> Ok(list.append(alts_acc, [alt]))
                  False ->
                    case dict.get(types, ref_name) {
                      Error(_) ->
                        Error(AliasExpansionError(
                          "Union alias references undefined type: " <> ref_name,
                          def.pos.line,
                          def.pos.column,
                        ))
                      Ok(target_def) ->
                        Ok(list.append(
                          alts_acc,
                          list.map(target_def.alternatives, fn(ta) {
                            apply_complement_to_alt(
                              ta,
                              ref_is_input,
                              def.pos.line,
                              def.pos.column,
                            )
                          }),
                        ))
                    }
                }
            }
          // Unreachable: _isUnionAlias verified all alternatives are TypeRefs.
          _ -> Ok(alts_acc)
        }
      }),
    )
    let expanded_def = TypeDef(name, [], expanded_alts, def.pos)
    use _ <- result.try(check_determinism(expanded_def))
    Ok(dict.insert(types_acc, name, expanded_def))
  })
}

// =============================================================================
// Complement application (Dart `_applyComplement` / `_applyComplementToAlt`)
// =============================================================================

/// Apply the complement to a `TypeExpr` if requested — the involution `(T?)? = T`.
fn apply_complement(
  expr: TypeExpr,
  apply: Bool,
  line: Int,
  column: Int,
) -> TypeExpr {
  case apply {
    False -> expr
    True ->
      case expr {
        TypeRef(name, is_input, _, _) ->
          TypeRef(name, !is_input, [], Pos(line, column))
        PrimitiveModeAlt(is_input, _) ->
          PrimitiveModeAlt(!is_input, Pos(line, column))
        _ -> expr
      }
  }
}

/// Apply the complement to all type references within a type alternative (used for
/// complemented union references, e.g. `Msg ::= NetMsg?`).
fn apply_complement_to_alt(
  alt: TypeExpr,
  apply: Bool,
  line: Int,
  column: Int,
) -> TypeExpr {
  case apply {
    False -> alt
    True ->
      case alt {
        TypeRef(name, is_input, _, _) ->
          TypeRef(name, !is_input, [], Pos(line, column))
        PrimitiveModeAlt(is_input, _) ->
          PrimitiveModeAlt(!is_input, Pos(line, column))
        ConstantAlt(_, _) -> alt
        ListNilAlt(_) -> alt
        ListConsAlt(head, tail, _) ->
          ListConsAlt(
            apply_complement_to_alt(head, True, line, column),
            apply_complement_to_alt(tail, True, line, column),
            Pos(line, column),
          )
        StructAlt(functor, args, _) ->
          StructAlt(
            functor,
            list.map(args, fn(a) { apply_complement_to_alt(a, True, line, column) }),
            Pos(line, column),
          )
        DiffListAlt(content, hole, _) ->
          DiffListAlt(
            apply_complement_to_alt(content, True, line, column),
            apply_complement_to_alt(hole, True, line, column),
            Pos(line, column),
          )
      }
  }
}

/// Replace simple-alias references in a type expression recursively (Dart
/// `_replaceAliasReferences`).
fn replace_alias_references(
  expr: TypeExpr,
  resolved: Dict(String, TypeExpr),
) -> TypeExpr {
  case expr {
    TypeRef(name, is_input, _, pos) ->
      case dict.get(resolved, name) {
        Ok(target) -> apply_complement(target, is_input, pos.line, pos.column)
        Error(_) -> expr
      }
    PrimitiveModeAlt(_, _) -> expr
    ConstantAlt(_, _) -> expr
    ListNilAlt(_) -> expr
    ListConsAlt(head, tail, pos) ->
      ListConsAlt(
        replace_alias_references(head, resolved),
        replace_alias_references(tail, resolved),
        pos,
      )
    StructAlt(functor, args, pos) ->
      StructAlt(functor, list.map(args, replace_alias_references(_, resolved)), pos)
    DiffListAlt(content, hole, pos) ->
      DiffListAlt(
        replace_alias_references(content, resolved),
        replace_alias_references(hole, resolved),
        pos,
      )
  }
}

// =============================================================================
// Determinism checking (Dart `_checkDeterminism`)
// =============================================================================

type DetState {
  DetState(
    functors: set.Set(String),
    constants: set.Set(String),
    primitives: set.Set(String),
    has_wildcard: Bool,
  )
}

/// Check that type alternatives are deterministic (distinguishable).
fn check_determinism(def: TypeDef) -> Result(Nil, TypeEnvError) {
  let init = DetState(set.new(), set.new(), set.new(), False)
  use _final <- result.try(list.try_fold(def.alternatives, init, fn(st, alt) {
    check_determinism_alt(alt, st, def)
  }))
  Ok(Nil)
}

fn check_determinism_alt(
  alt: TypeExpr,
  st: DetState,
  def: TypeDef,
) -> Result(DetState, TypeEnvError) {
  case alt {
    ConstantAlt(value, _) -> {
      let key = const_key(value)
      case set.contains(st.constants, key) {
        True ->
          Error(nondet(
            "Duplicate constant alternative: " <> key <> " in " <> def.name,
            def,
          ))
        False -> Ok(DetState(..st, constants: set.insert(st.constants, key)))
      }
    }
    ListNilAlt(_) -> add_functor(st, def, "[]/0", "Duplicate [] alternative in " <> def.name)
    ListConsAlt(_, _, _) ->
      add_functor(st, def, "[|]/2", "Duplicate [|] alternative in " <> def.name)
    StructAlt(functor, args, _) -> {
      let key = functor <> "/" <> int.to_string(list.length(args))
      add_functor(st, def, key, "Duplicate functor alternative: " <> key <> " in " <> def.name)
    }
    DiffListAlt(_, _, _) ->
      add_functor(st, def, "\\/2", "Duplicate \\ alternative in " <> def.name)
    PrimitiveModeAlt(_, _) ->
      case st.has_wildcard || !set.is_empty(st.primitives) {
        True ->
          Error(nondet(
            "Wildcard _ overlaps with other alternatives in " <> def.name,
            def,
          ))
        False -> Ok(DetState(..st, has_wildcard: True))
      }
    TypeRef(name, _, _, _) ->
      case name {
        "Integer" | "Real" | "Number" | "String" -> {
          use _ <- result.try(check_primitive_overlap(name, st, def))
          Ok(DetState(..st, primitives: set.insert(st.primitives, name)))
        }
        _ -> Ok(st)
      }
  }
}

fn add_functor(
  st: DetState,
  def: TypeDef,
  key: String,
  message: String,
) -> Result(DetState, TypeEnvError) {
  case set.contains(st.functors, key) {
    True -> Error(nondet(message, def))
    False -> Ok(DetState(..st, functors: set.insert(st.functors, key)))
  }
}

fn check_primitive_overlap(
  new_primitive: String,
  st: DetState,
  def: TypeDef,
) -> Result(Nil, TypeEnvError) {
  case st.has_wildcard {
    True ->
      Error(nondet(
        "Wildcard _ overlaps with " <> new_primitive <> " in " <> def.name,
        def,
      ))
    False ->
      case
        new_primitive == "Number"
        && { set.contains(st.primitives, "Integer") || set.contains(st.primitives, "Real") }
      {
        True ->
          Error(nondet("Number overlaps with Integer/Real in " <> def.name, def))
        False ->
          case
            { new_primitive == "Integer" || new_primitive == "Real" }
            && set.contains(st.primitives, "Number")
          {
            True ->
              Error(nondet(
                new_primitive <> " overlaps with Number in " <> def.name,
                def,
              ))
            False ->
              case set.contains(st.primitives, new_primitive) {
                True ->
                  Error(nondet(
                    "Duplicate primitive type " <> new_primitive <> " in " <> def.name,
                    def,
                  ))
                False -> Ok(Nil)
              }
          }
      }
  }
}

fn nondet(message: String, def: TypeDef) -> TypeEnvError {
  NonDeterministicTypeError(message, def.pos.line, def.pos.column)
}

/// The bare constant key (Dart `value.toString()`: atoms and strings unquoted).
fn const_key(value: type_ast.ConstValue) -> String {
  case value {
    CvAtom(name) -> name
    CvString(s) -> s
    CvInt(i) -> int.to_string(i)
    CvReal(r) -> float.to_string(r)
  }
}
