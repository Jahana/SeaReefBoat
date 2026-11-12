using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section 3.3): the
    // battlefields written in C# (every BattlefieldScript of XI.QuestContent, and the verify suites' test ones) run
    // inside the Lua battlefield framework in place of their scripts/battlefields files. Each becomes a Lua content
    // table (the proxy: Container:new('Battlefield[<id>]') with every field Battlefield:new gives a content, so the
    // engine's registerBattlefield and addGroups and the other battlefields' Lua read it as a Lua one) whose function
    // fields call the C# battlefield's hooks. It is xi.battlefield.contents[id] and in
    // xi.battlefield.contentsByZone[zone], and it takes the place of the Lua container with its id at the same index,
    // or the slot of its LegacyPath when the Lua file is gone. Once every C# battlefield is placed, the hooks of each
    // zone holding one are set again as Battlefield:register sets them, in the zone's final container order: the first
    // battlefield of the zone takes the events, a battlefield whose entry NPC no earlier one names takes that NPC's
    // trade and trigger, and every battlefield its exit NPCs' trigger. A C# battlefield's hooks are the framework
    // port's (BattlefieldFramework), a Lua one's the Lua framework's; a Lua battlefield that no longer comes first
    // loses its hooks, and one that now does gains them. This runs inside the register pass, before the lookup is
    // built.
    public sealed partial class ZoneHost
    {
        // the BattlefieldScripts of XI.QuestContent, each instantiated once, in type-name order
        private List<DiscoveredBattlefield> battlefieldScripts_ = new List<DiscoveredBattlefield>();
        // the C# battlefields this load registered, by key
        private readonly Dictionary<string, BoundBattlefield> boundBattlefields_ =
            new Dictionary<string, BoundBattlefield>(StringComparer.Ordinal);

        private sealed class DiscoveredBattlefield : DiscoveredScript<BattlefieldScript>
        {
        }

        // the BattlefieldScripts of the containers (DiscoverContainers), in type-name order
        private static List<DiscoveredBattlefield> DiscoverBattlefieldScripts(List<DiscoveredContainer> containers) =>
            DiscoverScripts<DiscoveredBattlefield, BattlefieldScript>(containers);

        // the coverage labels of the C# battlefield with the key (BattlefieldScript.CoverageLabels); null when no C#
        // battlefield has it
        private IReadOnlyList<string> BattlefieldHandlerLabels(string key)
        {
            var tests = TestQuestScripts.OfType<BattlefieldScript>();
            foreach (var s in battlefieldScripts_.Select(d => d.Script).Where(s => s != null).Concat(tests))
                if (s.Key == key) return s.CoverageLabels();
            return null;
        }

        // a label of a C# battlefield reached (the coverage hook)
        internal void BattlefieldReached(string key, string label) => HandlerCalled?.Invoke(key, label);

        // the battlefields of the register pass (CSharpQuests.register), after the missions: those with a LegacyPath
        // first, then the others by key, each registered as RegisterBattlefieldScript says; one that throws is left to
        // Lua and logged. Then the hooks of each zone that got one
        private int RegisterBattlefieldScripts(LuaState l, HashSet<string> keys, int top)
        {
            // the last pass's proxies are dropped and their references released (a battlefield switched off leaves
            // no stale content); their zones get their hooks again below, the list's Lua contents back in place
            var lastZones = boundBattlefields_.Values.Select(b => b.Zone).ToList();
            foreach (var old in boundBattlefields_.Values) ReleaseBattlefield(l, old, top);
            boundBattlefields_.Clear();
            var all = (SkipDiscoveredQuests ? new List<DiscoveredBattlefield>() : battlefieldScripts_)
                .Concat(TestQuestScripts.OfType<BattlefieldScript>()
                    .Select(s => new DiscoveredBattlefield { TypeName = s.GetType().FullName, Script = s }));
            var ordered = LegacyFirst(all, d => d.Script?.LegacyPath, d => d.Script?.Key ?? d.TypeName);
            int registered = RegisterEach(l, ordered, top, d => d.TypeName,
                (DiscoveredBattlefield d, ref string name, ref bool duplicate, out QuestScriptBinding bound) =>
                {
                    bound = null;
                    return RegisterBattlefieldScript(l, d, keys, ref name);
                });
            var zones = boundBattlefields_.Values.Select(b => b.Zone).Concat(lastZones).Distinct().OrderBy(z => z);
            foreach (var zone in zones)
            {
                try
                {
                    SetZoneBattlefieldHooks(l, zone);
                }
                catch (Exception e)
                {
                    log_($"quests: the battlefield hooks of zone {zone} not set: {e.Message}");
                }
                finally
                {
                    l.SetTop(top);
                }
            }
            return registered;
        }

        // one proxy of the last pass taken out of the Lua tables, its registry reference released
        private void ReleaseBattlefield(LuaState l, BoundBattlefield b, int top)
        {
            try
            {
                if (b.Proxy == null) return;
                l.GetPath("CSharpQuests.dropBattlefield");
                l.PushValue(1);
                l.PushRef(b.Proxy);
                l.Call(2, 0);
            }
            catch (Exception e)
            {
                log_($"quests: {b.Key}: its last proxy not released: {e.Message}");
            }
            finally
            {
                if (b.Proxy != null)
                {
                    l.Unref(b.Proxy);
                    b.Proxy = null;
                }
                l.SetTop(top);
            }
        }

        // resolves, validates, builds and substitutes one battlefield; false when it is skipped or refused (logged),
        // the Lua file then in charge. As a mission's (RegisterMissionScript): CSHARP_QUESTS off registers none; a key
        // in CSHARP_QUESTS_OFF leaves the Lua file in charge while it exists, and is logged and kept in C# when it is
        // gone; a battlefield refused with no Lua file to fall back to is left out, logged with the no-fallback line
        // and counted in QuestLoadFailures. name: the battlefield's key once known, for the caller's log line
        private bool RegisterBattlefieldScript(LuaState l, DiscoveredBattlefield d, HashSet<string> keys,
                                               ref string name)
        {
            if (d.Script == null)
            {
                if (CSharpQuestsEnabled) log_($"quests: {d.TypeName} not registered: {d.Error}");
                return false;
            }
            string key = d.Script.Key;
            name = key;
            bool first = keys.Add(key);
            if (!CSharpQuestsEnabled) return false;
            int? id = BattlefieldConstId(l, d.Script.ConstName);
            string containerId = id == null ? null : BattlefieldContainerId(id.Value);
            // the Lua file's container is in the list (the file is there); without one, a refusal has no fallback
            bool hasLua = id != null && HasContainer(l, containerId);
            // whether a refusal leaves the Lua in charge: hasLua, or for an unknown constant or a second class with
            // the key (where the container tells nothing) whether the LegacyPath file is there
            bool fallback = hasLua;
            bool Refuse(string reason, IList<string> each = null) =>
                RefuseContainer("battlefield", key, fallback, reason, each);
            if (CSharpQuestsOff.Contains(key))
            {
                // OFF leaves the Lua file in charge while it exists; after its deletion there is nothing to fall back
                // to, and rollback is a git revert
                if (id == null || hasLua) return false;
                log_($"quests: {key} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#");
            }
            if (!first)
            {
                fallback = LegacyFileExists(l, d.Script.LegacyPath);
                return Refuse($"another C# container ({d.TypeName}) has the key");
            }
            if (id == null)
            {
                fallback = LegacyFileExists(l, d.Script.LegacyPath);
                return Refuse("unknown battlefield constant");
            }
            BattlefieldData data;
            try
            {
                data = d.Script.Data;
            }
            catch (Exception e)
            {
                return Refuse($"its data threw: {InnerMessage(e)}");
            }
            var problems = ValidateBattlefieldScript(l, d.Script, id.Value);
            if (problems.Count > 0) return Refuse(string.Join("; ", problems), each: problems);

            var b = new BoundBattlefield(key, d.Script, data, id.Value);
            b.MakeCallbackRef = cb => BattlefieldCallbackFunction(b, cb);
            int top = l.Top;
            try
            {
                int proxy = BuildBattlefieldProxy(l, b);
                PlaceContainer(l, key, containerId, proxy, d.Script.LegacyPath);
                // xi.battlefield.contents[id] = proxy (the zone's list is set with the hooks)
                l.GetPath("xi.battlefield.contents");
                l.Push((double)id.Value);
                l.PushValue(proxy);
                l.SetTable(-3);
                boundBattlefields_[key] = b;
                return true;
            }
            catch (Exception e)
            {
                containerCache_ = null;
                // the proxy took its registry reference before the build threw
                if (b.Proxy != null)
                {
                    l.Unref(b.Proxy);
                    b.Proxy = null;
                }
                return Refuse(e.Message);
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // a loot table, { { { itemId, weight, amount }, ..., quantity }, ... }, pushed: the groups in order, each
        // group's entries in its array part
        internal static void PushLootGroups(LuaState l, IReadOnlyList<BattlefieldLootGroup> groups)
        {
            l.NewTable(groups.Count, 0);
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                l.NewTable(g.Entries.Count, g.Quantity != null ? 1 : 0);
                for (int j = 0; j < g.Entries.Count; j++)
                {
                    var e = g.Entries[j];
                    l.NewTable(0, 3);
                    l.Push((double)e.ItemId);
                    l.SetField(-2, "itemId");
                    l.Push((double)e.Weight);
                    l.SetField(-2, "weight");
                    if (e.Amount is { } amount)
                    {
                        l.Push((double)amount);
                        l.SetField(-2, "amount");
                    }
                    l.RawSetI(-2, j + 1);
                }
                if (g.Quantity is { } quantity)
                {
                    l.Push((double)quantity);
                    l.SetField(-2, "quantity");
                }
                l.RawSetI(-2, i + 1);
            }
        }

        // the Lua file of a LegacyPath is there: GetContainerFilenamesList (the scripts engine's view, a hidden file
        // gone) lists it; false for none
        private static bool LegacyFileExists(LuaState l, string legacyPath)
        {
            if (legacyPath == null) return false;
            int top = l.Top;
            try
            {
                l.GetGlobal("GetContainerFilenamesList");
                if (!l.IsFunction(-1)) return false;
                l.Call(0, 1);
                if (!l.IsTable(-1)) return false;
                int n = l.ObjLen(-1);
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(-1, i);
                    bool same = l.IsString(-1) && l.ToStringValue(-1) == legacyPath;
                    l.Pop(1);
                    if (same) return true;
                }
                return false;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // Battlefield[<id>]: Battlefield.getVarPrefix, the container id and var prefix
        internal static string BattlefieldContainerId(int id) => $"Battlefield[{id}]";

        // xi.battlefield.id[constName], null when it is not there
        private static int? BattlefieldConstId(LuaState l, string constName)
        {
            int top = l.Top;
            try
            {
                l.GetPath("xi.battlefield.id");
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, constName);
                return l.IsNumber(-1) ? (int)l.ToIntegral(-1) : null;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // the registration's checks (public for the verify suites): BattlefieldScriptValidator's, the constant's id is
        // the data's, the zone has an IDs table, every entry and exit NPC is in the zone's data, every text and mob key
        // the data names is in the zone's IDs table, and a level cap setting is in xi.settings.main
        public List<string> ValidateBattlefieldScript(LuaState l, BattlefieldScript s, int id)
        {
            var problems = new List<string>(BattlefieldScriptValidator.Validate(s));
            if (problems.Count > 0) return problems;
            var d = s.Data;
            if (d.BattlefieldId != id)
                problems.Add($"its id {d.BattlefieldId} is not xi.battlefield.id.{s.ConstName} ({id})");
            int top = l.Top;
            try
            {
                l.GetGlobal("zones");
                if (l.IsTable(-1)) l.RawGetI(-1, d.ZoneId); else l.PushNil();
                if (!l.IsTable(-1))
                {
                    problems.Add($"zone {d.ZoneId} has no IDs table");
                    return problems;
                }
            }
            finally
            {
                l.SetTop(top);
            }
            var names = ZoneEntityNames((ushort)d.ZoneId);
            if (names != null)
                foreach (var npc in (d.EntryNpcs ?? Array.Empty<string>()).Concat(d.ExitNpcs ?? Array.Empty<string>()))
                    if (!names.Contains(npc)) problems.Add($"no NPC {npc} in zone {d.ZoneId}");
            foreach (var t in BattlefieldTexts(d))
                if (ZoneTextId(l, t.Zone, t.Name) == null) problems.Add($"no text {t.Name} in zone {t.Zone}");
            foreach (var m in BattlefieldMobRefs(d))
                if (ZoneMobId(l, d.ZoneId, m.Key) == null) problems.Add($"no mob {m.Key} in zone {d.ZoneId}'s IDs");
            if (d.LevelCapSetting != null && SettingNumber(l, d.LevelCapSetting) == null)
                problems.Add($"no number xi.settings.main.{d.LevelCapSetting}");
            // a BattlefieldQuest's quest is an xi.quest.id constant of its log
            if (d is BattlefieldQuestData qd &&
                (QuestScript.AreaName(qd.QuestArea) is not { } area || QuestConstId(l, area, qd.Quest) == null))
                problems.Add($"no quest xi.quest.id.{QuestScript.AreaName(qd.QuestArea)}.{qd.Quest}");
            return problems;
        }

        // the texts the data names
        private static IEnumerable<ZoneText> BattlefieldTexts(BattlefieldData d)
        {
            if (d.RequiredItems?.WearMessage is { } wear) yield return wear;
            if (d.RequiredItems?.WornMessage is { } worn) yield return worn;
            if (d.RequiredKeyItems?.Message is { } message) yield return message;
            if (d.RequiredKeyItems?.DeleteMessage is { } delete) yield return delete;
        }

        // the mob ids the data names (mobIds, armouryCrates, paths)
        private static IEnumerable<BattlefieldMobId> BattlefieldMobRefs(BattlefieldData d)
        {
            foreach (var g in d.Groups)
            {
                foreach (var m in g.MobIds ?? Array.Empty<BattlefieldMobId>()) yield return m;
                foreach (var area in g.AreaMobIds ?? Array.Empty<BattlefieldMobId[]>())
                    foreach (var m in area) yield return m;
            }
            foreach (var c in d.ArmouryCrates ?? Array.Empty<BattlefieldArmouryCrate>()) yield return c.Id;
            foreach (var p in d.Paths) yield return p.Mob;
        }

        // zones[zone][kind][key] as a number, null for nil
        internal static int? ZoneEntryId(LuaState l, int zone, string kind, string key)
        {
            int top = l.Top;
            try
            {
                l.GetGlobal("zones");
                if (l.IsTable(-1)) l.RawGetI(-1, zone); else l.PushNil();
                if (l.IsTable(-1)) l.GetField(-1, kind); else l.PushNil();
                if (l.IsTable(-1)) l.GetField(-1, key); else l.PushNil();
                return l.IsNumber(-1) ? (int)l.ToIntegral(-1) : null;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // zones[zone].text[name], null for nil
        internal static int? ZoneTextId(LuaState l, int zone, string name) => ZoneEntryId(l, zone, "text", name);

        // zones[zone].mob[key], null for nil
        private static int? ZoneMobId(LuaState l, int zone, string key) => ZoneEntryId(l, zone, "mob", key);

        // xi.settings.main[name] as a number, null when it is not one
        private static double? SettingNumber(LuaState l, string name)
        {
            int top = l.Top;
            try
            {
                l.GetPath("xi.settings.main");
                if (!l.IsTable(-1)) return null;
                l.GetField(-1, name);
                return l.IsNumber(-1) ? l.ToNumber(-1) : null;
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // a mob id as the battlefield file writes it: zones[zone].mob[Key] + Offset
        private static int ResolveMobId(LuaState l, int zone, BattlefieldMobId m) =>
            (ZoneMobId(l, zone, m.Key) ?? throw new InvalidOperationException($"no mob {m.Key} in zone {zone}'s IDs")) +
            m.Offset;

        // ---- the proxy ---------------------------------------------------------------------------------------------

        // local content = Battlefield:new(data) as a Container:new('Battlefield[<id>]') with Battlefield:new's fields,
        // its groups (content.groups, then addEssentialMobs'), its loot and paths, and a function field per framework
        // method calling the C# battlefield's hook; leaves it on the stack and returns its index
        private int BuildBattlefieldProxy(LuaState l, BoundBattlefield b)
        {
            var d = b.Data;
            l.GetGlobal("Container");
            int cls = l.Top;
            l.GetField(cls, "new");
            l.PushValue(cls);
            l.PushString(BattlefieldContainerId(b.Id));
            l.Call(2, 1);
            l.Remove(cls);
            int proxy = l.Top;
            l.PushValue(proxy);
            b.Proxy = l.Ref();
            l.PushString("csharp:" + b.Key);
            l.SetField(proxy, "filename");

            void Num(string field, double v)
            {
                l.Push(v);
                l.SetField(proxy, field);
            }
            void Bool(string field, bool v)
            {
                l.Push(v);
                l.SetField(proxy, field);
            }
            void Strings(string field, string[] values)
            {
                if (values == null) return;
                PushIntsOrStrings(l, values.Cast<object>());
                l.SetField(proxy, field);
            }
            int Text(ZoneText t) => ZoneTextId(l, t.Zone, t.Name) ??
                                    throw new InvalidOperationException($"no text {t.Name} in zone {t.Zone}");

            Num("zoneId", d.ZoneId);
            Num("battlefieldId", d.BattlefieldId);
            Num("maxPlayers", d.MaxPlayers);
            Num("timeLimit", d.TimeLimit);
            Num("index", d.Index);
            Strings("entryNpcs", d.EntryNpcs);
            if (d.Area != 0) Num("area", d.Area);
            Strings("exitNpcs", d.ExitNpcs);
            if (d.Title != 0) Num("title", d.Title);
            if (d.GrantXP != 0) Num("grantXP", d.GrantXP);
            if (d.GrantXPLockout) Bool("grantXPLockout", true);
            // levelCap = data.levelCap or 0 (a setting read now, as the Lua file reads it when it loads)
            Num("levelCap", d.LevelCapSetting != null ? SettingNumber(l, d.LevelCapSetting) ?? 0 : d.LevelCap);
            Bool("allowSubjob", d.AllowSubjob);
            Bool("allowTrusts", d.AllowTrusts);
            Bool("hasWipeGrace", d.HasWipeGrace);
            // BattlefieldMission:new sets isMission whatever the data says
            Bool("isMission", d.IsMission || d is BattlefieldMissionData);
            Bool("canLoseExp", d.CanLoseExp);
            Bool("cleanupDebuffs", d.CleanupDebuffs);
            Bool("showTimer", d.ShowTimer);
            Num("delayToExit", d.DelayToExit);

            // requiredItems = { items..., wearMessage = id, wornMessage = id | { id, args... }, keep = true }
            l.NewTable();
            if (d.RequiredItems is { } items)
            {
                for (int i = 0; i < items.Items.Length; i++)
                {
                    l.Push((double)items.Items[i]);
                    l.RawSetI(-2, i + 1);
                }
                if (items.WearMessage is { } wear)
                {
                    l.Push((double)Text(wear));
                    l.SetField(-2, "wearMessage");
                }
                if (items.WornMessage is { } worn)
                {
                    if (items.WornMessageArgs != null)
                        PushIntsOrStrings(l, new object[] { Text(worn) }.Concat(items.WornMessageArgs.Cast<object>()));
                    else l.Push((double)Text(worn));
                    l.SetField(-2, "wornMessage");
                }
                if (items.Keep)
                {
                    l.Push(true);
                    l.SetField(-2, "keep");
                }
            }
            l.SetField(proxy, "requiredItems");

            // requiredKeyItems = { ki | { ki, ... }, ..., onlyInitiator, keep, message, deleteMessage }
            l.NewTable();
            if (d.RequiredKeyItems is { } kis)
            {
                for (int i = 0; i < kis.Items.Count; i++)
                {
                    if (kis.Items[i].IsTable) PushIntsOrStrings(l, kis.Items[i].Ids.Cast<object>());
                    else l.Push((double)kis.Items[i].Ids[0]);
                    l.RawSetI(-2, i + 1);
                }
                if (kis.OnlyInitiator)
                {
                    l.Push(true);
                    l.SetField(-2, "onlyInitiator");
                }
                if (kis.Keep)
                {
                    l.Push(true);
                    l.SetField(-2, "keep");
                }
                if (kis.Message is { } message)
                {
                    if (kis.MessageArgs != null)
                    {
                        l.NewTable();
                        l.Push((double)Text(message));
                        l.RawSetI(-2, 1);
                        PushIntsOrStrings(l, kis.MessageArgs.Cast<object>());
                        l.RawSetI(-2, 2);
                    }
                    else l.Push((double)Text(message));
                    l.SetField(-2, "message");
                }
                if (kis.DeleteMessage is { } deleteMessage)
                {
                    l.Push((double)Text(deleteMessage));
                    l.SetField(-2, "deleteMessage");
                }
            }
            l.SetField(proxy, "requiredKeyItems");

            PushIntsOrStrings(l, (d.LossEventParams ?? Array.Empty<int>()).Cast<object>());
            l.SetField(proxy, "lossEventParams");
            // armouryCrates = data.armouryCrates or false
            if (d.ArmouryCrates != null)
                PushIntsOrStrings(l, d.ArmouryCrates.Select(c => (object)ResolveMobId(l, d.ZoneId, c.Id)));
            else l.Push(false);
            l.SetField(proxy, "armouryCrates");
            Bool("experimental", d.Experimental);
            // allowedAreas = set{ ... }
            if (d.AllowedAreas != null)
            {
                l.NewTable();
                foreach (int area in d.AllowedAreas)
                {
                    l.Push(true);
                    l.RawSetI(-2, area);
                }
                l.SetField(proxy, "allowedAreas");
            }
            Num("csParam7", d.CsParam7);
            Num("csParam8", d.CsParam8);

            // BattlefieldMission:new's fields and BattlefieldQuest:new's (and the class's isQuest), a nil left out
            if (d is BattlefieldMissionData m)
            {
                if (m.MissionArea is { } missionArea) Num("missionArea", (int)missionArea);
                Num("mission", m.Mission.Value);
                if (m.MissionStatusArea is { } statusArea) Num("missionStatusArea", (int)statusArea);
                if (m.MissionStatus is { } missionStatus) Num("missionStatus", missionStatus);
                if (m.RequiredMissionStatus is { } required) Num("requiredMissionStatus", required);
                if (m.SkipStatus is { } skip) Num("skipMissionStatus", skip);
                if (m.RequiredVar != null)
                {
                    l.PushString(m.RequiredVar);
                    l.SetField(proxy, "requiredVar");
                    Num("requiredValue", m.RequiredValue);
                }
            }
            else if (d is BattlefieldQuestData qd)
            {
                Num("questArea", (int)qd.QuestArea);
                Num("quest", QuestConstId(l, QuestScript.AreaName(qd.QuestArea), qd.Quest) ??
                             throw new InvalidOperationException($"no quest {qd.Quest} in {qd.QuestArea}"));
                Bool("repeatable", qd.Repeatable);
                if (qd.RequiredVar != null)
                {
                    l.PushString(qd.RequiredVar);
                    l.SetField(proxy, "requiredVar");
                    Num("requiredValue", qd.RequiredValue);
                }
                Bool("isQuest", true);
            }

            // sections = { { [zoneId] = { onEventUpdate = { [csid] = fn }, onEventFinish = { ... } } } }: the
            // battlefield's own handlers; its hooks are set with the zone's (SetZoneBattlefieldHooks), which
            // utils.append merges into the same tables, as Battlefield:register does for one_to_be_feared.lua
            l.NewTable();
            l.NewTable();
            l.NewTable();
            PushSectionEvents(l, b, "onEventUpdate", d.OnEventUpdate);
            PushSectionEvents(l, b, "onEventFinish", d.OnEventFinish);
            l.RawSetI(-2, d.ZoneId);
            l.RawSetI(-2, 1);
            l.SetField(proxy, "sections");

            // groups: content.groups, then the essential ones
            var groups = BattlefieldFramework.AddEssentialMobs(d);
            l.NewTable();
            for (int i = 0; i < groups.Count; i++)
            {
                PushBattlefieldGroup(l, b, groups[i], d.Groups.IndexOf(groups[i]) + 1);
                l.RawSetI(-2, i + 1);
            }
            l.SetField(proxy, "groups");

            // paths: [mob id] = { x, y, z, ... }
            l.NewTable();
            foreach (var p in d.Paths)
            {
                PushIntsOrStrings(l, p.Points.Cast<object>());
                l.RawSetI(-2, ResolveMobId(l, d.ZoneId, p.Mob));
            }
            l.SetField(proxy, "paths");

            // loot: { { { itemId, weight, amount }, ..., quantity }, ... }
            PushLootGroups(l, d.Loot);
            l.SetField(proxy, "loot");

            // tradeItems: the requiredItems' array part
            PushIntsOrStrings(l, d.TradeItems.Cast<object>());
            l.SetField(proxy, "tradeItems");

            foreach (var (field, fn) in BattlefieldProxyMethods(b))
            {
                l.PushFunction(fn);
                l.SetField(proxy, field);
            }
            foreach (var (field, value) in d.ProxyFields.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                l.GetField(proxy, field);
                bool taken = !l.IsNil(-1);
                l.Pop(1);
                if (taken) throw new InvalidOperationException($"proxy field {field} is a framework field");
                PushProxyValue(l, value);
                l.SetField(proxy, field);
            }
            return proxy;
        }

        // the zone section's handlers of the kind, as a table of its section at the top of the stack; none when empty.
        // The lookup calls each with (player, csid, option, npc)
        private void PushSectionEvents(LuaState l, BoundBattlefield b, string kind,
                                       Dictionary<int, BattlefieldEventHandler> handlers)
        {
            if (handlers.Count == 0) return;
            l.NewTable();
            foreach (var (csid, run) in handlers.OrderBy(h => h.Key))
            {
                string label = BattlefieldScript.SectionLabel(kind, csid);
                l.PushFunction(BattlefieldClosure(b, label, player: 1, npc: 4, body: (q, cl) =>
                {
                    q.Reached(label);
                    run(q, cl.OptInt(2, 0), cl.OptInt(3, 0));
                    return 0;
                }));
                l.RawSetI(-2, csid);
            }
            l.SetField(-2, kind);
        }

        // a proxy field's value (BattlefieldData.ProxyFields) as Lua: the validator took its types
        private static void PushProxyValue(LuaState l, object value)
        {
            switch (value)
            {
                case object[] array:
                    l.NewTable();
                    for (int i = 0; i < array.Length; i++)
                    {
                        PushProxyValue(l, array[i]);
                        l.RawSetI(-2, i + 1);
                    }
                    break;
                case Dictionary<string, object> table:
                    l.NewTable();
                    foreach (var (k, v) in table)
                    {
                        PushProxyValue(l, v);
                        l.SetField(-2, k);
                    }
                    break;
                case int n:
                    l.Push((double)n);
                    break;
                default:
                    l.Push(value);
                    break;
            }
        }

        // an array of numbers or strings, as a new table
        private static void PushIntsOrStrings(LuaState l, IEnumerable<object> values)
        {
            l.NewTable();
            int i = 0;
            foreach (var v in values)
            {
                l.Push(v);
                l.RawSetI(-2, ++i);
            }
        }

        // one group of content.groups as CLuaBattlefield::addGroups reads it; number: its place in content.groups (0
        // for an essential group)
        private void PushBattlefieldGroup(LuaState l, BoundBattlefield b, BattlefieldGroup g, int number)
        {
            var d = b.Data;
            l.NewTable();
            if (g.Mobs != null)
            {
                PushIntsOrStrings(l, g.Mobs.Cast<object>());
                l.SetField(-2, "mobs");
            }
            if (g.MobIds != null)
            {
                PushIntsOrStrings(l, g.MobIds.Select(m => (object)ResolveMobId(l, d.ZoneId, m)));
                l.SetField(-2, "mobIds");
            }
            else if (g.AreaMobIds != null)
            {
                l.NewTable();
                for (int i = 0; i < g.AreaMobIds.Length; i++)
                {
                    PushIntsOrStrings(l, g.AreaMobIds[i].Select(m => (object)ResolveMobId(l, d.ZoneId, m)));
                    l.RawSetI(-2, i + 1);
                }
                l.SetField(-2, "mobIds");
            }
            void Flag(string field, bool? v)
            {
                if (v is not { } value) return;
                l.Push(value);
                l.SetField(-2, field);
            }
            Flag("superlink", g.Superlink ? true : null);
            Flag("isParty", g.IsParty ? true : null);
            Flag("spawned", g.Spawned);
            Flag("stationary", g.Stationary);
            if (g.SuperlinkGroup is { } superlinkGroup)
            {
                l.Push((double)superlinkGroup);
                l.SetField(-2, "superlinkGroup");
            }
            if (g.EssentialIndex > 0)
            {
                // allDeath = utils.bind(self.handleAllMonstersDefeated, self): (battlefield, mob)
                string label = $"essentialMobs:{g.EssentialIndex}";
                l.PushFunction(BattlefieldClosure(b, "allDeath " + label, bf: 1, mob: 2, body: (q, _) =>
                {
                    q.Reached(label);
                    BattlefieldFramework.Hook(q, nameof(BattlefieldScript.HandleAllMonstersDefeated));
                    b.Script.HandleAllMonstersDefeated(q, q.Mob);
                    return 0;
                }));
                l.SetField(-2, "allDeath");
                return;
            }
            void Callback(string field, Action<LiveBattlefieldContext, LuaState> run)
            {
                string label = $"group:{number}:{field}";
                l.PushFunction(BattlefieldClosure(b, label, bf: 1, mob: 2, body: (q, cl) =>
                {
                    q.Reached(label);
                    run(q, cl);
                    return 0;
                }));
                l.SetField(-2, field);
            }
            if (g.AllDeath != null) Callback("allDeath", (q, _) => g.AllDeath(q, q.Mob));
            if (g.RandomDeath != null) Callback("randomDeath", (q, _) => g.RandomDeath(q, q.Mob));
            if (g.Death != null) Callback("death", (q, cl) => g.Death(q, q.Mob, (int)cl.ToIntegral(3)));
            if (g.Setup != null)
            {
                string label = $"group:{number}:setup";
                l.PushFunction(BattlefieldClosure(b, label, bf: 1, body: (q, _) =>
                {
                    q.Reached(label);
                    g.Setup(q, q.MobsAt(2));
                    return 0;
                }));
                l.SetField(-2, "setup");
            }
        }

        // the proxy's function fields: content:<method>(...) as the framework's Lua calls it, each the C# hook
        private IEnumerable<(string Field, LuaFunction Fn)> BattlefieldProxyMethods(BoundBattlefield b)
        {
            var s = b.Script;
            // a hook with no result: content:<field>(args...)
            (string, LuaFunction) Run(string field, string hook, int player, int npc, int bf,
                                      Action<LiveBattlefieldContext, LuaState> run) =>
                (field, BattlefieldClosure(b, field, player: player, npc: npc, bf: bf, body: (q, l) =>
                {
                    BattlefieldFramework.Hook(q, hook);
                    run(q, l);
                    return 0;
                }));
            // a hook whose result is pushed
            (string, LuaFunction) Get(string field, string hook, int player, int npc, int trade,
                                      Func<LiveBattlefieldContext, LuaState, object> get) =>
                (field, BattlefieldClosure(b, field, player: player, npc: npc, trade: trade, body: (q, l) =>
                {
                    BattlefieldFramework.Hook(q, hook);
                    l.Push(get(q, l));
                    return 1;
                }));
            int Int(LuaState l, int at) => l.IsNumber(at) ? (int)l.ToIntegral(at) : 0;

            // (self, player, npc, isRegistrant, trade)
            yield return Get("checkRequirements", nameof(s.CheckRequirements), 2, 3, 5,
                             (q, l) => s.CheckRequirements(q, l.ToBoolean(4)));
            yield return Get("entryRequirement", nameof(s.EntryRequirement), 2, 3, 5,
                             (q, l) => s.EntryRequirement(q, l.ToBoolean(4)));
            // (self, player)
            yield return Get("checkSkipCutscene", nameof(s.CheckSkipCutscene), 2, 0, 0,
                             (q, _) => s.CheckSkipCutscene(q));
            // (self, player, csid, option, npc)
            yield return Get("onEntryEventUpdate", nameof(s.OnEntryEventUpdate), 2, 5, 0,
                             (q, l) => (double)s.OnEntryEventUpdate(q, Int(l, 3), Int(l, 4)));
            yield return Run("onEventFinishEnter", nameof(s.OnEventFinishEnter), 2, 5, 0,
                             (q, l) => s.OnEventFinishEnter(q, Int(l, 3), Int(l, 4)));
            yield return Run("onEventFinishWin", nameof(s.OnEventFinishWin), 2, 5, 0,
                             (q, l) => s.OnEventFinishWin(q, Int(l, 3), Int(l, 4)));
            yield return Run("onExitEventUpdate", nameof(s.OnExitEventUpdate), 2, 5, 0,
                             (q, l) => s.OnExitEventUpdate(q, Int(l, 3), Int(l, 4)));
            yield return Run("onEventFinishLeave", nameof(s.OnEventFinishLeave), 2, 5, 0,
                             (q, l) => s.OnEventFinishLeave(q, Int(l, 3), Int(l, 4)));
            yield return Run("onEventFinishExit", nameof(s.OnEventFinishExit), 2, 5, 0,
                             (q, l) => s.OnEventFinishExit(q, Int(l, 3), Int(l, 4)));
            yield return Run("onEventFinishBattlefield", nameof(s.OnEventFinishBattlefield), 2, 5, 0,
                             (q, l) => s.OnEventFinishBattlefield(q, Int(l, 3), Int(l, 4)));
            // (self, battlefield)
            yield return Run("setupBattlefield", nameof(s.SetupBattlefield), 0, 0, 2, (q, _) => s.SetupBattlefield(q));
            yield return Run("onBattlefieldInitialize", nameof(s.OnBattlefieldInitialize), 0, 0, 2,
                             (q, _) => s.OnBattlefieldInitialize(q));
            // (self, battlefield, tick)
            yield return Run("onBattlefieldTick", nameof(s.OnBattlefieldTick), 0, 0, 2,
                             (q, l) => s.OnBattlefieldTick(q, Int(l, 3)));
            // (self, player, battlefield)
            yield return Run("onBattlefieldRegister", nameof(s.OnBattlefieldRegister), 2, 0, 3,
                             (q, _) => s.OnBattlefieldRegister(q));
            // (self, battlefield, status)
            yield return Run("onBattlefieldStatusChange", nameof(s.OnBattlefieldStatusChange), 0, 0, 2,
                             (q, l) => s.OnBattlefieldStatusChange(q, Int(l, 3)));
            yield return Run("battlefieldEntry", nameof(s.BattlefieldEntry), 2, 0, 3, (q, _) => s.BattlefieldEntry(q));
            yield return Run("onBattlefieldEnter", nameof(s.OnBattlefieldEnter), 2, 0, 3,
                             (q, _) => s.OnBattlefieldEnter(q));
            yield return Run("onBattlefieldDestroy", nameof(s.OnBattlefieldDestroy), 0, 0, 2,
                             (q, _) => s.OnBattlefieldDestroy(q));
            // (self, player, battlefield, leavecode)
            yield return Run("onBattlefieldLeave", nameof(s.OnBattlefieldLeave), 2, 0, 3,
                             (q, l) => s.OnBattlefieldLeave(q, Int(l, 4)));
            yield return Run("onBattlefieldWin", nameof(s.OnBattlefieldWin), 2, 0, 3, (q, _) => s.OnBattlefieldWin(q));
            yield return Run("onBattlefieldLoss", nameof(s.OnBattlefieldLoss), 2, 0, 3,
                             (q, _) => s.OnBattlefieldLoss(q));
            // (self, player)
            yield return Run("onBattlefieldKick", nameof(s.OnBattlefieldKick), 2, 0, 0,
                             (q, _) => s.OnBattlefieldKick(q));
            // (self, battlefield, players)
            yield return Run("handleWipe", nameof(s.HandleWipe), 0, 0, 2, (q, _) => s.HandleWipe(q, q.PlayersArg(3)));
            yield return Run("onBattlefieldWipe", nameof(s.OnBattlefieldWipe), 0, 0, 2,
                             (q, _) => s.OnBattlefieldWipe(q, q.PlayersArg(3)));
            // (self, battlefield, mob)
            yield return ("handleAllMonstersDefeated", BattlefieldClosure(b, "handleAllMonstersDefeated", bf: 2, mob: 3,
                body: (q, _) =>
                {
                    BattlefieldFramework.Hook(q, nameof(s.HandleAllMonstersDefeated));
                    s.HandleAllMonstersDefeated(q, q.Mob);
                    return 0;
                }));
            // (self, player, npc)
            yield return Run("handleOpenArmouryCrate", nameof(s.HandleOpenArmouryCrate), 2, 3, 0,
                             (q, _) => s.HandleOpenArmouryCrate(q, q.Npc));
            // (self, battlefield, lootTable, npc, gilBonusMod): the lootTable is the caller's, as it is
            yield return ("handleLootRolls", BattlefieldClosure(b, "handleLootRolls", bf: 2, npc: 4, body: (q, l) =>
            {
                BattlefieldFramework.Hook(q, nameof(s.HandleLootRolls));
                s.HandleLootRolls(q, q.Battlefield, q.LootTableArg(3), q.Npc, Int(l, 5));
                return 0;
            }));
        }

        // a closure of the C# battlefield b (null: the framework's zone handlers, owner the key in the log line) over
        // its call's arguments: the positions of the player, the npc, the trade, the battlefield and the mob (0:
        // none); body runs on a context over them and pushes its results (0 or 1). A body that throws is logged
        // ("quests: <key> <name>: <message>") and returns nothing. The values the context keeps (the battlefield's
        // players, a content) stay on the stack until the closure returns
        private LuaFunction BattlefieldClosure(BoundBattlefield b, string name, int player = 0, int npc = 0,
                                               int trade = 0, int bf = 0, int mob = 0, string owner = null,
                                               Func<LiveBattlefieldContext, LuaState, int> body = null)
        {
            string key = b?.Key ?? owner;
            var binding = b?.Binding ?? new QuestScriptBinding(key, BattlefieldScript.FrameworkCore, "battlefield", 0);
            return l =>
            {
                int top = l.Top;
                int keep = 0;
                try
                {
                    var env = new BattlefieldCall(l, this, log_, b, binding, name);
                    var q = new LiveBattlefieldContext(env, player, npc, trade, bf, mob);
                    int n = body(q, l);
                    if (n > 0)
                    {
                        l.Insert(top + 1);
                        keep = 1;
                    }
                    return keep;
                }
                catch (Exception e)
                {
                    string line = $"quests: {key} {name}: {e.Message}";
                    log_(line);
                    HandlerThrew?.Invoke(line);
                    keep = 0;
                    return 0;
                }
                finally
                {
                    l.SetTop(top + keep);
                }
            };
        }

        // quest rework C3 PR2: a battlefield callback's C# function, which its closures (CSharpQuests.bfCallback)
        // call with (m, the engine's m arguments, the captures): a call of the battlefield whose mob is the engine's
        // first argument, and its player too when that is a char; the callback's label reached, then its body. An
        // exception is logged as a hook's is (BattlefieldClosure), and the queue or the listeners go on
        private LuaRef BattlefieldCallbackFunction(BoundBattlefield b, BattlefieldCallback cb)
        {
            string name = "callback " + cb.Name;
            lua_.PushFunction(l =>
            {
                int top = l.Top;
                try
                {
                    int m = l.IsNumber(1) ? (int)l.ToIntegral(1) : 0;
                    var env = new BattlefieldCall(l, this, log_, b, b.Binding, name);
                    int entity = m >= 1 ? 2 : 0;
                    int player = entity != 0 && l.ToObject(entity) is CharData ? entity : 0;
                    var q = new LiveBattlefieldContext(env, player, 0, 0, 0, entity);
                    q.Reached(cb.Label);
                    cb.Run(q, new LiveBattlefieldCallbackArgs(env, m, 2, m + 2, top - (m + 1)));
                    return 0;
                }
                catch (Exception e)
                {
                    string line = $"quests: {b.Key} {name}: {e.Message}";
                    log_(line);
                    HandlerThrew?.Invoke(line);
                    return 0;
                }
                finally
                {
                    l.SetTop(top);
                }
            });
            return lua_.Ref();
        }

        // ---- the zone's hooks --------------------------------------------------------------------------------------

        // the hook functions of a C# battlefield's section, as a table: onEntryTrade, onEntryTrigger,
        // redirectEventUpdate, redirectEventCall and exitTrigger[exit npc] (the framework port's, the exit one the
        // battlefield's own hook); leaves it on the stack
        private void PushBattlefieldHookFunctions(LuaState l, BoundBattlefield b)
        {
            string owner = b.Key;
            l.NewTable();
            // Battlefield.onEntryTrade(player, npc, trade, onUpdate)
            l.PushFunction(BattlefieldClosure(null, "onEntryTrade", player: 1, npc: 2, trade: 3, owner: owner,
                body: (q, cl) => PushAction(cl, BattlefieldFramework.OnEntryTrade(q, cl.ToBoolean(4)))));
            l.SetField(-2, "onEntryTrade");
            // Battlefield.onEntryTrigger(player, npc)
            l.PushFunction(BattlefieldClosure(null, "onEntryTrigger", player: 1, npc: 2, owner: owner,
                body: (q, cl) => PushAction(cl, BattlefieldFramework.OnEntryTrigger(q))));
            l.SetField(-2, "onEntryTrigger");
            // Battlefield.redirectEventUpdate(player, csid, option, npc): false, or nothing
            l.PushFunction(BattlefieldClosure(null, "redirectEventUpdate", player: 1, npc: 4, owner: owner,
                body: (q, cl) =>
                {
                    var r = BattlefieldFramework.RedirectEventUpdate(q, cl.OptInt(2, 0), cl.OptInt(3, 0));
                    if (r == null) return 0;
                    cl.Push(r.Value);
                    return 1;
                }));
            l.SetField(-2, "redirectEventUpdate");
            // Battlefield.redirectEventCall(eventName, player, csid, option), bound to its event name
            l.PushFunction(BattlefieldClosure(null, "redirectEventCall", player: 2, npc: 5, owner: owner,
                body: (q, cl) =>
                {
                    BattlefieldFramework.RedirectEventCall(q, cl.ToStringValue(1), cl.OptInt(3, 0), cl.OptInt(4, 0));
                    return 0;
                }));
            l.SetField(-2, "redirectEventCall");
            // content.onExitTrigger or Battlefield.onExitTrigger(player, npc), one per exit NPC (its coverage label)
            l.NewTable();
            foreach (var exitNpc in b.Data.ExitNpcs ?? Array.Empty<string>())
            {
                string label = "exit:" + exitNpc;
                l.PushFunction(BattlefieldClosure(b, "exitTrigger " + exitNpc, player: 1, npc: 2, body: (q, cl) =>
                {
                    q.Reached(label);
                    BattlefieldFramework.Hook(q, nameof(BattlefieldScript.OnExitTrigger));
                    return PushAction(cl, b.Script.OnExitTrigger(q));
                }));
                l.SetField(-2, exitNpc);
            }
            l.SetField(-2, "exitTrigger");
        }

        // an action (LiveBattlefieldAction) pushed, its registry reference released; 0 for none
        private static int PushAction(LuaState l, IBattlefieldAction action)
        {
            if (action is not LiveBattlefieldAction a) return 0;
            l.PushRef(a.Ref);
            l.Unref(a.Ref);
            return 1;
        }

        // the zone's battlefields in their final container order (CSharpQuests.battlefieldZone), each given its hooks
        // as Battlefield:register gives them in that order (BattlefieldFramework.RegisterHooks), a C# one through its
        // hook functions and a Lua one through the Lua framework's (CSharpQuests.battlefieldHooks)
        private void SetZoneBattlefieldHooks(LuaState l, ushort zone)
        {
            var mine = boundBattlefields_.Values.Where(b => b.Zone == zone).ToDictionary(b => b.Id);
            l.GetGlobal("CSharpQuests");
            int cs = l.Top;
            l.GetField(cs, "battlefieldZone");
            l.PushValue(1);
            l.Push((double)zone);
            l.NewTable();
            foreach (int id in mine.Keys)
            {
                l.Push(true);
                l.RawSetI(-2, id);
            }
            l.Call(3, 2);
            int inList = l.Top;
            int byZone = inList - 1;
            var earlier = new List<(int BattlefieldId, IReadOnlyList<string> EntryNpcs, bool HasListeners)>();
            int n = l.ObjLen(byZone);
            for (int i = 1; i <= n; i++)
            {
                l.RawGetI(byZone, i);
                int content = l.Top;
                int id = FieldIntOr(l, content, "battlefieldId", -1);
                var entryNpcs = FieldStrings(l, content, "entryNpcs");
                l.PushValue(content);
                l.GetTable(inList);
                bool placed = l.ToBoolean(-1);
                l.Pop(1);
                if (placed)
                {
                    var (setupEvents, setupEntryNpcs) = BattlefieldFramework.RegisterHooks(earlier, id, entryNpcs);
                    l.GetField(cs, "battlefieldHooks");
                    l.PushValue(content);
                    l.Push(setupEvents);
                    l.Push(setupEntryNpcs);
                    if (mine.TryGetValue(id, out var b)) PushBattlefieldHookFunctions(l, b);
                    else l.PushNil();
                    l.Call(4, 0);
                }
                l.GetField(content, "hasListeners");
                bool hasListeners = l.ToBoolean(-1);
                l.Pop(1);
                earlier.Add((id, entryNpcs, hasListeners));
                l.SetTop(content - 1);
            }
        }

        // t[field] as an integer, or fallback
        private static int FieldIntOr(LuaState l, int t, string field, int fallback)
        {
            l.GetField(t, field);
            int v = l.IsNumber(-1) ? (int)l.ToIntegral(-1) : fallback;
            l.Pop(1);
            return v;
        }

        // t[field] as an array of strings; null for nil
        private static string[] FieldStrings(LuaState l, int t, string field)
        {
            l.GetField(t, field);
            try
            {
                if (!l.IsTable(-1)) return null;
                int n = l.ObjLen(-1);
                var list = new string[n];
                for (int i = 1; i <= n; i++)
                {
                    l.RawGetI(-1, i);
                    list[i - 1] = l.IsString(-1) ? l.ToStringValue(-1) : null;
                    l.Pop(1);
                }
                return list;
            }
            finally
            {
                l.Pop(1);
            }
        }

        // the Lua helpers of the zone hooks, set with the CSharpQuests table (RegisterQuestScriptGlobals)
        private void RegisterBattlefieldScriptGlobals()
        {
            // CSharpQuests.battlefieldZone(list, zoneId, csharp): xi.battlefield.contentsByZone[zoneId] in place, its
            // entries not in the container list first (a reload's earlier ones, as the Lua's own reload leaves them;
            // the C# ids' replaced Lua contents dropped), then the zone's battlefields of the list in list order (the
            // order Battlefield:register ran in); returns it and the set of those from the list
            lua_.DoString(
                "CSharpQuests.battlefieldZone = function(list, zoneId, csharp)\n" +
                "    local inList, ordered = {}, {}\n" +
                "    for _, c in ipairs(list) do\n" +
                "        if type(c) == 'table' and type(c.id) == 'string' and c.battlefieldId ~= nil and\n" +
                "            c.zoneId == zoneId and c.id == string.format('Battlefield[%d]', c.battlefieldId)\n" +
                "        then\n" +
                "            inList[c] = true\n" +
                "            table.insert(ordered, c)\n" +
                "        end\n" +
                "    end\n" +
                "    local byZone = xi.battlefield.contentsByZone[zoneId]\n" +
                "    local result = {}\n" +
                "    for _, c in ipairs(byZone or {}) do\n" +
                "        if not inList[c] and not csharp[c.battlefieldId] then\n" +
                "            table.insert(result, c)\n" +
                "        end\n" +
                "    end\n" +
                "    for _, c in ipairs(ordered) do\n" +
                "        table.insert(result, c)\n" +
                "    end\n" +
                "    if byZone then\n" +
                "        for i = #byZone, 1, -1 do\n" +
                "            byZone[i] = nil\n" +
                "        end\n" +
                "        for i, c in ipairs(result) do\n" +
                "            byZone[i] = c\n" +
                "        end\n" +
                "    else\n" +
                "        byZone = result\n" +
                "        xi.battlefield.contentsByZone[zoneId] = byZone\n" +
                "    end\n" +
                "    return byZone, inList\n" +
                "end", "=CSharpQuests");
            // CSharpQuests.dropBattlefield(list, content): a C# content of the last pass taken out: of
            // contentsByZone, and of contents[id] (the list's content with that id takes its place: a Lua file's, or
            // nothing). A C# battlefield registered again in this pass sets contents[id] after this
            lua_.DoString(
                "CSharpQuests.dropBattlefield = function(list, content)\n" +
                "    local id = content.battlefieldId\n" +
                "    if xi.battlefield.contents[id] == content then\n" +
                "        local replacement = nil\n" +
                "        for _, c in ipairs(list) do\n" +
                "            if type(c) == 'table' and c ~= content and c.battlefieldId == id and\n" +
                "                c.id == string.format('Battlefield[%d]', id)\n" +
                "            then\n" +
                "                replacement = c\n" +
                "            end\n" +
                "        end\n" +
                "        xi.battlefield.contents[id] = replacement\n" +
                "    end\n" +
                "    local byZone = xi.battlefield.contentsByZone[content.zoneId]\n" +
                "    for i = #(byZone or {}), 1, -1 do\n" +
                "        if byZone[i] == content then\n" +
                "            table.remove(byZone, i)\n" +
                "        end\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            // CSharpQuests.battlefieldHooks(self, setupEvents, setupEntryNpcs, h): Battlefield:register's hook
            // setup (scripts/globals/battlefield.lua:514-558) on the content's zone section, with h's functions for
            // a C# content (h nil: a Lua content, the Lua framework's). A Lua content keeps the hooks it already has,
            // gains those it lacks, and loses those it should not have now (the events and its entry NPCs' trade and
            // trigger); its exit NPCs' stay as its register set them
            lua_.DoString(
                "CSharpQuests.battlefieldHooks = function(self, setupEvents, setupEntryNpcs, h)\n" +
                "    local zoneSection = self.sections[1][self.zoneId]\n" +
                "    local onEntryTrade = h and h.onEntryTrade or Battlefield.onEntryTrade\n" +
                "    local onEntryTrigger = h and h.onEntryTrigger or Battlefield.onEntryTrigger\n" +
                "    local redirectEventUpdate = h and h.redirectEventUpdate or Battlefield.redirectEventUpdate\n" +
                "    local redirectEventCall = h and h.redirectEventCall or Battlefield.redirectEventCall\n" +
                "    if setupEvents then\n" +
                "        if h or not self.hasListeners then\n" +
                "            utils.append(zoneSection, {\n" +
                "                onEventUpdate =\n" +
                "                {\n" +
                "                    [32000] = redirectEventUpdate,\n" +
                "                    [32003] = utils.bind(redirectEventCall, 'onExitEventUpdate'),\n" +
                "                },\n" +
                "                onEventFinish =\n" +
                "                {\n" +
                "                    [32000] = utils.bind(redirectEventCall, 'onEventFinishEnter'),\n" +
                "                    [32001] = utils.bind(redirectEventCall, 'onEventFinishWin'),\n" +
                "                    [32002] = utils.bind(redirectEventCall, 'onEventFinishLeave'),\n" +
                "                    [32003] = utils.bind(redirectEventCall, 'onEventFinishExit'),\n" +
                "                    [32004] = utils.bind(redirectEventCall, 'onEventFinishBattlefield'),\n" +
                "                    [32005] = utils.bind(redirectEventCall, 'onEventFinishBattlefield'),\n" +
                "                }\n" +
                "            })\n" +
                "        end\n" +
                "        self.hasListeners = true\n" +
                "    elseif self.hasListeners then\n" +
                "        for _, k in ipairs({ 'onEventUpdate', 'onEventFinish' }) do\n" +
                "            local t = zoneSection[k]\n" +
                "            if t then\n" +
                "                for csid = 32000, 32005 do\n" +
                "                    if k == 'onEventFinish' or csid == 32000 or csid == 32003 then\n" +
                "                        t[csid] = nil\n" +
                "                    end\n" +
                "                end\n" +
                "                if next(t) == nil then\n" +
                "                    zoneSection[k] = nil\n" +
                "                end\n" +
                "            end\n" +
                "        end\n" +
                "        self.hasListeners = nil\n" +
                "    end\n" +
                "    for _, entryNpc in ipairs(self.entryNpcs or {}) do\n" +
                "        local npcSection = zoneSection[entryNpc]\n" +
                "        if setupEntryNpcs then\n" +
                "            if h or npcSection == nil or npcSection.onTrigger ~= onEntryTrigger then\n" +
                "                utils.append(zoneSection, {\n" +
                "                    [entryNpc] =\n" +
                "                    {\n" +
                "                        onTrade   = onEntryTrade,\n" +
                "                        onTrigger = onEntryTrigger,\n" +
                "                    }\n" +
                "                })\n" +
                "            end\n" +
                "        elseif npcSection then\n" +
                "            if npcSection.onTrade == onEntryTrade then\n" +
                "                npcSection.onTrade = nil\n" +
                "            end\n" +
                "            if npcSection.onTrigger == onEntryTrigger then\n" +
                "                npcSection.onTrigger = nil\n" +
                "            end\n" +
                "            if next(npcSection) == nil then\n" +
                "                zoneSection[entryNpc] = nil\n" +
                "            end\n" +
                "        end\n" +
                "    end\n" +
                "    if h and self.exitNpcs then\n" +
                "        for _, exitNpc in ipairs(self.exitNpcs) do\n" +
                "            utils.append(zoneSection, {\n" +
                "                [exitNpc] =\n" +
                "                {\n" +
                "                    onTrigger = h.exitTrigger[exitNpc],\n" +
                "                }\n" +
                "            })\n" +
                "        end\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            // npcUtil.openCrate's callback (IBattlefieldEngine.OpenCrate): a closure over the player and the crate,
            // calling fn(player, npc)
            lua_.DoString(
                "CSharpQuests.bindCrate = function(fn, player, npc)\n" +
                "    return function()\n" +
                "        return fn(player, npc)\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            // fn: one function for every crate, running the callback the C# call left pending on a context over
            // (player, npc)
            lua_.PushFunction(l =>
            {
                var (cb, bound, binding, handler) = pendingCrate_;
                if (cb == null)
                {
                    log_("quests: a C# battlefield crate callback ran with no callback pending; the crate stays shut");
                    l.Push(false);
                    return 1;
                }
                var env = new BattlefieldCall(l, this, log_, bound, binding, handler);
                bool result = cb(new LiveBattlefieldContext(env, 1, 2, 0, 0, 0));
                l.Push(result);
                return 1;
            });
            openCrateCallback_ = lua_.Ref();
            // quest rework C3 PR2: a battlefield callback's closure, as the Lua battlefield's own closure keeps its
            // upvalues: the captured values held by the closure (Lua's to collect once the queue or the listener lets
            // go of it), raw called with (m, the engine's m arguments, the captures)
            lua_.DoString(
                "CSharpQuests.bfCallback = function(raw, ...)\n" +
                "    local n = select('#', ...)\n" +
                "    local captured = { ... }\n" +
                "    return function(...)\n" +
                "        local m = select('#', ...)\n" +
                "        local args = { ... }\n" +
                "        for i = 1, n do\n" +
                "            args[m + i] = captured[i]\n" +
                "        end\n" +
                "        return raw(m, unpack(args, 1, m + n))\n" +
                "    end\n" +
                "end", "=CSharpQuests");
            // CSharpQuests.moduleOverride(name, super, self, player, battlefield): Battlefield[name] as the Lua
            // modules' overrides left it (module_utils.lua's applyOverride keeps each override's super in its env),
            // called on (self, player, battlefield) with the innermost super, the framework's own method, set for the
            // call to a function that runs super (the C# framework) for self and the framework's own method for any
            // other content; restored after, also on an error. False, with nothing run, when no module overrides it
            lua_.DoString(
                "CSharpQuests.moduleOverride = function(name, super, self, player, battlefield)\n" +
                "    local inner = nil\n" +
                "    local f = Battlefield[name]\n" +
                "    while type(f) == 'function' do\n" +
                "        local env = getfenv(f)\n" +
                "        local s = type(env) == 'table' and rawget(env, 'super') or nil\n" +
                "        if type(s) ~= 'function' then\n" +
                "            break\n" +
                "        end\n" +
                "        inner = env\n" +
                "        f = s\n" +
                "    end\n" +
                "    if inner == nil then\n" +
                "        return false\n" +
                "    end\n" +
                "    local own = rawget(inner, 'super')\n" +
                "    rawset(inner, 'super', function(first, ...)\n" +
                "        if first == self then\n" +
                "            return super(first, ...)\n" +
                "        end\n" +
                "        return own(first, ...)\n" +
                "    end)\n" +
                "    local ok, err = pcall(Battlefield[name], self, player, battlefield)\n" +
                "    rawset(inner, 'super', own)\n" +
                "    if not ok then\n" +
                "        error(err, 0)\n" +
                "    end\n" +
                "    return true\n" +
                "end", "=CSharpQuests");
            // super: one function for every override chain, running the body the C# call left pending on a context
            // over (self, player, battlefield)
            lua_.PushFunction(l =>
            {
                var (body, bound, binding, handler) = pendingSuper_;
                if (body == null)
                {
                    log_("quests: a C# battlefield's module super ran with no body pending");
                    return 0;
                }
                var env = new BattlefieldCall(l, this, log_, bound, binding, handler);
                body(new LiveBattlefieldContext(env, 2, 0, 0, 3, 0));
                return 0;
            });
            moduleSuper_ = lua_.Ref();
        }

        // the callback of the npcUtil.openCrate call running now (LiveBattlefieldEngine.OpenCrate), with its call's
        // battlefield, binding and name
        internal (Func<IBattlefieldContext, bool> Callback, BoundBattlefield Bound, QuestScriptBinding Binding,
                  string Handler) pendingCrate_;
        internal LuaRef openCrateCallback_;

        // the framework method whose module override chain runs now (LiveBattlefieldEngine.ModuleOverride): the body
        // its last super runs, with its call's battlefield, binding and name
        internal (Action<IBattlefieldContext> Body, BoundBattlefield Bound, QuestScriptBinding Binding,
                  string Handler) pendingSuper_;
        internal LuaRef moduleSuper_;
    }

    // one C# battlefield of this load
    internal sealed class BoundBattlefield
    {
        public BoundBattlefield(string key, BattlefieldScript script, BattlefieldData data, int id)
        {
            Key = key;
            Script = script;
            Data = data;
            Id = id;
            Binding = new QuestScriptBinding(key, script.Core, "battlefield", id);
        }

        public string Key { get; }
        public BattlefieldScript Script { get; }
        public BattlefieldData Data { get; }
        public int Id { get; }
        public ushort Zone => (ushort)Data.ZoneId;
        // the player context's binding (the battlefield's empty core)
        public QuestScriptBinding Binding { get; }
        // the proxy content table (a registry reference)
        public LuaRef Proxy { get; set; }

        // quest rework C3 PR2: makes a callback's C# function (ZoneHost.BattlefieldCallbackFunction), each once, on
        // its first arming. Kept for the life of this registration, as a quest's (QuestScriptBinding.CallbackRef):
        // the LuaState keeps every C# function it is given, so one per arming would grow it with every timer
        public Func<BattlefieldCallback, LuaRef> MakeCallbackRef { get; set; }
        private Dictionary<BattlefieldCallback, LuaRef> callbackRefs_;

        public LuaRef CallbackRef(BattlefieldCallback cb)
        {
            callbackRefs_ ??= new Dictionary<BattlefieldCallback, LuaRef>(ReferenceEqualityComparer.Instance);
            if (!callbackRefs_.TryGetValue(cb, out var r))
            {
                if (MakeCallbackRef == null)
                    throw new InvalidOperationException($"{Key}: callbacks cannot be armed here");
                callbackRefs_[cb] = r = MakeCallbackRef(cb);
            }
            return r;
        }
    }

    // an action a hook returns: the Lua Event (a registry reference, released when pushed)
    internal sealed class LiveBattlefieldAction : IBattlefieldAction
    {
        private readonly LuaState l_;

        public LiveBattlefieldAction(LuaState l, LuaRef r)
        {
            l_ = l;
            Ref = r;
        }

        public LuaRef Ref { get; }

        // action:setPriority(n): the Lua action keeps its reference, and answers itself
        public IBattlefieldAction SetPriority(int priority)
        {
            int top = l_.Top;
            try
            {
                l_.CheckStack(8);
                l_.PushRef(Ref);
                int obj = l_.Top;
                l_.GetField(obj, "setPriority");
                l_.PushValue(obj);
                l_.Push((double)priority);
                l_.Call(2, 0);
            }
            finally
            {
                l_.SetTop(top);
            }
            return this;
        }
    }

    // a facet entity (a mob or a player facet) as the Lua value on the call's stack: its position; 0 for nil
    internal static class LiveEntity
    {
        public static LuaSlot Slot(IBattlefieldEntity e) => new LuaSlot(e switch
        {
            null => 0,
            LiveBattlefieldMob m => m.Slot,
            LiveQuestContext p => p.PlayerSlot,
            _ => throw new InvalidOperationException("an entity of another bridge"),
        });
    }

    // a Lua value at a position of the call's stack, as an argument
    internal readonly struct LuaSlot
    {
        public LuaSlot(int index) => Index = index;
        public int Index { get; }
    }

    // a BattlefieldLootTable's engine handle: a table at a stack position of one call
    internal sealed class LiveLootTable
    {
        public LiveLootTable(BattlefieldCall env, int slot)
        {
            Env = env;
            Slot = slot;
        }

        public BattlefieldCall Env { get; }
        public int Slot { get; }
    }

    // the call one closure runs: its state, its battlefield and the values it keeps on the stack
    internal sealed class BattlefieldCall
    {
        public BattlefieldCall(LuaState l, ZoneHost host, Action<string> log, BoundBattlefield bound,
                               QuestScriptBinding binding, string handler)
        {
            L = l;
            Host = host;
            Log = log;
            Bound = bound;
            Binding = binding;
            Handler = handler;
        }

        public LuaState L { get; }
        // null in a player context's own calls (LiveQuestContext): no coverage hook, no crate
        public ZoneHost Host { get; }
        public Action<string> Log { get; }
        // null: a zone handler of the framework
        public BoundBattlefield Bound { get; }
        public QuestScriptBinding Binding { get; }
        public string Handler { get; }

        public void Push(object a)
        {
            if (a is LuaSlot s)
            {
                if (s.Index == 0) L.PushNil();
                else L.PushValue(s.Index);
            }
            else L.Push(a);
        }

        // a value pushed and kept: its position
        public int Keep(Action push)
        {
            L.CheckStack(8);
            push();
            return L.Top;
        }

        // the function at path called with args, its one result kept: its position (nil kept too)
        public int KeepCallPath(string path, params object[] args)
        {
            L.CheckStack(args.Length + 8);
            L.GetPath(path);
            foreach (var a in args) Push(a);
            L.Call(args.Length, 1);
            return L.Top;
        }

        // <self>:<method>(args...), its one result kept: its position
        public int KeepMethod(int self, string method, params object[] args)
        {
            L.CheckStack(args.Length + 8);
            L.PushValue(self);
            int obj = L.Top;
            L.GetField(obj, method);
            L.PushValue(obj);
            foreach (var a in args) Push(a);
            L.Call(args.Length + 1, 1);
            L.Remove(obj);
            return L.Top;
        }

        // <self>:<method>(args...), its results read before the stack is restored
        public T Method<T>(int self, string method, int nresults, Func<T> read, params object[] args)
        {
            int top = L.Top;
            try
            {
                L.CheckStack(args.Length + 8);
                L.PushValue(self);
                int obj = L.Top;
                L.GetField(obj, method);
                L.PushValue(obj);
                foreach (var a in args) Push(a);
                L.Call(args.Length + 1, nresults);
                return read();
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // the function at path called with args, its results read before the stack is restored
        public T CallPath<T>(string path, int nresults, Func<T> read, params object[] args)
        {
            int top = L.Top;
            try
            {
                L.CheckStack(args.Length + 8);
                L.GetPath(path);
                foreach (var a in args) Push(a);
                L.Call(args.Length, nresults);
                return read();
            }
            finally
            {
                L.SetTop(top);
            }
        }

        public int ReadInt(int at = -1) => L.IsNumber(at) ? (int)L.ToIntegral(at) : 0;

        // a player at the position: the quest context over it, with the battlefield bindings (over this call: its
        // battlefield is the one its callbacks are armed for)
        public LiveQuestContext Player(int slot) =>
            new LiveQuestContext(L, Log, Binding, Handler, player: slot).InBattlefieldCall(this);

        // quest rework C3 PR2: <self>:<method>(leading..., fn), fn the closure CSharpQuests.bfCallback makes over the
        // callback's C# function and the captures (a facet of this call as its Lua value), so the engine keeps it as
        // it keeps a Lua closure (CLuaBaseEntity::timer, queue, addListener)
        public void ArmCallback(int self, string method, BattlefieldCallback callback, int ms, object[] captures,
                                params object[] leading)
        {
            BattlefieldCallback.CheckArming(Bound?.Script, callback, ms, captures);
            captures ??= Array.Empty<object>();
            int top = L.Top;
            try
            {
                L.CheckStack(captures.Length + leading.Length + 16);
                L.PushValue(self);
                int obj = L.Top;
                L.GetField(obj, method);
                L.PushValue(obj);
                foreach (var a in leading) Push(a);
                L.GetPath("CSharpQuests.bfCallback");
                L.PushRef(Bound.CallbackRef(callback));
                foreach (var c in captures) Push(CaptureArg(c));
                L.Call(1 + captures.Length, 1);
                L.Call(2 + leading.Length, 0);
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // a captured value as an argument: a facet its Lua value, anything else as it is
        private static object CaptureArg(object c) => c switch
        {
            LiveBattlefieldView v => new LuaSlot(v.Slot),
            IBattlefieldEntity e => LiveEntity.Slot(e),
            IBattlefieldView => throw new InvalidOperationException("a battlefield of another bridge"),
            _ => c,
        };

        // the array at the position, each element kept: their positions
        public List<int> KeepArray(int table)
        {
            var list = new List<int>();
            if (!L.IsTable(table)) return list;
            int n = L.ObjLen(table);
            for (int i = 1; i <= n; i++) list.Add(Keep(() => L.RawGetI(table, i)));
            return list;
        }
    }

    // a hook's call over the engine: the call's arguments as facets
    internal sealed class LiveBattlefieldContext : IBattlefieldContext
    {
        private readonly BattlefieldCall env_;
        private readonly int player_, npc_, trade_, bf_, mob_;

        public LiveBattlefieldContext(BattlefieldCall env, int player, int npc, int trade, int bf, int mob)
        {
            env_ = env;
            player_ = player;
            npc_ = npc;
            trade_ = trade;
            bf_ = bf;
            mob_ = mob;
        }

        private bool Has(int at) => at != 0 && !env_.L.IsNil(at);

        public BattlefieldScript Script => env_.Bound?.Script;
        public BattlefieldFramework Framework => Script == null ? null : framework_ ??= new BattlefieldFramework(this);
        private BattlefieldFramework framework_;
        public IBattlefieldContent Self => env_.Bound == null ? null
            : self_ ??= new LiveBattlefieldContent(env_, env_.Keep(PushSelf));
        private LiveBattlefieldContent self_;

        // the content table: this pass's proxy; a reload released the proxy of a pass whose closures still run in a fight
        // (the C++ keeps its sol::function), so that fight reads the content now in xi.battlefield.contents[id]
        private void PushSelf()
        {
            var bound = env_.Bound;
            if (bound.Proxy != null)
            {
                env_.L.PushRef(bound.Proxy);
                return;
            }
            env_.L.GetPath("xi.battlefield.contents");
            env_.L.RawGetI(-1, bound.Id);
            env_.L.Remove(-2);
            if (env_.L.IsNil(-1))
                throw new InvalidOperationException(
                    $"battlefield {bound.Key}: its proxy was released by a reload and xi.battlefield.contents[{bound.Id}] is nil");
        }
        public IBattlefieldText Text => new BattlefieldTextAccessor(this);
        public IBattlefieldPlayer Player => Has(player_) ? playerFacet_ ??= env_.Player(player_) : null;
        private LiveQuestContext playerFacet_;
        public IBattlefieldNpc Npc => Has(npc_) ? new LiveBattlefieldNpc(env_, npc_) : null;
        public IBattlefieldMob Mob => Has(mob_) ? new LiveBattlefieldMob(env_, mob_) : null;
        public IQuestTrade Trade => Has(trade_)
            ? new LiveQuestContext(env_.L, env_.Log, env_.Binding, env_.Handler, player: player_, trade: trade_)
            : null;
        public IBattlefieldView Battlefield => Has(bf_) ? new LiveBattlefieldView(env_, bf_) : null;
        public IBattlefieldEngine Engine => engine_ ??= new LiveBattlefieldEngine(env_);
        private LiveBattlefieldEngine engine_;

        public void Reached(string label)
        {
            if (env_.Bound != null) env_.Host?.BattlefieldReached(env_.Bound.Key, label);
        }

        // the call's argument positions (0: none), for the calls that pass them on
        public LuaSlot PlayerSlot => new LuaSlot(Has(player_) ? player_ : 0);
        public LuaSlot NpcSlot => new LuaSlot(Has(npc_) ? npc_ : 0);
        public LuaSlot TradeSlot => new LuaSlot(Has(trade_) ? trade_ : 0);
        public LuaSlot BattlefieldSlot => new LuaSlot(Has(bf_) ? bf_ : 0);

        // the players table at the position (handleWipe's), each kept
        public IReadOnlyList<IBattlefieldPlayer> PlayersArg(int at) =>
            env_.KeepArray(at).Select(s => (IBattlefieldPlayer)env_.Player(s)).ToList();

        // the mobs of the array argument at the position
        public IReadOnlyList<IBattlefieldMob> MobsAt(int at) =>
            env_.KeepArray(at).Select(s => (IBattlefieldMob)new LiveBattlefieldMob(env_, s)).ToList();

        // the table at the position (handleLootRolls' lootTable) as it is, nil too
        public BattlefieldLootTable LootTableArg(int at) => BattlefieldLootTable.OfEngine(new LiveLootTable(env_, at));
    }

    // an npc (CBaseEntity) at a position
    internal sealed class LiveBattlefieldNpc : IBattlefieldNpc
    {
        private readonly BattlefieldCall env_;

        public LiveBattlefieldNpc(BattlefieldCall env, int slot)
        {
            env_ = env;
            Slot = slot;
        }

        public int Slot { get; }
        public int Id => env_.Method(Slot, "getID", 1, () => env_.ReadInt());
        public string Name => env_.Method(Slot, "getName", 1, () => env_.L.ToStringValue(-1));
        public int TargId => env_.Method(Slot, "getTargID", 1, () => env_.ReadInt());
        public void SetAnimation(int value) => env_.Method(Slot, "setAnimation", 0, () => 0, value);
    }

    // a mob at a position
    internal sealed class LiveBattlefieldMob : IBattlefieldMob
    {
        private readonly BattlefieldCall env_;
        private readonly int slot_;

        public LiveBattlefieldMob(BattlefieldCall env, int slot)
        {
            env_ = env;
            slot_ = slot;
        }

        internal int Slot => slot_;
        public int Id => env_.Method(slot_, "getID", 1, () => env_.ReadInt());
        public int MainLevel => env_.Method(slot_, "getMainLvl", 1, () => env_.ReadInt());
        public void SetMobLevel(int level) => env_.Method(slot_, "setMobLevel", 0, () => 0, level);
        public void MessageText(int textId) =>
            env_.Method(slot_, "messageText", 0, () => 0, new LuaSlot(slot_), textId);

        public void PathThrough(double[] points, int flags)
        {
            int table = env_.Keep(() =>
            {
                env_.L.NewTable();
                for (int i = 0; i < points.Length; i++)
                {
                    env_.L.Push(points[i]);
                    env_.L.RawSetI(-2, i + 1);
                }
            });
            env_.Method(slot_, "pathThrough", 0, () => 0, new LuaSlot(table), flags);
        }

        // the Batch F members, each the binding of its name
        private int I(string method, params object[] args) => env_.Method(slot_, method, 1, () => env_.ReadInt(), args);
        private bool B(string method, params object[] args) =>
            env_.Method(slot_, method, 1, () => env_.L.ToBoolean(-1), args);
        private double N(string method, params object[] args) =>
            env_.Method(slot_, method, 1, () => env_.L.IsNumber(-1) ? env_.L.ToNumber(-1) : 0, args);
        private void D(string method, params object[] args) => env_.Method(slot_, method, 0, () => 0, args);
        private IBattlefieldMob Entity(string method, params object[] args)
        {
            int at = env_.KeepMethod(slot_, method, args);
            return env_.L.IsNil(at) ? null : new LiveBattlefieldMob(env_, at);
        }

        public uint LocalVar(string name) => (uint)N("getLocalVar", name);
        public void SetLocalVar(string name, uint value) => D("setLocalVar", name, (double)value);
        public void ResetLocalVars() => D("resetLocalVars");
        public bool IsSpawned => B("isSpawned");
        public bool IsAlive => B("isAlive");
        public bool IsDead => B("isDead");
        public bool IsEngaged => B("isEngaged");
        public bool IsMob => B("isMob");
        public void Spawn(int? despawnSec = null, int? respawnSec = null) => D("spawn", despawnSec, respawnSec);
        public void SetSpawn(double x, double y, double z, int? rot = null) => D("setSpawn", x, y, z, rot);

        public void SetPos(double x, double y, double z, int? rot = null)
        {
            if (rot == null) D("setPos", x, y, z);
            else D("setPos", x, y, z, rot.Value);
        }

        public (double X, double Y, double Z, int Rot) GetPos() => env_.Method(slot_, "getPos", 1, () =>
        {
            var l = env_.L;
            double Field(string key)
            {
                l.GetField(-1, key);
                double v = l.IsNumber(-1) ? l.ToNumber(-1) : 0;
                l.Pop(1);
                return v;
            }
            return (Field("x"), Field("y"), Field("z"), (int)Field("rot"));
        });

        public void LookAt(double x, double y, double z) => D("lookAt", x, y, z);
        public int HP => I("getHP");
        public void SetHP(int value) => D("setHP", value);
        public int HPP => I("getHPP");
        public int MaxHP => I("getMaxHP");
        public int MaxMP => I("getMaxMP");
        public void SetMP(int value) => D("setMP", value);
        public double TP => N("getTP");
        public void SetTP(int value) => D("setTP", value);
        public IBattlefieldMob GetTarget() => Entity("getTarget");
        public void Engage(int targId) => D("engage", targId);
        public void Disengage() => D("disengage");
        public void UpdateEnmity(IBattlefieldEntity entity) => D("updateEnmity", LiveEntity.Slot(entity));
        public void AddEnmity(IBattlefieldEntity entity, int ce, int ve) =>
            D("addEnmity", LiveEntity.Slot(entity), ce, ve);
        public void ResetEnmity(IBattlefieldEntity entity) => D("resetEnmity", LiveEntity.Slot(entity));

        public IReadOnlyList<BattlefieldEnmityEntry> GetEnmityList()
        {
            var list = new List<BattlefieldEnmityEntry>();
            int table = env_.KeepMethod(slot_, "getEnmityList");
            var l = env_.L;
            foreach (int row in env_.KeepArray(table))
            {
                int entity = env_.Keep(() => l.GetField(row, "entity"));
                var entry = new BattlefieldEnmityEntry { Entity = new LiveBattlefieldMob(env_, entity) };
                l.GetField(row, "ce");
                entry.Ce = env_.ReadInt();
                l.Pop(1);
                l.GetField(row, "ve");
                entry.Ve = env_.ReadInt();
                l.Pop(1);
                l.GetField(row, "active");
                entry.Active = l.ToBoolean(-1);
                l.Pop(1);
                l.GetField(row, "tameable");
                entry.Tameable = l.ToBoolean(-1);
                l.Pop(1);
                list.Add(entry);
            }
            return list;
        }

        public void UseMobAbility() => D("useMobAbility");
        public void UseMobAbility(int skillId) => D("useMobAbility", skillId);
        public void UseMobAbility(int skillId, IBattlefieldEntity target) =>
            D("useMobAbility", skillId, LiveEntity.Slot(target));
        public void SetMobAbilityEnabled(bool state) => D("setMobAbilityEnabled", state);
        public void SetMagicCastingEnabled(bool state) => D("setMagicCastingEnabled", state);
        public void SetAutoAttackEnabled(bool state) => D("setAutoAttackEnabled", state);
        public void SetBehavior(int behavior) => D("setBehavior", behavior);
        public int GetBehavior() => I("getBehavior");
        public void SetMobMod(int mobMod, int value) => D("setMobMod", mobMod, value);
        public void SetUnkillable(bool value) => D("setUnkillable", value);
        public void SetUntargetable(bool value) => D("setUntargetable", value);
        public void SetSpawnAnimation(int value) => D("setSpawnAnimation", value);
        public void SetAnimation(int value) => D("setAnimation", value);
        public void SetStatus(int value) => D("setStatus", value);

        public IBattlefieldView GetBattlefield()
        {
            int at = env_.KeepMethod(slot_, "getBattlefield");
            return env_.L.IsNil(at) ? null : new LiveBattlefieldView(env_, at);
        }

        public int GetBattlefieldID() => I("getBattlefieldID");
        public int Pool => I("getPool");
        public string Name => env_.Method(slot_, "getName", 1, () => env_.L.ToStringValue(-1));
        public int Race => I("getRace");
        public int TargId => I("getTargID");

        // quest rework C3 PR2: the entity's own timer, queue and addListener bindings, with a closure over the callback
        public void Timer(int ms, BattlefieldCallback callback, params object[] captures) =>
            env_.ArmCallback(slot_, "timer", callback, ms, captures, ms);
        public void Queue(int ms, BattlefieldCallback callback, params object[] captures) =>
            env_.ArmCallback(slot_, "queue", callback, ms, captures, ms);

        public void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            env_.ArmCallback(slot_, "addListener", callback, 0, captures, evt, identifier);
        }

        public void RemoveListener(string identifier) =>
            D("removeListener", identifier ?? throw new ArgumentNullException(nameof(identifier)));
    }

    // quest rework C3 PR2: a battlefield callback's arguments on its own call's stack: the engine's at first ..
    // first + count - 1, the captures at capture .. capture + captureCount - 1
    internal sealed class LiveBattlefieldCallbackArgs : IBattlefieldCallbackArgs
    {
        private readonly BattlefieldCall env_;
        private readonly int first_, capture_;

        public LiveBattlefieldCallbackArgs(BattlefieldCall env, int count, int first, int capture, int captureCount)
        {
            env_ = env;
            ArgCount = count;
            first_ = first;
            capture_ = capture;
            CaptureCount = captureCount;
        }

        public int ArgCount { get; }
        public int CaptureCount { get; }
        public IBattlefieldMob Entity => EntityArg(1);

        private int ArgSlot(int index) => index >= 1 && index <= ArgCount ? first_ + index - 1 : 0;

        private int CaptureSlot(int index) =>
            index >= 0 && index < CaptureCount ? capture_ + index
                : throw new ArgumentOutOfRangeException(nameof(index), index, $"{CaptureCount} captured");

        private IBattlefieldMob MobAt(int slot) =>
            slot == 0 || env_.L.IsNil(slot) ? null : new LiveBattlefieldMob(env_, slot);

        public IBattlefieldMob EntityArg(int index) => MobAt(ArgSlot(index));

        public IBattlefieldView CapturedBattlefield(int index)
        {
            int slot = CaptureSlot(index);
            return env_.L.IsNil(slot) ? null : new LiveBattlefieldView(env_, slot);
        }

        public IBattlefieldMob CapturedEntity(int index) => MobAt(CaptureSlot(index));

        public int CapturedInt(int index)
        {
            int slot = CaptureSlot(index);
            return env_.L.IsNumber(slot) ? (int)env_.L.ToIntegral(slot) : 0;
        }
    }

    // a status effect (CStatusEffect) at a position
    internal sealed class LiveBattlefieldEffect : IBattlefieldEffect
    {
        private readonly BattlefieldCall env_;

        public LiveBattlefieldEffect(BattlefieldCall env, int slot)
        {
            env_ = env;
            Slot = slot;
        }

        public int Slot { get; }
        public int Power => env_.Method(Slot, "getPower", 1, () => env_.ReadInt());
    }

    // a battlefield (CLuaBattlefield) at a position: each member the binding of its name
    internal sealed class LiveBattlefieldView : IBattlefieldView
    {
        private readonly BattlefieldCall env_;

        public LiveBattlefieldView(BattlefieldCall env, int slot)
        {
            env_ = env;
            Slot = slot;
        }

        public int Slot { get; }
        private int Int(string method, params object[] args) =>
            env_.Method(Slot, method, 1, () => env_.ReadInt(), args);
        private void Do(string method, params object[] args) => env_.Method(Slot, method, 0, () => 0, args);

        public int Id => Int("getID");
        public int ZoneId => Int("getZoneID");
        public int Area => Int("getArea");
        public int TimeLimit => Int("getTimeLimit");
        public int TimeInside => Int("getTimeInside");
        public int RemainingTime => Int("getRemainingTime");
        public int WipeTime => Int("getWipeTime");
        public void SetWipeTime(int seconds) => Do("setWipeTime", seconds);
        public int MaxParticipants => Int("getMaxParticipants");
        public int PlayerCount => Int("getPlayerCount");

        public IReadOnlyList<IBattlefieldPlayer> Players()
        {
            int table = env_.KeepMethod(Slot, "getPlayers");
            return env_.KeepArray(table).Select(s => (IBattlefieldPlayer)env_.Player(s)).ToList();
        }

        public IReadOnlyList<IBattlefieldMob> Mobs(bool required, bool additional)
        {
            int table = env_.KeepMethod(Slot, "getMobs", required, additional);
            return env_.KeepArray(table).Select(s => (IBattlefieldMob)new LiveBattlefieldMob(env_, s)).ToList();
        }

        public (string Name, int ClearTime, int PartySize) Record() => env_.Method(Slot, "getRecord", 3, () =>
            (env_.L.IsString(-3) ? env_.L.ToStringValue(-3) : null, env_.ReadInt(-2), env_.ReadInt(-1)));
        public void SetRecord(string name, int clearTime, int partySize) => Do("setRecord", name, clearTime, partySize);
        public int Status => Int("getStatus");
        public void SetStatus(int status) => Do("setStatus", status);
        public double LocalVar(string name) =>
            env_.Method(Slot, "getLocalVar", 1, () => env_.L.IsNumber(-1) ? env_.L.ToNumber(-1) : 0, name);
        public void SetLocalVar(string name, double value) => Do("setLocalVar", name, value);
        public int LastTimeUpdate => Int("getLastTimeUpdate");
        public void SetLastTimeUpdate(int seconds) => Do("setLastTimeUpdate", seconds);
        public void SetTimeLimit(int seconds) => Do("setTimeLimit", seconds);
        public (int Id, string Name) Initiator() => env_.Method(Slot, "getInitiator", 2, () =>
            (env_.ReadInt(-2), env_.L.IsString(-1) ? env_.L.ToStringValue(-1) : null));
        public int ArmouryCrate => Int("getArmouryCrate");
        public IBattlefieldMob InsertEntity(int targId, bool ally, bool inBattlefield)
        {
            int at = env_.KeepMethod(Slot, "insertEntity", targId, ally, inBattlefield);
            return env_.L.IsNil(at) ? null : new LiveBattlefieldMob(env_, at);
        }

        public IReadOnlyList<IBattlefieldMob> Allies()
        {
            int table = env_.KeepMethod(Slot, "getAllies");
            return env_.KeepArray(table).Select(s => (IBattlefieldMob)new LiveBattlefieldMob(env_, s)).ToList();
        }
        public void Cleanup(bool cleanup) => Do("cleanup", cleanup);
        public void Win() => Do("win");
        public void Lose() => Do("lose");

        // battlefield:addGroups(content.groups, hasMultipleAreas)
        public void AddGroups(IBattlefieldContent content, bool hasMultipleAreas)
        {
            var c = (LiveBattlefieldContent)content;
            int groups = env_.Keep(() => env_.L.GetField(c.Slot, "groups"));
            Do("addGroups", new LuaSlot(groups), hasMultipleAreas);
        }
    }

    // a content table at a position, Lua or a C# proxy: its fields and methods as the framework reads them
    internal sealed class LiveBattlefieldContent : IBattlefieldContent, IBattlefieldPaths
    {
        private readonly BattlefieldCall env_;

        public LiveBattlefieldContent(BattlefieldCall env, int slot)
        {
            env_ = env;
            Slot = slot;
        }

        public int Slot { get; }
        private LuaState L => env_.L;

        // t[field] read by read, the stack restored
        private T Field<T>(string field, Func<T> read)
        {
            int top = L.Top;
            try
            {
                L.GetField(Slot, field);
                return read();
            }
            finally
            {
                L.SetTop(top);
            }
        }

        public int BattlefieldId => Field("battlefieldId", () => env_.ReadInt());
        public int Index => Field("index", () => env_.ReadInt());
        public bool Experimental => Field("experimental", () => L.ToBoolean(-1));
        // content.loot, one table kept on the stack of this call: read twice it is one table, as the fake's
        public BattlefieldLootTable Loot => loot_ ??=
            BattlefieldLootTable.OfEngine(new LiveLootTable(env_, env_.Keep(() => L.GetField(Slot, "loot"))));
        private BattlefieldLootTable loot_;
        public IReadOnlyList<string> EntryNpcs => Field("entryNpcs", () =>
        {
            if (!L.IsTable(-1)) return (IReadOnlyList<string>)null;
            var list = new List<string>();
            int n = L.ObjLen(-1);
            for (int i = 1; i <= n; i++)
            {
                L.RawGetI(-1, i);
                list.Add(L.IsString(-1) ? L.ToStringValue(-1) : null);
                L.Pop(1);
            }
            return list;
        });
        public int RequiredItemCount => Field("requiredItems", () => L.IsTable(-1) ? L.ObjLen(-1) : 0);
        public int FirstRequiredItem => Field("requiredItems", () =>
        {
            L.RawGetI(-1, 1);
            return env_.ReadInt();
        });
        public int? WornMessage => Field("requiredItems", () =>
        {
            L.GetField(-1, "wornMessage");
            return L.IsNumber(-1) ? (int?)L.ToIntegral(-1) : null;
        });
        public int[] WornMessageTable => Field("requiredItems", () =>
        {
            L.GetField(-1, "wornMessage");
            if (!L.IsTable(-1)) return null;
            int n = L.ObjLen(-1);
            var values = new int[n];
            for (int i = 1; i <= n; i++)
            {
                L.RawGetI(-1, i);
                values[i - 1] = env_.ReadInt();
                L.Pop(1);
            }
            return values;
        });

        // npcUtil.tradeHas(trade, content.tradeItems)
        public bool TradeHas(IQuestTrade trade)
        {
            int tradeSlot = ((LiveQuestContext)trade).TradeSlot;
            int items = env_.Keep(() => L.GetField(Slot, "tradeItems"));
            return env_.CallPath("npcUtil.tradeHas", 1, () => L.ToBoolean(-1), new LuaSlot(tradeSlot),
                                 new LuaSlot(items));
        }

        public bool HasArmouryCrates => Field("armouryCrates", () => L.IsTable(-1));
        public int? ArmouryCrate(int area) => Field("armouryCrates", () =>
        {
            if (!L.IsTable(-1)) return null;
            L.RawGetI(-1, area);
            return L.IsNumber(-1) ? (int?)L.ToIntegral(-1) : null;
        });

        // content:checkRequirements(player, npc, isRegistrant[, trade])
        public bool CheckRequirements(IBattlefieldContext q, bool isRegistrant, bool passTrade)
        {
            var c = (LiveBattlefieldContext)q;
            return passTrade
                ? env_.Method(Slot, "checkRequirements", 1, () => L.ToBoolean(-1), c.PlayerSlot, c.NpcSlot,
                              isRegistrant, c.TradeSlot)
                : env_.Method(Slot, "checkRequirements", 1, () => L.ToBoolean(-1), c.PlayerSlot, c.NpcSlot,
                              isRegistrant);
        }

        // content:onEntryEventUpdate(player, csid, option, npc)
        public void OnEntryEventUpdate(IBattlefieldContext q, int csid, int option)
        {
            var c = (LiveBattlefieldContext)q;
            env_.Method(Slot, "onEntryEventUpdate", 0, () => 0, c.PlayerSlot, csid, option, c.NpcSlot);
        }

        // content[eventName](content, player, csid, option)
        public void Call(string eventName, IBattlefieldContext q, int csid, int option)
        {
            var c = (LiveBattlefieldContext)q;
            env_.Method(Slot, eventName, 0, () => 0, c.PlayerSlot, csid, option);
        }

        // a C# battlefield's proxy (its filename csharp:<key>) reports its label
        public void Reached(string label)
        {
            string file = Field("filename", () => L.IsString(-1) ? L.ToStringValue(-1) : null);
            if (file != null && file.StartsWith("csharp:", StringComparison.Ordinal))
                env_.Host?.BattlefieldReached(file.Substring("csharp:".Length), label);
        }

        // pairs(self.paths): each mob id with its points
        public IEnumerable<(int MobId, double[] Points)> ResolvedPaths()
        {
            var list = new List<(int, double[])>();
            int top = L.Top;
            try
            {
                L.GetField(Slot, "paths");
                if (!L.IsTable(-1)) return list;
                int paths = L.Top;
                L.PushNil();
                while (L.Next(paths))
                {
                    int id = (int)L.ToIntegral(-2);
                    int n = L.IsTable(-1) ? L.ObjLen(-1) : 0;
                    var points = new double[n];
                    for (int i = 1; i <= n; i++)
                    {
                        L.RawGetI(-1, i);
                        points[i - 1] = L.ToNumber(-1);
                        L.Pop(1);
                    }
                    list.Add((id, points));
                    L.Pop(1);
                }
                return list;
            }
            finally
            {
                L.SetTop(top);
            }
        }
    }

    // the Lua globals the framework calls
    internal sealed class LiveBattlefieldEngine : IBattlefieldEngine
    {
        private readonly BattlefieldCall env_;

        public LiveBattlefieldEngine(BattlefieldCall env) => env_ = env;

        private LuaState L => env_.L;
        private static LuaSlot SlotOf(IBattlefieldPlayer p) =>
            new LuaSlot(p == null ? 0 : ((LiveQuestContext)p).PlayerSlot);

        public int? Text(int zone, string name) => ZoneHost.ZoneTextId(L, zone, name);

        public IBattlefieldContent Content(int battlefieldId)
        {
            int at = env_.Keep(() =>
            {
                L.GetPath("xi.battlefield.contents");
                L.Push((double)battlefieldId);
                L.GetTable(-2);
                L.Remove(-2);
            });
            return L.IsTable(at) ? new LiveBattlefieldContent(env_, at) : null;
        }

        public IReadOnlyList<IBattlefieldContent> ZoneContents(int zone)
        {
            int at = env_.Keep(() =>
            {
                L.GetPath("xi.battlefield.contentsByZone");
                L.Push((double)zone);
                L.GetTable(-2);
                L.Remove(-2);
            });
            if (!L.IsTable(at)) return null;
            return env_.KeepArray(at).Select(s => (IBattlefieldContent)new LiveBattlefieldContent(env_, s)).ToList();
        }

        public int MaxAreas(int zone) => env_.CallPath("xi.battlefield.getMaxAreas", 1, () => env_.ReadInt(), zone);

        // xi.battlefield.itemUses[item]
        public int? ItemUses(int item)
        {
            int top = L.Top;
            try
            {
                L.GetPath("xi.battlefield.itemUses");
                if (!L.IsTable(-1)) return null;
                L.Push((double)item);
                L.GetTable(-2);
                return L.IsNumber(-1) ? (int?)L.ToIntegral(-1) : null;
            }
            finally
            {
                L.SetTop(top);
            }
        }

        public bool SendTimePrompts(IBattlefieldView battlefield, IReadOnlyList<IBattlefieldPlayer> players)
        {
            int table = env_.Keep(() =>
            {
                L.NewTable();
                for (int i = 0; i < players.Count; i++)
                {
                    L.PushValue(((LiveQuestContext)players[i]).PlayerSlot);
                    L.RawSetI(-2, i + 1);
                }
            });
            return env_.CallPath("xi.battlefield.SendTimePrompts", 1, () => L.ToBoolean(-1),
                                 new LuaSlot(((LiveBattlefieldView)battlefield).Slot), new LuaSlot(table));
        }

        public int BattlefieldOptions(IBattlefieldContext q, bool passTrade)
        {
            var c = (LiveBattlefieldContext)q;
            return passTrade
                ? env_.CallPath("xi.battlefield.getBattlefieldOptions", 1, () => env_.ReadInt(), c.PlayerSlot,
                                c.NpcSlot, c.TradeSlot)
                : env_.CallPath("xi.battlefield.getBattlefieldOptions", 1, () => env_.ReadInt(), c.PlayerSlot,
                                c.NpcSlot);
        }

        public bool QuestEvent(IBattlefieldContext q, string key, int csid, int option)
        {
            var c = (LiveBattlefieldContext)q;
            return env_.CallPath("xi.cs.questEvent", 1, () => L.ToBoolean(-1), c.PlayerSlot, c.NpcSlot, key,
                                 (double)csid, (double)option);
        }

        public bool RejectLevelSyncedParty(IBattlefieldContext q)
        {
            var c = (LiveBattlefieldContext)q;
            return env_.CallPath("xi.battlefield.rejectLevelSyncedParty", 1, () => L.ToBoolean(-1), c.PlayerSlot,
                                 c.NpcSlot);
        }

        public IBattlefieldNpc NpcById(int id)
        {
            int at = env_.KeepCallPath("GetNPCByID", id);
            return L.IsNil(at) ? null : new LiveBattlefieldNpc(env_, at);
        }

        public IBattlefieldMob MobById(int id)
        {
            int at = env_.KeepCallPath("GetMobByID", id);
            return L.IsNil(at) ? null : new LiveBattlefieldMob(env_, at);
        }

        public int? ZoneMobId(int zone, string key) => ZoneEntryId(zone, "mob", key);
        public int? ZoneNpcId(int zone, string key) => ZoneEntryId(zone, "npc", key);

        // zones[zone][kind][key] as a number, null for nil
        private int? ZoneEntryId(int zone, string kind, string key) => ZoneHost.ZoneEntryId(L, zone, kind, key);

        private static LuaSlot NpcSlot(IBattlefieldNpc npc) => new LuaSlot(((LiveBattlefieldNpc)npc).Slot);

        public void ShowCrate(IBattlefieldNpc crate) => env_.CallPath("npcUtil.showCrate", 0, () => 0, NpcSlot(crate));

        // crate:addListener('ON_TRIGGER', 'TRIGGER_CRATE', utils.bind(self.handleOpenArmouryCrate, self))
        public void AddCrateListener(IBattlefieldNpc crate, IBattlefieldContent self)
        {
            int content = ((LiveBattlefieldContent)self).Slot;
            int method = env_.Keep(() => L.GetField(content, "handleOpenArmouryCrate"));
            int bound = env_.KeepCallPath("utils.bind", new LuaSlot(method), new LuaSlot(content));
            env_.Method(((LiveBattlefieldNpc)crate).Slot, "addListener", 0, () => 0, "ON_TRIGGER", "TRIGGER_CRATE",
                        new LuaSlot(bound));
        }

        // npcUtil.openCrate(crate, callback): the callback a Lua closure over the player and the crate
        // (CSharpQuests.bindCrate) calling the one C# callback function, which runs this call's callback on a context
        // over them (the callback's frame cannot read this one's stack)
        public void OpenCrate(IBattlefieldContext q, IBattlefieldNpc crate, Func<IBattlefieldContext, bool> callback)
        {
            var host = env_.Host ?? throw new InvalidOperationException("no crate outside a battlefield hook");
            var c = (LiveBattlefieldContext)q;
            var outer = host.pendingCrate_;
            host.pendingCrate_ = (callback, env_.Bound, env_.Binding, env_.Handler);
            try
            {
                int fn = env_.KeepCallPath("CSharpQuests.bindCrate", host.openCrateCallback_, c.PlayerSlot,
                                           NpcSlot(crate));
                env_.CallPath("npcUtil.openCrate", 0, () => 0, NpcSlot(crate), new LuaSlot(fn));
            }
            finally
            {
                host.pendingCrate_ = outer;
            }
        }

        // Battlefield.<method> under the Lua modules' overrides: CSharpQuests.moduleOverride on q's content, player
        // and battlefield, its last super the host's one function, which runs body (pending for the call)
        public bool ModuleOverride(string method, IBattlefieldContext q, Action<IBattlefieldContext> body)
        {
            var host = env_.Host ??
                       throw new InvalidOperationException("no module override outside a battlefield hook");
            var c = (LiveBattlefieldContext)q;
            var self = (LiveBattlefieldContent)q.Self;
            var outer = host.pendingSuper_;
            host.pendingSuper_ = (body, env_.Bound, env_.Binding, env_.Handler);
            try
            {
                return env_.CallPath("CSharpQuests.moduleOverride", 1, () => L.ToBoolean(-1), method,
                                     host.moduleSuper_, new LuaSlot(self.Slot), c.PlayerSlot, c.BattlefieldSlot);
            }
            finally
            {
                host.pendingSuper_ = outer;
            }
        }

        // utils.selectFromLootGroups(player, lootTable): a table on this call's stack as it is, C# groups as a new
        // table
        public IReadOnlyList<(int ItemId, double? Amount)> SelectFromLootGroups(IBattlefieldPlayer player,
                                                                                BattlefieldLootTable lootTable)
        {
            int loot;
            if (lootTable.Groups != null) loot = env_.Keep(() => ZoneHost.PushLootGroups(L, lootTable.Groups));
            else if (lootTable.Handle is LiveLootTable t && t.Env == env_) loot = t.Slot;
            else throw new InvalidOperationException("a loot table of another call");
            int selected = env_.KeepCallPath("utils.selectFromLootGroups", SlotOf(player), new LuaSlot(loot));
            var list = new List<(int, double?)>();
            foreach (int entry in env_.KeepArray(selected))
            {
                L.GetField(entry, "itemId");
                int itemId = env_.ReadInt();
                L.Pop(1);
                L.GetField(entry, "amount");
                double? amount = L.IsNumber(-1) ? L.ToNumber(-1) : null;
                L.Pop(1);
                list.Add((itemId, amount));
            }
            return list;
        }

        public void GiveCurrency(IBattlefieldPlayer player, string currency, double amount, bool silent) =>
            env_.CallPath("npcUtil.giveCurrency", 0, () => 0, SlotOf(player), currency, amount, silent);

        public bool BcnmEnableExperimental => MapFlag("BCNM_ENABLE_EXPERIMENTAL");

        // xi.settings.map[name] as Lua truth
        private bool MapFlag(string name)
        {
            int top = L.Top;
            try
            {
                L.GetPath("xi.settings.map." + name);
                return L.ToBoolean(-1);
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // Battlefield:event(csid, args...)
        public IBattlefieldAction Event(int csid, params int[] args)
        {
            int top = L.Top;
            try
            {
                L.GetGlobal("Battlefield");
                int cls = L.Top;
                L.GetField(cls, "event");
                L.PushValue(cls);
                L.Push((double)csid);
                foreach (int a in args) L.Push((double)a);
                L.Call(args.Length + 2, 1);
                return new LiveBattlefieldAction(L, L.Ref());
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // Battlefield:progressOptionalCutscene(32003, { cs_option = 3, canSkip = true })
        public IBattlefieldAction ExitCutscene()
        {
            int top = L.Top;
            try
            {
                L.GetGlobal("Battlefield");
                int cls = L.Top;
                L.GetField(cls, "progressOptionalCutscene");
                L.PushValue(cls);
                L.Push(32003.0);
                L.NewTable();
                L.Push(3.0);
                L.SetField(-2, "cs_option");
                L.Push(true);
                L.SetField(-2, "canSkip");
                L.Call(3, 1);
                return new LiveBattlefieldAction(L, L.Ref());
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // Battlefield:progressEvent(csid, args...): Container:progressEvent, Event:new(...):progress()
        public IBattlefieldAction ProgressEvent(int csid, params int[] args)
        {
            int top = L.Top;
            try
            {
                L.CheckStack(args.Length + 8);
                L.GetGlobal("Battlefield");
                int cls = L.Top;
                L.GetField(cls, "progressEvent");
                L.PushValue(cls);
                L.Push((double)csid);
                foreach (int a in args) L.Push((double)a);
                L.Call(args.Length + 2, 1);
                return new LiveBattlefieldAction(L, L.Ref());
            }
            finally
            {
                L.SetTop(top);
            }
        }

        // SpawnMob(id): the mob, nil kept as null
        public IBattlefieldMob SpawnMob(int id)
        {
            int at = env_.KeepCallPath("SpawnMob", id);
            return L.IsNil(at) ? null : new LiveBattlefieldMob(env_, at);
        }

        public void DespawnMob(int id) => env_.CallPath("DespawnMob", 0, () => 0, id);

        public IBattlefieldPlayer GetPlayerByID(int id)
        {
            int at = env_.KeepCallPath("GetPlayerByID", id);
            return L.IsNil(at) ? null : env_.Player(at);
        }

        public uint GetSystemTime() => env_.CallPath("GetSystemTime", 1, () => (uint)L.ToNumber(-1));
        public int RandomInt(int lower, int upper) =>
            env_.CallPath("math.randomInt", 1, () => (int)L.ToIntegral(-1), (double)lower, (double)upper);
        // the values as a Lua array, shuffled by utils.shuffle, read back
        public IReadOnlyList<int> Shuffle(IReadOnlyList<int> values)
        {
            int table = env_.Keep(() =>
            {
                L.NewTable(values.Count, 0);
                for (int i = 0; i < values.Count; i++)
                {
                    L.Push((double)values[i]);
                    L.RawSetI(-2, i + 1);
                }
            });
            int shuffled = env_.KeepCallPath("utils.shuffle", new LuaSlot(table));
            return env_.KeepArray(shuffled).Select(at => (int)L.ToIntegral(at)).ToList();
        }

        public bool IsEntityBusy(IBattlefieldMob mob) =>
            env_.CallPath("xi.combat.behavior.isEntityBusy", 1, () => L.ToBoolean(-1), LiveEntity.Slot(mob));

        // quest rework C3 PR2: the entity object the facet's Lua userdata stands for, kept as a Lua table keeps the
        // userdata; read back by pushing that object, which the bindings take as the userdata
        public BattlefieldKeptEntity Keep(IBattlefieldEntity entity)
        {
            if (entity == null) return null;
            int slot = LiveEntity.Slot(entity).Index;
            return new BattlefieldKeptEntity(L.ToObject(slot) ??
                                             throw new InvalidOperationException("the entity is not an engine object"));
        }

        public IBattlefieldMob Entity(BattlefieldKeptEntity kept)
        {
            if (kept == null) return null;
            int at = env_.Keep(() => L.Push(kept.Handle));
            return new LiveBattlefieldMob(env_, at);
        }
    }

    // the player bindings the battlefield framework calls (quest rework C2), over the context's player
    internal sealed partial class LiveQuestContext : IBattlefieldPlayer
    {
        // the player's and the trade's positions
        internal int PlayerSlot => player_;
        internal int TradeSlot => trade_;

        // player:<method>(args...), an argument a LuaSlot for a Lua value at a position
        private T P<T>(string method, Func<T> read, params object[] args)
        {
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            return BfEnv.Method(player_, method, 1, read, args);
        }

        private void PDo(string method, params object[] args) => P(method, () => 0, args);
        private int PInt(string method, params object[] args) => P(method, () => (int)lua_.ToIntegral(-1), args);

        // a battlefield call's helpers over this context's state (the kept values of BattlefieldCall)
        private BattlefieldCall BfEnv => bfEnv_ ??= new BattlefieldCall(lua_, null, log_, null, quest_, handler_);
        private BattlefieldCall bfEnv_;

        // player:<method>(args...) with its result kept: its position
        private int PKeep(string method, params object[] args)
        {
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            return BfEnv.KeepMethod(player_, method, args);
        }

        public int ZoneId => PInt("getZoneID");
        public int EntityStatus => PInt("getStatus");
        public bool IsAlive => P("isAlive", () => lua_.ToBoolean(-1));
        public int GMLevel => PInt("getGMLevel");
        public int VisibleGMLevel => PInt("getVisibleGMLevel");

        public IReadOnlyList<IBattlefieldPlayer> AllianceList()
        {
            int table = PKeep("getAlliance");
            return BfEnv.KeepArray(table)
                .Select(s => (IBattlefieldPlayer)new LiveQuestContext(lua_, log_, quest_, handler_, player: s)
                    .InBattlefieldCall(BfEnv))
                .ToList();
        }

        private LiveBattlefieldView ViewAt(int at) => lua_.IsNil(at) ? null : new LiveBattlefieldView(BfEnv, at);

        public IBattlefieldView CurrentBattlefield => ViewAt(PKeep("getBattlefield"));
        public IBattlefieldView RegisteredBattlefield => ViewAt(PKeep("getRegisteredBattlefield"));

        public IBattlefieldEffect StatusEffect(int effect)
        {
            int at = PKeep("getStatusEffect", effect);
            return lua_.IsNil(at) ? null : new LiveBattlefieldEffect(BfEnv, at);
        }

        public void CopyStatusEffect(IBattlefieldEffect effect) =>
            PDo("copyStatusEffect", new LuaSlot(effect == null ? 0 : ((LiveBattlefieldEffect)effect).Slot));

        public int RegisterBattlefield(int battlefieldId, int area, int initiatorId, IBattlefieldContent content) =>
            PInt("registerBattlefield", battlefieldId, area, initiatorId,
                 new LuaSlot(((LiveBattlefieldContent)content).Slot));

        public bool BattlefieldAtCapacity(int battlefieldId) =>
            P("battlefieldAtCapacity", () => lua_.ToBoolean(-1), battlefieldId);
        public void EnterBattlefield() => PDo("enterBattlefield");
        public void LeaveBattlefield(int leaveCode) => PDo("leaveBattlefield", leaveCode);
        public void SetEnteredBattlefield(bool value) => PDo("setEnteredBattlefield", value);
        public void MessageBasic(int messageId, params int[] args) =>
            PDo("messageBasic", new object[] { messageId }.Concat(args.Cast<object>()).ToArray());
        public void MessageSpecial(int textId, params int[] args) =>
            PDo("messageSpecial", new object[] { textId }.Concat(args.Cast<object>()).ToArray());
        public void MessageText(IBattlefieldNpc npc, int textId, bool showName) =>
            PDo("messageText", new LuaSlot(npc == null ? 0 : ((LiveBattlefieldNpc)npc).Slot), textId, showName);
        public void ShowText(IBattlefieldMob mob, int textId) =>
            PDo("showText", new LuaSlot(((LiveBattlefieldMob)mob).Slot), textId);
        public void UpdateEventValues(params object[] args) => PDo("updateEvent", args);
        public void UpdateEventString(string s) => PDo("updateEventString", s);
        public int WornUses(int item) => PInt("getWornUses", item);
        public int IncrementItemWear(int item) => PInt("incrementItemWear", item);
        public void TradeComplete() => PDo("tradeComplete");
        public int PlayerVar(string name) => PInt("getVar", name);
        public void SetPlayerVar(string name, int value) => PDo("setVar", name, value);

        public void StartEventTable(int csid, int[] table)
        {
            int at = BfEnv.Keep(() =>
            {
                lua_.NewTable();
                for (int i = 0; i < table.Length; i++)
                {
                    lua_.Push((double)table[i]);
                    lua_.RawSetI(-2, i + 1);
                }
            });
            PDo("startEvent", csid, new LuaSlot(at));
        }

        public void DelStatusEffectsByFlag(int flag) => PDo("delStatusEffectsByFlag", flag);
        public void PrintToPlayer(string message, int channel) => PDo("printToPlayer", message, channel);
        // player:addTreasure(item, npc); no npc: nil (the binding's addTreasure(item))
        public void AddTreasure(int item, IBattlefieldNpc npc) =>
            PDo("addTreasure", item, new LuaSlot(npc == null ? 0 : ((LiveBattlefieldNpc)npc).Slot));

        public IReadOnlyList<IBattlefieldPlayer> Party()
        {
            int table = PKeep("getParty");
            return BfEnv.KeepArray(table)
                .Select(s => (IBattlefieldPlayer)new LiveQuestContext(lua_, log_, quest_, handler_, player: s)
                    .InBattlefieldCall(BfEnv))
                .ToList();
        }

        public IBattlefieldMob Pet()
        {
            int at = PKeep("getPet");
            return lua_.IsNil(at) ? null : new LiveBattlefieldMob(BfEnv, at);
        }

        public int TargId => PInt("getTargID");
        public int MaxHP => PInt("getMaxHP");
        public int MaxMP => PInt("getMaxMP");
        public void SetTP(int value) => PDo("setTP", value);
        public bool IsDead => P("isDead", () => lua_.ToBoolean(-1));
        public void AllowSendRaisePrompt() => PDo("allowSendRaisePrompt");
        public void SendEntityUpdateToPlayer(IBattlefieldMob entity, int entityUpdate, int updateMask) =>
            PDo("sendEntityUpdateToPlayer", LiveEntity.Slot(entity), entityUpdate, updateMask);

        // this player read in a battlefield call: its helpers are that call's (its battlefield, its zone host)
        internal LiveQuestContext InBattlefieldCall(BattlefieldCall env)
        {
            bfEnv_ = env;
            return this;
        }

        // quest rework C3 PR2: player:addListener(evt, identifier, <a closure over the battlefield callback>)
        public void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            if (player_ == 0 || lua_.IsNil(player_)) throw new InvalidOperationException("no player in this handler");
            BfEnv.ArmCallback(player_, "addListener", callback, 0, captures, evt, identifier);
        }
    }
}
