# Mods for tool makers

For programs that install or switch mods for Underworld Revisited: mod managers, editors that
start the game with a mod, launchers. It describes what the game reads and what a tool may
write. The mod formats themselves are in [MOD-SUPPORT-PLAN.md](MOD-SUPPORT-PLAN.md); the
editor packages (`.uwmod`) are the Underworld Editor (Avalonia)'s mod format 2, read as that
format describes them.

## What the build says about itself: `features.json`

Every build and every release package has a `features.json` beside the executable (`UR.exe`,
on Linux `UR.x86_64`). Read it instead of guessing from file names. An example:

```json
{
  "featuresFormat": 1,
  "name": "Underworld Revisited",
  "version": "0.21.0",
  "executable": "UR.exe",
  "games": ["uw1", "uw2"],
  "preview": ["uw2"],
  "uwmod": { "formats": [2], "games": ["uw1", "uw2"] },
  "mods": { "folder": "Mods", "order": "Mods/order.json", "sameFileInTwoPackages": "laterRefused", "folderArgument": "-mods" },
  "userFolder": {
    "windows": "%USERPROFILE%\\AppData\\LocalLow\\Underworld Revisited\\Underworld Revisited",
    "linux": "~/.config/unity3d/Underworld Revisited/Underworld Revisited",
    "portable": "UserData",
    "portableWhen": ["portable.txt beside the executable", "UserData beside the executable", "-portable"]
  },
  "doc": "https://github.com/PQMarine/UnderworldRevisited/blob/main/Docs/MOD-TOOLS.md"
}
```

- `featuresFormat` goes up only when a field changes its meaning. New fields may appear without it.
- `games` are the games the build runs, `preview` those not yet complete.
- `uwmod.formats` are the editor package formats it applies: the value of `"format"` in a
  package's `mod.json`, numbered as the Underworld Editor (Avalonia)'s own format description
  (`docs/mod-format.md` in its repository) numbers them. A package whose number is not in the
  list is not applied. `uwmod.games` are the games it applies them to.
- `version` is the release, as the main menu shows it.
- A build without the file is older than the first release after 0.20.2. Releases from 0.19.0 on
  apply `.uwmod` packages (format 2).

## Where the mods go

The **user folder** holds the settings, the extracted game and the mods:

- Windows: `%USERPROFILE%\AppData\LocalLow\Underworld Revisited\Underworld Revisited`
- Linux: `~/.config/unity3d/Underworld Revisited/Underworld Revisited`
- **Portable:** the folder `UserData` beside the executable, when a file `portable.txt` or a
  folder `UserData` lies beside it, or the game was started with `-portable`.

Mods lie in its folder **`Mods`**, one level of sub-folders allowed (`Mods/Graphics/<mod>/`). A mod
is a folder with a `mod.json`, a zip with one, or an editor package (`.uwmod`). Folder and file
names are free; the game knows a mod by the **id** in its `mod.json`. A tool may name its files
as it likes, a prefix of its own to recognise them included.

## Switching mods on and off: `Mods/order.json`

**Every mod in the folder is on**, unless `order.json` switches it off. The settings
(`settings.json`) hold nothing about mods; a tool never needs to edit them.

```json
{ "order": ["first.mod", "second.mod"], "disabled": ["some.mod"] }
```

- `order`: the load order by id. Mods it does not name load after those it names, alphabetically
  by id.
- `disabled`: the ids of mods switched off. They are not loaded and count as missing for a mod
  that requires them.
- A mod always loads after the mods it requires (`"requires": ["author.mod >= 1.0"]`), whatever
  the order says. A mod whose requirement is missing, too old or switched off is not loaded.
- No file means every mod on, alphabetically. A file that cannot be read is ignored with a line in
  the log.
- The game's Mods window writes this file at once when the player changes the order or a switch,
  and keeps every id it does not know. A tool should do the same: read it, change only its own ids,
  keep the rest.
- **Changes take effect at the next start of the game or the next load of a saved game.** Writing
  the file while the game runs is safe; the running game does not write it back except from the
  Mods window.

