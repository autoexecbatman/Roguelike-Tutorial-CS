"""
Checks that a walkthrough lists its files in an order a reader can follow.

C# compiles a project as a whole, so file order does not affect the compiler. It very much
affects a reader working top to bottom: paste a file that calls a method an later-listed file
introduces, build, and the compiler blames the file you just pasted.

The check: for every member a part adds to a file, no earlier-listed file may call it. That
catches the two ways this has gone wrong here - Combat before the Entity it needs, and Consumable
before the Fighter.Heal it calls.

Three things keep it honest, and every one was needed - the versions without them either reported
thirty-one imaginary problems or reported none at all while the real fault was in front of it:

- Comments are stripped, because a usage block routinely names a type the file does not depend on.
- A member declared in more than one listed file is skipped, since a bare `.Width` cannot be
  attributed to either.
- Only public and internal members count as declarations. Without that, Consumable's private Heal
  helper made Fighter's public Heal ambiguous, and the check went silent on the exact case it was
  written for.
- Neither a type declaration nor a constructor is a member. A class named Consumable, its
  constructor, and Entity's property of the same name are three different things; counting the
  first two made the property ambiguous, which hid Entity's new components from the check
  entirely.

What is left is a name introduced by exactly one file and called, in code, by a file listed
before it. Falsify it after any change: move a file above one it depends on and watch it report.

Usage - from the repository root:

    python tools/check-walkthrough-order.py

Prints one line per part and a total. A non-zero total means a reader pasting in the listed order
hits an error on a file that is perfectly correct.
"""

import os
import re

PARTS = [
    "part-01-drawing-and-moving",
    "part-02-entities-and-the-map",
    "part-03-dungeon-generation",
    "part-04-field-of-view",
    "part-05-placing-monsters",
    "part-06-combat",
    "part-07-log-and-health-bar",
    "part-08-items-and-inventory",
]

# Names too generic to match reliably: they appear in prose and in unrelated code.
IGNORED_NAMES = {"Main", "ToString", "Equals", "GetHashCode", "Read", "Write", "Add", "Remove"}

# Line and block comments. A usage block naming a type is not a dependency on the file that
# declares it, and matching inside comments is what made the first version of this useless.
COMMENTS = re.compile(r"(?s:/\*.*?\*/)|//[^\n]*")

# A property, method or field declaration, with its visibility captured. Only public and
# internal members can be the target of a call from another file, and matching private ones is
# what made the ambiguity filter discard the very case this tool exists for: Fighter's public
# Heal and Consumable's private helper of the same name.
DECLARATION = re.compile(
    r"^\s*(public|private|internal|protected)\s+(?:static\s+|readonly\s+|sealed\s+|override\s+)*"
    r"(?!class|enum|struct|record|interface)"
    r"[A-Za-z_][\w<>?\[\],\s\.]*?\s+([A-Z][A-Za-z0-9_]*)\s*[({=>]",
    re.M)


def declared_names(path):
    """Every member another file could call: public and internal, never private."""
    if not os.path.exists(path):
        return set()

    source = COMMENTS.sub("", open(path, encoding="utf-8").read())

    # A constructor shares its type's name and is never reached through a dot, so counting it
    # would make every same-named property elsewhere look ambiguous.
    own_type = os.path.splitext(os.path.basename(path))[0]

    return {
        name
        for visibility, name in DECLARATION.findall(source)
        if visibility in ("public", "internal")
        and name not in IGNORED_NAMES
        and name != own_type
    }


def listed_files(doc_path):
    """The source files a walkthrough presents, in the order it presents them."""
    text = open(doc_path, encoding="utf-8").read()

    return re.findall(r"### \[`RogueTutorial/([A-Za-z]+\.cs)`\]", text)


def main():
    total = 0

    for index, part in enumerate(PARTS):
        doc_path = os.path.join("docs", f"{part}.md")
        if not os.path.exists(doc_path):
            continue

        source_dir = os.path.join("parts", part, "RogueTutorial")
        previous_dir = os.path.join("parts", PARTS[index - 1], "RogueTutorial") if index > 0 else None

        files = listed_files(doc_path)

        # What each listed file introduces this part: declared now, not declared before.
        introduced = {}
        for name in files:
            now = declared_names(os.path.join(source_dir, name))
            before = declared_names(os.path.join(previous_dir, name)) if previous_dir else set()
            introduced[name] = now - before

        # A member declared in more than one listed file cannot be attributed to either, so a
        # bare `.Width` is evidence of nothing.
        seen = set()
        ambiguous = set()
        for name in files:
            for member in declared_names(os.path.join(source_dir, name)):
                if member in seen:
                    ambiguous.add(member)
                seen.add(member)

        problems = []

        for position, name in enumerate(files):
            body = COMMENTS.sub("", open(os.path.join(source_dir, name), encoding="utf-8").read())

            own = declared_names(os.path.join(source_dir, name))

            # Anything introduced by a file listed later is a forward reference for the reader.
            for later in files[position + 1:]:
                for member in introduced[later] - ambiguous - own:
                    if re.search(rf"\.{re.escape(member)}\b", body):
                        problems.append(f"{name} uses {later}'s {member}")

        total += len(problems)

        print(f"{part}: {len(files)} files, {len(problems)} out of order")
        for problem in problems:
            print(f"    {problem}")

    print(f"\ntotal forward references: {total}")


if __name__ == "__main__":
    main()
