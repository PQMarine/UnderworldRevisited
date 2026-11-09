UNDERWORLD REVISITED - TRANSLATION KIT
======================================

What a translator needs to translate Underworld Revisited, without Unity, without the .NET SDK
and without the project's source: the tool uwdump, the guide, and the port's own texts in English
and German. TRANSLATING.md explains everything; this file only says where to start. uwdump also
writes the game's pictures and palettes for artists (see PICTURES AND PALETTES below).

This kit contains NO text or picture of the game - neither the English of Ultima Underworld nor a
translation of it. The game's texts and pictures are made from YOUR OWN copy of the game (GOG's "Ultima
Underworld 1+2" or the same game from Steam), by uwdump, on your computer.


WHAT IS IN IT
-------------

  uwdump.exe (Windows) or uwdump (Linux)
                     The tool. One file, the .NET runtime inside; nothing to install.
                     On Linux, if it does not start: chmod +x uwdump
  TRANSLATING.md     The guide: the two files of a language, the name format, slots, select
                     and plural, the grammar header, fonts, checking, trying it in the game.
  Languages/en.tsv   The port's own texts (menus, settings, help, the frames of the game's
                     sentences) in English - copy it to <code>.tsv and translate.
  Languages/de.tsv   The same texts in German, a complete example.
  LICENSE, THIRD_PARTY_NOTICES.md, ThirdParty/
                     The licences: Underworld Revisited (MIT), the code uwdump contains from
                     UnderworldGodot and MeltySynth (MIT), the .NET runtime (MIT).


WHAT TO RUN
-----------

Open a terminal in this folder (Windows: in Explorer, type cmd in the address bar). <game> is
the folder of your GOG or Steam installation (the one with game.gog or game.bin) - without it
uwdump looks in the usual places. "xx" stands for your language's code (fr, ru, zh-TW ...).

  1. The port's own texts:
       uwdump text-template xx --out Languages/xx.tsv
     (or copy Languages/en.tsv) and translate the texts, keeping the ids.

  2. The game's texts, from your own copy of the game:
       uwdump strings-template <game> xx --out Languages/xx.strings.tsv
     and translate them (--empty writes the English as comments and leaves the texts empty).

  3. Check them:
       uwdump validate-tsv <game> Languages/xx.strings.tsv
       uwdump fit <game> --strings Languages/xx.strings.tsv
       uwdump looks <game> --strings Languages/xx.strings.tsv
     validate-tsv also checks the pictures of your language (Languages/xx/pictures, below), and
     fit measures the words written into the game's pictures. A string with characters the pixel
     fonts lack (Chinese, Japanese) is marked "?": only the game measures those - look at them
     there.

  4. Try them: put both files into the folder Languages beside UR.exe (or UR.x86_64) and choose
     the language in the Language row of the Game window. The row shows your language by its
     setting.Language.name, written in the language itself ("Deutsch").

  A pack of the game's texts made before 0.19.0 gets the checksum lines (#@, see TRANSLATING.md,
  "The checksum line") once, from the game it was translated from:
       uwdump strings-checksums <game> Languages/xx.strings.tsv
  They keep a line a mod changed from showing a translation of the old line.

  After a new release, bring your translation up to date with the new texts:
       uwdump text-template xx --from Languages/xx.tsv --out xx-new.tsv
  keeps your lines, adds the new ids as comments with their English ("# id TAB English":
  remove the "# " and translate) and lists the ids that are gone.

  uwdump without arguments lists every command.


WORDS IN THE GAME'S PICTURES
----------------------------

Some English words are painted into the original's pictures: the options panel's buttons, the
stats page's STR to EXP, the map's Level, ERASE and CLOSE, the main menu's captions, the
congratulations at the end. Two ways to translate them:

  - The ids picture.* in Languages/xx.tsv. The game paints over the English and writes your words
    in the original's own pixel letters (the main menu's captions in the save list's style). A
    button holds about seven letters on each of two lines; fit and validate-tsv report a word too
    wide.

  - Pictures of your own, painted over the originals - for a decorative script, the end screen, or
    a language whose letters four pixel rows cannot draw. Write them from your own game:
       uwdump picture-template <game> --out pictures-to-paint
     paint over the ones you translate, keep their size, and put them into Languages/xx/pictures
     with the names they have (OPBTN_00.png, WIN1.png). A picture of yours wins over the words.

Chinese, Japanese and Korean: see "Fonts and input methods" in TRANSLATING.md (grammar.rule.font,
grammar.rule.ime, grammar.rule.classic.font and grammar.rule.classic.bold).

