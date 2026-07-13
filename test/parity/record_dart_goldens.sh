#!/usr/bin/env bash
# test/parity/record_dart_goldens.sh — record Dart reference outcomes + wall-clock per
# corpus case into test/parity/goldens/ (feature 050, T037; contracts/corpus-parity.md).
# Host: git-bash (native). EXPLICIT re-record only — never run implicitly.
#
# PRECONDITION (analyze A1): test/parity/corpus.list pins the case list; this script
# records exactly those cases (see corpus-manifest.md for the reviewed scope).
#
# Outputs under test/parity/goldens/:
#   runtime/<block>.golden   per-goal normalized outcomes for a Section-A block (diff target)
#   load.golden              "<relpath>\t<LOADED|REJECTED>" per B/C/D/E file (diff target)
#   loadstage.dart.tsv       "<relpath>\t<stage>" informational rejection stage (NOT diffed)
#   timings.dart.tsv         "<case-id>\t<ms>" wall-clock (SC-009; NOT diffed)
#
# Usage: record_dart_goldens.sh [FILTER]
#   FILTER (optional): a block id (e.g. a9), or a section letter (B/C/D/E), or empty = all.

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GOLDENS="$SCRIPT_DIR/goldens"
CORPUS="$SCRIPT_DIR/corpus.list"
FILTER="${1:-}"

# shellcheck source=lib/normalize.sh
. "$SCRIPT_DIR/lib/normalize.sh"

# --- Dart REPL + path handling (mirror test/run_all_tests.sh) ----------------
DART="${DART:-$(command -v dart 2>/dev/null || echo /c/src/flutter/bin/cache/dart-sdk/bin/dart)}"
if ! "$DART" --version >/dev/null 2>&1; then
    echo "record_dart_goldens.sh: dart not found (set DART=...)" >&2
    exit 2
fi

to_repl_path() {  # MSYS /d/foo -> D:/foo for the Windows-native Dart REPL
    if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi
}

# Compile a kernel snapshot once for fast startup (shared with run_all_tests.sh).
REPL_SRC="$REPO_ROOT/glp_runtime/bin/glp_repl.dart"
REPL_SNAP="$REPO_ROOT/glp_runtime/.dart_tool/repl.dill"
NEEDS=false
[ -f "$REPL_SNAP" ] || NEEDS=true
if [ "$NEEDS" = false ] && [ -n "$(find "$REPO_ROOT/glp_runtime/lib" "$REPO_ROOT/glp_runtime/bin" -name '*.dart' -newer "$REPL_SNAP" 2>/dev/null | head -1)" ]; then
    NEEDS=true
fi
if [ "$NEEDS" = true ]; then
    echo "Compiling Dart REPL snapshot..." >&2
    mkdir -p "$REPO_ROOT/glp_runtime/.dart_tool"
    ( cd "$REPO_ROOT/glp_runtime" && "$DART" compile kernel -o "$REPL_SNAP" bin/glp_repl.dart ) >/dev/null 2>&1 || true
fi
REPL="$REPL_SNAP"; [ -f "$REPL" ] || REPL="$REPL_SRC"

now_ms() { date +%s%N | awk '{printf "%d", $1/1000000}'; }

# Run a REPL session: stdin = the input lines already built by the caller.
# Echoes the raw transcript on stdout. CWD = repo root (root self.glp resolution).
run_repl() { ( cd "$REPO_ROOT" && "$DART" run "$REPL" ) 2>&1; }

# Extract the k-th "GLP> " segment (1-based) from a segmented transcript file.
extract_segment() {  # $1=segmented file  $2=index
    awk -v idx="$2" '
        /^@@@SEG@@@/ { n=substr($0,10)+0; inseg=(n==idx); next }
        inseg { print }
    ' "$1"
}

mkdir -p "$GOLDENS/runtime"
: > "$GOLDENS/load.golden.tmp"
: > "$GOLDENS/loadstage.dart.tsv.tmp"
: > "$GOLDENS/timings.dart.tsv.tmp"

