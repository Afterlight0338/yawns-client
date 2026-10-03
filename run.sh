#!/usr/bin/env bash
# YAWNS launcher (NixOS).
#
# Builds a self-contained Release on first run (or with --build) and starts it inside the osu FHS runner,
# using the normal lazer library in ~/.local/share/osu.
#
# The library is shared with the regular lazer install, so:
# - YAWNS must be built from the same lazer release as the installed lazer (database schema), see LAZER_VERSION,
# - only one of the two may run at a time.
#
# Taiko/catch/mania, mobile, macOS, tournament, benchmarks and templates are deleted from this fork.
# After merging a new upstream tag, upstream edits to those files show as modify/delete conflicts; keep them deleted with:
#   git status --porcelain | awk '/^(DU|UD)/{print $2}' | xargs -r git rm -q

set -euo pipefail
cd "$(dirname "$(readlink -f "$0")")"

OUT=osu.Desktop/bin/publish
# The lazer release YAWNS is built from (OsuGameBase.LAZER_VERSION).
LAZER_VERSION=$(grep -oP 'LAZER_VERSION = "\K[^"]+' osu.Game/OsuGameBase.cs)
DATA="$HOME/.local/share/osu"

if [[ "${1:-}" == "--build" || ! -x "$OUT/yawns" ]]; then
    [[ "${1:-}" == "--build" ]] && shift
    # Start clean: publish never deletes stale files, and leftover ruleset DLLs would still be loaded.
    rm -rf "$OUT"
    nix-shell -p dotnet-sdk_10 --run "DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet publish osu.Desktop -c Release -r linux-x64 --self-contained -o $OUT"
fi

if pgrep -x 'osu!' >/dev/null || pgrep -x yawns >/dev/null; then
    echo "lazer or YAWNS is already running. Close it first, both use the same library." >&2
    exit 1
fi

# If the regular lazer has already run a newer release, its database may be newer than this build understands.
# lazer would then move the library aside and start empty, so refuse instead.
installed=$(sed -n 's/^Version = //p' "$DATA/game.ini" 2>/dev/null | tr -d '\r')
if [[ -n "$installed" && "$installed" != "$LAZER_VERSION" ]]; then
    echo "Your lazer last ran $installed, but YAWNS is built from lazer $LAZER_VERSION." >&2
    echo "Update YAWNS to $installed first (merge that release, set LAZER_VERSION, then ./run.sh --build)." >&2
    exit 1
fi

# One-time copy of the library database from before YAWNS first opened it.
if [[ -f "$DATA/client.realm" && ! -f "$DATA/client.realm.before-mappinglazer" ]]; then
    cp "$DATA/client.realm" "$DATA/client.realm.before-mappinglazer"
    echo "Backed up $DATA/client.realm to client.realm.before-mappinglazer"
fi

# SDL3 does not accept comma-separated driver lists, let it pick Wayland/X11 itself.
unset SDL_VIDEODRIVER

exec osu-fhs-runner -c "exec \"$PWD/$OUT/yawns\" \"\$@\"" -- "$@"