The intro's texts (block 3072 of the game's texts) show as subtitles while the recordings speak
as soon as your pack translates them (the Game window's "Subtitles in cutscenes", Auto).


PICTURES AND PALETTES
---------------------

For painting new art (weapon frames, icons ...), from your own copy of the game as above:
(the name after --out is the folder the files go to)

  A picture file of the game as PNGs, one per picture, named after the file and the picture's
  number in hex (WEAPONS.GR holds the weapon frames in the view):
       uwdump pictures <game> WEAPONS.GR --out my-weapons
  (A file manager may sort the hex numbers out of order, WEAPONS_0A before WEAPONS_01.)

  The first game's weapon frames sorted for painting: a folder per weapon, the files numbered in the
  order of the moves (three attacks of 4 wind-up and 5 swing pictures, then the ready pose), each
  still named after its picture (WEAPONS_xx). Only the right hand - the left mirrors it; add
  --both-hands for both. The empty 1x1 placeholders are left out:
       uwdump weapon-frames <game> --out my-weapon-frames

  The game's palette as a GIMP palette (.gpl; Krita, Aseprite and others read it too) and an
  Adobe Color Table (.act) - palette 0 is the one of the pictures above, "all" writes every one:
       uwdump palette <game> --out my-palettes
       uwdump palette <game> all --out my-palettes
  The colours are exactly the PNGs' ones; index 0 is the pictures' transparent colour.

  Every level seen from above as PNGs:
       uwdump maprender <game> --out my-maps

  For Ultima Underworld II add --game uw2 (pictures, palette and maprender work for it too; its
  weapon frames are WEAP.GR).
  The pictures stay Origin's art: use them for your own work, do not publish them as they are.


A WEAPON PICTURE PACK (WIDESCREEN)
----------------------------------

The modern scheme draws the weapon over a wide screen, where the original pictures end at the
edge of the old 4:3 view. A picture pack paints them on. It holds only the pixels you add: the
original's come from each player's own game, so the pack carries nothing of Origin's art.

On Windows the folder "Widescreen weapons" in this kit does all of the following with two files
to double-click, no command line needed - see its README.txt. The commands below are the same
steps by hand, and the way on Linux.

  1. Where the pictures end: every weapon picture at its place on a wide screen, with lines for
     the old view (white), 4:3 (grey), 16:9 (yellow) and 21:9 (the picture's edge), each named
     at the top in its colour, the cut edges in magenta, and edges.txt with how far each cut
     edge has to reach:
       uwdump weapon-guide <game> --out my-guide
     Only the pictures with a cut edge need painting. The left hand mirrors the right: paint the
     right hand only, the game mirrors your pixels for the left (paint a left-hand picture
     yourself only where it must differ).

  2. Paint on whole pictures, the original in them: start from weapon-frames (above), make the
     canvas larger and paint around the weapon in the game's palette (uwdump palette). Keep
     WEAPONS_xx in each file's name. Pixels without colour must be fully clear.
     Why the pictures look a little flat: the original ran at 320 x 200 on a 4:3 screen, so
     its pixels were 1.2 times as tall as wide. The PNGs have square pixels, so the weapons
     look a bit wider and flatter than in the game. Paint on them as they are - the game
     stretches your pixels the same way as the original's. To see the game's proportions,
     view a picture at 120 % height. The guides and edges.txt count in the pictures' own
     pixels too.

  3. Only the new pixels into a pack: finds the original in each painted picture, takes out
     every pixel where the original has one, and writes the rest with where the original lies:
       uwdump pack-strip <game> --from my-painted --out my-pack
     It warns where the original was painted over (the game keeps the original's pixels) and
     writes a mod.json to fill in where there is none.

  4. Check it as the game will read it - mod.json, names, colours outside the palette, pixels
     over the original's, half-clear pixels, and which pictures with a cut edge are still to do:
       uwdump pack-check <game> --pack my-pack

  5. Try it: copy the folder my-pack into the folder Mods (the Game window's Mods button opens
     it). A pack in a folder is read anew at every load, so a picture saved in between shows at
     the next load. Players get it as a zip of that folder, which they put into Mods as it is.

A pack's layout:

  mod.json                            id, name, version, author, games ["uw1"], credit, licence
  pictures/UW1/WEAPONS.GR/004.png     a picture, named by its number in hex
  pictures/UW1/WEAPONS.GR/extend.json where the original lies in each picture:
      { "pictures": { "004": { "left": 40, "top": 0 } } }

The id is lower case letters, digits, - and ., by custom yourname.mod-name; it stays the same
in every version. A picture may carry its shift in a grAb chunk instead, as GZDoom's sprites
(SLADE writes it); a picture's own line in extend.json wins over it.


A MOD MADE WITH THE EDITOR (.uwmod)
-----------------------------------

Mods made with the Underworld Editor (Avalonia), its mod format 2, go into the folder Mods
as they are. Whether one fits your game, what it changes and where a new character starts:
       uwdump uwmod-check <game> --pack my-mod.uwmod
Only the .uwmod holds nothing of the original. The editor's export of changed files (a zip with
whole game files) is not a mod: do not share it.


PUBLISHING
----------

Languages/xx.tsv, the port's own texts, may go into the project by pull request:
https://github.com/PQMarine/UnderworldRevisited. The game's texts (xx.strings.tsv) are a
translation of Origin's text: they never go into the project; a translator who publishes one
does so on their own responsibility. The same holds for pictures painted over the game's
(Languages/xx/pictures): they are Origin's art changed. See the end of TRANSLATING.md.
