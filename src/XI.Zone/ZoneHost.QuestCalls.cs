using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XI.Persistence;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Script;

namespace XI.Zone
{
    // Quest rework B3a (spec 2026-10-21-quest-rework-b3a-design.md, section 3): quests that live as `if` branches inside NPC
    // scripts. Such a quest is a C# QuestScript whose NPC handlers are Called(); the NPC script keeps its own order and
    // fallbacks and calls it at the branch's spot, xi.cs.quest(player, npc, key, 'onTrigger' | 'onTrade'[, trade]) -> boolean
    // and xi.cs.questEvent(player, npc, key, csid, option). The reply is rendered with the plain player:* calls an NPC script
    // writes, and Begin / Complete are player:addQuest / npcUtil.completeQuest (LiveQuestContext, called mode).
    // B3b prep adds the mob and zone scripts' calls: xi.cs.quest(player, mob, key, 'onMobDeath'[, optParams]) -> boolean (a
    // Called() OnMobDeath ran; optParams, the mob script's own third argument, is the handler's q.MobDeath: quest rework P7), xi.cs.questEvent(player, npc | nil, key, csid, option[, 'update']) -> boolean (a handler ran; 'update'
    // runs OnEventUpdate) and xi.cs.questZoneIn(player, key, prevZone) -> csid | nil (the zone's onZoneIn returns it, which
    // starts the cutscene). State-level handlers of a call-only quest are the called ones.
    // Quest rework C2 (spec 2026-11-05-quest-rework-c2-mission-platform-design.md, section 3.4) adds the missions'
    // calls in the same shape: xi.cs.mission(player, npc | mob, key, kind[, trade | optParams]),
    // xi.cs.missionEvent(player, npc, key, csid, option[, 'update']) and xi.cs.missionZoneIn(player, key, prevZone).
    // One dispatch serves both kinds: a quest's states match on its status and step, a mission's (IsMissionMatch) on
    // the log's current mission and status. Each call takes only its own kind's keys.
    public sealed partial class ZoneHost
    {
        // key -> binding of the quests with Called() handlers, rebuilt by every CSharpQuests.register
        private readonly Dictionary<string, QuestScriptBinding> npcCallQuests_ = new Dictionary<string, QuestScriptBinding>(StringComparer.Ordinal);
        // key -> NowMs of the last "unknown key" line, of the quest calls and of the mission calls
        private readonly Dictionary<string, long> unknownCallKeyLogged_ = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> unknownMissionKeyLogged_ =
            new Dictionary<string, long>(StringComparer.Ordinal);
        // the problems the narrow ValidateNpcCalls found at InitScripts
        internal List<string> NpcCallProblems { get; } = new List<string>();
        // whether the last load registered the key as an NPC-call quest (the golden replay's C#-side check)
        internal bool IsNpcCallQuest(string key) => npcCallQuests_.ContainsKey(key);

        private const long UnknownKeyLogIntervalMs = 60000;

        // xi.cs = { quest, questEvent, questZoneIn, mission, missionEvent, missionZoneIn }; xi exists by now, xi.cs is
        // made only when absent
        private void RegisterQuestCallGlobals()
        {
            int top = lua_.Top;
            lua_.GetGlobal("xi");
            if (!lua_.IsTable(-1))
            {
                lua_.SetTop(top);
                lua_.NewTable();
                lua_.PushValue(-1);
                lua_.SetGlobal("xi");
            }
            int xi = lua_.Top;
            lua_.GetField(xi, "cs");
            if (!lua_.IsTable(-1))
            {
                lua_.Pop(1);
                lua_.NewTable();
                lua_.PushValue(-1);
                lua_.SetField(xi, "cs");
            }
            int cs = lua_.Top;
            lua_.PushFunction(CallQuest);
            lua_.SetField(cs, "quest");
            lua_.PushFunction(CallQuestEvent);
            lua_.SetField(cs, "questEvent");
            lua_.PushFunction(CallQuestZoneIn);
            lua_.SetField(cs, "questZoneIn");
            lua_.PushFunction(CallMission);
            lua_.SetField(cs, "mission");
            lua_.PushFunction(CallMissionEvent);
            lua_.SetField(cs, "missionEvent");
            lua_.PushFunction(CallMissionZoneIn);
            lua_.SetField(cs, "missionZoneIn");
            lua_.SetTop(top);
        }

        // the call's name in the log lines: xi.cs.quest for a quest's calls, xi.cs.mission for a mission's
        private static string CallName(bool mission) => mission ? "xi.cs.mission" : "xi.cs.quest";

