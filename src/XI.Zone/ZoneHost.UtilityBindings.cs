using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Small CLuaBaseEntity bindings: GM flags (hidden, wallhack, freeze), equipment locks, messages and emotes, NPC
    // showing, teleport, the entity data store, rune counts and a few getters.
    public sealed partial class ZoneHost
    {
        private const byte CharBaseSpeed = 50;              // map.BASE_SPEED, a char's baseSpeed
        private const ushort EffectBust = 309, EffectIgnis = 523, EffectTenebrae = 530;
        private const ushort MsgStdCompass = 239;           // The compass reads: ...
        private const ushort WeaknessTriggerParam = 2582;
        private const byte ActionCategoryMobSkillFinish = 11;

        // ---- xi.entityData ----------------------------------------------------------------------------------------

        // CBaseEntity::serial(): never reused, so a table left behind is never read again
        private readonly Dictionary<object, double> entitySerials_ = new Dictionary<object, double>(ReferenceEqualityComparer.Instance);
        private double nextEntitySerial_ = 1;

        private double EntitySerial(object e)
        {
            if (!entitySerials_.TryGetValue(e, out var serial)) entitySerials_[e] = serial = nextEntitySerial_++;
            return serial;
        }

        // luautils::detail::getEntityDataTable: xi.entityData[serial], made on the way when `create`; pushes it and
        // returns true, or pushes nothing
        private bool PushEntityData(LuaState l, object e, bool create)
        {
            double serial = EntitySerial(e);
            l.GetGlobal("xi");
            if (!l.IsTable(-1))
            {
                l.Pop(1);
                if (!create) return false;
                l.NewTable();
                l.PushValue(-1);
                l.SetGlobal("xi");
            }
            l.GetField(-1, "entityData");
            if (!l.IsTable(-1))
            {
                l.Pop(1);
                if (!create)
                {
                    l.Pop(1);
                    return false;
                }
                l.NewTable();
                l.PushValue(-1);
                l.SetField(-3, "entityData");
            }
            l.Push(serial);
            l.GetTable(-2);
            if (!l.IsTable(-1))
            {
                l.Pop(1);
                if (!create)
                {
                    l.Pop(2);
                    return false;
                }
                l.NewTable();
                l.Push(serial);
                l.PushValue(-2);
                l.SetTable(-4);
            }
            l.Remove(-2);
            l.Remove(-2);
            return true;
        }

        // luautils::resetEntityData: on spawn (CBaseEntity::Spawn), resetData, and when the entity goes away
        // (~CBaseEntity), when the serial is let go too
        private void ResetEntityData(object e, bool destroyed = false)
        {
            if (lua_ == null || !entitySerials_.TryGetValue(e, out var serial)) return;
            if (destroyed) entitySerials_.Remove(e);
            lua_.GetGlobal("xi");
            if (lua_.IsTable(-1))
            {
                lua_.GetField(-1, "entityData");
                if (lua_.IsTable(-1))
                {
                    lua_.Push(serial);
                    lua_.PushNil();
                    lua_.SetTable(-3);
                }
                lua_.Pop(1);
            }
            lua_.Pop(1);
        }

        // ---- packets ----------------------------------------------------------------------------------------------

        // 0x009 GP_SERV_COMMAND_MESSAGE(param0..3, messageID): "Para0 %u Para1 %u Para2 %u Para3 %u", the size
        // (0x10 + strlen / 2) & 0xFE, so the client gets the text only as far as that reaches
        private static BasicPacket MessageParams4(uint p0, uint p1, uint p2, uint p3, ushort messageId)
        {
            var text = Encoding.ASCII.GetBytes($"Para0 {p0} Para1 {p1} Para2 {p2} Para3 {p3}");
            int len = Math.Min(text.Length, 99);
            int size = (((0x10 + (len >> 1)) & 0xFE) + 3) & ~3;
            var w = new PacketWriter(0x009, (uint)size);
            w.U16(10, messageId);
            w.Bytes(13, text.AsSpan(0, Math.Min(len, size - 13)));
            return w.Packet;
        }

        // 0x02D GP_SERV_COMMAND_BATTLE_MESSAGE2 for any speaker
        private static BasicPacket BattleMessage2Of(object sender, object target, int param, int value, ushort messageId)
        {
            var w = new PacketWriter(0x02D, 0x1C);
            w.U32(4, IdOf(sender));
            w.U32(8, IdOf(target));
            w.U16(12, TargIdOf(sender));
            w.U16(14, TargIdOf(target));
            w.U32(16, (uint)param);
            w.U32(20, (uint)value);
            w.U16(24, messageId);
            return w.Packet;
        }

        // 0x05A GP_SERV_COMMAND_MOTIONMES from an NPC: the emote and mode, no param
        private static BasicPacket NpcMotionMes(NpcEntity npc, uint targetId, ushort targetIndex, byte emote, byte mode)
        {
            var w = new PacketWriter(0x05A, 56);
            w.U32(4, npc.Id);
            w.U32(8, targetId);
            w.U16(12, npc.TargId);
            w.U16(14, targetIndex);
            w.U16(16, emote);
            w.U8(22, mode);
            return w.Packet;
        }

        // 0x077 GP_SERV_COMMAND_ENTITY_VIS: Flags 1, then up to 32 entity ids
        private static BasicPacket EntityVis(IReadOnlyList<uint> ids)
        {
            var w = new PacketWriter(0x077, 4 + S2cEntityVis.SizeOf);
            w.U8(4 + S2cEntityVis.OffFlags, 1);
            for (int i = 0; i < Math.Min(ids.Count, S2cEntityVis.UniqueNoCount); i++) w.U32(4 + S2cEntityVis.UniqueNoOffset(i), ids[i]);
            return w.Packet;
        }

        // 0x05B GP_SERV_COMMAND_WPOS(NORMAL) for any entity, at where it now stands
        private static BasicPacket WposOf(object e)
        {
            var (x, y, z, rot) = PosOf(e);
            var w = new PacketWriter(0x05B, 4 + S2cWpos.SizeOf);
            w.F32(4 + S2cWpos.OffX, x);
            w.F32(4 + S2cWpos.OffY, y);
            w.F32(4 + S2cWpos.OffZ, z);
            w.U32(4 + S2cWpos.OffUniqueNo, IdOf(e));
            w.U16(4 + S2cWpos.OffActIndex, TargIdOf(e));
            w.U8(4 + S2cWpos.OffDir, rot);
            return w.Packet;
        }

        // CHAR_INRANGE_SELF from any entity: a char gets it too, others have no session
        private void PushInRangeSelf(object e, BasicPacket p)
        {
            if (e is CharData c) PushCharInRange(c, p, true);
            else PushCharsNear(e, p);
        }

        // ---- GM hiding ----------------------------------------------------------------------------------------------

        // setGMHidden: the zone's chars get the despawn (or spawn) at once; SpawnPCs drops (or adds) it after
        private void SetGmHidden(CharData c, bool hidden)
        {
            c.GmHidden = hidden;
            c.UpdateMask |= CharStats.UpdateHp;
            Exec("UPDATE char_flags SET gmHiddenEnabled = ? WHERE charid = ?", BoundValue.Of(hidden ? 1 : 0), BoundValue.Of(c.Id));
            if (SessionOf(c) is not { InZone: true }) return;
            foreach (var (_, os) in ZoneChars(c))
                if (hidden) UpdateCharPacket(os, c, EntityUpdate.Despawn, 0);
                else UpdateCharPacket(os, c, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
        }

        private void RegisterUtilityBindings(Dictionary<string, LuaFunction> m)
        {
            m["setGMHidden"] = l =>
            {
                if (Pc(l, "setGMHidden") is CharData c) SetGmHidden(c, l.ToBoolean(2));
                return 0;
            };
            m["setWallhack"] = l =>
            {
                if (Pc(l, "setWallhack") is not CharData c) return 0;
                c.WallhackEnabled = l.ToBoolean(2);
                c.UpdateMask |= CharStats.UpdateHp;
                return 0;
            };
            m["setFreezeFlag"] = l =>
            {
                if (Pc(l, "setFreezeFlag") is not CharData c) return 0;
                c.FrozenFlagged = l.ToBoolean(2);
                c.UpdateMask |= CharStats.UpdateHp;
                return 0;
            };

            // ---- equipment locks ----
            m["setEquipBlock"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                c.EquipBlock = (ushort)l.ToIntArg(2);
                PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
                return 0;
            };
            // lockEquipSlot(slot): off and locked; the look, the slot and the encumbrance to the char
            m["lockEquipSlot"] = l =>
            {
                if (Pc(l, "lockEquipSlot") is not CharData c) return 0;
                byte slot = (byte)l.ToIntArg(2);
                if (slot > 15)
                {
                    log_("Invalid slot passed to function.");
                    return 0;
                }
                loader_.Stats?.EquipItem(c, 0, slot, Container.Inventory);
                c.EquipBlock |= (ushort)(1 << slot);
                PushTo(c, LoginPackets.GrapList(c));
                PushTo(c, LoginPackets.EquipList(0, slot, Container.Inventory));
                PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
                c.UpdateMask |= CharStats.UpdateLook;
                return 0;
            };
            m["unlockEquipSlot"] = l =>
            {
                if (Pc(l, "unlockEquipSlot") is not CharData c) return 0;
                byte slot = (byte)l.ToIntArg(2);
                if (slot > 15)
                {
                    log_("Invalid slot passed to function.");
                    return 0;
                }
                c.EquipBlock &= (ushort)~(1 << slot);
                PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
                return 0;
            };
            // setStatDebilitation(power): the debilitation arrows, in the encumbrance's high half
            m["setStatDebilitation"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                c.StatsDebilitation = (ushort)l.ToIntArg(2);
                PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
                return 0;
            };
            m["hasSlotEquipped"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Attempt to check item slot for a non-player ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int slot = l.ToIntArg(2);
                // a linkshell is not ITEM_EQUIPMENT
                return Ret(l, slot is >= 0 and < 16 && CharStats.EquippedItem(c, slot) != null);
            };
            // getWornUses(itemId): the use count in the first inventory stack of it
            m["getWornUses"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, 0);
                ushort id = (ushort)l.ToIntArg(2);
                var item = c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == id).OrderBy(i => i.Slot).FirstOrDefault();
                return Ret(l, item?.Extra[0] ?? 0);
            };
            // incrementItemWear(itemId): one more use on the first inventory stack's worn-item count (capped at 255),
            // which it returns; 0 without one
            m["incrementItemWear"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, 0);
                ushort id = (ushort)l.ToIntArg(2);
                var item = c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == id).OrderBy(i => i.Slot).FirstOrDefault();
                if (item == null) return Ret(l, 0);
                item.Extra[0] = (byte)Math.Min(item.Extra[0] + 1, byte.MaxValue);
                Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                     BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                return Ret(l, item.Extra[0]);
            };
            // findItems(itemId, container): the stacks of it in that container (inventory when out of range), or in
            // every container, slot order
            m["findItems"] = l =>
            {
                var c = Entity(l) as CharData;
                ushort id = (ushort)l.ToIntArg(2);
                IEnumerable<int> containers;
                if (!l.IsNil(3))
                {
                    int location = (byte)l.ToIntArg(3);
                    containers = new[] { location < CharData.MaxContainers ? location : (int)Container.Inventory };
                }
                else containers = Enumerable.Range(0, CharData.MaxContainers);
                l.NewTable();
                if (c == null) return 1;
                int n = 0;
                foreach (int container in containers)
                    foreach (var item in c.Items.Where(i => (int)i.Location == container && i.ItemId == id).OrderBy(i => i.Slot))
                    {
                        l.PushObject(item, ItemType);
                        l.RawSetI(-2, ++n);
                    }
                return 1;
            };
            m["getEquipmentModelIds"] = l =>
            {
                var c = Entity(l) as CharData;
                if (c == null) log_($"getEquipmentModelIds: caller is not a player ({NameOf(Entity(l))}).");
                l.NewTable();
                void Field(string key, ushort v)
                {
                    l.Push(v);
                    l.SetField(-2, key);
                }
                Field("head", c?.Look.Head ?? 0);
                Field("body", c?.Look.Body ?? 0);
                Field("hands", c?.Look.Hands ?? 0);
                Field("main", c?.Look.Main ?? 0);
                Field("sub", c?.Look.Sub ?? 0);
                return 1;
            };
            m["getSize"] = l =>
            {
                if (Entity(l) is CharData c) return Ret(l, c.Look.Size);
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["getAverageItemLevel"] = l => Ret(l, Entity(l) is CharData c ? CharStats.ItemLevelDifference(c) + 99 : 0);

            // ---- party, points, vars ----
            m["getAllianceSize"] = l =>
            {
                switch (Entity(l))
                {
                    case NpcEntity:
                        log_("CLuaBaseEntity::getAllianceSize() - NPC passed to function.");
                        return Ret(l, 1);
                    case CharData { Party: { } party }:
                        return Ret(l, party.Alliance != null ? party.Alliance.PartyList.Sum(p => p.Members.Count) : party.Members.Count);
                    case MobEntity { LinkParty: { } linkParty }:
                        return Ret(l, linkParty.Count);
                    default:
                        return Ret(l, 1);
                }
            };
            // delSeals(points, type): the seals' and crests' char_points column, less the points
            m["delSeals"] = l =>
            {
                if (Pc(l, "delSeals") is not CharData c) return 0;
                string column = l.ToIntArg(3) switch
                {
                    0 => "beastman_seal",
                    1 => "kindred_seal",
                    2 => "kindred_crest",
                    3 => "high_kindred_crest",
                    4 => "sacred_kindred_crest",
                    _ => null,
                };
                if (column != null) AddPoints(c, column, -l.ToIntArg(2));
                return 0;
            };
            // getCharVarsWithPrefix(prefix): { name = value } from char_vars for the live ones, cached on the way
            m["getCharVarsWithPrefix"] = l =>
            {
                l.NewTable();
                if (Entity(l) is not CharData c) return 1;
                long now = UnixNow();
                var r = Exec(SqlVarsLike, BoundValue.Of(c.Id), BoundValue.Of(l.ToStringArg(2) + "%"));
                while (r != null && r.Next())
                {
                    string name = r.Get<string>("varname");
                    int value = r.Get<int>("value");
                    uint expiry = r.Get<uint>("expiry");
                    if (expiry != 0 && expiry <= now) continue;
                    c.CharVars[name] = (value, expiry);
                    l.Push(value);
                    l.SetField(-2, name);
                }
                return 1;
            };
            m["getMogHouseOwner"] = l =>
            {
                l.PushObject(Pc(l, "getMogHouseOwner") is CharData c ? MogHouseHost(c) : null);
                return 1;
            };
            // visitMogHouse(hostId, npc): false when refused
            m["visitMogHouse"] = l =>
            {
                if (Pc(l, "visitMogHouse") is not CharData c) return Ret(l, false);
                uint? npcId = l.ToObject(3) switch { NpcEntity n => n.Id, BattleEntity b => b.Id, _ => null };
                if (npcId == null)
                {
                    log_($"No NPC given ({c.Name}).");
                    return Ret(l, false);
                }
                return Ret(l, VisitMogHouse(c, l.ToUIntArg(2), npcId.Value));
            };
            // the chars visiting this char's mog house
            m["getMogHouseVisitors"] = l =>
            {
                var visitors = Entity(l) is CharData c ? MogHouseVisitors(c) : new List<CharData>();
                l.NewTable(visitors.Count, 0);
                for (int i = 0; i < visitors.Count; i++)
                {
                    l.PushObject(visitors[i]);
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            };

            // ---- battle entity getters ----
            m["isAlly"] = l => Ret(l, Entity(l) is MobEntity { Allegiance: CharData.AllegiancePlayer });
            m["getCrystalElement"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.Element);
                log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                return Ret(l, 0);
            };
            m["getBaseSpeed"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => mob.BaseSpeed,
                NpcEntity npc => npc.Speed,
                CharData c => c.BaseSpeedOverride ?? MountUtils.BaseSpeed, // setBaseSpeed's, else map.BASE_SPEED
                _ => CharBaseSpeed,
            });
            m["numBustEffects"] = l =>
            {
                if (Entity(l) is NpcEntity)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return Ret(l, 0);
                }
                return Ret(l, ((BattleEntity)Entity(l)).StatusEffects.GetEffectsCount(EffectBust));
            };
            m["getHighestRuneEffect"] = l => Ret(l, Battle(l) is BattleEntity b ? HighestRuneEffect(b) : 0);
            m["getAllRuneEffects"] = l =>
            {
                if (Battle(l) is not BattleEntity b)
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                int n = 0;
                foreach (var id in Runes(b))
                {
                    l.Push(id);
                    l.RawSetI(-2, ++n);
                }
                return 1;
            };
            m["getActiveRuneCount"] = l => Ret(l, Battle(l) is BattleEntity b ? Runes(b).Count() : 0);

            // weaknessTrigger(level): a MobSkillFinish on itself with the colour's trigger animation
            m["weaknessTrigger"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to trigger weakness for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                ushort animation = l.ToIntArg(2) switch
                {
                    0 => 1808, // BLUE: BlueTrigger
                    1 => 1807, // YELLOW: YellowTrigger
                    2 => 1806, // RED: RedTrigger
                    3 => 1946, // WHITE: WhiteTrigger
                    _ => 0,
                };
                var action = new ActionPacket { ActorId = mob.Id, ActionType = ActionCategoryMobSkillFinish };
                var r = action.AddTarget(mob.Id).AddResult();
                r.Animation = animation;
                r.Param = WeaknessTriggerParam;
                PushCharsNear(mob, Battle2Packets.Battle2(action));
                return 0;
            };

            // ---- messages, emotes ----
            // messagePublic(messageId, target, param0, param1): a BATTLE_MESSAGE to the entity and those in range
            m["messagePublic"] = l =>
            {
                var self = Entity(l);
                if (l.ToObject(3) is not { } target) return 0;
                var p = SelfPackets.BattleMessage(IdOf(self), TargIdOf(self), IdOf(target), TargIdOf(target),
                                                  (int)OptU32(l, 4), (int)OptU32(l, 5), (ushort)l.ToIntArg(2));
                PushInRangeSelf(self, p);
                return 0;
            };
            // messageCombat(speaker = self, param0, param1, message): a BATTLE_MESSAGE2 to the char alone
            m["messageCombat"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Function called on non-PC entity ({NameOf(Entity(l))})");
                    return 0;
                }
                var speaker = l.IsNil(2) ? c : l.ToObject(2) ?? c;
                PushTo(c, BattleMessage2Of(speaker, c, l.ToIntArg(3), l.ToIntArg(4), (ushort)l.ToEnumArg(5)));
                return 0;
            };
            // sendEmote(target, emote, mode, othersOnly): from an NPC to those in range, from a char to them and itself
            // unless othersOnly
            m["sendEmote"] = l =>
            {
                var target = l.ToObject(2);
                byte emote = (byte)l.ToIntArg(3), mode = (byte)l.ToIntArg(4);
                switch (Entity(l))
                {
                    case NpcEntity npc:
                        PushCharsNear(npc, NpcMotionMes(npc, target != null ? IdOf(target) : npc.Id, target != null ? TargIdOf(target) : npc.TargId,
                                                        emote, mode));
                        break;
                    case CharData c:
                        var p = MotionMes(c, target != null ? IdOf(target) : c.Id, target != null ? TargIdOf(target) : c.TargId, emote, mode, 0);
                        PushCharInRange(c, p, !l.ToBoolean(5));
                        break;
                    default:
                        log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                        break;
                }
                return 0;
            };
            // showPosition: "The compass reads" with the char's position and facing
            m["showPosition"] = l =>
            {
                if (Pc(l, "showPosition") is not CharData c) return 0;
                PushTo(c, MessageParams4((uint)(int)c.X, (uint)(int)c.Y, (uint)(int)c.Z, c.Rotation, MsgStdCompass));
                return 0;
            };
            // enableEntities({ id, ... }): the client may show these entities
            m["enableEntities"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"enableEntities called on invalid type ({NameOf(Entity(l))})");
                    return 0;
                }
                if (!l.IsTable(2)) return 0;
                var ids = new List<uint>();
                for (int i = 1, n = l.ObjLen(2); i <= n; i++)
                {
                    l.RawGetI(2, i);
                    bool number = l.IsNumber(-1);
                    if (number) ids.Add((uint)l.ToIntegral(-1));
                    l.Pop(1);
                    if (!number) return 0;
                }
                PushTo(c, EntityVis(ids));
                return 0;
            };

            // ---- NPCs, position ----
            // showNPC(seconds = 15): a hidden NPC shows, and disappears again after the time
            m["showNPC"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to show NPC with invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                long ms = l.IsNil(2) ? 15000 : l.ToUIntArg(2) * 1000L;
                npc.Status = NpcEntity.StatusNormal;
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateCombat);
                foreach (var (c, s) in charSessions_)
                    if (SameArea(c, npc)) SpawnNpcs(s, c);
                QueueNative(npc, ms, false, e =>
                {
                    var n = (NpcEntity)e;
                    n.Status = NpcEntity.StatusDisappear;
                    DespawnNpc(n);
                });
                return 0;
            };
            // teleport({ x, y, z }, rotation | faceEntity): GP_SERV_COMMAND_WPOS moves the entity, to it and those in
            // range; the rotation is 0 unless given
            m["teleport"] = l =>
            {
                var e = Entity(l);
                float x = 0, y = 0, z = 0;
                if (l.IsTable(2))
                {
                    x = (float)FieldNumber(l, 2, "x", 0);
                    y = (float)FieldNumber(l, 2, "y", 0);
                    z = (float)FieldNumber(l, 2, "z", 0);
                }
                byte rot = 0;
                if (l.IsNumber(3)) rot = (byte)l.ToIntArg(3);
                else if (l.ToObject(3) is { } face)
                {
                    var (ex, _, ez, erot) = PosOf(e);
                    var (fx, _, fz, _) = PosOf(face);
                    rot = WorldAngle(ex, ez, erot, fx, fz);
                }
                switch (e)
                {
                    case BattleEntity b: (b.X, b.Y, b.Z, b.Rotation) = (x, y, z, rot); break;
                    case NpcEntity n: (n.X, n.Y, n.Z, n.Rotation) = (x, y, z, rot); break;
                }
                PushInRangeSelf(e, WposOf(e));
                MarkUpdate(e, CharStats.UpdatePos);
                return 0;
            };

            // ---- the entity data store ----
            m["getData"] = l =>
            {
                if (!PushEntityData(l, Entity(l), true))
                {
                    log_($"CLuaBaseEntity::getData: no data store for {NameOf(Entity(l))}");
                    l.PushNil();
                }
                return 1;
            };
            m["resetData"] = l =>
            {
                ResetEntityData(Entity(l));
                return 0;
            };
        }

        // GetStatusEffectsInIDRange(Ignis, Tenebrae): the runes held, in container order
        private static IEnumerable<ushort> Runes(BattleEntity b)
        {
            foreach (var e in b.StatusEffects.All)
                if (e.Id >= EffectIgnis && e.Id <= EffectTenebrae && !e.Deleted) yield return e.Id;
        }

        // GetHighestRuneEffect: the rune held most often; ties go to the one met first (the C++ walks a hash map)
        private static ushort HighestRuneEffect(BattleEntity b)
        {
            var counts = new List<(ushort Id, int Count)>();
            foreach (var id in Runes(b))
            {
                int i = counts.FindIndex(t => t.Id == id);
                if (i < 0) counts.Add((id, 1));
                else counts[i] = (id, counts[i].Count + 1);
            }
            ushort highest = 0;
            int highestCount = 0;
            foreach (var (id, count) in counts)
                if (highest == 0 || count > highestCount) (highest, highestCount) = (id, count);
            return highest;
        }
    }
}
