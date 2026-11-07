using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Small CLuaBaseEntity bindings, batch 7: renaming and allegiance, runes and maneuvers, the call-for-help flags,
    // angles and sides, party members, assault points and missions, model sizes, stat ranks and assorted getters.
    public sealed partial class ZoneHost
    {
        private const byte ContinentTheMiddleLands = 1, ContinentOtherAreas = 4;
        private static readonly string[] AssaultPointColumns =
            { "leujaoam_assault_point", "mamool_assault_point", "lebros_assault_point", "periqia_assault_point", "ilrusi_assault_point" };

        // GetNewestStatusEffectInIDRange / RemoveOldest- / RemoveNewestStatusEffectInIDRange's pick: the live effect in
        // the range started latest (or earliest), the first met on a tie
        private static StatusEffect EffectInRange(BattleEntity b, ushort start, ushort end, bool newest)
        {
            StatusEffect pick = null;
            foreach (var e in b.StatusEffects.All)
            {
                if (e.Id < start || e.Id > end || e.Deleted) continue;
                if (pick == null || (newest ? e.StartMs > pick.StartMs : e.StartMs < pick.StartMs)) pick = e;
            }
            return pick;
        }

        // RemoveAllStatusEffectsInIDRange: every one in the range, silently
        private static void RemoveAllInRange(BattleEntity b, ushort start, ushort end)
        {
            foreach (var e in b.StatusEffects.All.Where(e => e.Id >= start && e.Id <= end).ToList())
                b.StatusEffects.RemoveStatusEffect(e, EffectNotice.Silent);
        }

        // toEntitysLeft / toEntitysRight: A to B's side within the cone, seen from B
        private static bool ToEntitysSide((float X, float Y, float Z, byte Rot) a, (float X, float Y, float Z, byte Rot) b, byte coneAngle, bool left)
        {
            int diff = FacingAngle(b, a);
            int halfAngle = coneAngle / 2;
            return (left ? diff < 0 : diff > 0) && Math.Abs(diff) > 64 - halfAngle && Math.Abs(diff) < 64 + halfAngle;
        }

        // 0x009 GP_SERV_COMMAND_MESSAGE(messageID): the message alone
        private static BasicPacket MessageStandard(ushort messageId)
        {
            var w = new PacketWriter(0x009, 0x10);
            w.U16(10, messageId);
            return w.Packet;
        }

        private void RegisterMiscBindings(Dictionary<string, LuaFunction> m)
        {
            // ---- names, allegiance, flags ----
            // renameEntity(name, silent): the name every update now carries
            m["renameEntity"] = l =>
            {
                var e = Entity(l);
                string newName = l.ToStringArg(2);
                string oldName;
                switch (e)
                {
                    case MobEntity mob:
                        oldName = mob.PacketName;
                        mob.PacketName = newName;
                        mob.IsRenamed = true;
                        break;
                    case NpcEntity npc:
                        oldName = npc.PacketName;
                        npc.PacketName = newName;
                        npc.IsRenamed = true;
                        break;
                    default:
                        log_("Renaming player character entities isn't supported.");
                        return 0;
                }
                MarkUpdate(e, (byte)(EntityUpdatePacket.UpdateName | EntityUpdatePacket.UpdateHp));
                bool silent = l.Type(3) == LuaNative.LUA_TBOOLEAN && l.ToBoolean(3);
                if (!silent) log_($"Renaming {NameOf(e)}: {(oldName.Length == 0 ? "<empty>" : oldName)} -> {newName}");
                return 0;
            };
            m["getPacketName"] = l =>
            {
                l.PushString(Entity(l) switch
                {
                    MobEntity mob => mob.PacketName,
                    NpcEntity npc => npc.PacketName,
                    _ => "",
                });
                return 1;
            };
            // setAllegiance(allegiance): the side it fights for, sent with its next update
            m["setAllegiance"] = l =>
            {
                byte allegiance = (byte)l.ToEnumArg(2); // xi::Allegiance
                switch (Entity(l))
                {
                    case MobEntity mob: mob.Allegiance = allegiance; break;
                    case CharData c: c.Allegiance = allegiance; break;
                    default: return 0; // an NPC's allegiance never reaches its packet
                }
                MarkUpdate(Entity(l), (byte)(EntityUpdatePacket.UpdateHp | EntityUpdatePacket.UpdateName));
                return 0;
            };
            m["setNpcFlags"] = l =>
            {
                if (Entity(l) is not NpcEntity npc) return 0;
                npc.EntityFlags = (uint)l.ToEnumArg(2); // xi::EntityFlags
                MarkUpdate(npc, EntityUpdatePacket.UpdateHp);
                return 0;
            };
            // setPriorityRender(enabled): CliPriorityFlag, so clients draw it whatever else is around
            m["setPriorityRender"] = l =>
            {
                bool on = l.ToBoolean(2);
                switch (Entity(l))
                {
                    case MobEntity mob: mob.PriorityRender = on; break;
                    case CharData c: c.PriorityRender = on; break;
                    case NpcEntity npc: npc.PriorityRender = on; break;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdateHp);
                return 0;
            };
            m["getWallhack"] = l => Ret(l, Pc(l, "getWallhack") is CharData c && c.WallhackEnabled);
            m["getGMHidden"] = l => Ret(l, Pc(l, "getGMHidden") is CharData c && c.GmHidden);
            m["isJailed"] = l => Ret(l, Pc(l, "isJailed") is CharData c && InPrison(c));
            m["isSeekingParty"] = l => Ret(l, Pc(l, "isSeekingParty") is CharData c && c.InviteFlg);
            m["getLastOnline"] = l => Ret(l, Pc(l, "getLastOnline") is CharData c ? (uint)c.LastOnline : 0);
            m["getTimeCreated"] = l =>
            {
                if (Pc(l, "getTimeCreated") is not CharData c) return Ret(l, 0);
                var r = Exec("SELECT UNIX_TIMESTAMP(timecreated) AS created FROM chars WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
                return Ret(l, r != null && r.Next() && !r.IsNull("created") ? r.Get<uint>("created") : 0);
            };
            m["getContinentID"] = l => Ret(l, ZoneRegions.Of(ZoneOf(Entity(l))) != ZoneRegions.Unknown ? ContinentTheMiddleLands : ContinentOtherAreas);

            // ---- the char's progress ----
            m["addSeals"] = l =>
            {
                if (Pc(l, "addSeals") is not CharData c) return 0;
                string column = l.ToIntArg(3) switch
                {
                    0 => "beastman_seal",
                    1 => "kindred_seal",
                    2 => "kindred_crest",
                    3 => "high_kindred_crest",
                    4 => "sacred_kindred_crest",
                    _ => null,
                };
                if (column != null) AddPoints(c, column, l.ToIntArg(2));
                return 0;
            };
            m["getAssaultPoint"] = l =>
            {
                if (Pc(l, "getAssaultPoint") is not CharData c) return Ret(l, 0);
                int region = l.ToIntArg(2);
                return Ret(l, region is >= 0 and < 5 ? GetPoints(c, AssaultPointColumns[region]) : 0);
            };
            m["addAssaultPoint"] = l =>
            {
                int region = l.ToIntArg(2);
                if (Pc(l, "addAssaultPoint") is CharData c && region is >= 0 and < 5) AddPoints(c, AssaultPointColumns[region], l.ToIntArg(3));
                return 0;
            };
            m["delAssaultPoint"] = l =>
            {
                int region = l.ToIntArg(2);
                if (Pc(l, "delAssaultPoint") is CharData c && region is >= 0 and < 5) AddPoints(c, AssaultPointColumns[region], -l.ToIntArg(3));
                return 0;
            };
            m["getCurrentAssault"] = l => Ret(l, Pc(l, "getCurrentAssault") is CharData c ? (byte)c.AssaultCurrent : 0);
            m["hasCompletedAssault"] = l => Ret(l, Pc(l, "hasCompletedAssault") is CharData c && c.AssaultComplete((byte)l.ToIntArg(2)));
            // addAssault(id): the current assault, the offer page, the log saved
            m["addAssault"] = l =>
            {
                if (Pc(l, "addAssault") is not CharData c) return 0;
                if (c.AssaultCurrent != 0) log_("Lua::addAssault: player has a current assault");
                BitConverter.GetBytes((ushort)(byte)l.ToIntArg(2)).CopyTo(c.AssaultLog, 0);
                SendPartialMissionLog(c, MissionLogAssault, false);
                SaveMissionsList(c);
                return 0;
            };
            m["delAssault"] = l =>
            {
                if (Pc(l, "delAssault") is not CharData c) return 0;
                if ((byte)c.AssaultCurrent == (byte)l.ToIntArg(2))
                {
                    BitConverter.GetBytes((ushort)0).CopyTo(c.AssaultLog, 0);
                    SendPartialMissionLog(c, MissionLogAssault, false);
                }
                SaveMissionsList(c);
                return 0;
            };
            m["completeAssault"] = l =>
            {
                if (Pc(l, "completeAssault") is not CharData c) return 0;
                byte id = (byte)l.ToIntArg(2);
                if (c.AssaultCurrent != id) log_("Lua::completeAssault: completion of not current assault");
                BitConverter.GetBytes((ushort)0).CopyTo(c.AssaultLog, 0);
                if (id < 128) c.AssaultLog[2 + id] = 1;
                SendPartialMissionLog(c, MissionLogAssault, false);
                SendPartialMissionLog(c, MissionLogAssault, true);
                SaveMissionsList(c);
                return 0;
            };
            // delTitle(id): the bit off (and the current title, if it was that), CLISTATUS, SaveTitles
            m["delTitle"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"function call on invalid entity! (name: {NameOf(Entity(l))} type: {ObjTypeOf(Entity(l))})");
                    return 0;
                }
                ushort title = (ushort)l.ToIntArg(2);
                if (!DelBit(c.Titles, title)) return 0;
                if (c.Title == title) c.Title = 0;
                if (loader_.Stats != null) PushTo(c, SelfPackets.CliStatus(c, loader_.Stats, UnityStatus(c)));
                Exec("UPDATE chars LEFT JOIN char_stats USING(charid) SET titles = ?, title = ? WHERE charid = ? LIMIT 1",
                     BoundValue.Of(c.Titles), BoundValue.Of(c.Title), BoundValue.Of(c.Id));
                return 0;
            };
            // unseenKeyItem(id): the seen bit off, the table's SCENARIOITEM, SaveKeyItems
            m["unseenKeyItem"] = l =>
            {
                if (Pc(l, "unseenKeyItem") is not CharData c) return 0;
                int keyItem = (ushort)l.ToEnumArg(2), table = keyItem >> 9, index = keyItem % 512; // xi::KeyItem
                if (table >= 8)
                {
                    log_($"CLuaBaseEntity::unseenKeyItem() - Index {table} exceeds key items table capacity.");
                    return 0;
                }
                c.KeyItems[table * 128 + 64 + index / 8] &= (byte)~(1 << index % 8);
                PushTo(c, GameOkPackets.ScenarioItem(c, table));
                Exec("UPDATE chars SET keyitems = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.KeyItems), BoundValue.Of(c.Id));
                return 0;
            };
            m["hasLearnedAbility"] = l =>
            {
                if (Pc(l, "hasLearnedAbility") is not CharData c) return Ret(l, false);
                int id = (ushort)l.ToIntArg(2);
                return Ret(l, id / 8 < c.LearnedAbilities.Length && (c.LearnedAbilities[id / 8] & 1 << id % 8) != 0);
            };
            // setVolatileCharVar(name, value, expiry): the cache alone, never the database
            m["setVolatileCharVar"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                string name = l.ToStringArg(2);
                uint expiry = OptU32(l, 4);
                if (expiry > 0 && expiry <= UnixNow())
                {
                    log_($"Attempting to set variable '{name}' with an expired time: {expiry}");
                    return 0;
                }
                c.CharVars[name] = (l.ToIntArg(3), expiry);
                return 0;
            };
            m["getCostume2"] = l => Ret(l, Pc(l, "getCostume2") is CharData c ? c.Costume2 : 0);
            // setCostume2(costume): the model the look carries in its face's place, with the look list to the char
            m["setCostume2"] = l =>
            {
                if (Pc(l, "setCostume2") is not CharData c) return 0;
                ushort costume = (ushort)l.ToIntArg(2);
                if (c.Costume2 == costume || SessionOf(c) is not { } s) return 0;
                c.Costume2 = costume;
                c.UpdateMask |= CharStats.UpdateLook;
                Push(s, LoginPackets.GrapList(c));
                return 0;
            };

            // ---- items and gear ----
            // delItemAt(itemId, quantity, container, slot): that stack, if it is the item
            m["delItemAt"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                ushort id = (ushort)l.ToIntArg(2);
                int quantity = l.ToIntArg(3);
                int container = (byte)l.ToIntArg(4);
                byte slot = (byte)l.ToIntArg(5);
                if (container >= CharData.MaxContainers)
                {
                    log_("Lua::delItemAt: Attempting to delete an item from an invalid slot. Defaulting to main inventory.");
                    container = (int)Container.Inventory;
                }
                if (c.FindItem((Container)container, slot) is not { } item || item.ItemId != id) return Ret(l, false);
                using (var t = new ItemTransaction(this, c))
                {
                    if (t.ClaimSlot((Container)container, slot) == null || !t.Take((Container)container, slot, (uint)quantity) || !t.Commit())
                    {
                        log_($"CLuaBaseEntity::delItem: {c.Name} kept item in slot {slot}");
                        return Ret(l, false);
                    }
                }
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                return Ret(l, true);
            };
            // getGearModFromSlot(slot, mod): the item's own mod total, if the char's level reaches the item's
            m["getGearModFromSlot"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid Entity (Non-PC: {NameOf(Entity(l))}) calling function getGearModFromSlot.");
                    return Ret(l, 0);
                }
                int slot = l.ToIntArg(2);
                var mod = (Mod)l.ToEnumArg(3);
                if (slot is < 0 or >= 16 || c.Equipped[slot] is not { } item || item.ReqLvl > c.MLvl) return Ret(l, 0);
                return Ret(l, (short)item.Mods.Where(x => x.Mod == mod).Sum(x => x.Value));
            };
            m["getAmmoDmg"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                if (b is CharData c && c.Equipped[CharStats.SlotAmmo]?.Weapon is { } ammo) return Ret(l, (ushort)ammo.Damage);
                return Ret(l, 0); // lua::getAmmoDmg weapon in ammo slot is nullptr!
            };
            // getWeaponSkillLevel(slot): the char's skill in the weapon held there (main, sub, ranged, ammo)
            m["getWeaponSkillLevel"] = l =>
            {
                if (Battle(l) is not CharData c) return Ret(l, 0);
                int slot = l.ToIntArg(2);
                if (slot > 3)
                {
                    log_($"Invalid slotID ({slot}) passed to function.");
                    return Ret(l, 0);
                }
                return Ret(l, slot >= 0 && c.Equipped[slot]?.Weapon is { } weapon ? CharStats.GetSkill(c, weapon.Skill) : 0);
            };
            m["getRATT"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                return Ret(l, Ratt(b));
            };

            // ---- HP, MP, death ----
            m["getBaseHP"] = l => Ret(l, Battle(l) is { } b ? b.BaseMaxHp : 0);
            m["getBaseMP"] = l => Ret(l, Battle(l) is { } b ? b.BaseMaxMp : 0);
            m["restoreMP"] = l => Ret(l, Battle(l) is { } b && b.Animation != Anim.Death ? BattleAddMp(b, l.ToIntArg(2)) : 0);
            // CLuaBaseEntity::die({ expLoss, mijin }): the char falls on the next tick. Only the keys given change the
            // params staged for the next death (nextDeath_), which CharDie (ZoneHost.MobAi.cs: its EXP loss) hands to
            // the death state for OnRaise (ZoneHost.Raise.cs: Mijin Gakure's raise); then setHP(0)
            m["die"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid Entity ({NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                if (l.IsTable(2))
                {
                    var staged = c.NextDeath ?? DeathParams.Default;
                    staged = new DeathParams(FieldBoolOr(l, 2, "expLoss", staged.LosesExp), FieldBoolOr(l, 2, "mijin", staged.Mijin));
                    c.NextDeath = staged;
                }
                c.Hp = 0;
                BattleAddHp(c, 0);
                c.UpdateMask |= EntityUpdatePacket.UpdateHp;
                c.LastAttackerId = 0;
                return 0;
            };

            // ---- status effects ----
            m["hasBustEffect"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, false);
                ushort id = (ushort)l.ToIntArg(2);
                return Ret(l, b.StatusEffects.All.Any(e => e.Id == EffectBust && e.SubPower == id));
            };
            m["getNewestRuneEffect"] = l => Ret(l, Battle(l) is { } b ? EffectInRange(b, EffectIgnis, EffectTenebrae, true)?.Id ?? 0 : 0);
            m["removeNewestRune"] = l =>
            {
                if (Battle(l) is { } b && EffectInRange(b, EffectIgnis, EffectTenebrae, true) is { } e) b.StatusEffects.RemoveStatusEffect(e, EffectNotice.Silent);
                return 0;
            };
            m["removeOldestRune"] = l =>
            {
                if (Battle(l) is { } b && EffectInRange(b, EffectIgnis, EffectTenebrae, false) is { } e) b.StatusEffects.RemoveStatusEffect(e, EffectNotice.Silent);
                return 0;
            };
            m["removeAllRunes"] = l =>
            {
                if (Battle(l) is { } b) RemoveAllInRange(b, EffectIgnis, EffectTenebrae);
                return 0;
            };
            m["getActiveManeuverCount"] = l =>
                Ret(l, Battle(l) is { } b ? b.StatusEffects.All.Count(e => e.Id >= EffectId.FireManeuver && e.Id <= EffectId.DarkManeuver && !e.Deleted) : 0);
            m["removeOldestManeuver"] = l =>
            {
                if (Battle(l) is { } b && EffectInRange(b, EffectId.FireManeuver, EffectId.DarkManeuver, false) is { } e)
                    b.StatusEffects.RemoveStatusEffect(e, EffectNotice.Silent);
                return 0;
            };
            m["removeAllManeuvers"] = l =>
            {
                if (Battle(l) is { } b) RemoveAllInRange(b, EffectId.FireManeuver, EffectId.DarkManeuver);
                return 0;
            };

            // ---- mobs ----
            // updateTarget: the mob turns on whoever now holds the most enmity
            m["updateTarget"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to update target for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (MobHighestEnmity(mob) is { } target) mob.BattleTarget = target;
                return 0;
            };
            m["setIsAggroable"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.IsAggroable = l.ToBoolean(2);
                else log_($"Attempting to set is Aggroable for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["isAggroable"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.IsAggroable);
                log_($"Attempting to get is aggroable for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, false);
            };
            m["getRoamFlags"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.RoamFlags);
                log_($"Attempting to get roam flags for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["getLink"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.Link ? 1 : 0);
                log_($"Attempting to get link for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            // getCallForHelpFlag / setCallForHelpFlag: calling lifts the claim; either way the combat update goes out
            m["getCallForHelpFlag"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.CallForHelp);
                log_("getCallForHelpFlag called on invalid entity.");
                return Ret(l, false);
            };
            m["setCallForHelpFlag"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_("setCallForHelpFlag called on invalid entity.");
                    return 0;
                }
                if (l.ToBoolean(2))
                {
                    mob.EntityFlags |= MobEntity.EntityFlagCallForHelp;
                    mob.OwnerId = 0;
                }
                else mob.EntityFlags &= ~MobEntity.EntityFlagCallForHelp;
                mob.UpdateMask |= EntityUpdatePacket.UpdateCombat;
                return 0;
            };
            m["getCallForHelpBlocked"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.CallForHelpBlocked);
                log_("getCallForHelpBlocked called on invalid entity.");
                return Ret(l, false);
            };
            m["setCallForHelpBlocked"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.CallForHelpBlocked = l.ToBoolean(2);
                else log_("setCallForHelpBlocked called on invalid entity.");
                return 0;
            };
            m["getAbilityDistance"] = l =>
            {
                int id = (ushort)l.ToIntArg(2);
                if (GetMobSkill(id) is { } skill) return Ret(l, skill.Distance);
                log_($"Invalid SkillID ({id}) returned null, returning 0 for distance.");
                return Ret(l, 0);
            };
            // hasTPMoves: the mob's skill list (the SKILL_LIST mobmod, else its own) has anything in it
            m["hasTPMoves"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    if (Entity(l) is NpcEntity or CharData) log_("Non-Mob passed into function.");
                    return Ret(l, false);
                }
                int list = mob.GetMobMod(MobMod.SkillList);
                if (list == 0) list = mob.MobSkillList;
                return Ret(l, GetMobSkillList(list).Count > 0);
            };
            m["getStatRank"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"getStatRank: invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                int stat = l.ToIntArg(2);
                byte? rank = stat switch
                {
                    1 => mob.StrRank, 2 => mob.DexRank, 3 => mob.VitRank, 4 => mob.AgiRank, 5 => mob.IntRank, 6 => mob.MndRank,
                    7 => mob.ChrRank, 8 => mob.AttRank, 9 => mob.DefRank, 10 => mob.AccRank, _ => null,
                };
                if (rank == null) log_($"getStatRank: unsupported stat type ({stat}) for mob ({mob.Name}).");
                return Ret(l, rank ?? 0);
            };
            // setStatRank(stat, rank): taken up on the mob's next spawn (CalculateMobStats); 1-7 for the base stats,
            // 1-5 for ATT, DEF and ACC
            m["setStatRank"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"setStatRank: invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (!mob.IsDead)
                    log_($"setStatRank: called on a live mob (id: {mob.Id}, name: {mob.Name}). Stat changes will take effect on the next spawn.");
                int stat = l.ToIntArg(2);
                byte rank = (byte)l.ToIntArg(3);
                int maxRank = stat switch { >= 1 and <= 7 => 7, >= 8 and <= 10 => 5, _ => 0 };
                if (maxRank == 0)
                {
                    log_($"setStatRank: unsupported stat type ({stat}) for mob ({mob.Name}).");
                    return 0;
                }
                if (rank == 0 || rank > maxRank)
                {
                    log_($"setStatRank: invalid rank ({rank}) for stat type ({stat}) on mob ({mob.Name}). Valid range is 1-{maxRank}.");
                    return 0;
                }
                switch (stat)
                {
                    case 1: mob.StrRank = rank; break;
                    case 2: mob.DexRank = rank; break;
                    case 3: mob.VitRank = rank; break;
                    case 4: mob.AgiRank = rank; break;
                    case 5: mob.IntRank = rank; break;
                    case 6: mob.MndRank = rank; break;
                    case 7: mob.ChrRank = rank; break;
                    case 8: mob.AttRank = rank; break;
                    case 9: mob.DefRank = rank; break;
                    case 10: mob.AccRank = rank; break;
                }
                return 0;
            };
            m["getModelSize"] = l => Ret(l, Battle(l) switch
            {
                MobEntity mob => mob.ModelSize,
                CharData c => c.ModelSize,
                _ => 0,
            });
            m["setModelSize"] = l =>
            {
                byte size = Math.Min((byte)l.ToIntArg(2), (byte)3);
                switch (Battle(l))
                {
                    case MobEntity mob: mob.ModelSize = size; break;
                    case CharData c: c.ModelSize = size; break;
                }
                return 0;
            };
            m["setHitboxSize"] = l =>
            {
                float size = (float)l.ToNumber(2);
                switch (Battle(l))
                {
                    case MobEntity mob: mob.ModelHitboxSize = size; break;
                    case CharData c: c.ModelHitboxOverride = size; break;
                }
                return 0;
            };

            // ---- positions, angles, parties ----
            // getWorldAngle(target, degrees) or getWorldAngle(x, y, z): the 0-255 angle to it, or in `degrees` steps
            m["getWorldAngle"] = l =>
            {
                var (x, _, z, rot) = PosOf(Entity(l));
                if (l.ToObject(2) is { } target)
                {
                    var (tx, _, tz, _) = PosOf(target);
                    int angle = WorldAngle(x, z, rot, tx, tz);
                    int degrees = l.IsNumber(3) ? (short)l.ToIntArg(3) : 256;
                    if (degrees != 256)
                    {
                        if (degrees % 4 == 0)
                        {
                            angle = (short)Math.Round(angle * Math.PI / 128 * (degrees / (2 * Math.PI)), MidpointRounding.AwayFromZero);
                            angle %= degrees; // rounded up to the "final" angle means the starting one
                        }
                        else log_($"getWorldAngle: Called with degrees {degrees} which isn't multiple of 4 ");
                    }
                    return Ret(l, angle);
                }
                float px = l.IsNumber(2) ? (float)l.ToNumber(2) : 0, pz = l.IsNumber(4) ? (float)l.ToNumber(4) : 0;
                // position_t{ x, y, z, moving = false, rotation = 0 }: the point's own rotation does not matter
                return Ret(l, WorldAngle(x, z, rot, px, pz));
            };
            m["isToEntitysLeft"] = l => Ret(l, ToEntitysSide(PosOf(Entity(l)), PosOf(l.ToObjectArg<object>(2)), l.IsNil(3) ? (byte)64 : (byte)l.ToIntArg(3), true));
            m["isToEntitysRight"] = l => Ret(l, ToEntitysSide(PosOf(Entity(l)), PosOf(l.ToObjectArg<object>(2)), l.IsNil(3) ? (byte)64 : (byte)l.ToIntArg(3), false));
            // getTrickAttackChar(target): the alliance member in line behind whom the trick attacker stands
            m["getTrickAttackChar"] = l =>
            {
                l.PushObject(Entity(l) is BattleEntity self && l.ToObject(2) is BattleEntity target ? TrickAttackChar(self, target) : null);
                return 1;
            };
            // getPartyMember(member, allianceParty): itself for (0, 0), else that member of its party or of that
            // alliance party
            m["getPartyMember"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_("CLuaBaseEntity::getPartyMember() - NPC calling function.");
                    l.PushNil();
                    return 1;
                }
                int member = (byte)l.ToIntArg(2), allianceParty = (byte)l.ToIntArg(3);
                BattleEntity found = null;
                if (allianceParty == 0 && member == 0) found = b;
                else if (b is CharData { Party: { } party })
                {
                    if (allianceParty == 0 && member < party.Members.Count) found = party.Members[member];
                    else if (party.Alliance != null && allianceParty < party.Alliance.PartyList.Count &&
                             member < party.Alliance.PartyList[allianceParty].Members.Count)
                        found = party.Alliance.PartyList[allianceParty].Members[member];
                }
                else if (b is MobEntity { LinkParty: { } linkParty } && allianceParty == 0 && member < linkParty.Count)
                    found = linkParty[member];
                if (found == null) log_("Lua::getPartyMember :: Member or Alliance Number is not valid.");
                l.PushObject(found);
                return 1;
            };

            // ---- messages, scripts, local vars ----
            m["messageStandard"] = l =>
            {
                if (Entity(l) is CharData c) PushTo(c, MessageStandard((ushort)l.ToIntArg(2)));
                return 0;
            };
            // sendEmptyEntityUpdateToPlayer(entity): a bare 0x00E for it (UpdateFlags 0x20, an equipped model)
            m["sendEmptyEntityUpdateToPlayer"] = l =>
            {
                if (Entity(l) is not CharData c || l.ToObject(2) is not { } e) return 0;
                var w = new PacketWriter(0x00E, 0x50);
                w.U32(0x04, IdOf(e));
                w.U16(0x08, TargIdOf(e));
                w.U8(0x0A, 0x20);
                w.U8(0x30, 0x01);
                PushTo(c, w.Packet);
                return 0;
            };
            m["hasListener"] = l =>
            {
                var events = Entity(l) switch
                {
                    MobEntity mob => mob.Events,
                    NpcEntity npc => npc.Events,
                    CharData c => c.Events,
                    _ => null,
                };
                return Ret(l, events != null && events.HasListener(l.ToStringArg(2)));
            };
            m["clearLocalVarsWithPrefix"] = l =>
            {
                var vars = LocalVars(Entity(l));
                string prefix = l.ToStringArg(2);
                foreach (var name in vars.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList()) vars[name] = 0;
                return 0;
            };
        }
    }
}
