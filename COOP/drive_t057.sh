#!/usr/bin/env bash
# T057 — drive quickstart.md end-to-end on a scratch run. Read-only wrt the repo
# git (no commit/push grant → --paths are informational). Store is per-run,
# outside the repo at C:/pglite/marathon/qs-t057.
cd "D:/bstdev/research/glp/glpnet" || exit 99
PY="codeconv/.venv/Scripts/python.exe"
RUN="qs-t057"
M() {
  echo ""
  echo "### \$ codeconv marathon $* --run $RUN"
  "$PY" -m codeconv.cli marathon "$@" --run "$RUN" 2>&1
  echo "  [exit:$?]"
}
# capture has its own flag order; wrap separately where needed.

echo "===== STEP 1: register + resume ====="
"$PY" -m codeconv.cli marathon register --run "$RUN" --title "My work" \
  --stages discover,design,build,verify --budget 500000 --budget-unit tokens 2>&1; echo "  [exit:$?]"
M resume

echo ""
echo "===== STEP 2: keeper start + doctor ====="
M keeper start
M doctor --json

echo ""
echo "===== STEP 3: work discover -> checkpoint -> status ====="
M stage-start discover
M checkpoint discover --paths src/discover.py,tests/test_discover.py --budget 38000 -m "discover: initial sweep"
M status

echo ""
echo "===== STEP 4: grow the run mid-flight (append-stage) ====="
M append-stage harden
M status

echo ""
echo "===== STEP 5: capture emergent work -> mini-pipeline ====="
"$PY" -m codeconv.cli marathon capture --run "$RUN" --kind missing-prerequisite \
  --title "schema migration must land first" --blocks build 2>&1; echo "  [exit:$?]"
M resume --json

echo ""
echo "===== STEP 5b: advance item-1 mini-pipeline explicitly (default-deny) ====="
for K in mini_specify mini_clarify mini_plan mini_tasks mini_analyze; do
  M stage-start "item-1:$K"
  M checkpoint "item-1:$K" --paths "items/1/$K.md" -m "item-1 $K"
done
M status

echo ""
echo "===== STEP 6: survive a crash (keeper recover) + resume ====="
M keeper recover
M resume

echo ""
echo "===== STEP 7: preserved 024 strengths (US5) ====="
M gate --stage design --approve --by gabi
M rerun --stage discover --subagent design-b
M trace --subject design --input '{"k":1}' --accept
M reconcile

echo ""
echo "===== STEP 8a: finalize while incomplete (guard) ====="
M finalize

echo ""
echo "===== STEP 8b: complete remaining ordinary stages ====="
for S in design build verify harden; do
  M stage-start "$S"
  M checkpoint "$S" --paths "src/$S.py" -m "$S done"
done
M status

echo ""
echo "===== STEP 8c: finalize when all complete ====="
M finalize
M status

echo ""
echo "===== STEP 9: graceful shutdown ====="
M keeper stop

echo ""
echo "===== DONE ====="
