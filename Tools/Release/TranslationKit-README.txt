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

  4. Try them: put both files into the folder Languages beside UR.exe (or UR.x86_64) and choose
     the language in the Language row of the Game window.

  After a new release, bring your translation up to date with the new texts:
       uwdump text-template xx --from Languages/xx.tsv --out xx-new.tsv
  keeps your lines, adds the new ids as comments with their English ("# id TAB English":
  remove the "# " and translate) and lists the ids that are gone.

  uwdump without arguments lists every command.


PICTURES AND PALETTES
---------------------

For painting new art (weapon frames, icons ...), from your own copy of the game as above:
(the name after --out is the folder the files go to)

  A picture file of the game as PNGs, one per picture, named after the file and the picture's
  number in hex (WEAPONS.GR holds the weapon frames in the view):
       uwdump pictures <game> WEAPONS.GR --out my-weapons

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


PUBLISHING
----------

Languages/xx.tsv, the port's own texts, may go into the project by pull request:
https://github.com/PQMarine/UnderworldRevisited. The game's texts (xx.strings.tsv) are a
translation of Origin's text: they never go into the project; a translator who publishes one
does so on their own responsibility. See the end of TRANSLATING.md.
