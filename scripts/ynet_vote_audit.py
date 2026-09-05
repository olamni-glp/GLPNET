#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""Audit YNET oplog vote records and tally them on the VERIFIED franchise.

Why this exists — and what its first version got WRONG
------------------------------------------------------
v1 of this tool (2026-09-05T13:40Z) required ``actor == voter`` and treated every
disagreement as a defect. **That rule was wrong, and it was wrong in the
direction that mattered: it refused every genuine delegated vote in the fleet.**

``gavriella.ospark`` refuted it at 08:49Z with a cryptographic proof, which this
tool now reproduces independently rather than accepting on trust:

* ``voter_sig`` does NOT cover the outer ``signing_bytes`` envelope. It covers a
  smaller, declared field set — ``VOTER_SIGNED_FIELDS`` below — which is exactly
  why a naive verification against the envelope fails and looks like forgery.
* ``sha256(voter_spki)[:32] == voter``. **The voter id IS the hash of the key
  that signed the delegation**, so no forger can claim another host's franchise
  with a key of their own.

Measured over the live oplog: **every** vote carrying a proof verifies, and
**every** one is correctly key-bound. So ``actor != voter`` is a genuine
delegation, and a vote with no ``voter`` field at all is simply a direct vote.
Neither is a defect. v1's central claim was a false alarm.

The rule this tool now applies
------------------------------
    franchise(vote) =
        actor                       if no delegation proof is present  (direct)
        verified voter              if a proof is present AND it verifies
                                       AND sha256(voter_spki)[:32] == voter
        REFUSED                     if a proof is present and does NOT verify

**A bad proof is refused, never silently downgraded to the actor** — falling back
would let a forger strip a failing signature and vote as themselves.

The electorate is then **hosts**, resolved through each franchise's ``hello``,
because quorum was ruled to be "4 host oracles, not 15 lanes"
(``RULINGS-20260905T0050Z-shiras-hatzinor``).

What it still flags, because these ARE real
-------------------------------------------
=====  =========================================================================
 F1    a delegation proof that does not verify, or is not key-bound
 F2    a franchise with no ``hello`` anywhere (cannot be admitted)
 F3    ONE HOST casting votes for DIFFERENT candidates in one term — measured
       live in term 1, where two distinct ``shiras`` node ids each self-voted
 F4    one franchise submitting more than once in a term (benign only while all
       its submissions name the same candidate — dedupe, then say so)
 F5    a term spanning more than one ``roster_epoch``
 F6    one franchise naming DIFFERENT candidates in a term. The franchise is
       EXCLUDED from every candidate, never tie-broken: counting the first, the
       last, or the smaller would be a silent choice that quietly favours a
       candidate. F3 is a ROSTER problem (one host, many node ids); F6 is an
       EMITTER problem (one identity said two things). Reporting them as one
       finding would route one of them to the wrong owner.
=====  =========================================================================

Exit codes
----------
``0``  no fatal finding — the tally is well-defined
``1``  at least one ``FATAL_FINDINGS`` entry (F1/F2/F3/F6): each of these changes
       who wins, or who may be counted at all. F4 and F5 do not, and do not gate.
``2``  usage / unreadable oplog / ``cryptography`` unavailable

**Exit 0 is a claim, not a default.** An empty oplog exits 2, never 0. If
``cryptography`` is missing this tool exits **2** rather than reporting an
unverified tally, because an under-counted quorum reported as a real one is the
exact failure this tool exists to prevent.

Usage
-----
    python scripts/ynet_vote_audit.py [--oplog D:/coop/ynet/oplog] [--term N]
                                      [--json] [--self-test]
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import pathlib
import sys
import tempfile

# The voter signature covers THIS field set, not the outer envelope. Getting this
# wrong is what made a genuine delegation look unverifiable to three lanes.
VOTER_SIGNED_FIELDS = ("kind", "term", "candidate", "actor", "ts", "voter")

FINDING_TEXT = {
    "F1": "delegation proof does not verify or is not key-bound",
    "F2": "franchise has no hello record — cannot be admitted",
    "F3": "one host voted for different candidates in this term",
    "F4": "one franchise submitted more than once in this term",
    "F5": "term spans more than one roster_epoch",
    "F6": "one franchise named DIFFERENT candidates in this term — excluded, not tie-broken",
}

