//// glp/mad/message — a pending outgoing madGLP message (feature 050, T050.A0).
////
//// One element of the outgoing message set M_p (spec §6): a pair `(m, q)` where `q`
//// is the destination agent and `m` is an assignment `_w(a,i) := T` or `_r(a,i) := T`.
//// Modeled as the triple (global name, globalized term, destination). The serializer
//// list-wrap `[T↑ | _w(q,0)]` and the globalization of T (which mints W_p entries and
//// spawns `global_send` goals) are performed by the `_send` kernel (T050.A2) — this
//// type carries the already-formed assignment. M_p is `List(Message)` on the MadEngine
//// (T050.A3), drained by the Send transaction (spec §8.2).

import glp/mad/global_name.{type GlobalName}
import glp/runtime/terms.{type Term}

/// A pending outgoing message: assign `term` (the globalized value T↑) to global name
/// `name`, delivered to agent `dest` (spec §6 / §8.2 — the destination is carried in
/// the wire header; the body is the global name + globalized term).
pub type Message {
  Message(name: GlobalName, term: Term, dest: Term)
}

/// Build a pending message `(name := term, dest)`.
pub fn new(name: GlobalName, term: Term, dest: Term) -> Message {
  Message(name, term, dest)
}
