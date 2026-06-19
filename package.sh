#!/usr/bin/env bash
#
# Build the mod, assemble a CLEAN copy (no .git/obj/Source), and deploy it as
# REAL files into the RimWorld Mods folder.
#
# Why real files instead of a symlink: RimWorld loads a symlinked mod fine, but
# its Steam Workshop uploader only shows the "Upload to Steam Workshop" button
# for a genuine local folder. So we copy, not link.
#
# Usage:  ./package.sh
# Override the Mods folder if needed:  RIMWORLD_MODS=/path/to/Mods ./package.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/release/TileBiomeEditor"
MODS_DIR="${RIMWORLD_MODS:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods}"
DEST="$MODS_DIR/TileBiomeEditor"

echo "==> Building (Release)"
dotnet build "$ROOT/Source/TileBiomeEditor.csproj" -c Release

# PublishedFileId.txt links the folder to its Workshop item. Find an existing
# one in priority order: committed repo copy, then the deployed copy that Steam
# writes on first upload, then a prior release.
PFID=""
for f in "$ROOT/About/PublishedFileId.txt" "$DEST/About/PublishedFileId.txt" "$OUT/About/PublishedFileId.txt"; do
  if [ -f "$f" ]; then PFID="$(tr -d '[:space:]' < "$f")"; break; fi
done

echo "==> Assembling clean mod -> $OUT"
rm -rf "$OUT"
mkdir -p "$OUT/Assemblies"
cp -R "$ROOT/About"    "$OUT/About"
cp -R "$ROOT/Textures" "$OUT/Textures"
cp    "$ROOT/Assemblies/"*.dll "$OUT/Assemblies/"
for d in Languages Defs Patches Sounds; do
  [ -d "$ROOT/$d" ] && cp -R "$ROOT/$d" "$OUT/$d"
done
rm -f "$OUT/Assemblies/.gitkeep"

# Persist the Workshop link into both the release and the (committable) repo
# About/ folder so future uploads keep targeting the same item.
if [ -n "$PFID" ]; then
  printf '%s' "$PFID" > "$OUT/About/PublishedFileId.txt"
  printf '%s' "$PFID" > "$ROOT/About/PublishedFileId.txt"
  echo "==> Workshop link: $PFID"
fi

if [ -d "$MODS_DIR" ]; then
  echo "==> Deploying real files -> $DEST"
  rm -rf "$DEST"            # also removes any old symlink
  cp -R "$OUT" "$DEST"
else
  echo "==> Mods folder not found ($MODS_DIR) — skipped deploy"
fi

echo "==> Done."
