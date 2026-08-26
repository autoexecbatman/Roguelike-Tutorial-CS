"""
Inserts a generated diff into each walkthrough, above every file the part modifies.

A part that changes an existing file gives the reader the whole file to paste, which is right
for following along and useless for seeing what actually moved. This adds a unified diff against
the previous part's copy of that file, rendered as a ```diff block so the change is highlighted.

The diffs are generated from the files themselves rather than written by hand, so they cannot
drift. Running this again regenerates them: existing diff blocks are removed first, which makes
it safe to re-run after editing any part.

Files a part introduces get no diff, because there is nothing to compare them against.

Usage - from the repository root:

    python tools/insert-walkthrough-diffs.py

Prints one line per part naming the files that gained a diff.
"""

import difflib
import os
import re

# Ordered, because each part is diffed against the one before it.
PARTS = [
    "part-01-drawing-and-moving",
    "part-02-entities-and-the-map",
    "part-03-dungeon-generation",
    "part-04-field-of-view",
    "part-05-placing-monsters",
    "part-06-combat",
    "part-07-log-and-health-bar",
    "part-08-items-and-inventory",
    "part-09-ranged-scrolls-and-targeting",
    "part-10-saving-and-loading",
    "part-11-levelling-up",
    "part-12-deeper-levels",
]

# Marks a block this tool generated, so a re-run can strip it without touching prose.
MARKER = "<!-- generated-diff -->"

# Matches one file section: its heading, and everything up to the complete-file block.
SECTION = re.compile(
    r"(### \[`(RogueTutorial(?:\.Tests)?)/([A-Za-z]+\.cs)`\]\([^)]*\)\n\n)"
    r"(.*?)"
    r"(```csharp\n)",
    re.S)


def read(path):
    """Returns a file's lines, or None when it does not exist."""
    if not os.path.exists(path):
        return None

    return open(path, encoding="utf-8").read().lstrip("﻿").splitlines(keepends=True)


def strip_existing(body):
    """Removes any diff block this tool added previously, leaving the author's prose."""
    return re.sub(re.escape(MARKER) + r".*?" + re.escape(MARKER) + r"\n\n", "", body, flags=re.S)


def diff_block(previous_lines, current_lines, previous_part, name):
    """Builds the marked diff block, or an empty string when the file is unchanged."""
    diff = list(difflib.unified_diff(
        previous_lines, current_lines,
        fromfile=f"{previous_part}/{name}", tofile=f"current/{name}",
        n=3))

    if not diff:
        return ""

    part_number = previous_part.split("-")[1].lstrip("0")

    body = "".join(diff).rstrip("\n")

    return (f"{MARKER}\n"
            f"**Changed from Part {part_number}.** The complete file follows; this is only what moved:\n\n"
            f"```diff\n{body}\n```\n"
            f"{MARKER}\n\n")


def main():
    for index in range(1, len(PARTS)):
        part = PARTS[index]
        previous_part = PARTS[index - 1]

        doc_path = os.path.join("docs", f"{part}.md")
        if not os.path.exists(doc_path):
            continue

        document = open(doc_path, encoding="utf-8").read()
        gained = []

        def replace(match):
            heading, folder, name, body, fence = match.groups()

            body = strip_existing(body)

            previous_lines = read(os.path.join("parts", previous_part, folder, name))
            current_lines = read(os.path.join("parts", part, folder, name))

            # A file the part introduces has nothing to be diffed against.
            if previous_lines is None or current_lines is None:
                return heading + body + fence

            block = diff_block(previous_lines, current_lines, previous_part, name)
            if block:
                gained.append(name)

            return heading + body + block + fence

        document = SECTION.sub(replace, document)
        open(doc_path, "w", encoding="utf-8").write(document)

        print(f"{part}: {len(gained)} diffs {gained if gained else ''}")


if __name__ == "__main__":
    main()
