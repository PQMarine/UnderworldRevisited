# Makes the MADE-UP Japanese-looking test language "xj" (2026-11-08): every English word of the port's
# texts and of the game's strings becomes an invented run of kanji, hiragana or katakana, the words are
# joined without blanks and the punctuation turns full-width, as Japanese writes. No language's words and
# no text of the game: only the structure (ids, block/index, placeholders, codes) is kept, so the files
# test how the port DRAWS Japanese - line breaks without blanks, the fonts, the boxes - not a translation.
#
# Kept English on purpose: the mantras of block 2 (typed at a shrine; its skill and class names are made up), the strings the
# conversations compare a typed answer with (uwdump typed-words), key names in capitals (F1, WASD) and
# anything with a digit.
#
#   dotnet uwdump.dll text-template xj --out en.tsv
#   dotnet uwdump.dll strings-template <game> xj --out en.strings.tsv
#   dotnet uwdump.dll typed-words <game> > typed.txt
#   python make-xj.py en.tsv en.strings.tsv typed.txt <out folder>
#
# Writes <out>/xj.tsv and <out>/xj.strings.tsv. The second is built from the player's own STRINGS.PAK
# structure and stays out of the repository like every game-text file.

import hashlib
import re
import sys

KANJI = "山川水火木金土石田雨風雲花草竹林森日月空海島門道剣盾鍵本巻物魔法像骨肉酒薬毒光闇影声言語書読見聞行来立座寝食飲取置開閉戦守攻防術力体知技命心霊神聖王騎士竜鬼獣蛇虫鼠糸網壁床階段扉箱袋瓶杖弓矢斧槌鎧兜靴環冠宝珠銀銅鉄灯炎氷雷"
HIRAGANA = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだでどばびぶべぼっ"
KATAKANA = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワンガギグゲゴザジズゼゾダデドバビブベボパピプペポー"

# The full-width marks a Japanese text uses for the English ones.
PUNCT = {".": "。", ",": "、", "?": "？", "!": "！", ":": "：", ";": "；", "(": "（", ")": "）"}

FONTS = "os:Yu Gothic UI Semibold, os:Yu Gothic Medium, os:Yu Gothic, os:Meiryo, os:MS Gothic, os:Noto Sans CJK JP, os:Noto Sans JP"


