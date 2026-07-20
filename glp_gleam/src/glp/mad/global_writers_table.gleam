//// glp/mad/global_writers_table — the immutable per-agent global writers table W_p
//// (feature 050, T050.A0).
////
//// Faithful port of glp_runtime/lib/multiagent/global_writers_table.dart, made
//// IMMUTABLE — every op returns a NEW table (the whole Gleam engine's discipline;
//// mirrors glp/runtime/suspension.SuspensionTable). Tracks the local writers that
//// AWAIT incoming assignments from remote agents (spec §3): an entry exists exactly
//// while this agent is the RECEIVER of a link. Two entry kinds:
//// - GlobalizeEntry (X,q): created by Globalize of a writer; looked up by this
////   agent's OWN index i (the entry at index i is the global name `_w(owner,i)`).
//// - LocalizeEntry (X,q,i): created by Localize of a reader name `_r(q,i)`; found by
////   SEARCHING (remote_agent q, remote_index i).
////
//// Index 0 is the permanent network-input SERIALIZER entry (spec §4.1): updated on
//// each cold-call (`N := [T | N']`), NEVER removed. A single per-agent counter
//// allocates indices from 1 and NEVER reuses them, even after removal (spec §3.2 /
//// §13 Index Uniqueness).

import gleam/dict.{type Dict}
import gleam/list
import gleam/option.{type Option, None, Some}
import glp/runtime/terms.{type Term}

/// A W_p entry (spec §3.1). `remote_agent` is the ground agent-id term (the Dart used a
/// bare String; a Term is faithful to spec §2's `AgentId ::= String ; Integer ; peer(...)`).
pub type Entry {
  /// Globalize of a writer Y → `(Y, q)`; direct lookup by this agent's own index i.
  GlobalizeEntry(writer_addr: Int, remote_agent: Term)
  /// Localize of a reader `_r(q,i)` → `(Z, q, i)`; found by (remote_agent, remote_index).
  LocalizeEntry(writer_addr: Int, remote_agent: Term, remote_index: Int)
}

/// The immutable global writers table W_p of one agent. Opaque — every op returns a
/// new value. `owner` is the agent p that owns it (its GlobalizeEntries name
/// `_w(owner, i)`).
pub opaque type WritersTable {
  WritersTable(
    owner: Term,
    globalize: Dict(Int, Entry),
    localize: List(Entry),
    serializer: Option(Int),
    next_index: Int,
  )
}

/// A fresh table for `owner`: no serializer yet, counter at 1 (index 0 is reserved for
/// the serializer — spec §3.2).
pub fn new(owner: Term) -> WritersTable {
  WritersTable(
    owner: owner,
    globalize: dict.new(),
    localize: [],
    serializer: None,
    next_index: 1,
  )
}

/// The agent p that owns this table.
pub fn owner(t: WritersTable) -> Term {
  t.owner
}

// ---- serializer (index 0) — permanent network-input entry (spec §4.1) --------------

/// Install the permanent serializer entry at index 0, mapping to the network-input
/// stream writer `net_in_writer_addr`. Calling twice is a caller bug (Dart throws
/// StateError) → panic ("robustness is a workaround in disguise").
pub fn init_serializer(t: WritersTable, net_in_writer_addr: Int) -> WritersTable {
  case t.serializer {
    Some(_) ->
      panic as "global_writers_table: serializer entry already initialized"
    None -> WritersTable(..t, serializer: Some(net_in_writer_addr))
  }
}

/// The current serializer writer addr (None before boot init).
pub fn serializer_addr(t: WritersTable) -> Option(Int) {
  t.serializer
}

/// Advance the serializer to a fresh writer after a cold-call extends the network input
/// stream (spec §8.3: `N_q := [T↓ | N'_q]`, entry updated to `(N'_q, *)`). Updating
/// before init is a caller bug → panic.
pub fn update_serializer(t: WritersTable, new_writer_addr: Int) -> WritersTable {
  case t.serializer {
    None -> panic as "global_writers_table: cannot update serializer before init"
    Some(_) -> WritersTable(..t, serializer: Some(new_writer_addr))
  }
}

