# A character motor for modern movement: plan

Status: M0 to M4 built 2026-10-27/28: Modern is a choice in the game's Motion dialog, waiting for the user's tour (M4) and the tuning of the feel (M5). Written after two attempts at a modern feel on the
original's motion core were judged not satisfactory and reverted the same day (Todo.md, row
"True modern movement"). It takes Ultima Underworld II into account
([UW2-PLAN.md](UW2-PLAN.md)), so the motor does not have to be rebuilt for the second game.

## 1. Why a motor of its own

The player's motion is the original's code (`UWPlayerMotion` on `UWMotionCore`): it moves in
eighth cells, tests the four corners of a box against the tile grid, and answers a wall by undoing
the sub-step, turning along the wall and holding a cooldown. That is the original's feel - right
for the Original and Smooth modes, and the reason it feels like 1992: the view turns at a wall,
the player sticks at corners, diagonals and doors, and along a diagonal the slide stairs.

What did not work, and must not be repeated:

- **Patching the core's response** (a projected slide, an object normal, diagonal fixes): every fix
  moved the problem - sticking, then a staircase, then a jump where a diagonal meets a straight
  wall. A smooth slide cannot be bolted onto a cell-based undo-and-turn response.
- **A steering layer in front of the core**, with walls as half-planes: an approximation that is
  wrong exactly at corners, so new stuck spots kept appearing.
- **Testing unlike the game runs**: synthetic layouts and whole 16-tick frames, while the game runs
  the precision mode's tiny steps on real levels. The user found every stuck spot; each fix
  answered one spot instead of the class of cases.

The only truly modern feel so far came when Unity's CharacterController moved the player. That is
not to be repeated either: **the motor lives in the engine-free layer** (`UWDataImport`), without
Unity physics. What made that feel good is rebuilt there in principle: a round body sliding
against exact geometry, its velocity carried along the wall, no cell grid.

## 2. Goal and limits

- A third choice under *Motion*: **Modern**, beside Original and Smooth. Original and Smooth stay
  on the core, unchanged; the default stays Smooth.
- Modern changes **how moving feels**: the view stays where the player looks, the player slides
  along walls, round posts and furniture, all directions move at a usual speed, starting and
  stopping are quick.
- Modern keeps **what puzzles depend on**: the jump's height and reach, the step height, ledges
  and drops, water and lava edges and their effects, move triggers, doors, bridges, what can be
  stood on. A place the original cannot reach stays out of reach.
- **The player only.** Creatures, missiles and thrown objects stay on the original core.
- **Engine-free**, deterministic, the same at every frame rate.

## 3. What the player's motion does today

The motor takes over the middle column; the rest stays where it is and is shared by all three
modes.

| Stays shared (`UWPlayerMotion`, the host) | The motor takes over | Stays shared after the step |
|---|---|---|
| the commands (walk, free walk, back, slides, jumps, fly up and down), the turn input, the camera yaw | horizontal motion: walls, diagonals, the faces of floors too high to step onto, slopes, the ceiling | the tile state (`ProcessPlayerTileState`): the motion state and its speeds, swimming, lava, levitation, slow fall |
| the speeds by state (`SpeedRatio`) and the carried load (`MotionWeight`) | the step up within the step height, the ledge (a slow player holds at a drop, a fast one walks off) | the swim counter, the water rushing, the lava burn, the footsteps (the host) |
| the abilities (Leap, slow fall, levitate, water walk, fly) | the vertical motion: jump, fall, levitation, fly, slow fall - on the original's constants | the fall damage and its Acrobat check, the landing sound |
| the position and heading in PLAYER.DAT, the params the host reads | objects: solid ones block, 3D models and bridges carry, doors block closed and pass open, creatures block | the move triggers the step met, fired after the frame (`FirePendingPlayerTriggers`) |
| the easy step of the classic scheme (`EasyStep`) | the contact state the tile state is computed from | the player as a body in the core's world, for creatures and missiles |

The interface towards the host stays the same: `UWPlayerMotion.Frame` runs one step and fills
the params and the `FrameResult`. Inside, it calls either the core (Original, Smooth) or the motor
(Modern).

## 4. Architecture

### 4.1 Geometry

