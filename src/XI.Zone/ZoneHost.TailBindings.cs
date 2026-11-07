using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Binding batch 9: CLuaBaseEntity's countdown and objectiveUtility (GP_SERV_COMMAND_BATTLEFIELD's countdown,
    // progress bars, scoreboard, fence and help text arms), getItems, setPetName, getMinimumPetLevel, usePetAbility,
    // hasPartyJob, getShieldDefense, getSetBlueSpells, getSpawnSlotMobs, setLook, addWeaponSkillPoints and getHistory
    // (delCurrentQuest is ZoneHost.Progress's QuestWrite).
    public sealed partial class ZoneHost
    {
        // GP_SERV_COMMAND_BATTLEFIELD built up arm by arm, as its addCountdown / addBars / addScoreboard / addFence /
        // addHelpText do
        private sealed class BattlefieldPacket
        {
            private const int Body = 4;
            private readonly PacketWriter w_ = new PacketWriter(0x075, Body + S2cBattlefield.SizeOf);
            private byte flags_;
            private uint mode_;

            public BasicPacket Packet
            {
                get
                {
                    w_.U8(Body + S2cBattlefield.OffFlags, flags_);
                    w_.U32(Body + S2cBattlefield.OffMode, mode_);
                    return w_.Packet;
                }
            }

            public void AddCountdown(uint duration, uint warning, uint vanadielTimestamp)
            {
                flags_ |= (byte)ObjectiveUtilityType.Countdown;
                mode_ = mode_ == 0 ? 0x01u : mode_;
                w_.U32(Body + S2cBattlefield.OffTimestamp, vanadielTimestamp);
                w_.U32(Body + S2cBattlefield.OffDuration, duration);
                w_.U32(Body + S2cBattlefield.OffDurationWarn, warning);
            }

            public void AddBars(List<(byte[] Name, uint Value)> bars)
            {
                flags_ |= (byte)ObjectiveUtilityType.Progress;
                mode_ = 0xFFFF;
                for (int i = 0; i < bars.Count && i < ProgressbarArm.RowsCount; i++)
                {
                    int row = Body + S2cBattlefield.RowOffset(i);
                    w_.U32(row + ProgressbarRow.OffProgress, bars[i].Value);
                    var name = bars[i].Name;
                    w_.Bytes(row + ProgressbarRow.OffName, name.AsSpan(0, Math.Min(name.Length, ProgressbarRow.NameLen)));
                }
            }

            public void AddScoreboard(int marchlandScore, int strongholdScore, uint[] data)
            {
                mode_ = 0x1000;
                flags_ |= (byte)ObjectiveUtilityType.Progress;
                int sb = Body + S2cBattlefield.OffScoreboard;
                w_.I32(sb + ScoreboardArm.OffMarchlandScore, marchlandScore);
                w_.I32(sb + ScoreboardArm.OffStrongholdScore, strongholdScore);
                w_.U32(sb + ScoreboardArm.OffMarchlandProgress, data[0]);
                w_.U32(sb + ScoreboardArm.OffMarchlandProgressMax, data[1]);
                w_.U32(sb + ScoreboardArm.OffStrongholdProgress, data[2]);
                w_.U32(sb + ScoreboardArm.OffStrongholdProgressMax, data[3]);
                w_.U32(sb + ScoreboardArm.OffMarchlandNameOverride, data[4]);
                w_.U32(sb + ScoreboardArm.OffStrongholdNameOverride, data[5]);
            }

            public void AddFence(float x, float z, float radius, float render, bool blue, byte gateId)
            {
                flags_ |= (byte)ObjectiveUtilityType.Fence;
                w_.I32(Body + S2cBattlefield.OffFenceX, (int)(x * 1000));
                w_.I32(Body + S2cBattlefield.OffFenceY, (int)(z * 1000));
                w_.U32(Body + S2cBattlefield.OffFenceRadius, (uint)(radius * 1000));
                w_.U32(Body + S2cBattlefield.OffFenceRotation, (uint)(render * 1000));
                w_.U8(Body + S2cBattlefield.OffFenceColor, blue ? 1 : 0);
                w_.U8(Body + S2cBattlefield.OffFenceGateId, gateId);
            }

            public void AddHelpText(ushort title, ushort description)
            {
                flags_ |= (byte)ObjectiveUtilityType.Help;
                // the mini-game string table; the client adds 19 to the description
                w_.U16(Body + S2cBattlefield.OffMesNumTitle, title);
                w_.U16(Body + S2cBattlefield.OffMesNumDescription, description > 18 ? description - 19 : 0);
            }
        }

        private uint VanadielTimestampNow() => LoginPackets.VanadielTimestamp(UnixNow());

        // CPetController's precondition for usePetAbility: the attack state or no state at all
        private static bool InAttackStateOrNone(MobEntity m) =>
            !m.IsDead && m.SkillState == null && m.MagicState == null && m.RangedState == null && m.InactiveState == null;

        private static byte[] FieldBytes(LuaState l, int table, string key)
        {
            l.GetField(table, key);
            var v = l.IsString(-1) || l.IsNumber(-1) ? l.ToBytes(-1) : Array.Empty<byte>();
            l.Pop(1);
            return v;
        }

        private void RegisterTailBindings(Dictionary<string, LuaFunction> m)
        {
            // countdown(seconds): the countdown on the char's screen; without a number, the cleared packet
            m["countdown"] = l =>
            {
                if (Pc(l, "countdown") is not CharData c) return 0;
                var p = new BattlefieldPacket();
                if (l.IsNumber(2)) p.AddCountdown(OptU32(l, 2), 0, VanadielTimestampNow());
                PushTo(c, p.Packet);
                return 0;
            };
            // objectiveUtility({ countdown, bars, scoreboard, fence, help }): the arms the table names in one packet
            m["objectiveUtility"] = l =>
            {
                if (Pc(l, "objectiveUtility") is not CharData c) return 0;
                var p = new BattlefieldPacket();
                if (l.IsTable(2))
                {
                    l.GetField(2, "countdown");
                    int t = l.Top;
                    if (l.IsTable(t))
                        p.AddCountdown((uint)FieldInt(l, t, "duration", 0), (uint)FieldInt(l, t, "warning", 0), VanadielTimestampNow());
                    else if (l.IsNumber(t))
                        p.AddCountdown(OptU32(l, t), 0, VanadielTimestampNow());
                    l.Pop(1);

                    l.GetField(2, "bars");
                    t = l.Top;
                    if (l.IsTable(t))
                    {
                        var bars = new List<(byte[], uint)>();
                        for (int i = 1; i <= ProgressbarArm.RowsCount; i++)
                        {
                            l.RawGetI(t, i);
                            int row = l.Top;
                            bars.Add(l.IsTable(row) ? (FieldBytes(l, row, "title"), (uint)FieldInt(l, row, "value", 0)) : (Array.Empty<byte>(), 0u));
                            l.Pop(1);
                        }
                        p.AddBars(bars);
                    }
                    l.Pop(1);

                    l.GetField(2, "scoreboard");
                    t = l.Top;
                    if (l.IsTable(t))
                    {
                        string[] keys = { "marchlandProgress", "marchlandProgressMax", "strongholdProgress", "strongholdProgressMax",
                                          "marchlandNameOverride", "strongholdNameOverride" };
                        p.AddScoreboard((int)(uint)FieldInt(l, t, "marchlandScore", 0), (int)(uint)FieldInt(l, t, "strongholdScore", 0),
                                        keys.Select(k => (uint)FieldInt(l, t, k, 0)).ToArray());
                    }
                    l.Pop(1);

                    l.GetField(2, "fence");
                    t = l.Top;
                    if (l.IsTable(t))
                    {
                        float x = 0, z = 0;
                        l.GetField(t, "pos");
                        if (l.IsTable(-1))
                        {
                            int pos = l.Top;
                            x = (float)FieldNumber(l, pos, "x", 0);
                            z = (float)FieldNumber(l, pos, "z", 0);
                        }
                        l.Pop(1);
                        byte gateId = (byte)FieldInt(l, t, "gateId", c.StatusEffects.GetConfrontationSubPower() & 0x0F);
                        p.AddFence(x, z, (float)FieldNumber(l, t, "radius", 0), (float)FieldNumber(l, t, "render", 25), FieldBool(l, t, "blue"), gateId);
                    }
                    l.Pop(1);

                    l.GetField(2, "help");
                    t = l.Top;
                    if (l.IsTable(t)) p.AddHelpText((ushort)FieldInt(l, t, "title", 0), (ushort)FieldInt(l, t, "description", 0));
                    l.Pop(1);
                }
                PushTo(c, p.Packet);
                return 0;
            };

            // getItems(location): the items in that container (the inventory by default or out of range), slot order
            m["getItems"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not CharData c) return 1;
                int location = (int)Container.Inventory;
                if (l.IsNumber(2))
                {
                    location = (byte)l.ToIntArg(2);
                    if (location >= CharData.MaxContainers) location = (int)Container.Inventory;
                }
                int n = 0;
                foreach (var item in c.Items.Where(i => (int)i.Location == location).OrderBy(i => i.Slot))
                {
                    l.PushObject(item, ItemType);
                    l.RawSetI(-2, ++n);
                }
                return 1;
            };

            // setPetName(type, value[, value2]): char_pet's wyvern / automaton name (the automaton reloaded), or a
            // chocobo's two name parts
            m["setPetName"] = l =>
            {
                if (Pc(l, "setPetName") is not CharData c) return 0;
                var type = (PetType)(byte)l.ToIntArg(2);
                ushort value = (ushort)l.ToIntArg(3);
                if (l.IsNil(4))
                {
                    if (type == PetType.Wyvern)
                        Exec("INSERT INTO char_pet SET charid = ?, wyvernid = ? ON DUPLICATE KEY UPDATE wyvernid = ?",
                             BoundValue.Of(c.Id), BoundValue.Of(value), BoundValue.Of(value));
                    else if (type == PetType.Automaton)
                    {
                        Exec("INSERT INTO char_pet SET charid = ?, automatonid = ? ON DUPLICATE KEY UPDATE automatonid = ?",
                             BoundValue.Of(c.Id), BoundValue.Of(value), BoundValue.Of(value));
                        LoadAutomaton(c);
                    }
                }
                else if (l.IsNumber(4) && type == PetType.Chocobo)
                {
                    uint name = (value & 0xFFFFu) + ((uint)l.ToIntegral(4) << 16);
                    Exec("INSERT INTO char_pet SET charid = ?, chocoboid = ? ON DUPLICATE KEY UPDATE chocoboid = ?",
                         BoundValue.Of(c.Id), BoundValue.Of(name), BoundValue.Of(name));
                }
                return 0;
            };
            // getMinimumPetLevel: pet_list's minLevel for the pet
            m["getMinimumPetLevel"] = l =>
            {
                if (Entity(l) is PetEntity pet) return Ret(l, PetList().TryGetValue(pet.PetId, out var info) ? info.MinLevel : 0);
                log_("Non-CPetEntity called this function");
                return Ret(l, 0);
            };
            // usePetAbility(skillId, target): from the attack state or none, able to act, a pet skill: the pet uses it on
            // the target, or a mob pet on its battle target
            m["usePetAbility"] = l =>
            {
                if (Entity(l) is not PetEntity pet || !InAttackStateOrNone(pet)) return 0;
                if (pet.StatusEffects.HasPreventActionEffect()) return 0;
                ushort skillId = (ushort)l.ToIntArg(2);
                if (GetPetSkill(skillId) == null) return 0;
                var target = l.ToObject(3) as BattleEntity;
                if (target != null && EntityExists(target)) PetUseSkill(pet, target, skillId, NowMs());
                else PetUseSkill(pet, pet.BattleTarget, skillId, NowMs());
                return 0;
            };

            // hasPartyJob(job): a char (or a trust's master) whose party has a char or a char's trust of that main job
            m["hasPartyJob"] = l =>
            {
                var c = Entity(l) as CharData ?? (Entity(l) as TrustEntity)?.PMaster as CharData;
                if (c?.Party == null) return Ret(l, false);
                int job = l.ToIntArg(2);
                foreach (var member in c.Party.Members)
                {
                    if (member.MJob == job) return Ret(l, true);
                    if (member.PTrusts.Any(t => t.MJob == job)) return Ret(l, true);
                }
                return Ret(l, false);
            };
            // getShieldDefense: the DEF of the shield in the sub slot
            m["getShieldDefense"] = l =>
            {
                if (Entity(l) is CharData c)
                    return Ret(l, c.Equipped[CharStats.SlotSub] is { IsShield: true } shield ? shield.GetModifier(Mod.DEF) : 0);
                log_($"Entity is not a Player: ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            // getSetBlueSpells: the set blue magic's spell ids
            m["getSetBlueSpells"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling getSetBlueSpells ({NameOf(Entity(l))}).");
                    return 1;
                }
                int n = 0;
                foreach (byte spell in c.SetBlueSpells)
                {
                    if (spell == 0) continue;
                    l.Push(spell + BlueSpellOffset);
                    l.RawSetI(-2, ++n);
                }
                return 1;
            };
            // getSpawnSlotMobs: the ids of the mobs sharing the mob's spawn slot
            m["getSpawnSlotMobs"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get spawn slot members for invalid entity type ({NameOf(Entity(l))}).");
                    return 1;
                }
                int n = 0;
                foreach (var entry in mob.Slot?.Entries ?? Enumerable.Empty<SpawnSlot.Entry>())
                {
                    if (entry.Mob == null) continue;
                    l.Push(entry.Mob.Id);
                    l.RawSetI(-2, ++n);
                }
                return 1;
            };
            // setLook({ face, race }): an NPC drawn as an equipped model of that face and race
            m["setLook"] = l =>
            {
                if (Entity(l) is NpcEntity npc)
                {
                    npc.Look[0] = (ushort)ModelType.Equipped;
                    byte face = 0, race = 0;
                    if (l.IsTable(2))
                    {
                        face = (byte)FieldInt(l, 2, "face", 0);
                        race = (byte)FieldInt(l, 2, "race", 0);
                    }
                    npc.Look[1] = (ushort)(face | race << 8);
                    return 0;
                }
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            // addWeaponSkillPoints(slot, points): charutils::AddWeaponSkillPoints
            m["addWeaponSkillPoints"] = l =>
            {
                if (Pc(l, "addWeaponSkillPoints") is not CharData c) return Ret(l, false);
                int slot = (byte)l.ToIntArg(2);
                return Ret(l, slot < 16 && AddWeaponSkillPoints(c, slot, (ushort)l.ToIntArg(3)));
            };
            // getHistory(index): a char's m_charHistory counter
            m["getHistory"] = l =>
            {
                int index = (byte)l.ToIntArg(2);
                return Ret(l, Entity(l) is CharData c && index < HistoryCount ? c.History[index] : 0);
            };
        }
    }
}
