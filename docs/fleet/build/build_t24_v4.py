#!/usr/bin/env python3
"""Build FLEET-T24 SUPERSET v4.0 from v3.0 by surgical insertion only.

Every v3.0 byte must survive. The script asserts on every anchor, so a silent
drop is impossible -- the same discipline v3.0 used to build itself from v2.0.
"""
import sys, os, re

BASE = r"D:\BSTDEV\research\glp\GLPNET\docs\fleet"
SCRATCH = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(BASE, "FLEET-T24-ACTION-PLAN-SUPERSET-v3.0.md")
DST = os.path.join(BASE, "FLEET-T24-ACTION-PLAN-SUPERSET-v4.0.md")

v3 = open(SRC, encoding="utf-8").read()
rows = open(os.path.join(SCRATCH, "t24_v4_rows.md"), encoding="utf-8").read().rstrip("\n")
sections = open(os.path.join(SCRATCH, "t24_v4_sections.md"), encoding="utf-8").read().rstrip("\n")

# Split the two new sections file into its parts, keyed by heading.
def part(text, start_head, end_head=None):
    i = text.index(start_head)
    j = text.index(end_head) if end_head else len(text)
    return text[i:j].rstrip("\n")

p_45_46 = part(sections, "### 4.5 ", "### 2.8 ")
p_28    = part(sections, "### 2.8 ", "### 3.9 ")
p_39    = part(sections, "### 3.9 ")

before = len(v3)
out = v3

# --- 1. Header block -------------------------------------------------------
A1 = """    TEMPLATE VERSION   v3.0 — v2.0 plus the engineer directive of 2026-09-05T13:00Z"""
assert out.count(A1) == 1, "anchor A1"
NEW_HDR = """    TEMPLATE VERSION   v4.0 — v3.0 plus the engineer directive of 2026-09-06
                       (the nine remaining programme MVPs [04]-[12], the LEADER+PLANNER
                       programme, the automatic-failure criteria, and the fleetwide-action
                       stake). NOTHING REMOVED FROM v3.0.
    AMENDED BY         olamnit-glpnet @ OLAMNIT, 2026-09-06T20:30Z
    AMENDMENT METHOD   surgical insertion only — ten new objective rows (27-36), three new
                       sub-sections (4.5, 4.6, 2.8), one new scoring sub-section (3.9), one
                       row-numbering defect fixed, and the matching Annex B / §13 entries.
                       Every v3.0 byte is preserved; this build script asserts on every
                       anchor so a silent drop is impossible.
    v3.0 LINEAGE       v3.0 — v2.0 plus the engineer directive of 2026-09-05T13:00Z"""
out = out.replace(A1, NEW_HDR, 1)

# --- 2. Objective rows 27-36, inserted before the {{OBJ_ID}} placeholder row
A2 = "| 22 | `{{OBJ_ID}}` |"
assert out.count(A2) == 1, "anchor A2 (placeholder row)"
# v3.0 defect: the placeholder row is numbered 22, colliding with OBJ-MAILBOX-CONTAINER.
out = out.replace(A2, rows + "\n| 37 | `{{OBJ_ID}}` |", 1)

# --- 3. §4.5 + §4.6 before §4.3's closing "All of the above" line ----------
A3 = "\n**All of the above is critical, urgent, imperative and mandatory.**"
assert out.count(A3) == 1, "anchor A3"
out = out.replace(A3, "\n" + p_45_46 + "\n" + A3, 1)

# --- 4. §2.8 after the §2.7 mailbox correction, before §2A ------------------
A4 = "\n## §2A — PRECEDENCE, CONFLICT AND REFUSAL"
assert out.count(A4) == 1, "anchor A4"
out = out.replace(A4, "\n" + p_28 + "\n\n---\n" + A4, 1)

# --- 5. §3.9 before the end of §3 (anchor on the §4 heading) ---------------
A5 = "\n## §4 — OBJECTIVE REGISTER FOR THIS PERIOD"
assert out.count(A5) == 1, "anchor A5"
out = out.replace(A5, "\n" + p_39 + "\n\n---\n" + A5, 1)

