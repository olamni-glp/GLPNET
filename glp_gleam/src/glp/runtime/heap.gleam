//// glp/runtime/heap — the immutable threaded binding store (FCP two-cell heap).
////
//// R-001: the WAM mutable heap is re-expressed as an IMMUTABLE value threaded through
//// deref/bind/unify; every mutating op returns a new `Heap`. FCP bidirectional
//// writer/reader pairs; a variable's role is read from the cell tag ONLY (FR-002),
//// never address arithmetic. WxW and double-bind are reported loudly as `HeapError`,
//// never silent (R-005).
////
//// Dart source of truth: glp_runtime/lib/runtime/heap_fcp.dart

import gleam/dict.{type Dict}
import gleam/list
import gleam/set.{type Set}
import glp/runtime/suspension.{type GoalRef, type Suspension, GoalRef}
import glp/runtime/terms.{type Term}

/// A heap cell — the typed union IS the tag (no separate stored tag field; F2 vs Dart's
/// `dynamic content` + stored `CellTag`). `WriterCell` is an unbound writer that
/// preserves its paired reader and any attached suspensions (← Dart `WriterContent`,
/// heap_fcp.dart:48); `WriterBound` is a writer bound onward to another variable's
/// reader (the FCP `Pointer` chain, still writer-tagged); `ReaderCell` points to its
/// paired writer; `ValueCell` is bound to a ground value.
pub type Cell {
  WriterCell(reader_addr: Int, suspensions: List(Suspension))
  WriterBound(target: Int)
  ReaderCell(writer_addr: Int)
  ValueCell(term: Term)
}

/// Cell tag — DERIVED from the constructor (single source of truth), never stored. The
/// Dart needed a separate `tag` field because `content` was `dynamic`; Gleam's typed
/// union already encodes it, so a stored tag would be a second source that can drift.
pub type CellTag {
  WrtTag
  RoTag
  ValueTag
}

/// Derive the FCP cell tag from the cell constructor.
pub fn tag(cell: Cell) -> CellTag {
  case cell {
    WriterCell(_, _) -> WrtTag
    WriterBound(_) -> WrtTag
    ReaderCell(_) -> RoTag
    ValueCell(_) -> ValueTag
  }
}

/// The result of dereferencing a reference: a ground value, or the unbound writer the
/// chain ends at (← Dart `derefAddr` returning a `Term` or a `VarRef`).
pub type DerefResult {
  Bound(term: Term)
  Unbound(writer: Int)
}

/// A structural-invariant violation — kept DISTINCT from a normal unify `Fail` (R-005)
/// so SC-003 (truth table) and SC-004 (zero silent WxW) never conflate them. Mirrors the
/// Dart `StateError` cases (heap_fcp.dart:266,274,366,455).
pub type HeapError {
  WriterToWriter(w1: Int, w2: Int)
  AlreadyBound(addr: Int)
  NotAWriter(addr: Int)
  Cycle(addr: Int)
}

/// The immutable binding store: an indexed cell store + the next-free address (HP).
/// Internal representation is opaque and EXCLUDED from parity (only observable outcomes
/// are pinned — FR-009 / Clarification 2026-06-25).
pub opaque type Heap {
  Heap(cells: Dict(Int, Cell), hp: Int)
}

/// An empty heap (hp = 0).
pub fn new() -> Heap {
  Heap(cells: dict.new(), hp: 0)
}

/// Allocate a fresh local logic variable. Returns `#(heap', writer_addr, reader_addr)`.
/// Writer cell points to its paired reader; reader cell points back (FCP bidirectional).
/// The `+1` adjacency is an allocation convenience only — role is read from the tag,
/// never assumed by callers (FR-002). Mirrors Dart `allocateVariable`.
pub fn allocate_variable(heap: Heap) -> #(Heap, Int, Int) {
  let writer_addr = heap.hp
  let reader_addr = heap.hp + 1
  let cells =
    heap.cells
    |> dict.insert(writer_addr, WriterCell(reader_addr:, suspensions: []))
    |> dict.insert(reader_addr, ReaderCell(writer_addr:))
  #(Heap(cells: cells, hp: heap.hp + 2), writer_addr, reader_addr)
}

/// The paired reader address of a writer (← Dart `pairedReaderAddr`,
/// heap_fcp.dart:236). An unbound `WriterCell` carries its reader; for a
/// bound/non-writer cell, fall back to the allocation adjacency `writer + 1`
/// exactly as the reference does (heap_fcp.dart:242). Role stays tag-derived —
/// this reads the stored pairing, never assumes it (FR-002).
pub fn paired_reader(heap: Heap, writer: Int) -> Int {
  case dict.get(heap.cells, writer) {
    Ok(WriterCell(reader_addr, _)) -> reader_addr
    _ -> writer + 1
  }
}

