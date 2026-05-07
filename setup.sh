#!/usr/bin/env bash
# Sets up Avalonia UI development on Ubuntu.
# Idempotent: safe to re-run.

set -euo pipefail

log()  { printf '\033[1;34m[setup]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m  %s\n' "$*"; }
err()  { printf '\033[1;31m[error]\033[0m %s\n' "$*" >&2; }

require() {
    if ! command -v "$1" >/dev/null 2>&1; then
        err "'$1' not found. $2"
        exit 1
    fi
}

# --- 1. Prerequisites -------------------------------------------------------
log "Checking prerequisites..."
require dotnet "Install with: sudo apt install -y dotnet-sdk-10.0"

# Avalonia templates currently target net10.0, so we need an SDK >= 10.
# If only an older SDK is present, install dotnet-sdk-10.0 alongside it.
have_net10() { dotnet --list-sdks 2>/dev/null | awk '{print $1}' | grep -q '^10\.'; }

if ! have_net10; then
    log ".NET 10 SDK not found; installing dotnet-sdk-10.0 (required by Avalonia templates)..."
    sudo apt update
    sudo apt install -y dotnet-sdk-10.0
    if ! have_net10; then
        err "Failed to install .NET 10 SDK."
        exit 1
    fi
fi
log ".NET SDKs available: $(dotnet --list-sdks | awk '{print $1}' | paste -sd, -)"

# --- 2. Native libs Avalonia depends on ------------------------------------
log "Ensuring native libraries are present..."
NEEDED_PKGS=(libx11-6 libice6 libsm6 libfontconfig1 libicu74)
MISSING=()
for pkg in "${NEEDED_PKGS[@]}"; do
    dpkg -s "$pkg" >/dev/null 2>&1 || MISSING+=("$pkg")
done
if [ ${#MISSING[@]} -gt 0 ]; then
    log "Installing missing packages: ${MISSING[*]}"
    sudo apt update
    sudo apt install -y "${MISSING[@]}"
else
    log "All native libraries already installed."
fi

# --- 3. Avalonia project templates -----------------------------------------
log "Installing/updating Avalonia templates..."
if dotnet new list 2>/dev/null | grep -qi 'avalonia'; then
    log "Avalonia templates already installed; updating..."
    dotnet new update >/dev/null || warn "Template update reported issues (non-fatal)."
else
    dotnet new install Avalonia.Templates
fi

# --- 4. VS Code extensions (optional) --------------------------------------
if command -v code >/dev/null 2>&1; then
    log "Installing VS Code extensions..."
    code --install-extension ms-dotnettools.csdevkit --force
    code --install-extension AvaloniaTeam.vscode-avalonia --force
else
    warn "VS Code ('code') not found — skipping extension install."
fi

# --- 5. Smoke-test scaffolded project --------------------------------------
SMOKE_DIR="${TMPDIR:-/tmp}/avalonia-smoketest-$$"
log "Smoke-testing template build in $SMOKE_DIR ..."
mkdir -p "$SMOKE_DIR"
trap 'rm -rf "$SMOKE_DIR"' EXIT

(
    cd "$SMOKE_DIR"
    dotnet new avalonia.app -o SmokeTest >/dev/null
    cd SmokeTest
    dotnet build -nologo -v quiet >/dev/null
)
log "Smoke test passed — Avalonia builds cleanly."

# --- 6. Done ---------------------------------------------------------------
cat <<'EOF'

------------------------------------------------------------
Avalonia UI development is ready.

Create a new app:
  dotnet new avalonia.app  -o MyApp     # minimal
  dotnet new avalonia.mvvm -o MyApp     # MVVM (ReactiveUI)
  dotnet new list avalonia              # all templates

Then:
  cd MyApp && dotnet run
------------------------------------------------------------
EOF
