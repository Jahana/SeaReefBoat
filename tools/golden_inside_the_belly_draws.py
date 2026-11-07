"""Inside the Belly's #draws flow: write it, write the exploration flow, instrument the Lua draws, list the rolls.

Usage: py -3 tools/golden_inside_the_belly_draws.py gen | explore | instrument <Inside_the_Belly.lua> | parse <show output>

tests/golden/flows/otherAreas.inside_the_belly#draws.flow trades one fish per round to Zaldon (the quest completed) and
picks the fish of some rounds so that tradeFish's math.randomInt(1, 1000) lands on a running sum (a `roll <= sum` bound)
or on the Plumb Boots' window. A fish whose drawn entry has no min / max draws nothing more, so up to the first such
draw the rolls are those of the exploration flow (600 rounds of Armored Pisces, whose 166 needs a roll <= 4). Those
rounds come from an instrumented Lua run, which this script makes again (the quest's Lua is at the commit of the
group 3 Lua traces, or at HEAD while it is still there):

  git show <commit>:scripts/quests/otherAreas/Inside_the_Belly.lua > scripts/quests/otherAreas/Inside_the_Belly.lua
  py -3 tools/golden_inside_the_belly_draws.py instrument scripts/quests/otherAreas/Inside_the_Belly.lua
  py -3 tools/golden_inside_the_belly_draws.py explore > "tests/golden/flows/otherAreas.inside_the_belly#explore.flow"
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- record "otherAreas.inside_the_belly#explore" --out <tmp> --db <lane>
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- show "otherAreas.inside_the_belly#explore" --out <tmp> > <tmp>/show.txt
  py -3 tools/golden_inside_the_belly_draws.py parse <tmp>/show.txt
  rm "tests/golden/flows/otherAreas.inside_the_belly#explore.flow"; git checkout scripts/ (or rm the restored file)

Record the instrumented Lua into <tmp> only, never into tests/golden. The instrumentation writes each roll to the char
var ITBROLL and a running count to ITBROLLN (setCharVar draws nothing); parse reads them from show's char_vars lines (a
roll equal to the round before shows no ITBROLL change, so the last value carries forward) and lists the rounds whose
roll is a bound of a fish of the flow. `gen > tests/golden/flows/otherAreas.inside_the_belly#draws.flow` writes the
committed flow.

At HEAD the harness refuses a record into tests/golden while scripts/ holds uncommitted changes (the trace would
stamp HEAD over this Lua); a record into --out <tmp> goes on and says so.
"""
import re
import sys

COMMON = """\
lists Selbina/Zaldon/onTrade Selbina/onEventFinish/166 Selbina/onEventFinish/167
char zone=Selbina pos=-10.3,-7.287,-6.742 job=WAR lvl=30
setting map.FISHING_ENABLE true
quest OtherAreas.InsideTheBelly completed
skill Fishing 300
storage inventory 80
login
"""

HEADER = """\
# Inside the Belly (otherAreas 26): the reward draws, from the completed section (a GM: its finish gives the reward with
# no completion in the way; the accepted section's finish ends in a Lua error, see the main flow's waivers).
# FISHING_ENABLE (P6): `setting map.FISHING_ENABLE true` in the header; every section reads xi.settings.map.FISHING_ENABLE == true
# (false in settings/default/map.lua). The inventory is 80 (the rewards that do not stack).
# Each round gives the fish (a GM), trades it and finishes. tradeFish draws randomInt(1, 1000) and walks the entries,
# summing chance * 10 (doubles), 166 (0, the item) at the first sum the roll does not exceed, else 167; the finish gives
# the item (randomInt(min, max) of it for an entry with min and max), the fish's gil and its title.
# Rounds 1-278: the rolls of an instrumented run (tools/golden_inside_the_belly_draws.py; Armored Pisces, 166 at <= 4,
# fill the rounds no bound needs; no entry of theirs draws again, so the stream is the exploration's):
#   round 1, roll 150: a Grimmonite, its third sum (50 + 50 + 50): 166, the Silver Ring (`<` would play 167);
#   round 175, roll 12: a Matsya, sum 12 (1.2 * 10): 166, the Shaper's Shawl;
#   round 218, roll 287: an Ahtapot, its second sum (185 + 102): 166, the Decayed Ingot;
#   round 224, roll 185: an Ahtapot, its first sum: 166, the Mildewy Ingot (`<` would give the Decayed Ingot);
#   round 236, roll 223: an Abaia, past 218 (the three Aurora Bass entries), within 225: 166, the Plumb Boots;
#   round 278, roll 10: an Abaia, its first sum (15): 166, one Aurora Bass (randomInt(1, 1) in the finish).
# Rounds 279-302: Abaia (<= 15 one Aurora Bass, <= 93 two, <= 218 three: each draws randomInt(min, max) again in the
# finish, min = max; 690 gil); rounds 303-382: Monke-Onke (<= 100: the Poison Dust, randomInt(1, 6) of it; 150 gil);
# rounds 383-414: Ahtapot (350 gil). The trace shows each round's event and item.
"""


