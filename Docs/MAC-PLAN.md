# macOS: plan

A macOS release of the game, asked for by the user on 2026-11-09. The developer has no Mac and
nobody nearby with one; the PC is Windows 11 on an AMD CPU. The Linux build showed that a new
platform needs its own code, so nothing goes out untested on real Apple hardware. Nothing of this
plan is built yet. Everything below marked "verify" is not checked yet and has to be checked before
it is relied on.

## 1. Why not a macOS virtual machine

A macOS VM on this PC (VMware with an unlocker and a CPUID mask, or Docker-OSX under WSL2) was
looked at and dropped:

- **Only the Intel path.** Every Mac sold since 2020 is Apple Silicon (arm64). An x86 VM cannot
  test the arm64 player, the arm64 plugins or the signing rule that only arm64 enforces (section 4).
- **No Metal.** Unity's macOS player renders with Metal. VMware's and QEMU's macOS guests have no
  Metal device, so the player most likely does not start or stays black (verify only if it costs
  nothing, it is not worth an evening).
- **macOS on a non-Apple machine** goes against Apple's licence, and the AMD kernel patches make
  every macOS update a gamble.

The test machine is therefore a real Mac borrowed for a day, plus an automatic start check on
GitHub's macOS runners (section 5).

## 2. Building: on Windows, with Mono

