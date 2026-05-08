#!/usr/bin/env bash
# Run AxamlIdGen against the real metris-core tree.
#
# Use case: ephemeral build-time instrumentation. The tool injects
# AutomationProperties.AutomationId attributes into XAML in place, the WPF
# compiler consumes the modified tree, then you `git -C ../../metris-core
# checkout -- .` to discard the changes. NEVER commit the result.
#
# Idempotent: re-running on an already-instrumented tree adds 0 IDs.

set -euo pipefail

# ---- colors --------------------------------------------------------------
log()  { printf '\033[1;34m[metris-core]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m       %s\n' "$*"; }
err()  { printf '\033[1;31m[error]\033[0m      %s\n' "$*" >&2; }

# ---- args ----------------------------------------------------------------
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
METRIS_CORE="${METRIS_CORE_PATH:-$REPO_ROOT/../../metris-core}"
DRY_RUN=0
ASSUME_YES=0

usage() {
    cat <<EOF
Usage: ./metris-core.sh [OPTIONS]

Run AxamlIdGen against the real metris-core tree.

OPTIONS
  -n, --dry-run    Read-only scan. Prints discovered IDs and the per-tier
                   breakdown without mutating any XAML or creating any
                   files inside metris-core.
  -y, --yes        Non-interactive. Skip the "this will modify XAML"
                   confirmation prompt. Intended for CI / automated builds.
  -h, --help       Show this help.

ENVIRONMENT
  METRIS_CORE_PATH  Override metris-core location.
                    Default: \$REPO_ROOT/../../metris-core
                    (i.e. the metris-core submodule at the repo root,
                    since this tool lives at tools/axamlidgen)

WHAT IT DOES
  - Generates a tool config in /tmp (absolute output paths, so dry-run
    leaves metris-core completely untouched).
  - Builds AxamlIdGen in Release.
  - Runs 'scan' (--dry-run) or 'instrument' against metris-core.
  - Prints a per-tier breakdown of synthesized AutomationIds:
        Tier 1  x:Name           stable (semantic)
        Tier 2  Content          stable (semantic)
        Tier 3  PropertyName     stable (semantic)
        Tier 4  Binding          stable (semantic)
        Tier 5  GridPosition     stable (layout)
        Tier 6  SubtreeOrdinal   bounded (ordinal in subtree)
        Tier 7  Hash             fingerprint (own attrs)

WHAT 'instrument' PRODUCES INSIDE metris-core
  - ~1,422 modified XAML files (AutomationProperties.AutomationId injected
    on every allow-listed control that lacked one).
  - automation-ids.json
  - Generated/AutomationIds.g.cs       (namespace Metris.Generated)
  - automation-ids.generated.ts

  These mutations are intended to be EPHEMERAL — never commit them.
  Run 'instrument' before each compile/test cycle, then revert.

EXAMPLES
  ./metris-core.sh --dry-run               # safe inspection, no changes
  ./metris-core.sh                         # interactive instrument
  ./metris-core.sh --yes                   # non-interactive (CI)
  METRIS_CORE_PATH=/path/to/x ./metris-core.sh -n

REVERT (after an instrument run)
  git -C "\$METRIS_CORE_PATH" checkout -- . && \\
    git -C "\$METRIS_CORE_PATH" clean -fd

  Or relative to this script's location:
  git -C ../../metris-core checkout -- . && \\
    git -C ../../metris-core clean -fd

IDEMPOTENCY
  Re-running 'instrument' on an already-instrumented tree adds 0 IDs.
  Each XAML file is only modified if it gains a new AutomationId.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -n|--dry-run) DRY_RUN=1; shift ;;
        -y|--yes)     ASSUME_YES=1; shift ;;
        -h|--help)    usage; exit 0 ;;
        *)            err "Unknown option: $1"; usage; exit 64 ;;
    esac
done

# ---- validate ------------------------------------------------------------
if [ ! -d "$METRIS_CORE" ]; then
    err "metris-core not found at: $METRIS_CORE"
    err "Set METRIS_CORE_PATH env var or place metris-core at the default location."
    exit 2
