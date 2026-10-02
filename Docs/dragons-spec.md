# UW1 HUD dragons - implementation spec (from UW.EXE disassembly)

All line numbers refer to `UW1_asm.asm` in the session scratchpad. "L" = listing line.
Image indices are DRAGONS.GR indices (0..35). Coordinates are 320x200, top-left origin,
unless marked "EXE y" (the engine's y-up value).

## 0. Summary

- Two dragons, si=0 left, si=1 right. Each has one "current animation" (1 scroll, 2 nod,
  3 cover) and one "pending request". The tail has a separate idle wag.
- The state machine advances one step every 64 ticks of the 256 Hz game timer = **0.25 s per
  step** (only runs in the main 3D game mode, not in the map or conversation screens).
- Head animations are drawn as overlays on top of the static head (scroll, nod) or replace the
  head (cover). Tail wag replaces the tail picture.

## 1. Sprite engine calls (seg000)

The HUD uses a small retained sprite list (64 slots of 16 bytes in seg054, handle = slot no.).

| Call | Args (C order) | Meaning | Evidence |
|---|---|---|---|
| `seg000_2(layer, w, h)` | layer 0..3, save-buffer w/h | allocate handle; stores `layer` at slot+9; if layer != 0 allocates a background-save buffer w x h (`seg001_5A`); sets flag 0x10 if `seg048_DC7`=1 at creation | L52-187 |
| `seg000_A6(h, x, y, w, h)` | | set rect: x word slot+2, **EXE y** byte slot+4, w slot+5, h slot+7 | L243-333 |
| `UIDrawRelated_seg000_EB(h, imageId)` | | set image (slot+0Bh), set visible flag 2, mark dirty | L339-401 |
| `seg000_155(h)` | | clear visible flag 2 (hide; background restored from save buffer) | L461-510 |
| `seg000_330()` | | flush: restore hidden, then draw layers 0,1,2,3 in ascending order (layer 3 on top) | L799-1093 |

- Overlap test in `seg000_229` uses `y - h` as the lower edge (L756-766), i.e. EXE y is the
  top edge in a y-up system. **Screen top row = 199 - EXE y.** Verified by pixel match: image 0
  placed with EXE (36,65) matches MAIN.BYT surroundings exactly at top-left (36,134); image 18
  at (228,134); overlays line up seamlessly on the resting head (contact sheet built from
  DRAGONS.GR + MAIN.BYT + PALS.DAT palette 0).
- Images are drawn at their own size, top-left anchored at the rect origin; the rect w/h is only
  the save/overlap box. Palette index 0 is transparent (UNVERIFIED in code; visually required -
  head image corners are 0 and would otherwise paint black over the scroll).
- Flag 0x10 (`seg048_DC7`) meaning: UNVERIFIED (used for the head handle and the anim overlay).

### Image ids

`seg009_221` (L57716-57776): id >= 0x2000 -> pool index `(id - 0x2000) + dseg_15E4`.
`dseg_15E4` is set right before loading lfti, flasks, compass, dragons, inv... (L358519-358561).
LFTI.GR 12 + FLASKS.GR 77 + COMPASS.GR 20 = 109 = 0x6D, so **DRAGONS.GR index = id - 0x206D**.

## 2. Handles and static placement (seg036_3087_16E, L117247-117665)

Created once (guard `dseg_907`), then the static frames are (re)drawn on every call (L117529-117615).
`seg036_3087_16E` is called from `seg024_24DC_34D` (L85654), which is called when the game screen
bitmap is loaded (`ovr109_37B`, L351730-351745), and at L389733.

Per dragon si (tables in section 5):

| Handle | Created | Rect (x, EXE y, w, h) | Screen top-left | Image drawn | Part |
|---|---|---|---|---|---|
| `8D7[si]` | `seg000_2(2,13,10)` | (`81C[si]`, 65, 13, 10) | L (36,134), R (228,134) | `874[si]` = img 0 / 18 | neck piece above head |
| `8DB[si]` | `seg000_2(2,37,23)`, DC7=1 | (`820[si]`, 54, 37, 23) | L (36,145), R (204,145) | `878[si]` = img 1 / 19 | resting head |
| `8DF[si]` | `seg000_2(0)` (no save buffer) | (`854[si]`, 134, 12, 28) | L (40,65), R (224,65) | `0x207B + (si?0x12:0)` = img 14 / 32 | tail (rest frame) |
| `78F[si]` (dseg 78F / 791) | lazily in Dragons: `seg000_2(3,40,24)`, DC7=1 (L118838-118864) | set per animation | see section 3 | anim frames | head overlay |

MAIN.BYT contains the green body between tail and neck (e.g. rows ~97-133 at x~42-52) but not the
neck pieces, heads, or tail frames; those all come from DRAGONS.GR at runtime.

## 3. Animations

Step = one call of `Dragons_seg036_3087_A20` for that dragon (0.25 s, see section 6).
s/e = start/end frame from tables 91E/92A. Overlay rect from 824/830/83C/848 indexed
`si*6 + (anim-1)*2`.

### Per-animation data

| Anim | Side | Frames (s..e) | Overlay rect (x, EXE y, w, h) | Screen top-left of overlay | Static head |
|---|---|---|---|---|---|
| 1 scroll | L | 2..5 (33x13,33x13,33x13,32x14) | (40, 44, 33, 14) | (40,155) | stays visible underneath |
| 1 scroll | R | 20..23 (33x13,33x13,33x13,33x14) | (204, 44, 34, 14) | (204,155) | stays visible |
| 2 nod | L | 6..9 (23x13 each) | (48, 54, 24, 16) | (48,145) | stays visible |
| 2 nod | R | 24..27 (24x12,24x12,24x12,24x13) | (204, 54, 24, 16) | (204,145) | stays visible |
| 3 cover | L | 10..13 (37x23,37x18,34x14,34x14) | (36, 54, 37, 23) | (36,145) | hidden at start, redrawn with img 1 at end |
| 3 cover | R | 28..31 (38x23,38x18,40x14,38x14) | (200, 54, 38, 23) | (200,145) | hidden at start, redrawn with img 19 at end |

Scroll frames animate the legs/forearms (lower part of the head image, offset (4,10) on the left);
nod frames replace the head (offset (12,0) on the left, (0,0) on the right).

### Phase machine (phase var `78B[si*2]`, frame counter `918[si*2]`, loop counter `359B[si*2]`)

Common start (L118905-118926): if phase==0 and pending `362C[si]` != 0: current `35E6[si]` = pending,
phase = 1. Pending is cleared inside phase 1 of each animation.

**Anim 1 scroll** (jump table `106E`, L119882; phase 2 unused)
- P1 (L118979-119068): clear pending; set rect; counter=s; loop=1; phase=3; fall through to P3.
- P3 (L119070-119149): draw counter++; if counter > e: counter=s, loop--. If pending!=0 or loop==0: phase=4.
- P4 (L119151-119204): draw counter++; if counter > e: phase=5.
- P5 (L119206-119219): hide overlay; current=0; phase=0.
- Uninterrupted sequence: **L 2,3,4,5,2,3,4,5 then hide** (R 20..23 twice). 8 drawn steps + 1 = 2.25 s.

**Anim 2 nod** (table `1064`, L119877)
- P1 (L119242-119329): clear pending; set rect; counter=s; loop=3; phase=2; fall through.
- P2 (L119331-119384): draw counter++; if counter > s+1: phase=3.
- P3 (L119389-119479): draw counter++; if counter > e: counter=s+2, loop--. If pending!=0 or loop==0: phase=4.
- P4 (L119484-119526): draw counter--; if counter < s: phase=5.
- P5 (L119529-119540): hide overlay; current=0; phase=0.
- Uninterrupted: **L 6,7,8,9,8,9,8,9,8,7,6 then hide** (R 24,25,26,27,26,27,26,27,26,25,24).
  11 drawn steps + 1 = 3.0 s.

**Anim 3 cover** (table `105A`, L119872)
- P1 (L119559-119684): clear pending; **hide static head `8DB[si]`**; set rect; counter=s; loop=6; phase=2; fall through.
- P2 (L119686-119739): draw counter++; if counter > e: counter-- (=e), phase=3.
- P3 (L119744-119764): no draw. If pending!=0: phase=4; else loop--, if loop==0: phase=4.
- P4 (L119767-119822): counter--; draw counter; if counter==s: phase=5.
- P5 (L119825-119861): hide overlay; draw `8DB[si]` with `878[si]` (img 1 / 19); current=0; phase=0.
- Uninterrupted: **L 10,11,12,13, hold 13 for 6 more steps, 12,11,10, restore head**
  (R 28..31, hold, 30,29,28). 14 steps = 3.5 s; frame 13 visible 7 steps (1.75 s).

Step order inside a call: tail idle (section 4) first, then start-check, then the switch. Case
current==0 (L118949-118962): if idle flag `7DA[si]`==0, clear bit (4+si) in `785` so the function
stops being called. Current value > 3: nothing happens (not used by any caller).

## 4. Tail idle wag

Trigger (in `seg036_3087_5A5`, L118142-118174), evaluated once per 0.25 s step (same gate as section 6):
```
r = RNG()                 ; RNG_seg005_DE7 returns 0..32767 (L32393-32411)
if r < 0x666 (1638, ~5.0%) and 7DA[r&1] == 0:
    7DA[r&1] = 1
    785 |= 1 << ((r + 4) & 1)     ; = bit 0 or 1, NOT bit 4/5
```
A separate earlier roll with the same threshold does the flask bubbles (`7D8`, L118108-118139);
the tail roll always follows it. Expected: one tail request every ~5 s, random side.

Execution (L118866-118902), in each Dragons call for si with `7DA[si]==1`:
`draw 8DF[si] with 858[si*14 + 91C*2]; 91C++; if 91C >= 7: 91C = 0, 7DA[si] = 0`.
- Left sequence: img **14,15,16,17,16,15,14**; right: **32,33,34,35,34,33,32**. 7 steps = 1.75 s.

Quirks (verified in code):
1. The trigger sets bit 0/1 (HP/mana flask redraw) instead of bit 4/5. The flag stays pending
   and the wag only plays when that dragon's function is running anyway, i.e. during/at the start
   of a head animation of the same dragon (bit 4/5 set by `UpdateUIAnim`), and it keeps the
   function alive until the wag finishes (case 0 does not clear the bit while `7DA[si]`==1).
   In practice the scroll animation is frequent, so wags appear piggy-backed on head animations.
   Decide whether to replicate this or play the wag independently.
2. Counter `91C` is shared by both dragons. If both flags are set, each step advances it twice
   (left runs before right in the same pass), so frames interleave and one side ends early.

## 5. Data tables (decimal; dseg listing L281631-281801)

The block 810h-83Dh is printed by IDA as `unicode` strings (L281632-281634); bytes were read
from the raw listing (chars `A B $ E4 $ CC ( 0 $ CC CC C8 , 6 6 , 6 6 !`).

| Table | Indexing | Values |
|---|---|---|
| 81C | [si] claws/neck x | 36, 228 |
| 820 | [si] head x | 36, 204 |
| 824 | [si*3 + anim-1] overlay x | L: 40, 48, 36; R: 204, 204, 200 |
| 830 | overlay EXE y | L: 44, 54, 54; R: 44, 54, 54 |
| 83C | overlay w | L: 33, 24, 37; R: 34, 24, 38 |
| 848 | overlay h | L: 14, 16, 23; R: 14, 16, 23 |
| 854 | [si] tail x | 40, 224 |
| 858 | [si*7 + i] tail idle ids | L: 8315,8316,8317,8318,8317,8316,8315 (0x207B..; img 14,15,16,17,16,15,14); R: 8333,8334,8335,8336,8335,8334,8333 (img 32..35..32) |
| 874 | [si] neck ids | 8301 (img 0), 8319 (img 18) |
| 878 | [si] rest head ids | 8302 (img 1), 8320 (img 19) |
| 91E | [si*3 + anim-1] start ids | L: 8303, 8307, 8311 (img 2, 6, 10); R: 8321, 8325, 8329 (img 20, 24, 28) |
| 92A | end ids | L: 8306, 8310, 8314 (img 5, 9, 13); R: 8324, 8328, 8332 (img 23, 27, 31) |
| 8DB/8DF/8D7/8CF | [si] handles | runtime |
| 78B, 78F | [si] phase, overlay handle | runtime (78D/791 = right) |
| 918, 359B | [si] frame counter, loop counter | runtime |
| 7DA | [si] byte tail idle flag | runtime |
| 91C | shared tail idle counter | runtime |
| 35E6/35E7 | current anim L/R (= 35E2+mode) | runtime |
| 362C/362D | pending request L/R (= UIAnimValues 3628+mode) | runtime |

Constants in code: overlay buffer 40x24 (L118848-118852); loops 1/3/6 (L119063, L119324, L119679);
idle length 7 (L118895); neck rect EXE y 65, 13x10; head EXE y 54, 37x23; tail EXE y 134, 12x28.

## 6. Timing and where it runs

- Game timer: `seg014_1DC5_1D` registers `seg014_1DC5_B` (increments the 32-bit
  `PITTimerGlobal`, L63454-63466) and sets its rate with `seg020_94D(handle, 256)` (L63517-63526);
  `seg020_94D` converts Hz to microseconds via 1000000 (0x0F4240, L78282-78290). **256 Hz**.
  (Function names of the timer library are inferred, the numeric path is in the listing.)
- `seg036_3087_5A5` (UI update) is slot 13 of the per-mode game-loop table `GameLoopFunctions`
  (`dseg_1320`, L282415); mode 0's persistent mask `13AC` = 0x3800 (slots 11,12,13, L282528-282529),
  executed each loop iteration by `seg011_1CA3_2C` (L59149-59249).
- In `5A5` (L118057-118105): the `785` draw functions (flasks bubbles, dragons) and the random
  rolls run only when `(timerLowByte >> 6) != (last >> 6)`, i.e. once per 64 ticks = **0.25 s**,
  at most once per loop iteration (a slow frame never catches up multiple steps). Then
  `seg000_330` flushes the sprites (L118219).
- Bits: dragons are functions 4 and 5 in `UIDrawFunctions` (8F3/8F7 -> `Dragons_seg036_3087_A20`,
  L281757-281758), called in order 0..8, so left before right.

### Modes

- Mode table records: mode 1 (automap) at 132C (`OpenAutomap`, L282421, mask 0x1000 = slot 12 only),
  mode 2 (conversation) at 136C (`BeginConversation`, L282470, mask 0). `5A5` is only in mode 0.
  **Dragons do not animate on the map or in conversations**; their state is frozen and resumes
  on return (no reset call on mode switch; `seg036_3087_107`, the only full reset, is called
  from L351846 during player/game setup). On return `16E` redraws neck, resting head and tail
  rest frame; an in-progress cover animation therefore shows the resting head again until its
  P5 (UNVERIFIED visually).
- Inventory, stats, rune panels, combat are all mode 0: dragons animate there.

## 7. Requests (`UpdateUIAnim_seg036_3087_3DF`, mode 4, L117857-117945)

Callers push (value, mode=4):
- **1 scroll**: `seg043_37F0_1CA` (message scroll scrolls up, L138121-138129) and `seg043_37F0_8EB`
  (L139349-139355), both only when the active scroll `dseg_3650 == 0x0A60` (main message scroll).
  THE SAME FORK ALSO TURNS THE SCROLL EDGES (section 11): the dragon request and
  `seg043_37F0_11E` stand in the same branch, `seg043_37F0_163` in the other, so the left
  dragon and the rolling strips always move together, and in a conversation the strips move
  while the dragons are not on screen at all.
- **2 nod**: `AwardKillEXP_seg022_1725` (L83686-83706) when the killed object's `item_id >> 6 == 1`
  (NPC class, ids 64..127), before the fanfare.
- **3 cover**: `PlayerUpdateTick_seg024_24DC_3A4` (L85712-85745), every loop in mode 0, with
  `d` = player object byte +11h (damage accumulated this tick), `hp` = player object byte +8,
  `maxHP` = `dseg_5626`: request if `d*4 > maxHP` **or** (`hp < 16` and `d > 0`). Then byte +11h = 0 (L85754).

Side selection (A0/A1 = current 35E6/35E7, P0/P1 = pending 362C/362D, v = value):
```
if v == P0 or v == P1 or v == A0 or v == A1: return        ; ignored, no bit set
if A0 == 0 and A1 == 0:           side = RNG() & 1
elif A0 != 0 and A1 == 0:         side = 1
elif A0 == 0 and A1 != 0:         side = 0
else (both busy):
    if P0 == 0 and P1 == 0:       side = RNG() & 1
    elif P0 != 0 and P1 == 0:     side = 1
    else:                         side = 0                  ; overwrites P0
P[side] = v;  785 |= 1 << (4 + side)
```
Effects of a new request while busy:
- The same animation is never queued or restarted while it runs or is pending on either dragon.
- A pending request on the busy dragon shortens the running one: scroll leaves its loop
  (P3 -> P4, finishes the current pass), nod leaves its loop and plays backward from the current
  frame, cover ends its hold early. The pending one starts on the step after P5.
- A later request to the same side overwrites the pending value (only one slot).

## 8. Suggested port (Unity, UWGameUI 320x200 top-left)

- Create per side: Neck (img 0/18 at (36,134)/(228,134)), Head (img 1/19 at (36,145)/(204,145)),
  Tail (img 14/32 at (40,65)/(224,65)), Overlay (hidden). Draw order: tail, neck, head, overlay.
  Use `fCreateImage(name, parent, x, -y, w, h, ...)` with image native size, like COMPASS.GR.
- Tick a 0.25 s accumulator only while the main game screen is active (not map/conversation);
  run at most one step per Update.
- Implement sections 3, 4, 7 literally; for each draw set overlay sprite + position from the
  table for that animation and show it; P5 hides it (cover also restores head).

## 9. Differences from UnderworldGodot `uimanager_dragons.cs`

- Godot uses 0.2 s per frame and fixed 6-frame sequences ({2,3,4,5,4,3}, nod {8,9,8,9,8,8},
  cover {10,11,12,13,12,11}); EXE uses 0.25 s and the sequences above (scroll forward twice,
  nod 6..9 with loop, cover with a 6-step hold).
- Godot has a tail wag as animation id 0 that no caller uses; the EXE triggers it randomly
  (5% per 0.25 s) and never from game events.
- Godot places right-side frames with ad-hoc canvas offsets; EXE uses absolute rects from
  tables 824/830 (right cover at x=200, 4 px left of the resting head at 204).
- Godot has no pending-request/interrupt logic and allows the same animation on both sides.

## 10. DRAGONS.GR (C:\UW\DATA, 16264 bytes)

Header: type 1, 36 images; all images type 4 (8-bit uncompressed).

| idx | w x h | role | idx | w x h | role |
|---|---|---|---|---|---|
| 0 | 13x11 | L neck (static) | 18 | 10x11 | R neck (static) |
| 1 | 37x23 | L resting head (static) | 19 | 34x23 | R resting head (static) |
| 2 | 33x13 | L scroll 1 | 20 | 33x13 | R scroll 1 |
| 3 | 33x13 | L scroll 2 | 21 | 33x13 | R scroll 2 |
| 4 | 33x13 | L scroll 3 | 22 | 33x13 | R scroll 3 |
| 5 | 32x14 | L scroll 4 | 23 | 33x14 | R scroll 4 |
| 6 | 23x13 | L nod 1 | 24 | 24x12 | R nod 1 |
| 7 | 23x13 | L nod 2 | 25 | 24x12 | R nod 2 |
| 8 | 23x13 | L nod 3 | 26 | 24x12 | R nod 3 |
| 9 | 23x13 | L nod 4 | 27 | 24x13 | R nod 4 |
| 10 | 37x23 | L cover 1 | 28 | 38x23 | R cover 1 |
| 11 | 37x18 | L cover 2 | 29 | 38x18 | R cover 2 |
| 12 | 34x14 | L cover 3 | 30 | 40x14 | R cover 3 |
| 13 | 34x14 | L cover 4 | 31 | 38x14 | R cover 4 |
| 14 | 12x32 | L tail rest / wag 1 | 32 | 12x32 | R tail rest / wag 1 |
| 15 | 12x32 | L tail wag 2 | 33 | 12x32 | R tail wag 2 |
| 16 | 12x32 | L tail wag 3 | 34 | 12x32 | R tail wag 3 |
| 17 | 12x32 | L tail wag 4 | 35 | 12x32 | R tail wag 4 |

Tail images use palette index 1 (not 0) as background around the dragon and include pillar pixels.

## 11. The rolling scroll edges (SCRLEDGE.GR), read 2026-09-22

Not dragons, but they hang on the same trigger and were found while reading it (the user asked
what the several images at the sides are: a roll animation).

- **The file** holds 22 strips 4 pixels wide, in four groups: 0-4 and 5-9 are 29 high (main
  screen, left and right of the message box), 10-15 and 16-21 are 27 (conversation parchment).
  Inside a group every image is the SAME texture moved by six pixels, so a group is a closed
  loop; the main screen rolls upwards in five frames, the conversation downwards in six
  (measured on the exported images: 87 to 100 percent of the rows match at that shift, and
  between the groups nothing matches).
- **Image numbers**: pool base 0x2000 plus the 213 images loaded before SCRLEDGE.GR (LFTI 12,
  FLASKS 77, COMPASS 20, DRAGONS 36, INV 7, POWER 14, EYES 10, CHAINS 16, SPELLS 21), so
  strip 0 is id 0x20D5 - the same counting as for DRAGONS.GR in section 0.
- **When they turn**: one frame per SCROLL of the text area, not per message. The printer
  `seg043_37F0_4BF` (label 5E1) calls the scroll-up `seg043_37F0_1CA` for every line that would
  run past the bottom, `WriteTextWithMore_seg043_37F0_257` calls it once to make room for
  [MORE], and `seg043_37F0_8EB` does it when the area is cleared and when a conversation starts.
- **`seg043_37F0_11E`** (L137923-137972, main screen): draws counter + 0x20D5 at x 11 and
  counter + 0x20DA at x 306, both at EXE y 30, then counts up and wraps at 5.
- **`seg043_37F0_163`** (L137978-138048, conversation): draws counter + 0x20DF at x 52 and
  counter + 0x20E5 at x 220, each at EXE y 148, 121 and 94, all three with the SAME frame, then
  wraps at 6.
- The counters `dseg_A94` (main) and `dseg_A96` (conversation) are touched nowhere else and are
  never reset, so the phase carries from one conversation into the next.
- EXE y here is the top counted from the bottom of the 200 line screen: 199 - 30 = 169 is the
  top edge of our message box, and 199 - 148/121/94 gives 51, 78 and 105, the three heights
  measured in CONV.BYT. That is what confirmed the image numbers.
- Ours: `UWScrollEdgeRules` with `UWHudMessageLog` and `UWConversationScreen` (built 2026-09-22).

## UNVERIFIED items

- Palette index 0 transparency and the meaning of sprite flag 0x10 / `seg048_DC7`.
- Exact conditions under which `seg043_37F0_1CA` / `8EB` run (which text events scroll the main
  scroll); whether the conversation screen ever uses scroll struct 0x0A60 (requests made then would
  queue and play after returning).
- Visual result of returning from map/conversation mid-animation.
- Names/semantics of the timer library calls `seg020_75E` / `seg020_94D` (numbers verified).
