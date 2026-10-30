# Changelog

What changed from release to release, in the player's terms. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Releases up to 0.7.0 are described on
the [releases page](https://github.com/PQMarine/UnderworldRevisited/releases).

"Closer to the original" lists rules that now behave as the original game does, checked
against its code and, where noted, by playing the original. The deliberate exceptions are in
[Docs/DEVIATIONS.md](Docs/DEVIATIONS.md).

## [0.13.1] - 2026-10-30

### Added
- Gamma, a slider in the Graphics window under View: lifts the dark tones of the 3D view while
  black and white stay where they are, so small objects in dark corners show. It works in both
  render modes; above Low the Remastered lighting leaves much of a level dark, and there was no
  way to brighten it. The interface keeps its colours; 1.00 is the picture as drawn, Orig. puts it
  back there. Asked for in issue 9 by TinkerNightmare - thank you.

## [0.13.0] - 2026-10-29

### Added
- Ultima Underworld II, an early preview. With the second game in the same GOG installation, the
  Game window (menu bar on the main menu) switches to it. What there is: its main menu, its
  character creation, Journey Onward with the saves of the DOS game, and a walk through
  Britannia on its own motion (doors, stairs and teleports between levels, ice and currents)
  with its game screen - the scroll, the compass, the command bar, the flasks, the inventory with
  its containers and runes, the statistics page, and the options with saving and restoring; the
  saves load in the DOS game and back. What there is not yet: its rules (creatures stand, no
  fighting, no spells), conversations, the map, sound and music, the cutscenes, and the modern
  interface (the modern scheme walks without one). Ultima Underworld I is unchanged by it.
- Save folder, a row in the Game window: Automatic, GOG's cloud_saves, or the installation's own.
  GOG's current installers have DOSBox write the saves to a cloud_saves folder beside the game;
  Automatic takes the folder with the newest save and finds those, as the DOS game does.

### Closer to the original
- Items and creatures keep their height when the view looks down; they were squashed. The
  original draws a sprite as a flat picture facing the screen. Found by Reddit user
  Realistic_Koala5531 - thank you.
- Looking up and down in the classic scheme: four steps of 5.6 degrees each way, up to 22.5 (was
  three of 15), and sprites a little smaller the further the view tilts, as in the original.
- Items and creatures in the original's proportions: a sprite's pixel is a quarter taller than it is
  wide (4 to 5), as the original scales them; items stood too wide, creatures a little too tall.
- The view from where the original sees: the eye at the original's height, and the camera a little
  left of and above it, as the original's camera offsets it; the picture centred where the
  original centres it. Close up, creatures and walls had stood too high and too far left - Drog on
  level 1 by a tenth of the view.
- Sound effects last as long as in the original: the length was read the wrong way round, so
  some sounds held until the program ended and others were cut to a twelfth of a second.
- Dice roll as the original rolls them (the random number scaled to the sides, not the
  remainder); the spread is the same, the single throws are the original's.
- A save's PLAYER.DAT is enciphered with the key DOS takes from the character's name; older saves
  of ours carry a fixed key until saved again (both load either way).

## [0.12.0] - 2026-10-28

### Added
- Modern motion, a third choice under *Motion* beside Original and Smooth: you slide along walls
  and round corners, posts and doors instead of sticking, the view no longer turns when you run
  into a wall, and backwards and sideways run at three quarters of the forward speed (the slow
  walk slows them too). Jumps, steps, ledges, falls and their damage, bouncing off a wall in a
  jump, water, lava, the spells of motion, move triggers and doors work as in the original - a
  hard jump stays as hard as it was. It has its own *Response*, at once by default, and works in
  both control schemes. It is a motion of its own, checked against the original's on all nine
  levels before it was played.

## [0.11.0] - 2026-10-27

### Added
- Music from recordings: your own recordings of the music, one file per piece in the folder
  UW1/Music the Sound window opens, play in place of the AdLib, General MIDI or the MT-32. Each file
  is named by the number of the game's piece (01.ogg the introduction, 15.ogg Maps and Legends;
  Ogg Vorbis or WAV); a piece without a recording plays on the device as before. The remake by
  Pieces of 8-bit on Nexus Mods uses these names and can be copied in as it is. Suggested on Reddit.
- Steam Deck button names and glyphs for the gamepad (Controls, Gamepad tab): A B X Y, L1 R1,
  L2 R2, as the Deck prints them.

### Changed
- A folder per game beside settings.json, for Ultima Underworld II later: what belongs to the
  game lies in UW1 - the rules.json and the music recordings. A rules.json from an earlier version
  moves there by itself on the next start (if UW1 holds none yet). settings.json, graphics.json
  and the MT-32 ROMs stay where they are, for both games.