# ---------------------------------------------------------------------------
# Runtime block recorder: load files, run goals, split by prompt, write goldens.
# ---------------------------------------------------------------------------
record_block() {
    local id="$1"; shift
    local -a files goals
    eval "files=( \"\${_BLK_FILES[@]}\" )"
    eval "goals=( \"\${_BLK_GOALS[@]}\" )"
    [ -n "$FILTER" ] && [ "$FILTER" != "$id" ] && return 0

    local input="" f g
    for f in "${files[@]}"; do input+="$(to_repl_path "$REPO_ROOT/$f")"$'\n'; done
    for g in "${goals[@]}"; do input+="$g"$'\n'; done
    input+=":quit"$'\n'

    local start end raw seg
    start="$(now_ms)"
    raw="$(printf '%s' "$input" | run_repl)"
    end="$(now_ms)"
    printf 'block:%s\t%d\n' "$id" "$((end-start))" >> "$GOLDENS/timings.dart.tsv.tmp"

    # Segment the transcript from the first "GLP> " onward.
    seg="$(mktemp)"
    printf '%s\n' "$raw" | tr -d '\r' | awk '
        /^GLP> / { seg++; print "@@@SEG@@@" seg }
        seg>0 { print }
    ' > "$seg"

    local nf="${#files[@]}" ng="${#goals[@]}" i
    # Sanity: each loaded file segment should say "Loaded".
    for ((i=1; i<=nf; i++)); do
        if ! extract_segment "$seg" "$i" | grep -q 'Loaded:'; then
            echo "  WARN[$id]: file #$i did not load cleanly:" >&2
            extract_segment "$seg" "$i" | sed 's/^/    /' >&2
        fi
    done

    local out="$GOLDENS/runtime/$id.golden"
    {
        echo "# golden: Dart reference outcomes for block $id (feature 050 T037)"
        echo "# files: ${files[*]:-<none>}"
        for ((i=1; i<=ng; i++)); do
            echo "## goal: ${goals[$((i-1))]}"
            extract_segment "$seg" "$((nf+i))" | normalize_outcome
            echo
        done
    } > "$out"
    rm -f "$seg"
    echo "  recorded runtime/$id.golden (${ng} goals)" >&2
}

# ---------------------------------------------------------------------------
# Load-case recorder: batch a whole section in one session with :clear between,
# classify each file by its outcome line (path-scoped), binary token + stage.
# ---------------------------------------------------------------------------
record_load_section() {
    local section="$1"; shift
    local -a paths=( "$@" )
    [ "${#paths[@]}" -eq 0 ] && return 0
    [ -n "$FILTER" ] && [ "$FILTER" != "$section" ] && return 0

    local input="" p rp
    for p in "${paths[@]}"; do
        input+="$(to_repl_path "$REPO_ROOT/$p")"$'\n'":clear"$'\n'
    done
    input+=":quit"$'\n'

    local start end raw
    start="$(now_ms)"
    raw="$(printf '%s' "$input" | run_repl | tr -d '\r')"
    end="$(now_ms)"
    printf 'load:%s\t%d\n' "$section" "$((end-start))" >> "$GOLDENS/timings.dart.tsv.tmp"

    for p in "${paths[@]}"; do
        rp="$(to_repl_path "$REPO_ROOT/$p")"
        # The file's outcome line is the (first) transcript line mentioning its path.
        local line cls
        line="$(printf '%s\n' "$raw" | grep -F "$rp" | grep -E 'Loaded:|Error loading' | head -1)"
        cls="$(printf '%s' "$line" | classify_load)"   # "<TOKEN>\t<stage>"
        printf '%s\t%s\n' "$p" "$(printf '%s' "$cls" | cut -f1)" >> "$GOLDENS/load.golden.tmp"
        printf '%s\t%s\n' "$p" "$(printf '%s' "$cls" | cut -f2)" >> "$GOLDENS/loadstage.dart.tsv.tmp"
    done
    echo "  recorded load section $section (${#paths[@]} files)" >&2
}

