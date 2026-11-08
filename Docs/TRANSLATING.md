# Translating

How to translate [Underworld Revisited](../README.md) into another language.

- [Two files per language](#two-files-per-language)
- [Getting started](#getting-started)
- [The port's own texts](#the-ports-own-texts)
- [Words in the pictures](#words-in-the-pictures)
- [The game's texts](#the-games-texts)
- [Names: the name format](#names-the-name-format)
- [Slots, select and plural](#slots-select-and-plural)
- [The grammar header](#the-grammar-header)
- [Fonts and input methods](#fonts-and-input-methods)
- [Checking a translation](#checking-a-translation)
- [Trying it in the game](#trying-it-in-the-game)
- [Publishing](#publishing)

Every example word on this page is made up.

## Two files per language

A language is a code (`fr`, `ru`, `zh-TW`) and up to two files in the folder **Languages** beside
the game (beside the executable; in the Unity editor, the project folder):

| File | What it holds |
|---|---|
| `Languages/<code>.tsv` | The port's own texts: menus, settings, help, the modern interface, and the frames the game's sentences are built in ("You see {0}."). About 870 texts. |
| `Languages/<code>.strings.tsv` | The game's own texts from STRINGS.PAK: messages, object names, books, scrolls, conversations. About 7,100 strings. |

Either file may be missing, and any line may be missing: what a file does not hold stays English.
Both are UTF-8 text with one entry per line and TAB between the columns, so a text editor or a
spreadsheet is enough. Lines starting with `#` are comments. In a text, `\n` is a line break and
`\t` a TAB.

The classic interface draws its texts with the original's pixel fonts. They know ASCII and, added
by the port, `ä ö ü Ä Ö Ü ß`. A language that needs other letters names a font of its own (see
[Fonts and input methods](#fonts-and-input-methods)); none is shipped with the game.

## Getting started

### With the translation kit

Every release has a **translation kit** beside the game:
`UnderworldRevisited-<version>-TranslationKit-win64.zip` (or `...-linux64.tar.gz`). It holds the
tool `uwdump` as one file that needs nothing installed, this guide, `Languages/en.tsv` (the port's
texts in English) and `Languages/de.tsv` (a complete German example), and a README. It holds no
text of the game. Open a terminal in its folder; `<game>` is your GOG or Steam installation (the
folder with `game.gog` or `game.bin`; without it uwdump looks in the usual places):

1. The port's texts: `uwdump text-template <code> --out Languages/<code>.tsv` (or copy
   `Languages/en.tsv`), then translate the texts and keep the ids.
2. The game's texts: `uwdump strings-template <game> <code> --out Languages/<code>.strings.tsv`
   makes a template from **your own** copy of the game (`--empty` writes the English as comments
   and leaves the texts empty).
3. Check both with `validate-tsv`, `fit` and `looks` ([Checking a translation](#checking-a-translation))
   and try them in the game ([Trying it in the game](#trying-it-in-the-game)).

**After a new release** the port may have new texts. `uwdump text-template <code> --from
Languages/<code>.tsv --out <code>-new.tsv` brings your file up to date: your lines stay exactly as
you wrote them, every new id is added as a comment with its English (`# id TAB English` - remove
the `# ` and translate; until then the game shows the English), and the ids that are gone are
listed and kept at the end as `# obsolete: ...` comments, in case a text only moved to a new id.

### From the repository

With the source: `dotnet run --project Tools/UWDump -- <command> ...` runs the same commands (the
.NET 8 SDK is enough, no Unity). In the Unity editor, *Underworld Revisited / Text / Write the
English texts...* writes the same `en.tsv` as `uwdump text-template`. `Tools/Release/MakeReleaseZip.ps1
-KitOnly` makes the kit. The repository never contains the game's text, English or translated.

## The port's own texts

One line per text: the id, a TAB, the text.

```
setting.Language	Lingvo
grammar.look.you_see	Vu zei {0}.
```

`{0}`, `{1}` are filled in by the game. A text must not name a `{n}` the English does not have; it
must keep every `{n}` of the English, except in a frame that takes pieces of the game's text (see
[Slots](#slots-in-the-games-pieces)). Keep the blanks at the start and end of a text where the
English has them: many frames join pieces around them.

The ids starting with `grammar.` and `msg.` are the frames of sentences the original assembled
from parts. A comment above each in `Assets/UWDataImport/UWData/UWTextEnglish.Messages.cs` and
`UWTextEnglish.Grammar.cs` says what each `{n}` is and in which case it stands.

## Words in the pictures

Some English words of the original are not strings but pixels of its pictures: the options panel's
buttons and headings (SAVE GAME, MUSIC IS ON., ARE YOU SURE?), the stats page's STR to EXP and the
map's Level, ERASE and CLOSE. The ids `picture.options.*`, `picture.stats.*` and `picture.map.*` in your
`<code>.tsv` replace them: where your text differs from the English, the game paints over the
picture's lettering and writes your text in the original's small capitals, upper case, in the
picture's own colours (the stats page and the map in the original's italic and character-creation
fonts, without a shadow; "Level" keeps your case, and the level's number moves right after a longer
word). A button holds two lines of 28 pixels each, about seven letters; the text is
broken at blanks, or where you write `
`. A word too wide or a third line is clipped, and `uwdump fit`
and `validate-tsv` report it (`SPEICHERN` is 37 pixels: German says `SPIEL SICHERN`). Leave an id out
and the picture stays English. The pixel letters know ASCII and the German umlauts.

### Pictures of your own

The main menu's four captions are painted in a decorative script and the congratulations at the
end are a whole picture: these only a painter can translate truly. Without a picture of yours the
captions come from `picture.menu.*` (`Charakter erschaffen`): the game hides the English picture and
writes your text over the title screen in the save list's style, gold, lighter under the pointer. So can every other picture with words,
where the written words do not do (Chinese and Japanese need more than four pixel rows):

```
uwdump picture-template <game> --out pictures-to-paint
```

writes them from your own game as PNGs, named as the game looks for them (`OPBTN_00.png` for a
.GR picture and its index in hex, `WIN1.png` for a whole screen). Paint over the ones you
translate, keep their size, and put them into `Languages/<code>/pictures`. The game takes the
nearest colour of the picture's own palette for each pixel (a pixel with less than half alpha
becomes transparent), and a picture of yours wins over the words written anew. `validate-tsv`
checks the folder beside your pack: a name of no picture is warned, another size is an error (the
game keeps the original). The exported pictures are the game's own art: publish only what you
painted, like your translation of its texts. `uwdump pictures PANELS.GR` and `uwdump pictures
WIN1.BYT` export the stats pages and the screens of the first game too.

## The game's texts

One line per string: the block, a TAB, the index, a TAB, the text.

```
1	145	Halb geflickt hast du {1}
4	2	f:zirba&zirbas
```

The format is the one of the higesho fork's `Localization/ja.tsv`, so a translation made for it
runs as it is. Keep the conversation placeholders (`@SS1`, `@GI3C2`), the game's backslash codes
(`\m`, `\p`, `\0`) and a line break at the end where the English has one. An empty text keeps the
English.

**Cutscenes.** The texts of a cutscene (block 3072 plus its number, the intro 3072) are what its
recordings say in English. The original shows them only where no recording speaks; with the Game
window's *Subtitles in cutscenes* on Auto (the default) the game shows them with the recordings as
soon as your pack translates a line of that block. The written lines do not always match the spoken
ones word for word.

**The lizardmen's words.** Their language (`Sseth`, `sor'click`, `Bica`) is the game's own puzzle:
leave it as it is in the game's texts. The ids `lizard.*` in your `<code>.tsv` are what the words
mean; with the Gameplay window's *Lizard words* on, the game shows the meaning in brackets behind a
word in the lizardmen's conversations ("Bica [hallo, tschüss]"), so keep them short.

**Typed words.** Some words are compared with what the player types: the shrine's mantras (block 2)
and the answers some conversations ask for. Translate each such word the same way everywhere a
book, scroll or conversation names it, or the quest cannot be solved. `uwdump typed-words --strings
<code>.strings.tsv` lists every typed word with the lines that hint at it.

## Names: the name format

Object names (block 4) and the names built from blocks 1, 7 and 10 are written so that the grammar
can put them in a case, a number and after an article:

```
<gender>[<article>][+<tag>|-<tag>]...:<singular>[|<oblique>][|<case>=<form>]...[&<plural>][&<key>=<form>]...
```

| Part | Meaning |
|---|---|
| gender | `m`, `f`, `n`, `c` (common, as Dutch or Swedish "de/en" words) or `p` (a plural only) |
| article | nothing: the indefinite article; `d`: the definite one; `o`: none |
| `+tag`, `-tag` | a sound tag the name has, or does not have although its first letters give it (see [Sound tags](#sound-tags)) |
| `\|oblique` | the singular in every case but the nominative, where no case form is named |
| `\|case=form` | the singular in one case: `nom gen dat acc ins loc voc abl par ess tra ine ela ill ade all abe com` |
| `&plural` | the plural |
| `&category=form` | the form after a count of that CLDR plural category: `zero one two few many other` |
| `&case=form` | the plural (without a count) in a case |

A `_` reads as a blank. Without the prefix a name reads by the English rules (`a_wumpel&wumpels`),
so a name left English still works. Examples:

```
4	1	m:wumpel&wumpels
4	2	f:zirba|acc=zirbu|loc=zirbe&zirby&few=zirby&many=zirb
4	3	nd:Kopa da Lumo
4	4	p:klunker&dat=klunkern
4	5	m-v:hakko&hakkos
```

The grammar asks for a case and takes the named form, else (outside the nominative) the oblique
form, else the singular. After a count it takes the form named for the count's category, else for
`one` the singular (Russian 21, French 0), else the plural.

### Creatures and their names (block 7)

Looking at a creature shows its attitude word, its kind (block 4) and the name of its conversation
(block 7). How block 7's name joins in depends on its first letter, as in the original:

- **A name in lower case replaces the kind**: "You see a peaceful prisoner." (`7 41 prisoner`), the
  golem, the bandits, the guards, the imp. Write it in the name format (`7	41	m:prisonnier`):
  without the prefix the whole sentence falls back to the English rules, and you get the English
  article and word order.
- **A proper name in upper case follows the kind** through `grammar.look.named`: "You see a mellow
  outcast named Bragit." Leave it without a prefix. With one it would replace the kind. The frame's
  `{0}` is the kind with its article and words, and it selects on the kind's gender:
  `{0,select,f{{0} nommée {1}}other{{0} nommé {1}}}`.
- The kind needs the prefix too: a kind left English also makes the sentence English.
- Block 7's names from 257 on (the creatures of the void) stand alone with their article, so write
  them in the name format as well (`md:...` for "the Slasher of Veils").

## Slots, select and plural

### Slots in the game's pieces

Some strings of block 1 are pieces of a frame, not whole sentences: the anvil's outcomes, the
repair estimate, the taste, the theft, the level-up, the shrine. In English the frame puts the
name after the piece. A language whose word order differs writes the name into the piece as a
**slot** and leaves it out of the frame:

```
de.tsv:           msg.repair.result	{0}.
de.strings.tsv:   1	145	Halb geflickt hast du {1}
```

`{1}` in the piece is the frame's argument 1 (the thing). Which strings are pieces, of which frame,
and what each argument is: `Assets/UWDataImport/UWData/UWTextFragments.cs`; `uwdump validate-tsv`
names them too. Only these frames may leave an argument out, and then every piece of the frame
must name it. Only the game's pieces are read for slots: a name or anything the player typed is
shown as it stands, braces and all.

### Select

`{n,select,KEY{text}...other{text}}` chooses a text by argument n. A name chooses by its sound
tags, its gender and the case it stands in: first, for each tag, `gender.tag.case` and
`gender.tag` (`m.v.acc`, `m.v`); then, for each tag, `tag.case` and `tag` (`v`); then
`gender.case` (`m.acc`), then the gender (`m`), then `other`. Any other argument chooses by its
text.

```
msg.wear.damaged	{0} {0,select,f{es rumpa}n{es rumpi}other{es rumpo}}.
```

### Plural

`{n,plural,=N{text}CATEGORY{text}...other{text}}` chooses by the number of argument n: first an
exact `=N`, then the CLDR category of the language (`zero one two few many other`); `#` inside is
the number.

```
grammar.look.charges.many	 s {0,plural,one{# blikom}few{# blikami}many{# blikov}other{# blika}}
```

| Languages | Categories of whole numbers |
|---|---|
| en de nl sv fi no nb nn da hu tr, and every other code | one (1), other |
| fr, pt | one (0, 1), many (1000000 ...), other |
| es it ca pt-PT | one (1), many (1000000 ...), other |
| ru uk | one (1, 21, 31 ...), few (2-4, 22-24 ...), many (0, 5-20, 25-30 ...) |
| pl | one (1), few (2-4, 22-24 ...), many (0, 5-21 ...) |
| cs sk | one (1), few (2-4), other |
| ja zh ko | other |

`other` is always required. A select or plural may hold one more select or plural inside a branch
(one level). A literal brace or `#` is written `'{'`, `'}'`, `'#'`.

## The grammar header

German has a grammar of its own in code. Every other language starts without code: the ids below,
in its `<code>.tsv`, tell the grammar what it needs. The English values are the defaults.

| Id | Default | Meaning |
|---|---|---|
| `grammar.rule.genders` | `none` | the gender letters the names use (`m f`, `m f n`) |
| `grammar.rule.cases` | `nom acc dat` | the case keys names may give forms for |
| `grammar.rule.case.subject` | `nom` | the case of a subject |
| `grammar.rule.case.object` | `acc` | the case of an object ("You read {0}") |
| `grammar.rule.case.place` | `dat` | the case after "on" ("the trap on {1}") |
| `grammar.rule.case.look` | `acc` | the case of the thing in "You see {0}." |
| `grammar.rule.plural` | `auto` | a language code whose plural rule to use (`auto`: the file's own) |
| `grammar.rule.capital` | `no` | `yes`: a message line starts with a capital letter |
| `grammar.rule.letters` | `none` | the letters beyond ASCII the text entries accept (without a font of the language's own only what the classic fonts draw counts) |
| `grammar.rule.alphabet` | `ABCDEFGHIJKLMNOPQRSTUVWXYZ` | the gamepad letter grid's letter keys in order, upper case (blanks between them allowed); read for every language, German too |
| `grammar.rule.alphabet.lower` | `auto` | their lower case, letter for letter (`auto`: each letter's lower case) |
| `grammar.rule.font` | `none` | the fonts that draw what the port's fonts lack, in order - see [Fonts and input methods](#fonts-and-input-methods); read for every language |
| `grammar.rule.ime` | `no` | `yes`: the text fields use the system's input method (Chinese, Japanese, Korean); read for every language |
| `grammar.rule.classic.font` | `original` | `modern`: choosing the language switches the classic texts to the modern font (see [Fonts and input methods](#fonts-and-input-methods)); read for every language |
| `grammar.rule.classic.bold` | `no` | `yes`: in the modern font the classic texts draw their Chinese, Japanese and Korean characters bold; read for every language |
| `grammar.rule.tags` | `none` | the sound tags and the first letters that give them (`v=a e i o u h; z=sb sc`) - see [Sound tags](#sound-tags) |
| `grammar.article.indefinite` | `{0}` | the indefinite article around the noun phrase `{0}` |
| `grammar.article.definite` | `{0}` | the definite one |
| `grammar.article.your` | `{0}` | the possessive of the player's own thing |
| `grammar.phrase.words` | `{0} {1}` | a word (condition, attitude, "magical", "cursed") `{0}` beside the noun phrase `{1}`; `{1} {0}` puts it after, `{0}{1}` without a blank |
| `grammar.phrase.count` | `{0} {1}` | a count `{0}` and the noun phrase in the count's plural `{1}` |

The article and word frames select on the name in `{0}`:

```
grammar.rule.genders	m f
grammar.article.indefinite	{0,select,f{ka {0}}other{ko {0}}}
grammar.article.definite	{0,select,f{la {0}}other{lo {0}}}
grammar.phrase.words	{1} {0}
```

The words beside a name - the condition words of block 5, the attitude words, and the texts of
`grammar.word.magical` and `grammar.word.cursed` - may agree with it: `{0}` in them is the name.

### Sound tags

Some articles change with the sound that follows: French "le homme" is "l'homme", Italian has "lo
zaino" and "l'uomo", Spanish "el agua". `grammar.rule.tags` names tags and the first letters that
give them, separated by `;`:

```
grammar.rule.tags	v=a e i o u h é è ê â î ô û œ
```

A phrase starting with one of the beginnings has the tag (compared in lower case), and the select
tries the tags before the gender, so one branch serves both genders:

```
grammar.article.definite	{0,select,v{l'{0}}f{la {0}}other{le {0}}}
grammar.article.indefinite	{0,select,f{une {0}}other{un {0}}}
```

The tag belongs to the start of the phrase the frame gets: with the words after the noun
(`grammar.phrase.words	{1} {0}`) that is the noun, with a word before it that word. A name
corrects the rule where its first letters mislead: `-v` takes the tag away (the h aspiré,
`m-v:héros`), `+v` adds it. A tag with no beginnings (`x=`) is one only names give. A tag is
letters a-z and may not be a gender letter, a case key or a plural category.

A bare tag (`v`) wins over the gender, so where the genders differ before a tag, name the
gender with it (`m.z`). Italian, with two tags:

```
grammar.rule.tags	v=a e i o u; z=z gn ps x y sb sc sd sf sg sl sm sn sp sq sr st sv
grammar.article.definite	{0,select,v{l'{0}}m.z{lo {0}}f{la {0}}other{il {0}}}
grammar.article.indefinite	{0,select,f.v{un'{0}}m.z{uno {0}}f{una {0}}other{un {0}}}
```

Every name passed to a message's frame carries the tags of its start too, so a frame may elide
before it: `{0,select,v{d'{0}}other{de {0}}}`. An apostrophe in a frame is a plain character; only
`'{'`, `'}'` and `'#'` are escapes.

```
5	97	{0,select,f{grumma}other{grummo}}
grammar.word.cursed	{0,plural,one{{0,select,f{brakka}other{brakko}}}other{{0,select,f{brakkas}other{brakkos}}}}
```

With this, "ka zirba brakka" and "3 wumpos brakkos" come out. `Tools/Fixtures/i18n` holds two
complete made-up examples: `xg` (gendered, articles, words after the noun) and `xs` (no articles,
six cases, Russian plural categories).

### The letter grid and the alphabet

The gamepad's letter grid (it comes up when the game asks for text and the pad was used last) shows
the language's alphabet in rows of ten, then the signs `' - . ,`, then a row for each ten of the
language's other letters (`grammar.rule.letters`, those not in the alphabet; German: `ä ö ü ß`), the
digits, and case, space, delete, cancel and done. English keeps A to Z. A Cyrillic language writes

```
grammar.rule.alphabet	А Б В Г Д Е Ё Ж З И Й К Л М Н О П Р С Т У Ф Х Ц Ч Ш Щ Ъ Ы Ь Э Ю Я
grammar.rule.alphabet.lower	auto
```

and gets four rows of letters (`Tools/Fixtures/i18n/xc.tsv`). The text fields - a conversation's
typed answer, a mantra, a map note, a save name, the character's name - accept every letter of the
alphabet in both cases. A save keeps only Latin-1, though: a letter beyond it is refused in the
character's name and a map note and left out of a save's name.

The classic pixel fonts (FONT*.SYS) have glyphs for ASCII and the German umlauts and sharp s only; a
language with other letters (Greek, Cyrillic, most accents) names a font of its own, and without one
`uwdump validate-tsv` names the letters the classic fonts lack. Without a font of its own a Latin letter
still shows: its plain form stands in (`é` as `e`, `œ` as `oe`, `’` as `'`), while a letter with no plain
form (Greek, Cyrillic and so on) stays out of the classic text. The same goes for typing: where a text
field refuses `è` as it stands, the `e` arrives. Chinese, Japanese and Korean are
typed through the system's input method, not through the letter grid (next section).

## Fonts and input methods

No font beyond Latin comes with the game. A language names the fonts that draw what the port's own
fonts lack, in its `<code>.tsv`:

```
grammar.rule.font	xz.otf, os:Microsoft JhengHei, os:Noto Sans CJK TC
grammar.rule.ime	yes
grammar.rule.classic.font	modern
grammar.rule.classic.bold	yes
```

`grammar.rule.font` is a list separated by commas, in order. An entry is either a **font file** in
the folder `Languages` beside the game (`.ttf`, `.otf`, or the first font of a `.ttc`; a name, never
a path - it travels with your translation, under its own licence) or an **installed font**,
`os:` and its family name as the system lists it. Unity finds installed fonts in the system's font
folders: on Windows in `C:\Windows\Fonts` (a font installed for one user only, in
`%LOCALAPPDATA%\Microsoft\Windows\Fonts`, was not among them in a test - install it for all users),
on Linux in the folders of the system's font configuration (`/usr/share/fonts`,
`~/.local/share/fonts`; not tested).
An entry that is not there is left out, the next one is tried.

- **The classic interface** asks the entries in order for every character beyond ASCII its pixel
  font lacks; the first that has it draws it, without grey (every pixel ink or not), in the colour
  of the text, at the size where the font's capitals are as tall as the pixel font's (a character
  taller than the pixel font's rows is drawn smaller). Cyrillic and Greek read well that way; an
  ideograph in the six rows of the message scroll does not - FONTBIG (cutscenes, the main menu) and
  the modern font (the key that switches the classic texts to the modern font, F in the classic
  scheme) are where Chinese and Japanese are readable. So a Chinese, Japanese or Korean translation
  sets `grammar.rule.classic.font modern`: choosing the language switches the classic texts to the
  modern font once (F still switches back, and the choice survives a restart), and the original
  pieces of the modern scheme follow F too. Lines break between Chinese and Japanese
  characters too, never before `。` `、` or a closing mark.
- **The modern interface** (Lexend Exa, Liberation Sans) draws a character its font lacks with the
  `os:` entries first, then with whatever Unity finds on the system. A font file cannot serve here:
  Unity's dynamic fonts fall back only to installed fonts. A character the modern font has drawn
  once keeps its look until the game restarts.

In the modern font, a regular CJK face has thin strokes that fade on the original's stone and
parchment, in the grey and brown the original gives its texts. `grammar.rule.classic.bold yes` draws
the Chinese, Japanese and Korean characters of the classic texts bold (the Latin letters and digits
stay as they are); look at character creation and a conversation. A family is only used where it is
installed: Meiryo, for example, is missing on some Windows 11 systems, so name more than one.

`grammar.rule.ime yes` turns the system's input method (IME) on while a text field is open - a
conversation's answer, a mantra, a map note, a save name, the character's name - and the field takes
what it commits. **What a save keeps is bytes**, as in 1992: the character's name (PLAYER.DAT), a
map note and a save game's name hold Latin-1 only. A Chinese or Japanese character is refused in the
name and a map note and left out of a save's name; in a conversation's answer and a mantra, which
are compared and not kept, it is taken. A side file beside the save that keeps such texts is a plan,
not built.

`Tools/Fixtures/i18n/xz.tsv` is a made-up example. In the Unity editor *UW / Check font fallback*
puts it in force and shows what the classic and the modern fonts make of it.

## Checking a translation

```
uwdump validate-tsv <path to the game> Languages/<code>.strings.tsv
```

checks the game's texts against your own STRINGS.PAK: placeholders, codes and line breaks, the name
format of the language, the slots, select and plural of the pieces (each `{n}` an argument of its
frame, `other` present, categories the language has), and, with `<code>.tsv` beside the pack,
that every piece names what its frame leaves out, plus the grammar header. Errors make a string
wrong in the game; warnings are worth a look.

`uwdump fit <path> --strings Languages/<code>.strings.tsv` measures the game's strings that the classic
look draws into boxes of fixed width - character creation's questions, buttons, sex and class line,
attribute labels, skill list and name prompt, and the stats page's class and skill names - in the
pixel font of each place, and marks `OVER` what is wider than its box and would be clipped
(`--over` lists only those). `validate-tsv` reports the same as warnings. A string with characters
the pixel fonts lack (Chinese, Japanese) is marked `?`: the language's font draws them in the game
only, so uwdump cannot measure them; look at character creation and the stats page in the game.

`uwdump looks <path> --strings Languages/<code>.strings.tsv` prints every look sentence and every
message line built from names (families `message`, `repair`, `levelup`, `shrine`, `trap`) in your
language, with `<code>.tsv` beside the pack in force; `uwdump conv <path> --run <slot> --strings
...` runs a conversation. For the port's own texts, the self-check (Tools/UWSelfCheck) reads every
file in `Languages`.

## Trying it in the game

Put the files into `Languages` beside the game and choose the language in the **Language** row of
the *Game* window. The row shows a language by its own name, `setting.Language.name` in its
`<code>.tsv` (`Deutsch`, written in the language itself), else by its code. The change takes effect at once: the port's texts and the game's strings switch,
lines already in the message scroll stay as they were written. The command line `-language <code>`
and the environment variable `UR_LANGUAGE` choose a language at startup. `-language pseudo` shows
every text of the port in brackets and nearly three times as long, the extreme case, to find texts
that clip.

## Publishing

`Languages/<code>.tsv`, the port's own texts, may come into this repository by pull request. The
game's texts are not ours: a translated `<code>.strings.tsv` is a translation of Origin's text. It
never comes into this repository. A translator who publishes one does so on their own
responsibility. The same holds for pictures painted over the game's (`Languages/<code>/pictures`):
they are Origin's art changed, published only by the translator, never here.
