# Picture words: the English painted into the original's pictures

**Status: COMPLETE (2026-11-08).** Steps 1 to 4 built and seen in the game; the second game's pictures later.

Started 2026-11-08 after the made-up Japanese test language xj (Todo, "Japanese: checked against the
fork by higesho"). Some English words are not strings but pixels of the original's pictures, so every
translation shows them in English, German too. The user chose the combined approach the same day:

- **Plain lettering** (the small capitals of FONT4X5P) is painted over and written anew by the game
  from the language's texts. Every alphabetic language gets it from its `<code>.tsv`, without painting.
- **Decorative pictures** (the main menu's captions, the end screen) and anything a language wants to
  draw itself come as PNGs in `Languages/<code>/pictures/`. A Chinese or Japanese translation uses these
  for the plain lettering too, as four pixel rows cannot hold an ideograph.

English always shows the original pictures, byte for byte: a picture changes only where the language's
text differs from the English one, or where a PNG is given.

## Inventory (UW1, 2026-11-08)

| Picture | What | Way |
|---|---|---|
| OPTBTNS.GR 6 to 0x3C | the options panel's buttons, idle and lit, and its headings ("SAVE GAME:", "MUSIC IS ON.", "DETAIL LEVEL IS: LOW") | written anew |
| OPTBTNS.GR 2, 3, 5 | the headings painted into the save, quit and detail backgrounds ("SAVE GAME:", "QUIT GAME: ARE YOU SURE?", "SET NEW LEVEL TO:") | written anew |
| PANELS.GR 2 | the stats page's STR DEX INT VIT MANA EXP | written anew |
| BLNKMAP.BYT | "Level" (FONTCHAR, the number beside it is the game's), ERASE and CLOSE (FONT5X6I) on the map | written anew |
| OPBTN.GR 0 to 7 | the main menu's four captions, idle and lit, in a decorative script | PNG |
| WIN1.BYT | the congratulations at the end | PNG |

Kept: the title (OPSCR), the Origin and Blue Sky logos (PRES1, PRES2), the Roman numerals of the save
slots. CONV.BYT's sample text is painted over by the port already. OPTB.GR is the second game's. The
second game's pictures are a later step.

## How the lettering is found and written (measured on OPTBTNS.GR)

- **The ink** is a bright grey or white (204,204,220 idle, 252,252,252 lit, 220,220,220 on YES and NO);
  **the shadow** lies one pixel left and one down of every ink pixel, black or dark brown. Both are found
  per picture: the ink as the bright, nearly grey colour in the box, the shadow as the colour under-left
  of the ink.
- **The glyphs** are FONT4X5P's (four rows high, the fifth row the shadow): "SAVE", "GAME" and "RESTORE"
  rendered with it match the pictures pixel for pixel.
- **The place**: a button's box is the picture without its one-pixel border. Lines are six rows apart,
  the block centred with the extra row below (two lines from row 2, one line from row 5), each line centred
  with the shadow column counted (ink from inner left + floor((inner width - (width + 1)) / 2) + 1). These
  rules put the English words back where they were.
- **Painting over**: every ink pixel and its shadow in the box take the nearest other pixel of the same
  row, so the button's own grain stays.
- **The new text** is the language's text in upper case, broken at blanks into as many lines as the box
  holds (or at `\n`), drawn in the picture's own ink and shadow colours. What does not fit is reported by
  `uwdump fit` and `validate-tsv`.

## Steps

1. **BUILT 2026-11-08** (32 German words in de.tsv, all fitting; the English written anew looks as the
   original, pinned in the self-check as pixels per picture, as the original's art is not quite FONT4X5P).
   The engine-free core (`UWPictureWords`): finding, painting over and writing; the hook where the
   game's pictures are handed out (`UWTextures.GetTextureByType`), so every look of a picture follows; the
   options panel's texts (`picture.options.*`) with their English; self-checks that English leaves every
   picture untouched and that the English words written anew reproduce the original lettering.
2. **BUILT 2026-11-08**: the stats page (PANELS.GR 2: STR to EXP in FONT5X6I, black, no shadow, from column 7,
   seven rows apart) and the map (BLNKMAP.BYT: "Level" in FONTCHAR, ERASE and CLOSE in FONT5X6I, pressed dark
   into the parchment). A box may name its font, dark ink without a shadow (darker than seven tenths of the
   box's median brightness), left alignment and keeping the case. The map's level number moves right by
   what the language's word is wider than "Level" (UWHudMap). The lists of GetTexturesByType and the map's
   two reads of BLNKMAP go through the words too. German: STÄ GES INT VIT MANA ERF, Ebene, LÖSCHEN, ENDE
   (ZURÜCK is too wide for CLOSE's scroll). CONFIRMED in the game the same day. Per user, the stats page's
   labels are text of the page wherever the picture's own English is not shown (a translation, or the modern
   font): painted out (UWPictureWords.Erase) and drawn as its lines, following the key F. The map's "Level" is
   set a column closer than FONTCHAR (Box.Spacing -1), as the picture.
3. **BUILT 2026-11-08**: UWPicturePack (names, the PNG size from its header, the colours to the picture's
   palette), IUWPictureFiles read on the Unity side (UWPictureFiles, Texture2D.LoadImage), put in force with
   the language; a language's picture wins in UWPictureWords.Localize, another size is left out and named.
   The cutscene view reads WIN1 through GetTextureByType now. uwdump picture-template exports the 63
   pictures (OPTBTNS' 52, PANELS 2, BLNKMAP, OPBTN 0-7, WIN1), uwdump pictures exports PANELS.GR and the
   first game's .BYT screens, validate-tsv checks Languages/<code>/pictures. Test pictures for xj (mirrored
   OPBTN 0/1 and WIN1, a wrong-sized OPBTN 2) in Build/Languages/xj/pictures, not in git.
   Per user the same day: where a language has no picture of the main menu's captions but translates them
   (picture.menu.*), the caption is written as text in the save list's style (FONTBIG, its two colours) over
   the hidden picture, which stays the button's area (UWMainMenu.fSetButton); the main menu builds its
   pictures anew on a change of language.
   Before: the PNG pictures of a language (`Languages/<code>/pictures/OPBTN_00.png`, `WIN1.png`): read on the
   Unity side, mapped to the picture's palette, same size required; they win over step 1. `uwdump
   pictures` learns PANELS.GR and the BYT screens, so a translator can export what to paint over.
4. **COMPLETE 2026-11-08**: German texts for every word (the options panel, the stats page, the map, the main
   menu), each seen in the game by the user; `TRANSLATING.md` (Words in the pictures, Pictures of your own,
   Publishing) and the translation kit's README; `uwdump fit` and `validate-tsv` measure the boxes and check a
   language's pictures. THE PLAN IS COMPLETE. Left for later: the second game's pictures (its options panel
   and screens) when UW2 is translated.
