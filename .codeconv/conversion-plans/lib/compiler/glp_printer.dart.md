---
path: lib/compiler/glp_printer.dart
cycle_group_id: 53
scc_siblings: []
generated_at: 2026-05-21T15:00:29Z
source_sha256: 5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def
schema_version: 1
---

# Conversion Plan: lib/compiler/glp_printer.dart

## 1. Source Analysis

`lib/compiler/glp_printer.dart` is the GLP **printer** — the inverse of the lexer/parser. It is a single stateless `class GlpPrinter` (255 lines, 15 methods: 7 public + 8 private), zero instance fields, with one dependency: `import 'ast.dart';`.

Public methods (camelCase):
- `String printProgram(Program program)` — iterates `program.procedures`, accumulates into a local `StringBuffer`, appends `writeln()` between procedures, returns `buffer.toString()`.
- `String printProcedure(Procedure procedure)` — iterates `procedure.clauses`, `buffer.writeln(printClause(clause))` per clause.
- `String printClause(Clause clause)` — writes head via `printAtom`; computes `hasGuards = clause.guards != null && clause.guards!.isNotEmpty` and `hasBody = clause.body != null && clause.body!.isNotEmpty`; emits ` :- `, guards (`.map(printGuard).join(', ')`), ` | ` separator (only if both), body (`.map(printGoal).join(', ')`); appends `.`.
- `String printAtom(Atom atom)` — empty-args returns `atom.functor`; arity-2 infix via `_isInfixOperator` renders `${arg0} OP ${arg1}` (no parens); else `${functor}(${args.map(printTerm).join(', ')})`.
- `String printGoal(Goal goal)` — `is`-chain on `RemoteGoal` (renders `${printTerm(goal.module)} # ${printGoal(goal.goal)}`), `SpawnGoal` (renders `${printGoal(goal.innerGoal)}@${goal.agentId}`); falls through to generic-Goal branch (empty-args / infix / fallthrough).
- `String printGuard(Guard guard)` — computes `prefix = guard.negated ? '~' : ''`; empty-args, infix-with-parens, or fallthrough.
- `String printTerm(Term term)` — `is`-chain on `VarTerm` (`${name}?` for readers, `name` otherwise), `UnderscoreTerm` (returns `'_'`), `ConstTerm` (delegates to `_printConstValue`), `ListTerm` (delegates to `_printList`), `StructTerm` (delegates to `_printStruct`); fallback `term.toString()`.

Private methods (leading underscore):
- `String _printConstValue(Object? value)` — runtime-type dispatch: null → `'null'`; String + `_isAtom(value)` → unquoted; String otherwise → `"…"` with escapes; int/double → `value.toString()`; fallback `value.toString()`.
- `String _printList(ListTerm list)` — `isNil` returns `[]`; else traversal loop with `Term? current = list`, `Term? tail` accumulating elements; discriminates proper vs improper output (`[a,b]` vs `[a,b | T]`).
- `String _printStruct(StructTerm struct)` — comma-functor arity-2 special case (parenthesised conjunction `(a, b)`); infix-arity-2 special case (parenthesised); empty-args returns functor; else `${functor}(${args.map(printTerm).join(', ')})`.
- `bool _isInfixOperator(String functor)` — `const` Set membership over 17 operators: `:=`, `=`, `\\=`, `=..`, `+`, `-`, `*`, `/`, `//`, `mod`, `<`, `>`, `=<`, `>=`, `=:=`, `=\\=`, `=?=`.
- `bool _isInfixGuardOperator(String predicate)` — smaller `const` Set: `<`, `>`, `=<`, `>=`, `=:=`, `=\\=`, `=?=`.
- `bool _isAtom(String s)` — two `RegExp` allocations: head `[a-z]` (via `s[0].contains(RegExp(...))`), tail `^[a-zA-Z0-9_]*$` (via `s.substring(1).contains(RegExp(...))`). Per escalation #2 (commit `09757a26`): the function is **CORRECT** — `^`/`$` are input anchors under default `multiLine: false`; no bug exists.
- `String _escapeString(String s)` — chained `replaceAll` in load-bearing order: `\\` → `\\\\`, `"` → `\\"`, `\n` → `\\n`, `\t` → `\\t`.

Three doc-comment blocks `///` on each method. Section banner comments `// Head`, `// Guards and body`, `// Body separator - only use | if there are guards`. No mutable instance state. Every method allocates its own local `StringBuffer` (statelessness is load-bearing for re-entrancy / thread-safety).

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the ratified convspec verbatim. Construct-by-construct:

