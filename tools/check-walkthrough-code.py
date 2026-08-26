"""
Checks that every code block a walkthrough presents as a file really is that file.

A walkthrough hands the reader complete files to paste. If one drifts from the code in its
part's folder, the reader ends up with something that does not compile or does not match what
the text describes, and nothing else in the build would notice.

It also checks the other direction: every file a part changed must appear in that part's
walkthrough. A file the text forgets is invisible to the first check, and a reader following the
steps ends up with code that does not compile - which is how twelve missing constructor
arguments reached a reader once.

Program.cs is excluded from the completeness check. Every part changes exactly one line of it,
the window title, and each walkthrough gives that line in its own step; reprinting forty lines
per part to show one would be noise.

Usage - from the repository root:

    python tools/check-walkthrough-code.py

Prints one line per part and a total. A non-zero total means a block no longer matches its file,
or a changed file is missing from the text.
"""

import re, os, glob

os.chdir(r"D:\repo\roguelikeTutorialC#")

# Ordered, because completeness is measured against the part before.
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
]

# One line of this changes per part, and each walkthrough gives that line in its own step.
EXCLUDED_FROM_COMPLETENESS = {"Program.cs"}


def changed_files(part, previous_part):
    """Names every .cs file the part added or altered, relative to the part before it."""
    changed = []

    for folder in ["RogueTutorial", "RogueTutorial.Tests"]:
        current_dir = os.path.join("parts", part, folder)
        if not os.path.isdir(current_dir):
            continue

        for name in sorted(os.listdir(current_dir)):
            if not name.endswith(".cs") or name in EXCLUDED_FROM_COMPLETENESS:
                continue

            previous_path = os.path.join("parts", previous_part, folder, name)

            # A file that did not exist before is new, and new counts as changed.
            if not os.path.exists(previous_path):
                changed.append(f"{folder}/{name}")
                continue

            before = open(previous_path, encoding="utf-8").read()
            after = open(os.path.join(current_dir, name), encoding="utf-8").read()

            if before != after:
                changed.append(f"{folder}/{name}")

    return changed


total_stale = 0
total_missing = 0
total_missing = 0
for doc in sorted(glob.glob("docs/part-0*.md")):
    part = os.path.basename(doc).replace(".md", "")
    root = os.path.join("parts", part)
    if not os.path.isdir(root):
        continue

    text = open(doc, encoding="utf-8").read()
    blocks = re.findall(
        r"### \[`(RogueTutorial(?:\.Tests)?)/(.+?)`\]\(.*?\)\n\n.*?```csharp\n(.*?)```",
        text, re.S)

    stale = []
    for folder, name, block in blocks:
        path = os.path.join(root, folder, name)
        if not os.path.exists(path):
            stale.append(name + " (missing)")
            continue
        real = open(path, encoding="utf-8").read().lstrip("\ufeff").rstrip("\n")
        if block.rstrip("\n") != real:
            stale.append(name)

    # Completeness: every file this part changed should appear above.
    missing = []
    if part in PARTS and PARTS.index(part) > 0:
        shown = {f"{folder}/{name}" for folder, name, _ in blocks}

        for path in changed_files(part, PARTS[PARTS.index(part) - 1]):
            if path not in shown:
                missing.append(path)

    total_stale += len(stale)
    total_missing += len(missing)
    print(f"{part}: {len(blocks)} blocks, {len(stale)} stale, {len(missing)} missing "
          f"{(stale + missing) if (stale or missing) else ''}")

print(f"\ntotal stale blocks: {total_stale}")
print(f"total missing files: {total_missing}")
