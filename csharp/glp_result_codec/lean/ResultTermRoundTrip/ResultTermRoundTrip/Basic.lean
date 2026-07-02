/-
  Result-Envelope Term Sub-Codec — formal gate (feature 038, T042, OPTIONAL).

  A simplified, self-contained model of the C#/Dart/Gleam `TermCodec` term sub-codec,
  mirroring feature 029's `IlCodecRoundTrip`. We prove `decode (encode e) = some e` for
  every ground-term sequence `e` — sorry-free, in core Lean (no mathlib), so it checks in
  a single fast `lake build` (mirrors 029 SC-007 / FR-010).

  Scope (pass bar, mirrors 029 D8): the FLAT ground-term whitelist —
  int (0x02) | string (0x04) | atom (0x05) | var→writer (0x07). Stretch, NOT in the bar
  (exactly as 029 excluded recursive constants from its bar): the recursive struct (0x06)
  and the gated float (0x03). The token alphabet abstracts the codec byte stream; the
  discriminant tags mirror the real `TermCodec` tag table (contract §3) one-for-one.
-/

namespace ResultTermRoundTrip

/-- The abstract byte/token alphabet (stands in for the codec `byte[]`/`BitArray`). -/
inductive Tok where
  | tag : Nat → Tok        -- a term tag byte
  | int : Int → Tok        -- an embedded int64 operand
  | str : String → Tok     -- an embedded (len+UTF-8) string operand
  | nat : Nat → Tok        -- a varint count
  deriving DecidableEq, Repr

/-- Ground terms — the simplified flat whitelist (tags mirror contract §3). -/
inductive Term where
  | tint  : Int → Term            -- 0x02 int64
  | tstr  : String → Term         -- 0x04 string
  | tatom : String → Term         -- 0x05 atom
  | tvar  : String → Int → Term   -- 0x07 var→writer: GlobalVarId (agentId, localId)
  deriving DecidableEq, Repr

/-- A resolved-binding term sequence; encoded order IS the parity invariant. -/
abbrev Envelope := List Term

-- ── Encoder ────────────────────────────────────────────────────────────────────

def encTerm : Term → List Tok
  | .tint i    => [Tok.tag 2, Tok.int i]
  | .tstr s    => [Tok.tag 4, Tok.str s]
  | .tatom a   => [Tok.tag 5, Tok.str a]
  | .tvar ag l => [Tok.tag 7, Tok.str ag, Tok.int l]

def enc : Envelope → List Tok
  | []      => []
  | t :: ts => encTerm t ++ enc ts

/-- Top-level: length-prefixed, mirroring the codec `varint bindingsCount` header. -/
def encode (e : Envelope) : List Tok := Tok.nat e.length :: enc e

-- ── Decoder ────────────────────────────────────────────────────────────────────

def decTerm : List Tok → Option (Term × List Tok)
  | Tok.tag 2 :: Tok.int i :: r               => some (.tint i, r)
  | Tok.tag 4 :: Tok.str s :: r               => some (.tstr s, r)
  | Tok.tag 5 :: Tok.str a :: r               => some (.tatom a, r)
  | Tok.tag 7 :: Tok.str ag :: Tok.int l :: r => some (.tvar ag l, r)
  | _                                         => none

/-- Decode exactly `n` terms, returning the tail (mirrors the codec count loop). -/
def decN : Nat → List Tok → Option (Envelope × List Tok)
  | 0,     ts => some ([], ts)
  | n + 1, ts =>
      match decTerm ts with
      | some (t, ts') =>
          match decN n ts' with
          | some (rest, ts'') => some (t :: rest, ts'')
          | none              => none
      | none => none

def decode : List Tok → Option Envelope
  | Tok.nat k :: ts =>
      match decN k ts with
      | some (e, []) => some e
      | _            => none
  | _ => none

-- ── Round-trip soundness ────────────────────────────────────────────────────────

theorem decTerm_enc (t : Term) (r : List Tok) :
    decTerm (encTerm t ++ r) = some (t, r) := by
  cases t <;> rfl

theorem decN_enc (e : Envelope) (r : List Tok) :
    decN e.length (enc e ++ r) = some (e, r) := by
  induction e generalizing r with
  | nil => rfl
  | cons t ts ih =>
      simp only [enc, List.append_assoc, decN,
                 decTerm_enc t (enc ts ++ r), ih r]

/-- The formal gate: `decode ∘ encode = id`, sorry-free. -/
theorem decode_encode (e : Envelope) : decode (encode e) = some e := by
  have h : decN e.length (enc e) = some (e, []) := by
    simpa using decN_enc e []
  simp [encode, decode, h]

end ResultTermRoundTrip
