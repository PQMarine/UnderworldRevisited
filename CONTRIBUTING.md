# Contributing

Thank you for wanting to help with [Underworld Revisited](README.md). Bug reports, comparisons
with the original game and code are all welcome. This page explains how contributions are
handled here, because one part of it differs from most GitHub projects.

## How pull requests are handled

Pull requests are reviewed and, when they fit, **merged here** - you stay the author of your
commit.

One thing works differently from many projects: the game is developed in a separate working
copy, and its progress reaches this repository in batches ("exports"), each one a commit on
top of `main`. So:

- **Base your branch on the latest `main`**, and keep pull requests small. When `main` has
  moved on and your pull request no longer merges cleanly, you may be asked to update your
  branch.
- A merged change is taken into the working copy as well, so the next export keeps it. Now
  and then it is adjusted there to fit code that has changed meanwhile; your commit stays in
  the history either way.
- When a pull request cannot be merged as it is (for example because the code it changes was
  rewritten in the meantime), it is carried over by hand instead. Then you are credited as
  co-author of the commit that contains it (`Co-authored-by`, which GitHub shows under your
  name), and the pull request is closed with a link to that commit.
- Merged contributions are mentioned in the release notes of the next release.

## Before you start

- **Larger changes: open an issue first.** The code still moves quickly between releases (see
  [ROADMAP.md](ROADMAP.md)), and a short talk beforehand saves work on both sides. Small
  fixes can come as a pull request straight away.
- **Translations** of the port's own texts (`Languages/<code>.tsv`) are welcome as a pull
  request; [Docs/TRANSLATING.md](Docs/TRANSLATING.md) explains the files and the tools. A
  translation of the game's own texts (`<code>.strings.tsv`) never comes into this repository -
  see the section on publishing there.
- **For the game itself, the original decides.** The rules of the game - combat, magic,
  creatures, traps, objects - and the classic control scheme are meant to behave exactly like
  Ultima Underworld 1. A change there should say how the original behaves - observed in the
  game, or read from its code - and where. The deliberate differences are listed in
  [Docs/DEVIATIONS.md](Docs/DEVIATIONS.md); anything else that differs is a bug.
- **The modern side is freer.** The modern control scheme, the interface presets, the
  Remastered mode, the palette effects and the options are this project's own additions and
  not bound to the original. Improvements and new ideas there are welcome, even when they
  depart from how the original plays; for a larger one, open an issue first. They should only
  leave the rules of the game alone, so both schemes play the same game.

## Bug reports

A good report says:

- what you did, what happened and what you expected;
- whether the original game does it differently (if you can check);
- the release you use (for example 0.10.0), the operating system and the control scheme
  (classic or modern), and the language if it is not English;
- whether a `rules.json` is in use (it changes the game's numbers);
- for a crash or a hang, the file `Player.log` (Windows:
  `%USERPROFILE%\AppData\LocalLow\Underworld Revisited\Underworld Revisited\`, Linux:
  `~/.config/unity3d/Underworld Revisited/Underworld Revisited/`).

A save game that shows the problem helps a lot. It is your own save, so attaching it is up
to you.

## Code

- How to build the game and the tools is in [Docs/BUILDING.md](Docs/BUILDING.md). You need
  your own copy of Ultima Underworld from GOG.com.
- Run the self-check before you send a change; it must pass:
  `dotnet run --project Tools/UWSelfCheck -- "<your GOG installation>"`. A rule that can be
  tested without Unity should get a case there.
- **Where things go:** the game rules live in the engine-free layer `Assets/UWDataImport`
  (no `UnityEngine`), the Unity side in `Assets/UWScripts`.
- **Style:** follow the file you change. Names carry their kind as a prefix (`mO`, `lO`,
  `pO`, `li`, `pb`, `ls`, ...), private methods start with `f`. Comments and messages are in
  English; a comment explains why, and for a game rule names the original routine it follows.

## What must not go in

- **Nothing of the original game:** no game files, graphics, sounds, texts or excerpts of
  its program code. The game is read from your own installation at runtime.
- **No code copied from projects under an incompatible license** (for example GPL code):
  this project is under the MIT License. Formulas and data tables may be taken from other
  projects when their license allows it; say where they come from, as
  [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) does.

## License

By sending a contribution you agree that it is released under this project's
[MIT License](LICENSE).