        // the binding a call of the kind takes for the key: a quest call only a quest's, a mission call only a
        // mission's
        private bool TryCallBinding(string key, bool mission, out QuestScriptBinding b) =>
            npcCallQuests_.TryGetValue(key, out b) && (b.Mission != null) == mission;

        // the unknown key's line, at most once per key per 60 s of NowMs (the quest calls and the mission calls each
        // keep their own times)
        private void LogUnknownCallKey(string key, bool mission = false)
        {
            key ??= "(none)";
            var logged = mission ? unknownMissionKeyLogged_ : unknownCallKeyLogged_;
            long now = NowMs();
            if (logged.TryGetValue(key, out long last) && now - last < UnknownKeyLogIntervalMs) return;
            logged[key] = now;
            log_($"quests: {CallName(mission)}: unknown key {key}");
        }

        // the check context of one call: a quest's, or a mission's with the arguments Mission:getCheckArgs gives a
        // section check (player:getCurrentMission(log), player:getMissionStatus(log)), read once for the call
        private LiveQuestContext NewCallCheck(LuaState l, QuestScriptBinding b, int npc = 0, ushort? zone = null)
        {
            var check = new LiveQuestContext(l, log_, b, "npc call", player: 1, npc: npc, zone: zone, check: true,
                                             called: true);
            if (b.Mission == null) return check;
            int log = (int)b.Mission.Log;
            int current = check.CurrentMission(log), status = check.MissionStatus(log);
            return new LiveQuestContext(l, log_, b, "npc call", player: 1, npc: npc, zone: zone, check: true,
                                        called: true, missionCurrent: current, missionStatus: status);
        }

        // whether state i's match holds for the call; a check that throws is false (logged). A quest's state matches
        // on the status (read by the caller) and the step, as its section check does; a mission's on IsMissionMatch
        // over the check's mission arguments, and it passes a null status
        private bool CallStateMatches(QuestScriptBinding b, int i, QuestStatus? status, LiveQuestContext check)
        {
            var state = b.Def.States[i];
            try
            {
                if (b.Mission != null) return state.IsMissionMatch(check);
                int step = state.Matches.Any(m => m.Step.HasValue) ? check.Step : 0;
                bool match = state.IsMatch(status.Value, step, check);
                ReportRemoved(b, status.Value, check);
                return match;
            }
            catch (Exception e)
            {
                log_($"quests: {b.Key} check {i + 1}: {e.Message}");
                return false;
            }
        }

        // <the value at self>:<method>() -> read
        private static T LuaGet<T>(LuaState l, int self, string method, Func<T> read)
        {
            int top = l.Top;
            try
            {
                l.PushValue(self);
                int obj = l.Top;
                l.GetField(obj, method);
                l.PushValue(obj);
                l.Call(1, 1);
                return read();
            }
            finally
            {
                l.SetTop(top);
            }
        }

        // xi.cs.quest(player, entity, key, kind[, trade]) -> boolean; the entity is the npc, or the mob for onMobDeath
        private int CallQuest(LuaState l) => CallEntity(l, mission: false);
        // xi.cs.mission(player, entity, key, kind[, trade]) -> boolean, as xi.cs.quest for a mission's key
        private int CallMission(LuaState l) => CallEntity(l, mission: true);

        private int CallEntity(LuaState l, bool mission)
        {
            int top = l.Top;
            bool result = false;
            string key = l.IsString(3) ? l.ToStringValue(3) : null;
            try
            {
                if (key == null || !TryCallBinding(key, mission, out var b)) LogUnknownCallKey(key, mission);
                else result = RunNpcCall(l, b);
            }
            catch (Exception e)
            {
                log_($"quests: {CallName(mission)} {key}: {e.Message}");
                result = false;
            }
            finally
            {
                l.SetTop(top);
            }
            l.Push(result);
            return 1;
        }

