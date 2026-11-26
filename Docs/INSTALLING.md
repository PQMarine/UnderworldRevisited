# Installing and running Underworld Revisited

The short version is in the [README](../README.md#installation). This page has the details: where the games are
found, where the settings are kept, a portable copy, starting it from Steam or GOG Galaxy, the update check, and the
music devices and recordings.

## The games

Underworld Revisited plays **Ultima Underworld I** and **Ultima Underworld II** from GOG.com, Steam or the EA app.
Install them there first; Ultima Underworld II is sold on its own and is needed only to play it.

On the first start the game looks for the installations where GOG (Galaxy and the offline installer) and the EA app
record them in the Windows registry, whatever folder you chose there, then in the usual places (listed in
[Building](BUILDING.md#building-the-game)) and in the Steam libraries. If it finds none, it asks for the folder:
choose the one that contains `game.gog` (GOG) or `game.bin` (Steam; the EA app's installation folder or its `DATA`
folder). The choice is remembered.

In the menu bar of the main menu, *Game* lists every installation found with its store (GOG, Steam, EA app), shows
the save folder in use, and switches between the two games; a click takes another installation, and *Other
folder...* chooses a folder of your own.

On the first start of a game its files are extracted from `game.gog` (or `game.bin`) once into a local cache;
nothing is written to the installation except the save games. They are read from and written to the installation's
`UNDEROM1` (the first game) and `UNDEROM2` (the second), so they can be exchanged with the original games. The
Steam versions need none of their bundled DOSBox and its settings.

## Where the settings are kept

Normally the settings, the extracted game files and the mods are kept in your user folder (Windows:
`AppData\LocalLow\Underworld Revisited`, Linux: `~/.config/unity3d/Underworld Revisited`), with a folder per game
(`UW1`, `UW2`) for what belongs to one of them.

**Portable.** To keep them in a folder `UserData` beside the program instead, use *Make portable* in the Game
window or put an empty file `portable.txt` beside `UR.exe`; the next start uses it and copies your settings, game
folders, MT-32 ROMs and mods over. To start a portable copy fresh instead, make an empty folder `UserData` beside
`UR.exe` yourself. Paths to the installations on the same drive are kept relative, so a USB stick may change its
letter. The save games stay in the installations, as above.

Even when portable, the Unity engine itself still writes a few things outside `UserData`:

- its log `Player.log` (and `Player-prev.log` from the start before) in the user folder above;
- the window size, full screen mode, monitor and a session counter, on Windows in the registry under
  `HKEY_CURRENT_USER\Software\Underworld Revisited`, on Linux in a file `prefs` in the user folder;
- on Windows, crash reports in `%TEMP%\Underworld Revisited`.

The log can be moved with the start option `-logFile` and a full path, for example
`-logFile "D:\Games\Underworld Revisited\UserData\Player.log"` (with Steam in the launch options line below, after
the path to `UR.exe`). A relative path counts from the folder the game was started in, which is not always the
program's. The other entries cannot be moved.

## New versions

*Check for new versions* in the Game window, off by default, lets the main menu ask GitHub at most once a day for
the newest release. A newer one is named above the version in the corner, and a click on it opens its release page.
Only that request is sent, and nothing is downloaded or installed.

## Starting it from a store

**Steam, with Steam Cloud.** Steam keeps the save games in its cloud, but it synchronises them only when it starts a
game itself. Enter this in the game's *Properties > General > Launch Options* (your path to `UR.exe`):

```
"C:\Games\Underworld Revisited\UR.exe" %command%
```

"Play" then starts Underworld Revisited instead of the original: Steam fetches the saves before and uploads them
after, with Underworld Revisited's own notes file (`UWR.json`). Clear the line to start the original again. Do this
for each of the two games you want to start this way.

**GOG Galaxy.** Galaxy can start `UR.exe` in place of the original as well. Open the settings button beside *Play*
and choose *Manage installation > Configure...*. Under *Launch parameters* tick *Custom executables / arguments*,
then *Add another executable / arguments*, choose `UR.exe` as the file and select *Default executable*. *Play* then
starts Underworld Revisited, and Galaxy uploads the saves after it ends, with its own notes file (`UWR.json`). Select
the original's entry as the default again to go back.

**EA app.** Its launch options take no path, so it cannot start Underworld Revisited. Start `UR.exe` directly.

## Music

The *Sound* menu chooses what plays the music: the AdLib (as most heard it at the time), General MIDI, or the
MT-32. For the MT-32, put the control and PCM ROM of your own Roland MT-32 or CM-32L into the folder the *Open
folder* button there opens; they belong to Roland and do not come with the game. The menu shows which ROMs were
recognised.

### Music from recordings

Under *Music from recordings* in the same window, your own recordings of the music play in place of the device,
one file per piece, as `.ogg` (Ogg Vorbis) or `.wav`, in the folder its *Open folder* button opens: `UW1/Music` or
`UW2/Music` beside the settings. A piece without a recording plays on the device. Each file is named by the number
of the game's piece, as in the game's `SOUND` folder:

- **Ultima Underworld I** (`UW01.XMI` is `01`): `01` the introduction, `02` to `04` the levels, `05` to `07`
  combat, `10` Armed, `11` Victory, `12` Death, `13` Fleeing, `15` Maps and Legends.
- **Ultima Underworld II** (`UWR01.XMI` is `01`): `01` the main menu, `02` to `04` combat, `05` Armed, `06`
  Victory, `07` Death, `10` to `17` the worlds, `30` the introduction.

The high-quality remake of the first game's music by
[Pieces of 8-Bit's HQ Remakes](https://www.youtube.com/@HQRemakes)
([on Nexus Mods](https://www.nexusmods.com/ultimaunderworldunity/mods/1), made for Hank Morgan's UnderworldExporter)
uses these names: the files of its `UW1music` folder can be copied in as they are. It is not part of this project and
comes from its Nexus Mods page.
