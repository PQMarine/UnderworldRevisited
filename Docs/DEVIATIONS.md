# Where Underworld Revisited deliberately differs from the original

Underworld Revisited aims at the original's rules. Its rules are checked against the original
game's code and, where that leaves a question, by playing the original side by side. A few
differences are kept on purpose. They are listed here with their reasons, so that nobody
mistakes them for bugs, or "fixes" them back.

Everything the original does not have at all - the modern control scheme, the interface
presets, the Remastered render mode, the help window, the setup bar, the music devices - is an
addition and is not listed. Anything not listed here and not an addition is meant to behave as
in the original; if it does not, please report it.

## Rules

**Stacks stop at 511.** In the original, joining two stacks to a total between 512 and 998 sets
a bit that turns the stack into something else - 300 and 250 arrows become a single arrow
(seen in the original). Ours stops the stack at 511 and puts the rest in a free place. The
same limit holds where a trade joins piles: a drop on the player's trade slot, the coins paid to
a partner, and an item a conversation creates.

**Nothing in the rules depends on the frame rate.** Some of the original's rules run once per
drawn frame, so they run faster on a faster machine: standing on lava rolls for a point of fire
damage at 1 in 5 every frame. Ours rolls at a fixed rate, 32 times a second, about what the
original reaches in DOSBox (10 seconds on lava took about 70 of 78 hit points in both). The same
rate holds for a missile the player presses against, which the original strikes him with on
every frame (one held by Freeze Time kills fast), and the screen shake counts its rounds 20
times a second. The text cursor in the message scroll, which the original blinks by counting
passes of its input loop, blinks at 0.4 seconds on and 0.4 off in both interface schemes.

**Attribute points can be dealt by hand at character creation.** An option in the Game menu,
off by default: instead of rolling, the player distributes the class's bonus points. For those
who do not want their character to depend on luck.

**A new maximum of mana on level 7 is held back only while Tybal's orb stands.** The original
holds it back on level 7 whether the orb stands or not, so after breaking the orb a gain on
that level vanishes until the next recomputation. Taken as a bug.

**Garamon's bones leave the inventory before the burial conversation.** The original removes
them only afterwards, through a pointer to the object in hand. Closing their container during
the conversation, or putting them on the trade area, lets them survive the burial, and a
second attempt then only says the bones do not seem at rest. Seen in the original.

**Gate Travel looks for the moonstone before it moves the player.** The original moves first
and searches the target level afterwards. Ours searches first and leaves the player in place
when there is no stone, instead of landing in the middle of another level.

**Picking up still works in a level filled to the last object slot.** In the original the object
in hand needs a slot of its own, so in a completely full level nothing can be picked up.

**The last map note of a level can be deleted on the note pages beyond level 9.** In the
original it comes back when the map is reopened. Levels 1 to 9 behave as in the original.

**A bug in one conversation script is fixed.** The trolls' news conversation (number 288) tests
its counter of untold news against the wrong value, so its "no new news" answer and its reset
never come. The game runs a corrected copy; the loaded data and every export keep the original
words.

**Gifts are never lost.** The Key of Truth from the shrine and the seed of the silver tree go
onto the pointer, else into the pack, else to the floor. In the original they are only given
when the hand is empty; with something held nothing is given, and the mantra can be chanted
again.

**A follower that jumps to the player lands only where it can stand.** A creature following the
player (goal 3) that has fallen far behind jumps close to him; the original puts it down without
a test, on any floor height, even in rock or in the wrong medium. Ours refuses a solid tile or
water for a walker, and a flier keeps its height.

**Typed mantras and names lose spaces at their ends.** In the original " MU " with a space is not
a mantra, and the spaces belong to a name.

**A night does not touch objects flying through the air.** After an undisturbed night the
original also lands or removes the moving objects of the level; ours lets them fly on.

**Arriving where nothing fits kills on the arrival tile.** When no free tile is found around the
arrival and even clearing the tiles does not help, the original leaves the player at his old
place with no hit points left; ours puts him on the arrival tile first.

## Interface and controls

**Roaming Sight can be steered with the keys in the classic scheme too.** The original steers it
by the pointer alone.

**F9 and F10 are refused during Roaming Sight.** The original still tracks and makes camp there;
sleeping then leaves the spell gone and the view stuck.

**A blow does not start dragging an inventory item.** In the original, a blow charged with the
button held keeps charging while the pointer moves over the inventory, and passing over an item
starts dragging it at the same time.

**The use-mode pointer stays after a drop into the trade area.** In the original the pointer
turns back to the normal one while the use mode stays lit.

**A map note does not start exactly where the original starts it.** The note is placed from the
quill's tip, its first letter at the tip's column with its top four rows above the tip's row, as
the original's rule reads; the original's quill and its hot spot do not quite match that rule, so
a note written in ours may sit about a letter's width beside one written in DOSBox at the same
click. Kept on purpose (2026-10-23).

## Presentation

**The map piece shows its picture without the animation.** The piece of the map on level 7 plays
a short cutscene in the view window in the original; ours shows its first picture.

**Far objects are drawn fine, not coarse.** In the original a far sprite looks blocky; a flying
fireball grows from a single pixel to a block to its picture.

## Bugs of the original that are kept

**The Garamon of the burial conversation.** The conversation at the grave runs with a stand-in
partner, and what it changes stays with the stand-in, not with the ghost that appears
afterwards. A player who already answered up to "I shall think on it." meets a friendly
Garamon; only a second conversation with the ghost settles it. Kept as the original has it.