The motor reads the same world as the core (`IUWMotionWorld`: tiles and the bodies in a tile) and
builds exact shapes around the player, rebuilt per step from the tiles within reach:

- **Rock**: a solid tile, and an open tile whose floor is above the feet plus the step height, is
  a box over the tile's square.
- **Diagonals**: the closed half of a diagonal tile is a triangular prism.
- **Floors and slopes**: the floor height per tile, a slope as an inclined plane (as `fSlopeZ`
  reads it); the ceiling as a plane.
- **Objects**: solid bodies as cylinders or boxes by their COMOBJ radius and height (the same
  table the core reads), 3D models as things to stand on, a door's leaf and frame by its
  direction and state, a bridge's deck.
- **The player**: a vertical cylinder - round in plan, so it slides around corners and posts -
  with the original's height and a radius matched to the core's box, so doorways and corridors
  fit as before.

Geometry questions are answered exactly (circle against segment, against circle, against
polygon), with a small skin distance and no cell grid.

### 4.2 Collide and slide

One step:

1. The desired velocity from the command, the heading and the speed (the shared part).
2. **Horizontal**: move the circle along the velocity; at the first contact, keep the part of the
   motion along the surface and continue with the rest, up to four passes; in a corner, stop the
   part that runs into both. Sub-steps no longer than a fraction of a tile, so a long frame does
   not tunnel.
3. **Step up**: a contact whose top lies within the step height lifts the player onto it instead
   of blocking (a stair, a low object one can stand on).
4. **Vertical**: gravity, the jump and the falls integrated in continuous time with the
   original's constants (`JumpSpeed`, `JumpGravity`, `LeapGravity`, the levitation decay, the slow
   fall cap), so the arc's height and reach equal the precision mode's.
5. **Ground**: snap to the floor, slope or object top below within reach; else the player falls.
   The ledge rule as the original's: below three tenths of the forward speed the player holds at a
   drop.
6. **Contact state and triggers**: the floor kind under the feet (water, lava, ground) for the tile
   state; the trigger objects the body overlapped, handed to the host as today.

### 4.3 Rules per game

What differs between the games or is a rule of the original is not hard-coded in the motor but
asked of a small rules object per game, in the style of the UW2 plan's variants (section 4.3
there):

| Rule | UW1 | UW2 (from Underworld Exhumed, `UW1-UW2-DIFFERENCES.md`) |
|---|---|---|
| speed ratio per state | tenths (`SpeedRatio`) | twentieths; swimming from the Swimming skill, 0.2 to 0.95 |
| fall damage | yes | none in world 8 |
| ground friction | ground, water, lava | plus **ice**: the player slides and keeps momentum |
| external push | none | **currents**: a floor texture in water pushes the player |
| may move | always | not while paralysed |
| lava burn | not with dragon skin boots | no exception |

