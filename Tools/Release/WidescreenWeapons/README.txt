WIDESCREEN WEAPONS - PAINTING A PACK
====================================

The modern scheme draws your weapon over a wide screen, where the original pictures end at
the edge of the old 4:3 view. A pack paints them on. It holds only the pixels you add: the
original's come from each player's own game.

No command line needed - two files to double-click:

  1 Start painting.bat
      Makes the folder Work with
        guide    every weapon picture where it lies on a wide screen, with lines for the
                 old view (white), 4:3 (grey), 16:9 (yellow) and 21:9 (the edge), each
                 named at the top in its colour. Where a
                 picture is cut off at the old view's edge it is marked in magenta, and
                 edges.txt lists those pictures and how far each has to reach. Only these
                 need painting.
        painted  the weapon pictures, one folder per weapon, numbered in the order of the
                 moves. Paint on these.
        palette  the game's colours (.gpl for GIMP, Krita, Aseprite; .act for Photoshop).
      If your game is not found, drop the game's folder onto this file.
      Run again, it writes the guides and the palette anew and never touches your pictures.

  2 Make pack.bat
      Turns what you painted into the pack Work\pack and checks it; the report is also in
      Work\report.txt. Run it as often as you like.

How to paint:
  - Open a picture from Work\painted, make the canvas larger and paint around the weapon -
    the original must stay where it is in the picture, unchanged.
  - Paint with the game's palette. Other colours are turned into the nearest of it.
  - Pixels without colour must be fully clear (no half-clear edges).
  - Keep the file's name (it ends in WEAPONS_xx) and save it as PNG over the old one.
  - Paint the right hand only: the left hand is its mirror, the game mirrors your pixels.

Why the pictures look a little flat: the original ran at 320 x 200 on a 4:3 screen, so its
pixels were 1.2 times as tall as wide. The PNGs have square pixels, so the weapons look a bit
wider and flatter than in the game. Paint on them as they are - the game stretches your pixels
the same way as the original's. To see the game's proportions, view a picture at 120 % height.
The guides and edges.txt count in the pictures' own pixels too.

How to try it: copy the folder Work\pack into the game's folder Mods - the Mods window's
Mods button opens it - and load a game with the Modern preset. While the pack lies there as a
folder, a picture you saved shows at the next load.

Before you share the pack, put your name, the pack's id (yourname.widescreen-weapons) and a
credit line into Work\pack\mod.json, and a licence file saying how others may use your art.
Share the folder pack as a zip: players put the zip into Mods as it is.
