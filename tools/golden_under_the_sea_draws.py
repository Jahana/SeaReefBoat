"""Under the Sea's #draws flow: write it, instrument the Lua draw, and list the rounds of each draw value.

Usage: py -3 tools/golden_under_the_sea_draws.py gen [rounds] | instrument <Under_the_Sea.lua> | parse <show output>

tests/golden/flows/otherAreas.under_the_sea#draws.flow names, in its header, the rounds at which Zaldon's
math.randomInt(1, 100) draws 20, 21 and 100 on the pinned stream. Those rounds come from an instrumented Lua run, which
this script makes again (the quest's Lua is gone from HEAD since 3eeb6c46; 41dcb4cb has it):

  git show 41dcb4cb:scripts/quests/otherAreas/Under_the_Sea.lua > scripts/quests/otherAreas/Under_the_Sea.lua
  py -3 tools/golden_under_the_sea_draws.py instrument scripts/quests/otherAreas/Under_the_Sea.lua
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- record otherAreas.under_the_sea --out <tmp> --db <lane>
  dotnet run --no-build --project tests/XI.Verify.QuestGolden.Live -c Release -- show "otherAreas.under_the_sea#draws" --out <tmp> > <tmp>/show.txt
  py -3 tools/golden_under_the_sea_draws.py parse <tmp>/show.txt
  rm scripts/quests/otherAreas/Under_the_Sea.lua

The record runs the working tree's (instrumented, uncommitted) Lua at HEAD into <tmp> only; never record it into
tests/golden. The instrumentation writes each draw to the char var UTSDRAW and a running count to UTSDRAWN (setCharVar
draws nothing, so the stream is the same as the committed trace's); parse reads them from show's char_vars lines. A round
whose draw equals the round before shows no UTSDRAW change, so parse carries the last value forward. `gen` prints the
committed flow (290 rounds) to stdout: `gen > tests/golden/flows/otherAreas.under_the_sea#draws.flow`.

At HEAD the harness refuses a record into tests/golden while scripts/ holds uncommitted changes (the trace would
stamp HEAD over this Lua); a record into --out <tmp> goes on and says so.
"""
import re
import sys

HEADER = """\
# Under the Sea (otherAreas 17): Zaldon's draw, math.randomInt(1, 100) <= 20 (35, else 36), over {n} trades on the
# pinned stream. Each round gives one Fat Greedie (a GM: it does not stack), puts the quest back at Prog 3 without the
# ring (a GM) and trades the Greedie; either finish takes it (35 also gives the Etched Ring and sets Prog 4). The
# accepted section reads no setting.
# The rounds reach the draws a mutant of the range or the bound changes: an instrumented run (the Lua writing each draw
# to a char var; not committed) drew 20 at rounds 114, 138, 185, 209, 273 (caught: <= 19, < 20, randomInt(2, 100)),
# 21 at rounds 88, 271 (<= 21, randomInt(0, 100)) and 100 at round 274 (randomInt(1, 99) rejects that sample and draws
# again, so every later round moves). 56 of the 290 rounds play 35 (see the trace).
# Regenerate the flow, or redo the instrumented run: tools/golden_under_the_sea_draws.py (its docstring has the steps).
lists Selbina/Zaldon/onTrade Selbina/onEventFinish/35 Selbina/onEventFinish/36
char zone=Selbina pos=-10.3,-7.287,-6.742 job=WAR lvl=30
quest OtherAreas.UnderTheSea accepted
login
"""

ROUND = """\
item FatGreedie                         # round {r}
charvar Quest[4][17]Prog 3
delkeyitem EtchedRing
trade Zaldon FatGreedie x1
finish
"""

DRAW_LINE = "                        if math.randomInt(1, 100) <= 20 then\n"
DRAW_INSTRUMENTED = (
    "                        local utsDraw = math.randomInt(1, 100)\n"
    "                        player:setCharVar('UTSDRAW', utsDraw)\n"
    "                        player:setCharVar('UTSDRAWN', player:getCharVar('UTSDRAWN') + 1)\n"
    "                        if utsDraw <= 20 then\n"
)


def gen(rounds):
    sys.stdout.reconfigure(newline="\n")
    sys.stdout.write(HEADER.format(n=rounds) + "".join(ROUND.format(r=r) for r in range(1, rounds + 1)))


def instrument(path):
    text = open(path, encoding="utf8", newline="").read()
    if text.count(DRAW_LINE) != 1:
        sys.exit(f"{path}: the draw line occurs {text.count(DRAW_LINE)} times, not once")
    open(path, "w", encoding="utf8", newline="").write(text.replace(DRAW_LINE, DRAW_INSTRUMENTED))
    print(f"{path}: instrumented (UTSDRAW, UTSDRAWN)")


def parse(path):
    steps = re.split(r"\n(?=step )", open(path, encoding="utf8").read())
    draws, cur = [], None
    for s in steps:
        if not re.match(r"step \d+\s+flow:\d+\s+trade Zaldon", s):
            continue
        drawn = re.search(r"\+ varname=UTSDRAWN\|value=(\d+)\|", s)
        if not drawn:
            draws.append(None)  # no draw in this trade
            continue
        m = re.search(r"\+ varname=UTSDRAW\|value=(\d+)\|", s)
        if m:
            cur = int(m.group(1))
        draws.append(cur)
    print("rounds", len(draws), "drawn", sum(d is not None for d in draws))
    for v in (1, 19, 20, 21, 22, 100):
        print(v, [i + 1 for i, d in enumerate(draws) if d == v])
    print("35s", sum(d is not None and d <= 20 for d in draws))


if __name__ == "__main__":
    a = sys.argv[1:]
    if a[:1] == ["gen"] and len(a) <= 2:
        gen(int(a[1]) if len(a) == 2 else 290)
    elif a[:1] == ["instrument"] and len(a) == 2:
        instrument(a[1])
    elif a[:1] == ["parse"] and len(a) == 2:
        parse(a[1])
    else:
        sys.exit(__doc__.splitlines()[2])
