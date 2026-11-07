r"""In the Name of Science's generated flows (Yurim, Tavnazian Safehold): the nosTrades table, the pairs order and the
draws.

Usage: py -3 tools/golden_in_the_name_of_science.py table <obis|gorgets|earrings> | order | draws [rounds]
                                                     | instrument <Yurim.lua> | parse <show output>

`table <part>` prints `otherAreas.in_the_name_of_science#<part>`: each nosTrades entry of the part in turn, from no
target: its base traded (526: the target set, the base taken), a script talk (532 or 528 with the base), the pie (531
with the entry's hint), each organ one short (nothing), then the organs (529; its finish gives the entry's item, the
target 0, completed again). So every entry's hint, base, organs and item is taken.

`order` prints `#order`: trades holding several bases, each answered by the first base in pairs(nosTrades) order (526
with that base; the target reset by a GM after each). A trade holding bases A and B shows their order only when no
earlier base is inside the union of their items; ORDER below is that pairs order and queries() picks the 22 trades
whose answers, closed under transitivity, give every such observable pair (121 of the 153 pairs; the other 32, Flame
Gorget vs Hyorin Obi for one, are inside a trade that always holds an earlier base, the Karin Obi's, so no trade shows
them).
`py -3 <this> order --check` prints that count again.

`draws [rounds]` prints `#draws`: rounds of a default talk and a script talk with a target (math.randomInt(1, 100)
<= 30: 532, else 528). Its header names the rounds at which the pinned stream draws 30, 31, 1 and 100; those come from
an instrumented Lua run (not committed), which this script makes again on the unpatched Yurim.lua (the one the group's
Lua traces were recorded on: their lua-at, dcfd6bb6):

  git show dcfd6bb6:scripts/zones/Tavnazian_Safehold/npcs/Yurim.lua > scripts/zones/Tavnazian_Safehold/npcs/Yurim.lua
  py -3 tools/golden_in_the_name_of_science.py instrument scripts/zones/Tavnazian_Safehold/npcs/Yurim.lua
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- \
      record "otherAreas.in_the_name_of_science#draws" --out <tmp> --db <lane>
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- \
      show "otherAreas.in_the_name_of_science#draws" --out <tmp> > <tmp>/show.txt
  py -3 tools/golden_in_the_name_of_science.py parse <tmp>/show.txt
  git checkout scripts/zones/Tavnazian_Safehold/npcs/Yurim.lua

Record the instrumented Lua into <tmp> only, never into tests/golden. The instrumentation writes each draw to the char
var NOSDRAW and a running count to NOSDRAWN (setCharVar draws nothing, so the stream is the committed trace's), and on
the first script talk the pairs(nosTrades) keys in order to NOSORD1..NOSORD18; parse reads them from show's char_vars
lines (a draw equal to the one before shows no NOSDRAW change, so the last value carries forward).

At HEAD the harness refuses a record into tests/golden while scripts/ holds uncommitted changes (the trace would
stamp HEAD over this Lua); a record into --out <tmp> goes on and says so.
"""
import itertools
import re
import sys

SO, SE, G = "SilverObi", "SilverEarring", "Gorget"