# F1/F2/F3/F6 change who wins, or who may be counted, so they set a non-zero exit.
# F4 (a repeat that agrees with itself) and F5 (a schema-migration artifact) do not.
FATAL_FINDINGS = ("F1", "F2", "F3", "F6")


def _canonical(d: dict) -> bytes:
    return json.dumps(d, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def load_oplog(root: pathlib.Path):
    """Return (votes, hellos-by-node-id), deduped by record_id.

    The oplog carries genuine byte-duplicates of the same record_id across node
    files; counting those separately would inflate a tally, so dedupe is part of
    reading rather than an option.
    """
    votes: list[dict] = []
    hellos: dict = collections.defaultdict(list)
    seen: set = set()
    for f in sorted(root.glob("*.jsonl")):
        for lineno, line in enumerate(f.read_text(encoding="utf-8").splitlines(), 1):
            line = line.strip()
            if not line:
                continue
            try:
                o = json.loads(line)
            except json.JSONDecodeError:
                votes.append({"__unparseable__": f"{f.name}:{lineno}", "term": None})
                continue
            rid = o.get("record_id")
            if rid is not None:
                if rid in seen:
                    continue
                seen.add(rid)
            kind = o.get("kind") or o.get("op")
            if kind == "vote":
                votes.append(o)
            elif kind == "hello":
                hellos[o.get("node_id")].append(o)
    return votes, hellos


def resolve_franchise(v: dict):
    """Return (franchise_id | None, how). None means the vote is REFUSED."""
    from cryptography.hazmat.primitives.serialization import load_der_public_key

    spki, sig, voter = v.get("voter_spki"), v.get("voter_sig"), v.get("voter")
    if not (spki and sig and voter):
        return v.get("actor"), "direct"
    try:
        load_der_public_key(bytes.fromhex(spki)).verify(
            bytes.fromhex(sig), _canonical({k: v.get(k) for k in VOTER_SIGNED_FIELDS})
        )
    except Exception:
        # Never fall back to the actor: that would let a forger strip a failing
        # signature and have the vote counted as their own.
        return None, "REFUSED-signature"
    if hashlib.sha256(bytes.fromhex(spki)).hexdigest()[:32] != voter:
        return None, "REFUSED-not-key-bound"
    return voter, "delegated"


def audit(root: pathlib.Path, only_term):
    votes, hellos = load_oplog(root)
    if not votes:
        return {"error": f"no vote records found under {root}"}, 2

    def host_of(nid):
        h = hellos.get(nid)
        return (h[0].get("host") or "").lower() or None if h else None

    def lane_of(nid):
        h = hellos.get(nid)
        return h[0].get("lane") if h else None

    by_term = collections.defaultdict(list)
    for v in votes:
        by_term[v.get("term")].append(v)

    report = {"oplog": str(root), "terms": []}
    worst = 0
    for term in sorted(by_term, key=lambda t: (t is None, t)):
        if only_term is not None and term != only_term:
            continue
        recs = by_term[term]
        row = {"term": term, "votes": len(recs), "records": [], "findings": [],
               "tally_hosts": {}, "elected": None}

        epochs = {v.get("roster_epoch") for v in recs if "__unparseable__" not in v}
        row["roster_epochs"] = sorted(str(e) for e in epochs)
        if len(epochs) > 1:
            row["findings"].append(("F5", f"epochs {row['roster_epochs']}"))

        # candidate -> host -> [(franchise, ts)]
        tally = collections.defaultdict(lambda: collections.defaultdict(list))
        # franchise -> candidate -> [ts]  (FR-008: a franchise must name ONE candidate per term)
        by_franchise = collections.defaultdict(lambda: collections.defaultdict(list))
        for v in sorted(recs, key=lambda x: x.get("ts", "")):
            if "__unparseable__" in v:
                row["records"].append({"unparseable": v["__unparseable__"]})
                row["findings"].append(("F1", f"unparseable at {v['__unparseable__']}"))
                worst = max(worst, 1)
                continue
            fr, how = resolve_franchise(v)
            host = host_of(fr) if fr else None
            rec = {"ts": v.get("ts"), "actor": (v.get("actor") or "")[:12],
                   "franchise": (fr or "")[:12] or None, "how": how,
                   "host": host, "lane": lane_of(fr) if fr else None,
                   "candidate": (v.get("candidate") or "")[:12]}
            row["records"].append(rec)
            if fr is None:
                row["findings"].append(("F1", f"{v.get('ts')} {how}"))
                worst = max(worst, 1)
                continue
            if host is None:
                row["findings"].append(("F2", f"{v.get('ts')} franchise {fr[:12]}"))
                worst = max(worst, 1)
                continue
            tally[v.get("candidate")][host].append(((fr or "")[:12], v.get("ts")))
            by_franchise[fr][v.get("candidate")].append(v.get("ts"))

        # FR-008 / F6: a franchise that named more than one candidate is EXCLUDED from every
        # candidate, not tie-broken. Counting the first, the last, or the lexicographically
        # smaller would be a silent choice, which the specification forbids in as many words -
        # and any of those rules would quietly favour a candidate.
        for fr, cands in by_franchise.items():
            if len(cands) < 2:
                continue
            detail = "; ".join(
                f"{(c or '?')[:12]} at {', '.join(ts)}" for c, ts in sorted(cands.items(), key=lambda kv: str(kv[0]))
            )
            row["findings"].append(
                ("F6", f"franchise {(fr or '?')[:12]} named {len(cands)} candidates: {detail} "
                       f"— EXCLUDED from all of them")
            )
            worst = max(worst, 1)
            # Remove every submission this franchise made, from every candidate and host bucket.
            for cand in list(tally):
                for host in list(tally[cand]):
                    tally[cand][host] = [e for e in tally[cand][host] if e[0] != (fr or "")[:12]]
                    if not tally[cand][host]:
                        del tally[cand][host]
                if not tally[cand]:
                    del tally[cand]

        # F4: a franchise submitting twice for the same candidate is deduped, and said so.
        for cand, hosts in tally.items():
            for host, entries in hosts.items():
                if len(entries) > 1:
                    row["findings"].append(
                        ("F4", f"host {host} submitted {len(entries)}x for {str(cand)[:12]} "
                               f"({', '.join(t for _, t in entries)}) — deduped to 1")
                    )
        # F3: one host backing two different candidates in one term.
        host_cands = collections.defaultdict(set)
        for cand, hosts in tally.items():
            for host in hosts:
                host_cands[host].add(cand)
        for host, cands in host_cands.items():
            if len(cands) > 1:
                row["findings"].append(
                    ("F3", f"host {host} voted for {len(cands)} candidates: "
                           f"{sorted(str(c)[:12] for c in cands)}")
                )
                worst = max(worst, 1)

        row["tally_hosts"] = {(c or "?")[:12]: sorted(h) for c, h in tally.items()}
        # Derive the exit status from the findings themselves rather than setting it at each
        # emit site: a new finding code added without touching this line would otherwise default
        # to non-fatal silently, which is how a check stops gating without anyone noticing.
        if any(code in FATAL_FINDINGS for code, _ in row["findings"]):
            worst = max(worst, 1)
        report["terms"].append(row)

    if not report["terms"]:
        return {"error": "no records for the requested term"}, 2
    return report, worst


def render(report: dict, quorum: int) -> None:
    if "error" in report:
        print(f"ynet_vote_audit: {report['error']}", file=sys.stderr)
        return
    print(f"oplog: {report['oplog']}   quorum: {quorum} hosts")
    for t in report["terms"]:
        print()
        print(f"=== TERM {t['term']} — {t['votes']} vote record(s) ===")
        for r in t["records"]:
            if r.get("unparseable"):
                print(f"    UNPARSEABLE at {r['unparseable']}")
                continue
            print(f"    {r['ts']}  actor={r['actor']}  ->  franchise={r['franchise'] or 'REFUSED':<12}"
                  f" ({r['how']})  host={r['host']}  lane={r['lane']}  cand={r['candidate']}")
        print("  TALLY on the verified franchise, deduped by host:")
        for cand, hosts in sorted(t["tally_hosts"].items(), key=lambda kv: -len(kv[1])):
            mark = "  <-- QUORUM MET" if len(hosts) >= quorum else ""
            print(f"    {cand}: {len(hosts)} host(s) {hosts}{mark}")
        if t["findings"]:
            print("  FINDINGS:")
            for code, detail in t["findings"]:
                print(f"    {code} {FINDING_TEXT[code]}: {detail}")
        else:
            print("  FINDINGS: none")


# --- positive control -------------------------------------------------------

def _fixture(tmp: pathlib.Path):
    """Build a fixture with real Ed25519 keys so the verify path is exercised."""
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

    def mk():
        k = Ed25519PrivateKey.generate()
        spki = k.public_key().public_bytes(Encoding.DER, PublicFormat.SubjectPublicKeyInfo).hex()
        return k, spki, hashlib.sha256(bytes.fromhex(spki)).hexdigest()[:32]

    kA, spkiA, idA = mk()      # host H, delegates to submitter S
    kB, spkiB, idB = mk()      # host K, direct voter
    recs = [
        {"kind": "hello", "node_id": idA, "record_id": "hA", "host": "H", "lane": "h.a"},
        {"kind": "hello", "node_id": idB, "record_id": "hB", "host": "K", "lane": "k.b"},
        {"kind": "hello", "node_id": "subm", "record_id": "hS", "host": "H", "lane": "h.s"},
        {"kind": "hello", "node_id": "cand", "record_id": "hC", "host": "C", "lane": "c.c"},
    ]

    def delegated(rid, ts, term, cand, actor, voter_key, voter_spki, voter_id, corrupt=False):
        v = {"kind": "vote", "record_id": rid, "ts": ts, "term": term,
             "candidate": cand, "actor": actor, "voter": voter_id, "voter_spki": voter_spki}
        sig = voter_key.sign(_canonical({k: v.get(k) for k in VOTER_SIGNED_FIELDS}))
        v["voter_sig"] = (b"\x00" * 64 if corrupt else sig).hex()
        return v

    kC, spkiC, idC = mk()      # host M, a franchise that CONFLICTS with itself (T001)
    kD, spkiD, idD = mk()      # host N, a franchise that REPEATS itself benignly (T003)
    # Host P holds TWO node ids that back DIFFERENT candidates. This is the true F3 shape - the
    # live term-1 `shiras` case - and it is NOT a franchise conflict: each franchise names exactly
    # one candidate, so only the host-level grouping can see it. The first draft of this fixture
    # used ONE franchise naming two candidates, which F6 correctly claimed instead, leaving F3
    # untested.
    _, _, idE = mk()
    _, _, idF = mk()
    recs += [
        {"kind": "hello", "node_id": idC, "record_id": "hC2", "host": "M", "lane": "m.c"},
        {"kind": "hello", "node_id": idD, "record_id": "hD", "host": "N", "lane": "n.d"},
        {"kind": "hello", "node_id": idE, "record_id": "hE", "host": "P", "lane": "p.e"},
        {"kind": "hello", "node_id": idF, "record_id": "hF", "host": "P", "lane": "p.f"},
    ]

    # 1 valid delegation, 1 valid direct, 1 forged delegation, 1 unknown franchise,
    # and a host backing two candidates.
    recs += [
        delegated("v1", "T1", 7, "cand", "subm", kA, spkiA, idA),
        {"kind": "vote", "record_id": "v2", "ts": "T2", "term": 7, "candidate": "cand", "actor": idB},
        delegated("v3", "T3", 7, "cand", "subm", kA, spkiA, idA, corrupt=True),
        {"kind": "vote", "record_id": "v4", "ts": "T4", "term": 7, "candidate": "cand", "actor": "ghost"},
        # F3: host P backs two candidates through TWO DISTINCT franchises. Neither franchise
        # conflicts with itself, so F6 must NOT claim this - only the host grouping sees it.
        {"kind": "vote", "record_id": "v5a", "ts": "T5a", "term": 7, "candidate": "cand", "actor": idE},
        {"kind": "vote", "record_id": "v5b", "ts": "T5b", "term": 7, "candidate": "other", "actor": idF},
        # T001 (FR-008): ONE franchise, ONE term, TWO DIFFERENT candidates. Host M holds no other
        # franchise, so F3's host grouping cannot see this — it is the case F6 exists for.
        {"kind": "vote", "record_id": "v6", "ts": "T6", "term": 7, "candidate": "cand", "actor": idC},
        {"kind": "vote", "record_id": "v7", "ts": "T7", "term": 7, "candidate": "other", "actor": idC},
        # T003 (FR-007): the NEGATIVE control — one franchise, twice, SAME candidate. This must
        # produce F4 and NOT F6, or "always report a conflict" would satisfy T002.
        {"kind": "vote", "record_id": "v8", "ts": "T8", "term": 7, "candidate": "cand", "actor": idD},
        {"kind": "vote", "record_id": "v9", "ts": "T9", "term": 7, "candidate": "cand", "actor": idD},
    ]
    (tmp / "fx.jsonl").write_text("\n".join(json.dumps(r) for r in recs) + "\n", encoding="utf-8")
    return {"conflicting": idC, "repeating": idD}


def self_test() -> int:
    with tempfile.TemporaryDirectory() as td:
        root = pathlib.Path(td)
        ids = _fixture(root)
        report, code = audit(root, None)
        t = report["terms"][0]
        codes = [c for c, _ in t["findings"]]
        recs = {r["ts"]: r for r in t["records"] if not r.get("unparseable")}

        fails = []
        # --- T002 (FR-008): a franchise naming two candidates must be REPORTED and EXCLUDED ---
        if "F6" not in codes:
            fails.append("F6 did not fire on a franchise naming two candidates in one term")
        else:
            f6 = " ".join(d for c, d in t["findings"] if c == "F6")
            if ids["conflicting"][:12] not in f6:
                fails.append("F6 fired but did not name the conflicting franchise")
        # and it must contribute to NEITHER candidate — excluding, not tie-breaking
        for cand, hosts in t["tally_hosts"].items():
            if "m" in hosts:
                fails.append(f"conflicted franchise still counted for {cand}: excluded means excluded")
        # --- T003 (FR-007): the NEGATIVE control. A benign repeat is F4, never F6. ---
        f6_all = " ".join(d for c, d in t["findings"] if c == "F6")
        if ids["repeating"][:12] in f6_all:
            fails.append("F6 fired on a BENIGN repeat (same candidate twice) — it must be F4 only")
        f4_all = " ".join(d for c, d in t["findings"] if c == "F4")
        if "n" not in f4_all:
            fails.append("F4 did not fire on the benign same-candidate repeat")
        if [h for c, hosts in t["tally_hosts"].items() for h in hosts if h == "n"].count("n") != 1:
            fails.append("the benign repeat did not deduplicate to exactly one host vote")

        if recs["T1"]["how"] != "delegated" or recs["T1"]["host"] != "h":
            fails.append("T1: a VALID delegation must resolve to the delegating host")
        if recs["T2"]["how"] != "direct":
            fails.append("T2: a vote with no proof must resolve as a DIRECT vote")
        if recs["T3"]["franchise"] is not None:
            fails.append("T3: a FORGED delegation must be REFUSED, never downgraded to the actor")
        if "F1" not in codes:
            fails.append("F1 did not fire on the forged delegation")
        if "F2" not in codes:
            fails.append("F2 did not fire on the franchise with no hello")
        if "F3" not in codes:
            fails.append("F3 did not fire on the host backing two candidates")
        if code == 0:
            fails.append("audit() returned 0 on a fixture with three real findings")

    if fails:
        for f in fails:
            print(f"SELF-TEST FAIL: {f}", file=sys.stderr)
        return 1
    print("self-test: PASS — a valid delegation is COUNTED, a forged one is REFUSED (not "
          "downgraded), a franchise naming two candidates is EXCLUDED not tie-broken, a benign "
          "repeat is F4 and NOT F6, and F1/F2/F3/F6 all fire")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--oplog", type=pathlib.Path, default=pathlib.Path("D:/coop/ynet/oplog"))
    ap.add_argument("--term", type=int, default=None)
    ap.add_argument("--quorum", type=int, default=3, help="hosts required (4-host fleet, f=1)")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    try:
        import cryptography  # noqa: F401
    except ImportError:
        # An unverified tally under-counts delegations and would report a real
        # quorum as absent — the exact failure this tool exists to prevent.
        print("ynet_vote_audit: the 'cryptography' package is REQUIRED — refusing to "
              "report an unverified tally (pip install cryptography)", file=sys.stderr)
        return 2

    if args.self_test:
        return self_test()

    if not args.oplog.is_dir():
        print(f"ynet_vote_audit: no such oplog: {args.oplog}", file=sys.stderr)
        return 2

    report, code = audit(args.oplog, args.term)
    if args.json:
        print(json.dumps(report, indent=2, sort_keys=True, default=str))
    else:
        render(report, args.quorum)
    if "error" not in report:
        print()
        print(f"NO {'/'.join(FATAL_FINDINGS)} FINDINGS — the tally above is well-defined."
              if code == 0 else
              f"{'/'.join(FATAL_FINDINGS)} FINDINGS PRESENT — read them before quoting the tally above.")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
