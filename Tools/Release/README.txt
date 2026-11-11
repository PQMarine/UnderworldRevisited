UNDERWORLD REVISITED
====================

An open-source reimplementation of Ultima Underworld: The Stygian Abyss (1992) in Unity, built
on the original game data.

This package contains NO game data of Ultima Underworld - no graphics, sounds, texts, maps or
executables of the original. Everything is read at runtime from your own copy.

This project is not affiliated with or endorsed by the owners of the Ultima Underworld rights.
Ultima and Ultima Underworld are trademarks of their respective owners. Underworld Revisited is
a free, non-commercial fan project: it is not sold, and no donations are accepted.


WHAT YOU NEED
-------------

- Windows 10 or newer, 64 bit, and a graphics card with DirectX 11 - or a 64-bit Linux with
  a graphics driver for Vulkan or OpenGL 4 (the Linux package, UR.x86_64; it is new and far
  less tested than the Windows one, reports are welcome). Nothing else needs to be installed:
  the package brings its own runtime.
- ULTIMA UNDERWORLD 1 FROM GOG.COM ("Ultima Underworld 1+2") OR FROM STEAM ("Ultima
  Underworld I"), installed. Steam sells the same game files as GOG. Only this version is
  supported: some data is read directly from its UW.EXE, and other releases (original floppy,
  CD, other stores) differ and will not work.


STARTING
--------

1. Unpack this archive into a folder of its own.
2. Start UR.exe (Windows) or UR.x86_64 (Linux). If Linux refuses to run it, the archive lost
   the executable bit on the way: open a terminal in the folder and run
   chmod +x UR.x86_64 once.
3. The game looks for your GOG or Steam installation in the usual places:

     C:\GOG Games\Ultima Underworld
     D:\GOG Games\Ultima Underworld
     C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld
     C:\Program Files (x86)\GOG.com\Ultima Underworld
     C:\Ultima Underworld
     D:\Ultima Underworld

   and every Steam library's steamapps\common\Ultima Underworld I (the libraries Steam lists
   in its libraryfolders.vdf). On Linux it looks in ~/GOG Games, ~/Games (Heroic, Lutris), the
   Wine and Proton prefixes under ~/.wine and the Steam compatdata folders, and the Steam
   libraries under ~/.steam and ~/.local/share/Steam.

   If it does not find it, it asks for the folder: choose the folder that contains game.gog
   (GOG) or game.bin (Steam) - the same CD image under two names, however it was installed (the
   GOG installer, Steam, Heroic, Lutris, a Wine prefix, or innoextract on the Windows installer).
   The choice is remembered. Later it can be changed in the menu bar at the top of the main
   menu (Game > Game folder...).

Save games are kept in the SAVE folders of your Ultima Underworld installation, as in the
original, and are compatible with it.

STEAM: Steam keeps the saves in its cloud, but synchronises them only when it starts the game
itself. Put this into the game's Properties > General > Launch Options (your path to UR.exe):

     "C:\Games\Underworld Revisited\UR.exe" %command%

"Play" in Steam then starts Underworld Revisited, and Steam fetches and uploads the saves around
it. Clear the line to start the original again.


ULTIMA UNDERWORLD II (AN EARLY PREVIEW)
---------------------------------------

With the second game in the same installation (GOG's "Ultima Underworld 1+2" holds both, on
Steam "Ultima Underworld II" beside the first), the Game window in the menu bar on the main menu
switches to it. It has its main menu, its character creation, Journey Onward with the DOS game's
saves, and Britannia with its game screen, inventory, options and saves (they load in the DOS
game), its map, its conversations and trading, the modern interface, its creatures and fights, the
player's death, food and sleep, and a good part of its magic. Not yet: the rest of its spells,
traps and more of its rules, music, and cutscenes. The switch back is in the same window.


CONTROLS
--------

Two control schemes, both complete. The original scheme, the one the game starts with: the
mouse pointer steers as in 1992, and F1 to F10 and the Ctrl shortcuts work as in the original.
Shift+F2 switches to the modern scheme - or Original and Modern under Game in the menu bar, for
keyboards whose F keys need Fn: free mouse look, WASD, an action bar, bags, a character panel
and a rune panel; its layout can be changed with "Edit layout" in its game menu (Escape),
which also offers presets: Modern; Classic Modular, the original's own pieces as parts of their
own; Classic, the original's frame, widened to the screen ("Wide") or at 4:3 in the middle
("4:3"); and one's own layout, which can
start from a preset in the editor and is saved only when the editor is left.
The scheme in force is kept for the next start. Tab opens the help; its Controls tab lists the
keys of the scheme in force. The full list of keys is in Docs/CONTROLS.md, with the debug keys
for testing (noclip, the jump to a level and tile, the debug display) at its end; they are off
until switched on at the end of the keyboard tab under Controls.

A gamepad works in both schemes (tested with an Xbox controller). The sticks walk and look, A
uses, X looks, Y jumps, B closes, RT strikes, LT casts; R3 puts a pointer on the right stick for
the windows, with A and RT as the left mouse button and X and LT as the right one. Names and
mantras are typed on a letter grid. Hold View for the help: its Controls tab shows the buttons
as glyphs. Under Controls in the menu bar, its Gamepad tab: the button names (Xbox, PlayStation,
Nintendo, Steam Deck), every button, swapped sticks, inverted look and a deadzone per stick.

