# Third-party notices

Underworld Revisited is licensed under the MIT License (see `LICENSE`). It contains
code derived from the following projects, which are distributed under their own
licenses. Their copyright notices and license texts are reproduced below as those
licenses require.

No game data of Ultima Underworld is included. You need your own copy of the game. The
screenshots in `Screenshots/` show the original game's artwork; it belongs to the owners of
the Ultima Underworld rights and is not covered by the MIT License.

## ymfm

- Project: https://github.com/aaronsgiles/ymfm
- Used in: `Assets/UWDataImport/UWData/UWOpl2.cs` (a C# port of the YM3812 / OPL2
  emulation; the file carries the license text in its header)
- License: BSD 3-Clause

```
BSD 3-Clause License

Copyright (c) 2021, Aaron Giles
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## UnderworldGodot

- Project: https://github.com/hankmorgan/UnderworldGodot
- Used in: game rules, formulas and data tables in many files under
  `Assets/UWDataImport` and `Assets/UWScripts`, among them `UWCombat`, `UWCritterLoot`,
  `UWExperience`, `UWRunicMagic`, `UWCharacterGeneration`, `UWCritter`, `UWSleep`,
  `UWMusic`, `UWCreatureRespawner`, `UWDamageTypes`, `UWEnchantment`, `UWTvfxVoice`,
  `UWAdlibBank` and `UWGameUI`. Where source comments speak of "the reference", they mean
  UnderworldGodot.
- License: MIT

```
MIT License

Copyright (c) 2023 hankmorgan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## PDFium

- Project: https://pdfium.googlesource.com/pdfium/, built by
  https://github.com/bblanchon/pdfium-binaries (release chromium/8066, PDFium 156.0.8066.0)
- Used in: `Assets/Plugins/PDFium` (the unmodified native libraries of that release:
  `pdfium.dll` for Windows x64 and `libpdfium.so` for Linux x64), called from
  `Assets/UWScripts/UWPdfium.cs` to show the game's manual in the help window. The manual
  itself is not included; it is read from the player's GOG installation.
- License: PDFium under BSD 3-Clause and Apache 2.0; the build scripts of pdfium-binaries
  under MIT. The library contains further components, each under its own license:
  Abseil (Apache 2.0), AGG 2.3, fast_float (MIT), FreeType (FreeType License), ICU (Unicode
  License), Little CMS (MIT), libjpeg-turbo (IJG and BSD 3-Clause), OpenJPEG (BSD 2-Clause),
  libpng (libpng License), LLVM libc (Apache 2.0 with LLVM exception), simdutf (MIT) and
  zlib (zlib License).
- The full license texts are in `ThirdParty/PDFium/LICENSE` and
  `ThirdParty/PDFium/licenses/`, as distributed with the binaries.

Portions of this software are copyright (c) 2006 The FreeType Project
(www.freetype.org). All rights reserved.

This software is based in part on the work of the Independent JPEG Group.

## Lexend Exa

- Project: https://github.com/googlefonts/lexend, taken from
  https://github.com/google/fonts (`ofl/lexendexa/LexendExa[wght].ttf`), unmodified.
- Used in: `Assets/Resources/Fonts/LexendExa.ttf`, the optional modern font of the game
  texts (`Assets/UWScripts/UWTextLabel.cs`). Chosen because its width comes closest to the
  original message font.
- License: SIL Open Font License 1.1, Copyright 2018 The Lexend Project Authors, with
  Reserved Font Name "RevReading Lexend". The full text is in `ThirdParty/Lexend/OFL.txt`.

## Liberation Sans

