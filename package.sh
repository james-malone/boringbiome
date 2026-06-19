#!/usr/bin/env bash
#
# Build the mod and assemble a CLEAN copy under release/TileBiomeEditor that
# contains only what a Steam Workshop subscriber needs — no .git, obj/, or
# Source/. The Mods folder is symlinked to this release dir, so running this
# script is the one command for both testing in-game and publishing.
#
# Usage:  ./package.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/release/TileBiomeEditor"

echo "==> Building (Release)"
dotnet build "$ROOT/Source/TileBiomeEditor.csproj" -c Release

# PublishedFileId.txt links the folder to its Workshop item. Preserve it across
# rebuilds — first from a prior release, otherwise from the source About/ folder.
PFID=""
if [ -f "$OUT/About/PublishedFileId.txt" ]; then
  PFID="$(cat "$OUT/About/PublishedFileId.txt")"
elif [ -f "$ROOT/About/PublishedFileId.txt" ]; then
  PFID="$(cat "$ROOT/About/PublishedFileId.txt")"
fi

echo "==> Assembling clean mod -> $OUT"
rm -rf "$OUT"
mkdir -p "$OUT/Assemblies"
cp -R "$ROOT/About"    "$OUT/About"
cp -R "$ROOT/Textures" "$OUT/Textures"
cp    "$ROOT/Assemblies/"*.dll "$OUT/Assemblies/"

# Copy any other standard mod folders if they exist later.
for d in Languages Defs Patches Sounds; do
  [ -d "$ROOT/$d" ] && cp -R "$ROOT/$d" "$OUT/$d"
done

# Drop the .gitkeep placeholder that lives in the source Assemblies/ folder.
rm -f "$OUT/Assemblies/.gitkeep"

# Restore the Workshop link if we had one.
if [ -n "$PFID" ]; then
  printf '%s' "$PFID" > "$OUT/About/PublishedFileId.txt"
  echo "==> Preserved PublishedFileId.txt ($PFID)"
fi

echo "==> Done. Contents:"
find "$OUT" -type f | sed "s|$ROOT/||"
