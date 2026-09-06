#!/usr/bin/env python3
"""Build FLEET-T24 SUPERSET v4.1 from v4.0 by surgical insertion only.

Adds engineer directive item [13] YYBeacon (Yachad Beacon), which arrived after v4.0.
Same discipline as the v4.0 build: every v4.0 line must survive, asserted.
"""
import os

BASE = r"D:\BSTDEV\research\glp\GLPNET\docs\fleet"
SCRATCH = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(BASE, "FLEET-T24-ACTION-PLAN-SUPERSET-v4.0.md")
DST = os.path.join(BASE, "FLEET-T24-ACTION-PLAN-SUPERSET-v4.1.md")

v4 = open(SRC, encoding="utf-8").read()
row13 = open(os.path.join(SCRATCH, "t24_row13.md"), encoding="utf-8").read().rstrip("\n")

out = v4
before = len(v4)

# --- 1. header ------------------------------------------------------------
A1 = "    TEMPLATE VERSION   v4.0 — v3.0 plus the engineer directive of 2026-09-06"
assert out.count(A1) == 1, "A1"
NEW = """    TEMPLATE VERSION   v4.1 — v4.0 plus engineer directive item [13] YYBeacon (Yachad
                       Beacon), which arrived after v4.0 was built. NOTHING REMOVED FROM v4.0.
    AMENDED BY         olamnit-glpnet @ OLAMNIT, 2026-09-06T21:05Z
    AMENDMENT METHOD   surgical insertion only — one new objective row (37), one ordering note
                       in §4.5, and the matching Annex B / §13 entries. Asserted line-for-line.
    v4.0 LINEAGE       v4.0 — v3.0 plus the engineer directive of 2026-09-06"""
out = out.replace(A1, NEW, 1)

# --- 2. row 37, before the (renumbered) placeholder row --------------------
A2 = "| 37 | `{{OBJ_ID}}` |"
assert out.count(A2) == 1, "A2"
out = out.replace(A2, row13 + "\n| 38 | `{{OBJ_ID}}` |", 1)

# --- 3. ordering note in §4.5 (two precise substitutions, no line splitting) -----
A3a = "(rows 30–35).**"
assert out.count(A3a) == 1, "A3a"
out = out.replace(A3a, "(rows 30–35, 37).**", 1)

A3b = [ln for ln in out.splitlines() if "Three windows now run concurrently" in ln][0]
assert out.count(A3b) == 1, "A3b"
BEACON_NOTE = "\n".join([
    "\U0001F534 **Row 37 (`YYBeacon`) is LAST in the 72-hour chain, and that is a requirement,",
    "not a preference.** Its defining obligation is to **show the progress and status of every one",
    "of `[01]`–`[12]`**. A beacon built before those twelve expose a status surface can only",
    "show hand-entered content, which is a dashboard of claims — the exact defect this fleet's",
    "audit work exists to remove. **Build the status surfaces first; the beacon reads them.**",
    "",
    "",
])
out = out.replace(A3b, BEACON_NOTE + A3b, 1)

# --- 4. Annex B / §13 -----------------------------------------------------
A4 = "| **OBJ-LEADER-PLANNER** | §4 row 36 + §4.6 (in full) | engineer directive, LEADER+PLANNER block |"
assert out.count(A4) == 1, "A4"
out = out.replace(A4, A4 + "\n| **OBJ-YYBEACON** (72 h) | §4 row 37 + §4.5 ordering note | engineer directive `[13]` |", 1)

# --- assertions -----------------------------------------------------------
assert len(out) > before
DECLARED = {
    A1,
    "| 37 | `{{OBJ_ID}}` | `{{OBJECTIVE}}` | `{{OWNER}}` | `{{YES/NO}}` | `{{ACCEPTANCE}}` | `{{ACK}}` |",
    # deliberate: the 72-hour window now also contains row 37
    "(rows 30–35).** A period that closes with any 48-hour or 72-hour row untouched is **on the failure",
}
missing = [ln for ln in v4.splitlines()
           if ln.strip() and ln not in out and ln not in DECLARED]
# the §4.5 anchor spans two lines; both are re-emitted with row 37 added, so allow them
missing = missing
assert not missing, "DROPPED %d v4.0 line(s), first: %r" % (len(missing), missing[:1])
for must in ("OBJ-YYBEACON", "Yachad Beacon", "| 38 | `{{OBJ_ID}}` |", "rows 30–35, 37"):
    assert must in out, "missing: %r" % must

open(DST, "w", encoding="utf-8").write(out)
print("v4.0 lines: %d -> v4.1 lines: %d (+%d chars)"
      % (len(v4.splitlines()), len(out.splitlines()), len(out) - before))
print("dropped v4.0 lines: 0 (asserted)")
print("wrote", DST)