fi

if ! command -v dotnet >/dev/null 2>&1; then
    err "dotnet not found. Run ./setup.sh first."
    exit 2
fi

TOOL_PROJECT="$REPO_ROOT/tools/AxamlIdGen/AxamlIdGen.csproj"

# Resolve to absolute path; relative paths break --config resolution
METRIS_CORE_ABS="$(cd "$METRIS_CORE" && pwd)"

# Config lives in /tmp with absolute output paths so metris-core stays
# pristine until the actual instrument run mutates XAML + emits manifests.
CONFIG_PATH="${TMPDIR:-/tmp}/metris-core-axamlidgen.config.json"

cat > "$CONFIG_PATH" <<EOF
{
  "sentinelValue": "@auto",
  "namingPattern": "^[A-Z][A-Za-z0-9]+(_[A-Za-z][A-Za-z0-9]*)+\$",
  "anchorTierCutoff": 7,
  "missingPolicy": "strict",
  "includeGlobs": ["**/*.xaml"],
  "excludeGlobs": ["**/bin/**", "**/obj/**", "**/Generated/**"],
  "outputs": {
    "json": "$METRIS_CORE_ABS/automation-ids.json",
    "cSharp": "$METRIS_CORE_ABS/Generated/AutomationIds.g.cs",
    "cSharpNamespace": "Metris.Generated",
    "typeScript": "$METRIS_CORE_ABS/automation-ids.generated.ts"
  },
  "maxIdLength": 64
}
EOF

# ---- build tool ----------------------------------------------------------
log "Building AxamlIdGen (Release)..."
dotnet build "$TOOL_PROJECT" --configuration Release --nologo --verbosity quiet

# ---- confirm if mutating -------------------------------------------------
COMMAND="instrument"
[ "$DRY_RUN" -eq 1 ] && COMMAND="scan"

if [ "$COMMAND" = "instrument" ] && [ "$ASSUME_YES" -eq 0 ]; then
    cat <<EOF

╭────────────────────────────────────────────────────────────────────────╮
│  About to MODIFY XAML files in:                                        │
│  $METRIS_CORE_ABS
│                                                                        │
│  The tool injects AutomationProperties.AutomationId on every           │
│  allow-listed control lacking one. Mutations are intended to be        │
│  ephemeral — never commit them.                                        │
│                                                                        │
│  Revert with: git -C "$METRIS_CORE_ABS" checkout -- .
│  Or use:      ./metris-core.sh --dry-run    (read-only scan)
╰────────────────────────────────────────────────────────────────────────╯

EOF
    read -p "Proceed with instrument? [y/N] " -r reply
    case "$reply" in
        [yY]|[yY][eE][sS]) ;;
        *) log "Aborted."; exit 0 ;;
    esac
fi

# ---- run -----------------------------------------------------------------
log "Running AxamlIdGen $COMMAND on $METRIS_CORE_ABS"
log "(this may take a few seconds; the per-tier breakdown follows)"
echo

dotnet run --project "$TOOL_PROJECT" \
           --configuration Release \
           --no-build \
           -- "$COMMAND" "$METRIS_CORE_ABS" --config "$CONFIG_PATH"

# ---- post-run report -----------------------------------------------------
if [ "$COMMAND" = "instrument" ] && [ -d "$METRIS_CORE_ABS/.git" ]; then
    modified=$(git -C "$METRIS_CORE_ABS" status --porcelain 2>/dev/null | grep -cE '\.xaml$' || true)
    new_files=$(git -C "$METRIS_CORE_ABS" status --porcelain 2>/dev/null | grep -cE '^\?\?' || true)
    echo
    log "Working tree changes in metris-core:"
    log "  modified XAML files: $modified"
    log "  new untracked files: $new_files (manifest outputs)"
    log "Revert with: git -C \"$METRIS_CORE_ABS\" checkout -- . && git -C \"$METRIS_CORE_ABS\" clean -fd"
fi
