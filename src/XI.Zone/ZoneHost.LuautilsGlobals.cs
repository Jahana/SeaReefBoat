using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // luautils globals the GM commands and a few scripts reach: the item lookups by id and name (GetItemByID,
    // GetItemFlagsByID, GetItemLevelRequirementsByID, GetItemIDByName), the player lookups (GetPlayerByName,
    // GetPlayerIDByName = charutils::getCharIdFromName, PlayerHasValidSession), another char's vars by id (GetCharVar,
    // SetCharVar, ClearCharVarFromAll), the volatile server vars (serverutils' cache and its
    // one-minute write-back), SendToJailOffline, SendItemToDeliveryBox, SelectDailyItem (daily_system.cpp) and the
    // linkshell concierge's slots (Load / Set / DeleteLinkshellConciergeSlot, DecrementLinkshellConciergeMembersGoal).
    public sealed partial class ZoneHost
    {
        private void RegisterLuautilsGlobals()
        {
            // luautils::GetItemByID: xi::items::lookup, the read-only item (nil for an id not in item_basic)
            lua_.SetGlobal("GetItemByID", l =>
            {
                l.PushObject(LookupItem((ushort)l.ToUIntArg(1)), ItemType);
                return 1;
            });
            // luautils::GetItemFlagsByID: the template's flags, ItemFlag::None without one
            lua_.SetGlobal("GetItemFlagsByID", l => Ret(l, LookupItem((ushort)l.ToUIntArg(1))?.Flags ?? 0));
            // luautils::GetItemLevelRequirementsByID: a CItemEquipment's getReqLvl, 0 for anything else
            lua_.SetGlobal("GetItemLevelRequirementsByID", l =>
                Ret(l, loader_.Stats?.Items.Equipment((ushort)l.ToUIntArg(1))?.ReqLvl ?? 0));
            // luautils::GetItemIDByName: the one item_basic row whose name or sortname is LIKE the argument; several
            // give 0xFFFF - count + 1 (gil is 0xFFFF, so a warning value below it), none 0
            lua_.SetGlobal("GetItemIDByName", l =>
            {
                string name = l.ToStringArg(1);
                var r = Exec("SELECT itemid FROM item_basic WHERE name LIKE ? OR sortname LIKE ?", BoundValue.Of(name), BoundValue.Of(name));
                int rows = 0;
                ushort id = 0;
                while (r != null && r.Next())
                {
                    rows++;
                    id = r.Get<ushort>("itemid");
                }
                return Ret(l, rows switch { 0 => (ushort)0, 1 => id, _ => (ushort)(0xFFFF - rows + 1) });
            });

            // luautils::GetPlayerByName: zoneutils::GetCharByName, the first char of this process whose name matches
            // ignoring case (strcmpi)
            lua_.SetGlobal("GetPlayerByName", l =>
            {
                string name = l.ToStringArg(1);
                CharData found = null;
                foreach (var c in charSessions_.Keys)
                    if (string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = c;
                        break;
                    }
                l.PushObject(found);
                return 1;
            });
            // charutils::getCharIdFromName: chars.charname, 0 when there is none
            lua_.SetGlobal("GetPlayerIDByName", l =>
            {
                var r = Exec("SELECT charid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(l.ToStringArg(1)));
                return Ret(l, r != null && r.Next() ? r.Get<uint>("charid") : 0u);
            });
            // luautils::PlayerHasValidSession: an accounts_sessions row for the char
            lua_.SetGlobal("PlayerHasValidSession", l =>
            {
                var r = Exec("SELECT 1 FROM accounts_sessions WHERE charid = ? LIMIT 1", BoundValue.Of(l.ToUIntArg(1)));
                return Ret(l, r != null && r.Next());
            });

            // luautils::GetCharVar(charId, name): charutils::FetchCharVar, the db row (an expired one deleted, 0)
            lua_.SetGlobal("GetCharVar", l =>
            {
                uint charId = l.ToUIntArg(1);
                string name = l.ToStringArg(2);
                int value = 0;
                var r = Exec("SELECT value, expiry FROM char_vars WHERE charid = ? AND varname = ? LIMIT 1", BoundValue.Of(charId), BoundValue.Of(name));
                if (r != null && r.Next())
                {
                    value = r.Get<int>("value");
                    uint expiry = r.Get<uint>("expiry");
                    if (expiry > 0 && expiry <= UnixNow())
                    {
                        value = 0;
                        Exec("DELETE FROM char_vars WHERE charid = ? AND varname = ?", BoundValue.Of(charId), BoundValue.Of(name));
                    }
                }
                return Ret(l, value);
            });
            // luautils::SetCharVar(charId, name, value, expiry): an expiry already past is refused, else
            // charutils::SetCharVar (the char's own on this process, else the db and the world's CharVarUpdate)
            lua_.SetGlobal("SetCharVar", l =>
            {
                uint charId = l.ToUIntArg(1);
                string name = l.ToStringArg(2);
                int value = l.ToIntArg(3);
                uint expiry = l.IsNumber(4) ? l.ToUIntArg(4) : 0;
                if (expiry > 0 && expiry <= UnixNow())
                {
                    log_($"Attempting to set variable '{name}' with an expired time: {expiry}");
                    return 0;
                }
                SetCharVarById(charId, name, value, expiry);
                return 0;
            });
            // charutils::ClearCharVarFromAll(name, localOnly = false): every row of it, and every char of this
            // process's cache set to 0 (updateCharVarCache)
            lua_.SetGlobal("ClearCharVarFromAll", l =>
            {
                string name = l.ToStringArg(1);
                Exec("DELETE FROM char_vars WHERE varname = ?", BoundValue.Of(name));
                foreach (var c in charSessions_.Keys)
                {
                    c.CharVars[name] = (0, 0);
                    if (XI.Quests.CharProgress.IsProgressVar(name)) PersistProgress(c, () => true, p => p.SetVar(name, 0, 0));
                }
                return 0;
            });

            // luautils::GetVolatileServerVariable: serverutils::GetVolatileServerVar, the cache while it is not
            // expired, else GetServerVar (the row, which refreshes the cache)
            lua_.SetGlobal("GetVolatileServerVariable", l =>
            {
                string name = l.ToStringArg(1);
                if (serverVarCache_.TryGetValue(name, out var v) && (v.Expiry == 0 || v.Expiry > UnixNow())) return Ret(l, v.Value);
                return Ret(l, ServerVar(name));
            });
            // luautils::SetVolatileServerVariable(name, value, expiry): an expiry already past is refused, else the
            // cache, written back by PersistVolatileServerVars
            lua_.SetGlobal("SetVolatileServerVariable", l =>
            {
                string name = l.ToStringArg(1);
                int value = l.ToIntArg(2);
                uint expiry = l.IsNumber(3) ? l.ToUIntArg(3) : 0;
                if (expiry > 0 && expiry <= UnixNow())
                {
                    log_($"Attempting to set variable '{name}' with an expired time: {expiry}");
                    return 0;
                }
                serverVarCache_[name] = (value, expiry);
                serverVarChanges_.Add(name);
                return 0;
            });

            // luautils::SendToJailOffline(charId, cellId, x, y, z, rot): the inJail var and the char's position in
            // Mordion Gaol, out of its mog house
            lua_.SetGlobal("SendToJailOffline", l =>
            {
                uint charId = l.ToUIntArg(1);
                PersistCharVar(charId, "inJail", (sbyte)l.ToIntArg(2), 0);
                Exec("UPDATE chars SET pos_x = ?, pos_y = ?, pos_z = ?, pos_rot = ?, pos_zone = ?, moghouse = 0 WHERE charid = ?",
                     BoundValue.Of((float)l.ToNumber(3)), BoundValue.Of((float)l.ToNumber(4)), BoundValue.Of((float)l.ToNumber(5)),
                     BoundValue.Of((byte)l.ToUIntArg(6)), BoundValue.Of(MordionGaol), BoundValue.Of(charId));
                return 0;
            });

            // luautils::SendItemToDeliveryBox(name, itemId, quantity, senderText): a delivery_box row (box 1, the char
            // as its own sender); the quantity clamped to 1..the stack size (gil 999999999). SendToDBoxReturnCode
            lua_.SetGlobal("SendItemToDeliveryBox", l =>
            {
                string name = l.ToStringArg(1);
                ushort itemId = (ushort)l.ToUIntArg(2);
                uint quantity = l.ToUIntArg(3);
                string sender = l.ToStringArg(4);
                var r = Exec("SELECT charid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(name));
                uint charId = r != null && r.Next() ? r.Get<uint>("charid") : 0;
                if (charId == 0) return Ret(l, DBoxPlayerNotFound);
                bool isGil = itemId == 65535;
                var item = LookupItem(itemId);
                if (item == null && !isGil) return Ret(l, DBoxItemNotFound);
                uint stackSize = isGil ? 999999999u : item.StackSize;
                bool limited = quantity > stackSize;
                quantity = Math.Clamp(quantity, 1u, stackSize);
                var ok = Exec("INSERT INTO delivery_box (charid, box, itemid, quantity, senderid, sender) VALUES (?, ?, ?, ?, ?, ?)",
                              BoundValue.Of(charId), BoundValue.Of(1), BoundValue.Of(itemId), BoundValue.Of(quantity), BoundValue.Of(charId),
                              BoundValue.Of(sender));
                if (ok == null) return Ret(l, DBoxQueryError);
                return Ret(l, limited ? DBoxSuccessLimitedToStackSize : DBoxSuccess);
            });

            // luautils::SelectDailyItem(player, dial): daily::SelectItem
            lua_.SetGlobal("SelectDailyItem", l => Ret(l, l.ToObject(1) is CharData c ? SelectDailyItem(c, (byte)l.ToUIntArg(2)) : (ushort)0));

            // luautils::LoadLinkshellConciergeSlots(zoneId): the zone's linkshell_concierge rows of unbroken
            // linkshells, each with its linkshell's name and color (xi.linkshellConcierge)
            lua_.SetGlobal("LoadLinkshellConciergeSlots", l =>
            {
                var r = Exec("SELECT lc.slot_index, lc.linkshellid, lc.owner_char_id, lc.group_key, lc.flag, lc.lang, lc.members_goal, " +
                             "lc.active_tier, lc.characteristics, lc.tz, lc.days, lc.times, lc.posted_date, ls.name, ls.color " +
                             "FROM linkshell_concierge lc JOIN linkshells ls ON ls.linkshellid = lc.linkshellid " +
                             "WHERE lc.zone_id = ? AND ls.broken = 0", BoundValue.Of((ushort)l.ToUIntArg(1)));
                l.NewTable();
                int n = 0;
                while (r != null && r.Next())
                {
                    l.NewTable();
                    void Field(string key, double value)
                    {
                        l.Push(value);
                        l.SetField(-2, key);
                    }
                    Field("slotIndex", r.Get<byte>("slot_index"));
                    Field("linkshellid", r.Get<uint>("linkshellid"));
                    Field("ownerCharId", r.Get<uint>("owner_char_id"));
                    Field("groupKey", r.Get<ushort>("group_key"));
                    Field("flag", r.Get<byte>("flag"));
                    Field("lang", r.Get<byte>("lang"));
                    Field("membersGoal", r.Get<byte>("members_goal"));
                    Field("activeTier", r.Get<byte>("active_tier"));
                    Field("characteristics", r.Get<ushort>("characteristics"));
                    Field("tz", r.Get<byte>("tz"));
                    Field("days", r.Get<byte>("days"));
                    Field("times", r.Get<uint>("times"));
                    Field("postedDate", r.Get<uint>("posted_date"));
                    l.PushString(r.Get<string>("name"));
                    l.SetField(-2, "name");
                    Field("color", r.Get<ushort>("color"));
                    l.RawSetI(-2, ++n);
                }
                return 1;
            });
            // luautils::SetLinkshellConciergeSlot(zoneId, slotIndex, data): REPLACE the slot's row, a missing field 0
            lua_.SetGlobal("SetLinkshellConciergeSlot", l =>
            {
                BoundValue F(string key) => BoundValue.Of((uint)FieldInt(l, 3, key, 0)); // get_or<uint32 / uint16 / uint8>
                Exec("REPLACE INTO linkshell_concierge SET zone_id = ?, slot_index = ?, linkshellid = ?, owner_char_id = ?, group_key = ?, " +
                     "flag = ?, lang = ?, members_goal = ?, active_tier = ?, characteristics = ?, tz = ?, days = ?, times = ?, posted_date = ?",
                     BoundValue.Of((ushort)l.ToUIntArg(1)), BoundValue.Of((byte)l.ToUIntArg(2)), F("linkshellid"), F("ownerCharId"),
                     F("groupKey"), F("flag"), F("lang"), F("membersGoal"), F("activeTier"), F("characteristics"), F("tz"), F("days"),
                     F("times"), F("postedDate"));
                return 0;
            });
            lua_.SetGlobal("DeleteLinkshellConciergeSlot", l =>
            {
                Exec("DELETE FROM linkshell_concierge WHERE zone_id = ? AND slot_index = ?",
                     BoundValue.Of((ushort)l.ToUIntArg(1)), BoundValue.Of((byte)l.ToUIntArg(2)));
                return 0;
            });
            // luautils::garbageCollectStep / garbageCollectFull (the GM commands !gc_step / !gc_full): a 10 kb
            // incremental step, or a full cycle, and the state's top and memory logged
            lua_.SetGlobal("GarbageCollectStep", l =>
            {
                l.StepGarbage(10);
                log_("Garbage Collected (Step)");
                log_($"Current State Top: {l.Top}, Total Memory Used: {l.MemoryKb}kb");
                return 0;
            });
            lua_.SetGlobal("GarbageCollectFull", l =>
            {
                long before = l.MemoryKb;
                l.CollectGarbage();
                log_("Garbage Collected (Full)");
                log_($"Current State Top: {l.Top}, Total Memory Used: {before}kb -> {l.MemoryKb}kb");
                return 0;
            });
            // BuildString (the GM command !build): "<branch>-<sha>\n<subject>\n<date>" from the C++ build's git
            // version; here the assembly's informational version (its git sha when the SDK stamps one) stands for it
            lua_.SetGlobal("BuildString", l =>
            {
                var asm = typeof(ZoneHost).Assembly;
                string version = (System.Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyInformationalVersionAttribute))
                                  as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion ?? "unknown";
                l.PushString($"csharp-{version}\n\n{System.IO.File.GetLastWriteTimeUtc(asm.Location):yyyy-MM-dd HH:mm:ss}");
                return 1;
            });
            // luautils::SendLuaFuncStringToZone(requesterZone, executorZone, str): an ipc::LuaFunction the world routes
            // to the executor zone's process (OnIpc runs it)
            lua_.SetGlobal("SendLuaFuncStringToZone", l =>
            {
                SendIpc(new XI.Ipc.LuaFunction
                {
                    RequesterZoneId = (XI.Ipc.ZoneId)l.ToEnumArg(1), ExecutorZoneId = (XI.Ipc.ZoneId)l.ToEnumArg(2), FuncString = l.ToStringArg(3),
                });
                return 0;
            });
            // luautils::GetRecentFishers(minutes): the chars whose [Fish]LastCastTime falls in the last 1-60 minutes, by
            // char id: name, level, zone name and fishing skill (the GM command !getfishers)
            lua_.SetGlobal("GetRecentFishers", l =>
            {
                uint lookback = Math.Clamp(l.ToUIntArg(1), 1u, 60u);
                var r = Exec("SELECT cv.charid, c.charname, stats.mlvl, z.name, COALESCE(cs.value, 0) / 10 as skill FROM char_vars cv " +
                             "LEFT JOIN chars c ON c.charid = cv.charid LEFT JOIN char_skills cs ON cs.charid = cv.charid AND cs.skillid = 48 " +
                             "INNER JOIN char_stats stats ON stats.charid = c.charid INNER JOIN zone_settings z ON z.zoneid = c.pos_zone " +
                             "WHERE varname = '[Fish]LastCastTime' AND FROM_UNIXTIME(cv.value) > NOW() - INTERVAL ? MINUTE " +
                             "ORDER BY z.name, z.name, stats.mlvl, skill", BoundValue.Of(lookback));
                l.NewTable();
                while (r != null && r.Next())
                {
                    l.NewTable();
                    l.PushString(r.Get<string>("charname")); l.SetField(-2, "playerName");
                    l.Push((double)r.Get<uint>("mlvl")); l.SetField(-2, "jobLevel");
                    l.PushString(r.Get<string>("name")); l.SetField(-2, "zoneName");
                    l.Push((double)r.Get<uint>("skill")); l.SetField(-2, "skill");
                    l.RawSetI(-2, (int)r.Get<uint>("charid"));
                }
                return 1;
            });
            // luautils::DecrementLinkshellConciergeMembersGoal(zoneId, linkshellid): one member fewer wanted; a
            // posting with none left goes
            lua_.SetGlobal("DecrementLinkshellConciergeMembersGoal", l =>
            {
                var zone = BoundValue.Of((ushort)l.ToUIntArg(1));
                var ls = BoundValue.Of(l.ToUIntArg(2));
                Exec("UPDATE linkshell_concierge SET members_goal = members_goal - 1 WHERE zone_id = ? AND linkshellid = ? AND members_goal > 0", zone, ls);
                Exec("DELETE FROM linkshell_concierge WHERE zone_id = ? AND linkshellid = ? AND members_goal = 0", zone, ls);
                return 0;
            });
        }

        private const ushort MordionGaol = 131;
        private const byte DBoxSuccess = 0, DBoxSuccessLimitedToStackSize = 1, DBoxPlayerNotFound = 2, DBoxItemNotFound = 3, DBoxQueryError = 4;

        // serverutils' serverVarCache / serverVarChanges: the volatile server vars, written back once a minute
        private readonly Dictionary<string, (int Value, uint Expiry)> serverVarCache_ = new Dictionary<string, (int, uint)>();
        private readonly HashSet<string> serverVarChanges_ = new HashSet<string>();
        private const long PersistVolatileServerVarsIntervalMs = 60_000; // kPersistVolatileServerVarsInterval
        private long persistVolatileServerVarsMs_;

        // serverutils::PersistVolatileServerVars, on the map's one-minute interval: each changed var's row (0 deletes it)
        private void TickVolatileServerVars()
        {
            long now = NowMs();
            if (persistVolatileServerVarsMs_ == 0) persistVolatileServerVarsMs_ = now + PersistVolatileServerVarsIntervalMs;
            if (now < persistVolatileServerVarsMs_) return;
            persistVolatileServerVarsMs_ = now + PersistVolatileServerVarsIntervalMs;
            PersistVolatileServerVars();
        }

        private void PersistVolatileServerVars()
        {
            foreach (var name in serverVarChanges_)
            {
                var (value, expiry) = serverVarCache_[name];
                if (value == 0)
                    Exec("DELETE FROM server_variables WHERE name = ? LIMIT 1", BoundValue.Of(name));
                else
                    Exec("INSERT INTO server_variables VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE value = ?, expiry = ?",
                         BoundValue.Of(name), BoundValue.Of(value), BoundValue.Of(expiry), BoundValue.Of(value), BoundValue.Of(expiry));
            }
            serverVarChanges_.Clear();
        }

        // ---- daily_system.cpp: the Gobbie Mystery Box's dials --------------------------------------------------------

        private const uint ItemFlagCanUse = 0x200; // ItemFlagRare is ZoneHost.Progress.cs's
        private static readonly ushort[] GobbieJunk = { 2542, 2543, 4324, 4325, 4328, 4458, 4495, 4539 };
        private List<ushort>[] dailyDials_; // dials 1-6: materials, food, medicine, sundries 1, sundries 2, special

        // daily::SelectItem: a random item of the dial (any other dial the goblin junk); a Rare one the char already
        // has (recycle bin included) swapped for a random goblin junk item
        private ushort SelectDailyItem(CharData c, byte dial)
        {
            dailyDials_ ??= LoadDailyItems();
            IReadOnlyList<ushort> items = dial is >= 1 and <= 6 ? dailyDials_[dial - 1] : GobbieJunk;
            if (items.Count == 0) return 0;
            ushort selection = items[MobRng.Next(items.Count)];
            if (((LookupItem(selection)?.Flags ?? 0) & ItemFlagRare) != 0 && c.HasItem(selection))
                selection = GobbieJunk[MobRng.Next(GobbieJunk.Length)];
            return selection;
        }

        // daily::LoadDailyItems: the Mystery Box items (flags & 4) sorted onto the dials by their auction category
        private List<ushort>[] LoadDailyItems()
        {
            var dials = new List<ushort>[6];
            for (int i = 0; i < 6; i++) dials[i] = new List<ushort>();
            var r = Exec("SELECT itemid, aH, flags FROM item_basic WHERE flags & 4 > 0");
            bool any = false;
            while (r != null && r.Next())
            {
                any = true;
                ushort itemId = r.Get<ushort>("itemid");
                ushort ah = r.Get<ushort>("aH");
                uint flags = r.Get<uint>("flags");
                dials[5].Add(itemId);
                switch (ah)
                {
                    case 38: case 39: case 40: case 41: case 42: case 43: case 44: case 50:
                        dials[0].Add(itemId);
                        break;
                    case 52: case 53: case 54: case 55: case 56: case 57: case 58:
                        dials[1].Add(itemId);
                        break;
                    case 33:
                        dials[2].Add(itemId);
                        break;
                    case 15: case 36: case 49:
                        if ((flags & ItemFlagCanUse) != 0) dials[3].Add(itemId);
                        break;
                    case 47: case 51:
                        if (itemId == 489 || itemId == 17386) break; // Lu Shang's: probably the special dial only
                        dials[4].Add(itemId);
                        break;
                    default:
                        if (itemId is 605 or 1020 or 1021 or 1022 or 1023 or 15453 or 15454) dials[4].Add(itemId);
                        break;
                }
            }
            if (!any) log_("Failed to load daily tally items");
            return dials;
        }
    }
}