Because the motor carries a velocity (unlike the core's per-frame speed and heading), ice and
currents are a friction value and an added velocity, not a new mechanism. UW1's rules object
comes first; UW2's is written in UW2's phase 4, without touching the motor.

### 4.4 Where it lives

- `UWCharacterMotor` (new, engine-free): geometry, collide and slide, the vertical step, the
  contact state. No statics; it hangs on the player's motion, which hangs on the session - as the
  UW2 plan's phase 0 asks of every rule.
- `IUWMotorRules` with `UWMotorRulesUw1` (new): the table above.
- `UWPlayerMotion`: the mode chooses core or motor inside `Frame`; commands, state, abilities and
  the result stay.
- The setting `MotionSmoothing` becomes the motion mode (Original, Smooth, Modern; out-of-range
  values read Smooth as today's non-zero values do).
- The host (`UWPlayerMovement`) needs no change beyond the mode.

## 5. Testing: before the user plays it

The two attempts failed partly because the checks did not run the way the game runs. So the
motor's acceptance is mechanical and broad:

1. **Geometry checks**: each primitive against hand-computed cases (a corner, a diagonal, a post,
   a step, a slope, a door).
2. **Like the game**: every motor check runs in the precision mode's small steps, on real level
   data loaded from the player's installation, at several frame lengths.
3. **The sweep**: on all nine levels, thousands of starts on open floor with random headings,
   commands (walk, free walk, back, slides, jumps) and frame lengths. Each run is compared with
   the original core from the same start. Flagged are:
   - **stuck**: the motor does not move where the original does;
   - **inside**: the motor's body ends up inside rock or a solid object;
   - **beyond**: the motor reaches a tile the original cannot (a gap jumped too far, a ledge
     climbed, a closed door passed);
   - **triggers**: a move trigger the original fires on a path and the motor does not, or the
     reverse.
4. **The jump envelope**: the longest and highest jump, with and without Leap, equal to the
   precision mode's within a small tolerance.
5. **The known spots**: SAVE3's three places from 2026-10-27 as fixed cases - level 3 at 16/50
   (fine 18/96, heading 272 degrees), 44/52 (fine 111/235, 121 degrees) and 3/5 (fine 127/78,
   z 774, 178 degrees).

The motor goes to the user only when the sweep is clean. A flagged case becomes a fixed check and
is fixed as a class, never as a single spot.

## 6. Phases

Each phase ends with the self-check green, Original and Smooth byte-identical.

- **M0 - the test bench** (M): load real levels into the checks, the sweep harness, run against
  the original core alone to learn its own baseline (where the original itself sticks), the
  comparison report.
- **M1 - horizontal motion on rock** (M): geometry for tiles and diagonals, the player's cylinder,
  collide and slide, sub-steps; the sweep's *stuck* and *inside* clean on all levels for walking.
  BUILT 2026-10-27: `UWCharacterMotor`, the sweep's third mover with restarts at walls (30 to 500
  frames a second) and a *halt* check (the motor stops only where the walls it touches hold the
  direction), `CharacterMotorChecks` for the hand-computed cases. 9000 runs and 55096 restarts:
  stuck where the core moved 0, halts 0, overlaps 0, inside 0. Two classes found and fixed on the
  way: contacts grazed by a rounding error stopped the body on convex corners (now a grazing
  tolerance), and stepping down off a ledge made the ledge a wall the body was in (the feet now
  stand on the highest floor under the body).
- **M2 - heights** (M): floors, steps, slopes, ledges, the ceiling; the vertical step with the
  original's constants; the jump envelope check; *beyond* clean.
  BUILT 2026-10-27: the motor carries a velocity and a vertical speed; off the ground the arc is
  exact on the precision mode's constants (Vz / 256 fine z a tick, the take-off JumpSpeed less
  JumpEulerOffset, gravity -4, Leap -2, a fall -4). The ground is the highest floor under the body
  within the step height, a slope at the centre as the core snaps; on the ground the feet follow
  up and down by a level, deeper drops fall unless the body holds at ledges (then the edge is a
  wall for the centre, and a step that would still leave the ground is undone, as the original's
  callback does); a floor reached in the air is landed on, rising as falling; the head stops at
  the ceiling. Envelope against the core in the same run: apex 172.9 against the original's 171
  (Leap 345.7 against 342), running reach 959 against the precision mode's 950 fine (Leap 1912
  against 1902). The sweep adds jumps (a third of the runs) and creeping (a sixth) and checks
  *beyond*: no climb of more than a step on the ground, no landing above the arc and a step, the
  head under the ceiling, a slow player never off the ground without a jump. Four classes found
  and fixed on the way: a slope judged by its high end (a staircase of slopes became a wall the
  body stood in; now its height nearest the body, in fine z), lifts in the air that kept the
  upward speed (a step gained twice; now a floor reached rising ends the jump, as the core rises
  to it), a diagonal tile that was a ledge losing its rock, and a slow player landing past an edge.