To use one package of a tool and no other of the same tool: put the package in `Mods`, take the
tool's other packages out (or name them in `disabled`), done. To go back to the original game,
remove the tool's packages or name them in `disabled`.

## Editor packages (`.uwmod`)

- A package is applied **whole or not at all**: every original must match the SHA-256 and size
  the package names, every delta and every result too. A package for another release of the game
  is refused, with the reason in the log and in the Mods window.
- The game's files are never written. The game extracts the player's GOG or Steam image into
  `GameData/UW1` (or `UW2`) in the user folder; with packages on, it makes a copy beside it
  (`GameData/UW1-mods`) with the packages applied and reads that. The copy is made anew when the
  packages change. A tool should not write either folder.
- **Two packages that change the same file:** packages apply in load order, and a later package
  changing a file an earlier one changed is **refused** as conflicting (the Mods window says so).
  A tool that combines several mods should send them as one package.
- Both games: the second game's archive blocks (`BLOCK` deltas) are applied too.

## Checking without the game: `uwdump`

`uwdump` is the project's command line tool. It is in the **TranslationKit** packages of every
release (`uwdump.exe` on Windows, `uwdump` on Linux), one file with the .NET runtime inside. It
reads the player's own GOG or Steam installation; `<game>` is that folder, and without it uwdump
looks in the usual places. `--game uw2` makes it read the second game. `uwdump` without
arguments lists every command.

- `uwdump uwmod-check <game> --pack my-mod.uwmod` checks an editor package with the same code
  the game uses: whether it applies to this game, which files it changes and where a new
  character starts. The exit code says the result:
  - `0` the package applies,
  - `1` it is not applied (the reason is printed),
  - `2` the file is no package of mod format 2.

  A tool can run it before it starts the game instead of rebuilding the check.
- `uwdump rules-template` writes the rule set at the original's values, every key with its
  meaning, as a `rules.json` to start from (the release packages carry it as
  `Docs/rules-template.jsonc`).
- `uwdump pack-check <game> --pack <folder or zip>` shows what the game makes of a picture pack:
  names, colours, overlaps, shifts.

## Saved games

Saved games go into the game's own folder (`UNDEROM1/SAVE1` to `SAVE4`, for the second game
`UNDEROM2`), shared with the DOS game. In each save folder a file `UWR.json` records the packages
that were applied when it was saved (id, version, name). Loading a save made with other packages
tells the player what differs; it still loads. A tool that keeps saves per mod can read that
record, and should copy `UWR.json` along with the save.

## Trying a mod without touching the player's mods: `-mods`

`-mods <folder>` (or `-mods=<folder>`) makes that folder the Mods folder for this one start: the
game reads its mods and its `order.json` and leaves the player's own Mods folder alone. A relative
folder counts from the working directory. An editor can put the mod it is working on into a folder
of its own and start the game with it.

Two things are still shared with the player's normal start:
- the copy of the game's files with packages applied (`GameData/UW1-mods`), which is made anew
  when the packages differ from the last start's;
- the saved games in the game's own folder (see below).

`features.json` names the argument as `mods.folderArgument`; a build without it ignores `-mods`.

## Starting the game

- Start the executable from its folder. `-portable` makes it portable as above, `-mods` as above.
- Steam's launch option is `"<folder>\UR.exe" %command%`.
- The game looks for the original by itself (GOG and EA app installations, Steam libraries) or
  asks once; it needs no argument for it.

## Reading the Steam release

The Steam release (since 2026-11-05) keeps the game in `game.bin`. It is the same ISO 9660 image
(2048-byte sectors) as the GOG release's `game.gog` under another name, for the first game byte
for byte the same file. A tool that opens `game.gog` only has to look for `game.bin` as well:
each game has its own folder under `steamapps/common` ("Ultima Underworld I", "Ultima Underworld
II"), found through Steam's `libraryfolders.vdf`, with its saves in `UNDEROM1` or `UNDEROM2`
there. Underworld Revisited does it in `Assets/UWDataImport/UWGogInstall.cs` (MIT).

Questions and wishes: an issue on the repository.
