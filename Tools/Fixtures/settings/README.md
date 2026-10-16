# Settings fixtures

Fixtures for `Assets/Editor/UWSettingsFixtureCheck.cs` (menu UW / Check settings fixtures,
or `-executeMethod UWSettingsFixtureCheck.Run` in batch mode). Each `.json` is a settings.json
as some version of the game wrote it; the `.expected.txt` next to it records what every public
property of `UWUserSettings` reads from it. The check fails when a value reads differently.

- `v0.1.0.json`: the field set of the first release (October 2026), values chosen away from
  the defaults.
- `legacy-quirks.json`: values the game must normalise on reading (a mouse speed of 0 reads
  as 1, an invalid frame count as 8, modes out of range as Original, and so on). These are the
  rules a settings table must keep.
- `current-defaults.json`: an empty file, so every property reads its default.

Record the expected files once with `UW_RECORD=1` in the environment after a deliberate change
to how a value is read; otherwise run the check and read the diff. Real settings files of a
player are not kept here.
