---
path: lib/engine/claude_adapter.dart
cycle_group_id: 61
scc_siblings: []
generated_at: 2026-05-21T14:37:00Z
source_sha256: 6726b917ebcadfa2ec9bd6ce512327e28cd1b84bc8d6f096b8a788490bfafec9
schema_version: 1
---

# Conversion Plan: lib/engine/claude_adapter.dart

## 1. Source Analysis

The file `lib/engine/claude_adapter.dart` is a 5-line Dart placeholder skeleton with a single load-bearing element:

- **Line 1-2 (triple-slash doc-comment)**: `/// Adapter skeleton: will bridge GLP's argument-indexed ops to Claude's VM. /// No behavior yet — placeholder for wiring tests later.` — provenance describing the file's purpose as an adapter skeleton awaiting future wiring.
- **Line 3 (class declaration)**: `class ClaudeEngineAdapter {` — Dart top-level class, identifier lacking a leading underscore (library-public visibility by Dart convention).
- **Line 4 (constructor)**: `const ClaudeEngineAdapter();` — `const` zero-argument default constructor. The class body is otherwise empty (no fields, no methods, no other members).
- **Line 5 (close brace)**: `}` — closes the class.

Dependencies: none (per tombstone `dependencies: []`).
Callers: none (per tombstone `callers: []`).
Topo-level: 0. This is a leaf node with no inbound or outbound edges in the depgraph — it is a singleton skeleton.

The only observable behaviour of the `const` constructor on a zero-state class is reference identity: two `const ClaudeEngineAdapter()` expressions in the same compilation refer to the same canonical instance (Dart `const` instance canonicalisation).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the RATIFIED convspec construct-by-construct.

- **Triple-slash doc-comment** → C# XML doc-comment (`///`) on the .NET type, preserving the text verbatim (including the em-dash) so the "adapter skeleton / no behavior yet / placeholder for wiring tests later" provenance survives the port.
- **`class ClaudeEngineAdapter`** (Dart top-level reference type) → `public class ClaudeEngineAdapter` in a .NET namespace mirroring `lib/engine/` per the workspace's pair-specific namespace convention. The class is a **reference type** (`class`), NOT a `struct` or `record struct` — Dart class instances are heap reference objects with identity, and any future wiring may compare instances by reference; a value-type translation would silently change identity semantics.
- **`const ClaudeEngineAdapter();`** (Dart `const` zero-argument default constructor) → no explicit constructor in the .NET counterpart; the C# compiler synthesises an implicit `public` parameterless constructor, which matches the Dart default constructor's zero-argument shape. The `const` modifier is intentionally **dropped at the construct level** — .NET has no compile-time-canonicalised instance constructor (`const` in .NET is reserved for compile-time-constant fields of primitive/string types per the .NET language reference). Identity-canonicalisation semantics are **deferred to per-callsite decisions** in downstream consumer files (recorded in their specs, not here): each Dart `const ClaudeEngineAdapter()` call site maps either to (a) a `static readonly` singleton field initialised once when the original call site is itself a compile-time-constant Dart context, or (b) a fresh `new ClaudeEngineAdapter()` when it isn't.
- **Visibility** → Dart top-level identifier without a leading underscore is library-public; the .NET counterpart is **`public`** (the .NET default `internal` would narrow visibility relative to Dart and break cross-file consumers).
- **Empty class body** → empty class body in .NET (no fields, no methods, no explicit ctor). Synthesised parameterless ctor only.
- **Null-safety** → no fields, so no field-level null-safety choices arise. The type itself is non-nullable under an enabled nullable context.
- **Async / Stream / isolate / sealed / mixin** → ABSENT from the source; correctly not asserted in the target.

Conversion units (mirroring convspec):
- Namespace declaration mirroring `lib/engine/` per the workspace's pair-specific namespace convention.
- `public class ClaudeEngineAdapter` (empty body; synthesised implicit parameterless constructor; verbatim XML-doc comment).

## 3. Decomposed Task Units

- **T1 — Emit namespace declaration.** Definition of done: target file `lib/engine/claude_adapter.cs` opens with a `namespace` declaration mirroring `lib/engine/` per the workspace's pair-specific namespace convention.
- **T2 — Emit XML doc-comment.** Definition of done: a `///`-prefixed XML doc-comment block precedes the type, preserving both lines of the original Dart `///` provenance verbatim (em-dash included).
- **T3 — Emit `public class ClaudeEngineAdapter` with empty body.** Definition of done: a `public class ClaudeEngineAdapter { }` declaration exists with no fields, no methods, and no explicit constructor (compiler-synthesised implicit `public` parameterless ctor only).
- **T4 — Verify reference-type choice.** Definition of done: the emitted type is a `class` (not `struct`/`record struct`), confirming identity semantics are preserved.
- **T5 — Verify `const` modifier dropped.** Definition of done: no `const` modifier appears on the synthesised constructor or anywhere in the emitted file; canonicalisation is left to per-callsite decisions in consumer specs.

## 4. Research Findings

None required. The convspec's `rf-dart-const-ctor-vs-csharp-default-ctor` research finding (verbatim citations from `dart.dev/language/constructors` and `learn.microsoft.com` class / constructors references) is mirrored in §2 and fully resolves every decision. The single documented semantic gap (Dart `const` instance constructor → no .NET equivalent) has a deterministic resolution (drop at construct level, defer canonicalisation to per-callsite decisions in consumers).

## 5. Consistency Pass

- §2 vs §3: each conversion unit in §2 (namespace, XML doc, empty `public class`, dropped `const`, reference-type choice) has a corresponding decomposed task in §3 (T1, T2, T3, T5, T4 respectively). No drift.
- §2 vs convspec: §2 mirrors the convspec's two `conversion_units` entries verbatim and preserves all four convspec nuance bullets (value-vs-reference, `const` constructor semantics, null-safety, async/stream/isolate/sealed/mixin absent). No drift.
- §3 vs convspec: T1–T5 cover the convspec's `conversion_units` plus the verification gates required by the convspec's nuance section (reference-type, `const`-dropped). No drift.
- §4 vs convspec: convspec's `rf-dart-const-ctor-vs-csharp-default-ctor` is acknowledged; no additional research required (FR-013 escalation criterion — undecidability — does not apply because every decision is derivable from authoritative documentation cited in the convspec).
- §2 vs CLAUDE.md / contracts: the namespace convention defers to "the workspace's pair-specific namespace convention" exactly as the convspec does — neither artifact pins a concrete namespace string, both correctly delegate to the langpair registry. Visibility-default contract (`public` to match Dart top-level) preserved. No drift.
- Tombstone `dependencies: []` and `callers: []` are consistent with the singleton-leaf shape used throughout §§1–3. No drift.

No gaps. No escalations required.

## 6. Escalations

None.
