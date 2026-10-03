#!/usr/bin/env bash
# YAWNS: for prebuilt releases only. Torus, Torus-Alternate and Venera need a commercial licence to redistribute,
# so a release builds osu.Game.Resources from source without them (YAWNS uses Inter instead, see OsuFont.cs).
# Run inside a throwaway copy of the repo, not the live checkout: it rewrites osu.Game.csproj, osu.sln and osu.Desktop.slnf.
set -euo pipefail
cd "$(dirname "$0")"

VERSION=$(grep -oP 'ppy.osu.Game.Resources" Version="\K[^"]+' osu.Game/osu.Game.csproj)
rm -rf ../osu-resources
git clone -q --depth 1 --branch "$VERSION" https://github.com/ppy/osu-resources ../osu-resources
rm -rf ../osu-resources/osu.Game.Resources/Fonts/{Torus,Torus-Alternate,Venera}

./UseLocalResources.sh
echo "osu.Game.Resources $VERSION from source, without Torus/Venera."