Unity builds a macOS player on Windows with the scripting backend Mono (the Hub's module "Mac
Build Support (Mono)"). Only IL2CPP needs a Mac, and the game uses Mono on every platform. No CI
build, no Game-CI, no Unity licence in GitHub secrets.

- Target: Intel 64-bit + Apple silicon (one universal `UR.app`). That the architecture choice is
  offered with a Windows host: verify in step M0.
- Output `Build/Mac/UR.app`, beside `Build/Linux` (the win64 zip leaves out `Mac` as it leaves
  out `Linux`).
- Lowest macOS version: whatever Unity 6000.6 needs (verify in Unity's system requirements).

## 3. What the code has today

Found in the code on 2026-11-09:

- **Native libraries, Windows and Linux only:** `Assets/Plugins/mt32emu` (Munt's libmt32emu,
  built by `Tools/Munt/build-windows.bat` and `build-linux.sh` from `ThirdParty/Munt`) and
  `Assets/Plugins/PDFium` (prebuilt, version 156.0.8066). MeltySynth is a managed DLL and runs
  as it is. Without the two the Mac build has no MT-32 music and no manuals in the help.
  `UWMt32MusicDriver` and `UWPdfium` import by the bare names `mt32emu` and `pdfium`, which Unity
  resolves to `libmt32emu.dylib` and `libpdfium.dylib` (verify with the first build).
- **The folder beside the program:** `UWUserFolder.ProgramFolder` (portable mode, `UserData`,
  `portable.txt`) and `UWLanguage.GameFolder` (`Languages`) are
  `Path.GetDirectoryName(Application.dataPath)`. On macOS `dataPath` is `UR.app/Contents`, so both
  land INSIDE the bundle (`UR.app/`): writing there breaks the bundle's signature, and a
  quarantined app runs from a read-only copy (App Translocation). On macOS the folder beside the
  program is the one that holds `UR.app`. One rule for both (UWUserFolder), not two checks.
  The Mods folder is `persistentDataPath/Mods` unless portable, so it follows the same rule.
- **Where the game is looked for:** `UWGogInstall.GetCandidateFolders(bool pbLinux)` and
  `GetSteamLibraries(bool pbLinux)` know Windows and Linux; `UWSettings.GetCandidateGogPaths`,
  `Tools/Shared/DataPath.cs` and `UWGameFolderBrowser` pass that bool. macOS needs a third case:
  the GOG Mac release's place and layout (unknown, step M0) and Steam's
  `~/Library/Application Support/Steam` with its `libraryfolders.vdf`. The bool becomes an
  operating-system value in one place.
- **`UWOsKeys`** (the gamepad letter grid types real keys): SendInput on Windows, XTest on Linux,
  nothing on macOS. A macOS version (CGEvent) needs the player to grant Accessibility access in
  the system settings. Decision 3.
- **Ctrl on the Mac:** paste in the kit rows (`UWKitRows`) is Ctrl+V, Mac users press Cmd+V.
  The game's own keys bound to `leftCtrl` (descend in `UWControls`) stay, but on macOS Ctrl+click
  is a right click: check that descending while clicking still works (step M4).
- **Linux workarounds** (`UWIntroPlayer`: the pointer hidden again twice a second during
  sequences) are `UNITY_STANDALONE_LINUX` only; whether the Mac needs the same shows in M4.
- **The release script** `Tools/Release/MakeReleaseZip.ps1` knows `win64` and `linux64`.

## 4. Packing and signing

Without a paid Apple developer account the app is not notarised. Two things still have to be
right, or the player sees "UR is damaged" whatever they do:

- **The executable bit.** A zip made on Windows has none, so `UR.app/Contents/MacOS/UR` would not
  start. The archive is made in WSL (or with the GNU tar of Git for Windows, as for Linux) and
  sets it. A zip keeps it when made with Info-ZIP in WSL; macOS's own unzip handles both zip and
  tar.gz (verify which keeps the bundle intact on a double click).
- **An ad-hoc signature.** arm64 code on Apple Silicon only runs signed; ad-hoc (no identity) is
  enough. Unity's player may come signed, but adding the plugins and changing `Info.plist` makes
  the signature invalid. `rcodesign` (the apple-codesign project, runs on Linux) signs the whole
  bundle ad-hoc in WSL. Decision 2 has the alternative.

For the players (README, release notes, website): the first start is refused by Gatekeeper. Since
macOS 15 the old right-click > Open no longer helps; the way is System Settings > Privacy &
Security > Open Anyway, or once in the Terminal `xattr -cr <path to UR.app>`, which also ends the
read-only App Translocation copy.

## 5. Testing

Two kinds, both before the first Mac release:

- **An automatic start check on GitHub's macOS runners** (Apple Silicon, free for the public
  repository). It needs no game data and must never get any (the original's files never go to
  GitHub, not even as a secret). A new command-line argument `-platform-check` makes the player
  log the operating system, the architecture, `dataPath`, `persistentDataPath`, the folder beside
  the program, the game folders it would look in, and load both native libraries (mt32emu's
  version, PDFium's init and destroy), then quit with an exit code. The workflow runs it once
  natively and once under Rosetta (`arch -x86_64`, the Intel slice), and keeps the log. Whether the
  runner can render at all (Metal in its VM) is unknown; a screenshot with `screencapture` shows
  it, and if it cannot, the check still covers start, paths and plugins. How the built app gets
  to the runner: decision 4.
- **An interactive test on a rented Mac** (Apple Silicon, for example Scaleway, AWS EC2 Mac or
  MacinCloud, used over remote desktop; Apple's licence makes 24 hours the shortest rental). The
  user installs their own GOG (or Steam) copy there and works through a Mac test list: the GOG
  and Steam game found, first start and data extraction, intro and logos, both schemes, gamepad,
  MT-32 and General MIDI, the manuals, save and load (and the DOSBox round trip with the saves
  copied over), Retina and window modes, pointer lock, Cmd+Q, portable mode beside the app, a
  language pack, a mod. One rental at the first release; later releases need it only when Mac
  code changed.

After the first release, Mac players are asked for reports (an issue "Mac testers wanted", the
forums), as the Linux build was.

## 6. Steps

- **M0 Facts, no code.** Install the Hub module "Mac Build Support (Mono)" for 6000.6.8f1; check
  the build profile offers the universal architecture on Windows; build once, unchanged, and look
  at the bundle (`Contents/MacOS`, `Contents/Plugins`, `Info.plist`, whether Unity signed it).
  Download GOG's Mac installer of Ultima Underworld 1+2 on Windows (if GOG has one: verify) and
  open it with 7-Zip: where `game.gog` lies and how the bundle is laid out, for the search list.
  Check whether Steam's release has a macOS version. Read Unity 6000.6's lowest macOS version.
- **M1 The two libraries.** `Tools/Munt/build-macos.sh` (clang, `-arch x86_64 -arch arm64`,
  the same unmodified source and `config.h`) run by a GitHub Actions workflow on a macOS runner,
  the dylib taken from the workflow's artifact and checked in under
  `Assets/Plugins/mt32emu/macOS`. PDFium 156.0.8066 for macOS from the same source as the Windows
  and Linux files (a universal file, or the two architectures joined with `lipo` on the runner),
  under `Assets/Plugins/PDFium/macOS`. Import settings: Standalone OSX, any CPU, not the editor.
  BUILDING.md and THIRD_PARTY_NOTICES.md updated.
- **M2 The code.** The folder beside the program as one rule in UWUserFolder (the folder holding
  `UR.app` on macOS), UWLanguage reading it; the operating system instead of `pbLinux` in
  UWGogInstall, the GOG Mac and Steam Mac places added; Cmd as Ctrl for the text shortcuts;
  UWOsKeys as decision 3 says; `-platform-check`. A check in the engine-free layer for the macOS
  candidate folders (with a made-up home folder).
- **M3 Packing.** `MakeReleaseZip.ps1 -Platform macos`: stages `UR.app` with the licences and the
  README, refuses original data as for the others, signs ad-hoc and packs in WSL with the
  executable bit, writes `UnderworldRevisited-<version>-macos.zip` (or `.tar.gz`). The
  translation kit for macOS (uwdump published for `osx-arm64` and `osx-x64`, signed the same way)
  can come later.
- **M4 Tests.** The start check workflow (section 5) green on arm64 and under Rosetta; then the
  rented Mac with the test list. Findings fixed and checked again on the same rental day where
  possible.
- **M5 Release.** The Mac archive beside win64 and linux64 in the next release that has UW1
  changes anyway, marked as new and experimental; README and website with the Gatekeeper steps;
  the call for Mac testers.

## 7. Open decisions

1. **Rented Mac or testers only.** A day's rental costs money; asking the community first is free
   but slow and puts an untested build in front of people. Proposed: one rental for M4.
2. **Signing in WSL with rcodesign, or on the macOS runner with Apple's codesign.** The runner
   would mean the release archive comes out of CI. Proposed: WSL, so the release stays on this
   machine as for Windows and Linux; the runner builds only the libraries and runs the check.
3. **The gamepad letter grid on macOS.** CGEvent typing needs Accessibility access granted by
   the player; feeding the characters straight into our text inputs needs none but means a second
   way beside the real key presses. Proposed: look first whether every text input can take the
   grid's characters directly; if so, use that on macOS only.
4. **How the built app reaches the start check.** A draft (unpublished) GitHub release holding the
   archive, which the workflow downloads; or a workflow artifact uploaded with `gh`. Proposed: the
   draft release, it is the same archive that is later published.
5. **Which releases get a Mac test.** Proposed: the first one fully (M4), later ones the start
   check every time and a rental only when Mac-specific code or the plugins changed, as the Linux
   VM test is kept for the releases that need it.