**C1. `class GlpPrinter` → `public sealed class GlpPrinter`.** Stateless dispatcher with ZERO instance fields; all public Dart methods become public C# (PascalCase); all leading-underscore Dart methods become `private` (PascalCase). `sealed` because nothing extends it; not `static` (preserves `new GlpPrinter().PrintProgram(p)` call shape). No equality override. Reuses idiom `rf-dart-leading-underscore-privacy-to-csharp-private`. Load-bearing nuance: each method allocates its OWN local `StringBuilder` — no promotion of `_buffer` to a field, preserving re-entrancy/thread-safety identical to Dart source.

**C2. `StringBuffer` local accumulator → `StringBuilder` with LF-only line terminator.** `final buffer = StringBuffer()` → `var buffer = new System.Text.StringBuilder()`. `buffer.write(s)` → `buffer.Append(s)`. **`buffer.writeln()` MUST map to `buffer.Append('\n')` (NOT `AppendLine`)** — `StringBuilder.AppendLine()` appends `Environment.NewLine` (CRLF on Windows) and would break byte-identical cross-OS output; the Dart printer emits LF only. `buffer.writeln(s)` → `buffer.Append(s).Append('\n')`. `buffer.toString()` → `buffer.ToString()`. Idiom: `rf-dart-stringbuffer-to-csharp-stringbuilder` with the LF refinement (lexer.dart did not use `writeln`; this file does).

**C3. Cascade-if with nullable-collection + `isNotEmpty` double check (in `printClause`) → C# property pattern.** `clause.guards != null && clause.guards!.isNotEmpty` → `clause.Guards is { Count: > 0 }`. NRT flow-narrows `Guards` to non-null in the consequent, so `clause.Guards!.…` becomes plain `clause.Guards.…`. Three-branch output preserved verbatim (no `:-`, `:- guards`, `:- guards | body`, `:- body`). `.map(fn).join(', ')` → `string.Join(", ", coll.Select(Fn))`. Idiom: `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`.

**C4. `is`-chain runtime dispatch in `printTerm` → C# `switch` expression with declaration patterns.** ```csharp
return term switch {
    VarTerm v => v.IsReader ? $"{v.Name}?" : v.Name,
    UnderscoreTerm _ => "_",
    ConstTerm c => PrintConstValue(c.Value),
    ListTerm l => PrintList(l),
    StructTerm s => PrintStruct(s),
    _ => term.ToString() ?? string.Empty
};
``` Discard arm REQUIRED — `Term` is not C# `sealed` (per ast.dart's decision); without the discard arm CS8509 would fire. Fallback `term.ToString() ?? string.Empty` tightens Dart's non-nullable `toString` to NRT's `string?` return. Idiom: `rf-dart-is-chain-to-csharp-switch-expression-type-pattern`.

**C5. `is`-chain in `printGoal` with subclass-specific fields → switch expression with subclass-narrowed arms.** ```csharp
return goal switch {
    RemoteGoal r => $"{PrintTerm(r.Module)} # {PrintGoal(r.Goal)}",
    SpawnGoal s => $"{PrintGoal(s.InnerGoal)}@{s.AgentId}",
    _ => /* generic-Goal branch: empty-args → goal.Functor; infix-arity-2 → "{a0} OP {a1}"; else "{Functor}({comma-args})" */
};
``` Arm order load-bearing: `RemoteGoal`/`SpawnGoal` must precede the discard fallback because both extend `Goal`; the generic-Goal branch is the discard arm. Property `Goal` on `RemoteGoal` (a property whose name equals a type name) compiles cleanly under C# member-access resolution; spec consumers MUST NOT rename it.

**C6. Conditional prefix via `?:` ternary (in `printGuard`) → C# `?:` 1:1.** `string prefix = guard.Negated ? "~" : string.Empty;` (uses `string.Empty` not `""`). Three output paths via if-chain or nested switch on `(Args.Count, IsInfixGuardOperator)`. Infix guard form parenthesises `(a OP b)` — load-bearing for GLP parser round-trip; spec MUST preserve. (Atom/goal/struct infix do NOT parenthesise — deliberate asymmetry preserved verbatim.) Idiom: same as C3.

**C7. `const <String>{...}` set → `private static readonly FrozenSet<string>` with `StringComparer.Ordinal`.** ```csharp
private static readonly FrozenSet<string> InfixOps =
    new[] { ":=", "=", "\\=", "=..", "+", "-", "*", "/", "//", "mod",
            "<", ">", "=<", ">=", "=:=", "=\\=", "=?=" }
    .ToFrozenSet(StringComparer.Ordinal);
