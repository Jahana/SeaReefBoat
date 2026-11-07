using System;
using System.Collections.Generic;
using System.IO;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // NPC interaction: GP_CLI_COMMAND_ACTION Talk runs the NPC's onTrigger through InteractionGlobal (the existing
    // scripts/ tree on the embedded LuaJIT), the script starts events, and GP_CLI_COMMAND_EVENTEND feeds the client's
    // answers back to onEventUpdate / onEventFinish (CCharEntity's event queue and luautils' event entry points).
    public sealed partial class ZoneHost
    {
        private const ushort SpellGeoRegen = 798, SpellGeoGravity = 827;

        private ScriptEngine scripts_;
        private LuaState lua_;

        // the session each loaded char is on, for bindings that push packets
        private readonly Dictionary<CharData, MapSession> charSessions_ = new Dictionary<CharData, MapSession>();

        private const byte AnimationEvent = 4, AnimationOpenDoor = 8, AnimationCloseDoor = 9;
        private const ushort MsgEventSkipped = 117;

        // luautils::init's part after the engine is up: the C# bindings, then the global scripts and InteractionGlobal
        public void InitScripts(ScriptEngine scripts)
        {
            scripts_ = scripts;
            lua_ = scripts.Lua;
            scripts.LuaInit();
            scripts.SettingsInit();
            scripts.LuautilsInit();
            RegisterBindings();
            scripts.OnRoeParseRecords = ParseRoeRecords;
            scripts.OnRoeParseTimed = ParseRoeTimedSchedule;

            var zones = new List<ScriptZone>();
            var r = Exec("SELECT zoneid, name FROM zone_settings ORDER BY zoneid");
            while (r != null && r.Next())
            {
                string name = r.Get<string>("name");
                zones.Add(new ScriptZone { Id = r.Get<ushort>("zoneid"), Name = name, DataFolders = { ZoneInfo.FolderName(name) } });
                zoneNameOf_[r.Get<ushort>("zoneid")] = name;
            }
            zoneNamesRead_ = true;
            scripts.LoadGlobalScripts(zones);
            // luautils.cpp:569-576: the Lua modules init.txt lists, before the containers (platform round P11)
            LoadLuaModules();
            if (loader_ != null)
            {
                loader_.Progress.Registry = BuildQuestKeyRegistry();
                log_($"quests: {loader_.Progress.Registry.Count} quest keys");
            }
            // zoneutils::GetManagedZones: the zones this process serves (its loaded zones, or under --lazy the managed ones)
            var managed = (HashSet<ushort>)ZonesAssignedToThisProcess();
            scripts.InitInteractionGlobal(zones.FindAll(z => managed.Contains((ushort)z.Id)));
            // the xi.cs.quest calls of the real NPC scripts against the registered NPC-call quests
            NpcCallProblems.Clear();
            NpcCallProblems.AddRange(ValidateNpcCallsGuarded(scripts.ScriptsDir, narrow: true));
            RegisterTrialListeners();
            LoadExpTables();
            log_($"lua: {scripts.ScriptsLoaded} global scripts, {scripts.LoadErrors} load errors, {lua_.MemoryKb} KB");
        }

        // itemutils::Initialize: the Magian trial listeners loaded after the items (xi.magian.registerTrialListeners puts
        // xi.magian.onItemEquip / onItemUnequip on xi.items[name] of every kill / experience / weapon skill trial's
        // item). LoadItemList has loaded each item's scripts/items/{name}.lua (update_if_empty) before; the host loads
        // an item's script the first time it is used, so the scripts of those trials' items are loaded here first and
        // keep their own functions under the listeners as in the C++
        private void RegisterTrialListeners()
        {
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.magian.trials");
                var itemIds = new List<ushort>();
                if (lua_.IsTable(-1))
                {
                    int trials = lua_.Top;
                    lua_.PushNil();
                    while (lua_.Next(trials))
                    {
                        if (lua_.IsTable(-1))
                        {
                            bool listener = false;
                            foreach (var key in new[] { "defeatMob", "gainExp", "useWeaponskill" })
                            {
                                lua_.GetField(-1, key);
                                listener |= lua_.ToBoolean(-1);
                                lua_.Pop(1);
                            }
                            lua_.GetField(-1, "requiredItem");
                            if (listener && lua_.IsTable(-1))
                            {
                                lua_.GetField(-1, "itemId");
                                if (lua_.IsNumber(-1)) itemIds.Add((ushort)(long)lua_.ToNumber(-1));
                                lua_.Pop(1);
                            }
                            lua_.Pop(1);
                        }
                        lua_.Pop(1);
                    }
                }
                lua_.SetTop(top);
                foreach (var id in itemIds)
                    if (LookupItem(id)?.Name is { } name && itemScripts_.Add($"./scripts/items/{name}.lua"))
                        scripts_.LoadLuaObjectFromFile($"./scripts/items/{name}.lua");

                lua_.GetPath("xi.magian.registerTrialListeners");
                if (!lua_.IsFunction(-1))
                {
                    log_("xi.magians.registerTrialListeners not valid!");
                    return;
                }
                lua_.Call(0, 0);
            }
            catch (LuaException e)
            {
                log_($"lua: xi.magian.registerTrialListeners: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnEntityLoad for the zone's NPCs, and the zone's own script
        private void LoadZoneScripts(string zoneName)
        {
            if (scripts_ == null || zoneName == "") return;
            string dir = Path.Combine(scripts_.ScriptsDir, "zones", zoneName, "npcs");
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.lua"))
                    scripts_.LoadLuaObjectFromFile($"./scripts/zones/{zoneName}/npcs/{Path.GetFileName(file)}");
            }
            scripts_.LoadLuaObjectFromFile($"./scripts/zones/{zoneName}/Zone.lua");
        }

        private MapSession SessionOf(CharData c) => charSessions_.TryGetValue(c, out var s) ? s : null;

        private void PushTo(CharData c, BasicPacket p) => SessionOf(c)?.PacketList.AddLast(p);

        // every char in the zone that has the NPC spawned sees its update
        private void BroadcastNpcUpdate(NpcEntity n, byte updatemask)
        {
            foreach (var (c, s) in charSessions_)
                if (c.SpawnNpcList.Contains(n.Id)) UpdateEntityPacket(s, n, EntityUpdate.Update, updatemask);
        }

        // ReportErrorToPlayer is gated on the GM level; the log always gets it
        private void ScriptError(string where, LuaException e) => log_($"lua: {where}: {e.Message}");

        // ---- GP_CLI_COMMAND_ACTION -----------------------------------------------------------------------------------
        // Talk, Attack, AttackOff, ChangeTarget, Help, SendResRdy, Blockaid, Fish, CastMagic, Weaponskill, Shoot,
        // JobAbility, the death menu's Home Point, the raise and tractor menus, Assist, Mount, Dismount and ChocoboDig
        // (ZoneHost.Mounts.cs) and a Monipulator's MonsterSkill (ZoneHost.Monstrosity.cs)

        private const ushort MsgStdCannotUseCommandAtTheMoment = 142, MsgStdBlockaidActivated = 221, MsgStdBlockaidCanceled = 222,
                             MsgStdBlockaidCurrentlyActive = 223, MsgStdBlockaidCurrentlyInactive = 224;

        private void OnAction(MapSession s, CharData c, BasicPacket p)
        {
            var actionId = (ActionId)BitConverter.ToUInt16(p.Buffer, C2sAction.OffActionID);
            // a jailed char's controller calls go nowhere
            if (c.NoController && actionId is ActionId.Attack or ActionId.CastMagic or ActionId.Weaponskill or ActionId.Shoot or
                    ActionId.JobAbility or ActionId.AttackOff or ActionId.ChangeTarget)
                return;
            switch (actionId)
            {
                case ActionId.Talk:
                    OnTalk(s, c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    break;
                case ActionId.Attack:
                    if (c.StatusEffects.HasStatusEffect(EffectId.Mounted)) c.StatusEffects.DelStatusEffectSilent(EffectId.Mounted);
                    Engage(c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    break;
                case ActionId.CastMagic:
                {
                    var castTarget = AbilityEntityByTargId(c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    ushort castSpellId = (ushort)C2sAction.GetSpellId(p.Buffer);
                    PlayerCast(c, castTarget, castSpellId);
                    // a Geo- spell's luopan goes at the target plus the packet's offset (each axis within 19 yalms; the
                    // packet's x / z / y fill x / y / z, as in C++); reset first so no other spell reuses it
                    if (castSpellId >= SpellGeoRegen && castSpellId <= SpellGeoGravity)
                    {
                        (c.ActionOffsetX, c.ActionOffsetY, c.ActionOffsetZ) = (0, 0, 0);
                        if (castTarget != null)
                        {
                            c.ActionOffsetX = castTarget.X + Math.Clamp(C2sAction.GetPosX(p.Buffer), -19.0f, 19.0f);
                            c.ActionOffsetY = castTarget.Y + Math.Clamp(C2sAction.GetPosZ(p.Buffer), -19.0f, 19.0f);
                            c.ActionOffsetZ = castTarget.Z + Math.Clamp(C2sAction.GetPosY(p.Buffer), -19.0f, 19.0f);
                        }
                    }
                    break;
                }
                case ActionId.Weaponskill:
                    OnWeaponSkillAction(s, c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex), (ushort)C2sAction.GetSkillId(p.Buffer));
                    break;
                case ActionId.MonsterSkill:
                    MonsterSkillAction(c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex), (ushort)C2sAction.GetSkillId(p.Buffer));
                    break;
                case ActionId.Shoot:
                    RangedAttack(s, c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    break;
                case ActionId.JobAbility:
                    OnJobAbilityAction(s, c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex), (ushort)C2sAction.GetSkillId(p.Buffer));
                    break;
                case ActionId.AttackOff:
                    Disengage(c);
                    break;
                case ActionId.ChangeTarget:
                    ChangeTarget(c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    break;
                case ActionId.Help:
                    // FFXIclopedia: a Monipulator cannot call for help
                    if (c.Monstrosity == null) CallForHelp(s, c);
                    break;
                case ActionId.Fish:
                    if (c.MoghouseId != 0)
                    {
                        log_($"GP_CLI_COMMAND_ACTION: Player {c.Name} trying to fish in Mog House");
                        Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                        return;
                    }
                    StartFishing(s, c);
                    break;
                case ActionId.Mount:
                    OnMount(s, c, C2sAction.GetMountId(p.Buffer));
                    break;
                case ActionId.Dismount:
                    OnDismount(c);
                    break;
                case ActionId.ChocoboDig:
                    OnChocoboDig(s, c);
                    break;
                case ActionId.SendResRdy:
                    // a mog house spawns its conditional NPCs; elsewhere the NPCs (and the PCs, the requestedInfoSync),
                    // the mobs, the pets and the trusts
                    if (c.MoghouseId == 0)
                    {
                        SpawnNpcs(s, c);
                        SpawnMobs(s, c);
                        SpawnPets(s, c);
                        SpawnTrusts(s, c);
                        SpawnPcs(s, c);
                    }
                    else SpawnConditionalNpcs(s, c);
                    break;
                case ActionId.Blockaid:
                    OnBlockaid(s, c, (BlockaidStatus)BitConverter.ToUInt32(p.Buffer, C2sAction.OffUnion + ActionBufMenu.OffStatusId));
                    break;
                case ActionId.RaiseMenu:
                    OnRaiseMenu(c, C2sAction.GetHomepointMenuStatus(p.Buffer), NowMs());
                    break;
                case ActionId.TractorMenu:
                    OnTractorMenu(s, c, C2sAction.GetTractorMenuStatus(p.Buffer));
                    break;
                case ActionId.Assist:
                    AssistTarget(c, BitConverter.ToUInt16(p.Buffer, C2sAction.OffActIndex));
                    break;
                case ActionId.HomepointMenu:
                    if (c.Monstrosity != null)
                    {
                        MonstrosityDeathMenu(c, (byte)C2sAction.GetHomepointMenuStatus(p.Buffer));
                        break;
                    }
                    SetCharVar(c, "expLost", 0, 0);
                    c.RequestedWarp = WarpRequest.HomePoint;
                    break;
            }
        }

        // ACTIONID::Blockaid: refused while under ALLIED_TAGS
        private void OnBlockaid(MapSession s, CharData c, BlockaidStatus status)
        {
            if (c.StatusEffects.HasStatusEffect(EffectId.AlliedTags))
                Push(s, SystemMessage(MsgStdCannotUseCommandAtTheMoment));
            else if (status == BlockaidStatus.Disable && c.BlockingAid)
            {
                Push(s, SystemMessage(MsgStdBlockaidCanceled));
                c.BlockingAid = false;
            }
            else if (status == BlockaidStatus.Enable && !c.BlockingAid)
            {
                Push(s, SystemMessage(MsgStdBlockaidActivated));
                c.BlockingAid = true;
            }
            else if (status == BlockaidStatus.Toggle)
            {
                c.BlockingAid = !c.BlockingAid;
                Push(s, SystemMessage(c.BlockingAid ? MsgStdBlockaidCurrentlyActive : MsgStdBlockaidCurrentlyInactive));
            }
        }

        // the talk range of OnTalk: the NPC within 6 yalms of the char (the squared 3D distance 36 at most)
        internal const int TalkRange = 6;
        internal static bool InTalkRange(CharData c, NpcEntity npc)
        {
            float dx = npc.X - c.X, dy = npc.Y - c.Y, dz = npc.Z - c.Z;
            return dx * dx + dy * dy + dz * dz <= TalkRange * TalkRange;
        }

        // ACTIONID::Talk
        private void OnTalk(MapSession s, CharData c, ushort actIndex)
        {
            // talking to an NPC cancels /heal
            c.StatusEffects.DelStatusEffectSilent(EffectId.Healing);
            // talking to an NPC force disengages
            if (c.AttackTarget != null && !c.NoController) Disengage(c);
            // blocked states send a release: a costume, crafting, mounted, dead or fishing
            if (c.Costume != 0 || !Pv(c).BlockedBy(BlockedState.Crafting | BlockedState.Mounted | BlockedState.Dead | BlockedState.Fishing).Valid)
            {
                s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.Standard));
                return;
            }

            // Releasing a trust
            if (actIndex >= ZoneInfo.DynamicTargIdStart && ReleaseTrust(c, actIndex)) return;

            var zone = AreaOf(c);
            NpcEntity npc = null;
            foreach (var n in zone.Npcs)
            {
                if (n.TargId == actIndex)
                {
                    npc = n;
                    break;
                }
            }
            if (npc == null) return;
            // a Monipulator triggers nothing outside the Feretory, doors and levers included (FFXIclopedia, retail)
            if (c.Monstrosity != null && c.Zone != Monstrosity.ZoneFeretory)
            {
                s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.Standard));
                return;
            }
            if (npc.ContentDisabled)
            {
                s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.Standard));
                return;
            }

            // the moogles inside mog houses are the exception to the status check (the own mog house: a visitor gets no
            // response)
            if (InTalkRange(c, npc) && (npc.Status == NpcEntity.StatusNormal || c.MoghouseId == c.Id))
            {
                Trigger(c, npc);
                c.History[HistoryNpcInteractions]++;
            }

            if (!c.IsNpcLocked)
            {
                c.EventPreparation.Reset();
                s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.Standard));
            }
        }

        // CAIContainer::Trigger + CTriggerState: a door's script returns -1 and the door opens for 7s
        private void Trigger(CharData c, NpcEntity npc)
        {
            bool isDoor = OnTrigger(c, npc) == -1;
            TriggerListener(npc, "ON_TRIGGER", c, npc);
            NpcTriggered(npc);
            if (isDoor && npc.Animation == AnimationCloseDoor)
            {
                npc.Animation = AnimationOpenDoor;
                npc.DoorCloseAtMs = NowMs() + 7000;
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateHp);
            }
        }

        // luautils::OnTrigger
        private int OnTrigger(CharData c, NpcEntity npc)
        {
            if (lua_ == null) return 0;
            string zone = AreaOf(c).Name;
            string file = $"./scripts/zones/{zone}/npcs/{npc.Name}.lua";
            c.EventPreparation.TargetEntity = npc;
            c.EventPreparation.ScriptFile = file;

            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal.onTrigger");
                lua_.PushObject(c);
                lua_.PushObject(npc);
                scripts_.PushFileFunction(file, "onTrigger");
                lua_.Call(3, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                ScriptError("onTrigger", e);
                return -1;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // the door swings shut again
        private void TickDoors()
        {
            long now = NowMs();
            foreach (var (_, z) in zones_)
            {
                foreach (var n in z.Info.Npcs)
                {
                    if (n.DoorCloseAtMs == 0 || now <= n.DoorCloseAtMs) continue;
                    n.DoorCloseAtMs = 0;
                    n.Animation = AnimationCloseDoor;
                    BroadcastNpcUpdate(n, EntityUpdatePacket.UpdateHp);
                }
            }
        }

        // ---- GP_CLI_COMMAND_EVENTEND -------------------------------------------------------------------------------

        private void OnEventEnd(MapSession s, CharData c, BasicPacket p)
        {
            uint endPara = BitConverter.ToUInt32(p.Buffer, 0x08);
            ushort mode = BitConverter.ToUInt16(p.Buffer, 0x0E);
            ushort eventId = BitConverter.ToUInt16(p.Buffer, 0x12);
            if (mode > 1 || !c.IsInEvent || c.CurrentEvent.EventId != eventId)
            {
                log_($"zone: {c.Name} sent event end {eventId} mode {mode} outside that event");
                return;
            }

            uint result = c.CurrentEvent.Option != 0 ? (uint)c.CurrentEvent.Option : endPara;
            log_($"event: {c.Name} {(mode == 1 ? "update" : "end")} {eventId} result {result}");
            if (mode == 1)
            {
                // an optional cutscene's chosen option locks the char
                if (result != uint.MaxValue && c.CurrentEvent.HasCutsceneOption((int)result))
                {
                    c.Locked = true;
                    c.CurrentEvent.CanSkip = false;
                }
                OnEventUpdate(c, eventId, result);
            }
            else
            {
                OnEventFinish(c, eventId, result);
                // reset if this event did not start another one
                if (c.CurrentEvent.EventId == eventId) EndCurrentEvent(s, c);
            }
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.EventRecvPending));
        }

        // ---- GP_CLI_COMMAND_EVENTENDXZY -----------------------------------------------------------------------------
        // An event update that carries a position (a telepoint warp and the like). The script's onEventUpdate
        // returning 1 (the default) moves the char there, unless it set the noPosUpdate local var; the client's pending
        // position is cleared either way. A living pet is put where the char is, disengages and loses all enmity.

        private const byte PosModeNormal = 0, PosModeEvent = 1, PosModeClear = 2;

        private void OnEventEndXzy(MapSession s, CharData c, BasicPacket p)
        {
            uint result = BitConverter.ToUInt32(p.Buffer, 20);
            ushort eventId = BitConverter.ToUInt16(p.Buffer, 26);

            log_($"event: {c.Name} update {eventId} result {result} (with a position)");
            int updateResult = OnEventUpdate(c, eventId, result);
            bool updatePosition = c.LocalVars.GetValueOrDefault("noPosUpdate") == 0 && updateResult == 1;
            c.LocalVars["noPosUpdate"] = 0;

            if (updatePosition)
            {
                // GP_SERV_COMMAND_WPOS2 (EVENT) moves the char before it is written
                c.X = BitConverter.ToSingle(p.Buffer, 4);
                c.Y = BitConverter.ToSingle(p.Buffer, 8);
                c.Z = BitConverter.ToSingle(p.Buffer, 12);
                c.Rotation = p.Buffer[31];
                s.PacketList.AddLast(Wpos2(c, PosModeEvent));
                s.PacketList.AddLast(Wpos(c));
            }
            else
            {
                s.PacketList.AddLast(Wpos2(c, PosModeClear));
            }
            // a living pet goes with the char and disengages, and the mobs hating it (its notoriety) forget it
            if (c.PPet is MobEntity pet && pet.IsAlive && !pet.IsDead)
            {
                pet.X = c.X;
                pet.Y = c.Y;
                pet.Z = c.Z;
                pet.Rotation = c.Rotation;
                if (pet.Engaged) MobDisengage(pet, NowMs());
                foreach (var mob in pet.Notoriety) mob.Enmity.Clear(pet.Id);
            }
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.EventRecvPending));
        }

        // ---- GP_CLI_COMMAND_PASSWARDS ------------------------------------------------------------------------------
        // An event that asked for typed input (a password): the string goes to onEventUpdate in place of the result.

        private void OnPasswards(MapSession s, CharData c, BasicPacket p)
        {
            var span = p.Buffer.AsSpan(12, 16);
            int len = span.IndexOf((byte)0);
            var input = span.Slice(0, len < 0 ? 16 : len).ToArray();

            log_($"event: {c.Name} update {c.CurrentEvent.EventId} string {System.Text.Encoding.Latin1.GetString(input)}");
            RunEventScript(c, "onEventUpdate", (ushort)c.CurrentEvent.EventId, () => lua_.PushBytes(input));
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.EventRecvPending));
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.CancelInput));
        }

        // LoadEventScript: the event's script, else (in an instance) the instance's script
        // (./scripts/zones/{the instance's zone}/instances/{its name}), else the zone's. An event an instance's own script
        // starts outside any interaction (onInstanceComplete's exit event) has no script file of its own, so the
        // instance's onEventFinish answers it
        private void PushEventFunction(CharData c, string name)
        {
            scripts_.PushFileFunction(c.CurrentEvent.ScriptFile, name);
            if (!lua_.IsNil(-1)) return;
            lua_.Pop(1);
            if (c.Instance is { } run)
            {
                scripts_.PushFileFunction($"./scripts/zones/{run.Data.ZoneName}/instances/{run.Data.Name}", name);
                if (!lua_.IsNil(-1)) return;
                lua_.Pop(1);
            }
            scripts_.PushFileFunction($"./scripts/zones/{AreaOf(c).Name}/Zone.lua", name);
        }

        // luautils::OnEventUpdate / OnEventFinish: the event is what eventPreparation points at while the script runs
        private int RunEventScript(CharData c, string entry, ushort eventId, uint result) =>
            RunEventScript(c, entry, eventId, () => lua_.Push((double)result));

        private int RunEventScript(CharData c, string entry, ushort eventId, Action pushResult)
        {
            if (lua_ == null) return 0;
            var previous = c.EventPreparation;
            c.EventPreparation = c.CurrentEvent;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("InteractionGlobal." + entry);
                lua_.PushObject(c);
                lua_.Push((double)eventId);
                pushResult();
                lua_.PushObject(c.CurrentEvent.TargetEntity);
                PushEventFunction(c, entry);
                lua_.Call(5, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : (entry == "onEventUpdate" ? 1 : 0);
            }
            catch (LuaException e)
            {
                ScriptError(entry, e);
                return -1;
            }
            finally
            {
                lua_.SetTop(top);
                c.EventPreparation = previous;
            }
        }

        private int OnEventUpdate(CharData c, ushort eventId, uint result) => RunEventScript(c, "onEventUpdate", eventId, result);

        private int OnEventFinish(CharData c, ushort eventId, uint result)
        {
            if (c.CurrentEvent.TargetEntity != null) LocalVars(c.CurrentEvent.TargetEntity).Remove("pauseNPCPathing");
            // the C# quests' finish handlers this dispatch runs (RunHandler counts them; carry-harness-2): one quest
            // running two (two of its states both matching, the second once the first changed the status) is logged
            var outer = OpenFinishDispatch();
            try
            {
                return RunEventScript(c, "onEventFinish", eventId, result);
            }
            finally
            {
                CloseFinishDispatch(eventId, outer);
            }
        }

        // a finish dispatch opened: the one open before it (its tally and whether one was open), to put back at its
        // close. The new dispatch's tally is made by RunHandler at its first C# finish handler, so a dispatch that runs none
        // allocates nothing
        private (Dictionary<string, List<string>> Runs, bool Open) OpenFinishDispatch()
        {
            var outer = (finishRuns_, finishOpen_);
            finishRuns_ = null;
            finishOpen_ = true;
            return outer;
        }

        // the dispatch closed: the outer one's state back first, then a line for each quest that ran two finish
        // handlers or more in it
        private void CloseFinishDispatch(int eventId, (Dictionary<string, List<string>> Runs, bool Open) outer)
        {
            var runs = finishRuns_;
            (finishRuns_, finishOpen_) = outer;
            if (runs == null) return;
            foreach (var (key, labels) in runs)
                if (labels.Count > 1) log_(DoubleFinishLine(key, eventId, labels));
        }

        // a finish dispatch around run, as OnEventFinish opens one (a test entry: QuestScripts.Live's nested dispatch)
        internal void FinishDispatchForTest(int eventId, Action run)
        {
            var outer = OpenFinishDispatch();
            try
            {
                run();
            }
            finally
            {
                CloseFinishDispatch(eventId, outer);
            }
        }

        // ---- CCharEntity's event queue -----------------------------------------------------------------------------

        private void QueueEvent(MapSession s, CharData c, EventInfo e)
        {
            foreach (var queued in c.EventQueue)
            {
                if (queued.EventId == e.EventId)
                {
                    log_("zone: CCharEntity::queueEvent: Character attempted to start multiple of the same event.");
                    return;
                }
            }
            c.EventQueue.Add(e);
            TryStartNextEvent(s, c);
        }

        private void TryStartNextEvent(MapSession s, CharData c)
        {
            if (c.IsInEvent) return;
            if (c.EventQueue.Count == 0)
            {
                // the next PostTick's 0x0DF + 0x037
                SetAnimation(s, c, AnimationNone, always: true);
                return;
            }

            c.CurrentEvent = c.EventQueue[0];
            c.EventQueue.RemoveAt(0);
            c.EventPreparation = new EventInfo();
            if (c.Animation == AnimationHealing) c.StatusEffects.DelStatusEffect(EffectId.Healing);
            if (c.CurrentEvent.TargetEntity != null) LocalVars(c.CurrentEvent.TargetEntity)["pauseNPCPathing"] = 1;
            c.Locked = c.CurrentEvent.Type == EventType.Cutscene;

            var e = c.CurrentEvent;
            log_($"event: {c.Name} start {e.EventId} ({e.Type}) params [{string.Join(", ", e.Params)}]");
            if (e.Strings.Count == 0)
                s.PacketList.AddLast(e.Params.Count > 0 || e.TextTable != -1 ? EventPackets.EventNum(c, e) : EventPackets.Event(c, e));
            else
                s.PacketList.AddLast(EventPackets.EventStr(c, e));
            SetAnimation(s, c, AnimationEvent, always: true);
        }

        private void EndCurrentEvent(MapSession s, CharData c)
        {
            c.CurrentEvent = new EventInfo();
            c.EventPreparation = new EventInfo();
            c.Locked = false;
            TryStartNextEvent(s, c);
        }
    }
}