        private bool RunNpcCall(LuaState l, QuestScriptBinding b)
        {
            string kindName = l.IsString(4) ? l.ToStringValue(4) : null;
            var kind = kindName switch
            {
                "onTrigger" => QuestHandlerKind.OnTrigger,
                "onTrade" => QuestHandlerKind.OnTrade,
                "onMobDeath" => QuestHandlerKind.OnMobDeath,
                _ => (QuestHandlerKind?)null,
            };
            if (kind == null)
            {
                log_($"quests: {CallName(b.Mission != null)} {b.Key}: unknown kind {kindName ?? "(none)"}");
                return false;
            }
            // a mob death with no killer has no player: nothing to run
            if (l.IsNil(1) || l.IsNil(2)) return false;
            // the entity's name as the lookup keys it (npc:getName(), mob:getName())
            bool death = kind == QuestHandlerKind.OnMobDeath;
            ushort zone = (ushort)LuaGet(l, 2, "getZoneID", () => (int)l.ToIntegral(-1));
            string npcName = LuaGet(l, 2, "getName", () => l.IsString(-1) ? l.ToStringValue(-1) : null);
            var def = b.Def;
            LiveQuestContext check = null;
            QuestStatus? status = null;
            for (int i = 0; i < def.States.Count; i++)
            {
                var state = def.States[i];
                var h = state.Handlers.FirstOrDefault(x => def.IsCalled(x) && x.Zone == zone && x.Entity == npcName && x.Kind == kind.Value);
                if (h == null) continue;
                // a fresh context per call reads the quest's vars live; a check only reads
                if (check == null)
                {
                    check = NewCallCheck(l, b, npc: death ? 0 : 2, zone: zone);
                    if (b.Mission == null) status = check.Status;
                }
                if (!CallStateMatches(b, i, status, check)) continue;

                string name = HandlerName(h);
                // a mob death's handler has the mob, not an npc (Npc null); its MobDeath the optParams the mob script passed
                // as the fifth argument (quest rework P7), null without one
                var ctx = death
                    ? new LiveQuestContext(l, log_, b, name, player: 1, mob: 2, zone: zone, called: true, mobDeath: l.IsTable(5) ? 5 : 0)
                    : new LiveQuestContext(l, log_, b, name, player: 1, npc: 2, trade: kind == QuestHandlerKind.OnTrade && !l.IsNil(5) ? 5 : 0,
                                           zone: zone, called: true);
                if (!RunHandler(b, h, name, ctx, out var reply)) return false;
                // a Run handler (OnMobDeath) has no reply: it ran
                if (h.Run != null) return true;
                if (reply == null) return false;
                try
                {
                    if (RenderCallReply(l, reply, zone, 1, 2) is { } error)
                    {
                        log_($"quests: {b.Key} {name}: {error}");
                        return false;
                    }
                }
                catch (Exception e)
                {
                    log_($"quests: {b.Key} {name}: {e.Message}");
                    return false;
                }
                return true;
            }
            return false;
        }

        // xi.cs.questEvent(player, npc, key, csid, option[, 'update']) -> boolean: whether a handler ran; npc may be nil (a
        // zone event), 'update' runs OnEventUpdate, none OnEventFinish
        private int CallQuestEvent(LuaState l) => CallEvent(l, mission: false);
        // xi.cs.missionEvent(player, npc, key, csid, option[, 'update']) -> boolean, as xi.cs.questEvent for a
        // mission's key
        private int CallMissionEvent(LuaState l) => CallEvent(l, mission: true);

        private int CallEvent(LuaState l, bool mission)
        {
            int top = l.Top;
            bool result = false;
            string key = l.IsString(3) ? l.ToStringValue(3) : null;
            try
            {
                if (key == null || !TryCallBinding(key, mission, out var b)) LogUnknownCallKey(key, mission);
                else result = RunNpcCallEvent(l, b);
            }
            catch (Exception e)
            {
                log_($"quests: {CallName(mission)}Event {key}: {e.Message}");
                result = false;
            }
            finally
            {
                l.SetTop(top);
            }
            l.Push(result);
            return 1;
        }

        private bool RunNpcCallEvent(LuaState l, QuestScriptBinding b)
        {
            var kind = QuestHandlerKind.OnEventFinish;
            if (!l.IsNil(6))
            {
                string kindName = l.IsString(6) ? l.ToStringValue(6) : null;
                if (kindName != "update")
                {
                    log_($"quests: {CallName(b.Mission != null)} {b.Key}: unknown kind {kindName ?? "(none)"}");
                    return false;
                }
                kind = QuestHandlerKind.OnEventUpdate;
            }
            if (l.IsNil(1) || !l.IsNumber(4)) return false;
            int csid = (int)l.ToIntegral(4), option = l.IsNumber(5) ? (int)l.ToIntegral(5) : 0;
            // a string option (an update after a typed input): OptionText, Option 0
            string optionText = l.IsString(5) ? l.ToStringValue(5) : null;
            var def = b.Def;
            var check = new LiveQuestContext(l, log_, b, "npc call", player: 1, npc: 2, check: true, called: true);
            ushort zone = check.Zone;
            QuestStatus? status = null;
            // a mission's check context, with its mission arguments, made on the first state with handlers
            LiveQuestContext missionCheck = null;
            for (int i = 0; i < def.States.Count; i++)
            {
                var state = def.States[i];
                var hs = state.Handlers.Where(x => def.IsCalled(x) && x.Kind == kind && x.Zone == zone && x.Id == csid).ToList();
                if (hs.Count == 0) continue;
                if (b.Mission != null) missionCheck ??= NewCallCheck(l, b, npc: 2);
                else status ??= check.Status;
                if (!CallStateMatches(b, i, status, missionCheck ?? check)) continue;
                bool ran = false;
                foreach (var h in hs)
                {
                    string name = HandlerName(h);
                    var ctx = new LiveQuestContext(l, log_, b, name, player: 1, npc: 2, zone: zone, csid: csid, option: option, called: true,
                                                   optionText: optionText);
                    ran |= RunHandler(b, h, name, ctx, out _);
                }
                return ran;
            }
            return false;
        }