The menu bar at the top of the main menu (and of the game menu) holds the settings. Under
Motion you choose how you move: Original computes the original's motion code step by step
as the game did in 1992 - how many steps a second is the "Original fps" setting there -,
Smooth computes the same rules once per frame for an even picture, and Modern moves you by a
motion of its own - you slide along walls, the view stays where you look, backwards and
sideways are almost as fast as forwards - with the original's jumps, ledges and falls; each
setting carries a short explanation. Controls has the mouse look speed of the modern scheme, its inverted look
up and down, and the gamepad's settings. F10 shows the menu bar for the keyboard: the arrow
keys move, Enter or Space chooses, Escape or F10 leaves. Graphics has the Mode - Palette, the
original's look; Palette colours, real light and shadows in the original's colours; Remastered,
the modern look - and its Effects, Off, Subtle, Full or your own, set with "Effects..." In the
Palette mode those are the port's own effects on the original's look: use them with care, turned
up they quickly take it away. The window also has the classic screen at 4:3 or widened to the
screen ("Classic screen widescreen"), the view angle ("View"), the gamma of the 3D view ("Gamma",
for a darker level - 1.00 is the picture as drawn), the size of the dither ("Dither grain"), and
the world at the original's resolution or a multiple of it up to 4x, with the interface staying
sharp. It can be moved by its title row, to see the view while choosing.
Colours has the colour help and the look of these windows: Stone, in the colours of the
game's icon, with a gradient or clouds, or Plain.
Gameplay has what changes how the game plays: "Fix bugs of the original" (on by default) and
"Remove exploits of the original", and the aids, each off by default - "Choose attributes at
creation", "Show every mantra and spell in the help" (else it shows those your character has
learned in the game), "Lizard words" (the lizardmen's words with their meanings in brackets) and
"Skill raises" (Highest, the most a chant can give; Fixed, the same result after loading).

PORTABLE
--------

The settings, the extracted game files and the mods are normally kept in your user folder (Windows:
%USERPROFILE%\AppData\LocalLow\Underworld Revisited\Underworld Revisited). "Make portable" in the
Game window, or an empty file portable.txt beside UR.exe, keeps them in a folder UserData beside the
program from the next start on, and your settings, game folders, MT-32 ROMs and mods are copied over.
To start a portable copy fresh instead, make an empty folder UserData beside UR.exe yourself. Copy
that folder along when you unpack a newer version. The folder
alone keeps it portable too: to go back, delete portable.txt and UserData (copy what is in UserData
to the user folder first to keep your settings). The save games stay in the game's installation
either way.

Even when portable, the Unity engine itself still writes a few things outside UserData:

  - its log Player.log (and Player-prev.log from the start before) in the user folder above;
  - the window size, full screen mode, monitor and a session counter, on Windows in the registry
    under HKEY_CURRENT_USER\Software\Underworld Revisited, on Linux in a file prefs in the user
    folder;
  - on Windows, crash reports in %TEMP%\Underworld Revisited.

The log can be moved with the start option -logFile and a full path, for example

     -logFile "D:\Games\Underworld Revisited\UserData\Player.log"

(with Steam in the launch options line, after the path to UR.exe). A relative path counts from the
folder the game was started in, which is not always the program's. The other entries cannot be moved.


RULES
-----

The game's numbers can be changed without touching the program: put a file rules.json into the
folder UW1 beside settings.json (Windows: %USERPROFILE%\AppData\LocalLow\Underworld Revisited\
Underworld Revisited\UW1, Linux: ~/.config/unity3d/Underworld Revisited/Underworld Revisited/UW1)
with only the lines you want to change - a rules.json an earlier version read beside settings.json
moves there by itself -, for example

    { "magic.manaPerCircle": 2, "quirks.litTaperStaysLit": false }

Docs/rules-template.jsonc lists every key with the original's value and a line on what it
does. The file is read when a game starts or is loaded; a key the game does not know or a value
out of range is reported in the debug display (Shift+F1, with the debug keys on) and skipped. Without the file the game
plays as the original.


LANGUAGES
---------

The Language row in the Game window chooses the language of the port's own texts (English or
German); the change takes effect at once. The files are in the folder Languages beside the game.
A translation of the game's own texts is a file <code>.strings.tsv that a translator publishes on
their own; put it into Languages too, with its folder of pictures if it brings one. With such a
translation the cutscenes show subtitles for their speech ("Subtitles in cutscenes" in the Game
window: Auto, Off or On). How to make one: Docs/TRANSLATING.md in the repository and the
Translation Kit beside each release.


MUSIC
-----

The Sound menu in the menu bar chooses what plays the music: the AdLib, as most heard it in
1992; General MIDI, the music's MT-32 version on a soundfont that comes with the game; or the
Roland MT-32 itself, emulated by Munt. The MT-32 needs the control and PCM ROM of a real MT-32
or CM-32L, which belong to Roland and do not come with the game: put your own into the folder
the "Open folder" button there opens. The menu shows which ROMs were recognised.

Music from recordings: your own recordings of the music, one file per piece, play in place of
the device. Put them into the folder the second "Open folder" button opens (UW1\Music beside
settings.json), each named by the number of the game's piece (UW01.XMI in its SOUND folder is
01): 01 the introduction, 02 to 04 the levels, 05 to 07 combat, 10 Armed, 11 Victory, 12 Death,
13 Fleeing, 15 Maps and Legends, as .ogg or .wav. A piece without a recording plays on the device.
The high-quality remake by Pieces of 8-Bit's HQ Remakes uses these names, and the files of its
UW1music folder can be copied in as they are. It is not part of this package. Get it from their
pages:

     https://www.youtube.com/@HQRemakes
     https://www.nexusmods.com/profile/OfficialPo8Mods/mods


LICENCES
--------

Underworld Revisited is released under the MIT License, see LICENSE. It contains code, a
font, a soundfont and a library from other projects under their own licences; their notices are in
THIRD_PARTY_NOTICES.md. README.md is the project's full description.
