#!/usr/bin/env python3
"""Lint the Russian translation file.

Always: valid JSON object, no duplicate keys, string values, no empty values, known tags only,
distinct bot command words (CMDS_*), and a warning for unbalanced tags.

With --english (the game's own English.json, not in the repo): missing and obsolete keys,
placeholders that differ from English, and color tags that differ from English.

Exit code 1 on any error. Warnings fail only with --strict.

    python tools/check_translation.py
    python tools/check_translation.py --english English.json
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_FILE = ROOT / "RussianTranslation" / "Resources" / "Russian.json"

# Formatter.ReplaceColorCodes in the game: <Key>...</> and friends.
COLOR_TAGS = {"Key", "Command", "Scary", "Error", "Secret", "Task", "Inactive", "Tutorial"}
# Values the game's code substitutes with str.Replace; they must appear exactly as in English.
PLACEHOLDERS = {"Credits", "Nickname", "Name", "Value", "KeyBind", "Index", "SaveName", "Success",
                "0", "1", "2", "3", "4", "5"}
TAG = re.compile(r"<[^<>]*>")
PLACEHOLDER = re.compile(r"<([A-Z0-9]\w*)>")


def load(path: Path, problems: list[str]) -> dict[str, object] | None:
    def no_duplicates(pairs: list[tuple[str, object]]) -> dict[str, object]:
        seen: dict[str, object] = {}
        for key, value in pairs:
            if key in seen:
                problems.append(f"error: {key}: duplicate key, only the last one is used")
            seen[key] = value
        return seen

    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=no_duplicates)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as ex:
        problems.append(f"error: {path}: {ex}")
        return None
    if not isinstance(data, dict):
        problems.append(f"error: {path}: expected a JSON object of key: text")
        return None
    return data


def tag_problems(text: str, extra_placeholders: set[str]) -> tuple[list[str], list[str]]:
    """Unknown tags are errors. Unbalanced ones only warnings: TextMeshPro tolerates them, and
    the game's own English has a few unclosed <color=...>."""
    problems: list[str] = []
    unbalanced: list[str] = []
    tags = TAG.findall(text)
    for tag in tags:
        name = tag[1:-1]
        known = (tag in ("</>", "</color>", "<b>", "</b>")
                 or name in COLOR_TAGS or name in PLACEHOLDERS or name in extra_placeholders
                 or re.fullmatch(r"<color=#[0-9A-Fa-f]{3,8}>", tag))
        if not known:
            problems.append(f"unknown tag {tag}")
    counts = Counter(tags)
    openers = sum(n for tag, n in counts.items() if tag[1:-1] in COLOR_TAGS)
    if openers != counts["</>"]:
        unbalanced.append(f"{openers} color tags but {counts['</>']} closing </>")
    color_open = sum(n for tag, n in counts.items() if tag.startswith("<color="))
    if color_open != counts["</color>"]:
        unbalanced.append(f"{color_open} <color=...> but {counts['</color>']} </color>")
    if counts["<b>"] != counts["</b>"]:
        unbalanced.append(f"{counts['<b>']} <b> but {counts['</b>']} </b>")
    return problems, unbalanced


def placeholders(text: str) -> Counter[str]:
    return Counter(m for m in PLACEHOLDER.findall(text) if m not in COLOR_TAGS)


def color_tags(text: str) -> Counter[str]:
    return Counter(m for m in PLACEHOLDER.findall(text) if m in COLOR_TAGS)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file", nargs="?", type=Path, default=DEFAULT_FILE, help="translation to check")
    parser.add_argument("--english", type=Path, help="the game's English.json to compare against")
    parser.add_argument("--strict", action="store_true", help="fail on warnings too")
    args = parser.parse_args()

    problems: list[str] = []
    russian = load(args.file, problems)
    english = load(args.english, problems) if args.english else None
    if russian is None or (args.english and english is None):
        print("\n".join(problems))
        return 1

    english_placeholders: set[str] = set()
    if english:
        for text in english.values():
            if isinstance(text, str):
                english_placeholders |= set(placeholders(text))

    commands: dict[str, str] = {}
    for key, text in russian.items():
        if not isinstance(text, str):
            problems.append(f"error: {key}: value is not a string")
            continue
        if not text.strip():
            problems.append(f"warning: {key}: empty, the game will show English")
            continue
        unknown, unbalanced = tag_problems(text, english_placeholders)
        source = english.get(key) if english else None
        if isinstance(source, str) and tag_problems(source, set())[1] == unbalanced:
            unbalanced = []  # same as the game's English
        problems += [f"error: {key}: {p}" for p in unknown]
        problems += [f"warning: {key}: {p}" for p in unbalanced]
        if key.startswith("CMDS_"):
            word = text.strip().lower()
            if word in commands:
                problems.append(f"error: {key}: bot command '{text}' is also {commands[word]}")
            commands[word] = key

    if english:
        missing = [k for k in english if k not in russian]
        obsolete = [k for k in russian if k not in english]
        for key in missing:
            problems.append(f"warning: {key}: not translated, shown in English")
        for key in obsolete:
            problems.append(f"warning: {key}: not in this game version, ignored")
        for key, text in russian.items():
            source = english.get(key)
            if not isinstance(text, str) or not isinstance(source, str) or not text.strip():
                continue
            want, got = placeholders(source), placeholders(text)
            if want != got:
                lost = sorted((want - got).elements())
                added = sorted((got - want).elements())
                detail = ", ".join(filter(None, [
                    f"missing <{'> <'.join(lost)}>" if lost else "",
                    f"not in English <{'> <'.join(added)}>" if added else ""]))
                problems.append(f"error: {key}: placeholders differ: {detail}")
            if set(color_tags(source)) != set(color_tags(text)):
                problems.append(f"warning: {key}: color tags differ from English "
                                f"({sorted(color_tags(source))} vs {sorted(color_tags(text))})")
        translated = sum(1 for k, v in russian.items() if k in english and isinstance(v, str) and v.strip())
        same = [k for k, v in russian.items() if english.get(k) == v]
        print(f"translated {translated}/{len(english)}, {len(missing)} missing, {len(obsolete)} obsolete, "
              f"{len(same)} identical to English")

    errors = sum(1 for p in problems if p.startswith("error"))
    warnings = len(problems) - errors
    for problem in problems:
        print(problem)
    print(f"{args.file.name}: {len(russian)} keys, {errors} errors, {warnings} warnings")
    return 1 if errors or (args.strict and warnings) else 0


if __name__ == "__main__":
    sys.exit(main())
