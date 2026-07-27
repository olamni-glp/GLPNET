//// glp/engine/module_runtime — the module-RPC registry (feature 059, WP T078
//// `close-module-system-runtime-rpc`).
////
//// The runtime state the `Distribute`/`Transmit` opcodes and the `_activate/2`
//// body kernel operate on — the Gleam analogue of the Dart
//// `GlpRuntime.glpChannels` (`glp_runtime/lib/runtime/runtime.dart:31`) plus the
//// per-module `ReplModuleContext.imports`. It is threaded through `reduce` exactly
//// like `MadState` (T050) and `LinkRuntime` (T074): carried as an `Option` on the
//// `RunnerContext`, read back off the `Reduced` outcome, and advanced by a
//// `step_module` scheduler variant.
////
//// Faithful dynamic dispatch (§19.4/§19.7, spec-note-module-rpc-runtime.md; Gabi
//// ruling 2026-07-27): a module-qualified `M # goal(...)` call routes the goal —
//// as an ordinary GLP term — onto the target module's **channel stream**, waking
//// the module's suspended `serve/2` loop, which dispatches it via `_activate/2` to
//// the exported procedure over the shared heap arg-cells (so output args flow back
//// on the same addresses). This module holds only the registry data; the channel
//// send + the label dispatch live in the runner (no new dynamic-spawn primitive —
//// `_activate` reuses the existing `SpawnReq` path, per the ruling).

import gleam/dict.{type Dict}

/// The module-RPC registry: the module NAME → its channel writer address (the
/// heap-resident stream tail its `serve/2` loop reads). A channel send binds the
/// current writer to a cons cell `[goal | freshTail]` and advances the writer; the
/// write wakes the suspended `serve/2` goal. Populated by `activate` on load.
///
/// (The `Distribute` import table `index → name` lives on the compiled
/// `BytecodeProgram` — `program.import_name` — not here; this registry holds only
/// the dynamic activation state, the Gleam analogue of Dart `glpChannels`.)
pub type ModuleRuntime {
  ModuleRuntime(channels: Dict(String, Int))
}

/// An empty registry (no modules activated).
pub fn new() -> ModuleRuntime {
  ModuleRuntime(channels: dict.new())
}

/// Register an activated module: its `name` now routes to the channel stream whose
/// tail writer is at `writer_addr`. Idempotent by name (re-activation overwrites,
/// mirroring Dart `activateDynamicModule`'s `containsKey` guard semantics — the
/// caller only activates a not-yet-present module).
pub fn activate(
  runtime: ModuleRuntime,
  name: String,
  writer_addr: Int,
) -> ModuleRuntime {
  ModuleRuntime(channels: dict.insert(runtime.channels, name, writer_addr))
}

/// Advance the channel writer for `name` after a `send` extended its stream (the
/// new tail writer address). No-op if the module is not registered.
pub fn set_channel(
  runtime: ModuleRuntime,
  name: String,
  writer_addr: Int,
) -> ModuleRuntime {
  case dict.has_key(runtime.channels, name) {
    True -> ModuleRuntime(channels: dict.insert(runtime.channels, name, writer_addr))
    False -> runtime
  }
}

/// The channel writer address for an activated module, or `Error(Nil)` if the
/// module was never activated (the runner surfaces this LOUDLY — Dart prints
/// "module … not activated (no GLP channel)", never a silent drop).
pub fn channel(runtime: ModuleRuntime, name: String) -> Result(Int, Nil) {
  dict.get(runtime.channels, name)
}

/// Whether any module is activated (the scheduler uses this, like the link pump's
/// activity check, to decide whether a `step_module` pass is even needed).
pub fn is_empty(runtime: ModuleRuntime) -> Bool {
  dict.is_empty(runtime.channels)
}
