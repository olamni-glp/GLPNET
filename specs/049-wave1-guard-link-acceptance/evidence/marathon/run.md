# US4 marathon durability — run record (T022)

- **Run id**: `mrun-9724364d684a` (opened 2026-07-08, buildkit-marathon 2026.7.4.1, feature `049-wave1-guard-link-acceptance`)
- **Durable store**: out-of-repo deploy-home catalog, target `fb9d55f94f8b`; run mirror at
  `C:\Users\smbuser\AppData\Local\buildkit\deploy-home\targets\fb9d55f94f8b\marathon-mrun-9724364d684a.md`
  (constitution VI-b exemption — verified, not modified)
- **CLI**: `D:\bstdev\research\buildkit\.venv313\Scripts\buildkit-marathon.exe` (Python 3.13 venv; system 3.14 breaks DBOS)
- **Structure**: item `mitem-019f427ef4b9-…9087ae` ("us4-durability-probe") expanded into steps
  `us4-step-1`, `us4-step-2`, `us4-step-3` (expansion-with-lineage)
- **Plan**: checkpoint steps 1–2 (≥2 durable checkpoints), leave step 3 running, kill the owning process
  mid-flight (T023), resume from a fresh session; then exercise the durable-first/commit re-drive (T024).

**Checkpoint 1 (us4-step-1)**: checkpoint_id 6, commit `66e31e35`, durable row complete.

**Checkpoint 2 (us4-step-2)**: checkpoint_id 7, commit `415cbff5`. Step-3 in-flight kill probe follows (T023).