### Closer to the original
- Remastered: a pillar or chest standing in a tile with a diagonal wall is no longer covered by
  that wall; the original paints the wall with its tile, before what stands in it.
- Land creatures keep stopping at lava edges after a fire elemental or another lava-proof
  creature has moved; only fliers keep the original's quirk (they count lava as costly on their
  paths once Tyball has moved).
- A container that holds something valuable at any depth, not only one level down, is spared
  when it lands in water or lava, burns, or is cleared away for room.
- Creatures searching for a path give up where the original does (a search wave stops at 64
  tiles).
- Cutscenes: Escape on the title counts as a key, as in the original's script; a repeat plays once more, as in the original (the
  final death's skulls flash 11 times), and the lines and fades before it are not run again on
  each pass; the intro's erase command takes the subtitle away.
- A creature's alarm after the player's blow ends one tick earlier, as in the original.

## [0.10.0] - 2026-10-26

### Added
- Languages: the Language row in the Game window chooses the language, and the change takes effect
  at once - menus, windows, the HUD and the game's messages switch without a restart (lines already
  in the message scroll stay as they were written).
- German for the port's own texts: menus, settings, help, the modern interface and the sentence
  frames (`Languages/de.tsv`).
- The game's own texts can be translated too: a language file `Languages/<code>.strings.tsv` beside
  the game is laid over the texts of your own copy of the game. The project ships no such file -
  the game's texts belong to their rights holders, and a translator publishes a translation of
  them on their own.
- Grammar for translations: articles, cases, genders and plurals per language, names placed inside
  a sentence where the language needs it, and plural rules for many languages.
- The classic fonts draw ä ö ü Ä Ö Ü ß in the original's style, and they can be typed in names,
  map notes, save names, mantras and conversation answers; the gamepad's letter grid gets a row
  of them when the language uses them.
- For translators: `Docs/TRANSLATING.md` and a Translation Kit beside every release
  (`uwdump` without installing .NET, templates and the German file as an example), with
  `uwdump text-template`, `strings-template`, `validate-tsv`, `fit` (labels that would be clipped
  in the classic look), `looks` and `typed-words`.
- Prepared for other scripts: a language can name a fallback font (a system font, e.g. for
  Cyrillic or Chinese) and turn on the system's input method; such a language still needs a font of
  its own for the classic look.

### Fixed
- Looking at the ceiling says so instead of "a stone floor".
- The modern E and the pad's A use a decoration that cannot talk (the double door on level 1)
  instead of offering to talk to it.

## [0.9.2] - 2026-10-26

### Closer to the original
- Hit points and mana are whole numbers, as the original keeps them.
- What a conversation sets for hit points, mana, hunger and poison is kept as it is, even above the
  maximum, as the original stores it; until now it was cut to the maximum.
- Creatures measure distances with the original's square root and aim casters and archers by its
  angle table: a follower catching up and a shot from close by may land a little differently.
- The pause between two spells runs on the game's clock: time in a conversation or a menu no
  longer counts.
- The player's noise dies down at a fixed rate in game time, a quarter of a second per step - after
  stopping, a sneaking player is quiet again after about four seconds (Docs/DEVIATIONS.md).

### Added
- The rule set: an optional rules.json beside settings.json changes the game's numbers - combat,
  magic, experience, vitals, sleep, creatures, respawn, motion, trade, inventory, lights, the
  original's quirks and our deviations as switches (`uwdump rules-template` lists every key with
  its default). Without the file the game plays as before; a bad entry is reported and skipped.
- A second look for the game's windows: Colours - Window theme Stone, in the colours of the game's
  icon - the rune stone's grey-brown with a faint torchlight, ember-coloured highlights, lit edges -
  with the background as a gradient or a cloud pattern. Stone is the default; Plain, the look so
  far, is one click away.
  The HUD's panels and bags keep their own colours.

### Changed
- The Remastered effects window is a window like the others (and follows the window theme) instead
  of the marbled options picture. Graphics shows Custom, the own values, as the current level; the
  new button Remastered effects... beside Palette effects... opens the window and switches to
  Custom. In the classic options Very High opens the menu bar with the window.
- The Controls window is taller and shows more of the keys at once.
- With F10 and the arrow keys, Left and Right now change a selected slider (by its step, held to
  repeat), and a list scrolls along with the selection.

### Fixed
- Remastered: a creature behind a door or portcullis in the same tile - Urgo in his cell on level 3 -
  no longer stands in front of the bars.

## [0.9.1] - 2026-10-25

### Closer to the original

- Cutscenes take a click as the original does: it jumps to the scene's next mark, with the pictures
  in between run unseen, or ends a pause. In the dreams the picture now moves on with the subtitle.
  The intro skips three frames the original skips too.

