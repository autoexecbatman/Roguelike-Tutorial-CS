"""
Checks that every code block a walkthrough presents as a file really is that file.

A walkthrough hands the reader complete files to paste. If one drifts from the code in its
part's folder, the reader ends up with something that does not compile or does not match what
the text describes, and nothing else in the build would notice.

Usage - from the repository root:

    python tools/check-walkthrough-code.py

Prints one line per part and a total. A non-zero total means a block no longer matches its file.
"""

import re, os, glob

os.chdir(r"D:\repo\roguelikeTutorialC#")

total_stale = 0
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

    total_stale += len(stale)
    print(f"{part}: {len(blocks)} blocks, {len(stale)} stale {stale if stale else ''}")

print(f"\ntotal stale blocks: {total_stale}")