- Project: Liberation Fonts 2.1.5 (https://github.com/liberationfonts), `LiberationSans-Regular.ttf`
  unmodified, taken from the Ubuntu package fonts-liberation2 2.1.5-1.
- Used in: `Assets/Resources/Fonts/LiberationSans-Regular.ttf`, the font of the port's own
  windows (menu bar, help window, message log), loaded by `Assets/UWScripts/UWInterfaceFont.cs`.
  Chosen because it has the metrics of Arial, which Linux lacks.
- License: SIL Open Font License 1.1, Digitized data copyright (c) 2010 Google Corporation with
  Reserved Font Arimo, Tinos and Cousine; Copyright (c) 2012 Red Hat, Inc. with Reserved Font
  Name Liberation. The full text is in `ThirdParty/Liberation/OFL.txt`.

## Kenney Input Prompts Pixel

- Project: "Input Prompts Pixel" 1.0 by Kenney (www.kenney.nl), the packed tile sheet,
  unmodified (`Tilemap/tilemap_packed.png`).
- Used in: `Assets/Resources/UWGlyphs/KenneyInputPromptsPixel.bytes` (the PNG under another
  extension, read by `Assets/UWScripts/UWGlyphs.cs`), the input glyphs of the interface.
- License: Creative Commons Zero (CC0). The licence file is in `ThirdParty/Kenney/License.txt`.

## MeltySynth

- Project: https://github.com/sinshu/meltysynth, version 2.4.1 (commit 4ba079c), built
  unmodified from its source as a .NET Standard 2.1 library.
- Used in: `Assets/Plugins/MeltySynth/MeltySynth.dll`, the General MIDI synthesizer of the
  music (`Assets/UWDataImport/UWData/UWGmMusicDriver.cs`).
- License: MIT, Copyright (C) 2021 Nobuaki Tanaka; it carries the notices of the code it builds
  on, C# Synth (Copyright (C) 2014 Alex Veltsistas) and TinySoundFont (Copyright (C) 2017, 2018
  Bernhard Schelling, based on SFZero, Copyright (C) 2012 Steve Folta), under the same MIT
  licence. The full text is in `ThirdParty/MeltySynth/LICENSE.txt`.

## Munt (libmt32emu)

- Project: https://github.com/munt/munt, libmt32emu 2.8.3 (commit 6e7c01f), Copyright (C)
  2003-2009 Dean Beeler, Jerome Fisher, (C) 2011-2026 Dean Beeler, Jerome Fisher, Sergey V.
  Mikayev; the authors are listed in `ThirdParty/Munt/AUTHORS.txt`.
- Used in: `Assets/Plugins/mt32emu/Windows/x86_64/mt32emu.dll` and
  `Assets/Plugins/mt32emu/Linux/x86_64/libmt32emu.so`, built UNMODIFIED from that source as
  shared libraries with its C interface (`Tools/Munt/build-windows.bat` and
  `Tools/Munt/build-linux.sh` with `Tools/Munt/config.h`), loaded at run time and called only through that interface by
  `Assets/UWDataImport/UWData/UWMt32MusicDriver.cs`. It may be replaced by any compatible build.
  The emulation needs the ROMs of a Roland MT-32 or CM-32L, which are NOT included.
- License: GNU Lesser General Public License 2.1 or later; the text is in
  `ThirdParty/Munt/COPYING.LESSER.txt`. The complete source the library was built from is in
  `ThirdParty/Munt/munt-src-6e7c01f.zip`.

## Fluid (R3) General MIDI SoundFont

- Project: FluidR3_GM.sf2 by Frank Wen, taken from the Debian package fluid-soundfont-gm 3.1-6.
- Used in: `Assets/StreamingAssets/UWMusic/UWGeneralMidi.sf2`, cut from it by
  `Tools/UWSoundFontTrim` to the presets and key ranges the game's music plays; nothing inside
  a kept zone is changed.
- License: MIT, Copyright (c) 2000-2002, 2008 Frank Wen. The readme with the contributors and
  the licence text are in `ThirdParty/FluidR3/License.txt`.

## Acknowledgements

These sources were used as documentation only; no code was taken from them.

- **uw-formats.txt**, the community description of the Ultima Underworld file formats.
  The first version of this project (2012) was written from it.
- **hankmorgan's reverse engineering of UW.EXE**
  (https://github.com/hankmorgan/UWReverseEngineering): his disassembly with named routines
  and his guide to the mechanics. Most of the game rules in this project, among them the
  motion code, the creature AI, traps and magic, were read from it and rewritten. The
  disassembly itself is not part of this project.
