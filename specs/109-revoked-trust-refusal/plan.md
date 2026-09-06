# Implementation Plan: Revoked trust material is refused at load

**Spec**: [spec.md](./spec.md) · **Feature**: `109-revoked-trust-refusal`
**Engineer ruling**: G-03 — compiled-in constant, not a config denylist; current-generation
assertion as a **complement**.

---

## 1 · The seam

One file, one place: `csharp/glp_link/transports/SharedCertMaterial.cs`, in `Load(string certDir)`,
**after** the existing cert/pin consistency check and **before** `return (cert, pin)`.

That position is chosen deliberately and is itself a requirement (FR-007):

- **After** the consistency check, so a mismatched pair is still attributed to the *mismatch* rule,
  not to the generation rule. The two failures are different operator situations and must not be
  conflated.
- **Before** the return, so nothing can obtain a `(cert, pin)` tuple for revoked material — every
  consumer (`LinkEstablish`, `LinkListenKernel`, `StaticMacaroonMaterial`, `LazyQuicComposition`)
  goes through this one call, so guarding here guards all of them without touching any of them.

**No other file changes.** The guard is invisible to correct material by construction.

## 2 · What gets added

```
KnownPins (new, internal to SharedCertMaterial):
  CurrentPin   = "jKMVqlvEL0evFBPw4TWIlEln3TBbXT1u1t072Zp1AlY="     (gen-3)
  RevokedPins  = { "0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=" } (gen-1; private key is PUBLIC)
```

Both are `const` / `static readonly` — **compiled in**, per FR-002. No file is read, no environment
variable is consulted, so there is no empty-config failure mode to reason about.

**Two ordered checks:**

1. **FR-001/FR-003 — explicitly revoked.** If `pin ∈ RevokedPins` → throw, naming the pin, the rule
   (*revoked*), and the remedy. Checked **first**, because "this key is public" is a strictly more
   urgent and more specific statement than "this is not the current generation", and the operator
   must see the more serious one.
2. **FR-004 — not the current generation.** If `pin != CurrentPin` → throw with a **different**
   message naming the rule (*not current*). Catches gen-2, a future gen-4, a partial restore, and
   any compromise nobody has enumerated.

FR-008 (whitespace insensitivity) is **already satisfied** by the existing
`File.ReadAllText(fpPath).Trim()` — the plan does not re-implement it, and a test pins that a
trailing newline cannot smuggle revoked material past the guard.

## 3 · Why a denylist alone would not have been enough

`RevokedPins` can only refuse what somebody already enumerated. It would have caught SHIRAS, because
we measured SHIRAS. It would **not** catch a generation nobody has looked at yet — and the whole
lesson of this incident is that nobody was looking for 25 days.

`CurrentPin` is **fail-closed by construction**: anything that is not exactly the current generation
is refused, whether or not we have heard of it. That is why FR-004 is specified alongside FR-001
rather than as a nicer alternative to it. The denylist buys a *better message* for the one case we
know about; the assertion buys *coverage*.

## 4 · The rotation cost, stated honestly

Hard-coding `CurrentPin` means **a rotation is a code change**, and until every host takes the new
build, hosts on different builds refuse each other.

This is a real cost and it is the right trade for shared, SPKI-pinned material: a rotation *should*
be deliberate, reviewed and simultaneous. The alternative — a config file — reintroduces exactly
the fail-open shape the engineer ruled against. **Recorded here so the next rotation is not
surprised by it**, and carried into `§16.1` of the FTAP as a known consequence rather than
discovered later as a defect.

## 5 · Test plan (FR-009 / SC-004)

New file: `csharp/glp_link.tests/SharedCertMaterialGenerationTests.cs`

Fixtures are **generated in the test**, never checked in — this feature must not add key material
to the repo, and a self-signed throwaway cert is enough because the guard compares pins, not trust
chains. A fixture whose SPKI pin is forced to equal the revoked constant is built by generating a
cert and writing a `.fingerprint` that matches it, then asserting against the *rule*, not the bytes.

| test | proves | control type |
|---|---|---|
| `RevokedPin_IsRefused` | the refusal FIRES on the revoked pin | **positive** |
| `RevokedPin_MessageNamesPinRuleAndRemedy` | SC-003 — all three present in the text | positive |
| `CurrentPin_IsAccepted` | the guard does NOT over-fire on good material | **negative** |
| `UnknownGeneration_IsRefusedAsNotCurrent` | FR-004 catches unenumerated material | positive |
| `RevokedBeatsNotCurrent_OrderIsPinned` | FR-007 — the revoked rule reports first | ordering |
| `ExistingChecksStillFireFirst` | FR-007 — missing pfx / empty pin / mismatch unchanged | regression |
| `TrailingNewline_DoesNotEvadeTheGuard` | FR-008 | positive |

**SC-002 is measured by the existing suite**, not by new tests: the 064 service-box drills load real
material through this exact path, so if the guard over-fires they go red. That is a stronger
negative control than anything self-written, and it is why the drills are re-run as a gate.

## 6 · Out of scope (restated from the spec so the implementer cannot drift)

No rotation. No new key material committed. No git-history rewrite. No change to
`provision/revoked.jsonl` (derived per-device credentials — a different mechanism entirely).
