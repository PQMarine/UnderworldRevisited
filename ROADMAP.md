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

## Open: controls

- **Gamepad.** Both control schemes need mouse and keyboard today. A gamepad scheme of its
  own, built on the modern scheme (the sticks to walk and look, the action bar and the bags on
  the buttons), for the Steam Deck, a gamepad on the PC and the handhelds below.

## Open: platforms

- **macOS.** No build yet, untested.
- **Linux on 64-bit ARM (a Raspberry Pi 5, say).** No build yet, untested. Unity can build for
  ARM64 Linux (with IL2CPP instead of Mono, and the PDFium library for the manual as an ARM64
  build). A rough guess: the CPU is plenty for the game logic, the GPU is the limit - the
  Palette mode at around 720p, the Remastered mode's effects likely too heavy; the original's
  render height (see Open: presentation) would help most. Asked on Reddit.
  An outlook further on, also asked there: PortMaster, the port collection for Linux handhelds.
  Most of its devices offer only OpenGL ES 2 without Vulkan, which Unity 6 no longer serves, so
  only the stronger handhelds with Vulkan come into question, and only with the gamepad scheme.

## Open: presentation

- **General MIDI / MT-32 music.** AdLib is complete; the other music versions would need a
  soundfont or an MT-32 emulation. A simpler way, suggested on Reddit: recordings the player
  supplies, one file per track, played in place of the synthesizer when present (made on a
  real MT-32 or a good General MIDI setup; they cannot ship with the project, the music
  belongs to the rights holders).
- **Hallucination.** All three pictures the original rolls from are built and confirmed, two
  of them as approximations: the "light table" the original copies from its own engine
  variables is modelled on screenshots (those variables' run-time values are not in the
  file), and the scrambled textures show straight streaks where the original swirls (its
  texture mapper could not be read).
- **Remastered mode.** Metal is not told apart from other materials (the palette has no
  established range for it); more effects are planned. Planned: items and creatures get a
  depth estimated from their outline, so they meet floors and walls more softly and the
  lights shade them.
- **A render resolution of the original's.** The world drawn at the original's render height
  and scaled up, while the width follows the screen's aspect ratio instead of a fixed 4:3, as
  the Doom ports do it; the interface stays at full resolution. Suggested on Reddit.
- **Modern effects in the Palette mode.** That mode has no colours to compute with: the
  textures hold palette indices, the light picks one of a few shade steps of SHADES.DAT, and
  only then the palette gives the colour. What fits is what moves the shade step before that
  lookup, so every pixel stays a colour of the palette: ambient occlusion (corners and joints
  a step or two darker, first in line, it exists in the Remastered mode already), ground
  shadows under items and creatures, torches, lava and fire as light sources raising the step
  near them (the original lights only from the player) with their shadows as a step down,
  and the relief of the Remastered mode as a step lighter or darker by the light's direction.
  What does not fit is what computes with finished colours: bloom, specular highlights,
  tone mapping. Mapping those back to the nearest palette colour tends to look muddy, the
  palette has few tones for a shine. In between: dithering between two steps, softer light
  edges that stay in the palette, though the hard edges are part of the original's look.
  Each effect switchable on its own, as in the Remastered mode. Suggested on Reddit.
- **The modern interface from the original's own pieces.** A variant of the modern scheme's
  interface that takes the original's parts as they are, split up: its font, its compass, its
  message scroll, instead of the modern font and the heading as text. Suggested on Reddit.
- **The own-tile rule** of the Remastered mode (an object is never covered by the tile it stands
  in) does not yet cover diagonal walls of that tile. The Palette mode now places items and
  creatures in the original's draw order instead (tile by tile, doors split the objects of their
  tile, a bridge's deck covers what lies under it); the order of items among themselves at a door
  still follows the simpler rule.

## Later

- Move more rules from the Unity side into the engine-free layer, so the game logic can be
  used without Unity.
- Ultima Underworld II.
- A second front end in Unreal Engine beside the Unity one, to go all out on the effects
  (ray tracing among them). Likely after Ultima Underworld II.
- Maybe: Detect Monster as a heatmap on the help window's map tab (an idea, not planned).
- Maybe: cut content brought back as an option, should more of it turn up. Found so far: Thorlson,
  an old warrior with a complete conversation who asks for an honourable death in battle and can
  join the player as an ally, but stands in no level (an idea, not planned).
- Maybe: an option to render the 3D view at the original's low resolution (about the view
  window's 176 by 112 pixels) and blow it up without smoothing, for the original's coarse look
  of far objects - switchable, not the default (an idea, not planned).
