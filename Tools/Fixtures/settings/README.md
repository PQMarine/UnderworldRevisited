# Settings fixtures

Fixtures for `Assets/Editor/UWSettingsFixtureCheck.cs` (menu UW / Check settings fixtures,
or `-executeMethod UWSettingsFixtureCheck.Run` in batch mode). Each `.json` is a settings.json
as some version of the game wrote it; the `.expected.txt` next to it records what every public
property of `UWUserSettings` reads from it, and the `.written` file what the game writes back
after reading it (JsonUtility's bytes, line ends aside). The check fails when a value reads
differently or a byte is written differently.

The engine-free self-check (group SettingsTable, `Tools/UWCheckLib/Checks/SettingsTableChecks.cs`)
reads the same `.json` files through the settings table (`UWUserSettingsTable`) and has to
write every `.written` file again byte for byte - so the table, its key order and its number
format stay tied to what Unity writes.

- `v0.1.0.json`: the field set of the first release (October 2026), values chosen away from
  the defaults.
- `legacy-quirks.json`: values the game must normalise on reading (a mouse speed of 0 reads
  as 1, an invalid frame count as 8, modes out of range as Original, and so on). These are the
  rules a settings table must keep.
- `legacy-quirks-2026-10-24.json`: more of them, for the entries added since (the swimming
  view, the strengths), an interface preset of -1 beside an own layout, and a key nobody reads.
- `full-2026-10-24.json`: every field away from its default, a path with backslashes, quotes
  and a non-ASCII letter, a tiny float (JsonUtility writes it without an exponent), and a key
  nobody reads.
- `current-defaults.json`: an empty file, so every property reads its default.
- `debug-folds-2026-10-25.json`: the debug panel's folded sections (cleanup 8c), the one text added
  since; every other property at its default.
- `pointer-speed-2026-10-25.json`: the pointer's speed of the game's own pointer (cleanup 7.7B), the
  one number added since; every other property at its default.
- `window-theme-2026-10-25.json`: the windows' theme Stone and its ground Clouds (cleanup phase 8d),
  the two numbers added since; every other property at its default. Every older fixture reads
  Plain and the gradient.
- `language-2026-10-26.json`: the language chosen in the Game window (translation wave 1), the one
  text added since; every other property at its default. Every older fixture reads it empty (English).
- `modern-motion-2026-10-28.json`: the motion Modern (the character motor) and its own ramp of 4 ticks,
  the mode's new value and the one number added since; every other property at its default. Every older
  fixture reads its motion as before (a non-zero switch as Smooth) and Modern's ramp at once (0).
- `game-2026-10-28.json`: the game chosen in the Game window (UW2 plan phase 0 step 5), the second's
  id, the one text added since; every other property at its default. Every older fixture reads it
  empty (the first game).

Record the expected and written files once with `UW_RECORD=1` in the environment after a
deliberate change to how a value is read or written (a new field changes every `.written`
file); otherwise run the check and read the diff. Real settings files of a player are not kept
here.
