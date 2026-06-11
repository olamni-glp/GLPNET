/-
  IL/Bytecode Round-Trip Codec — formal gate (feature 029, T022/T023).

  A simplified, self-contained model of the C# `IlCodec` mirroring its essential
  structure: a closed discriminant per opcode, a recursive ground-constant
  sub-encoder, and a length-prefixed program. We prove `decode (encode p) = some p`
  for every program `p` — sorry-free (SC-007 / FR-010), in core Lean (no mathlib).

  Scope (pass bar, D8): the v1 family subset + ground constants
  (null | bool | int | string). Stretch (NOT in the bar): v2 + recursive constants.
  The token alphabet abstracts the C# byte stream; the discriminant tags mirror the
  real `OpcodeDiscriminant`/`ConstantCodec` tables one-for-one in spirit.
-/

namespace IlCodecRoundTrip

/-- The abstract byte/token alphabet (stands in for the C# `byte[]`). -/
inductive Tok where
  | tag : Nat → Tok        -- a discriminant byte
  | bool : Bool → Tok      -- an embedded primitive operand
  | int : Int → Tok
  | str : String → Tok
  | nat : Nat → Tok
  deriving DecidableEq, Repr

/-- Ground constants — the simplified closed whitelist. -/
inductive Const where
  | cnull : Const
  | cbool : Bool → Const
  | cint  : Int → Const
  | cstr  : String → Const
  deriving DecidableEq, Repr

/-- A v1-opcode subset, each with a closed discriminant. -/
inductive Op where
  | commit  : Op                 -- marker
  | proceed : Op                 -- marker
  | label   : String → Op        -- Label marker (carries a name)
  | uconst  : Const → Op         -- UnifyConstant (recursive constant operand)
  | getvar  : Nat → Nat → Op     -- GetVariable (two operand integers)
  deriving DecidableEq, Repr

abbrev Program := List Op

-- ── Encoder ────────────────────────────────────────────────────────────────────

def encConst : Const → List Tok
  | .cnull   => [Tok.tag 0]
  | .cbool b => [Tok.tag 1, Tok.bool b]
  | .cint i  => [Tok.tag 2, Tok.int i]
  | .cstr s  => [Tok.tag 3, Tok.str s]

def encOp : Op → List Tok
  | .commit     => [Tok.tag 10]
  | .proceed    => [Tok.tag 11]
  | .label s    => [Tok.tag 12, Tok.str s]
  | .uconst c   => Tok.tag 13 :: encConst c
  | .getvar v a => [Tok.tag 14, Tok.nat v, Tok.nat a]

def enc : Program → List Tok
  | []        => []
  | op :: ops => encOp op ++ enc ops

/-- Top-level: length-prefixed, mirroring the C# `varint instructionCount` header. -/
def encode (p : Program) : List Tok := Tok.nat p.length :: enc p

-- ── Decoder ────────────────────────────────────────────────────────────────────

def decConst : List Tok → Option (Const × List Tok)
  | Tok.tag 0 :: r              => some (.cnull, r)
  | Tok.tag 1 :: Tok.bool b :: r => some (.cbool b, r)
  | Tok.tag 2 :: Tok.int i :: r  => some (.cint i, r)
  | Tok.tag 3 :: Tok.str s :: r  => some (.cstr s, r)
  | _                          => none

def decOp : List Tok → Option (Op × List Tok)
  | Tok.tag 10 :: r                       => some (.commit, r)
  | Tok.tag 11 :: r                       => some (.proceed, r)
  | Tok.tag 12 :: Tok.str s :: r          => some (.label s, r)
  | Tok.tag 13 :: r                       =>
      match decConst r with
      | some (c, r') => some (.uconst c, r')
      | none         => none
  | Tok.tag 14 :: Tok.nat v :: Tok.nat a :: r => some (.getvar v a, r)
  | _                                     => none

/-- Decode exactly `n` opcodes, returning the tail. -/
def decN : Nat → List Tok → Option (Program × List Tok)
  | 0,     ts => some ([], ts)
  | n + 1, ts =>
      match decOp ts with
      | some (op, ts') =>
          match decN n ts' with
          | some (ops, ts'') => some (op :: ops, ts'')
          | none             => none
      | none => none

def decode : List Tok → Option Program
  | Tok.nat k :: ts =>
      match decN k ts with
      | some (p, []) => some p
      | _            => none
  | _ => none

-- ── Round-trip soundness ────────────────────────────────────────────────────────

theorem decConst_enc (c : Const) (r : List Tok) :
    decConst (encConst c ++ r) = some (c, r) := by
  cases c <;> rfl

theorem decOp_enc (op : Op) (r : List Tok) :
    decOp (encOp op ++ r) = some (op, r) := by
  cases op with
  | uconst c => simp [encOp, decOp, decConst_enc c r]
  | _ => rfl

theorem decN_enc (p : Program) (r : List Tok) :
    decN p.length (enc p ++ r) = some (p, r) := by
  induction p generalizing r with
  | nil => rfl
  | cons op ops ih =>
      simp only [enc, List.append_assoc, decN,
                 decOp_enc op (enc ops ++ r), ih r]

/-- The formal gate (SC-007): `decode ∘ encode = id`, sorry-free. -/
theorem decode_encode (p : Program) : decode (encode p) = some p := by
  have h : decN p.length (enc p) = some (p, []) := by
    simpa using decN_enc p []
  simp [encode, decode, h]

end IlCodecRoundTrip
