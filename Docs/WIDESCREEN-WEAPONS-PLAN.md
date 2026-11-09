# Widescreen weapons: plan

GitHub issue 16 (2026-11-06): an artist offers hand-drawn widescreen extensions of the weapon and
hand pictures, so that the weapon no longer ends at the edge of the original's 4:3 view on a wide
screen. This plan is the architecture, decided with the user on 2026-11-07. Nothing of it is built
yet; the first step is the mod folder (section 4).

## 1. The legal line

The repository and the releases hold no picture of the original, and no pack ships with the game.
A pack holds only the pixels the artist added; the original's pixels come at runtime from the
player's own installation, exactly as every other picture of the game does. The pack has its own
licence and credit (the artist's), which the game shows while the pack is active. GZDoom's WidePix
is no precedent for bundling: Doom's LICENSE.DOC allowed id's images in mods, EA gave no such
permission for Ultima Underworld.

## 2. What is there today

- **UW1:** `UWWeaponView` gives the frame of WEAPONS.GR (palette indices, `UWTexture`) and its
  place from the original's offsets (`GetWeaponOffset`). Three views draw it:
  - the classic scheme (`UWClassicHud`): inside the frame's view window, the frame covers the rest;
  - the modern scheme (`UWModernWeaponView`): the original's 172 x 112 view mapped onto the screen,
    its height 0.8 of the screen's (`WeaponScale`), pixel aspect 1/1.2, NOT clipped - on a wide
    screen the picture's cut edge shows, which is what the pack fills;
  - Classic Wide (`UWModernWeaponView` under the frame's weapon layer): the wider hole of the frame.
- **UW2:** WEAP.GR has 16 colour slots that WEAP.CM turns into the body's skin colours
  (`UWGameScreenUw2.fWeaponPicture`, load_weapcm). The modern HUD (`UWModernHudUw2.ShowWeapon`)
  clips the weapon to the 4:3 view on purpose.
- **WEAPONS.GR's layout (UW1):** 8 blocks of 0x1C pictures - sword, axe, mace, fist, right hand
  0x00-0x6F, then the left hand 0x70-0xDF, the mirror of the right (one stray pixel in 0x35);
  per block 0x00-0x08 slash, 0x09-0x11 bash, 0x12-0x1A thrust (4 wind-up and 5 swing frames
  each), 0x1B ready; 1x1 pictures are empty placeholders.
- **WEAP.GR's layout (UW2):** pictures from (1 - lefty) * 0x7C + kind * 0x1F
  (`UWClassicLayoutUw2`).
- `uwdump pictures` exports a picture file as PNG and `uwdump palette` (0.16.6) the palettes, both
  from the player's own installation.

## 3. Decisions (user, 2026-11-07)

1. **The mod folder** of Docs/MOD-SUPPORT-PLAN.md, phase 0, built now as far as this needs:
   a catalog and a `mod.json` per mod; the Mods window comes later.
   - A mod is a folder OR a zip in `Mods/`, read in place (System.IO.Compression), never unpacked.
     Players drop the zip in; the artist works in a folder and sees a saved picture at the next load.
   - Folder and zip names are free; a mod may lie one level deeper (`Mods/Graphics/<mod>/`), and a
     zip may hold one top folder. Files of different mods never clash, each is read in its own
     container.
   - The mod's identity is its `id` in `mod.json`: lower case letters, digits, `-` and `.`, by
     convention `author.mod-name`; it stays the same over versions (`version` is its own field),
     as settings.json will name enabled mods by it. Two mods with one id: the folder wins over the
     zip, with a warning in the log. A mod without a valid id is not loaded (log).
   - Two mods that replace the same picture: the later in the load order wins; until the Mods
     window, the order is alphabetical by id.
2. **Active when installed.** No option of its own before the Mods window (it would have to go
   again); to compare with the original, take the mod out of the folder. The log and the debug
   display say which mods are loaded.
3. **The artist paints on whole pictures**, the original included, and `uwdump pack-strip`
   (section 5) takes out every pixel equal to the original and writes the shift. The pack holds
   only new pixels without the artist having to cut them out.
4. **No target width.** The loader takes any size and any shift per picture. A picture is drawn as
   far as it is painted; where the painting ends, it ends at an edge as today, and a wider screen
   shows that edge further out. What leaves the picture sideways (an arm at the 4:3 view's side)
   needs the extension up to the screen's edge or turned down out of the bottom; the guide
   (section 5) shows where. As a hint for the artist: about 63 pixels a side cover 16:9, about 110
   cover 21:9 (at the modern scheme's 0.8 scale).
5. **Both games in the format, UW1 built first** (the artist paints UW1). UW2 adds later: a palette
   of WEAP.GR's 16 slots from `uwdump palette`, the loader reading UW2 pictures as those slots, and
   the UW2 modern HUD's clip widened while a pack is active.

## 4. Architecture

### 4.1 The mod folder (engine-free, `Assets/UWDataImport`)

- `UWModSource`: one mod's container - a folder or a zip - listing and reading its files by
  relative path.
- `UWModCatalog`: scans the mod folder (and one level below), reads every `mod.json`, checks the
  id, resolves duplicates (folder before zip) and the order (alphabetical until the Mods window),
  and reports what it found and refused.
- `UWModSet`: the mods in force, in order - handed to the data root like the game-text pack, and
  part of its identity (the data is rebuilt when the set changes).
- The folder: `<persistentDataPath>/Mods`, beside settings.json (decided by the user 2026-11-07,
  MOD-SUPPORT-PLAN.md open decision 1): one folder for both games (`mod.json`'s `games` says which
  a mod is for), the same for the Steam and the GOG game, untouched by unpacking a new release.
- In the game, a line with the mods loaded (or "none") and an **Open folder** button that makes
  the folder if it is missing - the line the MT-32 ROMs and the recorded music have
  (`UWSetupMenu.fDrawFolderLine`, with `UWAudioMixer.Mt32RomFolder` as the model), on the Game
  page until the Mods window takes it over (per user, 2026-11-07).

`mod.json`:

```json
{
  "id": "author.widescreen-weapons",
  "name": "Widescreen Weapons",
  "version": "1.0",
  "author": "author",
  "kind": "pictures",
  "games": ["uw1"],
  "licence": "LICENSE.txt",
  "credit": "Widescreen weapons by author",
  "requires": []
}
```

- `requires` (added 2026-11-08 per user, after OpenXcom's `master`): the ids of mods this one
  needs, each optionally with a lowest version (`"author.widescreen-weapons >= 1.0"`). A mod whose
  requirement is missing or too old is not loaded, with a line in the log and the debug display;
  one that is loaded comes after what it requires in the order. A shield pack for issue 21 that
  builds on the widescreen weapons would name them here.

### 4.2 The pack's pictures

```
pictures/UW1/WEAPONS.GR/000.png ... 0DF.png
pictures/UW1/WEAPONS.GR/extend.json
pictures/UW2/WEAP.GR/...            (later)
```

- One PNG per picture, named by its index in the file (hex, three digits). A picture the pack does
  not have stays the original's.
- Indexed or RGB PNG in the game's palette (UW1: palette 0, `uwdump palette`); UW2: the 16 slots.
  Every colour must be one of the palette's (`pack-check`); transparent where nothing is painted
  AND where the original lies.
- `extend.json`: where the original lies in the larger picture, `left` and `top` in pixels - a
  default for the file and per picture where it differs:

```json
{ "default": { "left": 0, "top": 0 }, "pictures": { "012": { "left": 40, "top": 0 } } }
```

- **The shift in the PNG itself** (added 2026-11-08 per user): a picture may carry it in a `grAb`
  chunk, as GZDoom's sprites do and as SLADE writes it - the artist of issue 16 comes from Doom's
  WidePix. Read as `left` and `top`, the shift of the extension beyond the original (not Doom's
  absolute draw offset). Precedence: the picture's own entry in `extend.json`, then its `grAb`,
  then `extend.json`'s default, then 0, 0. `pack-check` warns where `grAb` and `extend.json` say
  different things.
- Left-handers: a left-hand picture the pack lacks is the mirror of the right hand's extension,
  with its shift mirrored, as the original's left half mirrors its right. The artist paints 112
  pictures, not 224.

### 4.3 Composing (engine-free)

`UWPictureExtension` builds, per picture, an extended `UWTexture`: the original's pixels from the
player's file at (left, top), the pack's pixels around them; where both have a pixel, the
original's wins. It stays palette indices, so the palette effects, the colour modes and UW2's
skin colours act on the extension as on the original. The originals stay untouched; the extended
set lives beside them with each picture's shift.

### 4.4 The views (Unity side)

- `UWWeaponView` offers the extended picture and its shift beside the original.
- `UWModernWeaponView` (modern scheme and Classic Wide) draws the extended picture where there is
  one, at the original's offset minus the shift.
- The classic scheme keeps the original: the frame covers everything outside the view anyway.
- UW2 (later): `UWGameScreenUw2.fWeaponPicture` composes from the extended set,
  `UWModernHudUw2.ShowWeapon` clips to the screen instead of the 4:3 view while a pack is active.
- The pack's credit in the help's About or the debug display while it is active.

## 5. Tools (uwdump, in the translation kit)

- `uwdump weapon-frames [path] --out dir [--both-hands]`: BUILT 2026-11-09 (issue 16: the painter's
  file manager sorted the hex names out of order) - WEAPONS.GR sorted as UWWeaponAnimation reads it,
  a folder per weapon, files numbered in the order of the moves and named after their picture, the
  right hand only unless asked, the 1x1 placeholders left out.

- `uwdump weapon-guide [path] --out dir`: every weapon picture at its place in the view, on a 21:9
  canvas with lines for the 4:3 view and 16:9 - which pictures touch an edge and where an arm
  leaves the view. Made from the player's own installation, like `uwdump pictures`.
- `uwdump pack-strip [path] <painted dir> --out <pack dir>`: whole painted pictures in, only the new
  pixels and `extend.json` out (decision 3); finds the original in the painted picture by its
  pixels, warns where the painting changed an original pixel.
- `uwdump pack-check [path] <pack dir or zip>`: `mod.json` and the id, `requires`, file names, colours
  outside the palette, overlaps with the original, missing pictures, sizes, a `grAb` that disagrees
  with `extend.json` - a report, nothing changed.
- The kit's README gets a section for pack authors (the id rule, the layout, the three commands).

## 6. Steps

1. BUILT 2026-11-09: The mod folder: `UWModSource`, `UWModCatalog`, `UWModSet`, the folder, the Game page's line
   with its Open folder button, the log line and the debug display's list; self-check with a made-up folder and zip (ids, duplicates, order, one
   level deeper, a zip with a top folder). Also `UWModManifest` (ours and the editor's format 2, `requires` with
   a lowest version, rings refused) and `UWJsonReader`; check ModCatalog. The editor's `.uwmod` packages are
   listed already; applying their deltas is a step of its own.
2. `UWPictureExtension` and the extended set in `UWTextures`; self-check with a made-up pack (the
   shift, the original winning, the mirror for the left hand).
3. The UW1 views (`UWWeaponView`, `UWModernWeaponView`), the credit.
4. `uwdump weapon-guide`, `pack-strip`, `pack-check`, the kit's README; the artist's first frames
   as the test (never into the repository).
5. Later, when someone paints for UW2: the 16-slot palette, UW2's composing and its HUD's clip.

## 7. Not in this plan

The Mods window (load order, on/off), other kinds of mods (rules, levels, texts), and pictures
other than the weapons - they follow MOD-SUPPORT-PLAN.md and reuse the mod folder built here.
