# Quickstart — 064 post-wave gap closure

## Baselines (run before any change; all must be green)

```
bash test/run_all_tests.sh                                   # REPL suite incl. A31 (549 baseline)
cd glp_gleam && ./smoke.sh                                   # gleam build + test (569 baseline, WSL)
dotnet test csharp/glp_link.tests csharp/glp_il_codec.tests csharp/glp_engine_host.tests
bash test/parity/run_gleam_corpus.sh                         # 206/206 gate
bash test/parity/cross_runtime/run_all.sh                    # Section I 18/18
```

## US1 verification (Gleam link tail)

```
gleam test  # new suites: dist_unify_test, quiescence_test, multi_accept_test
bash test/parity/cross_runtime/run_all.sh   # extended with dist-unify/quiescence/multi-link/bridge scenarios, both directions, x10 loops for SC-001
```

## US2 verification (multi-client serve path)

```
dotnet test csharp/glp_engine_host.tests    # ClientSession + serve-path suites
# manual smoke: 1 host + 3 clients, interleaved goals, per-client replies
```

## US3 verification (IL request kind)

```
dotnet test csharp/glp_il_codec.tests csharp/glp_split_protocol.tests
# corpus equivalence: IL path vs text path diff == empty
```

## US4 verification (FE/BE + embed)

```
# two-process split: start BE, drive FE with the standard scenario script, diff vs single-process
# embed: gleam test glp_embed_host_test
```

## Gates

- MVP gate (Anchor review) after US1+US2; incremental reviews after US3/US4/US5.
- Zero regression across every suite at every checkpoint; commit+push per checkpoint (scoped adds).
- Ship: /bk-codexreview → buildkit ship --skip-preflight (announce CalVer first per fleet rule) → /bk-close.