// ---- globalize-writer entries (direct index lookup) --------------------------------

/// Allocate the next index i, store `(writer_addr, remote_agent)` at i, and return the
/// new table + i (spec §5.1 writer branch). The entry's global name is `_w(owner, i)`.
pub fn add_globalize(
  t: WritersTable,
  writer_addr: Int,
  remote_agent: Term,
) -> #(WritersTable, Int) {
  let i = t.next_index
  #(
    WritersTable(
      ..t,
      globalize: dict.insert(
        t.globalize,
        i,
        GlobalizeEntry(writer_addr, remote_agent),
      ),
      next_index: i + 1,
    ),
    i,
  )
}

/// Allocate the next index WITHOUT creating an entry (spec §5.1 reader branch — the
/// outgoing `_r(owner,i)` link is handled by a spawned `global_send`, no entry).
pub fn allocate_index(t: WritersTable) -> #(WritersTable, Int) {
  let i = t.next_index
  #(WritersTable(..t, next_index: i + 1), i)
}

/// Look up a GlobalizeEntry by this agent's own index (Receive of `_w(owner, i)`).
pub fn lookup(t: WritersTable, index: Int) -> Result(Entry, Nil) {
  dict.get(t.globalize, index)
}

/// Remove a GlobalizeEntry after its assignment arrives (spec §8.3). Index 0 is the
/// permanent serializer — never removed (spec §4.1); a no-op there. Idempotent.
pub fn remove_globalize(t: WritersTable, index: Int) -> WritersTable {
  case index {
    0 -> t
    _ -> WritersTable(..t, globalize: dict.delete(t.globalize, index))
  }
}

// ---- localize-reader entries (search by remote agent + index) ----------------------

/// Add a LocalizeEntry `(writer_addr, remote_agent, remote_index)` (spec §5.2 `_r`
/// branch). `Error(Nil)` if an entry for (remote_agent, remote_index) already exists
/// (spec §13 Entry Lifecycle: created exactly once).
pub fn add_localize(
  t: WritersTable,
  writer_addr: Int,
  remote_agent: Term,
  remote_index: Int,
) -> Result(WritersTable, Nil) {
  case find_localize(t, remote_agent, remote_index) {
    Ok(_) -> Error(Nil)
    Error(Nil) ->
      Ok(
        WritersTable(..t, localize: [
          LocalizeEntry(writer_addr, remote_agent, remote_index),
          ..t.localize
        ]),
      )
  }
}

/// Find the LocalizeEntry matching (remote_agent, remote_index) — Receive of `_r(q, i)`.
pub fn find_localize(
  t: WritersTable,
  remote_agent: Term,
  remote_index: Int,
) -> Result(Entry, Nil) {
  list.find(t.localize, fn(e) {
    case e {
      LocalizeEntry(_, a, i) -> a == remote_agent && i == remote_index
      _ -> False
    }
  })
}

/// Remove the LocalizeEntry for (remote_agent, remote_index) after its assignment
/// arrives (spec §8.3). Idempotent.
pub fn remove_localize(
  t: WritersTable,
  remote_agent: Term,
  remote_index: Int,
) -> WritersTable {
  WritersTable(
    ..t,
    localize: list.filter(t.localize, fn(e) {
      case e {
        LocalizeEntry(_, a, i) -> a != remote_agent || i != remote_index
        _ -> True
      }
    }),
  )
}

// ---- counts / inspection -----------------------------------------------------------

/// The next index the counter would allocate (spec §3.2; never decreases).
pub fn next_index(t: WritersTable) -> Int {
  t.next_index
}

/// Number of live GlobalizeEntries.
pub fn globalize_count(t: WritersTable) -> Int {
  dict.size(t.globalize)
}

/// Number of live LocalizeEntries.
pub fn localize_count(t: WritersTable) -> Int {
  list.length(t.localize)
}
