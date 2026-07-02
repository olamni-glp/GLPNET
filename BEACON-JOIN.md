# BEACON-JOIN — connect **GLPNET** to the team beacon (slot 4 of 8)

**Advisory only.** This registers THIS repo (`GLPNET`) with the shared team **beacon**
(the wall radiator), reports its buildkit pipeline stage, and posts a short status
announcement in its time slot. It never touches this repo's pipeline, source, or git —
it only talks to the beacon store.

## The live beacon (read this — it's why earlier joins timed out)
- **Live home:** `C:\Users\gavri\AppData\Local\bk-beacon\live8` — the running store
  (PGlite bridge + macaroon secret), kept alive by `launch_beacon.py`.
- Earlier runbooks pointed at `D:\BSTDEV\bk-beacon-home`, whose store bridge is **dead**;
  every write there returned `funnel commit timed out`. That was a wrong-home mismatch, not a
  busy store. The active pointer is now fixed to live8, and the joiner below targets live8
  **explicitly**, so the pointer no longer matters.
- The joiner writes via `connect_existing` (attaches to the live bridge, **takes no lock**) —
  the same path the beacon's own reporters use. **Do not verify with `beacon list`**: on the
  live store that takes the launcher's lock and fails. Use the read in step 3.

## Your slot — **4 of 8**
Joins are staggered ~10 s by slot (no stampede) and **retried until this repo appears in the
roster**; then the repo posts its status once per its 2-minute wall-clock slot. If the beacon
store is DOWN, the joiner reports `beacon_host_up = False` and **stops** — it never retry-storms.

## 1. Join now + keep posting in your slot
```powershell
python "C:\Users\gavri\AppData\Local\bk-beacon\slot_join.py" --slot 4
```
The joiner auto-detects repo name, machine, `buildkit --version`, and stage (from
`.specify/feature.json`). It prints `beacon_host_up`, then `JOINED: …` once verified.

## 2. Join only (no ongoing posting)
```powershell
python "C:\Users\gavri\AppData\Local\bk-beacon\slot_join.py" --slot 4 --post-hours 0
```

## 3. Verify (read-only — no lock)
```powershell
python -c "import sys; sys.path.insert(0, r'D:\bstdev\research\buildkit-beacon\src'); from buildkit_cli.beacon.store import session as bs; from buildkit_cli.beacon.mcp.service import BeaconMcp; s=bs.connect_existing(r'C:\Users\gavri\AppData\Local\bk-beacon\live8'); print('STORE DOWN' if s is None else [r for r in BeaconMcp(s).list() if r['repo']=='GLPNET'])"
```

## If something looks off
- `beacon_host_up = False` / `STORE DOWN` — the live beacon isn't running. Stop and tell the
  operator to bring up live8 (`launch_beacon.py`). Do **not** loop joins by hand.
- `ModuleNotFoundError: buildkit_cli` — the beacon source moved from
  `D:\bstdev\research\buildkit-beacon\src`; fix `BEACON_SRC` at the top of `slot_join.py`.
- A down beacon never fails THIS repo's pipeline/source/git — the whole flow is advisory.