- **M3 - objects** (M): solid objects, standing on 3D models and bridges, doors open and closed,
  creatures, move triggers; *triggers* clean.
  BUILT 2026-10-28: the objects as the core's scan takes them - a box of the COMOBJ radius in
  eighths (4 the whole tile, a creature's one smaller), from its z up by its height; switches and
  lying things of height 0 passed; the player meets them with a reach of one eighth (the core
  shrinks a creature mover's box). A solid box is a wall where its height range overlaps the body's,
  unless its top is at or below the feet (a top fallen past is landed on) or it is a 3D model
  within the step height (a bridge, a barrel: they carry); no step up through the ceiling or into
  a solid object above the head (both refused as the core does); a body inside a box is pushed
  out the shortest way. Solid objects met are pushed (PushObject, speed 0xEB); move triggers and
  objects used when struck are met when the centre enters a new eighth cell, as the core scans,
  once a frame. Doors need no rule: an open door is raised by 0x18. The sweep draws no start
  inside an object, and a TRIGGER PASS approaches every move trigger of every level in straight
  lines (12 headings and side offsets each), the precision mode and the motor side by side up to
  the first contact: 339 approaches, 175 triggers on the shared lines, no mismatch.
  `CharacterMotorObjectChecks`: a closed door, an open one, a door slid along, a bridge over a
  drop, a creature blocking and pushed, a trigger walked through and passed, a step under a
  raised gate. Classes found on the way: a creature's top fallen past, a step up two pieces of
  furniture to a head above the ceiling, starts inside a box the clearance did not see, a step
  into a raised gate, a trigger fired on stepping out of it after a start inside.
- **M4 - in the game** (S): the mode in the Motion dialog, the hand-over in `UWPlayerMotion.Frame`,
  the state, water and lava, abilities, saving; the user's tour.
  BUILT 2026-10-28 (the tour open): `MotionSmoothing` is the mode (0 Original, 1 Smooth, 2 Modern;
  another number reads Smooth), `ModernRampTicks` Modern's response (at once by default), the
  Motion dialog shows Modern's response row in place of Smooth's. `UWPlayerMotion.Modern`: the
  frame keeps the shared part (fInitialCalc - commands, ramp, jump, fly; the state machine; the
  impact block; the tile) and hands the motion to the motor; back and the sides at three
  quarters (decision 1); the params stay the state the host and the save read, a change from
  outside taken over at the next frame (positions, and the state machine's vertical speed and
  gravity - the snapshot is taken before it); the contact by the motor's ground (an object: the
  ground; a floor: the core's own sample, the water edge too); a landing's impact as the core's
  bounce computes it, doubled where it would bounce on; the easy step asks the motor
  (decision 3, `UWCharacterMotor.TryStand`). `ModernMotionChecks`: the ledge rules every mode is
  held to (creep holds, run lands, fall hurts, levitation hovers and flies, slow fall, water),
  the slide without the view turning, back and sides at 0.75, the jump's apex, a placement taken
  over, the easy step, a trigger reaching the world.
- **M5 - the feel** (S): back and side speeds, the start and stop, the view - tuned with the user.

UW2 needs no phase of its own here: its rules object comes with UW2's phase 4, its level data
through the per-game readers of UW2's phase 1, which the motor reads through `IUWMotionWorld` as
the core does.

## 7. Risks

- **Puzzles tuned to the original's motion** - jump gaps, ledges, trigger reach. Mitigation: the
  *beyond* and *triggers* parts of the sweep, the jump envelope check, the original's vertical
  constants.
- **Objects are many kinds** (doors with states, bridges, portcullises, 3D models, creatures).
  Mitigation: M3 on its own, every kind a check, the core's tables as the source of shapes.
- **Two motions to maintain.** Mitigation: the motor is the player's only; the shared part
  (commands, state, triggers, saving) stays one code path.
- **The sweep's cost**: thousands of runs per level must stay within a self-check's minutes; a
  short sweep in the regular self-check, the full one on demand.

## 8. Decisions (by the user, 2026-10-27)

1. **Back and side speeds in Modern**: three quarters of the forward speed, both (the original's
   are a fifth and a quarter). Jumps still run on the forward speed, so no gap changes.
2. **The player's shape**: round - a vertical cylinder, its radius matched to the core's box so
   doorways and corridors fit as before; the sweep checks that no place the original cannot reach
   becomes reachable.
3. **Modern in both control schemes**: a choice in the Motion dialog whatever the scheme; the
   classic scheme's easy step asks the motor, not the core, whether a target spot can be stood on.
4. **The start and stop**: the ramp row applies to Modern too, with a value of its own whose
   default is at once (0), so Smooth keeps its default.
5. **When**: M0 to M5 before UW2's phase 0. The test bench of M0 then guards UW2's phases 0 and 1
   against changes to UW1 as well.
