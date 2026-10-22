#!/usr/bin/env bash
# Builds UxPlay at a pinned commit and gathers it, the GStreamer plugins, and every DLL they load
# into a self-contained folder the tray app runs from:
#
#   <out>/bin/                 uxplay.exe, gst-plugin-scanner.exe, gst-inspect-1.0.exe, all DLLs
#   <out>/lib/gstreamer-1.0/   GStreamer plugins
#
# Receiver.cs points GStreamer at this layout through GST_* environment variables.
# Run it in an MSYS2 UCRT64 shell that has the packages listed in .github/workflows/build.yml.
#
# usage: scripts/bundle-receiver.sh <uxplay-commit> <out-dir>
set -euo pipefail

commit=$1
out=$(realpath -m "$2")
work=$(mktemp -d)
gst_packages=(gstreamer gst-plugins-base gst-plugins-good gst-plugins-bad gst-libav)

echo "::group::Build UxPlay $commit"
git init -q "$work/uxplay"
git -C "$work/uxplay" fetch -q --depth 1 https://github.com/FDH2/UxPlay.git "$commit"
git -C "$work/uxplay" checkout -q FETCH_HEAD
# USE_MDNS selects UxPlay's built-in mDNS responder, so users don't need Apple's Bonjour service.
cmake -S "$work/uxplay" -B "$work/build" -G Ninja -DCMAKE_BUILD_TYPE=Release -DUSE_MDNS=1 -DNO_MARCH_NATIVE=ON
cmake --build "$work/build"
cmake --install "$work/build" --prefix "$work/stage"
echo "::endgroup::"

rm -rf "$out"
mkdir -p "$out/bin" "$out/lib/gstreamer-1.0"

# Copies a PE file into <dest> and every MSYS2 DLL it (transitively) loads into <out>/bin.
copy_with_deps() {
  cp "$1" "$2/"
  ldd "$1" | awk '$3 ~ /^\/ucrt64\// { print $3 }' | xargs -r cp -u -t "$out/bin/"
}

echo "::group::Collect receiver"
copy_with_deps "$work/stage/bin/uxplay.exe" "$out/bin"
copy_with_deps /ucrt64/libexec/gstreamer-1.0/gst-plugin-scanner.exe "$out/bin"
copy_with_deps /ucrt64/bin/gst-inspect-1.0.exe "$out/bin"

for package in "${gst_packages[@]}"; do
  pacman -Qlq "mingw-w64-ucrt-x86_64-$package" | grep -E '/lib/gstreamer-1\.0/[^/]+\.dll$' || true
done | sort -u | while read -r plugin; do
  # Plugins whose optional dependencies aren't installed can't load anyway; leave them out.
  if ldd "$plugin" | grep -qE 'not found|\?\?\?'; then
    echo "skipping $(basename "$plugin"): missing dependencies"
    continue
  fi
  copy_with_deps "$plugin" "$out/lib/gstreamer-1.0"
done
echo "::endgroup::"

cp "$work/uxplay/LICENSE" "$out/UXPLAY-LICENSE.txt"
echo "UxPlay source: https://github.com/FDH2/UxPlay/tree/$commit" > "$out/UXPLAY-SOURCE.txt"

echo "Receiver bundle: $(du -sh "$out" | cut -f1), $(find "$out/lib/gstreamer-1.0" -name '*.dll' | wc -l) plugins, $(find "$out/bin" -name '*.dll' | wc -l) DLLs"
