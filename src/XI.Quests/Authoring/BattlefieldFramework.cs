// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section
// 3.3): the Lua battlefield framework (scripts/globals/battlefield.lua:382-1372), ported line for line. One method per
// Lua function, in the file's order, each marked with its line. The static methods are the framework's zone
// handlers (Battlefield.onEntryTrade, ...), which no battlefield owns; the instance methods are the Battlefield
// methods, with the battlefield (self) the context's Script, and each call the Lua makes through self:<method> goes
// through the battlefield's hook (BattlefieldScript), so an override takes it as a Lua file's does. Every engine call
// is the binding the Lua calls (IBattlefieldPlayer, IBattlefieldView), and every Lua global the Lua calls (npcUtil,
// xi.battlefield.*, utils.selectFromLootGroups) is called (IBattlefieldEngine). utils.mask and utils.minutes are
// plain arithmetic and are computed here.
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    // the xi constants the framework reads
    public static class BattlefieldConst
    {
        // xi.battlefield.status
        public const int StatusOpen = 0, StatusLocked = 1, StatusWon = 2, StatusLost = 3;
        // xi.battlefield.returnCode
        public const int Wait = 1, Cutscene = 2, IncrementRequest = 3, Locked = 4, ReqsNotMet = 5, BattlefieldFull = 6;
        // xi.battlefield.leaveCode
        public const int LeaveExit = 1, LeaveWon = 2, LeaveWarpDc = 3, LeaveLost = 4;
        // xi.effect.BATTLEFIELD, xi.effect.LEVEL_SYNC
        public const int EffectBattlefield = 254, EffectLevelSync = 269;
        // xi.msg.basic.WAIT_LONGER, xi.msg.basic.TIME_LEFT
        public const int MsgWaitLonger = 94, MsgTimeLeft = 202;
        // xi.status.DISAPPEAR
        public const int EntityDisappear = 2;
        // xi.effectFlag.ERASABLE, xi.effectFlag.WALTZABLE
        public const int FlagErasable = 0x2, FlagWaltzable = 0x4000;
        // xi.mod.MOGHANCEMENT_GIL_BONUS_P
        public const int ModMoghancementGilBonusP = 1158;
        // xi.item.GIL
        public const int ItemGil = 65535;
        // xi.msg.channel.NS_SHOUT
        public const int ChannelNsShout = 14;
        // xi.path.flag.PATROL
        public const int PathPatrol = 32;
        // the entry menu's mask with every battlefield (onEntryTrigger's GM menu)
        public const int AllOptions = 268435455;
    }

    // q.Text over the context's engine: both the live context and the fake's use this one
    public sealed class BattlefieldTextAccessor : IBattlefieldText
    {
        private readonly IBattlefieldContext q_;

        public BattlefieldTextAccessor(IBattlefieldContext q) => q_ = q;

        public int Id(ZoneText text)
        {
            if (q_.Script is { } script && text.Zone != script.Data.ZoneId)
                throw new InvalidOperationException(
                    $"text {text} is not of the battlefield's zone {script.Data.ZoneId}");
            return q_.Engine.Text(text.Zone, text.Name) ??
                   throw new InvalidOperationException($"zone {text.Zone} has no text {text.Name}");
        }
    }

    public sealed class BattlefieldFramework
    {
        private readonly IBattlefieldContext q_;

        public BattlefieldFramework(IBattlefieldContext q)
        {
            q_ = q ?? throw new ArgumentNullException(nameof(q));
            if (q.Script == null) throw new ArgumentException("the framework's methods need a battlefield", nameof(q));
        }

        private BattlefieldScript S => q_.Script;
        private BattlefieldData D => S.Data;
        private IBattlefieldPlayer Player => q_.Player ?? throw new InvalidOperationException("no player in this call");
        private IBattlefieldView Bf =>
            q_.Battlefield ?? throw new InvalidOperationException("no battlefield in this call");

        // the coverage hook of a hook: reached when the battlefield's own class overrides it
        public static void Hook(IBattlefieldContext q, string hook)
        {
            if (q.Script != null && q.Script.Overrides(hook)) q.Reached("hook:" + hook);
        }

        // utils.mask.getBit(mask, pos)
        private static bool MaskGetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // utils.mask.setBit(mask, pos, true)
        private static int MaskSetBit(int mask, int pos) => mask | (1 << pos);

        // utils.minutes(n)
        private static int Minutes(int n) => n * 60;

        // Container:getLocalVar(player, name) / setLocalVar, getVar / setVar: the name behind the battlefield's prefix
        private string Prefixed(string name) => $"Battlefield[{D.BattlefieldId}]{name}";

        // the zone's text by name (zones[zone].text[name]); a nil text is an error, as a nil id is at the binding
        private static int TextId(IBattlefieldContext q, int zone, string name) =>
            q.Engine.Text(zone, name) ?? throw new InvalidOperationException($"zone {zone} has no text {name}");

        // battlefield.lua:417 (Battlefield:new) and :477 (Battlefield.getVarPrefix) are the battlefield's data and its
        // prefix (BattlefieldData, Prefixed); :481 (Battlefield:register) is the bridge's registration
        // (ZoneHost.BattlefieldScripts) with its hook rules in RegisterHooks

        // battlefield.lua:481: the hooks register gives each battlefield of a zone, in the zone's registration order:
        // the events (onEventUpdate 32000 and 32003, onEventFinish 32000 to 32005) only to the first, the entry NPC's
        // onTrade and onTrigger only where no earlier battlefield names one of its entry NPCs, and the exit NPCs'
        // always.
        // reloaded: the battlefield re-registers over its own earlier registration that took the events (the reload
        // clause), which then takes both again
        public static (bool SetupEvents, bool SetupEntryNpcs) RegisterHooks(
            IReadOnlyList<(int BattlefieldId, IReadOnlyList<string> EntryNpcs, bool HasListeners)> earlier,
            int battlefieldId, IReadOnlyList<string> entryNpcs)
        {
            bool setupEvents = true;
            bool setupEntryNpcs = true;
            foreach (var content in earlier)
            {
                // Always setup listeners if we're reloading a battlefield
                if (battlefieldId == content.BattlefieldId && content.HasListeners)
                {
                    setupEvents = true;
                    setupEntryNpcs = true;
                    break;
                }
                // Do not setup events if there are any other battlefields
                setupEvents = false;
                // Do not setup npcs if there is another battlefield using the same entry npc
                if (entryNpcs != null)
                    foreach (var entryNpc in entryNpcs)
                        if (content.EntryNpcs != null && content.EntryNpcs.Contains(entryNpc))
                        {
                            setupEntryNpcs = false;
                            break;
                        }
            }
            return (setupEvents, setupEntryNpcs);
        }

        // battlefield.lua:571
        public bool IsValidEntry() => q_.Npc != null && (D.EntryNpcs ?? Array.Empty<string>()).Contains(q_.Npc.Name);

        // battlefield.lua:577
        public bool EntryRequirement(bool isRegistrant) => true;

        // battlefield.lua:581
        public bool CheckRequirements(bool isRegistrant)
        {
            if (!IsValidEntry()) return false;

            // Do not show battlefields to registrant when either they don't require items and player is trading or
            // that do require items and but player is not trading
            if (isRegistrant && (q_.Trade == null) != (D.TradeItems.Length == 0)) return false;

            // Everyone must hold the required key items by default.
            // Key items are not consumed here, only checked, consumption is handled in onBattlefieldEnter
            bool needsKeyItems = true;

            // Battlefields flagged 'onlyInitiator' only require them from the battlefield initiator to enter.
            var kis = D.RequiredKeyItems;
            if (kis != null && kis.OnlyInitiator) needsKeyItems = isRegistrant;

            if (needsKeyItems && kis != null)
                foreach (var keyItem in kis.Items)
                {
                    if (keyItem.IsTable)
                    {
                        // Some battlefields may be entered with one of several key items. (e.g. Requiem of Sin)
                        bool hasAnyKeyItem = false;
                        foreach (var subitem in keyItem.Ids)
                            if (Player.HasKeyItem(subitem))
                            {
                                hasAnyKeyItem = true;
                                break;
                            }
                        if (!hasAnyKeyItem) return false;
                    }
                    else if (!Player.HasKeyItem(keyItem.Ids[0]))
                        return false;
                }

            if (q_.Trade != null && D.TradeItems.Length > 0)
                if (!q_.Trade.HasExactly(D.TradeItems)) return false;

            // Additional Requirements that may be necessary for battlefield entry
            // contained within the script itself, defaults to True
            Hook(q_, nameof(BattlefieldScript.EntryRequirement));
            return S.EntryRequirement(q_, isRegistrant);
        }

        // battlefield.lua:639
        public bool CheckSkipCutscene() => false;

        // battlefield.lua:643: the action (the entry menu) or null
        public static IBattlefieldAction OnEntryTrade(IBattlefieldContext q, bool onUpdate = false)
        {
            var player = q.Player;
            // Check if player's party has level sync
            if (q.Engine.RejectLevelSyncedParty(q)) return null;

            // Validate trade
            if (q.Trade == null) return null;

            // Validate battlefield status
            if (player.HasStatusEffect(BattlefieldConst.EffectBattlefield) && !onUpdate)
            {
                player.MessageBasic(BattlefieldConst.MsgWaitLonger, 0, 0);
                return null;
            }

            // Check if another party member has battlefield status effect. If so, don't allow trade.
            var alliance = player.AllianceList();
            foreach (var member in alliance)
                if (member.HasStatusEffect(BattlefieldConst.EffectBattlefield))
                {
                    player.MessageBasic(BattlefieldConst.MsgWaitLonger, 0, 0);
                    return null;
                }

            int zoneId = player.ZoneId;

            // Determine which battlefields are available given the traded items
            int options = q.Engine.BattlefieldOptions(q, true);

            if (options == 0)
            {
                if (q.Engine.Text(zoneId, "NO_BATTLEFIELD_ENTRY") is { } noEntryMessage)
                    player.MessageSpecial(noEntryMessage);
                return null;
            }

            // Ensure that the traded item(s) are not worn out
            var contents = q.Engine.ZoneContents(zoneId) ??
                           throw new InvalidOperationException($"zone {zoneId} has no battlefields");
            foreach (var content in contents)
            {
                if (content.RequiredItemCount > 0 &&
                    (content.WornMessage != null || content.WornMessageTable != null) &&
                    content.TradeHas(q.Trade))
                {
                    int itemId = content.FirstRequiredItem;
                    // Gets the total number of item uses for the given item. Default to one since that is the majority.
                    int totalUses = q.Engine.ItemUses(itemId) ?? 1;

                    if (player.WornUses(itemId) >= totalUses)
                    {
                        content.Reached(BattlefieldScript.LabelWorn);
                        if (content.WornMessageTable is { } table)
                            player.MessageSpecial(table[0], table.Skip(1).ToArray());
                        else if (totalUses > 1) player.MessageSpecial(content.WornMessage.Value, itemId);
                        else player.MessageSpecial(content.WornMessage.Value, 0, 0, 0, itemId);
                        return null;
                    }
                }
            }

            // Open menu of valid battlefields
            if (!onUpdate) return OpenEntryMenu(q, options, false);
            return null;
        }

        // battlefield.lua:721: Helper function to open the battlefield entry menu for the player
        public static IBattlefieldAction OpenEntryMenu(IBattlefieldContext q, int options, bool enterExisting)
        {
            q.Player.SetLocalVar("[BCNM]EnterExisting", enterExisting ? 1 : 0);
            q.Player.SetLocalVar("[BCNM]MenuOptions", options);
            return q.Engine.Event(32000, 0, 0, 0, options, 0, 0, 0, 0);
        }

        // battlefield.lua:728
        public static IBattlefieldAction OnEntryTrigger(IBattlefieldContext q)
        {
            var player = q.Player;
            // Cannot enter if anyone in party is level/master sync'd
            if (q.Engine.RejectLevelSyncedParty(q)) return null;

            // Player has battlefield status effect. That means a battlefield is open OR the player is inside one.
            if (player.HasStatusEffect(BattlefieldConst.EffectBattlefield))
            {
                // Player is outside battlefield. Attempting to enter.
                var status = player.StatusEffect(BattlefieldConst.EffectBattlefield);
                int id = status.Power;
                var content = q.Engine.Content(id);
                if (content == null) return null;

                if (!content.CheckRequirements(q, false, false)) return null;

                int enterOptions = MaskSetBit(0, content.Index);
                return OpenEntryMenu(q, enterOptions, true);
            }

            // Player doesn't have battlefield status effect. That means player wants to register a new battlefield OR
            // is attempting to enter a closed one. Check if another party member has battlefield status effect. If so,
            // that means the player is trying to enter a closed battlefield.
            int zone = player.ZoneId;
            var alliance = player.AllianceList();
            foreach (var member in alliance)
                if (member.HasStatusEffect(BattlefieldConst.EffectBattlefield))
                {
                    // A member registered for the fight lost clearance when it started, anyone else never had it
                    var registered = player.RegisteredBattlefield;
                    if (registered != null && registered.Status != BattlefieldConst.StatusOpen)
                        player.MessageText(q.Npc, TextId(q, zone, "CLEARED_BUT_MEMBERS_ENGAGED"), false);
                    else
                        player.MessageSpecial(TextId(q, zone, "PARTY_MEMBERS_ARE_ENGAGED"));
                    return null;
                }

            // No one in party/alliance has battlefield status effect. We want to register a new battlefield.
            int options = q.Engine.BattlefieldOptions(q, false);

            // GMs get access to all BCNMs with visible GM
            if (player.GMLevel > 0 && player.VisibleGMLevel >= 3) options = BattlefieldConst.AllOptions;

            if (options == 0)
            {
                if (q.Engine.Text(player.ZoneId, "NO_BATTLEFIELD_ENTRY") is { } noEntryMessage)
                    player.MessageSpecial(noEntryMessage);
                return null;
            }

            return OpenEntryMenu(q, options, false);
        }

        // battlefield.lua:795: Static function to lookup the correct battlefield to handle this event update; false
        // or null (nil), as the Lua returns
        public static bool? RedirectEventUpdate(IBattlefieldContext q, int csid, int option)
        {
            var player = q.Player;
            if (option == 0 || option == 255) return false;

            var contents = q.Engine.ZoneContents(player.ZoneId);
            int value = (option >> 4) & 0x1F;

            // Only the battlefields the menu was opened with can be registered from it
            if (!MaskGetBit(player.LocalVar("[BCNM]MenuOptions"), value))
            {
                player.UpdateEvent(BattlefieldConst.ReqsNotMet);
                // checked in GP_CLI_COMMAND_EVENTENDXZY
                player.SetLocalVar("noPosUpdate", 1);
                return false;
            }

            if (contents == null) throw new InvalidOperationException($"zone {player.ZoneId} has no battlefields");
            foreach (var content in contents)
                if (value == content.Index)
                {
                    content.OnEntryEventUpdate(q, csid, option);
                    break;
                }
            return null;
        }

        // battlefield.lua:825: The entry event asks for the zone's arenas in order. Bits 0 to 3 of the option hold the
        // arena the client wants to move into and bits 4 to 8 the menu index. The client tries the next arena on WAIT
        // (or no reply) then restarts from the first arena on INCREMENT_REQUEST, and prints the matching message and
        // stops on LOCKED, REQS_NOT_MET and BATTLEFIELD_FULL. Registering the arena that was asked for keeps the
        // battlefield and the position the client moves to in sync
        public int OnEntryEventUpdate(int csid, int option)
        {
            var player = Player;
            int requestedArea = option & 0xF;
            if (D.Area == 0 && (requestedArea < 1 || requestedArea > q_.Engine.MaxAreas(player.ZoneId)))
            {
                player.UpdateEvent(BattlefieldConst.ReqsNotMet);
                player.SetLocalVar("noPosUpdate", 1);
                return 0;
            }

            int clearTime = 1;
            string name = "Meme";
            int partySize = 1;
            int area = D.Area != 0 ? D.Area : requestedArea;

            // allowedAreas disables arenas whose mobs are not placed yet, the client moves on as if they were taken
            if (D.AllowedAreas != null && !D.AllowedAreas.Contains(area))
            {
                player.UpdateEvent(BattlefieldConst.Wait);
                player.SetLocalVar("noPosUpdate", 1);
                return 0;
            }

            int result = player.RegisterBattlefield(D.BattlefieldId, area, player.Id, q_.Self);
            int status = BattlefieldConst.StatusOpen;

            if (result != BattlefieldConst.Cutscene)
            {
                if (result == BattlefieldConst.IncrementRequest) result = BattlefieldConst.Wait;
                // If you cant enter now, you cant enter by trying again right now...
                else if (result == BattlefieldConst.ReqsNotMet)
                    player.DelStatusEffect(BattlefieldConst.EffectBattlefield);

                player.UpdateEvent(result);

                // TODO: Remove the localVar when issue with function return is resolved
                player.SetLocalVar("noPosUpdate", 1);
                return 0;
            }

            // Only allow entrance if battlefield is open and player has battlefield effect, witch can be lost mid
            // battlefield selection.
            if (player.CurrentBattlefield == null && player.HasStatusEffect(BattlefieldConst.EffectBattlefield))
                player.EnterBattlefield();

            // Handle record
            int initiatorId = 0;
            var battlefield = player.CurrentBattlefield;

            if (battlefield != null)
            {
                (name, clearTime, partySize) = battlefield.Record();
                initiatorId = battlefield.Initiator().Id;
            }

            // Register party members
            if (initiatorId == player.Id)
            {
                var effect = player.StatusEffect(BattlefieldConst.EffectBattlefield);
                int zone = player.ZoneId;

                // Handle traded items if not wearing them
                if (D.RequiredItems?.WearMessage == null && D.TradeItems.Length > 0)
                    if (!(D.RequiredItems?.Keep ?? false)) player.TradeComplete();

                // Handle party/alliance members
                var alliance = player.AllianceList();
                foreach (var member in alliance)
                    if (member.ZoneId == zone &&
                        member.EntityStatus != BattlefieldConst.EntityDisappear &&
                        !member.HasStatusEffect(BattlefieldConst.EffectBattlefield) &&
                        member.CurrentBattlefield == null)
                    {
                        member.CopyStatusEffect(effect);
                        member.RegisterBattlefield(D.BattlefieldId, area, player.Id, q_.Self);
                    }
            }

            // The client only skips the cutscene when this equals 100 plus the menu index it is given
            int autoSkipCS = player.LocalVar(Prefixed("CS")) == 1 ? 100 + D.Index : 0;
            Hook(q_, nameof(BattlefieldScript.CheckSkipCutscene));
            bool skip = S.CheckSkipCutscene(q_);
            player.UpdateEventValues(result, D.Index, autoSkipCS, clearTime, partySize, skip, D.CsParam7, D.CsParam8);
            player.UpdateEventString(name);

            return status < BattlefieldConst.StatusLocked && result < BattlefieldConst.Locked ? 1 : 0;
        }

        // battlefield.lua:925: content[eventName](content, player, csid, option), the battlefield the player is in or
        // last entered
        public static void RedirectEventCall(IBattlefieldContext q, string eventName, int csid, int option)
        {
            var player = q.Player;
            int battlefieldId;
            var battlefield = player.CurrentBattlefield;

            if (battlefield != null) battlefieldId = battlefield.Id;
            else battlefieldId = player.LocalVar("battlefieldID");

            if (battlefieldId == 0) return;

            var content = q.Engine.Content(battlefieldId) ??
                          throw new InvalidOperationException($"attempt to index a nil content ({battlefieldId})");
            content.Call(eventName, q, csid, option);
        }

        // battlefield.lua:943
        public void OnEventFinishEnter(int csid, int option)
        {
            // The menu also closes for players that were granted no arena
            if (Player.CurrentBattlefield == null) return;

            Player.SetEnteredBattlefield(true);
            Player.SetLocalVar(Prefixed("CS"), 1);
        }

        // battlefield.lua:953
        public void OnEventFinishWin(int csid, int option)
        {
            if (D.Title != 0) Player.AddTitle(D.Title);

            if (D.GrantXP != 0)
            {
                if (D.GrantXPLockout)
                {
                    if (Player.PlayerVar(Prefixed("XP")) > Player.SystemTime) return;
                    Player.SetPlayerVar(Prefixed("XP"), Player.JstMidnight);
                }

                Player.AddExp(D.GrantXP);
            }
        }

        // battlefield.lua:971
        public static IBattlefieldAction OnExitTrigger(IBattlefieldContext q)
        {
            if (q.Player.CurrentBattlefield != null) return q.Engine.ExitCutscene();
            return null;
        }

        // battlefield.lua:978: The exit event only wants its updates acknowledged, the player leaves on finish
        public void OnExitEventUpdate(int csid, int option)
        {
        }

        // battlefield.lua:981
        public void OnEventFinishLeave(int csid, int option) => Player.LeaveBattlefield(1);

        // battlefield.lua:985
        public void OnEventFinishExit(int csid, int option)
        {
            if (option == 4 && Player.CurrentBattlefield != null) Player.LeaveBattlefield(1);
        }

        // battlefield.lua:991
        public void OnEventFinishBattlefield(int csid, int option)
        {
        }

        // battlefield.lua:996: Override this function if necessary to perform additional steps in battlefield
        // Initialize.
        public void SetupBattlefield()
        {
        }

        // battlefield.lua:999
        public void OnBattlefieldInitialize()
        {
            var battlefield = Bf;
            bool hasMultipleAreas = D.Area == 0;
            battlefield.AddGroups(q_.Self, hasMultipleAreas);

            // Experimental battlefields are at most partially implemented. Increase mob levels for temporary tuning.
            if (D.Experimental)
            {
                var battlefieldMobs = battlefield.Mobs(true, true);
                foreach (var mobObj in battlefieldMobs) mobObj.SetMobLevel(Math.Min(mobObj.MainLevel * 2, 255));
            }

            foreach (var path in Paths())
            {
                var mEntity = q_.Engine.MobById(path.MobId);
                mEntity?.PathThrough(path.Points, BattlefieldConst.PathPatrol);
            }

            Hook(q_, nameof(BattlefieldScript.SetupBattlefield));
            S.SetupBattlefield(q_);
        }

        // the content's paths (self.paths) with their mob ids resolved, through the content table
        private IEnumerable<(int MobId, double[] Points)> Paths() =>
            q_.Self is IBattlefieldPaths p ? p.ResolvedPaths() : Array.Empty<(int, double[])>();

        // battlefield.lua:1022
        public void OnBattlefieldTick(int tick)
        {
            var battlefield = Bf;
            int status = battlefield.Status;
            double cutsceneTimer = battlefield.LocalVar("cutsceneTimer");
            bool isExiting = status == BattlefieldConst.StatusLost || status == BattlefieldConst.StatusWon;

            if (isExiting) battlefield.SetLocalVar("cutsceneTimer", cutsceneTimer - 1);

            // Check that players haven't all died or that their dead time is over.
            var players = battlefield.Players();
            Hook(q_, nameof(BattlefieldScript.HandleWipe));
            S.HandleWipe(q_, players);

            bool hasTimeRemaining = q_.Engine.SendTimePrompts(battlefield, players);

            if (!hasTimeRemaining)
            {
                foreach (var player in players)
                    player.MessageSpecial(TextId(q_, player.ZoneId, "TIME_IN_THE_BATTLEFIELD_IS_UP"));

                battlefield.SetStatus(BattlefieldConst.StatusLost);
                isExiting = true;
            }

            if (isExiting)
            {
                // Remaining in battlefield for an extended time
                if (cutsceneTimer > 0) return;

                if (status == BattlefieldConst.StatusLost)
                    foreach (var player in players)
                        player.MessageSpecial(TextId(q_, player.ZoneId, "PARTY_MEMBERS_HAVE_FALLEN"));

                battlefield.Cleanup(true);
            }
        }

        // battlefield.lua:1062
        public void OnBattlefieldRegister()
        {
        }

        // battlefield.lua:1065
        public void OnBattlefieldStatusChange(int status)
        {
            var battlefield = Bf;
            // Remove battlefield effect for players in alliance not inside battlefield once the battlefield gets
            // locked. Do this only once.
            if (status == BattlefieldConst.StatusLocked && battlefield.LocalVar("statusRemoval") == 0)
            {
                battlefield.SetLocalVar("statusRemoval", 1);
                var players = battlefield.Players();

                foreach (var player in players)
                {
                    var alliance = player.AllianceList();
                    foreach (var member in alliance)
                        if (member.HasStatusEffect(BattlefieldConst.EffectBattlefield) &&
                            member.CurrentBattlefield == null)
                            member.DelStatusEffect(BattlefieldConst.EffectBattlefield);
                }
            }
        }

        // battlefield.lua:1090
        public void BattlefieldEntry()
        {
        }

        // Battlefield.onBattlefieldEnter as a content reaches it: through a Lua module's override when one is applied
        // (modules/era/lua/globals/testimony_single_use.lua), whose last super is the framework's own method below,
        // as it is for a Lua content; else the framework's own method
        public void OnBattlefieldEnter()
        {
            if (q_.Engine.ModuleOverride("onBattlefieldEnter", q_, q => q.Framework.OnBattlefieldEnterOwn())) return;
            OnBattlefieldEnterOwn();
        }

        // battlefield.lua:1094
        public void OnBattlefieldEnterOwn()
        {
            var player = Player;
            var battlefield = Bf;
            player.SetLocalVar("battlefieldID", battlefield.Id);

            int initiatorId = battlefield.Initiator().Id;

            // Handle key item consumption. Runs for every player that enters the battlefield. If onlyInitiator is true,
            // then only the player that registered the battlefield will have their key item consumed.
            var kis = D.RequiredKeyItems;
            if (kis is { Items.Count: > 0 } && (!kis.OnlyInitiator || player.Id == initiatorId))
            {
                q_.Reached(BattlefieldScript.LabelKeyItems);
                var items = new List<int>();

                foreach (var item in kis.Items)
                {
                    // If this item is a table then that means we only need one of them so delete the first one we find
                    if (item.IsTable)
                    {
                        foreach (var subitem in item.Ids)
                            if (player.HasKeyItem(subitem))
                            {
                                if (!kis.Keep) player.DelKeyItem(subitem);
                                items.Add(subitem);
                            }
                    }
                    else
                    {
                        if (!kis.Keep) player.DelKeyItem(item.Ids[0]);
                        items.Add(item.Ids[0]);
                    }
                }

                if (kis.Message is { } message)
                {
                    int id = TextId(q_, message.Zone, message.Name);
                    if (kis.MessageArgs != null) player.MessageSpecial(id, kis.MessageArgs);
                    else player.MessageSpecial(id, items.ToArray());
                }

                if (!kis.Keep && kis.DeleteMessage is { } deleteMessage)
                {
                    int id = TextId(q_, deleteMessage.Zone, deleteMessage.Name);
                    foreach (var keyItemId in items) player.MessageSpecial(id, keyItemId);
                }
            }

            var required = D.RequiredItems;
            if (player.Id == initiatorId && required?.WearMessage is { } wear && player.HasItem(required.Items[0]))
            {
                q_.Reached(BattlefieldScript.LabelWear);
                int itemId = required.Items[0];
                int uses = player.IncrementItemWear(itemId);
                // Gets number of item uses. (Tests = 3; Else = 1)
                int totalUses = q_.Engine.ItemUses(itemId) ?? 1;
                int wearMessage = TextId(q_, wear.Zone, wear.Name);

                if (totalUses > 1)
                {
                    int remaining = totalUses - uses;
                    player.MessageSpecial(wearMessage, itemId, remaining + 1, remaining);
                }
                else player.MessageSpecial(wearMessage, 0, 0, 0, itemId);
            }

            // The Lua's commented-out first-cast listener (mobs waiting for the initiator's cutscene) is not ported.

            player.MessageSpecial(TextId(q_, D.ZoneId, "ENTERING_THE_BATTLEFIELD_FOR"), 0, D.Index);

            if (D.MaxPlayers > 6)
            {
                // Update tooling does not allow for duplicate messages to be stored in IDs.lua, even if the ID is
                // different. Apollyon does not differentiate between party and alliance for message, so if the entry
                // is nil, use party message.
                int messageId = q_.Engine.Text(D.ZoneId, "MEMBERS_OF_YOUR_ALLIANCE") ??
                                TextId(q_, D.ZoneId, "MEMBERS_OF_YOUR_PARTY");
                player.MessageSpecial(messageId, 0, 0, 0, D.MaxPlayers);
            }
            else player.MessageSpecial(TextId(q_, D.ZoneId, "MEMBERS_OF_YOUR_PARTY"), 0, 0, 0, D.MaxPlayers);

            player.MessageSpecial(TextId(q_, D.ZoneId, "TIME_LIMIT_FOR_THIS_BATTLE_IS"), 0, 0, 0,
                                  (int)Math.Floor(D.TimeLimit / 60.0));

            if (D.Experimental)
                player.PrintToPlayer("This battlefield has been marked as experimental.  Enemy levels have increased!",
                                     BattlefieldConst.ChannelNsShout);

            Hook(q_, nameof(BattlefieldScript.BattlefieldEntry));
            S.BattlefieldEntry(q_);
        }

        // battlefield.lua:1207
        public void OnBattlefieldDestroy()
        {
        }

        // battlefield.lua:1211
        public void OnBattlefieldLeave(int leaveCode)
        {
            if (D.CleanupDebuffs && leaveCode == BattlefieldConst.LeaveWon)
                Player.DelStatusEffectsByFlag(BattlefieldConst.FlagErasable + BattlefieldConst.FlagWaltzable);

            if (leaveCode == BattlefieldConst.LeaveWon)
            {
                Hook(q_, nameof(BattlefieldScript.OnBattlefieldWin));
                S.OnBattlefieldWin(q_);
            }
            else if (leaveCode == BattlefieldConst.LeaveLost)
            {
                Hook(q_, nameof(BattlefieldScript.OnBattlefieldLoss));
                S.OnBattlefieldLoss(q_);
            }
        }

        // battlefield.lua:1226
        public void OnBattlefieldWin()
        {
            var battlefield = Bf;
            var (_, clearTime, partySize) = battlefield.Record();

            Player.SetLocalVar("battlefieldWin", battlefield.Id);
            Player.StartEvent(32001, battlefield.Area, clearTime, partySize, battlefield.TimeInside, Player.ZoneId,
                              D.Index, 0);
        }

        // battlefield.lua:1233
        public void OnBattlefieldLoss() => Player.StartEventTable(32002, D.LossEventParams ?? Array.Empty<int>());

        // battlefield.lua:1237
        public void OnBattlefieldKick() => Player.StartEventTable(32002, D.LossEventParams ?? Array.Empty<int>());

        // battlefield.lua:1241
        public void HandleWipe(IReadOnlyList<IBattlefieldPlayer> players)
        {
            var battlefield = Bf;
            int wipeTime = battlefield.WipeTime;
            int elapsed = battlefield.TimeInside;

            // If party has not yet wiped.
            if (wipeTime <= 0)
            {
                // Return if any players are still alive
                foreach (var player in players)
                    if (player.IsAlive) return;

                Hook(q_, nameof(BattlefieldScript.OnBattlefieldWipe));
                S.OnBattlefieldWipe(q_, players);
            }
            // Party has already wiped.
            else
            {
                if (elapsed - wipeTime > Minutes(3)) battlefield.SetStatus(BattlefieldConst.StatusLost);
                else
                {
                    // Cancel the wipe timer if any players are alive once again
                    foreach (var player in players)
                        if (player.IsAlive)
                        {
                            battlefield.SetWipeTime(0);
                            break;
                        }
                }
            }
        }

        // battlefield.lua:1272
        public void OnBattlefieldWipe(IReadOnlyList<IBattlefieldPlayer> players)
        {
            var battlefield = Bf;
            // Party has wiped. Save and send time remaining before being booted.
            if (D.HasWipeGrace)
            {
                foreach (var player in players)
                    player.MessageSpecial(TextId(q_, player.ZoneId, "THE_PARTY_WILL_BE_REMOVED"), 0, 0, 0, 3);

                battlefield.SetWipeTime(battlefield.TimeInside);
            }
            else battlefield.SetStatus(BattlefieldConst.StatusLost);
        }

        // battlefield.lua:1285: the groups addEssentialMobs adds to self.groups, after the content's own
        // (content.groups), in call order: { mobs = mobNames, superlink = true, isParty = true, allDeath =
        // handleAllMonstersDefeated }, plus the spawned a file sets on the group after the call
        public static IReadOnlyList<BattlefieldGroup> AddEssentialMobs(BattlefieldData d)
        {
            var groups = new List<BattlefieldGroup>(d.Groups);
            for (int i = 0; i < d.EssentialMobs.Count; i++)
                groups.Add(new BattlefieldGroup
                {
                    Mobs = d.EssentialMobs[i].Mobs, Spawned = d.EssentialMobs[i].Spawned, Superlink = true,
                    IsParty = true, EssentialIndex = i + 1,
                });
            return groups;
        }

        // battlefield.lua:1294
        public void HandleAllMonstersDefeated(IBattlefieldMob mob)
        {
            var battlefield = Bf;
            int? crateId = battlefield.ArmouryCrate;

            // Default to core returning us a value, but if that fails, use the fallback definition if available. This
            // should only be used where mobIds table has to be used instead of mobs!
            if (crateId == 0 && q_.Self.HasArmouryCrates) crateId = q_.Self.ArmouryCrate(battlefield.Area);

            if (crateId != 0)
            {
                var crate = q_.Engine.NpcById(crateId ?? throw new InvalidOperationException("GetNPCByID(nil)"));

                if (crate != null)
                {
                    battlefield.InsertEntity(crate.TargId, false, true);
                    q_.Engine.ShowCrate(crate);
                    q_.Engine.AddCrateListener(crate, q_.Self);
                }
            }
            else
            {
                battlefield.SetLocalVar("cutsceneTimer", D.DelayToExit);
                battlefield.SetStatus(BattlefieldConst.StatusWon);
            }
        }

        // battlefield.lua:1318
        public void HandleOpenArmouryCrate(IBattlefieldNpc npc)
        {
            // the callback's own call holds the player and the npc (the closure's upvalues)
            q_.Engine.OpenCrate(q_, npc, inner =>
            {
                var player = inner.Player;
                var battlefield = player.CurrentBattlefield;
                Hook(inner, nameof(BattlefieldScript.HandleLootRolls));
                S.HandleLootRolls(inner, battlefield, inner.Self.Loot, inner.Npc,
                                  player.Mod(BattlefieldConst.ModMoghancementGilBonusP));
                battlefield.SetStatus(BattlefieldConst.StatusWon);
                battlefield.SetLocalVar("cutsceneTimer", D.DelayToExit);

                return true;
            });
        }

        // battlefield.lua:1329: the loot label is reached when the battlefield has loot of its own (as before the
        // pool became a parameter), whichever table the call draws from
        public void HandleLootRolls(IBattlefieldView battlefield, BattlefieldLootTable lootTable, IBattlefieldNpc npc,
                                    int gilBonusMod)
        {
            if (D.Loot.Count > 0) q_.Reached(BattlefieldScript.LabelLoot);
            var players = battlefield.Players();
            var firstPlayer = players.Count > 0 ? players[0] : null;

            var selectedLoot = q_.Engine.SelectFromLootGroups(firstPlayer, lootTable);
            foreach (var entry in selectedLoot)
            {
                if (entry.ItemId != BattlefieldConst.ItemGil) firstPlayer.AddTreasure(entry.ItemId, npc);
                else
                {
                    double gilBonusPct = (100 + gilBonusMod) / 100.0;
                    double amount = entry.Amount ?? throw new InvalidOperationException("a gil entry with no amount");
                    double gilPerPlayer = amount * gilBonusPct / players.Count;

                    foreach (var player in players) q_.Engine.GiveCurrency(player, "gil", gilPerPlayer, true);
                }
            }
        }

        // battlefield.lua:1348 (xi.battlefield.getBattlefieldOptions) and :1372 (xi.battlefield.rejectLevelSyncedParty)
        // are globals of the xi.battlefield table: the handlers call them as Lua (IBattlefieldEngine), as the Lua ones
        // do, so they reach every content of the zone, Lua or C#, through its checkRequirements
    }

    // battlefield.lua:1388: BattlefieldMission, the Battlefield of a mission fight. Its four methods, ported line for
    // line; the battlefield's data is a BattlefieldMissionData (battlefield.lua:1406, BattlefieldMission:new). Every
    // other method is Battlefield's (BattlefieldFramework), as the Lua's __index makes it
    public sealed class BattlefieldMission
    {
        private readonly IBattlefieldContext q_;

        public BattlefieldMission(IBattlefieldContext q)
        {
            q_ = q ?? throw new ArgumentNullException(nameof(q));
            if (q.Script?.Data is not BattlefieldMissionData)
                throw new ArgumentException("BattlefieldMission's methods need a mission battlefield", nameof(q));
        }

        private BattlefieldMissionData D => (BattlefieldMissionData)q_.Script.Data;
        // self.mission (the validator refuses a mission battlefield without one)
        private int Mission =>
            D.Mission ?? throw new InvalidOperationException("a mission battlefield with no mission");
        private IBattlefieldPlayer Player => q_.Player ?? throw new InvalidOperationException("no player in this call");
        private IBattlefieldView Bf =>
            q_.Battlefield ?? throw new InvalidOperationException("no battlefield in this call");

        // self.missionArea or player:getNation()
        private int MissionArea => D.MissionArea is { } area ? (int)area : Player.Nation;
        // self.missionStatusArea or player:getNation()
        private int MissionStatusArea => D.MissionStatusArea is { } area ? (int)area : Player.Nation;
        // player:getMissionStatus(missionStatusArea, self.missionStatus): a nil position reads the whole status
        private int MissionStatusOf(int missionStatusArea) => D.MissionStatus is { } pos
            ? Player.MissionStatus(missionStatusArea, pos)
            : Player.MissionStatus(missionStatusArea);

        // battlefield.lua:1425
        public bool CheckRequirements(bool isRegistrant)
        {
            if (!q_.Framework.CheckRequirements(isRegistrant)) return false;

            var player = Player;
            int missionArea = MissionArea;
            int current = player.CurrentMission(missionArea);

            if (D.RequiredMissionStatus is { } requiredMissionStatus)
            {
                int missionStatusArea = MissionStatusArea;
                int status = MissionStatusOf(missionStatusArea);

                return (!isRegistrant && (player.HasCompletedMission(missionArea, Mission) ||
                                          (current == Mission && status >= requiredMissionStatus))) ||
                       (current == Mission && status == requiredMissionStatus);
            }
            else if (D.RequiredVar != null)
            {
                // COP Missions use a charVar to track since missionStatus is used to define menu settings.
                // When converting these, allow for the same structure of status, but with defined requiredVar (str)
                // and requiredValue (uint requirement).  These are currently mutually exclusive, and treated
                // as such.

                int status = player.CharVar(D.RequiredVar);

                return (!isRegistrant && (player.HasCompletedMission(missionArea, Mission) ||
                                          (current == Mission && status >= D.RequiredValue))) ||
                       (current == Mission && status == D.RequiredValue);
            }
            else
            {
                return (!isRegistrant && player.HasCompletedMission(missionArea, Mission)) ||
                       current == Mission;
            }
        }

        // battlefield.lua:1458
        public bool CheckSkipCutscene()
        {
            var player = Player;
            int missionArea = MissionArea;
            int current = player.CurrentMission(missionArea);
            int missionStatusArea = MissionStatusArea;
            int status = MissionStatusOf(missionStatusArea);

            return player.HasCompletedMission(missionArea, Mission) ||
                   (D.RequiredMissionStatus != null && current == Mission && status > D.SkipStatus) ||
                   (D.RequiredVar != null && current == Mission && player.CharVar(D.RequiredVar) > D.RequiredValue);
        }

        // battlefield.lua:1470
        public void OnBattlefieldWin()
        {
            var player = Player;
            var battlefield = Bf;
            int missionArea = MissionArea;
            int current = player.CurrentMission(missionArea);
            int missionStatusArea = MissionStatusArea;

            if (current == Mission) player.SetLocalVar("battlefieldWin", battlefield.Id);

            var (_, clearTime, partySize) = battlefield.Record();
            int canSkipCS = player.HasCompletedMission(missionArea, Mission) ||
                            (D.RequiredMissionStatus != null && current == Mission &&
                             MissionStatusOf(missionStatusArea) > D.SkipStatus) ||
                            (D.RequiredVar != null && current == Mission &&
                             player.CharVar(D.RequiredVar) > D.RequiredValue)
                ? 1
                : 0;

            player.StartEvent(32001, battlefield.Area, clearTime, partySize, battlefield.TimeInside, player.ZoneId,
                              D.Index, canSkipCS);
        }

        // battlefield.lua:1488
        public void OnEventFinishWin(int csid, int option)
        {
            var player = Player;
            if (D.Title != 0) player.AddTitle(D.Title);

            // Only grant mission XP once per JP midnight
            string xpVar = $"Battlefield[{D.BattlefieldId}]XP";
            if (D.GrantXP != 0 && player.PlayerVar(xpVar) <= player.SystemTime)
            {
                player.SetPlayerVar(xpVar, player.JstMidnight);
                player.AddExp(D.GrantXP);
            }
        }
    }

    // battlefield.lua:1500: BattlefieldQuest, the Battlefield of a quest fight (BattlefieldQuest.isQuest = true). Its
    // three methods, ported line for line; the battlefield's data is a BattlefieldQuestData (battlefield.lua:1519,
    // BattlefieldQuest:new, with HEAD's repeatable). Every other method is Battlefield's (BattlefieldFramework)
    public sealed class BattlefieldQuest
    {
        private readonly IBattlefieldContext q_;

        public BattlefieldQuest(IBattlefieldContext q)
        {
            q_ = q ?? throw new ArgumentNullException(nameof(q));
            if (q.Script?.Data is not BattlefieldQuestData)
                throw new ArgumentException("BattlefieldQuest's methods need a quest battlefield", nameof(q));
        }

        private BattlefieldQuestData D => (BattlefieldQuestData)q_.Script.Data;
        private IBattlefieldPlayer Player => q_.Player ?? throw new InvalidOperationException("no player in this call");
        private IBattlefieldView Bf =>
            q_.Battlefield ?? throw new InvalidOperationException("no battlefield in this call");

        // battlefield.lua:1534
        public bool CheckRequirements(bool isRegistrant)
        {
            if (!q_.Framework.CheckRequirements(isRegistrant)) return false;

            var player = Player;
            if (D.RequiredVar != null)
            {
                // COP Missions use a charVar to track since missionStatus is used to define menu settings.
                // When converting these, allow for the same structure of status, but with defined requiredVar (str)
                // and requiredValue (uint requirement).  These are currently mutually exclusive, and treated
                // as such.

                int status = player.CharVar(D.RequiredVar);
                bool hasAccepted = player.QuestStatus(D.QuestArea, D.Quest) == QuestStatus.Accepted;

                return (!isRegistrant && (player.HasCompletedQuest(D.QuestArea, D.Quest) ||
                                          (hasAccepted && status >= D.RequiredValue))) ||
                       (hasAccepted && status == D.RequiredValue);
            }
            else
            {
                return player.QuestStatus(D.QuestArea, D.Quest) >= QuestStatus.Accepted;
            }
        }

        // battlefield.lua:1557
        public bool CheckSkipCutscene() => Player.QuestStatus(D.QuestArea, D.Quest) == QuestStatus.Completed;

        // battlefield.lua:1562
        public void OnBattlefieldWin()
        {
            var player = Player;
            var battlefield = Bf;
            var status = player.QuestStatus(D.QuestArea, D.Quest);

            if (status == QuestStatus.Accepted || (D.Repeatable && status == QuestStatus.Completed))
                player.SetLocalVar("battlefieldWin", battlefield.Id);

            var (_, clearTime, partySize) = battlefield.Record();
            int canSkipCS = status != QuestStatus.Accepted ? 1 : 0;

            player.StartEvent(32001, battlefield.Area, clearTime, partySize, battlefield.TimeInside, player.ZoneId,
                              D.Index, canSkipCS);
        }
    }

    // a content table whose paths (self.paths) are resolved to mob ids: the bridge's, from the battlefield's data
    public interface IBattlefieldPaths
    {
        IEnumerable<(int MobId, double[] Points)> ResolvedPaths();
    }
}
