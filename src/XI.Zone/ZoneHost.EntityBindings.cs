using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity methods, binding batch 3 (the next most called without one): openSendBox, injectActionPacket,
    // copyStatusEffect, entityVisualPacket (GP_SERV_COMMAND_MAPSCHEDULOR) and the SendEntityVisualPacket global, the
    // unique events (setUniqueEvent / delUniqueEvent / hasCompletedUniqueEvent, char_unlocks.unique_event), setMaxHP /
    // updateHealth, forMembersInRange, isMobType, delContainerItems, updateNPCHideTime, closeDoor, warp, getStorageItem,
    // setNation, setGMLevel / setVisibleGMLevel / getVisibleGMLevel, getEntity, getModelId, the costume (getCostume /
    // setCostume), canUseAbilities, getPlaytime, getSeals, sendEntityUpdateToPlayer, isPlayerInTriggerArea and
    // setDamage; then batch 4: getMeritCount / setMerits, updateEventString (GP_SERV_COMMAND_PENDINGSTR),
    // seenKeyItem, independentAnimation (GP_SERV_COMMAND_MAGICSCHEDULOR), addUsedItem, checkSoloPartyAlliance,
    // getMaxGearMod and setCampaignAllegiance; batch 5: drawIn and the DrawIn global (battleutils::DrawIn), customMenu
    // (luautils::SetCustomMenuContext / HandleCustomMenu, answered by a tell to "_CUSTOM_MENU"), resetAI,
    // set / getfTPModifierOverride, canSee (and inWater), checkImbuedItems and hasRaiseTractorMenu. Draw-in aborts when
    // geometry blocks the way to the draw-in point, then snaps onto the navmesh. The other teleport types (getTeleport, the outposts / portals / maws in add / hasTeleport) are
    // ZoneHost.Bindings.cs's. warp saves a pet that persists through zoning (setPetZoningInfo). Not ported:
    // getPlaytime(false)'s stale value (the C# play time is counted live, so both read the same).
    public sealed partial class ZoneHost
    {
        // charutils::SaveCharGMLevel: the account level, and gm mode on from a visible level of 3
        private void SaveCharGmLevel(CharData c)
        {
            Exec("UPDATE chars SET gmlevel = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.AccountGmLevel), BoundValue.Of(c.Id));
            Exec("UPDATE char_flags SET gmModeEnabled = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.GmLevel >= 3 ? 1 : 0), BoundValue.Of(c.Id));
        }

        // CBaseEntity::GetEntity(targid) in the entity's zone (or instance): a mob or an NPC below 0x400, a char below 0x700
        private object EntityInArea(object near, ushort targid)
        {
            if (targid == 0) return null;
            if (targid < 0x400)
            {
                var info = AreaOf(near);
                foreach (var m in info.Mobs)
                    if (m.TargId == targid) return m;
                foreach (var n in info.Npcs)
                    if (n.TargId == targid) return n;
                return null;
            }
            if (targid >= 0x700) return null;
            foreach (var (o, _) in charSessions_)
                if (SameArea(o, near) && o.TargId == targid) return o;
            return null;
        }

        // GP_SERV_COMMAND_MAPSCHEDULOR (0x039): UniqueNoCas, UniqueNoTar, the four-character id, ActIndexCast / Tar
        private static BasicPacket MapSchedulor(object entity, string command)
        {
            var w = new PacketWriter(0x039, 20);
            if (entity != null)
            {
                uint id = IdOf(entity);
                ushort targid = TargIdOf(entity);
                w.U32(4, id);
                w.U32(8, id);
                w.U16(16, targid);
                w.U16(18, targid);
            }
            var bytes = System.Text.Encoding.ASCII.GetBytes(command ?? "");
            for (int i = 0; i < 4 && i < bytes.Length; i++) w.U8(12 + i, bytes[i]);
            return w.Packet;
        }

        // GP_SERV_COMMAND_PENDINGSTR (0x05D): int32 num[9], then four char[16] (15 characters each)
        private static BasicPacket PendingStr(string[] strings, uint[] nums)
        {
            var w = new PacketWriter(0x05D, 4 + S2cPendingstr.SizeOf);
            for (int i = 0; i < S2cPendingstr.NumCount; i++) w.U32(4 + S2cPendingstr.NumOffset(i), nums[i]);
            for (int i = 0; i < S2cPendingstr.StringCount; i++)
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(strings[i]);
                for (int j = 0; j < bytes.Length && j < S2cPendingstr.StringLen - 1; j++) w.U8(4 + S2cPendingstr.StringOffset(i) + j, bytes[j]);
            }
            return w.Packet;
        }

        // GP_SERV_COMMAND_MAGICSCHEDULOR (0x03A): UniqueNoCas / Tar, ActIndexCast / Tar, fileNum, type
        private static BasicPacket MagicSchedulor(object entity, object target, ushort animId, byte type)
        {
            var w = new PacketWriter(0x03A, 20);
            w.U32(4, IdOf(entity));
            w.U32(8, IdOf(target));
            w.U16(12, TargIdOf(entity));
            w.U16(14, TargIdOf(target));
            w.U16(16, animId);
            w.U8(18, type);
            return w.Packet;
        }

        // CBattleEntity::getMaxGearMod: a char's highest value of the mod over its gear up to the back (the gear
        // above its level skipped; the values compared as uint16, as the C++ does), anything else's getMod
        internal static short MaxGearMod(BattleEntity b, Mod mod)
        {
            if (mod == Mod.NONE) return 0;
            if (b is not CharData c) return b.GetMod(mod);
            ushort max = 0;
            for (int slot = 0; slot < 15; slot++)
            {
                if (c.Equipped[slot] is not EquipmentInfo item || item.ReqLvl > c.MLvl) continue;
                ushort value = (ushort)item.GetModifier(mod);
                if (value > max) max = value;
            }
            return (short)max;
        }

        private const ushort MsgDrawnIn = 232;
        private const byte StatusCutsceneOnly = 6;

        // luautils' customMenuContext: each char's open menu table
        private readonly Dictionary<uint, LuaRef> customMenus_ = new Dictionary<uint, LuaRef>();

        // battleutils::DrawIn: the target set down offset from the point (degrees off its facing) a yalm higher, and
        // the WPOS and "is drawn in!" to it and the chars around; not a dead or mounted one, nor one only in cutscenes
        private void DrawIn(BattleEntity target, float x, float y, float z, byte rotation, float offset, float degrees)
        {
            float radians = ZoneLine.RotationToRadian(rotation) + degrees * MathF.PI / 180.0f;
            float nx = x + MathF.Cos(2 * MathF.PI - radians) * offset, nz = z + MathF.Sin(2 * MathF.PI - radians) * offset;
            float ny = y;
            // geometry between the source's eyeline and the draw-in point aborts it
            if (XiMeshRayIntersect(ZoneOf(target), new System.Numerics.Vector3(x, y - EntityHeight, z), new System.Numerics.Vector3(nx, ny - EntityHeight, nz))) return;
            // snapped onto the navmesh, then a little higher just in case
            NavMeshOf(target.Zone)?.SnapToValidPosition(ref nx, ref ny, ref nz);
            ny -= 1.0f;
            if (target is MobEntity { Status: StatusCutsceneOnly }) return;
            bool dead = target is CharData dc ? dc.Hp <= 0 : target is MobEntity dm && (dm.IsDead || !dm.IsAlive);
            if (dead || target.StatusEffects.HasStatusEffect(EffectId.Mounted)) return;
            // GP_SERV_COMMAND_WPOS(NORMAL) moves the entity
            (target.X, target.Y, target.Z, target.Rotation) = (nx, ny, nz, rotation);
            var wpos = Wpos(target);
            var message = SelfPackets.BattleMessage(target.Id, target.TargId, target.Id, target.TargId, 0, 0, MsgDrawnIn);
            if (target is CharData c)
            {
                PushCharInRange(c, wpos, true);
                PushCharInRange(c, message, true);
            }
            else
            {
                PushCharsNear(target, wpos);
                PushCharsNear(target, message);
            }
        }

        // the menu's function at the key, called with the char (and the extra argument) when the table has one
        private void CallMenuFunction(LuaState l, CharData c, string key, params object[] extra)
        {
            l.GetField(-1, key);
            if (!l.IsFunction(-1))
            {
                l.Pop(1);
                return;
            }
            l.PushObject(c);
            foreach (var a in extra) l.Push(a);
            l.Call(1 + extra.Length, 0);
        }

        private static bool Contains(byte[] haystack, byte[] needle) => IndexOf(haystack, needle) >= 0;

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
                if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
            return -1;
        }

        private static byte[] Raw(string latin1) => System.Text.Encoding.Latin1.GetBytes(latin1);

        // "...: Result (Canceled.)" and the JP form: the player closed the prompt
        private static readonly byte[][] CustomMenuCancel =
        {
            Raw("\x3A\x8C\x8B\x89\xCA\x28\x83\x4C\x83\x83\x83\x93\x83\x5A\x83\x8B\x29"),
            Raw(": Result (Canceled.)"),
        };
        // the prompt dropped for an event, or with four GM tells already waiting
        private static readonly byte[][] CustomMenuEventCancel =
        {
            Raw("\x8C\xBB\x8D\xDD\x83\x43\x83\x78\x83\x93\x83\x67\x92\x86\x82\xC5\x82\xB7\x81\x42\x82\xB1\x82\xCC\x67\x6D\x74\x65\x6C\x6C\x82\xCD\x96\xB3\x8C\xF8\x82\xC5\x82\xB7\x81\x42"),
            Raw("\x8C\xBB\x8D\xDD\x34\x82\xC2\x82\xCC\x67\x6D\x74\x65\x6C\x6C\x82\xF0\x8E\xF3\x82\xAF\x95\x74\x82\xAF\x82\xC4\x82\xA2\x82\xDC\x82\xB7\x81\x42\x82\xB1\x82\xCC\x67\x6D\x74\x65\x6C\x6C\x82\xCD\x96\xB3\x8C\xF8\x82\xC5\x82\xB7\x81\x42"),
            Raw("\x3A\x8C\x8B\x89\xCA\x28\x83\x43\x83\x78\x83\x93\x83\x67\x82\xAA\x8B\x4E\x93\xAE\x82\xB5\x82\xBD\x82\xBD\x82\xDF\x83\x4C\x83\x83\x83\x93\x83\x5A\x83\x8B\x29"),
            Raw("Currently in an event. This GMTELL was invalidated."),
            Raw("Currently four GMTELL's have been received. This GMTELL was invalidated."),
            Raw(": Result (Canceled due to event activation.)"),
        };
        private static readonly byte[] CustomMenuResultJp = Raw("\x3A\x8C\x8B\x89\xCA\x28"), CustomMenuResultNa = Raw(": Result (");

        // luautils::HandleCustomMenu: onCancelled, or the option whose name the answer carries; then onEnd, and the
        // context is gone
        private void HandleCustomMenu(CharData c, byte[] selection)
        {
            if (selection.Length == 0)
            {
                log_($"Custom menu for {c.Name} was provided an empty string.");
                return;
            }
            var menu = customMenus_[c.Id];
            customMenus_.Remove(c.Id);
            bool cancelled = Array.Exists(CustomMenuCancel, m => Contains(selection, m));
            bool cancelledEvent = Array.Exists(CustomMenuEventCancel, m => Contains(selection, m));
            var l = lua_;
            int top = l.Top;
            try
            {
                l.PushRef(menu);
                if (cancelled || cancelledEvent) CallMenuFunction(l, c, "onCancelled", cancelledEvent);
                else
                {
                    byte[] result = Array.Empty<byte>();
                    int at = IndexOf(selection, CustomMenuResultJp);
                    if (at >= 0) result = selection[(at + CustomMenuResultJp.Length)..];
                    if (result.Length == 0 && (at = IndexOf(selection, CustomMenuResultNa)) >= 0) result = selection[(at + CustomMenuResultNa.Length)..];
                    // the closing parenthesis
                    if (result.Length != 0) result = result[..^1];
                    l.GetField(-1, "options");
                    int options = l.Top;
                    if (l.IsTable(options))
                    {
                        l.PushNil();
                        while (l.Next(options))
                        {
                            if (l.IsTable(-1))
                            {
                                l.RawGetI(-1, 1);
                                var name = l.ToBytes(-1) ?? Array.Empty<byte>();
                                l.Pop(1);
                                if (name.AsSpan().SequenceEqual(result))
                                {
                                    l.RawGetI(-1, 2);
                                    l.PushObject(c);
                                    try
                                    {
                                        l.Call(1, 0);
                                    }
                                    catch (LuaException e)
                                    {
                                        log_($"Menu error: {e.Message}");
                                    }
                                    l.SetTop(options);
                                    break;
                                }
                            }
                            l.Pop(1);
                        }
                    }
                    l.SetTop(options - 1);
                }
                CallMenuFunction(l, c, "onEnd");
            }
            catch (LuaException e)
            {
                ScriptError("customMenu", e);
            }
            finally
            {
                l.SetTop(top);
                l.Unref(menu);
            }
        }

        private void RegisterEntityBindings(Dictionary<string, LuaFunction> m)
        {
            // luautils::SendEntityVisualPacket(npcId, command): the NPC's GP_SERV_COMMAND_MAPSCHEDULOR to the chars in
            // range of it (CZone::PushPacket CHAR_INRANGE); an id that is no NPC does nothing
            lua_.SetGlobal("SendEntityVisualPacket", l =>
            {
                uint id = l.ToUIntArg(1);
                if (HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var npc) == true)
                    PushCharsNear(npc, MapSchedulor(npc, l.ToStringValue(2)));
                return 0;
            });
            m["openSendBox"] = l =>
            {
                if (Entity(l) is CharData c) OpenDeliveryBox(c, UContainerType.SendDeliveryBox);
                return 0;
            };
            // injectActionPacket(targetId, category, animation, info, reaction, message, actionParam, param): one result
            // to the chars in range and the entity itself
            m["injectActionPacket"] = l =>
            {
                var self = Entity(l);
                var action = new ActionPacket { ActorId = IdOf(self), ActionType = (byte)l.ToIntArg(3), ActionId = (ushort)l.ToIntArg(8) };
                var r = action.AddTarget(l.ToUIntArg(2)).AddResult();
                r.Animation = (ushort)l.ToIntArg(4);
                r.Info = (byte)l.ToIntArg(5);
                r.Resolution = (byte)l.ToIntArg(6);
                r.MessageId = (ushort)l.ToIntArg(7);
                r.Param = (ushort)l.ToIntArg(9);
                var packet = Battle2Packets.Battle2(action);
                if (self is CharData c) PushCharInRange(c, packet, true);
                else PushCharsNear(self, packet);
                return 0;
            };
            // copyStatusEffect(effect): the effect again on this entity, for what is left of its duration
            m["copyStatusEffect"] = l =>
            {
                if (Entity(l) is not BattleEntity b || l.ToObject(2) is not StatusEffect o) return Ret(l, false);
                long remainingMs = 0;
                if (o.DurationMs > 0) remainingMs = Math.Max((o.StartMs - NowMs() + o.DurationMs) / 1000 * 1000, 0);
                var e = new StatusEffect(o.Id, o.Icon, o.Power, o.TickMs, remainingMs, o.SubId, o.SubPower, o.SubIcon, o.Tier, o.Flags,
                                         o.SourceType, o.SourceTypeParam, o.OriginId);
                return Ret(l, b.StatusEffects.AddStatusEffect(e, EffectNotice.ShowMessage));
            };
            m["entityVisualPacket"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                PushTo(c, MapSchedulor(l.IsNil(3) ? null : l.ToObject(3), l.ToStringValue(2)));
                return 0;
            };

            // the unique events: uint32 m_uniqueEvents[5], laid out as its bytes
            m["setUniqueEvent"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to set Unique Event mask for Non-PC.");
                    return 0;
                }
                AddBit(c.UniqueEvents, l.ToIntArg(2));
                Exec("UPDATE char_unlocks SET unique_event = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.UniqueEvents), BoundValue.Of(c.Id));
                return 0;
            };
            m["delUniqueEvent"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to del from Unique Event mask for Non-PC.");
                    return 0;
                }
                DelBit(c.UniqueEvents, l.ToIntArg(2));
                Exec("UPDATE char_unlocks SET unique_event = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.UniqueEvents), BoundValue.Of(c.Id));
                return 0;
            };
            m["hasCompletedUniqueEvent"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Attempt to compare Unique Event mask for Non-PC.");
                    return Ret(l, false);
                }
                return Ret(l, HasBit(c.UniqueEvents, (ushort)l.ToIntArg(2)));
            };

            m["setMaxHP"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return 0;
                }
                b.BaseMaxHp = Math.Max(1, l.ToIntArg(2));
                CharStats.UpdateHealth(b);
                return 0;
            };
            m["updateHealth"] = l =>
            {
                if (Entity(l) is BattleEntity b) CharStats.UpdateHealth(b);
                else log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            // forMembersInRange(range, fn): fn for each party member in the zone within the range
            m["forMembersInRange"] = l =>
            {
                if (Entity(l) is not BattleEntity target || !l.IsFunction(3)) return 0;
                float range = (float)l.ToNumber(2);
                foreach (var member in PartyOf(target))
                {
                    if (!SameArea(member, target) || Distance(target, member) > range) continue;
                    l.PushValue(3);
                    l.PushObject(member);
                    l.Call(1, 0);
                }
                return 0;
            };
            // isMobType(type): NORMAL only when no bit is set, else any of the type's bits
            m["isMobType"] = l =>
            {
                if (Entity(l) is not MobEntity mob) return Ret(l, false);
                ushort type = (ushort)l.ToEnumArg(2); // xi::MobType
                return Ret(l, type == MobType.Normal ? mob.Type == MobType.Normal : (mob.Type & type) != 0);
            };
            // delContainerItems(container = inventory): what is equipped from it comes off, then every slot empties
            m["delContainerItems"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int location = l.IsNumber(2) ? (byte)l.ToIntArg(2) : 0;
                if (location >= CharData.MaxContainers)
                {
                    log_("Attempting to delete items from an invalid container.");
                    return Ret(l, false);
                }
                var container = (Container)location;
                for (byte slot = 0; slot <= 15; slot++)
                    if (CharStats.EquippedItem(c, slot) is { } equipped && equipped.Location == container)
                        loader_.Stats?.EquipItem(c, 0, slot, Container.Inventory);
                byte size = c.StorageSize[location];
                for (byte i = 1; i <= size; i++)
                {
                    if (c.FindItem(container, i) is not { } item) continue;
                    // one transaction per slot, so a slot that refuses to empty does not strand the rest
                    using var t = new ItemTransaction(this, c);
                    if (t.ClaimSlot(container, i) == null || !t.Take(container, i, item.Quantity) || !t.Commit())
                        log_($"CLuaBaseEntity::delContainerItems: {c.Name} kept item in slot {i}");
                }
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                return Ret(l, true);
            };
            // updateNPCHideTime(seconds = 15): a hidden NPC comes back after the time
            m["updateNPCHideTime"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to update NPC hide time with invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (npc.Status != NpcEntity.StatusDisappear) return 0;
                long ms = l.IsNil(2) ? 15000 : l.ToUIntArg(2) * 1000L;
                QueueNative(npc, ms, false, e =>
                {
                    var n = (NpcEntity)e;
                    n.Status = NpcEntity.StatusNormal;
                    foreach (var (c, s) in charSessions_)
                        if (SameArea(c, n)) SpawnNpcs(s, c);
                });
                return 0;
            };
            // closeDoor(seconds = 7): an open door shuts, then opens again after the time
            m["closeDoor"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_("CLuaBaseEntity::closeDoor() - Non-NPC passed to function.");
                    return 0;
                }
                if (npc.Animation != AnimationOpenDoor) return 0;
                long ms = l.IsNil(2) ? 7000 : l.ToUIntArg(2) * 1000L;
                npc.Animation = AnimationCloseDoor;
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateCombat);
                QueueNative(npc, ms, false, e =>
                {
                    var n = (NpcEntity)e;
                    n.Animation = AnimationOpenDoor;
                    BroadcastNpcUpdate(n, EntityUpdatePacket.UpdateCombat);
                });
                return 0;
            };
            // warp: the zone tick sends the char to its home point
            m["warp"] = l =>
            {
                if (Entity(l) is CharData c)
                {
                    c.RequestedWarp = WarpRequest.Warp;
                    SavePetForZoning(c);
                }
                else log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return 0;
            };
            // getStorageItem(container, slot, equipSlot): equipSlot 255 for the container's slot, else what is
            // equipped there (a linkshell for the linkshell slots)
            m["getStorageItem"] = l =>
            {
                Item item = null;
                if (Entity(l) is CharData c)
                {
                    int container = l.ToIntArg(2), slot = l.ToIntArg(3), equip = l.ToIntArg(4);
                    if (equip == 255) item = container < CharData.MaxContainers ? c.FindItem((Container)container, (byte)slot) : null;
                    else if (equip >= LinkshellItem.SlotLink1) item = LinkshellItem.Equipped(c, equip);
                    else item = CharStats.EquippedItem(c, equip);
                }
                else log_($"{NameOf(Entity(l))} is trying to access their inventory, but is not TYPE_PC, so doesn't have one!");
                l.PushObject(item, ItemType);
                return 1;
            };
            m["setNation"] = l =>
            {
                if (Pc(l, "setNation") is not CharData c) return 0;
                c.Nation = (byte)l.ToIntArg(2);
                Exec("UPDATE chars SET nation = ? WHERE charid = ?", BoundValue.Of(c.Nation), BoundValue.Of(c.Id));
                return 0;
            };
            m["setGMLevel"] = l =>
            {
                if (Pc(l, "setGMLevel") is not CharData c) return 0;
                c.AccountGmLevel = (byte)l.ToIntArg(2);
                SaveCharGmLevel(c);
                return 0;
            };
            // setVisibleGMLevel: the level the 0x037 shows, through the UPDATE_HP
            m["setVisibleGMLevel"] = l =>
            {
                if (Pc(l, "setVisibleGMLevel") is not CharData c) return 0;
                c.GmLevel = (byte)l.ToIntArg(2);
                c.UpdateMask |= EntityUpdatePacket.UpdateHp;
                SaveCharGmLevel(c);
                return 0;
            };
            m["getVisibleGMLevel"] = l => Ret(l, Pc(l, "getVisibleGMLevel") is CharData c ? c.GmLevel : 0);
            m["getEntity"] = l =>
            {
                l.PushObject(EntityInArea(Entity(l), (ushort)l.ToIntArg(2)));
                return 1;
            };
            // getModelId: look.modelid (a char's face and race)
            m["getModelId"] = l => Ret(l, Entity(l) switch
            {
                CharData c => c.Look.ModelId,
                MobEntity mob => mob.Look[1],
                NpcEntity npc => npc.Look[1],
                _ => 0,
            });
            m["getCostume"] = l => Ret(l, Pc(l, "getCostume") is CharData c ? c.Costume : 0);
            // setCostume: the look update for the others and the 0x037 for the char, unless shut down or gone
            m["setCostume"] = l =>
            {
                if (Pc(l, "setCostume") is not CharData c) return 0;
                ushort costume = (ushort)l.ToIntArg(2);
                if (c.Costume == costume || SessionOf(c) is not { } s) return 0;
                c.Costume = costume;
                c.UpdateMask |= CharStats.UpdateLook;
                Push(s, CharStatus(c));
                return 0;
            };
            // canUseAbilities: none of the effects that stop one, and a state that may change
            m["canUseAbilities"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_("canUseAbilities() : Wrong Entity Type");
                    return Ret(l, false);
                }
                var fx = b.StatusEffects;
                bool held = fx.HasStatusEffect(EffectId.SleepI) || fx.HasStatusEffect(EffectImpairment) || fx.HasStatusEffect(EffectId.SleepII) ||
                            fx.HasStatusEffect(EffectId.Stun) || fx.HasStatusEffect(EffectAmnesia) || fx.HasStatusEffect(EffectId.Lullaby) ||
                            fx.HasStatusEffect(EffectId.Petrification) || fx.HasStatusEffect(EffectId.Terror);
                return Ret(l, !held && CanChangeState(b));
            };
            m["getPlaytime"] = l => Ret(l, Pc(l, "getPlaytime") is CharData c ? PlayTime(c) : 0);
            // getSeals(type): beastman seals, kindred seals, kindred / high kindred / sacred kindred crests
            m["getSeals"] = l =>
            {
                if (Pc(l, "getSeals") is not CharData c) return Ret(l, 0);
                string column = l.ToIntArg(2) switch
                {
                    0 => "beastman_seal",
                    1 => "kindred_seal",
                    2 => "kindred_crest",
                    3 => "high_kindred_crest",
                    4 => "sacred_kindred_crest",
                    _ => null,
                };
                return Ret(l, column != null ? GetPoints(c, column) : 0);
            };
            // sendEntityUpdateToPlayer(entity, type, mask): the char's updateEntityPacket for one entity
            m["sendEntityUpdateToPlayer"] = l =>
            {
                if (Entity(l) is not CharData c || SessionOf(c) is not { } s) return 0;
                var type = (EntityUpdate)l.ToIntArg(3);
                byte mask = (byte)l.ToIntArg(4);
                switch (l.ToObject(2))
                {
                    case MobEntity mob: UpdateEntityPacket(s, mob, type, mask); break;
                    case NpcEntity npc: UpdateEntityPacket(s, npc, type, mask); break;
                    case CharData other: UpdateCharPacket(s, other, type, mask); break;
                }
                return 0;
            };
            m["isPlayerInTriggerArea"] = l =>
            {
                if (Pc(l, "isPlayerInTriggerArea") is not CharData c) return Ret(l, false);
                return Ret(l, ZoneLuaChar(c).TriggerAreaIds.Contains(l.ToUIntArg(2)));
            };
            // setDamage(damage, slot): a mob's weapon damage in the main, sub or ranged slot
            m["setDamage"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"function call on invalid entity! (name: {NameOf(Entity(l))})");
                    return 0;
                }
                ushort damage = (ushort)l.ToIntArg(2);
                switch (l.ToIntArg(3))
                {
                    case 0: mob.MainDamage = damage; break;
                    case 1: mob.SubDamage = damage; break;
                    case 2: mob.RangedDamage = damage; break;
                    default: log_($"attempted to setDamage for an invalid slot! (name: {mob.Name})"); break;
                }
                return 0;
            };

            m["getMeritCount"] = l => Ret(l, Entity(l) is CharData c ? c.MeritPoints : 0);
            // setMerits(points): SetMeritPoints (the points as a uint8, at most the bank), the merit and monstrosity
            // MISCDATA, SaveCharExp
            m["setMerits"] = l =>
            {
                if (Pc(l, "setMerits") is not CharData c) return 0;
                c.MeritPoints = (byte)Math.Min((byte)l.ToIntArg(2), (byte)loader_.MaxMeritPoints(c));
                if (loader_.Stats != null) PushTo(c, SelfPackets.Merits(c, loader_.Stats));
                PushTo(c, MonPage1(c));
                PushTo(c, GameOkPackets.Monstrosity2(c));
                SaveCharExp(c, c.MJob);
                return 0;
            };
            // updateEventString(string0..3, param0..8): the strings and numbers an event's text reads
            m["updateEventString"] = l =>
            {
                if (Pc(l, "updateEventString") is not CharData c) return 0;
                var strings = new string[4];
                for (int i = 0; i < 4; i++) strings[i] = l.Type(2 + i) == LuaNative.LUA_TSTRING ? l.ToStringValue(2 + i) : "";
                var nums = new uint[9];
                for (int i = 0; i < 9; i++) nums[i] = l.IsNumber(6 + i) ? l.ToUIntArg(6 + i) : 0;
                PushTo(c, PendingStr(strings, nums));
                return 0;
            };
            // seenKeyItem(id): the key item's seen bit
            m["seenKeyItem"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"CLuaBaseEntity::seenKeyItem() - Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                int id = (ushort)l.ToEnumArg(2), table = id / 512, index = id % 512; // xi::KeyItem
                if (table >= 8)
                {
                    log_($"charutils::seenKeyItem() - Index {table} exceeds key items table capacity.");
                    return Ret(l, false);
                }
                return Ret(l, (c.KeyItems[table * 128 + 64 + index / 8] & 1 << index % 8) != 0);
            };
            // independentAnimation(target, animId, mode): to the chars in range and the entity itself
            m["independentAnimation"] = l =>
            {
                var self = Entity(l);
                if (l.ToObject(2) is not { } target) return 0;
                var packet = MagicSchedulor(self, target, (ushort)l.ToIntArg(3), (byte)l.ToIntArg(4));
                if (self is CharData c) PushCharInRange(c, packet, true);
                else PushCharsNear(self, packet);
                return 0;
            };
            // addUsedItem(itemId): a charged item into the inventory with its last use now (its reuse timer running)
            m["addUsedItem"] = l =>
            {
                if (Pc(l, "addUsedItem") is not CharData c) return Ret(l, false);
                ushort id = (ushort)l.ToIntArg(2);
                if (FreeSlots(c, Container.Inventory) == 0) return Ret(l, false);
                if (ItemBasic(id) == null)
                {
                    log_($"AddItem: Item <{id}> is not found in a database");
                    return Ret(l, false);
                }
                if (loader_.Stats?.Items.Equipment(id) is not { Charged: true })
                {
                    log_($"addUsedItem: tried to setLastUseTime but itemID <{id}> is not type ITEM_CHARGED");
                    return Ret(l, false);
                }
                var item = new Item { ItemId = id, Quantity = 1 };
                if (Usable(id) is { MaxCharges: > 0 } usable) item.Extra[1] = usable.MaxCharges;
                BitConverter.GetBytes(LoginPackets.VanadielTimestamp(UnixNow())).CopyTo(item.Extra, 4);
                using var t = new ItemTransaction(this, c);
                return Ret(l, t.GiveItem(Container.Inventory, item) != null && t.Commit());
            };
            // checkSoloPartyAlliance: 0 alone, 1 in a party, 2 in an alliance
            m["checkSoloPartyAlliance"] = l =>
            {
                if (Pc(l, "checkSoloPartyAlliance") is not CharData c) return Ret(l, 0);
                return Ret(l, c.Party == null ? 0 : c.Party.Alliance != null ? 2 : 1);
            };
            m["getMaxGearMod"] = l => Ret(l, Entity(l) is BattleEntity b ? MaxGearMod(b, (Mod)l.ToEnumArg(2)) : 0);
            m["setCampaignAllegiance"] = l =>
            {
                if (Pc(l, "setCampaignAllegiance") is not CharData c) return 0;
                c.CampaignAllegiance = (byte)l.ToIntArg(2);
                Exec("UPDATE chars SET campaign_allegiance = ? WHERE charid = ?", BoundValue.Of(c.CampaignAllegiance), BoundValue.Of(c.Id));
                return 0;
            };

            // drawIn(): the mob's battle target to the mob; drawIn(target, offset, degrees, { x, y, z, rot }): that
            // target (in the zone) to the mob, or to the point given
            m["drawIn"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to draw-in from non-mob entity: {NameOf(Entity(l))}");
                    return 0;
                }
                if (l.Top < 2)
                {
                    if (mob.BattleTarget is { } battleTarget) DrawIn(battleTarget, mob.X, mob.Y, mob.Z, mob.Rotation, 0, 0);
                    return 0;
                }
                if (l.ToObject(2) is not { } targetObj)
                {
                    log_("Attempt to draw-in non-valid target.");
                    return 0;
                }
                if (targetObj is not BattleEntity target || !SameArea(target, mob)) return 0;
                float offset = l.IsNumber(3) ? (float)l.ToNumber(3) : 0, degrees = l.IsNumber(4) ? (float)l.ToNumber(4) : 0;
                float x = mob.X, y = mob.Y, z = mob.Z;
                byte rot = mob.Rotation;
                if (l.IsTable(5))
                {
                    x = (float)FieldNumber(l, 5, "x", x);
                    y = (float)FieldNumber(l, 5, "y", y);
                    z = (float)FieldNumber(l, 5, "z", z);
                    rot = (byte)LuaState.LlRound(FieldNumber(l, 5, "rot", rot)); // get_or with the uint8 rotation
                }
                DrawIn(target, x, y, z, rot, offset, degrees);
                return 0;
            };
            // customMenu({ title, options = { { name, fn }... }, onStart, onCancelled, onEnd }): onStart, then the
            // GM prompt from "_CUSTOM_MENU" listing the title and the options' names, each in quotes
            m["customMenu"] = l =>
            {
                if (Entity(l) is not CharData c || !l.IsTable(2)) return 0;
                if (customMenus_.TryGetValue(c.Id, out var old)) l.Unref(old);
                l.PushValue(2);
                customMenus_[c.Id] = l.Ref();
                l.PushValue(2);
                CallMenuFunction(l, c, "onStart");
                l.Pop(1);
                var text = new List<byte>();
                void Quoted(byte[] b)
                {
                    text.Add((byte)'"');
                    text.AddRange(b);
                    text.Add((byte)'"');
                }
                l.GetField(2, "title");
                Quoted(l.ToBytes(-1) ?? Array.Empty<byte>());
                l.Pop(1);
                l.GetField(2, "options");
                int options = l.Top;
                if (l.IsTable(options))
                {
                    l.PushNil();
                    while (l.Next(options))
                    {
                        if (l.IsTable(-1))
                        {
                            l.RawGetI(-1, 1);
                            Quoted(l.ToBytes(-1) ?? Array.Empty<byte>());
                            l.Pop(1);
                        }
                        l.Pop(1);
                    }
                }
                l.Pop(1);
                const byte messageGmPrompt = 12;
                PushTo(c, ChatStd(c, messageGmPrompt, text.ToArray(), "_CUSTOM_MENU"));
                return 0;
            };
            // resetAI: PAI->Reset: the path, the controller, every state retired without its cleanup, the action
            // and timer queues; a swinging entity stops
            m["resetAI"] = l =>
            {
                long now = NowMs();
                switch (Entity(l))
                {
                    case MobEntity mob:
                        mob.PathFind?.Clear();
                        MobControllerReset(mob, now);
                        mob.BattleTarget = null;
                        mob.SkillState = null;
                        mob.MagicState = null;
                        mob.RangedState = null;
                        mob.InactiveState = null;
                        // the death state, and the rewards queued a second after it
                        mob.DeathAtMs = mob.DefeatMsgAtMs = 0;
                        mob.DespawnAtMs = 0;
                        ClearActionQueues(mob);
                        if (mob.Animation == Anim.Attack || mob.Engaged)
                        {
                            mob.Engaged = false;
                            if (mob.Animation == Anim.Attack) mob.Animation = Anim.None;
                            mob.BattleTarget = null;
                            mob.UpdateMask |= EntityUpdatePacket.UpdateHp;
                        }
                        break;
                    case NpcEntity npc:
                        npc.PathFind?.Clear();
                        npc.Inactive = null;
                        npc.TriggerHoldTicks = 0;
                        ClearActionQueues(npc);
                        break;
                    // a char's controller resets nothing; its states go without their cleanup (no claim handed on,
                    // no recast, no synth result). The death state stays: the char's tick puts a char at 0 HP back in it.
                    case CharData c:
                        c.AttackTarget = null;
                        c.MagicState = null;
                        c.InactiveState = null;
                        c.WsState = null;
                        c.SkillState = null;
                        c.RangedState = null;
                        c.AbilityState = null;
                        c.ItemUse = null;
                        c.Synth = null;
                        ClearActionQueues(c);
                        if (c.Animation == Anim.Attack)
                        {
                            c.Animation = Anim.None;
                            c.BattleTarget = null;
                            c.UpdateMask |= CharStats.UpdateHp;
                        }
                        break;
                }
                return 0;
            };
            m["setfTPModifierOverride"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"function call on invalid entity! (name: {NameOf(Entity(l))})");
                    return 0;
                }
                mob.FtpOverrides[(ushort)l.ToIntArg(2)] = new[] { (float)l.ToNumber(3), (float)l.ToNumber(4), (float)l.ToNumber(5) };
                return 0;
            };
            // getfTPModifierOverride(skillId): { ftp1, ftp2, ftp3 }, or nil when none is set
            m["getfTPModifierOverride"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"function call on invalid entity! (name: {NameOf(Entity(l))})");
                    l.PushNil();
                    return 1;
                }
                if (!mob.FtpOverrides.TryGetValue((ushort)l.ToIntArg(2), out var ftp))
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable(3);
                for (int i = 0; i < 3; i++)
                {
                    l.Push((double)ftp[i]);
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            };
            // canSee(target [, ignoreInvisibleBoundaries = true]): no geometry between the eyes, 2 yalms up each
            m["canSee"] = l =>
            {
                if (l.ToObject(2) == null)
                {
                    log_($"Attempting to see invalid entity (from {NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                var self = Entity(l);
                var (tx, ty, tz, _) = PosOf(l.ToObject(2));
                var (sx, sy, sz, _) = PosOf(self);
                bool flag = l.IsNil(3) || l.ToBoolean(3);
                var src = new System.Numerics.Vector3(sx, sy - EntityHeight, sz);
                var dst = new System.Numerics.Vector3(tx, ty - EntityHeight, tz);
                return Ret(l, !XiMeshRayIntersect(ZoneOf(self), src, dst, flag));
            };
            // inWater(): shallow or deep water under the entity (PathFind::InWater's logic)
            m["inWater"] = l =>
            {
                var self = Entity(l);
                var (x, y, z, _) = PosOf(self);
                var terrain = XiMeshOf(ZoneOf(self))?.GetTerrainAt(x, y, z) ?? TerrainType.None;
                return Ret(l, terrain is TerrainType.ShallowWater or TerrainType.DeepWater);
            };
            // checkImbuedItems: any of the imbued items (5365..5384) in any container
            m["checkImbuedItems"] = l =>
            {
                if (Pc(l, "checkImbuedItems") is not CharData c) return Ret(l, false);
                return Ret(l, c.Items.Exists(i => i.ItemId >= 5365 && i.ItemId <= 5384));
            };
            // hasRaiseTractorMenu: a raise or tractor offer open; true for a non-char
            m["hasRaiseTractorMenu"] = l => Ret(l, Entity(l) is not CharData c || c.HasRaise != 0 || c.HasTractor != 0);
        }
    }
}