# Yurim.lua nosTrades: name -> (item id, hint, base, organs)
NOS = {
    "KarinObi": (15435, 1, (SO, "RedChip"), (("PhuaboOrgan", 7), ("XzomitOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "HyorinObi": (15436, 2, (SO, "ClearChip"), (("XzomitOrgan", 7), ("PhuaboOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "FurinObi": (15437, 3, (SO, "GreenChip"), (("AernOrgan", 7), ("HpemdeOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "DorinObi": (15438, 4, (SO, "YellowChip"), (("HpemdeOrgan", 7), ("AernOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "RairinObi": (15439, 5, (SO, "PurpleChip"),
                  (("PhuaboOrgan", 7), ("HpemdeOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "SuirinObi": (15440, 6, (SO, "BlueChip"), (("HpemdeOrgan", 7), ("PhuaboOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "KorinObi": (15441, 7, (SO, "WhiteChip"), (("XzomitOrgan", 7), ("AernOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "AnrinObi": (15442, 8, (SO, "BlackChip"), (("AernOrgan", 7), ("XzomitOrgan", 3), ("SampleOfLuminianTissue", 3))),
    "FlameGorget": (15495, 9, (G, "RedChip"), (("PhuaboOrgan", 10), ("XzomitOrgan", 5), ("YovraOrgan", 1))),
    "SnowGorget": (15496, 10, (G, "ClearChip"), (("PhuaboOrgan", 10), ("AernOrgan", 5), ("YovraOrgan", 1))),
    "BreezeGorget": (15497, 11, (G, "GreenChip"), (("PhuaboOrgan", 10), ("HpemdeOrgan", 5), ("YovraOrgan", 1))),
    "SoilGorget": (15498, 12, (G, "YellowChip"), (("XzomitOrgan", 10), ("AernOrgan", 5), ("YovraOrgan", 1))),
    "ThunderGorget": (15499, 13, (G, "PurpleChip"), (("XzomitOrgan", 10), ("HpemdeOrgan", 5), ("YovraOrgan", 1))),
    "AquaGorget": (15500, 14, (G, "BlueChip"), (("AernOrgan", 10), ("HpemdeOrgan", 5), ("YovraOrgan", 1))),
    "LightGorget": (15501, 15, (G, "WhiteChip"),
                    (("AernOrgan", 7), ("PhuaboOrgan", 3), ("HpemdeOrgan", 3), ("YovraOrgan", 2))),
    "ShadowGorget": (15502, 16, (G, "BlackChip"),
                     (("HpemdeOrgan", 7), ("PhuaboOrgan", 3), ("AernOrgan", 3), ("YovraOrgan", 2))),
    "SanativeEarring": (14791, 17, (SE, "WhiteChip"), (("SampleOfLuminianTissue", 5), ("EuvhiOrgan", 5))),
    "RelaxingEarring": (14792, 17, (SE, "BlackChip"), (("SampleOfLuminianTissue", 5), ("EuvhiOrgan", 5))),
}
PARTS = {
    "obis": ["KarinObi", "HyorinObi", "FurinObi", "DorinObi", "RairinObi", "SuirinObi", "KorinObi", "AnrinObi"],
    "gorgets": ["FlameGorget", "SnowGorget", "BreezeGorget", "SoilGorget", "ThunderGorget", "AquaGorget", "LightGorget",
                "ShadowGorget"],
    "earrings": ["SanativeEarring", "RelaxingEarring"],
}
# pairs(nosTrades) on the engine's LuaJIT (the instrumented run's NOSORD1..18; the C# BaseOrder)
ORDER = ["KarinObi", "RelaxingEarring", "FlameGorget", "HyorinObi", "SnowGorget", "FurinObi", "BreezeGorget",
         "AquaGorget", "ShadowGorget", "LightGorget", "DorinObi", "SanativeEarring", "SoilGorget", "ThunderGorget",
         "AnrinObi", "KorinObi", "SuirinObi", "RairinObi"]
ITEMS = [SO, SE, G, "RedChip", "ClearChip", "GreenChip", "YellowChip", "PurpleChip", "BlueChip", "WhiteChip",
         "BlackChip"]

COMMON = """\
char zone=TavnazianSafehold pos=82.5,-25.0,3.5 job=WAR lvl=75
mission Cop EmptinessBleeds current
quest OtherAreas.InTheNameOfScience accepted
"""


def label(name):
    return re.sub(r"(?<!^)([A-Z])", r" \1", name)


def out(text):
    sys.stdout.reconfigure(newline="\n")
    sys.stdout.write(text)


def trade_line(items, comment):
    line = "trade Yurim " + " ".join(f"{i} x{q}" for i, q in items)
    return f"{line:<39} # {comment}\n" if len(line) < 39 else f"{line}  # {comment}\n"


TABLE_HEADER = """\
# In the Name of Science (otherAreas 76): the nosTrades table, the {part}:
# {names}.
# Accepted, The Warriors' Path completed (current Emptiness Bleeds), a WAR 75. For each entry, from no target: the
# GMs give its base, a pie and its organs; its base traded: 526 (the base; the target set; the base taken); a script
# talk after the default 527: 532 or 528 with the base (the draw); the pie: 531 (pie, 0, the hint; taken); each organ
# one short: nothing (no target-0 base arm, no pie); the organs: 529 (Gorget, Silver Earring, Silver Obi); its finish:
# the entry's item, the target 0, completed (again after the first), the organs taken. Every reward is rare and
# different. Generated: tools/golden_in_the_name_of_science.py table {part}.
"""


def table(part):
    names = PARTS[part]
    s = TABLE_HEADER.format(part=part, names=", ".join(label(n) for n in names))
    s += COMMON + "login\n"
    for n in names:
        item, hint, base, organs = NOS[n]
        s += f"item {base[0]:<34} # {label(n)} ({item}, hint {hint}): its base, a pie, its organs (GMs)\n"
        s += f"item {base[1]}\nitem ApplePie\n"
        for o, q in organs:
            s += f"item {o} x{q}\n"
        s += trade_line([(base[0], 1), (base[1], 1)], f"no target: the {label(n)} base: target {item}; 526")
        s += "finish                                  # the base taken\n"
        s += "trigger Yurim                           # the default 527\nfinish\n"
        s += f"trigger Yurim                           # a target: 532 or 528 ({label(base[0])}, {label(base[1])})\n"
        s += "finish\n"
        s += f"trade Yurim ApplePie x1                 # 531 (Apple Pie, 0, {hint})\n"
        s += "finish                                  # the pie taken\n"
        for i, (o, q) in enumerate(organs):
            short = [(oo, qq - 1 if j == i else qq) for j, (oo, qq) in enumerate(organs)]
            short = [(oo, qq) for oo, qq in short if qq > 0]
            s += trade_line(short, f"one {label(o)} short: nothing")
        s += trade_line(list(organs), "the organs: 529")
        s += f"finish                                  # the {label(n)}; the target 0; completed; the organs taken\n"
    return s


def first(S):
    for n in ORDER:
        if set(NOS[n][2]) <= S:
            return n
    return None


def revealed(S):
    sat = [n for n in ORDER if set(NOS[n][2]) <= S]
    return {(sat[0], x) for x in sat[1:]}


def closure(rel):
    succ = {n: {b for a, b in rel if a == n} for n in ORDER}
    changed = True
    while changed:
        changed = False
        for a in ORDER:
            new = set().union(*[succ[b] for b in succ[a]]) if succ[a] else set()
            if not new <= succ[a]:
                succ[a] |= new
                changed = True
    return {(a, b) for a in ORDER for b in succ[a]}


def observable():
    rel = set()
    for a, b in itertools.combinations(ORDER, 2):
        m = first(set(NOS[a][2]) | set(NOS[b][2]))
        if m in (a, b):
            rel.add((m, b if m == a else a))
    return rel


def queries():
    target = observable()
    cands = []
    for k in range(2, 9):
        for S in itertools.combinations(ITEMS, k):
            r = revealed(set(S))
            if r:
                cands.append((S, r))
    chosen, have = [], set()
    while not target <= closure(have):
        best = max(cands, key=lambda c: (len(closure(have | c[1]) & target), -len(c[0])))
        chosen.append(best[0])
        have |= best[1]
    return chosen


ORDER_HEADER = """\
# In the Name of Science (otherAreas 76): Yurim's base loop walks pairs(nosTrades) and takes the first base the
# trade holds (526 with it; the target set; only that base confirmed and taken). Accepted, The Warriors' Path
# completed, no target. Each trade holds several bases (up to 8 items); its answer shows that base before every
# other base in the trade. The 22 trades give, with transitivity, every pair whose order some trade can show (121 of
# 153; in the other 32 every trade holding both holds an earlier base too). After each: the GM resets the target.
# The pairs order (LuaJIT, the engine's; the C# BaseOrder):
# {order}
# Generated: tools/golden_in_the_name_of_science.py order.
"""


def order():
    s = ORDER_HEADER.format(order=" ".join(str(NOS[n][0]) for n in ORDER))
    s += COMMON + "login\n"
    held = {i: 0 for i in ITEMS}
    for S in queries():
        S = sorted(S, key=ITEMS.index)
        need = [i for i in S if held[i] == 0]
        for i in need:
            s += f"item {i}\n"
            held[i] += 1
        n = first(set(S))
        s += (f"# {len([m for m in ORDER if set(NOS[m][2]) <= set(S)])} bases: {label(n)} first: "
              f"526 ({label(NOS[n][2][0])}, {label(NOS[n][2][1])})\n")
        s += "trade Yurim " + " ".join(f"{i} x1" for i in S) + "\n"
        s += "finish                                  # its base taken, the rest kept\n"
        s += "charvar NAME_OF_SCIENCE_target 0        # a GM: no target again\n"
        for i in NOS[n][2]:
            held[i] -= 1
    return s


DRAWS_HEADER = """\
# In the Name of Science (otherAreas 76): Yurim's draw, math.randomInt(1, 100) <= 30 (532, else 528, the target's base),
# over {n} script talks on the pinned stream. Accepted, The Warriors' Path completed, the target the Karin Obi (a GM
# before login). Each round: the default 527 (UseFallback), then the script talk; neither finish reads anything.
# An instrumented run (the Lua writing each draw to a char var; not committed) drew 30 at round 105 (532; a < 30 or
# <= 29 plays 528), 31 at round 12 (528; a <= 31 plays 532), 1 at round 241 and 100 at round 274 (randomInt(1, 99)
# rejects that sample and draws again, so round 274 moves). 275 rounds reach them all.
# Regenerate the flow, or redo the instrumented run: tools/golden_in_the_name_of_science.py (its docstring).
"""


def draws(rounds):
    s = DRAWS_HEADER.format(n=rounds)
    s += COMMON + "charvar NAME_OF_SCIENCE_target 15435\nlogin\n"
    for r in range(1, rounds + 1):
        s += f"trigger Yurim                           # round {r}: the default 527\nfinish\n"
        s += "trigger Yurim                           # 532 or 528 (Silver Obi, Red Chip)\nfinish\n"
    return s


DRAW_LINE = "            if math.randomInt(1, 100) <= 30 then\n"
DRAW_INSTRUMENTED = (
    "            if player:getCharVar('NOSORD1') == 0 then\n"
    "                local n = 0\n"
    "                for k, _ in pairs(nosTrades) do\n"
    "                    n = n + 1\n"
    "                    player:setCharVar('NOSORD' .. n, k)\n"
    "                end\n"
    "            end\n"
    "            local nosDraw = math.randomInt(1, 100)\n"
    "            player:setCharVar('NOSDRAW', nosDraw)\n"
    "            player:setCharVar('NOSDRAWN', player:getCharVar('NOSDRAWN') + 1)\n"
    "            if nosDraw <= 30 then\n"
)


def instrument(path):
    text = open(path, encoding="utf8", newline="").read()
    if text.count(DRAW_LINE) != 1:
        sys.exit(f"{path}: the draw line occurs {text.count(DRAW_LINE)} times, not once")
    open(path, "w", encoding="utf8", newline="").write(text.replace(DRAW_LINE, DRAW_INSTRUMENTED))
    print(f"{path}: instrumented (NOSDRAW, NOSDRAWN, NOSORD1..18)")


def parse(path):
    text = open(path, encoding="utf8").read()
    order = dict(re.findall(r"\+ varname=NOSORD(\d+)\|value=(\d+)\|", text))
    if order:
        print("pairs order", " ".join(order[str(i)] for i in range(1, len(order) + 1)))
        print("ORDER      ", " ".join(str(NOS[n][0]) for n in ORDER))
    steps = re.split(r"\n(?=step )", text)
    draws_, cur = [], None
    for st in steps:
        if not re.match(r"step \d+\s+flow:\d+\s+trigger Yurim", st):
            continue
        if not re.search(r"\+ varname=NOSDRAWN\|value=(\d+)\|", st):
            continue  # the default talk
        m = re.search(r"\+ varname=NOSDRAW\|value=(\d+)\|", st)
        if m:
            cur = int(m.group(1))
        draws_.append(cur)
    print("rounds", len(draws_))
    for v in (1, 2, 29, 30, 31, 32, 99, 100):
        print(v, [i + 1 for i, d in enumerate(draws_) if d == v])
    print("532s", sum(d <= 30 for d in draws_))


if __name__ == "__main__":
    a = sys.argv[1:]
    if a[:1] == ["table"] and len(a) == 2 and a[1] in PARTS:
        out(table(a[1]))
    elif a == ["order"]:
        out(order())
    elif a == ["order", "--check"]:
        pairs = len(ORDER) * (len(ORDER) - 1) // 2
        print(len(observable()), "observable pairs of", pairs, ";", len(queries()), "trades")
    elif a[:1] == ["draws"] and len(a) <= 2:
        out(draws(int(a[1]) if len(a) == 2 else 275))
    elif a[:1] == ["instrument"] and len(a) == 2:
        instrument(a[1])
    elif a[:1] == ["parse"] and len(a) == 2:
        parse(a[1])
    else:
        sys.exit(__doc__.splitlines()[3])
