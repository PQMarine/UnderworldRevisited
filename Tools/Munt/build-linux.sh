#!/bin/sh
# Builds Munt's libmt32emu (LGPL 2.1 or later) UNMODIFIED as libmt32emu.so for Linux x86-64 -
# the library UWMt32MusicDriver calls. The source is ThirdParty/Munt/munt-src-6e7c01f.zip
# (github.com/munt/munt, commit 6e7c01f, libmt32emu 2.8.3):
#
#   sh build-linux.sh <munt-src-6e7c01f.zip> <output folder>
#
# The same settings as build-windows.bat (config.h beside this file): C interface only, internal
# resampler, hidden symbols but the C API, libstdc++ and libgcc linked in so the library does not
# depend on the C++ runtime of the system it runs on - and kept private (--exclude-libs): exported,
# their 4000 symbols clashed with the player's own at exit, "free(): invalid size" on every quit
# (2026-10-09) - symbols stripped. Build it on the oldest system
# to be supported (the shipped one on Ubuntu 22.04, glibc 2.35): it then needs at most glibc 2.34.
# Needs g++ and unzip (or python3).
set -e

if [ $# -lt 2 ]; then
  echo "sh build-linux.sh <munt-src-6e7c01f.zip> <output folder>"
  exit 1
fi

HERE=$(cd "$(dirname "$0")" && pwd)
OUT=$(mkdir -p "$2" && cd "$2" && pwd)
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

if command -v unzip >/dev/null 2>&1; then
  unzip -q "$1" -d "$WORK"
else
  python3 -c "import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$1" "$WORK"
fi

SRC="$WORK/munt-6e7c01f/mt32emu/src"

g++ -O2 -fPIC -shared -std=c++11 -fvisibility=hidden \
  -DMT32EMU_WITH_INTERNAL_RESAMPLER -Dmt32emu_EXPORTS \
  -I"$HERE" -I"$SRC" \
  "$SRC/Analog.cpp" "$SRC/BReverbModel.cpp" "$SRC/Display.cpp" "$SRC/File.cpp" "$SRC/FileStream.cpp" \
  "$SRC/LA32FloatWaveGenerator.cpp" "$SRC/LA32Ramp.cpp" "$SRC/LA32WaveGenerator.cpp" "$SRC/MidiStreamParser.cpp" \
  "$SRC/Part.cpp" "$SRC/Partial.cpp" "$SRC/PartialManager.cpp" "$SRC/Poly.cpp" "$SRC/ROMInfo.cpp" \
  "$SRC/Synth.cpp" "$SRC/Tables.cpp" "$SRC/TVA.cpp" "$SRC/TVF.cpp" "$SRC/TVP.cpp" "$SRC/sha1/sha1.cpp" \
  "$SRC/SampleRateConverter.cpp" "$SRC/c_interface/c_interface.cpp" \
  "$SRC/srchelper/srctools/src/FIRResampler.cpp" "$SRC/srchelper/srctools/src/SincResampler.cpp" \
  "$SRC/srchelper/srctools/src/IIR2xResampler.cpp" "$SRC/srchelper/srctools/src/LinearResampler.cpp" \
  "$SRC/srchelper/srctools/src/ResamplerModel.cpp" "$SRC/srchelper/InternalResampler.cpp" \
  -static-libstdc++ -static-libgcc -Wl,--exclude-libs,ALL -s -o "$OUT/libmt32emu.so"

echo "built $OUT/libmt32emu.so ($(nm -D --defined-only "$OUT/libmt32emu.so" | grep -c ' T mt32emu_') C functions)"
