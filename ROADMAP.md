# Roadmap

What is finished, what is still open, and what comes later. The README's "State" section
lists what is in; this file lists what is not, or not yet confirmed against the original.

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
- The original's keys: F1 to F10 and the Ctrl shortcuts (save, restore, music, sound, detail,
  quit) work as in the original; the port's own keys sit on Shift, the help window on Tab
- The port's own additions: the help window beside the view (stats with the hidden values,
  a live map, notes kept with the save game, spells, mantras and the game's manual), a modern
  readable font, the original's 4:3 frame on wide screens, dealing the attribute points at
  character creation yourself
- The modern control scheme: free mouse look, WASD, an action bar, bags, a character panel and
  a rune panel, a minimap, a conversation screen with trading, and a layout editor to move and
  size every part of it - switched with Shift+F2
- The motion of the player, the creatures and everything thrown or shot as the original's
  own code, read out of the executable and run without Unity physics: slopes, ledges, the
  water edge, the deflection off walls, jumping, swimming, levitation and slow fall behave as
  in the original. Under *Motion* in the menu bar the arithmetic can run call by call as
  the original did, or as a precise, frame-rate independent computation of the same rules
- A Linux build (x86_64), tested in a Kubuntu VM

## Open: gameplay parity

- Nothing known at the moment beyond what the play-through compared. Reports of
  differences to the original are welcome.

## Open: platforms

- **macOS.** No build yet, untested.

## Open: presentation

- **General MIDI / MT-32 music.** AdLib is complete; the other music versions would need a
  soundfont or an MT-32 emulation.
- **Hallucination.** All three pictures the original rolls from are built and confirmed, two
  of them as approximations: the "light table" the original copies from its own engine
  variables is modelled on screenshots (those variables' run-time values are not in the
  file), and the scrambled textures show straight streaks where the original swirls (its
  texture mapper could not be read).
- **Remastered mode.** Metal is not told apart from other materials (the palette has no
  established range for it); more effects are planned. Planned: items and creatures get a
  depth estimated from their outline, so they meet floors and walls more softly and the
  lights shade them.
- **The own-tile rule** of the Remastered mode (an object is never covered by the tile it stands
  in) does not yet cover diagonal walls of that tile. The Palette mode now places items and
  creatures in the original's draw order instead (tile by tile, doors split the objects of their
  tile, a bridge's deck covers what lies under it); the order of items among themselves at a door
  still follows the simpler rule.

## Later

- Move more rules from the Unity side into the engine-free layer, so the game logic can be
  used without Unity.
- Ultima Underworld II.
- Maybe: Detect Monster as a heatmap on the help window's map tab (an idea, not planned).
- Maybe: cut content brought back as an option, should more of it turn up. Found so far: Thorlson,
  an old warrior with a complete conversation who asks for an honourable death in battle and can
  join the player as an ally, but stands in no level (an idea, not planned).
- Maybe: an option to render the 3D view at the original's low resolution (about the view
  window's 176 by 112 pixels) and blow it up without smoothing, for the original's coarse look
  of far objects - switchable, not the default (an idea, not planned).
