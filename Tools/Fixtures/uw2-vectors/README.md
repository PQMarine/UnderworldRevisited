# UW2.EXE test vectors (from Underworld Exhumed)

These ten tables are copied unchanged from Underworld Exhumed's `uw2/vectors/` (commit 81cc4b5,
https://github.com/abedegno/underworld-exhumed, MIT licence, see THIRD_PARTY_NOTICES.md). Each
holds inputs and outputs of one routine of Ultima Underworld II, computed by running that routine's
bytes from the GOG `UW2.EXE` in an emulator. They are numbers only: no code or data of the game.
UE's own README describes every table, its columns and its source routine.

Where a routine draws random numbers, `seed` is the whole 32-bit state of the game's `rand()`
before the call and `seed_out` after it (Turbo C: `seed = seed * 0x015A4E35 + 1`, the result bits
16 to 30). The self-check (group Uw2Vectors, `Tools/UWCheckLib/Checks/Uw2VectorChecks.cs`)
starts the game's generator from `seed` (`UWOriginalRandom.FromState`), runs our rule code and
compares the result and the state afterwards.

Decision 3 of `Docs/UW2-PLAN.md` (2026-10-28) took them as fixtures. The routines both games share
are held to them now; `check_res`, `debris_type` and `is_sharp` differ between the games and are
checked when the second game's variants come (phase 4).