def made_up(word):
    """An invented word for an English one, the same every time, about half as many characters."""
    h = hashlib.md5(word.lower().encode("utf-8")).digest()
    n = max(1, min(6, (len(word) + 1) // 2))
    kind = h[0] % 4
    out = []
    for i in range(n):
        b = h[1 + i]
        if kind == 0 or (kind == 1 and i == 0):
            out.append(KANJI[b % len(KANJI)])
        elif kind == 1 or kind == 2:
            c = HIRAGANA[b % len(HIRAGANA)]
            out.append("し" if i == 0 and c == "っ" else c)
        else:
            c = KATAKANA[b % len(KATAKANA)]
            out.append("カ" if i == 0 and c == "ー" else c)
    return "".join(out)


def keep_word(word):
    return any(ch.isdigit() for ch in word) or (word.isupper() and 2 <= len(word) <= 5)


TOKEN = re.compile(r"(\\.)|(\{[^{}]*\})|(<[^<>]*>)|(@[A-Z]+[0-9A-Z]*)|([A-Za-z][A-Za-z']*)|(\s+)|(\.\.\.)|(.)", re.S)


def convert(text):
    """English words to invented ones, joined without blanks; codes, placeholders and markup stay."""
    tokens = []
    for m in TOKEN.finditer(text):
        code, ph, tag, conv, word, space, dots, other = m.groups()
        if word is not None:
            if keep_word(word):
                tokens.append(("keep", word))
            else:
                tokens.append(("ja", made_up(word.strip("'"))))
        elif space is not None:
            tokens.append(("space", space))
        elif dots is not None:
            tokens.append(("punct", "…"))
        elif ph is not None or conv is not None:
            # A placeholder stands where a Japanese word would: no blanks around it either.
            tokens.append(("ja", m.group(0)))
        elif other == '"':
            tokens.append(("quote", '"'))
        elif other is not None and other in PUNCT:
            tokens.append(("punct", other))
        else:
            tokens.append(("keep", m.group(0)))

    # Paired double quotes become the corner brackets.
    quotes = [i for i, t in enumerate(tokens) if t[0] == "quote"]
    for n, i in enumerate(quotes):
        tokens[i] = ("punct", ("「" if n % 2 == 0 else "」") if len(quotes) % 2 == 0 else '"')

    out = []
    for i, (kind, value) in enumerate(tokens):
        prev = tokens[i - 1][0] if i > 0 else None
        nxt = tokens[i + 1][0] if i + 1 < len(tokens) else None
        if kind == "space":
            # Between Japanese words and after a full-width mark: no blank. At the start and end: kept.
            if prev in ("ja", "punct") and nxt in ("ja", "punct") and 0 < i < len(tokens) - 1:
                continue
            if prev == "punct" and nxt in ("ja", "punct") and out and out[-1] in "。、？！：；）…「」":
                continue
            if nxt == "punct" and prev == "ja" and i + 1 < len(tokens) and tokens[i + 1][1] in "「」":
                continue
            out.append(value)
        elif kind == "punct":
            # Only after or before a Japanese word, never inside numbers ("1.5") or codes.
            near_ja = prev == "ja" or nxt == "ja" or value in "…「」"
            out.append(PUNCT.get(value, value) if near_ja else value)
        else:
            out.append(value)
    return "".join(out)


def name_format(text):
    """An object name (block 4): the singular without its English article, in the name format."""
    singular = text.split("&")[0]
    if "_" in singular:
        singular = singular.split("_", 1)[1]
    return "n:" + convert(singular).replace(" ", "_")


def main(en_tsv, en_strings, typed_txt, out_dir):
    typed = set()
    for line in open(typed_txt, encoding="utf-8", errors="replace"):
        m = re.search(r"(compare|contains)\s+(\d+):(\d+)", line) or re.match(r"\s+()(2):(\d+)\s", line)
        if m:
            typed.add((int(m.group(2)), int(m.group(3))))

    rules = {
        "setting.Language.name": "擬似日本語 xj",
        "grammar.rule.font": FONTS,
        "grammar.rule.ime": "yes",
        "grammar.rule.classic.font": "modern",
        "grammar.rule.classic.bold": "yes",
        "grammar.rule.plural": "ja",
        "grammar.rule.genders": "n",
        "grammar.phrase.words": "{0}{1}",
        "grammar.phrase.count": "{0}{1}",
        "grammar.look.named": "{1}という{0}",
    }

    with open(out_dir + "/xj.tsv", "w", encoding="utf-8", newline="\n") as f:
        f.write("# A MADE-UP Japanese-looking test language \"xj\" (Tools/Fixtures/i18n/make-xj.py): invented\n")
        f.write("# kanji and kana for every English word, no language's words and no text of the game.\n")
        for line in open(en_tsv, encoding="utf-8"):
            line = line.rstrip("\n")
            if not line or line.startswith("#") or "\t" not in line:
                continue
            key, value = line.split("\t", 1)
            if key in rules:
                value = rules[key]
            elif key.startswith("grammar.rule.") or key.startswith("grammar.article."):
                continue
            else:
                value = convert(value)
            f.write(key + "\t" + value + "\n")

    with open(out_dir + "/xj.strings.tsv", "w", encoding="utf-8", newline="\n") as f:
        f.write("# A MADE-UP game-text pack of the test language \"xj\" (make-xj.py): invented kanji and kana in\n")
        f.write("# place of every string, mantras and typed answers kept. Not a translation; never commit it.\n")
        for line in open(en_strings, encoding="utf-8"):
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t", 2)
            if len(parts) < 3 or not parts[2]:
                continue
            block, index, text = int(parts[0]), int(parts[1]), parts[2]
            if (block, index) in typed:
                continue
            if block == 4 or (block == 7 and text[:1].islower()):
                value = name_format(text)
            else:
                value = convert(text)
            f.write("%d\t%d\t%s\n" % (block, index, value))


if __name__ == "__main__":
    main(*sys.argv[1:5])