private static readonly FrozenSet<string> InfixGuards =
    new[] { "<", ">", "=<", ">=", "=:=", "=\\=", "=?=" }
    .ToFrozenSet(StringComparer.Ordinal);
``` Explicit `StringComparer.Ordinal` MANDATORY (locale-independent, matches Dart code-unit equality). FrozenSet (.NET 8+) chosen over HashSet (immutability) and over ImmutableHashSet (lookup speed). GLP operator strings `\\=`, `=\\=`, `=:=`, `=?=` preserved byte-for-byte. Idiom: `rf-dart-const-set-to-csharp-frozenset-ordinal`.

**C8. Mutable traversal loop in `_printList` → C# pattern-variable while.** ```csharp
var elements = new List<Term>();
Term? current = list;
Term? tail = null;
while (current is ListTerm cur && !cur.IsNil) {
    if (cur.Head is not null) elements.Add(cur.Head);
    if (cur.Tail is null) break;
    if (cur.Tail is ListTerm) { current = cur.Tail; }
    else { tail = cur.Tail; break; }
}
return tail is not null
    ? $"[{string.Join(", ", elements.Select(PrintTerm))} | {PrintTerm(tail)}]"
    : $"[{string.Join(", ", elements.Select(PrintTerm))}]";
``` Pattern variable `cur` is scoped to the loop body; `cur.Head` non-null inside the `is not null` guard via NRT flow. The Dart `current.head!` (null-forgiving) becomes plain `cur.Head` after the guard. Idiom: `rf-dart-mutable-local-traversal-loop-with-nullable-pointer-to-csharp-pattern-variable-while`.

**C9. Polymorphic value type-test in `_printConstValue` → switch expression with `when` clause.** ```csharp
return value switch {
    null => "null",
    string s when IsAtom(s) => s,
    string s => $"\"{EscapeString(s)}\"",
    int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
    double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
    _ => value.ToString() ?? "null"
};
``` `int` and `double` formatting MUST use `CultureInfo.InvariantCulture` — the WRITE-side counterpart to lexer.dart's READ-side invariant-parse mandate. The `when`-guarded string arm precedes the unguarded one. Dart `int || double` short-circuit OR collapses to two separate arms (no union types in current C# switch arms). Idiom: `rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture`.

**C10. `_isAtom` — two `RegExp` allocations → two `Regex.IsMatch` calls (faithful 1:1).** ```csharp
private static bool IsAtom(string s) {
    if (string.IsNullOrEmpty(s)) return false;
    if (!System.Text.RegularExpressions.Regex.IsMatch(s[0].ToString(), "[a-z]")) return false;
    return System.Text.RegularExpressions.Regex.IsMatch(s.Substring(1), @"^[a-zA-Z0-9_]*$");
}
``` Per escalation #2 closure (commit `09757a26`): the Dart source is **correct** — `^`/`$` are input anchors under default `multiLine: false`; for `"hello world!"` the cursor at position 0 after the empty `*`-match is not at end-of-input so `$` fails. Hoisting to `private static readonly Regex` with `RegexOptions.Compiled | RegexOptions.CultureInvariant` is a recommended performance refinement; an inline ASCII char-range loop is a semantically-equivalent alternative (acceptable codegen optimisation). Idiom: `rf-dart-regex-anchored-char-class-to-csharp-regex-ismatch-cultureinvariant`.

**C11. `_escapeString` chained `replaceAll` → chained `string.Replace` in EXACT order.** ```csharp
return s.Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t");
``` Order load-bearing: backslash-first prevents re-escaping of backslashes inserted by later steps. `String.Replace(string, string)` is ordinal by default per Microsoft Learn — no explicit comparer required. Idiom: `rf-dart-chained-replaceall-to-csharp-chained-replace-ordinal`.

**C12. `import 'ast.dart';` → `using` directive or same-namespace reference.** Maps to `using Glp.Compiler;` (or `using Glp.Compiler.Ast;` if sub-namespace-per-file) per feature-018's directory→namespace mapping. If both files land in the same namespace, no `using` is needed. Idiom: `rf-dart-relative-import-to-csharp-using-or-same-namespace`.

**Required namespace usings** (per convspec `conversion_units`):
- `System.Text` (StringBuilder)
- `System.Text.RegularExpressions` (Regex.IsMatch)
- `System.Linq` (Select)
- `System.Collections.Generic` (List<T>)
- `System.Collections.Frozen` (FrozenSet<string>, .NET 8+)
- `System.Globalization` (CultureInfo.InvariantCulture)
- placeholder for `ast.dart`'s namespace (feature-018 mapping)

**Renames** required to dodge C# keywords / reserved tokens:
- `_printStruct(StructTerm struct)` → `PrintStruct(StructTerm structArg)` (Dart `struct` is fine; C# `struct` is a keyword — parameter MUST be renamed).

## 3. Decomposed Task Units

- **T1.** Class shell — `public sealed class GlpPrinter` with no instance fields. (C1) — done.
- **T2.** Public method `PrintProgram(Program)` — local `StringBuilder`, foreach `program.Procedures`, `Append(PrintProcedure(p)).Append('\n')`, `ToString()`. (C2) — done.
- **T3.** Public method `PrintProcedure(Procedure)` — local `StringBuilder`, foreach `procedure.Clauses`, `Append(PrintClause(c)).Append('\n')`, `ToString()`. (C2) — done.
- **T4.** Public method `PrintClause(Clause)` — head via `PrintAtom`; property-pattern `is { Count: > 0 }` on Guards/Body; three-branch `:-`/`|`/`.` output. (C2, C3) — done.
- **T5.** Public method `PrintAtom(Atom)` — empty-args, infix-arity-2 (no parens), fallthrough. (C6) — done.
- **T6.** Public method `PrintGoal(Goal)` — switch expression with `RemoteGoal`/`SpawnGoal` arms preceding generic-Goal discard fallback. (C5) — done.
- **T7.** Public method `PrintGuard(Guard)` — prefix ternary, empty-args / infix-with-parens (load-bearing) / fallthrough. (C6) — done.
- **T8.** Public method `PrintTerm(Term)` — switch expression with VarTerm/UnderscoreTerm/ConstTerm/ListTerm/StructTerm arms + discard. (C4) — done.
- **T9.** Private method `PrintConstValue(object?)` — switch expression with `when`-guarded string arm, int/double with `CultureInfo.InvariantCulture`, discard fallback. (C9) — done.
- **T10.** Private method `PrintList(ListTerm)` — IsNil shortcut; pattern-variable while-loop traversal; proper-vs-improper output discrimination. (C8) — done.
- **T11.** Private method `PrintStruct(StructTerm structArg)` — comma-functor arity-2 (parenthesised); infix-arity-2 (parenthesised); empty-args; fallthrough. Param rename for C# keyword. (C6, renames) — done.
- **T12.** Static fields `InfixOps` and `InfixGuards` as `FrozenSet<string>` with `StringComparer.Ordinal`. (C7) — done.
- **T13.** Private methods `IsInfixOperator(string)` and `IsInfixGuardOperator(string)` — expression-bodied `=> Set.Contains(x)`. (C7) — done.
- **T14.** Private method `IsAtom(string)` — two `Regex.IsMatch` calls mirroring Dart 1:1 (optionally hoisted to `static readonly Regex` with `Compiled | CultureInvariant`). (C10) — done.
- **T15.** Private method `EscapeString(string)` — chained `string.Replace` in exact order. (C11) — done.
- **T16.** Required `using` directives at file top including placeholder for `ast.dart`'s namespace (C12). — done.
- **T17.** Doc-comments `///` → C# XML-doc `<summary>` 1:1; section banner `//` comments preserved verbatim. — done.

