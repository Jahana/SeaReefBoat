#!/usr/bin/env python3
"""Generate src/XI.QuestContent/Ids/*.g.cs: the Lua enums under scripts/enum (Zone, Item, ...) and xi.battlefield.id
(scripts/globals/battlefield.lua: Battlefield), the quest keys from
scripts/globals/quests.lua (QuestKey), the mission logs and ids from scripts/globals/missions.lua (MissionLog, Mission),
and each zone's IDs.lua text/mob keys plus its npcs/mobs script names (Text, Mob, Npc).
Npc also takes every entity name (script ?? template) of the zone's data/zones/<folder>/npcs.yaml and mobs.yaml.
A name collision fails unless tools/content_id_overrides.txt has a line "<Class> <LUA_KEY> <CSharpName>" (Class may name
one zone: Npc.BastokMarkets).
A constant of the current .g.cs files that the Lua no longer produces fails as "removed: <Class>.<Name>", and nothing
is written (--allow-removed writes anyway; it does not delete a .g.cs file that is no longer generated at all).

Run from the csharp folder:  python tools/gen_content_ids.py [--check] [--out <dir>] [--data <dir>] [--allow-removed]
  --check       generate in memory and compare with the files: exit 1 naming each differing file; writes nothing
  --out         the folder of the .g.cs files (default src/XI.QuestContent/Ids)
  --data        the folder of the zone data folders (default data/zones)
  --count-yaml  print the zone yaml statistics (zones, names added / skipped, Npc collisions); writes nothing
Standard library only; the output is deterministic (Lua file order, LF line endings like the rest of src/).
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "XI.QuestContent", "Ids")

# (lua table, source file under scripts/enum, C# class, C# constant type)
ENUMS = [
    ("xi.zone", "zone.codegen.lua", "Zone", "ushort"),
    ("xi.keyItem", "key_item.codegen.lua", "KeyItem", "int"),
    ("xi.item", "item.lua", "Item", "int"),
    ("xi.fameArea", "fame_area.codegen.lua", "FameArea", "int"),
    ("xi.title", "title.lua", "Title", "int"),
    ("xi.slot", "slot.lua", "Slot", "int"),
    ("xi.job", "job.codegen.lua", "Job", "int"),
    ("xi.skill", "skill.codegen.lua", "Skill", "int"),
    ("xi.nation", "nation.lua", "Nation", "int"),
    ("xi.wsUnlock", "ws_unlock.lua", "WsUnlock", "int"),
    ("xi.weather", "weather.codegen.lua", "Weather", "int"),
    ("xi.time", "time.lua", "Time", "int"),
    ("xi.magic.spell", "magic.lua", "Spell", "int"),
    ("xi.mobFamily", "mob_family.codegen.lua", "MobFamily", "int"),
    ("xi.ecosystem", "ecosystem.codegen.lua", "Ecosystem", "int"),
    ("xi.attackType", "attack_type.codegen.lua", "AttackType", "int"),
    ("xi.unityLeader", "unity_leader.lua", "UnityLeader", "int"),
    ("xi.roeTrigger", "roe_trigger.lua", "RoeTrigger", "int"),
    # a source file outside scripts/enum is named from scripts/
    ("xi.battlefield.id", "globals/battlefield.lua", "Battlefield", "int"),
]

ENTRY = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(-?\d+)\s*,")


def pascal(key):
    name = "".join(p[:1].upper() + p[1:].lower() for p in key.split("_") if p)
    return "_" + name if name[:1].isdigit() else name


LONG_OPEN = re.compile(r"\[(=*)\[")


def strip(line, state):
    """(text, code) of one line. text drops --[[ ]] / --[==[ ]==] comments, [[ ]] / [==[ ]==] long strings and the --
    comment; code also drops quoted strings, so braces in them don't count. A long bracket may span lines: state[0]
    holds the closing bracket of the one still open."""
    text, code = [], []
    quote = None
    i = 0
    while i < len(line):
        if state[0]:
            end = line.find(state[0], i)
            if end < 0:
                break
            i = end + len(state[0])
            state[0] = None
            continue
        c = line[i]
        long_open = None
        if not quote:
            long_open = LONG_OPEN.match(line, i + 2) if line.startswith("--", i) else LONG_OPEN.match(line, i)
        if quote:
            text.append(c)
            if c == "\\" and i + 1 < len(line):
                i += 1
                text.append(line[i])
            elif c == quote:
                quote = None
        elif long_open:
            state[0] = "]" + long_open.group(1) + "]"
            i = long_open.end()
            continue
        elif c in "'\"":
            quote = c
            text.append(c)
        elif line.startswith("--", i):
            break
        else:
            text.append(c)
            code.append(c)
        i += 1
    return "".join(text), "".join(code)


def lua_lines(path):
    """(text, code) of each line of a Lua file (see strip)."""
    state = [None]
    with open(path, encoding="utf-8") as f:
        for line in f:
            yield strip(line, state)


def depth_change(code):
    return code.count("{") - code.count("}")


def parse(path, table):
    """Integer entries of `table = { ... }`, up to the } that closes it (not the first one at column 0)."""
    entries = []
    inside = False
    depth = 0
    for _, code in lua_lines(path):
        if not inside:
            if re.match(r"^" + re.escape(table) + r"\s*=", code):
                inside = True
                depth += depth_change(code)
            continue
        m = ENTRY.match(code)
        if m and depth >= 1:
            entries.append((m.group(1), int(m.group(2))))
        depth += depth_change(code)
        if depth <= 0 and "}" in code:
            break
    if not entries:
        sys.exit("no entries for %s in %s" % (table, path))
    return entries


OVERRIDES = {}


def load_overrides():
    path = os.path.join(ROOT, "tools", "content_id_overrides.txt")
    if not os.path.exists(path):
        return
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = override_parts(line)
            if parts is None:
                continue
            if len(parts) != 3:
                sys.exit("bad override line: " + line.strip())
            OVERRIDES[(parts[0], parts[1])] = parts[2]


def override_parts(line):
    """An override line's fields (None for a blank or comment line): <Class> <LUA_KEY> <CSharpName>, split at spaces; a
    key with a space is written in double quotes (Npc.CaedarvaMire "Gloom Phantom" GloomPhantomSpaced). A # outside
    quotes starts the comment."""
    parts, cur, quoted = [], None, False
    for c in line.rstrip("\r\n"):
        if quoted:
            if c == '"':
                quoted = False
            else:
                cur += c
        elif c == '"':
            quoted, cur = True, cur if cur is not None else ""
        elif c == "#":
            break
        elif c.isspace():
            if cur is not None:
                parts.append(cur)
                cur = None
        else:
            cur = (cur or "") + c
    if cur is not None:
        parts.append(cur)
    if quoted:
        return [line.strip()]  # an unclosed quote: a bad line
    return parts if parts else None


FAILED = []


class Names:
    """Hands out unique C# names inside one class; a collision needs an override."""

    def __init__(self, cls, enclosing):
        self.cls = cls  # "Item" or "Npc.BastokMarkets"
        self.top = cls.split(".")[0]
        self.used = {enclosing: None}

    def add(self, lua_key, name, value):
        # an override for this class (Npc.BastokMarkets) before one for the whole class (Npc)
        name = OVERRIDES.get((self.cls, lua_key)) or OVERRIDES.get((self.top, lua_key)) or name
        if name in self.used:
            other = self.used[name]
            if other == (lua_key, value):
                return None  # the very same entry again
            other_key, other_value = other if other else (name, "?")
            FAILED.append("collision in %s: %s from %s = %s and %s = %s; add an override line"
                          % (self.cls, name, other_key, other_value, lua_key, value))
            return None
        self.used[name] = (lua_key, value)
        return name