        // xi.cs.questZoneIn(player, key, prevZone) -> csid | { csid, textTable, flags } | nil: the first matching state's OnZoneIn for the player's zone,
        // its reply's event id; it starts nothing (the zone's onZoneIn returns the id, as a Lua zone script does)
        private int CallQuestZoneIn(LuaState l) => CallZoneIn(l, mission: false);
        // xi.cs.missionZoneIn(player, key, prevZone), as xi.cs.questZoneIn for a mission's key
        private int CallMissionZoneIn(LuaState l) => CallZoneIn(l, mission: true);

        private int CallZoneIn(LuaState l, bool mission)
        {
            int top = l.Top;
            QuestReply zoneIn = null;
            string key = l.IsString(2) ? l.ToStringValue(2) : null;
            try
            {
                if (key == null || !TryCallBinding(key, mission, out var b)) LogUnknownCallKey(key, mission);
                else zoneIn = RunNpcCallZoneIn(l, b);
            }
            catch (Exception e)
            {
                log_($"quests: {CallName(mission)}ZoneIn {key}: {e.Message}");
                zoneIn = null;
            }
            finally
            {
                l.SetTop(top);
            }
            // the cutscene id, or { id, textTable, flags } (quest rework P8), as the container's onZoneIn returns it
            if (zoneIn != null) PushZoneInReply(l, zoneIn); else l.PushNil();
            return 1;
        }

        private QuestReply RunNpcCallZoneIn(LuaState l, QuestScriptBinding b)
        {
            if (l.IsNil(1)) return null;
            var def = b.Def;
            var check = new LiveQuestContext(l, log_, b, "npc call", player: 1, check: true, called: true);
            ushort zone = check.Zone;
            QuestStatus? status = null;
            // a mission's check context, with its mission arguments, made on the first state with a zone-in
            LiveQuestContext missionCheck = null;
            for (int i = 0; i < def.States.Count; i++)
            {
                var state = def.States[i];
                var h = state.Handlers.FirstOrDefault(x => def.IsCalled(x) && x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == zone);
                if (h == null) continue;
                if (b.Mission != null) missionCheck ??= NewCallCheck(l, b);
                else status ??= check.Status;
                if (!CallStateMatches(b, i, status, missionCheck ?? check)) continue;
                string name = HandlerName(h);
                var ctx = new LiveQuestContext(l, log_, b, name, player: 1, zone: zone, called: true);
                if (!RunHandler(b, h, name, ctx, out var reply) || reply == null) return null;
                // as the container's onZoneIn: an event id, or { id, textTable, flags }; a reply with arguments is refused
                // rather than started without them, and one with modifiers or another named field likewise
                if (QuestScriptValidator.ZoneInReplyProblem(reply) is { } problem)
                {
                    log_($"quests: {b.Key} {name}: {problem}");
                    return null;
                }
                return reply;
            }
            return null;
        }