## 4. Research Findings

none required — every construct is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/lib/compiler/glp_printer.dart.md` (sha matches: `5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def`). All ten idiom IDs (`rf-dart-leading-underscore-privacy-to-csharp-private`, `rf-dart-stringbuffer-to-csharp-stringbuilder`, `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`, `rf-dart-is-chain-to-csharp-switch-expression-type-pattern`, `rf-dart-const-set-to-csharp-frozenset-ordinal`, `rf-dart-mutable-local-traversal-loop-with-nullable-pointer-to-csharp-pattern-variable-while`, `rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture`, `rf-dart-regex-anchored-char-class-to-csharp-regex-ismatch-cultureinvariant`, `rf-dart-chained-replaceall-to-csharp-chained-replace-ordinal`, `rf-dart-relative-import-to-csharp-using-or-same-namespace`) are documented in §"Rationale & Research Provenance" of the convspec with cached WebFetch citations to api.dart.dev and learn.microsoft.com.

The escalation-#2 closure note (commit `09757a26`, 2026-05-20) is recorded in convspec construct `dart.regex_anchored_one_char_class_check_via_two_regexp_calls` nuance (1): "ANCHOR SEMANTICS — IMPORTANT, prior analyses had this wrong … The function thus behaves exactly as its docstring says … There is no bug." This plan does not re-raise that point.

## 5. Consistency Pass

All decisions in §2 / §3 are derived directly from the ratified convspec at `.codeconv/conversion-specs/lib/compiler/glp_printer.dart.md` (sha `5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def`):
- C1/T1, C2/T2-T4, C3/T4, C4/T8, C5/T6, C6/T5+T7+T11, C7/T12-T13, C8/T10, C9/T9, C10/T14, C11/T15, C12/T16 — fixed — derived from `.codeconv/conversion-specs/lib/compiler/glp_printer.dart.md` `constructs` list (one construct per axis, target_decision quoted verbatim per construct).
- Param rename `struct` → `structArg` — fixed — derived from convspec `conversion_units` line: "PrintStruct(StructTerm structArg) … (note: param renamed from Dart `struct` since `struct` is a C# keyword)".
- Carry-forward idioms (lexer.dart's `rf-dart-stringbuffer-to-csharp-stringbuilder`, ast.dart's `rf-dart-is-chain-to-csharp-switch-expression-type-pattern` + `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`) — fixed — derived from convspec §"Rationale & Research Provenance" and from the corresponding sibling specs `.codeconv/conversion-specs/lib/compiler/lexer.dart.md` and `.codeconv/conversion-specs/lib/compiler/ast.dart.md`.
- LF-only line terminator refinement (Append('\n') not AppendLine) — fixed — derived from convspec construct `dart.stringbuffer_local_accumulator_with_write_writeln_tostring` target_decision: "The C# spec MANDATES `buffer.Append('\\n')` (NOT `AppendLine`)".
- Discard-arm requirement on switch expressions over non-sealed Term — fixed — derived from convspec construct `dart.type_pattern_switch_via_is_chain_over_sealed_sumtype` nuance (1) and `rf-dart-is-chain-to-csharp-switch-expression-type-pattern` §"Conclusion".
- Arm order for PrintGoal (`RemoteGoal`/`SpawnGoal` before generic fallback) — fixed — derived from convspec construct `dart.type_pattern_switch_via_is_chain_over_sealed_sumtype` nuance (4): "C# WARNS on unreachable arms; emitting the subclass arms BEFORE the fallback is mandatory."
- Infix-guard parenthesisation (`(a OP b)`) load-bearing for round-trip — fixed — derived from convspec construct `dart.conditional_string_prefix_via_bool_field_and_ternary` target_decision: "The parenthesisation around the infix form `(arg0 OP arg1)` is LOAD-BEARING for round-trip … the spec must NEVER strip them."
- `StringComparer.Ordinal` mandated on FrozenSet construction — fixed — derived from convspec construct `dart.const_set_of_string_for_keyword_membership_with_ordinal_compare` target_decision: "EXPLICIT `StringComparer.Ordinal` is mandatory".
- `CultureInfo.InvariantCulture` mandated on int/double `ToString` — fixed — derived from convspec construct `dart.runtime_type_test_via_is_chain_polymorphic_value_with_branching` target_decision: "MUST use `CultureInfo.InvariantCulture`".
- IsAtom faithful regex translation (no bug) — fixed — derived from escalation-#2 closure recorded in commit `09757a26` and convspec construct `dart.regex_anchored_one_char_class_check_via_two_regexp_calls` nuance (1).
- EscapeString chained Replace order load-bearing — fixed — derived from convspec construct `dart.chained_replaceall_for_escape_sequence_application` target_decision: "Map to a chain of `string.Replace(string, string)` calls preserving the EXACT ORDER".
- Namespace mapping deferred to feature-018 directory→namespace policy — fixed — derived from convspec construct `dart.import_relative_to_csharp_namespace_or_using` target_decision: "The conversion-spec records the relationship explicitly so the codegen stage can decide."

## 6. Escalations

None.
