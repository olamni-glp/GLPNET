# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Refuse to call work "merged" or "shipped" when it exists on no remote.

WHY THIS EXISTS — one measured incident, 2026-09-06
    This lane merged the fleet's M6 send fix into another lane's `develop`, verified 93/93 green,
    and broadcast "R-C MERGED — REBUILD NOW" to 44 channels. Every part of that was true when
    written and none of it survived: the merge could not be pushed (a lane may not push another
    lane's integration branch), so it lived only in one machine's object store, and

        develop@{4}: reset: moving to origin/develop

    discarded it four reflog entries later. `git branch --contains <sha>` then returned nothing.
    The branch itself had NEVER been on origin. The fleet acted on the broadcast for eight hours
    and any lane that rebuilt got a binary without the fix.

    The root cause is one sentence this lane published as a virtue:
    "already in the object store on this machine — no push, no fetch, no network."
    Local reachability is not durability. `git log` showing your commit proves it existed, never
    that it survives someone else's reset.

WHAT THIS DOES
    Answers one question per ref, mechanically: is this commit reachable from any REMOTE ref?

        python3 scripts/unpushed_claim_guard.py fdb823c9
        python3 scripts/unpushed_claim_guard.py --repo ../qhstate 095-m6-send-spool d4d374ab

    Exit 0 — every ref given is on a remote. The claim is safe to publish.
    Exit 1 — at least one is remote-unreachable. DO NOT publish it as merged or shipped.
    Exit 2 — usage, or a ref git cannot resolve. Refusing beats guessing.

    It is deliberately not a git hook. The failure it catches is not committing; it is CLAIMING.
    Run it in the seconds before a broadcast, a handoff, or a restart brief that says "merged".

WHAT IT DOES NOT DO
    It never pushes, never fetches and never writes. `--fetch` is offered because a stale
    remote-tracking ref can produce a FALSE ABSENCE (the work is on origin, your clone has not
    heard) — but it is opt-in, because a guard that reaches the network is a guard that fails
    when the network does, and a guard that fails is worse than none.
"""

from __future__ import annotations

import argparse
import subprocess
import sys


def _git(repo: str, *args: str) -> tuple[int, str]:
    proc = subprocess.run(
        ["git", "-C", repo, *args],
        capture_output=True, text=True, check=False,
    )
    return proc.returncode, (proc.stdout or "").strip()


def _resolve(repo: str, ref: str) -> str | None:
    code, out = _git(repo, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}")
    return out if code == 0 and out else None


def _remote_refs_containing(repo: str, sha: str) -> list[str]:
    """Remote-tracking refs from which `sha` is reachable.

    `for-each-ref --contains` is the honest instrument here: `branch -a --contains` also lists
    LOCAL branches, and a local branch is exactly the false comfort this guard exists to remove.
    """
    code, out = _git(
        repo, "for-each-ref", "--format=%(refname:short)", "--contains", sha, "refs/remotes/"
    )
    if code != 0:
        return []
    return [line for line in out.splitlines() if line.strip()]


def _tags_containing(repo: str, sha: str) -> list[str]:
    code, out = _git(repo, "tag", "--contains", sha)
    if code != 0:
        return []
    return [line for line in out.splitlines() if line.strip()]


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Refuse a merged/shipped claim about work that is on no remote."
    )
    parser.add_argument("refs", nargs="+", help="commit SHAs or branch names being claimed")
    parser.add_argument("--repo", default=".", help="repository to inspect (default: cwd)")
    parser.add_argument(
        "--fetch", action="store_true",
        help="git fetch --all first, so a stale remote-tracking ref cannot report a false absence",
    )
    args = parser.parse_args()

    if _git(args.repo, "rev-parse", "--git-dir")[0] != 0:
        print(f"guard: REFUSED — not a git repository: {args.repo}", file=sys.stderr)
        return 2

    if args.fetch:
        code, _ = _git(args.repo, "fetch", "--all", "--quiet")
        if code != 0:
            print("guard: REFUSED — --fetch was asked for and failed; a guard that silently "
                  "skips its own freshness check is the defect it is meant to catch.",
                  file=sys.stderr)
            return 2

    unresolved: list[str] = []
    unreachable: list[tuple[str, str]] = []
    safe: list[tuple[str, str, str]] = []

    for ref in args.refs:
        sha = _resolve(args.repo, ref)
        if sha is None:
            unresolved.append(ref)
            continue
        remotes = _remote_refs_containing(args.repo, sha)
        tags = _tags_containing(args.repo, sha)
        if remotes or tags:
            where = ", ".join(remotes[:3] + [f"tag:{t}" for t in tags[:2]])
            safe.append((ref, sha[:8], where))
        else:
            unreachable.append((ref, sha[:8]))

    for ref, short, where in safe:
        print(f"  ON A REMOTE   {ref} ({short}) — {where}")
    for ref, short in unreachable:
        print(f"  LOCAL ONLY    {ref} ({short}) — reachable from NO remote ref and NO tag")
    for ref in unresolved:
        print(f"  UNRESOLVED    {ref} — git cannot resolve this to a commit")

    if unresolved:
        print(f"\nguard: REFUSED — {len(unresolved)} ref(s) do not resolve. "
              f"An unresolvable ref is not evidence of anything.", file=sys.stderr)
        return 2

    if unreachable:
        print(f"\nguard: REFUSED — {len(unreachable)} of {len(args.refs)} ref(s) exist only in "
              f"this clone.\nDo NOT publish this as merged or shipped. Push the branch and open a "
              f"PR; a reset in this repo would erase it and the broadcast would already be out.",
              file=sys.stderr)
        return 1

    print(f"\nguard: OK — all {len(args.refs)} ref(s) are reachable from a remote.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