# --- 6. §13 adaptation-log entry -------------------------------------------
A6 = "| **OBJ-YSTORE** (48 h) | §4 row 23 | engineer directive `[01]` |"
assert out.count(A6) == 1, "anchor A6"
NEW_LOG = A6 + """
| **OBJ-YNTERCHANGE** (48 h) | §4 row 27 | engineer directive `[04]` |
| **OBJ-YMAP** (48 h) | §4 row 28 | engineer directive `[05]` |
| **OBJ-YGUARD** (48 h) | §4 row 29 | engineer directive `[06]` |
| **OBJ-YENGAGE** (72 h) | §4 row 30 | engineer directive `[07]` |
| **OBJ-YBUILD** (72 h) | §4 row 31 | engineer directive `[08]` |
| **OBJ-YWORK** (72 h) | §4 row 32 | engineer directive `[09]` |
| **OBJ-YRECON** (72 h) | §4 row 33 | engineer directive `[10]` |
| **OBJ-YANALYZE** (72 h) | §4 row 34 | engineer directive `[11]` |
| **OBJ-YHIVE** (72 h) | §4 row 35 | engineer directive `[12]` |
| **OBJ-LEADER-PLANNER** | §4 row 36 + §4.6 (in full) | engineer directive, LEADER+PLANNER block |
| The 72-hour window and its dependency chain | §4.5 | engineer directive, 72 h framing |
| Automatic-failure criteria AF-1..AF-6 | §2.8 | engineer directive, "AUTOMATIC FAILURE INCLUDES" |
| Fleetwide-action stake (x10 / +10M; zero / -1M) and the collaboration mandate | §3.9 | engineer directive, reward+penalty and "NEVER TO say ... too big for me" |
| `ynetd.py` `--term` default fix — recorded CLOSED on OLAMNIT, four verbs not one | §4.6 status box | measured by `olamnit-glpnet` 2026-09-06T20:00Z; landed by `@olamnit-yngwin` |"""
out = out.replace(A6, NEW_LOG, 1)

# --- assertions: nothing lost ---------------------------------------------
assert len(out) > before, "output must be longer than input"
# Every v3.0 line must still be present, EXCEPT the two this amendment deliberately
# rewords. They are enumerated here so the exception is declared and reviewable rather
# than the check being loosened -- an unenumerated drop still fails the build.
DECLARED_EDITS = {
    # the version line: superseded by the v4.0 header, and its text is preserved
    # verbatim in the new "v3.0 LINEAGE" line
    "    TEMPLATE VERSION   v3.0 — v2.0 plus the engineer directive of 2026-09-05T13:00Z",
    # v3.0 DEFECT FIXED: the fill-in placeholder row was numbered 22, colliding with
    # row 22 OBJ-MAILBOX-CONTAINER. Renumbered to 37. No content lost.
    "| 22 | `{{OBJ_ID}}` | `{{OBJECTIVE}}` | `{{OWNER}}` | `{{YES/NO}}` | `{{ACCEPTANCE}}` | `{{ACK}}` |",
}
missing = [ln for ln in v3.splitlines()
           if ln.strip() and ln not in out and ln not in DECLARED_EDITS]
assert not missing, "DROPPED %d v3.0 line(s), first: %r" % (len(missing), missing[:1])
# and prove each declared edit really was applied, not silently absent from v3.0
for ln in DECLARED_EDITS:
    assert ln in v3, "declared edit not present in v3.0 -- stale exception: %r" % ln[:60]
assert "v3.0 LINEAGE       v3.0 — v2.0 plus the engineer directive of 2026-09-05T13:00Z" in out, \
    "the reworded version line must be preserved verbatim in the lineage line"
assert "| 37 | `{{OBJ_ID}}` |" in out, "renumbered placeholder row missing"

for must in ("OBJ-YNTERCHANGE", "OBJ-YMAP", "OBJ-YGUARD", "OBJ-YENGAGE", "OBJ-YBUILD",
             "OBJ-YWORK", "OBJ-YRECON", "OBJ-YANALYZE", "OBJ-YHIVE", "OBJ-LEADER-PLANNER",
             "### 4.5 ", "### 4.6 ", "### 2.8 ", "### 3.9 ", "AF-6", "LeaderPing",
             "Intent ∖ Outcome", "DIFFERENTIAL ORACLE", "bk-planner"):
    assert must in out, "missing inserted content: %r" % must

open(DST, "w", encoding="utf-8").write(out)
print("v3.0 lines : %d" % len(v3.splitlines()))
print("v4.0 lines : %d" % len(out.splitlines()))
print("v3.0 chars : %d -> v4.0 chars: %d  (+%d)" % (before, len(out), len(out) - before))
print("dropped v3.0 lines: 0  (asserted)")
print("wrote %s" % DST)
