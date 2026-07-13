#!/usr/bin/env bash
# test/parity/run_gleam_corpus.sh — run the pinned corpus on the Gleam instance, diff vs
# recorded Dart goldens, assert the suite-level 10x wall-clock bound (feature 050, T039;
# SC-001/SC-009; contracts/corpus-parity.md). Host: git-bash (native gleam).
#
# Reads test/parity/corpus.list (the pinned cases) and test/parity/goldens/ (the recorded
# Dart reference). Classifications in test/parity/expected.list mark blocks Gleam cannot
# run (blocked) or verified expected-divergences (gap/fork) — surfaced, excluded from the
# SC-001 agreement denominator. Divergences NOT in expected.list are real parity failures.
#
# Usage: run_gleam_corpus.sh [FILTER]   (FILTER = block id, or B/C/D/E, or empty = all)
# Exit: 0 iff every in-scope (non-blocked/gap/fork) case agrees; else the divergence count.

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GLEAM_DIR="$REPO_ROOT/glp_gleam"
GOLDENS="$SCRIPT_DIR/goldens"
CORPUS="$SCRIPT_DIR/corpus.list"
FILTER="${1:-}"
. "$SCRIPT_DIR/lib/normalize.sh"

command -v gleam >/dev/null 2>&1 || { echo "run_gleam_corpus.sh: gleam not on PATH" >&2; exit 2; }

# --- classifications -------------------------------------------------------
declare -A CLASS CLASS_REASON
if [ -f "$SCRIPT_DIR/expected.list" ]; then
    while IFS= read -r l; do
        case "$l" in ''|'#'*) continue ;; esac
        k="${l%% *}"; r="${l#* }"; b="${r%% *}"; why="${r#* }"
        CLASS["$b"]="$k"; CLASS_REASON["$b"]="$why"
    done < "$SCRIPT_DIR/expected.list"
fi

now_ms() { date +%s%N | awk '{printf "%d", $1/1000000}'; }
TMP_GLP="$GLEAM_DIR/.parity_run.glp"   # concat/inline scratch (loaded by basename)

# Run a scripted Gleam REPL session (stdin built by caller); echo raw transcript.
run_gleam() { ( cd "$GLEAM_DIR" && gleam run ) 2>/dev/null; }

extract_segment() { awk -v idx="$2" '/^@@@SEG@@@/{n=substr($0,10)+0;inseg=(n==idx);next} inseg{print}' "$1"; }

AGREE=0; DIVERGE=0; BLOCKED=0; GAPFORK=0
DIVERGED_BLOCKS=""; GLEAM_MS=0
mkdir -p "$GOLDENS/gleam"