/// The paired writer of a reader, if it is a local reader (← Dart
/// `tryWriterForReader`, heap_fcp.dart:181). A `ReaderCell` points back to its
/// writer; anything else yields `Error(Nil)` (the Dart returns null — e.g. an
/// imported reader, which the local Gleam foundation does not have).
pub fn paired_writer(heap: Heap, reader: Int) -> Result(Int, Nil) {
  case dict.get(heap.cells, reader) {
    Ok(ReaderCell(writer_addr)) -> Ok(writer_addr)
    _ -> Error(Nil)
  }
}

fn has_tag(heap: Heap, addr: Int, want: CellTag) -> Bool {
  case dict.get(heap.cells, addr) {
    Ok(cell) -> tag(cell) == want
    Error(_) -> False
  }
}

/// Is `addr` a writer cell? Role read from the cell tag only (FR-002).
pub fn is_writer(heap: Heap, addr: Int) -> Bool {
  has_tag(heap, addr, WrtTag)
}

/// Is `addr` a reader cell? Role read from the cell tag only (FR-002).
pub fn is_reader(heap: Heap, addr: Int) -> Bool {
  has_tag(heap, addr, RoTag)
}

/// Is `addr` a value cell (bound to ground)? Role read from the cell tag only (FR-002).
pub fn is_value(heap: Heap, addr: Int) -> Bool {
  has_tag(heap, addr, ValueTag)
}

/// Fetch a cell. Addresses are always valid in correct use; an invalid address is a
/// caller bug — mirrors the Dart `cells[addr]` RangeError (kept off the `HeapError`
/// channel, which is reserved for the four defined structural verdicts).
fn cell_at(heap: Heap, addr: Int) -> Cell {
  let assert Ok(cell) = dict.get(heap.cells, addr)
  cell
}

fn set_cell(heap: Heap, addr: Int, cell: Cell) -> Heap {
  Heap(..heap, cells: dict.insert(heap.cells, addr, cell))
}

// ==============================================================================
// Dereferencing (US1 · T011) — ← heap_fcp.dart derefAddr
// ==============================================================================

/// Dereference `addr`, following the reader→writer→(value|unbound) chain with PATH
/// COMPRESSION threaded into the returned heap (R-006): a subsequent `deref` of the same
/// address on the returned heap reaches a fixpoint in O(1) (SC-002). Cycle / writer→writer
/// traversal are reported loudly (FR-003/FR-004; ← Dart StateError, heap_fcp.dart:266,274).
pub fn deref(heap: Heap, addr: Int) -> Result(#(Heap, DerefResult), HeapError) {
  deref_walk(heap, addr, set.new(), [], False, -1)
}