        // the reply as the plain player:* call an NPC script writes; null when done, else why not (nothing sent)
        private string RenderCallReply(LuaState l, QuestReply r, ushort zone, int player, int npc)
        {
            if ((QuestScriptValidator.CallReplyProblem(r) ?? QuestScriptValidator.ArgsProblem(r, called: true)) is { } problem) return problem;
            if (r.Kind == QuestReplyKind.NoAction) return null;
            int top = l.Top;
            string method = r.Kind switch
            {
                QuestReplyKind.Cutscene => "startCutscene",
                QuestReplyKind.MessageSpecial => "messageSpecial",
                QuestReplyKind.MessageText => "messageText",
                QuestReplyKind.MessageName => "messageName",
                _ => "startEvent",
            };
            int? text = null;
            if (r.Kind is QuestReplyKind.MessageSpecial or QuestReplyKind.MessageText or QuestReplyKind.MessageName)
            {
                text = ReplyTextId(l, r, zone);
                if (text == null) return UnknownText(r, zone);
            }
            l.PushValue(player);
            int self = l.Top;
            l.GetField(self, method);
            l.PushValue(self);
            int n = 1;
            switch (r.Kind)
            {
                // player:messageText(npc, id, args...)
                case QuestReplyKind.MessageText:
                    l.PushValue(npc);
                    l.Push((double)text.Value);
                    n += 2;
                    break;
                // player:messageName(id, player, args...)
                case QuestReplyKind.MessageName:
                    l.Push((double)text.Value);
                    l.PushValue(player);
                    n += 2;
                    break;
                case QuestReplyKind.MessageSpecial:
                    l.Push((double)text.Value);
                    n++;
                    break;
                default:
                    l.Push((double)r.Id);
                    n++;
                    break;
            }
            n += PushReplyArgs(l, r);
            l.Call(n, 0);
            l.SetTop(top);
            return null;
        }

        // ---- the call sites in the scripts --------------------------------------------------------------------------

        // the quest calls and the mission calls (quest rework C2): xi.cs.<quest | mission>[Event | ZoneIn](
        private static readonly Regex CallSite =
            new Regex(@"\bxi\.cs\.(questZoneIn|questEvent|quest|missionZoneIn|missionEvent|mission)\s*\(",
                      RegexOptions.Compiled);
        private static readonly Regex StringLiteral = new Regex(@"^(['""])((?:(?!\1).)*)\1$", RegexOptions.Compiled | RegexOptions.Singleline);

        // the arguments of the call whose '(' is just before start, split at the top level; strings and brackets respected
        private static List<string> CallArguments(string text, int start)
        {
            var args = new List<string>();
            int depth = 0, from = start;
            char quote = '\0';
            for (int i = start; i < text.Length; i++)
            {
                char ch = text[i];
                if (quote != '\0')
                {
                    if (ch == '\\') i++;
                    else if (ch == quote) quote = '\0';
                    continue;
                }
                if (ch is '\'' or '"') quote = ch;
                else if (ch is '(' or '{' or '[') depth++;
                else if (ch is '}' or ']') depth--;
                else if (ch == ')')
                {
                    if (depth == 0)
                    {
                        args.Add(text.Substring(from, i - from).Trim());
                        return args;
                    }
                    depth--;
                }
                else if (ch == ',' && depth == 0)
                {
                    args.Add(text.Substring(from, i - from).Trim());
                    from = i + 1;
                }
            }
            return args;
        }

        // path -> (last write, length, the text when it mentions xi.cs., else null): a reload in the same process re-reads
        // only the files that changed
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Ticks, long Length, string Text)> ScanCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, (long, long, string)>(StringComparer.Ordinal);

        // the file's text when it mentions xi.cs., else null
        private static string ScanText(string file)
        {
            var info = new FileInfo(file);
            long ticks = info.LastWriteTimeUtc.Ticks, length = info.Length;
            if (ScanCache.TryGetValue(file, out var hit) && hit.Ticks == ticks && hit.Length == length) return hit.Text;
            string text = File.ReadAllText(file);
            if (!text.Contains("xi.cs.", StringComparison.Ordinal)) text = null;
            ScanCache[file] = (ticks, length, text);
            return text;
        }

        // scriptsRoot/zones/*/npcs/*.lua, mobs/*.lua and Zone.lua: every xi.cs.quest( / xi.cs.questEvent( / xi.cs.questZoneIn(
        // call is checked against the registered NPC-call quests (its key, a literal kind), and every called handler against
        // the scripts: a Called() NPC's script calls the quest, a Called() mob's script calls it with 'onMobDeath', the zone's
        // Zone.lua calls questZoneIn for a called OnZoneIn, and some script of the zone calls questEvent(..., 'update') for a
        // called OnEventUpdate. Each problem is logged
        // quest rework C2: the mission calls (xi.cs.mission( / missionEvent( / missionZoneIn() likewise, against the
        // registered NPC-call missions; and scriptsRoot/battlefields/*/*.lua too, each file a script of the zone its
        // folder names that is no entity's (as Zone.lua: a call there needs a called handler of the zone, and an update
        // call counts for the zone)
        // narrow (the server's startup): only the scripts of the (zone, entity) pairs that have Called() handlers and
        // the Zone.lua and battlefield files of the zones with called state-level handlers, a handful of files (a
        // zone's other scripts only while its update call is not found in them); the full scan is the test gate's
        // the startup's call: a script that can't be read must not stop the server from starting, so the failure is logged
        // and returned as a problem (the test gate still fails on it)
        public IReadOnlyList<string> ValidateNpcCallsGuarded(string scriptsRoot, bool narrow)
        {
            try { return ValidateNpcCalls(scriptsRoot, narrow); }
            catch (Exception ex)
            {
                string line = $"validation failed: {ex.GetBaseException().Message}";
                log_($"quests: npc call: {line}");
                return new[] { line };
            }
        }

