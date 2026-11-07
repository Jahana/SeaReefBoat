using System;
using System.Collections.Generic;
using System.Linq;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity methods the scripts call most that had no binding: the spell list (canLearnSpell / addSpell /
    // delSpell, what every spell scroll calls), openDoor, setCurrency / delCurrency, hasEquipped, getCursorTarget,
    // getGender, hasVisitedZone, delItem, getPartySize, getContainerSize / changeContainerSize, canUseMisc and
    // changeMusic; then the learned abilities (addLearnedAbility / canLearnAbility / delLearnedAbility: the corsair
    // rolls), unequipItem, canEquipItem, engage and getParty. Not ported: getCursorTarget's pets and trusts, engage for
    // a pet or trust (none here).
    public sealed partial class ZoneHost
    {
        private const ushort MsgLearnsNewSpell = 23, MsgSpellAlreadyKnown = 96, MsgSpellCannotLearn = 95,
                             MsgLearnsNewAbility = 442, MsgAbilityCannotLearn = 443, MsgAbilityAlreadyKnown = 444;

        // the bitset helpers hasBit / addBit / delBit (charutils): false when nothing changed or the bit is past the set
        private static bool AddBit(byte[] bits, int bit)
        {
            if (bit < 0 || bit / 8 >= bits.Length || (bits[bit / 8] & 1 << bit % 8) != 0) return false;
            bits[bit / 8] |= (byte)(1 << bit % 8);
            return true;
        }

        private static bool DelBit(byte[] bits, int bit)
        {
            if (bit < 0 || bit / 8 >= bits.Length || (bits[bit / 8] & 1 << bit % 8) == 0) return false;
            bits[bit / 8] &= (byte)~(1 << bit % 8);
            return true;
        }

        // m_LearnedAbilities is 49 bytes; the chars row may hold fewer
        private static byte[] LearnedAbilitiesOf(CharData c)
        {
            if (c.LearnedAbilities.Length < 49) Array.Resize(ref c.LearnedAbilities, 49);
            return c.LearnedAbilities;
        }

        // ability::CanLearnAbility: the ability's job as main or sub job, at its level
        private bool CanLearnAbility(CharData c, ushort id) =>
            loader_.Abilities?.Get(id) is { } a && (c.MJob == a.Job && c.MLvl >= a.Level || c.SJob == a.Job && c.SLvl >= a.Level);

        // CBattleEntity::ForParty: the party's members, or the entity alone
        private static IEnumerable<BattleEntity> PartyOf(BattleEntity b) => b switch
        {
            CharData { Party: { } party } => party.Members.ToArray(),
            MobEntity { LinkParty: { } link } => link.ToArray(),
            _ => new[] { b },
        };

        // CBaseEntity::GetEntity(targid) in the char's zone: a mob or an NPC below 0x400, a char below 0x700
        private object EntityAt(CharData c, ushort targid)
        {
            if (targid == 0) return null;
            if (targid < 0x400)
            {
                if (MobByTargId(c, targid) is { } mob) return mob;
                foreach (var n in AreaOf(c).Npcs)
                    if (n.TargId == targid) return n;
                return null;
            }
            return targid < 0x700 ? CharByTargId(c, targid) : null;
        }

        // the optional { key = bool } table addSpell and delSpell take
        private static bool ParamBool(LuaState l, int idx, string key, bool defaultValue)
        {
            if (!l.IsTable(idx)) return defaultValue;
            l.GetField(idx, key);
            bool v = l.Type(-1) == LuaNative.LUA_TBOOLEAN ? l.ToBoolean(-1) : defaultValue;
            l.Pop(1);
            return v;
        }

        // charutils::SaveCharInventoryCapacity
        private void SaveCharInventoryCapacity(CharData c)
        {
            var s = c.StorageSize;
            Exec("UPDATE char_storage SET inventory = ?, safe = ?, locker = ?, satchel = ?, sack = ?, `case` = ?, wardrobe = ?, " +
                 "wardrobe2 = ?, wardrobe3 = ?, wardrobe4 = ?, wardrobe5 = ?, wardrobe6 = ?, wardrobe7 = ?, wardrobe8 = ? WHERE charid = ?",
                 BoundValue.Of(s[(int)Container.Inventory]), BoundValue.Of(s[(int)Container.MogSafe]), BoundValue.Of(s[(int)Container.MogLocker]),
                 BoundValue.Of(s[(int)Container.MogSatchel]), BoundValue.Of(s[(int)Container.MogSack]), BoundValue.Of(s[(int)Container.MogCase]),
                 BoundValue.Of(s[(int)Container.Wardrobe]), BoundValue.Of(s[(int)Container.Wardrobe2]), BoundValue.Of(s[(int)Container.Wardrobe2 + 1]),
                 BoundValue.Of(s[(int)Container.Wardrobe2 + 2]), BoundValue.Of(s[(int)Container.Wardrobe2 + 3]), BoundValue.Of(s[(int)Container.Wardrobe2 + 4]),
                 BoundValue.Of(s[(int)Container.Wardrobe2 + 5]), BoundValue.Of(s[(int)Container.Wardrobe2 + 6]), BoundValue.Of(c.Id));
        }

        private void RegisterPlayerBindings(Dictionary<string, LuaFunction> m)
        {
            // canLearnSpell: 96 already known, 95 not for the char's jobs, else 0
            m["canLearnSpell"] = l =>
            {
                if (Pc(l, "canLearnSpell") is not CharData c) return Ret(l, 0);
                int id = l.ToIntArg(2);
                if (HasBit(c.SpellList, id)) return Ret(l, MsgSpellAlreadyKnown);
                return Ret(l, GetSpell((ushort)id) is { } spell && CharCanUseSpell(c, spell) ? 0 : MsgSpellCannotLearn);
            };
            // addSpell(id, { silentLog = false, saveToDB = true, sendUpdate = true }): charutils::addSpell, the
            // MAGIC_DATA, "learns a new spell!" and the char_spells row
            m["addSpell"] = l =>
            {
                if (Pc(l, "addSpell") is not CharData c) return 0;
                int id = l.ToIntArg(2);
                bool silentLog = ParamBool(l, 3, "silentLog", false), saveToDb = ParamBool(l, 3, "saveToDB", true),
                     sendUpdate = ParamBool(l, 3, "sendUpdate", true);
                if (GetSpell((ushort)id) == null || HasBit(c.SpellList, id) || id / 8 >= c.SpellList.Length) return 0;
                c.SpellList[id / 8] |= (byte)(1 << id % 8);
                if (sendUpdate) PushTo(c, LoginPackets.MagicData(c));
                if (!silentLog) PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgLearnsNewSpell));
                if (saveToDb) Exec("INSERT IGNORE INTO char_spells VALUES (?, ?)", BoundValue.Of(c.Id), BoundValue.Of((ushort)id));
                return 0;
            };
            // delSpell(id, { saveToDB = true, sendUpdate = true })
            m["delSpell"] = l =>
            {
                if (Pc(l, "delSpell") is not CharData c) return 0;
                int id = l.ToIntArg(2);
                bool saveToDb = ParamBool(l, 3, "saveToDB", true), sendUpdate = ParamBool(l, 3, "sendUpdate", true);
                if (GetSpell((ushort)id) == null || !HasBit(c.SpellList, id)) return 0;
                c.SpellList[id / 8] &= (byte)~(1 << id % 8);
                if (sendUpdate) PushTo(c, LoginPackets.MagicData(c));
                if (saveToDb) Exec("DELETE FROM char_spells WHERE charid = ? AND spellid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of((ushort)id));
                return 0;
            };
            // openDoor(seconds = 7): a closed door opens, and closes again after the time
            m["openDoor"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to open door with invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (npc.Animation != AnimationCloseDoor) return 0;
                npc.Animation = AnimationOpenDoor;
                npc.DoorCloseAtMs = NowMs() + (l.IsNil(2) ? 7000 : (long)OptU32(l, 2) * 1000);
                BroadcastNpcUpdate(npc, EntityUpdatePacket.UpdateCombat);
                return 0;
            };
            m["setCurrency"] = l =>
            {
                if (Pc(l, "setCurrency") is CharData c) SetPoints(c, l.ToStringArg(2), l.ToIntArg(3));
                return 0;
            };
            m["delCurrency"] = l =>
            {
                if (Pc(l, "delCurrency") is CharData c) AddPoints(c, l.ToStringArg(2), -l.ToIntArg(3));
                return 0;
            };
            // hasEquipped(itemId): any of the 16 equip slots
            m["hasEquipped"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, false);
                int id = l.ToIntArg(2);
                return Ret(l, c.Equip.Exists(e => e.EquipSlot <= 15 && StorageItem(c, (Container)e.Container, e.InvSlot)?.ItemId == id));
            };
            // getCursorTarget: the entity the char has targeted (its last position update's facetarget)
            m["getCursorTarget"] = l =>
            {
                if (Entity(l) is CharData c && EntityAt(c, c.FaceTarget) is { } target) l.PushObject(target, BaseEntityType);
                else l.PushNil();
                return 1;
            };
            m["getGender"] = l =>
            {
                if (Entity(l) is CharData c) return Ret(l, c.Gender);
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["hasVisitedZone"] = l => Ret(l, Pc(l, "hasVisitedZone") is CharData c && HasBit(c.ZonesVisited, l.ToIntArg(2)));
            // delItem(itemId, quantity, container = inventory): the first stack of it, through an ItemClaimTransaction
            m["delItem"] = l =>
            {
                if (Pc(l, "delItem") is not CharData c) return Ret(l, false);
                ushort id = (ushort)l.ToIntArg(2);
                uint quantity = (uint)l.ToIntArg(3);
                int loc = l.IsNumber(4) ? l.ToIntArg(4) : 0;
                if (loc >= CharData.MaxContainers)
                {
                    log_("Attempting to delete an item from an invalid container.");
                    return Ret(l, false);
                }
                if (SearchItem(c, (Container)loc, id) is not { } item) return Ret(l, false);
                using var t = new ItemTransaction(this, c);
                if (t.ClaimSlot((Container)loc, item.Slot) == null || !t.Take((Container)loc, item.Slot, quantity) || !t.Commit())
                {
                    log_($"CLuaBaseEntity::delItem: {c.Name} kept item in slot {item.Slot}");
                    return Ret(l, false);
                }
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                return Ret(l, true);
            };
            // getPartySize(allianceParty = 0): the party's members, or that alliance party's
            m["getPartySize"] = l =>
            {
                if (Battle(l) is not CharData c) return Ret(l, Entity(l) is NpcEntity ? 0 : 1);
                int allianceParty = l.IsNil(2) ? 0 : l.ToIntArg(2);
                if (c.Party == null) return Ret(l, 1);
                if (allianceParty == 0) return Ret(l, c.Party.Members.Count);
                if (c.Party.Alliance != null)
                {
                    var parties = c.Party.Alliance.PartyList;
                    if (allianceParty >= parties.Count) throw new LuaException("getPartySize: alliance party out of range");
                    return Ret(l, parties[allianceParty].Members.Count);
                }
                return Ret(l, 1);
            };
            m["getContainerSize"] = l =>
            {
                if (Pc(l, "getContainerSize") is not CharData c) return Ret(l, 0);
                int loc = l.ToIntArg(2);
                return Ret(l, loc < CharData.MaxContainers ? c.StorageSize[loc] : 0);
            };
            // changeContainerSize(container, change): CItemContainer::AddBuff (the size held to 0..80 and never below
            // the items in it), the ITEM_MAX and the char_storage row
            m["changeContainerSize"] = l =>
            {
                if (Pc(l, "changeContainerSize") is not CharData c) return 0;
                int loc = l.ToIntArg(2);
                int change = (sbyte)l.ToIntArg(3);
                if (loc >= CharData.MaxContainers)
                {
                    log_($"CLuaBaseEntity::changeContainerSize: bad container id ({loc})");
                    return 0;
                }
                if (loc == (int)Container.Storage) AddStorageBuff(c, change);
                else
                {
                    c.ContainerBuff[loc] += change;
                    int size = Math.Clamp(c.ContainerBuff[loc], 0, 80);
                    if (size >= c.Items.Count(i => i.Location == (Container)loc && i.Slot != 0)) c.StorageSize[loc] = (byte)size;
                    else log_($"ItemContainer <{loc}>: Bad new container size {size}");
                }
                PushTo(c, LoginPackets.ItemMax(c));
                SaveCharInventoryCapacity(c);
                return 0;
            };
            m["canUseMisc"] = l => Ret(l, ZoneInfoOf(ZoneOf(Entity(l))).CanUseMisc((ushort)l.ToEnumArg(2)));
            // changeMusic(slot, track): GP_SERV_COMMAND_MUSIC
            m["changeMusic"] = l =>
            {
                if (Pc(l, "changeMusic") is not CharData c) return 0;
                var w = new PacketWriter(0x05F, 8);
                w.U16(4, (ushort)l.ToEnumArg(2)); // xi::MusicSlot
                w.U16(6, (ushort)l.ToIntArg(3));
                PushTo(c, w.Packet);
                return 0;
            };

            // addLearnedAbility: the learned set, the ability list, the chars row, COMMAND_DATA and "learns a new
            // ability!"
            m["addLearnedAbility"] = l =>
            {
                if (Pc(l, "addLearnedAbility") is not CharData c) return 0;
                int id = l.ToIntArg(2);
                if (!AddBit(LearnedAbilitiesOf(c), id)) return 0;
                AddBit(c.Abilities, id);
                SaveLearnedAbilities(c);
                PushTo(c, LoginPackets.CommandData(c));
                PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgLearnsNewAbility));
                return 0;
            };
            // canLearnAbility: 444 already learned, 443 not for the char's jobs, else 0
            m["canLearnAbility"] = l =>
            {
                if (Pc(l, "canLearnAbility") is not CharData c) return Ret(l, 0);
                ushort id = (ushort)l.ToIntArg(2);
                if (HasBit(c.LearnedAbilities, id)) return Ret(l, MsgAbilityAlreadyKnown);
                return Ret(l, CanLearnAbility(c, id) ? 0 : MsgAbilityCannotLearn);
            };
            m["delLearnedAbility"] = l =>
            {
                if (Pc(l, "delLearnedAbility") is not CharData c) return 0;
                if (!DelBit(LearnedAbilitiesOf(c), l.ToIntArg(2))) return 0;
                SaveLearnedAbilities(c);
                PushTo(c, LoginPackets.CommandData(c));
                return 0;
            };
            // unequipItem(slot): charutils::UnequipItem
            m["unequipItem"] = l =>
            {
                if (Entity(l) is CharData c) loader_.Stats?.UnequipItem(c, (byte)l.ToIntArg(2));
                return 0;
            };
            // canEquipItem(itemId, checkLevel = false): equipment for the main job and race, at the level when asked
            m["canEquipItem"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Attempt to check equippable item with non-player ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                ushort id = (ushort)l.ToIntArg(2);
                bool checkLevel = !l.IsNil(3) && l.ToBoolean(3);
                var info = loader_.Stats?.Items.Equipment(id);
                if (info == null)
                {
                    log_($"PItem was null, itemID = {id}.");
                    return Ret(l, false);
                }
                if ((info.Jobs & 1u << (c.MJob - 1)) == 0 || !info.EquippableByRace(c.Look.Race)) return Ret(l, false);
                return Ret(l, !checkLevel || info.ReqLvl <= c.MLvl);
            };
            // engage(targid): PAI->Engage on the entity at that act index (a char attacks a mob, a mob a char)
            m["engage"] = l =>
            {
                int targid = l.ToIntArg(2);
                if (targid <= 0) return 0;
                switch (Entity(l))
                {
                    case CharData c:
                        Engage(c, (ushort)targid);
                        break;
                    case MobEntity mob:
                        foreach (var (o, _) in charSessions_)
                            if (SameArea(o, mob) && o.TargId == targid)
                            {
                                MobEngage(mob, o, NowMs());
                                break;
                            }
                        break;
                }
                return 0;
            };
            // getParty: the party's members (the entity alone without one)
            m["getParty"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                int i = 1;
                foreach (var member in PartyOf(b))
                {
                    l.PushObject(member);
                    l.RawSetI(-2, i++);
                }
                return 1;
            };
            // getPartyWithTrusts: ForPartyWithTrusts (the members, then each member's trusts; alone, the char and its own)
            m["getPartyWithTrusts"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                int i = 1;
                var members = c.Party?.Members.ToArray() ?? new[] { c };
                foreach (var member in members)
                {
                    l.PushObject(member);
                    l.RawSetI(-2, i++);
                }
                foreach (var member in members)
                {
                    foreach (var trust in member.PTrusts)
                    {
                        l.PushObject(trust);
                        l.RawSetI(-2, i++);
                    }
                }
                return 1;
            };
        }
    }
}