GENERATED = {}  # file name -> bytes; main writes or compares them


def write(name, lines):
    GENERATED[name] = "\n".join(lines).encode("utf-8")


CLASS = re.compile(r"^\s*public static class (\w+)")
CONST = re.compile(r"^\s*public (?:const|static readonly) \S+ (\w+) =")


def constants(data):
    """The dotted names (Npc.BastokMarkets.Foo) of the constants in a generated file's bytes."""
    names = set()
    stack = []
    pending = None
    for line in data.decode("utf-8").split("\n"):
        c = CLASS.match(line)
        if c:
            pending = c.group(1)
        elif line.strip() == "{":
            stack.append(pending)
            pending = None
        elif line.strip() == "}":
            stack.pop()
        else:
            m = CONST.match(line)
            if m:
                names.add(".".join([s for s in stack if s] + [m.group(1)]))
    return names


def quest_ids():
    """(area name, [(CONST, id)]) in file order: xi.quest.area maps the log to the area name, xi.quest.id[area] holds the ids."""
    path = os.path.join(ROOT, "scripts", "globals", "quests.lua")
    areas = {}
    result = []
    current = None
    seen_id = False
    for line, _ in lua_lines(path):
        an = re.match(r"^\s*\[xi\.questLog\.(\w+)\]\s*=\s*'(\w+)'", line)
        if an and not seen_id:
            areas[an.group(1)] = an.group(2)
            continue
        if line.startswith("xi.quest.id"):
            seen_id = True
            continue
        if not seen_id:
            continue
        head = re.match(r"^    \[xi\.quest\.area\[xi\.questLog\.(\w+)\]\]\s*=", line)
        if head:
            current = (areas[head.group(1)], [])
            result.append(current)
            continue
        if line.startswith("}") and result:
            break
        e = re.match(r"^        (\w+)\s*=\s*(\d+)", line)
        if e and current is not None:
            current[1].append((e.group(1), int(e.group(2))))
    return result


