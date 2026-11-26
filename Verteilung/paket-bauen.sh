#!/usr/bin/env bash
# Builds the portable student package from the current source (runs inside the Docker
# container; invoke with: docker compose run --rm paket). Output: artifacts/Tempus_Windows_und_Quellcode.zip
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
git config --global --add safe.directory "$repo"
output="$repo/artifacts"
mkdir -p "$output"
stage="$(mktemp -d "$output/stage.XXXXXX")"
trap 'rm -rf "$stage"' EXIT

dotnet publish "$repo/Tempus.Web/Tempus.Web.csproj" -c Release -r win-x64 --self-contained true -o "$stage/Programm"
cp -r "$repo/Demodaten" "$repo/Lizenzen" "$stage/"
cp "$repo/Verteilung/Start-Tempus.cmd" "$repo/Verteilung/LIESMICH.md" "$stage/"

# The file list from git keeps local data and bin/obj out of the package.
mkdir -p "$stage/Quellcode"
git -C "$repo" ls-files -z -- Tempus.sln Tempus.Core Tempus.Web .gitignore \
    | while IFS= read -r -d '' f; do
        mkdir -p "$stage/Quellcode/$(dirname "$f")"
        cp "$repo/$f" "$stage/Quellcode/$f"
    done

zip_tmp="$output/paket.tmp.zip"
rm -f "$zip_tmp"
(cd "$stage" && zip -qr "$zip_tmp" .)
mv -f "$zip_tmp" "$output/Tempus_Windows_und_Quellcode.zip"
echo "$output/Tempus_Windows_und_Quellcode.zip"