fn deref_walk(
  heap: Heap,
  current: Int,
  visited: Set(Int),
  readers: List(Int),
  prev_was_writer: Bool,
  prev_addr: Int,
) -> Result(#(Heap, DerefResult), HeapError) {
  case set.contains(visited, current) {
    True -> Error(Cycle(current))
    False -> {
      let visited = set.insert(visited, current)
      let cell = cell_at(heap, current)
      // WxW during traversal: followed from a writer and landed on a writer (FR-004).
      case prev_was_writer && tag(cell) == WrtTag {
        True -> Error(WriterToWriter(prev_addr, current))
        False ->
          case cell {
            ReaderCell(writer_addr) ->
              deref_walk(
                heap,
                writer_addr,
                visited,
                [current, ..readers],
                False,
                current,
              )
            WriterBound(target) ->
              // A writer bound onward to its OWN paired reader is a self-reference the Dart
              // `derefAddr` treats as STILL UNBOUND (the bidirectional recognizer,
              // heap_fcp.dart:312-323), NOT a cycle. Mirror that observable outcome: yield
              // Unbound(current). A genuine multi-hop pointer cycle still trips the
              // visited-set guard at the top of deref_walk.
              case cell_at(heap, target) {
                ReaderCell(back) if back == current ->
                  Ok(#(compress(heap, readers, current), Unbound(current)))
                _ -> deref_walk(heap, target, visited, readers, True, current)
              }
            WriterCell(_, _) ->
              Ok(#(compress(heap, readers, current), Unbound(current)))
            ValueCell(term) ->
              Ok(#(compress(heap, readers, current), Bound(term)))
          }
      }
    }
  }
}

/// Shortcut every reader cell traversed so it points straight at the terminal address
/// (the unbound writer, or the value cell). Same logical value, shorter chain — read-only
/// safe (spec Edge Case). Internal layout, excluded from parity (FR-009).
fn compress(heap: Heap, readers: List(Int), target_addr: Int) -> Heap {
  let cells =
    list.fold(readers, heap.cells, fn(cs, r) {
      dict.insert(cs, r, ReaderCell(target_addr))
    })
  Heap(..heap, cells: cells)
}

// ==============================================================================
// Binding (US1 · T012/T013) — ← heap_fcp.dart bindWriter / bindWriterToReader
// ==============================================================================

/// Bind an unbound writer to a ground value → `ValueCell` (FR-005). Single-assignment:
/// a value/var-bound writer is `AlreadyBound`; a non-writer is `NotAWriter`. Returns the
/// activation list — `[]` until suspension production lands in US3 (T022).
pub fn bind_writer(
  heap: Heap,
  writer: Int,
  value: Term,
) -> Result(#(Heap, List(GoalRef)), HeapError) {
  case cell_at(heap, writer) {
    WriterCell(_, suspensions) ->
      Ok(#(set_cell(heap, writer, ValueCell(value)), activate(suspensions)))
    ValueCell(_) -> Error(AlreadyBound(writer))
    WriterBound(_) -> Error(AlreadyBound(writer))
    ReaderCell(_) -> Error(NotAWriter(writer))
  }
}

/// Bind an unbound writer onward to another variable's reader → `WriterBound` chain
/// (FR-006). Binding to a writer target is a WxW violation (FR-004). Armed suspensions on
/// the writer are forwarded to the target chain's TERMINAL unbound writer (FR-008); returns
/// `[]` (nothing fires until that terminal itself binds a value).
pub fn bind_writer_to_var(
  heap: Heap,
  writer: Int,
  reader: Int,
) -> Result(#(Heap, List(GoalRef)), HeapError) {
  case cell_at(heap, writer) {
    WriterCell(_, suspensions) ->
      case cell_at(heap, reader) {
        ReaderCell(_immediate_writer) ->
          forward_to_terminal(
            heap,
            writer,
            reader,
            list.filter(suspensions, fn(s) { s.armed }),
          )
        WriterCell(_, _) | WriterBound(_) ->
          Error(WriterToWriter(writer, reader))
        ValueCell(_) -> Error(NotAWriter(reader))
      }
    ValueCell(_) -> Error(AlreadyBound(writer))
    WriterBound(_) -> Error(AlreadyBound(writer))
    ReaderCell(_) -> Error(NotAWriter(writer))
  }
}

/// Bind `writer` onward to `reader` and forward `armed` suspensions to the target chain's
/// TERMINAL unbound writer (FR-008) — not merely the reader's IMMEDIATE paired writer, which
/// would silently drop them when that writer is already bound onward (`WriterBound`). The
/// terminal is resolved by `deref` on the pre-binding heap (which also path-compresses the
/// target chain). Reachable via `unify` when the bound writer carries a suspension.
fn forward_to_terminal(
  heap: Heap,
  writer: Int,
  reader: Int,
  armed: List(Suspension),
) -> Result(#(Heap, List(GoalRef)), HeapError) {
  case armed {
    [] -> Ok(#(set_cell(heap, writer, WriterBound(reader)), []))
    _ ->
      case deref(heap, reader) {
        Error(e) -> Error(e)
        Ok(#(heap, Unbound(terminal))) ->
          Ok(
            #(
              forward_suspensions(
                set_cell(heap, writer, WriterBound(reader)),
                terminal,
                armed,
              ),
              [],
            ),
          )
        // Target chain already resolves to a ground value — not the intended use of
        // bind_writer_to_var (its precondition is an unbound reader); record the binding,
        // fire nothing (the verdict carries no activation here).
        Ok(#(heap, Bound(_))) ->
          Ok(#(set_cell(heap, writer, WriterBound(reader)), []))
      }
  }
}

// ==============================================================================
// Suspension storage + activation (US3 · T021/T022)
// — ← heap_fcp.dart suspendOnWriter / _walkAndActivate / _forwardSuspensions
// ==============================================================================

/// Record a suspension on an unbound writer, preserving the reader pairing (FR-008; ←
/// `suspendOnWriter`, prepend). `NotAWriter` on a non-unbound-writer cell.
pub fn suspend_on_writer(
  heap: Heap,
  writer: Int,
  susp: Suspension,
) -> Result(Heap, HeapError) {
  case cell_at(heap, writer) {
    WriterCell(reader_addr, suspensions) ->
      Ok(set_cell(heap, writer, WriterCell(reader_addr, [susp, ..suspensions])))
    _ -> Error(NotAWriter(writer))
  }
}

/// Armed suspensions → one `GoalRef` each, in list order (newest-first); disarmed records
/// are skipped (the double-activation guard — ← `_walkAndActivate` + `disarm`). The cell is
/// replaced by a `ValueCell` by the caller, so the activated records are not re-fired.
fn activate(suspensions: List(Suspension)) -> List(GoalRef) {
  suspensions
  |> list.filter(fn(s) { s.armed })
  |> list.map(fn(s) { GoalRef(s.goal_id, s.resume_pc) })
}

/// Forward suspensions onto a target writer's list (← `_forwardSuspensions`); a bound /
/// non-writer target is ignored (faithful to the Dart, which forwards only onto a writer).
fn forward_suspensions(
  heap: Heap,
  target_writer: Int,
  susps: List(Suspension),
) -> Heap {
  case susps {
    [] -> heap
    _ ->
      case cell_at(heap, target_writer) {
        WriterCell(reader_addr, existing) ->
          set_cell(
            heap,
            target_writer,
            WriterCell(reader_addr, list.append(susps, existing)),
          )
        _ -> heap
      }
  }
}