### Fixed
- Clicking on through the intro no longer lays the spoken lines over each other.
- Night Vision turns the world grey in the Remastered mode too.
- Changing the colour help during a hallucination takes effect at once.
- The open Sound dialog no longer writes the settings file over and over.
- The modern scheme's right Ctrl+R no longer draws or sheathes the weapon besides opening Restore:
  while Ctrl holds a shortcut, its letter does nothing else (both schemes, rebound keys too).
- One press of Escape, Start or the pad's B closes only the topmost thing; Escape that cancels a
  key rebind no longer closes the game menu, and no hotkey fires while a key is being rebound.
- Shift+F11 no longer changes the brightness as well.
- In the classic count prompt the pad's RT takes one and LT all, as the mouse buttons do.

### Changed
- The menu bar's Game and Graphics items open windows like the other items instead of drop-down
  menus. Graphics: the detail level, Palette effects..., widescreen, view angle and the world at the
  original's resolution. Game: the game folder, the control scheme, the three switches and Quit.
- In the Remastered mode the Light spell shines yellowish like a torch; Daylight stays white.
- A click during a cutscene's spoken line still moves on, and it also ends the final death's
  blinking skulls - the original takes neither (Docs/DEVIATIONS.md).
- The menu bar's windows, the help window, the layout editor, the game menu and the gamepad's
  letter grid are drawn by the game's own interface instead of Unity's immediate-mode GUI. They look
  as before; labels and buttons size themselves by their text, and the notes, the folder path and
  the save name are typed in fields of their own (paste works, selecting and copying does not yet).
- THE GAME'S OWN MOUSE POINTER: in the running game the pointer is the game's, moved by the mouse
  as under DOS, with the system pointer captured and hidden; the menus and setup windows free the
  system pointer. New setting Controls - Pointer speed (25 to 400 %; 100 % is one mouse step per
  screen pixel, without acceleration). Alt+Tab lets the mouse go; the click that comes back does
  nothing in the game.
- The easy movement (Shift or Alt with the walk keys) follows the walk and turn keys when they are
  rebound; X stays its own key.
- The pad's A, X and B in the count prompt, the split box, the yes/no question, the main menu and
  character creation follow a rebind of the pad's click, right button and back.
- The debug display (Shift+F1) is clearer: sections that fold, values that light up when they
  change, and the Shift+F7/Shift+F10 tools as its second tab.
- Internal: rendering, audio, input, menus, settings, the help window and the cutscene player are
  restructured (code cleanup phases 6, 7, 8, 8b and 8c, see Docs/CLEANUP-PLAN.md). Underworld
  Revisited's own texts are in one table and can be translated with a language file beside the
  game (Languages/<code>.tsv).

## [0.9.0] - 2026-10-24

### Closer to the original

The view and the pointer
- The classic view has the original's field of view: 74.7 degrees across its 113-row window, from
  the numbers the original sets up its window with. Walls no longer look a little too far away.
- While the left button is held in the classic view, the pointer stays inside the original's view
  window and no longer runs past its edge or into the rune hollow below it.
- The rune shelf takes a click anywhere in its cells, without gaps between the runes, as the
  original's (both schemes).

Combat
- The power gem pulses as the original's: for a bow or a sling from the moment the shot is ready,
  for a blow once it is fully charged.
- With no arrows or sling stones the attack is refused at once, without a wind-up.
- Walking into water in the middle of a charged blow puts the weapon away with its own animation.

Things
- A trapped thing that was carried and dropped keeps its trap: searching for it finds it.
- An open container on the paperdoll shows its open picture.

The map
- A note the original wrote keeps every byte of it when the save comes back from this game.

### Fixed
- Classic scheme: the pointer no longer shows up while a map note is typed, during a yes/no
  question (the anvil) or the count prompt.
- Gamepad: answering the count prompt or a yes/no question with a button no longer clicks
  again at the pointer.
- Classic Wide and Classic Modular: Tab opens the help, and the frame closes up for it as the
  classic widescreen frame does; the split box works on the original's page; the page, the rune
  tablet and the stats take no clicks while the panel turns; the inventory page shows during a
  conversation; the frame and the eyes follow the colour help; "There is no place to put that."
  shows as in the classic frame.
- Modern scheme: the Character tab shows a stack's count in the hand, shoulder and ring slots;
  the right button closes a container (its window's badge, the original page's open-container
  icon); a container on the original page shows its open picture; the mantra shows only its box,
  not a running line among the messages.
- Modern conversation: the gamepad's walk stick scrolls the history while the pointer is on it, a
  glyph shows it; a name or answer with angle brackets shows as typed.
