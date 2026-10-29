# Roadmap

What is finished, what comes next, what is still open, what comes later, and the ideas that are
not planned. The README's "State" section lists what is in; this file lists what is not, or not
yet confirmed against the original.

"Confirmed" means a behaviour was compared with the original game running in DOSBox. Much
of what is built was also read out of the original's executable.

## Finished

- The whole game of Ultima Underworld 1 can be started, played and saved: all nine levels,
  the Void and the end sequence
- World, objects, doors, locks, traps and triggers, including all trap actions and the
  special deaths
- Combat, magic, creatures with their goals and attitudes, the group alarm, theft, respawn
- Conversations on the original bytecode, trading, repairing
- Character creation, inventory, automap, sleeping and dreams, hunger and fatigue
- What the automap reveals: the original's own visibility sweep (two edge rays from the exact
  position in the tile, clipped at walls and tile corners), ported from the executable
- Save games that the original can load and vice versa
- Intro, cutscenes, AdLib music and sound effects, the classic interface with its
  animations, the 4:3 display of the original
- A full play-through, compared step by step with the original: from character creation
  through the eight talismans and the Key of Infinity to the end of the game
- The Palette and Remastered render modes
- Effects of the port's own for the Palette mode, every pixel still a palette colour: ambient
  occlusion, grime along the walls with a colour, wall depth with its shadow, darker joints,
  ground shadows, glowing lava and flames, and light sources by the original's light table with
  walls casting shadows - presets Off, Subtle, Full (asked for on Reddit); the grime in the
  Remastered mode too
- The original's keys: F1 to F10 and the Ctrl shortcuts (save, restore, music, sound, detail,
  quit) work as in the original; the port's own keys sit on Shift, the help window on Tab
- The port's own additions: the help window beside the view (stats with the hidden values,
  a live map, notes kept with the save game, spells, mantras and the game's manual), a modern
  readable font, the original's 4:3 frame on wide screens, the world at the original's
  resolution or a multiple of it up to 4x (one pixel row per row of the 320x200 screen at 1x,
  the width following the screen, the interface at full resolution; asked for on Reddit), dealing the attribute points at
  character creation yourself
- The modern control scheme: free mouse look, WASD, an action bar, bags, a character panel and
  a rune panel, a minimap, a conversation screen with trading, and a layout editor to move and
  size every part of it, with an inspector for each part's settings - switched with Shift+F2.
  Interface presets: Modern; Classic Modular, the original's own pieces as parts of their own (the
  compass on its stone disc, the message scroll, the power gem, the flasks, the hollow with the
  prepared runes, the rune tablet, the stats panel, the character page with the original
  inventory, the conversation in the original's look, the original's font); Classic Wide, the
  classic frame stretched over a wide screen at fixed places, the stone grown anew instead of
  stretched, with the dragons and the pieces on it; or a layout of one's own, which can start
  from any preset and is only saved when the editor is left. Every part can
  wear a background in the original's colours and patterns; the original's pictures keep the
  pixel proportion of the 4:3 screen they were drawn for
- The classic scheme always at 4:3, and optionally widened to the screen the same way as Classic
  Wide (the conversation and the map stay 4:3); a slider for the view angle of the 3D view
- The motion of the player, the creatures and everything thrown or shot as the original's
  own code, read out of the executable and run without Unity physics: slopes, ledges, the
  water edge, the deflection off walls, jumping, swimming, levitation and slow fall behave as
  in the original. Under *Motion* in the menu bar the arithmetic can run call by call as
  the original did, or as a precise, frame-rate independent computation of the same rules
- The game's own mouse pointer in the running game, moved as under DOS, with a pointer speed
  setting; every window drawn by the game's own interface, in two looks (Stone, in the colours
  of the game's icon, and Plain)
- The rule set: an optional `rules.json` changes the game's numbers and switches the original's
  quirks and our deviations, every key listed with its default by `uwdump rules-template`
- A Linux build (x86_64), tested in a Kubuntu VM
- A gamepad in both control schemes: the sticks to walk and look, the buttons to act, a pointer
  on the stick for every window, a letter grid for typing, glyphs in the help (Xbox,
  PlayStation, Nintendo, Steam Deck - chosen, not guessed), every button rebindable, swapped sticks, a
  deadzone per stick; tested with an Xbox controller