# Inline guard case (Section E): write a temp .glp, load it, classify.
record_guardcase() {
    local id="$1"; shift
    local clause="$*"
    [ -n "$FILTER" ] && [ "$FILTER" != "E" ] && [ "$FILTER" != "$id" ] && return 0
    local tmp; tmp="$(mktemp).glp"
    printf '%s\n' "$clause" > "$tmp"
    local rp raw line cls start end
    rp="$(to_repl_path "$tmp")"
    start="$(now_ms)"
    raw="$(printf '%s\n:quit\n' "$rp" | run_repl | tr -d '\r')"
    end="$(now_ms)"
    printf 'guard:%s\t%d\n' "$id" "$((end-start))" >> "$GOLDENS/timings.dart.tsv.tmp"
    line="$(printf '%s\n' "$raw" | grep -E 'Loaded:|Error loading' | head -1)"
    cls="$(printf '%s' "$line" | classify_load)"
    printf 'E:%s\t%s\n' "$id" "$(printf '%s' "$cls" | cut -f1)" >> "$GOLDENS/load.golden.tmp"
    printf 'E:%s\t%s\n' "$id" "$(printf '%s' "$cls" | cut -f2)" >> "$GOLDENS/loadstage.dart.tsv.tmp"
    rm -f "$tmp"
    echo "  recorded guardcase E:$id" >&2
}

# ---------------------------------------------------------------------------
# Parse corpus.list and drive the recorders.
# ---------------------------------------------------------------------------
_BLK_ID=""; _BLK_FILES=(); _BLK_GOALS=()
declare -a LOAD_B LOAD_C LOAD_D
LOAD_B=(); LOAD_C=(); LOAD_D=()

flush_block() {
    [ -z "$_BLK_ID" ] && return 0
    record_block "$_BLK_ID"
    _BLK_ID=""; _BLK_FILES=(); _BLK_GOALS=()
}

while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in
        ''|'#'*) continue ;;
    esac
    tok="${line%% *}"; rest="${line#"$tok"}"; rest="${rest# }"
    case "$tok" in
        block)     flush_block; _BLK_ID="$rest" ;;
        file)      _BLK_FILES+=( "$rest" ) ;;
        goal)      _BLK_GOALS+=( "$rest" ) ;;
        loadcase)  flush_block
                   sec="${rest%% *}"; path="${rest#* }"
                   case "$sec" in
                       B) LOAD_B+=( "$path" ) ;;
                       C) LOAD_C+=( "$path" ) ;;
                       D) LOAD_D+=( "$path" ) ;;
                   esac ;;
        guardcase) flush_block
                   gid="${rest%% *}"; gclause="${rest#* }"
                   record_guardcase "$gid" "$gclause" ;;
        *) echo "  WARN: unknown record: $line" >&2 ;;
    esac
done < "$CORPUS"
flush_block

record_load_section B "${LOAD_B[@]}"
record_load_section C "${LOAD_C[@]}"
record_load_section D "${LOAD_D[@]}"

# Commit the batched outputs (sorted for stable diffs).
sort "$GOLDENS/load.golden.tmp"       -o "$GOLDENS/load.golden"        && rm -f "$GOLDENS/load.golden.tmp"
sort "$GOLDENS/loadstage.dart.tsv.tmp" -o "$GOLDENS/loadstage.dart.tsv" && rm -f "$GOLDENS/loadstage.dart.tsv.tmp"
sort "$GOLDENS/timings.dart.tsv.tmp"   -o "$GOLDENS/timings.dart.tsv"   && rm -f "$GOLDENS/timings.dart.tsv.tmp"

echo "record_dart_goldens.sh: done." >&2