- Messages page by the lines the scroll or box shows, with [MORE] where the classic box has it,
  and the cursor blinks at the same rate in both schemes. The Classic Wide scroll uses its width.
- Map notes can be typed on Linux.
- The classic weight number no longer stands on the big map in the modern scheme.
- The paperdoll and the power gem follow the colour help; dragged icons show no black lines.
- Memory: level changes, map rebuilds, icons and runes no longer leave pictures and meshes
  behind.

### Changed
- The pointer is drawn by the game in both schemes, at the size of the frame's pixels (classic)
  or of the crosshair (modern); Windows drew it smaller than asked.
- A map note starts at the quill's tip, its letters ending on the row above it. That comes close
  to the original, but at the same click a note may sit about a letter's width beside the
  original's (Docs/DEVIATIONS.md).
- Internal: the interface is restructured for both schemes - the classic and the modern one are
  drawn from shared, engine-free descriptions of what they show, and the game asks one interface
  object instead of testing the scheme in many places (code cleanup phase 5, see
  Docs/CLEANUP-PLAN.md). The classic layout's values moved out of the Inspector into one table.

## [0.8.4] - 2026-10-21

### Added

- Modern scheme: a slow-walk key (Left Shift by default, changeable under Controls). Held, the
  walk keys go at the original's walking pace instead of running; a checkbox in the Controls
  dialog makes the key switch the slow walk on and off instead (not while the pointer is on a
  window). Requested in issue #4.
- Modern scheme: the easy movement moved from Shift to Left Alt (changeable under Controls), so
  Shift can walk. The classic scheme keeps Shift, as the original.