def rounds_draws():
    special = {1: "Grimmonite", 175: "Matsya", 218: "Ahtapot", 224: "Ahtapot", 236: "Abaia", 278: "Abaia"}
    fish = [special.get(r, "ArmoredPisces") for r in range(1, 279)]
    fish += ["Abaia"] * 24 + ["MonkeOnke1"] * 80 + ["Ahtapot"] * 32
    return fish


def write(fish, header):
    sys.stdout.reconfigure(newline="\n")
    out = [header, COMMON]
    for r, f in enumerate(fish, 1):
        out.append(f"item {f}\n" + f"trade Zaldon {f} x1".ljust(40) + f"# round {r}\nfinish\n")
    sys.stdout.write("".join(out))


def instrument(path):
    text = open(path, encoding="utf8", newline="").read()
    edits = [
        ("    local roll    = math.randomInt(1, 1000)\n",
         "    local roll    = math.randomInt(1, 1000)\n"
         "    player:setCharVar('ITBROLL', roll)\n"
         "    player:setCharVar('ITBROLLN', player:getCharVar('ITBROLLN') + 1)\n"),
    ]
    for old, new in edits:
        if text.count(old) != 1:
            sys.exit(f"{path}: {old.strip()!r} occurs {text.count(old)} times, not once")
        text = text.replace(old, new)
    open(path, "w", encoding="utf8", newline="").write(text)
    print(f"{path}: instrumented (ITBROLL, ITBROLLN)")


BOUNDS = {150: "Grimmonite 3rd", 50: "Grimmonite 1st", 100: "Grimmonite 2nd / Monke-Onke", 12: "Matsya",
          185: "Ahtapot 1st", 287: "Ahtapot 2nd", 15: "Abaia 1st", 93: "Abaia 2nd", 218: "Abaia 3rd", 225: "Abaia 4th"}


def parse(path):
    steps = re.split(r"\n(?=step )", open(path, encoding="utf8").read())
    rolls, cur = [], None
    for s in steps:
        if not re.match(r"step \d+\s+flow:\S+\s+trade Zaldon", s):
            continue
        if not re.search(r"\+ varname=ITBROLLN\|value=(\d+)\|", s):
            rolls.append(None)  # no draw in this trade
            continue
        m = re.search(r"\+ varname=ITBROLL\|value=(\d+)\|", s)
        if m:
            cur = int(m.group(1))
        rolls.append(cur)
    print("rounds", len(rolls), "drawn", sum(r is not None for r in rolls))
    for i, r in enumerate(rolls, 1):
        if r is not None and (r in BOUNDS or 219 <= r <= 225):
            print(i, r, BOUNDS.get(r, "Abaia: Plumb Boots window"))


if __name__ == "__main__":
    a = sys.argv[1:]
    if a == ["gen"]:
        write(rounds_draws(), HEADER)
    elif a == ["explore"]:
        write(["ArmoredPisces"] * 600, "# exploration only: never commit (tools/golden_inside_the_belly_draws.py)\n")
    elif a[:1] == ["instrument"] and len(a) == 2:
        instrument(a[1])
    elif a[:1] == ["parse"] and len(a) == 2:
        parse(a[1])
    else:
        sys.exit(__doc__.splitlines()[2])