def mission_ids():
    """(log {NAME: id}, [(area name, [(CONST, id)])]) of scripts/globals/missions.lua: xi.mission.log_id, xi.mission.area
    (log -> area name) and the xi.mission.id table of each area, in file order (the `nation` table has no log: left out)."""
    path = os.path.join(ROOT, "scripts", "globals", "missions.lua")
    logs = []
    areas = {}
    result = []
    current = None
    state = None
    for line, _ in lua_lines(path):
        if line.startswith("xi.mission.log_id"):
            state = "log"
            continue
        if line.startswith("xi.mission.area"):
            state = "area"
            continue
        if line.startswith("xi.mission.id"):
            state = "id"
            continue
        if state in ("log", "area", "id") and line.startswith("}"):
            if state == "id":
                break
            state = None
            continue
        if state == "log":
            e = re.match(r"^\s+(\w+)\s*=\s*(\d+)", line)
            if e:
                logs.append((e.group(1), int(e.group(2))))
        elif state == "area":
            e = re.match(r"^\s*\[xi\.mission\.log_id\.(\w+)\]\s*=\s*'(\w+)'", line)
            if e:
                areas[e.group(1)] = e.group(2)
        elif state == "id":
            head = re.match(r"^    \[xi\.mission\.area\[xi\.mission\.log_id\.(\w+)\]\]\s*=", line)
            if head:
                current = (areas[head.group(1)], [])
                result.append(current)
                continue
            if line.startswith("    ['"):
                current = None
                continue
            e = re.match(r"^        (\w+)\s*=\s*(\d+)", line)
            if e and current is not None:
                current[1].append((e.group(1), int(e.group(2))))
    return logs, result


