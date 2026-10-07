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
- ULTIMA UNDERWORLD 1 FROM GOG.COM ("Ultima Underworld 1+2"), installed. Only the GOG version
  is supported: some data is read directly from its UW.EXE, and other releases (original
  floppy, CD, other stores) differ and will not work.


STARTING
--------

1. Unpack this archive into a folder of its own.
2. Start UR.exe (Windows) or UR.x86_64 (Linux). If Linux refuses to run it, the archive lost
   the executable bit on the way: open a terminal in the folder and run
   chmod +x UR.x86_64 once.
3. The game looks for your GOG installation in the usual places:

     C:\GOG Games\Ultima Underworld
     D:\GOG Games\Ultima Underworld
     C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld
     C:\Program Files (x86)\GOG.com\Ultima Underworld
     C:\Ultima Underworld
     D:\Ultima Underworld

   On Linux it looks in ~/GOG Games, ~/Games (Heroic, Lutris) and the Wine and Proton prefixes
   under ~/.wine and the Steam compatdata folders for the same folder.

   If it does not find it, it asks for the folder: choose the folder that contains game.gog -
   the CD image that every GOG release of the game carries, however it was installed (the GOG
   installer, Heroic, Lutris, a Wine prefix, or innoextract on the Windows installer).
   The choice is remembered. Later it can be changed in the menu bar at the top of the main
   menu (Game > Game folder...).

Save games are kept in the SAVE folders of your Ultima Underworld installation, as in the
original, and are compatible with it.


CONTROLS
--------

Two control schemes, both complete. The original scheme, the one the game starts with: the
mouse pointer steers as in 1992, and F1 to F10 and the Ctrl shortcuts work as in the original.
Shift+F2 switches to the modern scheme: free mouse look, WASD, an action bar, bags, a character
panel and a rune panel; its layout can be changed with "Edit layout" in its game menu (Escape).
The scheme in force is kept for the next start. Tab opens the help; its Controls tab lists the
keys of the scheme in force. The full list of keys is in README.md.

The menu bar at the top of the main menu (and of the game menu) holds the settings. Under
Motion you choose how the original's motion code is run: Original computes it step by step
as the game did in 1992 - how many steps a second is the "Original fps" setting there -,
Smooth computes the same rules once per frame for an even picture; each setting carries a
short explanation. Controls has the mouse look speed of the modern scheme. Graphics has the
detail levels, the classic screen at 4:3, and the world at the original's resolution or a
multiple of it up to 4x, with the interface staying sharp. Its "Palette effects..." adds
effects of the port's own to the original's palette look (presets Subtle and Full) - use
them with care, turned up they quickly take away the look of the original; "Very
high - own effects..." sets every effect of the Remastered mode.


LICENCES
--------

Underworld Revisited is released under the MIT License, see LICENSE. It contains code and a
font from other projects under their own licences; their notices are in
THIRD_PARTY_NOTICES.md. README.md is the project's full description.