- The modern scheme's E, R, the action bar keys 1 to 0 and its fly keys can now be changed under
  Controls (issue #4).
- The modern message box has a "Lines" setting in the layout editor (2 to 8); Shift and the mouse
  wheel on the box, or dragging its top edge, set it too, for the message scroll as well. Issue #4.

## [0.8.3] - 2026-10-19

### Closer to the original

The end
- The journey through the moongate into the Void starts whenever no talisman is left to destroy,
  also right after loading such a save, as in the original.

Creatures
- The golem keeps the 125 hit points his conversation gives him, also after saving and loading.
  A creature saved with 0 hit points keeps them.
- An explosion is charged to whoever launched it: a creature caught in its own fireball, or killed
  by another creature's, gives the player no experience and raises no alarm against him.
- Creatures cast through the same spell path as the player and the traps. Nothing a creature
  casts works in the Void or on a tile without magic, and its area spells lie along its own facing.
- Flame Wind and other blasts strike everyone on their tiles, whatever their height (a bat flying
  high is hit too), and objects in wall squares: a secret door can be burst open.
- A creature that dies far away or during Freeze Time finishes its death when it moves again, with
  its special death (Tybal, the golem) and its loot exactly once; before, it vanished after three
  seconds. Killed during Freeze Time, it shows its first death picture at once.
- Blows, missiles, blasts, Poison and Smite Undead still land on a falling creature, and the
  player's blow on it alerts its kin. A falling body blocks a summoning. A drowning creature keeps
  its hit points.
- A creature's loot is rolled and scattered in the original's order. A conversation partner's goods
  are rolled when the conversation starts and show newest first in the trade window.
- A creature walking into a missile is hit by it and pushes it on.

Combat
- Blows, weapon wear and missiles roll their dice in the original's order. A missile rolls its damage
  when it hits and takes armour and blood from the same body part.
- Walking into a missile hurts, whoever shot it, and the hit is charged to the shooter. Pressing
  against one held by Freeze Time hurts again and again, and fast; the arrow keeps its direction
  until it flies on.
- The player's own missile starts the combat music when it hits him. A trap's missile belongs to
  nobody: no experience, no alarm, no music.
- Every missile hit shakes the view, a trap's included. The shake runs as in the original: a light
  hit gives a short jolt, and every shake ends with one.
- Sleeping while poisoned respects Poison Resistance.
- Jumping into water splashes.

### Fixed
- Things in flight are saved: an arrow in the air was lost when saving, and one in a save of the
  original hung in the air for good and pointed the wrong way. Saves with an arrow in the air now
  load in both games, and it flies on.

### Changed
- Walking into a missile and the screen shake run at fixed rates, not once per drawn frame as in the
  original (Docs/DEVIATIONS.md).
- The F1 overlay shows whether the last hit shook the view, and how hard.
- Internal: the player's damage, death, terrain and movement, and the creatures' game side, their
  attacks, spells and death, are restructured into engine-free rules (code cleanup phase 4, see
  Docs/CLEANUP-PLAN.md).

## [0.8.2] - 2026-10-18

### Fixed
- Create Food shows the food again; since 0.8.0 it was made but not drawn.

### Changed
- Traps fire at a fixed 16 times a second at every frame rate (before, slightly fewer at
  60 frames a second). The flight through the moongate at the end takes the same time at
  every frame rate and pauses behind a menu.
- Internal: traps, spells, conversations and sleep reach the world through one interface, every
  blow on a creature or a thing takes one road, and timed events run on the game's clock
  (code cleanup phase 3, see Docs/CLEANUP-PLAN.md). Nothing else should change in play.

## [0.8.1] - 2026-10-17

### Closer to the original
- A stack of things shows its condition when looked at, as in the original: "5 day-old ears of
  corn".
- A large pile of coins offered to a partner who likes coins is worth its full value again;
  before, it could count as almost nothing and the partner refused.
- Water, lava or a deep drop between the player and a thing keeps the hand from it, as in the
  original: "You cannot reach that."; a bridge carries the reach across.
- Creatures alarmed by a blow stay alarmed in a game saved right after it, as in the original:
  after loading, the victim's kin attack at once.
- A falling creature can still be struck, as in the original. A drifting creature of the
  Ethereal Void that is slain drifts on in its death throes instead of vanishing.
- A blood splash or other short effect caught in a save plays out once after loading and goes,
  as in the original; before, it looped for ever.
- Water at the lowest floor height cannot be fished, as in the original.
- The silver seed finds no room for roots where a barrel, bench, boulder or creature stands in the
  way, as in the original; before, it grew a tree right inside furniture.
- An item a conversation puts down on a tile lies at the original's spot in it, not in the middle.

### Changed
- Docs/DEVIATIONS.md: no rule depends on the frame rate. Standing on lava burns at a fixed rate
  instead of once per drawn frame as in the original.

## [0.8.0] - 2026-10-17

### Closer to the original

Magic
- Paralyze and the other spells aimed at one creature ahead choose it as the original does, by
  its search over the area, not always the farthest one.
- Cancelling Levitate or Fly leaves a Slow Fall that is nearly done and ends at the next tick.
- While a spell waits for its target, an item is being used on something or an item hangs on the
  pointer, no spell can be cast and the runes cannot be clicked. In the modern scheme every panel
  waits too while a spell waits; only a spell meant for an item takes the item clicked.
- Casting checks the Casting skill plus 5 against twice the spell's circle. A short pause
  follows every successful cast, and a cast within it is refused.
- Mana is paid before the test for a place where magic does not work, so casting there costs
  it. The cast sound plays only when the spell takes.
- A backfire hurts the caster by (circle / 2) dice of 8, never below three hit points.
- A weapon's Accuracy or Damage enchantment adds to the attack or to the base damage.
- An area spell cast by a creature is aimed and sorted from the creature, not from the player.
- The oil flask on something it cannot be used on says "You cannot use oil on that.".
- The Key of Infinity works only on the one door it is made for, and is used up there.
  Anywhere else: "It seems to have no effect.".
- Active spells last as long as in the original: Light, Night Vision and Daylight much longer,
  Thick Skin and its kin a little longer. Each lasts its full time, and "is nearly done" shows
  on its last two ticks. When one ends, the last spell moves into its place.
- After a cast, the first rune clicked starts a new shelf.
- "Not experienced enough" and "not enough mana" play the fizzle sound.
- A potion, scroll or wand of a lasting spell stays silent when three spells are already active.
- Levitate and Fly give a small hop when cast.
- Spell 50 does nothing in UW1; there is no Dispel Hunger. Its charge is still spent.
- Open, Name Enchantment and Remove Trap need the object within reach ("You cannot reach
  that."), and a click on nothing keeps the spell waiting.
- Strengthen Door shows the target cursor.
- A spell missile's damage type comes from the game's tables: acid does nothing to a ghost,
  Missile Protection stops a magic missile.
- An area spell's centre and its count follow the original; it stops at the creature that uses
  the count up.
- The spell picture shows only where a spell took; Smite Undead shows none.
- Flame Wind and Sheet Lightning show one picture per struck tile, in the air.
- A fireball's impact shows the explosion with three to five flames around it.
- Create Food lands where the original puts it and needs room there.
- Roaming Sight cast with three spells active still detaches the view, without an icon, as in
  the original.

Sleep
- A comfortable night needs the bedroll and enough food. Every night heals hit points and
  mana by a share of their maximum, a comfortable one about twice as much.
- Passing out brings no dream and no "You make camp.". At night every open door of the level
  may shut, three times in ten, and so may a raised portcullis or an opened secret door.
- An undisturbed night lets time pass for the level: wounded creatures heal half way to their
  kind's average, creatures go back to their posts, allies stop being allies, summoned and
  wandering ones are gone, a race the player angered grows colder, and far odds and ends
  are cleared away.
- Only a creature that knows where the player is keeps him awake; a friendly guard nearby, or
  one that never noticed him, no longer does.
- A sleep ambush finds its way as the original's creatures do, closed doors included.
- Passing out drunk in water, at the shore or on lava is deadly. A night without a dream is
  1.5 seconds of black.

Creatures
- Leaving a level settles its creatures at once: half way to their average hit points, never
  above it, facing a random way, forgetting what they had noticed.
- An ally stops being one three times in four every five minutes, as in the original.
- A Rune of Warding, a damage trap or lava hurts a creature without making it the player's
  enemy, and a kill there gives no experience.
- A creature that wants to talk keeps waiting for the player after a conversation that does not
  change its goal.
- Creatures walk instead of gliding in their combat pose, guards fall when the floor under them
  sinks and burn on lava, lost attackers become guards again, and fliers hold their height to
  where they are going.
- Creatures find their ways as the original's do: the same order of search, at most sixteen
  stored paths at once, leaps over pits on the way, and no sight line beyond ten tiles.
- A creature that picks a lock opens the door on its next push, silently.
- A creature dying while the game is saved finishes its death after loading, with its loot.

Combat
- A blow in the classic scheme finds its target as in the original, from the objects' extents in
  the level data: it reaches a creature just round a corner and an open door's frame instead of
  striking the wall.
- A blow at a door, a chest or any other thing always lands, never critical, and does more from
  the side or behind, as in the original.
- A blow at a pillar, a bench, a shrine, a boulder or a gravestone hits it with the sound and the
  flash, as in the original; a bench wears down to debris, the others take no harm.
- A critical hit multiplies the base damage before the minimum of 2 is applied.
- Toughness on a ring or a shield protects the body twice, not the hands.
- A blocked blow sounds at the target: metal on metal or a dull thud.
- A blow at a wall in reach sparks on the wall at the blow's height.
- Every blow on a spiked door strikes the spike first, from creatures and spells too.
- A door or portcullis does not close on a creature or a barrel in the doorway; it swings back
  open, as it already did for the player.
- A blow charges only after the wind-up; a blow released right then strikes with the weapon's
  minimum. Bows, slings and crossbows do not charge: after the wind-up the crosshair appears,
  and only a release after that shoots.
- Where a blow lands follows the blow: a stab mostly hits the legs, an overhead bash often the
  head, and the right column of the view raises the blow by a third. A creature's blow lands at
  a random third of the player's height.
- On easy, every missile does half damage to the player, a mage's fireball too, and the player's
  own rebounding missile meets the armour.
- A door spike takes a blow only after the door's sturdiness has reduced it; it lasts longer on
  wooden doors and never breaks on metal ones.
- A creature's miss, and its missile, start the combat music.
- What lies in a smashed nightstand is lost. A smashed chest or barrel loses one lock; a second,
  locked lock keeps it shut.
- The player's blow passes over an ally when something else is in reach.
- A creature's eyes compare its hit points with the average of its kind.
- Boots and helmets held in the off hand cast their enchantment; dragon skin boots held there
  protect from lava.
- A torch or lantern at quality 1 no longer reads "burned out".
- A critical that draws blood leaves a second splat; a missile hit sounds twice and bleeds at
  its own height.
- A thrown bow, sling or crossbow does its plain damage; only thrown ammunition is scaled.
- A torch, wand or other non-weapon in the hand reaches only as far as the fist.

Health and the character
- Only blows and missiles shake the view; poison, falls, lava, traps, drowning and leeches do not.
- A worn curse strikes whenever the inventory changes, a spell begins or ends, a light goes out,
  on sleep, mantra and level arrival, after a crystal ball's vision and when the pole is taken up
  or put to something - every time, with a flash - and never while standing still.
  At 3 hit points or less it does nothing.
- Poison is kept in a saved game, and hurts on the same tick as the mana roll.
- Leeches always say they remove the poison and never shake the view.
- The flasks have 12 steps and bubble only at the lower steps.
- A Mana lesson at a trainer raises the maximum mana only at the next level or mantra.
- A mantra at a shrine recomputes the maximum vitality from level and strength as well, and a
  level-up no longer cuts the current vitality.
- Every Lore lesson lets items be examined again, even when the skill does not rise.
- Names can have up to 29 characters.
- A skill picked a second time at character creation gains as in the original.
- Swimming, lava, levitating and flying make the player louder, not only water.
- Dreams 8 and 9 are remembered in a saved game.

Doors
- The player can open a spiked door; the spike comes off. (Checked in the original.)
- Looking at a spiked door describes the door first, then says it is spiked.
- A broken door can still be struck: closed, it bursts open at the next blow.
- Looking at a key tells about its lock, looking at bones whose they are.
- A door trap and the Gronk conversation open and close doors by the same rule, a moving door
  included.
- A creature walking through a door no longer sets off the trap meant for the player who
  opens it, such as the storeroom doors on level 1.

Containers and objects
- Using a container in the world empties all of it, hidden things included. The pouch on
  level 3 that summons a headless when picked up can be emptied first, and then nothing
  comes. (Checked in the original.)
- A bag burnt by fire stays where it lies, worn out, when it holds something valuable;
  otherwise it spills and becomes debris. A destroyed chest or bag never smokes; a nightstand
  burnt by fire smokes one time in four, as other burnt objects do.
- New objects start in the original's condition: remains, bones, a caught fish, the pieces of
  a broken boulder, the Key of Truth.
- A fish, the Cup of Wonder, the silver tree's seed and the Key of Truth go into the hand,
  else into the pack, else to the floor - never refused, never lost.
- A trap that creates a creature copies only the first thing it carries.
- A creature placed over water or lava by a trap or a spell never vanishes on placement.
- An item put down lands where the original puts it within the tile.
- Things scattered around a point (loot, a spilled barrel) follow the original's search for a
  free spot; there is no random spot within a tile any more.
- A respawned creature keeps its mark through a save, so no second one appears on top of it.
- A creature the player summons is not temporary: it stays when the player leaves the level.
- A creature from an ambush trap stays on its level when the player leaves; only wandering
  creatures vanish then.
- A pack without room says the original's "There is no place to put that.".
- The anvil destroys an item on a bad failure only when the roll beats quality and skill, as in
  the original, where it ate good items far too often before. Slings, bows and crossbows cannot
  be repaired.
- A rock hammer splits a boulder into several rocks of one or two sizes smaller; a small
  boulder yields stones only half the time.
- A thing dropped on a bag in a hand or shoulder slot goes into the bag. Bags opened inside each
  other all have to have room.
- Stacks cannot be combined; a combination makes a new object.
- Spikes, keys, lockpicks, bones and the pole answer with the original's own words when used on
  the wrong thing, carried things included.
- A torch made from wood keeps the wood's condition; the seed of the silver tree is never
  cleared away.
- Charges show only on charged items, never on cursed ones. A bandit, a guard or a golem named in
  lower case is called by that name when looked at.
- Using a sceptre or ruby lying in the world spends one of its charges on its spell, as in the
  original (the spell has nothing to act on there).
- A glowing rock used from the floor joins the carried one; burning incense on the floor shows
  its vision.
- Fishing needs water no higher than the player. A window shows its picture only from close by.
- Name Enchantment on a book, key or bones in the world reads its second line too.
- On the flute the cup's appearance stands alone, a note is shorter and Alt raises the octave.

Traps
- A floor that a lever, a trap or the bullfrog raises or lowers carries the things lying on it
  and the creatures standing on it, instead of leaving them in the air or in the floor.
- The pit trap on level 3 no longer opens a hole in the wall; in UW1 it does nothing.
- A Rune of Warding goes off once and is then gone. Casting it says "The Rune of Warding is
  placed.", even facing a wall, and a monster wandering up while the player sleeps does not set
  it off.
- Poison resistance protects from poison traps and poisoned potions.
- The fireball trap on level 5 flies flat along its own heading instead of homing in.
- Only buttons and switches click when pressed, with or without anything attached.
- A one-time ambush always appears; it failed once in 64 before.
- The inventory check on level 7 does not count the item held on the pointer.
- The troll of level 7 is found by his conversation, and walks home after talking.
- The bullfrog puzzle's tiles rise only to height 13.
- Disarming a poisoned potion names a "damage trap".
- A used-up trap inside a door is removed from it.

Trading and conversations
- A partner hands nothing over while the player holds something on the pointer. With the
  trade area full, the item goes down at the partner's feet.
- A conversation that puts an item at the player's feet now does so.
- In the classic trade, "That is too heavy to take." appears on the parchment.
- A partner with more goods than the four trade slots passes over each further item five times
  in eight, as in the original (five in seven before). He keeps his first weapon or bag, looks
  at up to forty things, and his goods leave his pack while they are laid out.
- An accepted offer leaves the player's worthless or disliked things in his slot, and coins paid
  join the partner's coin pile.
- The player's own trade slot: like piles join and keep the slot's quality, a bag in the slot
  takes nothing, and taking an offer back checks the weight.
- A partner naming an item for repair or appraisal says its plain name ("this longsword"), and
  his price carries his own guesswork.
- A conversation ending on the partner's line, or leaving him hostile, waits for a key.
- Typed answers are kept as typed, up to 50 characters.
- A conversation changes the attitude of the one creature it names, or of the partner's race
  around him; loners keep theirs. A refused demand makes the partner attack as before, and a
  guard talked to returns to guarding.
- A hostile creature started by a trap answers "You get no response.".
- In the classic scheme the number keys choose answers 1 to 4, as in the original.

World
- Armageddon frees every object slot of the level.
- Blood, smoke, splashes and flashes need a free object slot, as in the original, and show
  over water and lava too.
- A planted silver tree looks right and stays animated after loading, in the original as well.
- The flash of a blow on a chest, barrel or sack sits on the object at the blow's height.
- The silver tree and a fountain's water cannot be reached through to take what is behind
  them.
- The compass needle turns at the original's points.
- A lit taper stays lit when put down or thrown.
- Stacks join by quality band and average their quality.
- The Void is drawn unlit in the Palette mode.
- Arriving by stairs, trap, Gate Travel or the silver tree on a tile that is taken puts the
  player on the nearest free tile.
- Roaming Sight and the crystal ball's vision see as in good light.
- Map notes: at most 100 a level, they end at the parchment's edge, and the eraser takes the
  nearest note.
- The compass's time, mantra results, Track and repair lines end with a full stop.

### Added
- The menu bar and its dialogs can be used with the keyboard: F10 shows the bar, the arrow keys
  move, Enter or Space chooses, Escape or F10 leaves. The modern game menu takes the arrow keys
  too.
- [Docs/DEVIATIONS.md](Docs/DEVIATIONS.md): where the game deliberately differs from the original.
- Controls: the modern scheme's mouse look can be inverted up and down, as the gamepad's stick
  already could (idea and first version by krispit-dev).
- Motion: the view while swimming follows the original (it jitters, more when swimming fast); a
  switch beside the head bob sets it to Original, Smooth (the gentle sway of before, with a
  strength slider) or Off.

### Fixed
- A Rune of Warding laid in a save of an earlier version is set off by creatures again.
- Classic conversation: a long player name is no longer cut off above the portrait.
- Classic scheme: an item being used on something replaces the pointer; the normal pointer no
  longer shows beside it.
- Using an item on something out of reach says "You cannot reach that.", as the original does.
- Modern scheme: Escape during Roaming Sight ends the spell; the view no longer comes back a
  moment later.
- The player's arrow, bolt or spell missile leaves one blood splat on a creature, not two.
- An explosion no longer flashes on everything it damages (the original shows hit effects only
  for direct hits), and a missile's flash on an object appears at the missile's height.
- Modern scheme: looking at a trapped object can find the trap and offer to disarm it again.
- Cancelling a dream or another sequence with Escape no longer opens the game menu as well.
- Modern Character tab: a long name no longer runs into the class line below 100 % interface
  scale, and no longer flickers at some window sizes. When it does not fit, the class line drops
  the word "level", then the class name, and only then is the name shortened with "...".
- Levitate and Fly gave no hop, the quake trap never threw the player up, and sleeping did not
  see levitation or water walking: these looked for the player's movement in the wrong place.
- After restoring a game, the first rune clicked empties the hollow again when a cast came
  before, as in the original.
- A save keeps how many runes lie in the hollow; in the original the next rune overwrote the
  first one of a hollow loaded from our save.
- Palette mode: the flash of a blow on a door flickered against the door.
- A creature summoned or created during play stood frozen in the original after loading our
  save: it was missing from the level's list of moving creatures. Saving again repairs an
  older save.
- Linux: the menu bar and its dialogs ignored mouse clicks in borderless fullscreen below the
  desktop's resolution (for example 1920x1080 on a 4K screen).
- The options open in the Void; only saving is refused there, as in the original.
- Switching the control scheme with a menu open left the game stuck.
- Remastered: a creature behind a door could show through the door's lintel and frame.
- The modern scheme's anvil sequence showed only its frame every second time.
- A light lit from a stack could carry an unrelated object along into the save.
- A chain of traps that counts down was saved with a wrong count after the eighth step.
- The dragged item follows the pointer after switching from the modern to the classic scheme.
- The setup bar's dialogs open one at a time.
- Using an item from the modern action bar in Classic Wide aims at the middle of the view.
- Landing and splash sounds are as loud as the original makes them for the object's weight.
- The modern crosshair names a single item in the singular, a stack in the plural.
- Modern conversation: a number typed into the split box no longer chooses an answer, and Q
  looks at the thing under the pointer in the bags during a conversation.

### Changed
- A large code cleanup (phases 0 to 2 of [Docs/CLEANUP-PLAN.md](Docs/CLEANUP-PLAN.md)): rules
  that existed several times now have one home, and the self-check covers them. Each game is
  its own session, so nothing of one game carries over into the next after New Game or Load,
  and its random numbers come from the original's own generator.