def gen_quest_ids():
    """QuestId.g.cs: the numeric xi.quest.id of each quest, named as QuestKey.g.cs names it (same overrides)."""
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/quests.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class QuestId", "    {"]
    for area, quests in sorted(quest_ids(), key=lambda a: a[0]):
        cls = area[:1].upper() + area[1:]
        names = Names("QuestKey." + cls, cls)
        lines += ["        public static class %s" % cls, "        {"]
        for const, qid in quests:
            n = names.add(const, pascal(const), qid)
            if n:
                lines.append("            public const int %s = %d;" % (n, qid))
        lines += ["        }"]
    lines += ["    }", "}", ""]
    write("QuestId.g.cs", lines)


def gen_missions():
    logs, areas = mission_ids()
    names = Names("MissionLog", "MissionLog")
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/missions.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class MissionLog", "    {"]
    for key, value in logs:
        n = names.add(key, pascal(key), value)
        if n:
            lines.append("        public const int %s = %d;" % (n, value))
    lines += ["    }", "}", ""]
    write("MissionLog.g.cs", lines)
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/missions.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class Mission", "    {"]
    for area, missions in sorted(areas, key=lambda a: a[0]):
        cls = area[:1].upper() + area[1:]
        names = Names("Mission." + cls, cls)
        lines += ["        public static class %s" % cls, "        {"]
        for const, mid in missions:
            n = names.add(const, pascal(const), mid)
            if n:
                lines.append("            public const int %s = %d;" % (n, mid))
        lines += ["        }"]
    lines += ["    }", "}", ""]
    write("Mission.g.cs", lines)


def gen_quest_keys():
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/quests.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class QuestKey", "    {"]
    for area, quests in sorted(quest_ids(), key=lambda a: a[0]):
        cls = area[:1].upper() + area[1:]
        names = Names("QuestKey." + cls, cls)
        lines += ["        public static class %s" % cls, "        {"]
        for const, qid in quests:
            n = names.add(const, pascal(const), qid)
            if n:
                lines.append('            public const string %s = "%s.%s";' % (n, area, const.lower()))
        lines += ["        }"]
    lines += ["    }", "}", ""]
    write("QuestKey.g.cs", lines)


def gen_mission_keys():
    """MissionKey.g.cs: mission.<log>.<const lower> per mission, with the names of Mission.g.cs (same overrides)."""
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/missions.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class MissionKey", "    {"]
    _, areas = mission_ids()
    for area, missions in sorted(areas, key=lambda a: a[0]):
        cls = area[:1].upper() + area[1:]
        names = Names("Mission." + cls, cls)
        lines += ["        public static class %s" % cls, "        {"]
        for const, mid in missions:
            n = names.add(const, pascal(const), mid)
            if n:
                lines.append('            public const string %s = "mission.%s.%s";' % (n, area.lower(), const.lower()))
        lines += ["        }"]
    lines += ["    }", "}", ""]
    write("MissionKey.g.cs", lines)


def gen_battlefield_keys(entries):
    """BattlefieldKey.g.cs: battlefield.<const lower> per xi.battlefield.id constant, named as Battlefield.g.cs names it
    (entries: (Lua key, C# name, id) of the Battlefield class the enum loop wrote)."""
    if not entries:
        raise SystemExit("gen_battlefield_keys: no battlefield constants: "
                         "the enum loop did not reach the Battlefield class")
    lines = ["// generated by tools/gen_content_ids.py from scripts/globals/battlefield.lua; do not edit",
             "namespace XI.QuestContent", "{", "    public static class BattlefieldKey", "    {"]
    for key, name, _ in entries:
        lines.append('        public const string %s = "battlefield.%s";' % (name, key.lower()))
    lines += ["    }", "}", ""]
    write("BattlefieldKey.g.cs", lines)


def zone_sections(path):
    """(zone CONST, {text|mob: [(key, int value or None)]}): the entries at depth 1 of those two tables."""
    zone = None
    sections = {"text": [], "mob": []}
    depth = 0
    section = None
    pending = None
    for _, code in lua_lines(path):
        zm = re.match(r"^zones\[xi\.zone\.(\w+)\]", code)
        if zm and depth == 0:
            zone = zm.group(1)
        if depth == 1:
            sm = re.match(r"^\s*(\w+)\s*=\s*(\{|$)", code)
            if sm:
                pending = sm.group(1)
        if depth == 2 and section in sections:
            m = re.match(r"^\s*([A-Za-z_]\w*)\s*=", code)
            if m:
                lit = ENTRY.match(code)
                sections[section].append((m.group(1), int(lit.group(2)) if lit else None))
        before = depth
        depth += depth_change(code)
        if before == 1 and depth == 2:
            section = pending
        elif depth < 2:
            section = None
    return zone, sections


def ident(file_name):
    name = re.sub(r"[^A-Za-z0-9]", "", file_name)
    return "_" + name if name[:1].isdigit() else name


SETTING = re.compile(r"^INSERT INTO `zone_settings` VALUES \((\d+),'[^']*',\d+,'([^']*)'\);")


def zone_settings():
    """{zoneid: name} of sql/zone_settings.sql (the name resolves scripts/zones/<name> and, through folder_name, the
    data folder)."""
    result = {}
    with open(os.path.join(ROOT, "sql", "zone_settings.sql"), encoding="utf-8") as f:
        for line in f:
            if line.startswith("INSERT INTO `zone_settings`"):
                m = SETTING.match(line)
                if not m:
                    sys.exit("zone_settings.sql: unreadable row: " + line.strip())
                result[int(m.group(1))] = m.group(2)
    return result


def folder_name(name):
    """The data/zones folder of a zone_settings name: port of XI.Zone ZoneInfo.FolderName (src/XI.Zone/ZoneInfo.cs),
    "Port_Windurst" -> port_windurst, "Abyssea-Konschtat" -> abyssea_konschtat."""
    out = []
    for ch in name.lower():
        if ch == "'":
            continue
        if ch.isascii() and ch.isalnum():
            out.append(ch)
        elif out and out[-1] != "_":
            out.append("_")
    return "".join(out).rstrip("_")


def yaml_scalar(s):
    # port of ScriptEngine.YamlScalar: trimmed, a " #" comment dropped, one pair of matching quotes removed
    s = s.strip()
    hash_at = s.find(" #")
    if hash_at >= 0:
        s = s[:hash_at].rstrip()
    if len(s) >= 2 and s[0] in "\"'" and s[-1] == s[0]:
        s = s[1:-1]
    return s


def yaml_entity_names(path, section):
    """The entity names (script ?? template) of one mobs.yaml ("spawns") or npcs.yaml ("npcs"), in file order: port of
    ScriptEngine.ScanEntityScripts (src/XI.Script/ScriptEngine.cs), two-space keys are ids, four-space script/template
    fields."""
    names = []
    if not os.path.exists(path):
        return names
    state = {"in": False, "id": False, "script": None, "template": None}

    def flush():
        if state["id"]:
            name = state["script"] if state["script"] is not None else state["template"]
            if name is not None:
                names.append(name)
        state.update(id=False, script=None, template=None)

    with open(path, encoding="utf-8") as f:
        for raw in f:
            raw = raw.rstrip("\r\n")
            if not raw or raw[0] == "#":
                continue
            if raw[0] != " ":
                flush()
                state["in"] = raw.startswith(section + ":")
                continue
            if not state["in"]:
                continue
            if len(raw) > 2 and raw[2] != " " and raw.startswith("  "):
                flush()
                state["id"] = re.fullmatch(r"[0-9]+", raw.strip().rstrip(":")) is not None
                continue
            if raw.startswith("    ") and len(raw) > 4 and raw[4] != " ":
                line = raw[4:]
                if line.startswith("script:"):
                    state["script"] = yaml_scalar(line[7:])
                elif line.startswith("template:"):
                    state["template"] = yaml_scalar(line[9:])
    flush()
    return names


# an object name of the zone data (doors, gates, levers): an underscore, then digits only (Norg's Oaken Door _700, the
# Lower Delkfutts Tower doors _540 ... _545). Handlers name such entities (['_700']), so they get a constant (quest rework
# P6). Its C# name is the name itself, which ident() gives (it keeps a name of a leading digit as _<name>: _0z0) and
# which is a legal C# identifier: Npc.Norg._700 = "_700"
LETTERLESS_OBJECT = re.compile(r"^_[0-9]+$")


def skip_entity(name):
    # names no handler can mean: no letter (2, 01, _, !, @450) unless an object name (_700, above), or a # / ?
    # (HomePoint#1, ???)
    if LETTERLESS_OBJECT.match(name):
        return False
    return not any(c.isalpha() for c in name) or "#" in name or "?" in name


def cs_string(value):
    return value.replace("\\", "\\\\").replace('"', '\\"')


DATA = os.path.join(ROOT, "data", "zones")
COUNT = None  # --count-yaml: the statistics of the zone yaml names, nothing written


def zone_yaml_names(folder):
    """(kept, skipped) entity names of one data folder's mobs.yaml spawns and npcs.yaml npcs, each a sorted set."""
    found = set(yaml_entity_names(os.path.join(DATA, folder, "mobs.yaml"), "spawns"))
    found.update(yaml_entity_names(os.path.join(DATA, folder, "npcs.yaml"), "npcs"))
    skipped = {n for n in found if skip_entity(n)}
    return sorted(found - skipped), sorted(skipped)


def gen_zone_classes(zone_ids):
    texts, mobs, npcs = [], [], []
    zones_dir = os.path.join(ROOT, "scripts", "zones")
    settings = zone_settings()
    mapped = set()
    for d in sorted(os.listdir(zones_dir)):
        ids = os.path.join(zones_dir, d, "IDs.lua")
        if not os.path.exists(ids):
            continue
        const, sections = zone_sections(ids)
        if const is None or pascal(const) not in zone_ids:
            sys.exit("%s: zone constant %s is not in the Zone class" % (ids, const))
        zcls = pascal(const)
        zid = zone_ids[zcls]

        tn = Names("Text." + zcls, zcls)
        t = []
        for key, value in sections["text"]:
            if value is None:
                continue
            n = tn.add(key, key, value)
            if n:
                t.append('            public static readonly ZoneText %s = new ZoneText(%d, "%s");' % (n, zid, key))
        texts.append((zcls, t))

        mn = Names("Mob." + zcls, zcls)
        m = []
        for key, _ in sections["mob"]:
            n = mn.add(key, key, key)
            if n:
                m.append('            public const string %s = "%s";' % (n, key))
        mobs.append((zcls, m))

        nn = Names("Npc." + zcls, zcls)
        files = set()
        for sub in ("npcs", "mobs"):
            sd = os.path.join(zones_dir, d, sub)
            if os.path.isdir(sd):
                files.update(f[:-4] for f in os.listdir(sd) if f.endswith(".lua") and os.path.isfile(os.path.join(sd, f)))
        # the entities of the zone's data folder (the folder the engine reads for this zone id): script files first, so
        # their constants keep their names, then the yaml names that have no file
        folder = folder_name(settings[zid]) if zid in settings else None
        kept, skipped = zone_yaml_names(folder) if folder else ([], [])
        if folder:
            mapped.add(folder)
        added = [n for n in kept if n not in files]
        consts = {}
        for value in sorted(files) + added:
            if not ident(value) and not (OVERRIDES.get(("Npc." + zcls, value)) or OVERRIDES.get(("Npc", value))):
                FAILED.append("no identifier for Npc.%s entity %s (no ASCII letter or digit); add an override line" % (zcls, ascii(value)))
                continue
            n = nn.add(value, ident(value), value)
            if n:
                consts[value] = n
        if COUNT is not None:
            COUNT["zones"] += 1 if folder and os.path.isdir(os.path.join(DATA, folder)) else 0
            COUNT["added"] += sum(1 for v in added if v in consts)
            COUNT["skipped"] += len(skipped)
        c = ['            public const string %s = "%s";' % (consts[v], cs_string(v)) for v in sorted(consts)]
        npcs.append((zcls, c))

    if COUNT is not None:
        # data folders the engine never reads for a scripts zone (no IDs.lua / Zone class maps to them)
        COUNT["unmapped"] = sorted(f for f in os.listdir(DATA) if os.path.isdir(os.path.join(DATA, f)) and f not in mapped
                                   and (os.path.exists(os.path.join(DATA, f, "npcs.yaml"))
                                        or os.path.exists(os.path.join(DATA, f, "mobs.yaml"))))

    for cls, source, items in (("Text", "text tables of scripts/zones/*/IDs.lua", texts),
                               ("Mob", "mob tables of scripts/zones/*/IDs.lua", mobs),
                               ("Npc", "npcs and mobs scripts of scripts/zones/*", npcs)):
        lines = ["// generated by tools/gen_content_ids.py from the %s; do not edit" % source]
        if cls == "Text":
            lines.append("using XI.Quests.Authoring;")
        lines += ["namespace XI.QuestContent", "{", "    public static class %s" % cls, "    {"]
        for zcls, entries in sorted(items):
            if not entries:
                continue
            lines += ["        public static class %s" % zcls, "        {"] + entries + ["        }"]
        lines += ["    }", "}", ""]
        write(cls + ".g.cs", lines)


def main():
    global OUT, DATA, COUNT
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out")
    ap.add_argument("--data")
    ap.add_argument("--allow-removed", action="store_true")
    ap.add_argument("--count-yaml", action="store_true")
    args = ap.parse_args()
    if args.out:
        OUT = os.path.abspath(args.out)
    if args.data:
        DATA = os.path.abspath(args.data)
    if args.count_yaml:
        COUNT = {"zones": 0, "added": 0, "skipped": 0}
    load_overrides()
    zone_ids = {}
    battlefield_names = []
    for table, fname, cls, ctype in ENUMS:
        rel = fname if "/" in fname else "enum/" + fname
        src = os.path.join(ROOT, "scripts", *rel.split("/"))
        names = Names(cls, cls)  # a member cannot share its enclosing type's name
        lines = [
            "// generated by tools/gen_content_ids.py from scripts/%s; do not edit" % rel,
            "namespace XI.QuestContent",
            "{",
            "    public static class %s" % cls,
            "    {",
        ]
        for key, value in parse(src, table):
            name = names.add(key, pascal(key), value)
            if name is None:
                continue
            if cls == "Battlefield":
                battlefield_names.append((key, name, value))
            if cls == "Zone":
                zone_ids[name] = value
            lines.append("        public const %s %s = %d;" % (ctype, name, value))
        lines += ["    }", "}", ""]
        write(cls + ".g.cs", lines)
    gen_quest_keys()
    gen_quest_ids()
    gen_missions()
    gen_mission_keys()
    gen_battlefield_keys(battlefield_names)
    gen_zone_classes(zone_ids)

    if COUNT is not None:
        collisions = [f for f in FAILED if f.startswith("collision in Npc.")]
        print("zones mapped: %d" % COUNT["zones"])
        print("data folders without a Zone class: %d" % len(COUNT["unmapped"]))
        for f in COUNT["unmapped"]:
            print("  " + f)
        print("names added: %d" % COUNT["added"])
        print("names skipped: %d" % COUNT["skipped"])
        print("Npc collisions: %d" % len(collisions))
        for f in collisions:
            print("  " + f)
        return

    current = {}
    if os.path.isdir(OUT):
        for name in sorted(os.listdir(OUT)):
            if name.endswith(".g.cs"):
                with open(os.path.join(OUT, name), "rb") as f:
                    current[name] = f.read()
    # the Lua -> C# direction: every constant of the current files is still produced
    removed = []
    for name, data in sorted(current.items()):
        removed += sorted(constants(data) - constants(GENERATED.get(name, b"")))
    for r in removed:
        print("removed: " + r)
    if removed and not args.allow_removed:
        FAILED.append("%d constant(s) removed; nothing written (--allow-removed writes anyway)" % len(removed))

    if args.check:
        for name in sorted(set(current) | set(GENERATED)):
            if current.get(name) != GENERATED.get(name):
                FAILED.append("differs: " + os.path.join(OUT, name))
    elif not removed or args.allow_removed:
        os.makedirs(OUT, exist_ok=True)
        for name, data in sorted(GENERATED.items()):
            with open(os.path.join(OUT, name), "wb") as f:
                f.write(data)

    if FAILED:
        for f in FAILED:
            print(f)
        sys.exit(1)


if __name__ == "__main__":
    main()
