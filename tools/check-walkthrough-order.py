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
- A member declared in more than one listed file is reported only when every file declaring it
  comes later. Skipping such names outright was simpler and wrong: RestoreMemory is declared on
  both GameWorld and VisibilityMap, and discarding it hid a real forward reference. If every
  declarer is later, the call is forward whichever one it meant.
- Only public and internal members count as declarations. Without that, Consumable's private Heal
  helper made Fighter's public Heal ambiguous, and the check went silent on the exact case it was
  written for.
- Neither a type declaration nor a constructor is a member. A class named Consumable, its
  constructor, and Entity's property of the same name are three different things; counting the
  first two made the property ambiguous, which hid Entity's new components from the check
  entirely.
- A one-letter member is not attributable at all: SavedEntity.Y and Keys.Y look identical to a
  textual match.

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
    "part-09-ranged-scrolls-and-targeting",
    "part-10-saving-and-loading",
    "part-11-levelling-up",
    "part-12-deeper-levels",
    "part-13-equipment",
]

# Names too generic to match reliably: they appear in prose and in unrelated code.
IGNORED_NAMES = {"Main", "ToString", "Equals", "GetHashCode", "Read", "Write", "Add", "Remove"}


def is_attributable(name):
    """A one-letter member cannot be blamed on a file: SavedEntity.Y and Keys.Y look the same."""
    return len(name) > 1


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
        and is_attributable(name)
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

        # Every member the previous part already had, anywhere in it.
        existed_before = set()
        if previous_dir and os.path.isdir(previous_dir):
            for name in sorted(os.listdir(previous_dir)):
                if name.endswith(".cs"):
                    existed_before |= declared_names(os.path.join(previous_dir, name))

        # Where each member is declared, so a name on two files can still be judged rather than
        # thrown away. Discarding them outright hid three real faults before this replaced it.
        declared_in = {}
        for position, name in enumerate(files):
            for member in declared_names(os.path.join(source_dir, name)):
                declared_in.setdefault(member, []).append(position)

        problems = []

        for position, name in enumerate(files):
            body = COMMENTS.sub("", open(os.path.join(source_dir, name), encoding="utf-8").read())

            # Anything introduced by a file listed later is a forward reference for the reader -
            # but only when every file declaring that name comes later. If any declarer is at or
            # before this one, the call could have meant that, and reporting it would be noise.
            for later_position in range(position + 1, len(files)):
                later = files[later_position]

                for member in introduced[later]:
                    if any(where <= position for where in declared_in.get(member, [])):
                        continue

                    # A name that already existed last part resolves without anything listed
                    # here: Entity.Name predates Part 8, so a call to .Name is not waiting on
                    # the ItemKind.Name that part happens to add.
                    if member in existed_before:
                        continue

                    if re.search(rf"\.{re.escape(member)}\b", body):
                        problems.append(f"{name} uses {later}'s {member}")

        total += len(problems)

        print(f"{part}: {len(files)} files, {len(problems)} out of order")
        for problem in problems:
            print(f"    {problem}")

    print(f"\ntotal forward references: {total}")


if __name__ == "__main__":
    main()