        // xi.cs.quest's kinds and questEvent's sixth argument (none: OnEventFinish)
        private static readonly HashSet<string> CallKinds = new HashSet<string>(StringComparer.Ordinal) { "onTrigger", "onTrade", "onMobDeath" };
        private static readonly HashSet<string> EventKinds = new HashSet<string>(StringComparer.Ordinal) { "update" };

        public IReadOnlyList<string> ValidateNpcCalls(string scriptsRoot, bool narrow = false)
        {
            var problems = new List<string>();
            var zoneIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
            var zoneNames = new Dictionary<ushort, string>();
            var r = Exec("SELECT zoneid, name FROM zone_settings");
            while (r != null && r.Next())
            {
                zoneIds[r.Get<string>("name")] = r.Get<ushort>("zoneid");
                zoneNames[r.Get<ushort>("zoneid")] = r.Get<string>("name");
            }
            string zonesDir = Path.Combine(scriptsRoot, "zones");
            string battlefieldsDir = Path.Combine(scriptsRoot, "battlefields");
            // what the scripts call: (key, zone, entity) any call from an entity's script ("" for Zone.lua and a
            // battlefield file); (key, zone, entity) an 'onMobDeath' call from it; (key, zone) Zone.lua's zone-in call;
            // (key, zone) an event call with 'update'
            var seen = new HashSet<(string, ushort, string)>();
            var deaths = new HashSet<(string, ushort, string)>();
            var zoneIns = new HashSet<(string, ushort)>();
            var updates = new HashSet<(string, ushort)>();
            var read = new HashSet<string>(StringComparer.Ordinal);
            var called = new HashSet<(ushort, string)>();
            var zoneLevel = new HashSet<ushort>();
            if (narrow)
                foreach (var nb in npcCallQuests_.Values)
                    foreach (var h in nb.Def.States.SelectMany(st => st.Handlers).Where(nb.Def.IsCalled))
                        if (h.Entity != null) called.Add((h.Zone, h.Entity));
                        else zoneLevel.Add(h.Zone);

            // the files' calls, in file order
            void Scan(List<string> files, ushort zoneId)
            {
                files = files.Where(read.Add).ToList();
                var texts = new string[files.Count];
                System.Threading.Tasks.Parallel.For(0, files.Count, i => texts[i] = ScanText(files[i]));
                for (int fi = 0; fi < files.Count; fi++)
                {
                    string file = files[fi], text = texts[fi];
                    if (text == null) continue;
                    string rel = Path.GetRelativePath(scriptsRoot, file).Replace('\\', '/');
                    // a battlefield file is a script of its folder's zone that is no entity's, as Zone.lua is
                    bool isBattlefield = rel.StartsWith("battlefields/", StringComparison.Ordinal);
                    bool isZone = !isBattlefield && Path.GetFileName(file) == "Zone.lua";
                    bool zoneWide = isZone || isBattlefield;
                    string npc = Path.GetFileNameWithoutExtension(file);
                    foreach (Match call in CallSite.Matches(text))
                    {
                        int lineStart = text.LastIndexOf('\n', call.Index) + 1;
                        if (text.Substring(lineStart, call.Index - lineStart).Contains("--", StringComparison.Ordinal)) continue;
                        var args = CallArguments(text, call.Index + call.Length);
                        string name = call.Groups[1].Value;
                        // the mission calls are the quest calls' shape: fn is the quest call's name of the same shape
                        bool mission = name.StartsWith("mission", StringComparison.Ordinal);
                        string fn = mission ? "quest" + name.Substring("mission".Length) : name;
                        // questZoneIn(player, key, prevZone); quest / questEvent(player, entity, key, ...)
                        int keyAt = fn == "questZoneIn" ? 1 : 2;
                        var literal = args.Count > keyAt ? StringLiteral.Match(args[keyAt]) : null;
                        if (literal == null || !literal.Success)
                        {
                            problems.Add($"{rel}: the key must be a string literal");
                            continue;
                        }
                        string key = literal.Groups[2].Value;
                        // quest's fourth argument, questEvent's sixth: a string literal (a variable could not be
                        // checked, and a mob's onMobDeath call would be reported missing for no clear reason)
                        int kindAt = fn == "quest" ? 3 : fn == "questEvent" ? 5 : -1;
                        string kind = null;
                        if (kindAt >= 0 && args.Count > kindAt)
                        {
                            if (StringLiteral.Match(args[kindAt]) is not { Success: true } kindLiteral)
                            {
                                problems.Add($"{rel}: the kind must be a string literal ({args[kindAt]})");
                                continue;
                            }
                            kind = kindLiteral.Groups[2].Value;
                            if (!(fn == "quest" ? CallKinds : EventKinds).Contains(kind))
                            {
                                problems.Add($"{rel}: unknown kind '{kind}'");
                                continue;
                            }
                        }
                        // the zone-in is the zone's onZoneIn: a call anywhere else would never run at a zone-in
                        if (fn == "questZoneIn" && !isZone)
                        {
                            problems.Add($"{rel}: xi.cs.{name} outside Zone.lua " +
                                         "(its onZoneIn is the only zone-in)");
                            continue;
                        }
                        // a quest call takes only a quest's key, a mission call only a mission's
                        if (!TryCallBinding(key, mission, out var b))
                        {
                            problems.Add($"{rel}: unknown key {key}");
                            continue;
                        }
                        var def = b.Def;
                        bool has = fn == "questZoneIn"
                            ? def.States.Any(st => st.Handlers.Any(h => def.IsCalled(h) && h.Zone == zoneId && h.Kind == QuestHandlerKind.OnZoneIn))
                            : zoneWide
                                ? def.States.Any(st => st.Handlers.Any(h => def.IsCalled(h) && h.Zone == zoneId))
                                : def.States.Any(st => st.Handlers.Any(h => def.IsCalled(h) && h.Zone == zoneId &&
                                                                            (h.Entity == npc || fn == "questEvent" && h.Entity == null)));
                        if (!has)
                        {
                            problems.Add($"{rel}: {key} has no handlers for {(isZone ? "Zone" : npc)}");
                            continue;
                        }
                        if (fn == "questZoneIn")
                        {
                            zoneIns.Add((key, zoneId));
                            continue;
                        }
                        seen.Add((key, zoneId, zoneWide ? "" : npc));
                        if (kind == "onMobDeath" && !zoneWide) deaths.Add((key, zoneId, npc));
                        if (kind == "update") updates.Add((key, zoneId));
                    }
                }
            }

            // a zone's battlefield folder: battlefields/<zone>, else the zone's name with '-' as '_' (the folder of
            // Riverne-Site_A01 is Riverne_Site_A01): ContainerFolders.BattlefieldDir
            string BattlefieldDir(string zoneName) => ContainerFolders.BattlefieldDir(battlefieldsDir, zoneName);
            // the zone of a battlefield folder named so
            var folderZoneIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
            foreach (var (zn, id) in zoneIds) folderZoneIds.TryAdd(ContainerFolders.Battlefield(zn), id);

            // a zone's battlefield files: battlefields/<zone>/*.lua
            List<string> BattlefieldFiles(string zoneName)
            {
                string dir = BattlefieldDir(zoneName);
                return Directory.Exists(dir)
                    ? Directory.GetFiles(dir, "*.lua").OrderBy(f => f, StringComparer.Ordinal).ToList()
                    : new List<string>();
            }

            // a zone's scripts: zones/<zone>/npcs/*.lua, mobs/*.lua, then Zone.lua, then its battlefield files
            List<string> AllFiles(string zoneName)
            {
                var files = new List<string>();
                string zoneDir = Path.Combine(zonesDir, zoneName);
                foreach (var sub in new[] { "npcs", "mobs" })
                {
                    string dir = Path.Combine(zoneDir, sub);
                    if (Directory.Exists(dir)) files.AddRange(Directory.GetFiles(dir, "*.lua").OrderBy(f => f, StringComparer.Ordinal));
                }
                string zoneFile = Path.Combine(zoneDir, "Zone.lua");
                if (File.Exists(zoneFile)) files.Add(zoneFile);
                files.AddRange(BattlefieldFiles(zoneName));
                return files;
            }

            // whether the scripts tree has a folder for the zone, under zones/ or battlefields/
            bool HasZoneFolder(string zoneName) =>
                Directory.Exists(Path.Combine(zonesDir, zoneName)) || Directory.Exists(BattlefieldDir(zoneName));
            IEnumerable<string> FolderNames(string dir) =>
                Directory.Exists(dir)
                    ? Directory.GetDirectories(dir).Select(Path.GetFileName)
                    : Enumerable.Empty<string>();

            // the zones by folder name: narrow, those with called handlers; else every folder of zones/ and
            // battlefields/
            IEnumerable<string> zoneFolders = narrow
                ? called.Select(c => c.Item1).Concat(zoneLevel).Distinct().Where(zoneNames.ContainsKey)
                    .Select(z => zoneNames[z]).Where(HasZoneFolder).OrderBy(n => n, StringComparer.Ordinal)
                : FolderNames(zonesDir).Union(FolderNames(battlefieldsDir), StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal);
            foreach (var zoneName in zoneFolders)
            {
                if (!zoneIds.TryGetValue(zoneName, out ushort zoneId)) folderZoneIds.TryGetValue(zoneName, out zoneId);
                if (!narrow)
                {
                    Scan(AllFiles(zoneName), zoneId);
                    continue;
                }
                string zoneDir = Path.Combine(zonesDir, zoneName);
                var files = new List<string>();
                foreach (var sub in new[] { "npcs", "mobs" })
                    foreach (var name in called.Where(c => c.Item1 == zoneId).Select(c => c.Item2).OrderBy(n => n, StringComparer.Ordinal))
                    {
                        string f = Path.Combine(zoneDir, sub, name + ".lua");
                        if (File.Exists(f)) files.Add(f);
                    }
                string zoneFile = Path.Combine(zoneDir, "Zone.lua");
                if (zoneLevel.Contains(zoneId) && File.Exists(zoneFile)) files.Add(zoneFile);
                // a called state-level handler (an event's, a zone-in's) may be called from the zone's battlefields
                if (zoneLevel.Contains(zoneId)) files.AddRange(BattlefieldFiles(zoneName));
                Scan(files, zoneId);
            }

            // each quest's called handlers, in key order: per (zone, entity) in declaration order, then the zone-ins, then
            // the updates (by zone)
            var keys = npcCallQuests_.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            IEnumerable<ushort> CalledZones(QuestScriptDefinition def, QuestHandlerKind kind) =>
                def.States.SelectMany(st => st.Handlers).Where(h => h.Entity == null && h.Kind == kind && def.IsCalled(h)).Select(h => h.Zone).Distinct();
            // narrow: an update call can sit in any script of its zone, so a zone whose update call is not found yet is read whole
            if (narrow)
                foreach (var zoneId in keys.SelectMany(k => CalledZones(npcCallQuests_[k].Def, QuestHandlerKind.OnEventUpdate)
                                                                 .Where(z => !updates.Contains((k, z)))).Distinct().ToList())
                {
                    if (!zoneNames.TryGetValue(zoneId, out var zn) || !HasZoneFolder(zn)) continue;
                    Scan(AllFiles(zn), zoneId);
                }
            string ZoneName(ushort zone) => zoneNames.TryGetValue(zone, out var zn) ? zn : zone.ToString();
            foreach (var key in keys)
            {
                var def = npcCallQuests_[key].Def;
                // what the scripts never call: the quest, or the mission (quest rework C2)
                string what = npcCallQuests_[key].Mission != null ? "mission" : "quest";
                foreach (var g in def.States.SelectMany(st => st.Handlers).Where(h => h.Entity != null && def.IsCalled(h)).GroupBy(h => (h.Zone, h.Entity)))
                {
                    bool any = g.Any(h => h.Kind != QuestHandlerKind.OnMobDeath), death = g.Any(h => h.Kind == QuestHandlerKind.OnMobDeath);
                    if (any && !seen.Contains((key, g.Key.Zone, g.Key.Entity)) || death && !deaths.Contains((key, g.Key.Zone, g.Key.Entity)))
                        problems.Add($"{key}: {ZoneName(g.Key.Zone)}/{g.Key.Entity} " +
                                     $"has handlers but its script never calls the {what}");
                }
                foreach (var zone in CalledZones(def, QuestHandlerKind.OnZoneIn).Where(z => !zoneIns.Contains((key, z))))
                    problems.Add($"{key}: {ZoneName(zone)}/Zone has handlers but its script never calls the {what}");
                foreach (var zone in CalledZones(def, QuestHandlerKind.OnEventUpdate).Where(z => !updates.Contains((key, z))))
                    problems.Add($"{key}: {ZoneName(zone)}/onEventUpdate " +
                                 $"has handlers but its script never calls the {what}");
            }
            foreach (var p in problems) log_($"quests: npc call: {p}");
            return problems;
        }
    }
}