- The music's MT-32 version: on General MIDI with a soundfont that comes with the game
  (cut from FluidR3 GM, the instruments matched to the MT-32's by name), and on the Roland
  MT-32 itself through Munt's emulation with the player's own ROMs - compared with a recording
  of the original on a real MT-32
- Music from recordings (suggested on Reddit): recordings the player supplies, one file per
  piece, played in place of the synthesizer; a piece without one plays on the device
- Modern movement, a third choice under *Motion*: a motion of its own in place of the
  original's code for the player - sliding along walls and round corners, the view staying where
  it looks, backwards and sideways at three quarters of the forward speed - with the original's
  jumps, steps, ledges, falls, water, lava, triggers and doors; compared with the original's
  motion on all nine levels before it was played

## Next

- **Ultima Underworld II**, after the preview of 0.13.0 (the game beside the first, its data,
  a walk through Britannia, its game screen, the inventory, the options and saves, the main menu
  and the character creation): the map, the conversations and the modern interface, then its
  rules. [Docs/UW2-PLAN.md](Docs/UW2-PLAN.md) has the plan.

## Open: gameplay parity

- Nothing known at the moment beyond what the play-through compared. Reports of
  differences to the original are welcome.

## Open: controls

- **Gamepad, further.** Vibration.
- **Steam Deck - contributions welcome.** The Linux build runs there, and the gamepad shows the
  Deck's button names. What would make sense beyond that - a layout of the modern interface for
  its small screen, a Steam Input template (trackpads, back buttons), settings that suit it - is
  best found out on the device, and there is none here to test on. A pull request from someone
  with a Steam Deck is welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).

## Open: platforms

- **macOS.** No build yet, untested: it needs someone with a Mac to test it.

## Open: presentation

- **Hallucination.** All three pictures the original rolls from are built and confirmed, two
  of them as approximations: the "light table" the original copies from its own engine
  variables is modelled on screenshots (those variables' run-time values are not in the
  file), and the scrambled textures show straight streaks where the original swirls (its
  texture mapper could not be read).
- **Remastered mode.** Metal is not told apart from other materials (the palette has no
  established range for it); more effects are planned. Planned: items and creatures get a
  depth estimated from their outline, so they meet floors and walls more softly and the
  lights shade them.
- **Items at a door in the Palette mode.** The Palette mode places items and creatures in the
  original's draw order (tile by tile, doors split the objects of their tile, a bridge's deck
  covers what lies under it); the order of several items among themselves in a door's tile still
  follows a simpler rule. It would only show where such items overlap on screen; none has been
  seen, so it waits for a case. The Remastered mode's own-tile rule (an object is never covered
  by the tile it stands in) covers diagonal walls since 2026-10-27.

## Open: translations

- **Languages** are supported since 0.10.0: the port's own texts in German, the game's texts by
  language files that translators publish on their own ([Docs/TRANSLATING.md](Docs/TRANSLATING.md)).
- **Other scripts** (Cyrillic, Chinese, Japanese) are prepared in the modern look; the classic
  look would need pixel fonts of their own, and names in save games keep the original's bytes.
  Built when a translator for such a language comes forward.

## Later

- Ultima Underworld II beyond its rules (see Next): sound and music, the cutscenes, the whole
  game played through. [Docs/UW2-PLAN.md](Docs/UW2-PLAN.md) has the plan, derived from
  Underworld Exhumed's decompilation of `UW2.EXE`.
- The game logic without Unity: since the code cleanup (0.8.0 to 0.9.2) the rules live in the
  engine-free layer; what is left is handing each rule its game session instead of the
  transitional `UWGameSession.Current`.
- A second front end in Unreal Engine beside the Unity one, to go all out on the effects
  (ray tracing among them). Likely after Ultima Underworld II.
- **Mod support - contributions welcome.** Not planned to be built here for now, but a pull
  request for it is welcome. What exists already: the rule set (`rules.json`, 248 numbers and
  switches) and the language files for every text. What is missing - a mod folder with a load
  order, table and picture overlays, difficulty settings in the game, a randomizer, scripting -
  is laid out in [Docs/MOD-SUPPORT-PLAN.md](Docs/MOD-SUPPORT-PLAN.md), with where each piece
  would attach; see [CONTRIBUTING.md](CONTRIBUTING.md) and open an issue first.

## Ideas, not planned

- **Linux on 64-bit ARM (a Raspberry Pi 5, say).** Unity can build for ARM64 Linux (with IL2CPP
  instead of Mono, and the PDFium library for the manual as an ARM64 build). A rough guess: the
  CPU is plenty for the game logic, the GPU is the limit - the Palette mode at around 720p, the
  Remastered mode's effects likely too heavy; the world at the original's resolution (in the
  Graphics menu) would help most. Asked on Reddit. Further on, also asked there: PortMaster, the
  port collection for Linux handhelds. Most of its devices offer only OpenGL ES 2 without
  Vulkan, which Unity 6 no longer serves, so only the stronger handhelds with Vulkan come into
  question, and only with the gamepad scheme.
- **The sound effects on the MT-32**, as the original may have played them (its file `UW.MT`
  holds 64 timbres of their own - unchecked).
- **A shore line along water** in the Palette mode, a few levels lighter and moving with the
  palette rotation - it may look odd.
- **The original font traced into outlines** (an idea from Reddit), so it stays sharp at any
  size - either pixel-exact or smoothed, keeping the letter shapes without the pixel steps.
  Generated from the player's own game files at run time, as the font belongs to the game.
- **Detect Monster as a heatmap** on the help window's map tab.
- **Cut content brought back as an option**, should more of it turn up. Found so far: Thorlson,
  an old warrior with a complete conversation who asks for an honourable death in battle and can
  join the player as an ally, but stands in no level.