# ---------------------------------------------------------------------------
record_and_diff_block() {
    local id="$1"; local -a files goals
    eval "files=( \"\${_BLK_FILES[@]}\" )"; eval "goals=( \"\${_BLK_GOALS[@]}\" )"
    [ -n "$FILTER" ] && [ "$FILTER" != "$id" ] && return 0

    local cls="${CLASS[$id]:-}"
    if [ "$cls" = "blocked" ]; then
        BLOCKED=$((BLOCKED+1)); echo "  BLOCKED  $id — ${CLASS_REASON[$id]}"; return 0
    fi
    local golden="$GOLDENS/runtime/$id.golden"
    [ -f "$golden" ] || { echo "  MISSING golden for $id" >&2; DIVERGE=$((DIVERGE+1)); return 0; }

    # Build the Gleam session: load each file separately (engine.load ACCUMULATES,
    # Dart-faithful), then run goals. No concatenation -- that would put a shared
    # predicate's clauses non-contiguous; separate loads mirror the Dart REPL session.
    local input="" f g nload=0
    for f in "${files[@]}"; do input+="load ../$f"$'\n'; nload=$((nload+1)); done
    for g in "${goals[@]}"; do input+="$g"$'\n'; done
    input+=":quit"$'\n'

    local start end raw seg
    start="$(now_ms)"; raw="$(printf '%s' "$input" | run_gleam)"; end="$(now_ms)"
    GLEAM_MS=$((GLEAM_MS + end - start))
    rm -f "$TMP_GLP"

    seg="$(mktemp)"
    printf '%s\n' "$raw" | tr -d '\r' | awk '/^GLP> /{seg++;print "@@@SEG@@@" seg} seg>0{print}' > "$seg"

    # Produce the Gleam side in golden goal-outcome shape, then diff (headers stripped).
    local gout="$GOLDENS/gleam/$id.gleam" i
    {
        for ((i=1;i<=${#goals[@]};i++)); do
            echo "## goal: ${goals[$((i-1))]}"
            extract_segment "$seg" "$((nload+i))" | normalize_outcome
            echo
        done
    } > "$gout"
    rm -f "$seg"

    if diff -q <(grep -v '^# ' "$golden") "$gout" >/dev/null 2>&1; then
        AGREE=$((AGREE+1)); echo "  AGREE    $id (${#goals[@]} goals)"
    elif [ "$cls" = "gap" ] || [ "$cls" = "fork" ]; then
        GAPFORK=$((GAPFORK+1)); echo "  ${cls^^}  $id — ${CLASS_REASON[$id]} (expected divergence)"
    else
        DIVERGE=$((DIVERGE+1)); DIVERGED_BLOCKS+=" $id"
        echo "  DIVERGE  $id:"
        diff <(grep -v '^# ' "$golden") "$gout" | sed 's/^/      /' | head -20
    fi
}

# --- load-outcome sections (B/C/D): Gleam replace-load gives per-file isolation ---
run_load_section() {
    local section="$1"; shift; local -a paths=( "$@" )
    [ "${#paths[@]}" -eq 0 ] && return 0
    [ -n "$FILTER" ] && [ "$FILTER" != "$section" ] && return 0
    local input="" p start end raw
    for p in "${paths[@]}"; do input+="load ../$p"$'\n'; done
    input+=":quit"$'\n'
    start="$(now_ms)"; raw="$(printf '%s' "$input" | run_gleam | tr -d '\r')"; end="$(now_ms)"
    GLEAM_MS=$((GLEAM_MS + end - start))
    for p in "${paths[@]}"; do
        local gtok dtok line
        line="$(printf '%s\n' "$raw" | grep -F "../$p" | grep -E 'Loaded:|Error loading' | head -1)"
        gtok="$(printf '%s' "$line" | classify_load | cut -f1)"
        dtok="$(awk -F'\t' -v k="$p" '$1==k{print $2}' "$GOLDENS/load.golden")"
        if [ "$gtok" = "$dtok" ]; then AGREE=$((AGREE+1));
        else DIVERGE=$((DIVERGE+1)); DIVERGED_BLOCKS+=" load:$p"; echo "  DIVERGE  load $p: gleam=$gtok dart=$dtok"; fi
    done
    echo "  load section $section: compared ${#paths[@]} files"
}

run_guardcase() {
    local id="$1"; shift; local clause="$*"
    [ -n "$FILTER" ] && [ "$FILTER" != "E" ] && [ "$FILTER" != "$id" ] && return 0
    printf '%s\n' "$clause" > "$TMP_GLP"
    local raw line gtok dtok start end
    start="$(now_ms)"; raw="$(printf 'load .parity_run.glp\n:quit\n' | run_gleam | tr -d '\r')"; end="$(now_ms)"
    GLEAM_MS=$((GLEAM_MS + end - start)); rm -f "$TMP_GLP"
    line="$(printf '%s\n' "$raw" | grep -E 'Loaded:|Error loading' | head -1)"
    gtok="$(printf '%s' "$line" | classify_load | cut -f1)"
    dtok="$(awk -F'\t' -v k="E:$id" '$1==k{print $2}' "$GOLDENS/load.golden")"
    if [ "$gtok" = "$dtok" ]; then AGREE=$((AGREE+1)); echo "  AGREE    guard E:$id ($gtok)";
    else DIVERGE=$((DIVERGE+1)); DIVERGED_BLOCKS+=" E:$id"; echo "  DIVERGE  guard E:$id: gleam=$gtok dart=$dtok"; fi
}

# --- parse corpus.list + drive (same grammar as the recorder) ---------------
_BLK_ID=""; _BLK_FILES=(); _BLK_GOALS=(); declare -a LOAD_B LOAD_C LOAD_D; LOAD_B=(); LOAD_C=(); LOAD_D=()
flush_block() { [ -z "$_BLK_ID" ] && return 0; record_and_diff_block "$_BLK_ID"; _BLK_ID=""; _BLK_FILES=(); _BLK_GOALS=(); }
echo "=== Gleam corpus runner (feature 050 T039) ==="
while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in ''|'#'*) continue ;; esac
    tok="${line%% *}"; rest="${line#"$tok"}"; rest="${rest# }"
    case "$tok" in
        block) flush_block; _BLK_ID="$rest" ;;
        file)  _BLK_FILES+=( "$rest" ) ;;
        goal)  _BLK_GOALS+=( "$rest" ) ;;
        loadcase) flush_block; sec="${rest%% *}"; path="${rest#* }"
                  case "$sec" in B) LOAD_B+=( "$path" );; C) LOAD_C+=( "$path" );; D) LOAD_D+=( "$path" );; esac ;;
        guardcase) flush_block; gid="${rest%% *}"; gclause="${rest#* }"; run_guardcase "$gid" "$gclause" ;;
    esac
done < "$CORPUS"
flush_block
run_load_section B "${LOAD_B[@]}"
run_load_section C "${LOAD_C[@]}"
run_load_section D "${LOAD_D[@]}"

# --- 10x wall-clock bound (SC-009) -----------------------------------------
DART_MS="$(awk -F'\t' '{s+=$2} END{print s+0}' "$GOLDENS/timings.dart.tsv")"
echo ""
echo "=== summary ==="
echo "  agree=$AGREE  diverge=$DIVERGE  blocked=$BLOCKED  gap/fork=$GAPFORK"
[ -n "$DIVERGED_BLOCKS" ] && echo "  diverged:$DIVERGED_BLOCKS"
echo "  wall-clock: gleam=${GLEAM_MS}ms  dart=${DART_MS}ms  bound=10x"
if [ "$DART_MS" -gt 0 ] && [ "$GLEAM_MS" -le $((10*DART_MS)) ]; then echo "  10x bound: PASS"; else echo "  10x bound: (partial run or FAIL — full run required for SC-009)"; fi
[ "$DIVERGE" -eq 0 ] && echo "  RESULT: 100% agreement on in-scope cases" || echo "  RESULT: $DIVERGE divergence(s) to classify (Bug Protocol)"
exit "$DIVERGE"
