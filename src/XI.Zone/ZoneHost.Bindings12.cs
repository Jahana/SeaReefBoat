using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity bindings, batch 12: getEntitiesInRange (CTargetFind's area, cone or single search on the skill
    // finder), positionSpecial (GP_SERV_COMMAND_WPOS with a mode; LOCK / UNLOCK lock the char), the trigger-area set
    // (onPlayerTriggerAreaEnter / Leave, clearPlayerTriggerAreas), getMasterThreatMob, addWyvernJobTraits, printAllMods,
    // isFellow and petAbility (a warning, as C++). Batch 13: injectPacket (a packet file to the char), sendDebugPacket (a
    // table of bytes), startSequence (inSequence), raceChange (charutils::raceChange), breakLinkshell (by name) and get /
    // setMannequinPose (the mannequin's exdata race and pose). Batch 14: jail (jailutils::Add), sendChocoboRace
    // (ZoneHost.ChocoboRace) and sendLinkshellConcierge (ZoneHost.Linkshell).
    public sealed partial class ZoneHost
    {
        // POSMODE (packets/s2c/0x05b_wpos.h)
        private const byte PosModePop = 0x03, PosModeMaterialize = 0x06, PosModeLock = 0x08, PosModeUnlock = 0x09, PosModeRotate = 0x0A;
        // CharRace / CharFace / CharSize bounds
        private const byte RaceHumeMale = 1, RaceGalka = 8, FaceMax = 15, SizeLarge = 2;

        // CCharEntity::setLocked: a locked char drops its fight (its pet too) and its claim
        private void SetLocked(CharData c, bool locked, long now)
        {
            c.Locked = locked;
            if (!locked) return;
            if (c.BattleTarget != null) EndAttackState(c, now);
            if (c.PPet is MobEntity pet && pet.Engaged) MobDisengage(pet, now);
            RelinquishClaim(c, now);
        }
        // AOE_TYPE (mobskill.h)
        private const byte AoeTypeRound = 1, AoeTypeCone = 4;
        private const ushort TargetSelfFlag = 0x0001; // TARGET_SELF

        // GP_SERV_COMMAND_WPOS for an entity with a POSMODE: NORMAL, EVENT, POP, RESET and MATERIALIZE move it there,
        // ROTATE only turns it
        private static BasicPacket WposMode(object e, (float X, float Y, float Z, byte Rot) pos, byte mode)
        {
            if (mode is PosModeNormal or PosModeEvent or PosModePop or PosModeReset or PosModeMaterialize)
            {
                if (e is BattleEntity b) (b.X, b.Y, b.Z, b.Rotation) = pos;
                else if (e is NpcEntity n) (n.X, n.Y, n.Z, n.Rotation) = pos;
            }
            else if (mode == PosModeRotate)
            {
                if (e is BattleEntity b) b.Rotation = pos.Rot;
                else if (e is NpcEntity n) n.Rotation = pos.Rot;
            }
            var (x, y, z, rot) = PosOf(e);
            var (id, targId) = e switch
            {
                BattleEntity b => (b.Id, b.TargId),
                NpcEntity n => (n.Id, n.TargId),
                _ => (0u, (ushort)0),
            };
            var w = new PacketWriter(0x05B, 4 + S2cWpos.SizeOf);
            w.F32(4 + S2cWpos.OffX, x);
            w.F32(4 + S2cWpos.OffY, y);
            w.F32(4 + S2cWpos.OffZ, z);
            w.U32(4 + S2cWpos.OffUniqueNo, id);
            w.U16(4 + S2cWpos.OffActIndex, targId);
            w.U8(4 + S2cWpos.OffMode, mode);
            w.U8(4 + S2cWpos.OffDir, rot);
            return w.Packet;
        }

        // getMasterThreatMob's test: the master is the top of the mob's active list (CE + VE, living owners)
        private static bool IsMasterTopEnmityOnMob(MobEntity mob, BattleEntity master)
        {
            BattleEntity top = null;
            int topHate = int.MinValue;
            foreach (var entry in mob.Enmity.Entries)
            {
                if (!entry.Active || entry.Owner == null || !entry.Owner.IsAlive) continue;
                int totalHate = entry.CE + entry.VE;
                if (totalHate > topHate)
                {
                    topHate = totalHate;
                    top = entry.Owner;
                }
            }
            return top != null && top.Id == master.Id;
        }

        private void RegisterBindings12(Dictionary<string, LuaFunction> m)
        {
            m["getEntitiesInRange"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 1;
                }
                if (l.ToObject(2) is not BattleEntity target)
                {
                    log_($"Invalid target entity in function ({NameOf(b)}).");
                    return 1;
                }
                float distance = l.IsNumber(5) ? (float)l.ToNumber(5) : 0;
                if (distance < 0)
                {
                    log_($"Invalid distance in function call ({NameOf(b)}).");
                    return 1;
                }
                byte findFlags = l.IsNumber(6) ? (byte)l.ToIntArg(6) : (byte)0;
                ushort validTargets = l.IsNumber(7) ? (ushort)l.ToIntArg(7) : (ushort)0;
                // mob buff abilities also hit the monster's pets
                if ((validTargets & TargetSelfFlag) != 0) findFlags |= FindFlagsPet;
                if ((validTargets & TargetIgnoreBattleId) == TargetIgnoreBattleId) findFlags |= FindFlagsIgnoreBattleId;
                int aoeType = l.IsNumber(3) ? l.ToIntArg(3) : 0;
                byte radiusOrigin = l.IsNumber(4) ? (byte)l.ToIntArg(4) : (byte)0;

                var find = new SkillTargetFind(this, b);
                // the target finder always adds the primary target: it must be valid
                if (radiusOrigin == 2 || Distance(b, target) <= distance)
                {
                    if (aoeType == AoeTypeRound) find.WithinArea(target, radiusOrigin, distance, validTargets, findFlags);
                    else if (aoeType == AoeTypeCone) find.WithinCone(target, distance, 45.0f, validTargets, (byte)aoeType, findFlags);
                    else
                    {
                        var single = target;
                        if (b is MobEntity && target is CharData && CoverAbilityUser(target, b) is { } coverUser) single = coverUser;
                        find.SingleTarget(single, validTargets, findFlags);
                    }
                }
                int i = 1;
                foreach (var found in find.Targets)
                {
                    l.PushObject(found);
                    l.RawSetI(-2, i++);
                }
                return 1;
            };

            m["positionSpecial"] = l =>
            {
                var e = Entity(l);
                if (e is not BattleEntity and not NpcEntity || !l.IsTable(2)) return 0;
                var pos = ((float)FieldNumber(l, 2, "x", 0), (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0),
                           (byte)FieldNumber(l, 2, "rot", 0));
                byte mode = (byte)l.ToEnumArg(3); // POSMODE
                // RESET unlocks a char, LOCK / UNLOCK set it
                if (e is CharData lc && mode is PosModeReset or PosModeLock or PosModeUnlock) SetLocked(lc, mode == PosModeLock, NowMs());
                PushInRangeSelf(e, WposMode(e, pos, mode));
                return 0;
            };

            m["injectPacket"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                byte[] data;
                try
                {
                    data = File.ReadAllBytes(l.ToStringValue(2));
                }
                catch (Exception)
                {
                    log_("CLuaBaseEntity::injectPacket : Cannot open file");
                    return 0;
                }
                if (data.Length < 2)
                {
                    log_("CLuaBaseEntity::injectPacket : Did not read size");
                    return 0;
                }
                // the size at byte 1, in words of 2
                int size = data[1] * 2;
                if (data.Length < size)
                {
                    log_("CLuaBaseEntity::injectPacket : Did not read entire packet");
                    return 0;
                }
                PushTo(c, new BasicPacket(data.AsSpan(0, size).ToArray()));
                return 0;
            };

            m["sendDebugPacket"] = l =>
            {
                if (Entity(l) is not CharData c || !l.IsTable(2)) return 0;
                int n = l.ObjLen(2);
                var bytes = new byte[Math.Max(n, 4)];
                for (int i = 0; i < n; i++)
                {
                    l.RawGetI(2, i + 1);
                    bytes[i] = l.IsNumber(-1) ? (byte)l.ToIntegral(-1) : (byte)0;
                    l.Pop(1);
                }
                PushTo(c, new BasicPacket(bytes));
                return 0;
            };

            // the char's party drops its sync (the designee) or forgets it, and the others in range get the char's levels
            m["disableLevelSync"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (c.Party != null)
                {
                    if (c.Party.SyncTarget == c) c.Party.SetSyncTarget("", MsgStdLevelSyncRemoveLeftParty);
                    else c.Party.DisableSync();
                }
                PushCharInRange(c, CharSyncOf(c), false);
                return 0;
            };

            m["startSequence"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, false);
                c.InSequence = true;
                return Ret(l, true);
            };

            m["sendChocoboRace"] = l =>
            {
                if (Entity(l) is CharData c && l.IsTable(2)) SendChocoboRace(c, l, 2);
                return 0;
            };
            m["sendLinkshellConcierge"] = l =>
            {
                if (Entity(l) is CharData c && l.IsTable(2)) SendLinkshellConcierge(c, l, 2);
                return 0;
            };

            // jailutils::Add (Mordion Gaol's onZoneIn for an inJail char)
            m["jail"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                c.NoController = true;
                return 0;
            };

            // charutils::raceChange: char_look's face, race and size, the gear the new race cannot wear off, a rezone
            m["raceChange"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int race = l.ToEnumArg(2), face = l.ToEnumArg(3), size = l.ToEnumArg(4); // CharRace, CharFace, CharSize
                if (race < RaceHumeMale || race > RaceGalka || face > FaceMax || size > SizeLarge)
                {
                    log_($"charutils::raceChange: Arguments out of bounds for charid: {c.Id}");
                    return Ret(l, false);
                }
                try
                {
                    Exec("UPDATE char_look SET face = ?, race = ?, size = ? WHERE charid = ?",
                         BoundValue.Of((byte)face), BoundValue.Of((byte)race), BoundValue.Of((byte)size), BoundValue.Of(c.Id));
                }
                catch (Exception)
                {
                    log_($"charutils::raceChange: Failed to update char_look for charid: {c.Id}");
                    return Ret(l, false);
                }
                (c.Look.Face, c.Look.Race, c.Look.Size) = ((byte)face, (byte)race, (byte)size);
                for (int slot = CharStats.SlotMain; slot <= CharStats.SlotBack; slot++)
                    if (c.Equipped[slot] is { } item && !item.EquippableByRace(race)) loader_.Stats?.UnequipItem(c, slot);
                ForceRezone(c);
                return Ret(l, true);
            };

            m["breakLinkshell"] = l =>
            {
                var r = Exec("SELECT broken, linkshellid FROM linkshells WHERE name = ?", BoundValue.Of(l.ToStringValue(2)));
                if (r == null || !r.Next()) return Ret(l, false);
                if (r.Get<byte>("broken") != 0) return Ret(l, true);
                uint lsid = r.Get<uint>("linkshellid");
                (GetLinkshell(lsid) ?? LoadLinkshell(lsid))?.BreakLinkshell();
                UnloadLinkshell(lsid);
                return Ret(l, true);
            };

            // the char's mannequin of that id (one of each at most), in any container
            Item Mannequin(CharData c, ushort itemId)
            {
                for (int i = 0; i < CharData.MaxContainers; i++)
                    if (SearchItem(c, (Container)i, itemId) is { } item && IsMannequin(item)) return item;
                return null;
            }
            m["getMannequinPose"] = l =>
            {
                if (Entity(l) is not CharData c || Mannequin(c, (ushort)l.ToIntArg(2)) is not { } item) return Ret(l, 0);
                return Ret(l, item.Extra[MannequinPoseOffset]);
            };
            m["setMannequinPose"] = l =>
            {
                if (Entity(l) is not CharData c || Mannequin(c, (ushort)l.ToIntArg(2)) is not { } item) return 0;
                item.Extra[MannequinRaceOffset] = (byte)l.ToIntArg(3);
                item.Extra[MannequinPoseOffset] = (byte)l.ToIntArg(4);
                if (Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND itemId = ? LIMIT 1",
                         BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of(item.ItemId)) == null)
                    log_("lua_baseentity::setMannequinPose: Cannot insert item to database");
                return 0;
            };

            m["onPlayerTriggerAreaEnter"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                ZoneLuaChar(c).TriggerAreaIds.Add(l.ToUIntArg(2));
                return 0;
            };
            m["onPlayerTriggerAreaLeave"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                ZoneLuaChar(c).TriggerAreaIds.Remove(l.ToUIntArg(2));
                return 0;
            };
            m["clearPlayerTriggerAreas"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                ZoneLuaChar(c).TriggerAreaIds.Clear();
                return 0;
            };

            // the pet's master's threat: the nearest-listed mob (within 22, or the override) on the master's notoriety
            // that targets the master or has it on top, preferring one that is not the master's own target
            m["getMasterThreatMob"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Attempting to get master threat target for invalid entity type ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                if (b.PMaster is not BattleEntity master)
                {
                    l.PushNil();
                    return 1;
                }
                float maxDistance = l.IsNumber(2) ? (float)l.ToNumber(2) : 22.0f;
                BattleEntity mastersTarget = master switch
                {
                    CharData mc => mc.BattleTarget,
                    MobEntity mm => mm.BattleTarget,
                    _ => null,
                };
                MobEntity threatening = null;
                foreach (var mob in master.Notoriety)
                {
                    if (!mob.IsAlive || Distance(master, mob) > maxDistance) continue;
                    bool isTargetingMaster = mob.BattleTarget != null && mob.BattleTarget.Id == master.Id;
                    if (isTargetingMaster || IsMasterTopEnmityOnMob(mob, master))
                    {
                        if (mastersTarget == null || mob.Id != mastersTarget.Id)
                        {
                            l.PushObject(mob);
                            return 1;
                        }
                        threatening ??= mob;
                    }
                }
                if (threatening != null) l.PushObject(threatening);
                else l.PushNil();
                return 1;
            };

            // a DRG's wyvern takes a job's traits (its master's sub job under WYVERN_SUBJOB_TRAITS)
            m["addWyvernJobTraits"] = l =>
            {
                if (Entity(l) is PetEntity { PetType: PetType.Wyvern } pet)
                    AddTraits(pet, TraitTableOf().ForJob((byte)l.ToIntArg(2), log_), (byte)l.ToIntArg(3));
                else
                    log_($"Invalid entity type calling addWyvernJobTraits ({NameOf(Entity(l))}).");
                return 0;
            };

            m["printAllMods"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                var mods = Enum.GetValues<Mod>().Distinct().ToArray();
                int longest = mods.Max(mod => mod.ToString().Length);
                log_($"{b.Name}'s ({b.Id}) mods:");
                foreach (var mod in mods)
                {
                    int value = b.GetMod(mod);
                    if (value != 0) log_($"| {mod.ToString().PadRight(longest)} | {value,-8} |");
                }
                return 0;
            };

            // no fellows in this port, as there is no TYPE_FELLOW entity
            m["isFellow"] = l => Ret(l, false);

            m["petAbility"] = l =>
            {
                log_($"CLuaBaseEntity::petAbility() - Non-implemented function called with parameter {l.ToIntArg(2)}");
                return 0;
            };
        }
    }
}
