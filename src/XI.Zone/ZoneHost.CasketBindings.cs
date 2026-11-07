using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // The CLuaBaseEntity methods xi.caskets (the treasure caskets lv-appropriate kills leave) reaches that had no binding:
    // getLeaderID, getAlliance, messageName (GP_SERV_COMMAND_TALKNUMWORK2's name constructor), addTempItem, setModelId
    // and getEventTarget. Not ported: ItemClaimTransaction::start's refusal while another transaction is open (the
    // temp item goes through a fresh one).
    public sealed partial class ZoneHost
    {
        private void RegisterCasketBindings(Dictionary<string, LuaFunction> m)
        {
            // CLuaBaseEntity::getLeaderID: the alliance id, else the party id, else the char's own id
            m["getLeaderID"] = l =>
            {
                if (Pc(l, "getLeaderID") is not CharData c) return Ret(l, 0);
                if (c.Party != null) return Ret(l, c.Party.Alliance != null ? c.Party.Alliance.Id : c.Party.Id);
                return Ret(l, c.Id);
            };

            // CLuaBaseEntity::getAlliance: every member of the alliance (ForAlliance), an empty table for an NPC
            m["getAlliance"] = l =>
            {
                switch (Entity(l))
                {
                    case CharData c:
                        return ZoneLuaPushArray(l, new List<CharData>(ForAlliance(c)));
                    case BattleEntity b:
                        return ZoneLuaPushArray(l, new[] { b });
                    default:
                        log_("CLuaBaseEntity::getAlliance() - NPC passed to function.");
                        l.NewTable();
                        return 1;
                }
            };

            // CLuaBaseEntity::messageName(messageID, entity, p0..p3, chat = 4, sender, showSender): to a char, from the
            // sender (the char itself without one); from anything else, to the chars in range
            m["messageName"] = l =>
            {
                ushort messageId = (ushort)l.ToIntArg(2);
                object nameEntity = l.IsNil(3) ? null : l.ToObject(3);
                int Param(int i) => l.IsNil(i) ? 0 : (int)l.ToIntegral(i);
                int p0 = Param(4), p1 = Param(5), p2 = Param(6), p3 = Param(7);
                int chatType = l.IsNil(8) ? 4 : (int)l.ToIntegral(8);

                var self = Entity(l);
                if (self is CharData c)
                {
                    object luaSender = l.IsNil(9) ? null : l.ToObject(9);
                    object sender = luaSender ?? c;
                    bool showSpeaker = luaSender != null;
                    if (!l.IsNil(10)) showSpeaker = l.ToBoolean(10);
                    PushTo(c, TalkNumWork2Name(sender, messageId, nameEntity, p0, p1, p2, p3, chatType, showSpeaker));
                }
                else
                {
                    PushCharsNear(self, TalkNumWork2Name(self, messageId, nameEntity, p0, p1, p2, p3, chatType, false));
                }
                return 0;
            };

            // CLuaBaseEntity::addTempItem(itemID, quantity = 1): one stack into the temporary items, through an
            // ItemClaimTransaction (not silent)
            m["addTempItem"] = l =>
            {
                if (Pc(l, "addTempItem") is not CharData c) return Ret(l, false);
                ushort itemId = (ushort)l.ToIntArg(2);
                uint quantity = l.IsNil(3) ? 1 : (uint)l.ToIntegral(3);
                bool given = false;
                if (FreeSlots(c, Container.TempItems) != 0 && quantity != 0)
                {
                    if (ItemBasic(itemId) == null)
                    {
                        log_($"AddItem: Item <{itemId}> is not found in a database");
                    }
                    else
                    {
                        using var transaction = new ItemTransaction(this, c);
                        given = transaction.Give(Container.TempItems, itemId, quantity, silent: false).HasValue && transaction.Commit();
                    }
                }
                return Ret(l, given);
            };

            // CLuaBaseEntity::setModelId(modelId, slot): a char (or any entity given a slot) changes that slot's look
            // (main without one; a char is sent its GRAP_LIST); anything else changes its model id. UPDATE_LOOK either way.
            m["setModelId"] = l =>
            {
                ushort modelId = (ushort)l.ToIntArg(2);
                var e = Entity(l);
                bool hasSlot = l.IsNumber(3);
                if (e is CharData || hasSlot)
                {
                    int slot = hasSlot ? (int)l.ToEnum(3) : LookSlotMain; // as<SLOTTYPE>
                    if (e is CharData c)
                    {
                        switch (slot)
                        {
                            case LookSlotMain: c.Look.Main = modelId; break;
                            case LookSlotSub: c.Look.Sub = modelId; break;
                            case LookSlotRanged: c.Look.Ranged = modelId; break;
                            case LookSlotHead: c.Look.Head = modelId; break;
                            case LookSlotBody: c.Look.Body = modelId; break;
                            case LookSlotHands: c.Look.Hands = modelId; break;
                            case LookSlotLegs: c.Look.Legs = modelId; break;
                            case LookSlotFeet: c.Look.Feet = modelId; break;
                        }
                        PushTo(c, LoginPackets.GrapList(c));
                    }
                    else if (LookOf(e) is { } look && LookIndexOfSlot(slot) is int index)
                    {
                        look[index] = modelId;
                    }
                }
                else if (LookOf(e) is { } look)
                {
                    look[1] = modelId; // CBaseEntity::SetModelId: look.modelid
                }
                MarkUpdate(e, CharStats.UpdateLook);
                return 0;
            };

            // CLuaBaseEntity::getEventTarget: the current event's target entity (nil, with a warning, when it has none)
            m["getEventTarget"] = l =>
            {
                if (Pc(l, "getEventTarget") is not CharData c)
                {
                    l.PushNil();
                    return 1;
                }
                if (c.CurrentEvent.TargetEntity == null)
                {
                    log_($"EventTarget for event {c.CurrentEvent.EventId} is empty: {c.Name}");
                    l.PushNil();
                    return 1;
                }
                l.PushObject(c.CurrentEvent.TargetEntity);
                return 1;
            };
        }

        // SLOTTYPE
        private const int LookSlotMain = 0, LookSlotSub = 1, LookSlotRanged = 2, LookSlotHead = 4, LookSlotBody = 5, LookSlotHands = 6,
            LookSlotLegs = 7, LookSlotFeet = 8;

        // an NPC's or mob's look_t laid flat (size, modelid, head, body, hands, legs, feet, main, sub, ranged)
        private static ushort[] LookOf(object e) => e switch
        {
            NpcEntity npc => npc.Look,
            MobEntity mob => mob.Look,
            _ => null,
        };

        private static int? LookIndexOfSlot(int slot) => slot switch
        {
            LookSlotHead => 2, LookSlotBody => 3, LookSlotHands => 4, LookSlotLegs => 5, LookSlotFeet => 6, LookSlotMain => 7, LookSlotSub => 8, LookSlotRanged => 9,
            _ => null,
        };

        // GP_SERV_COMMAND_TALKNUMWORK2 (0x027), the message-with-name constructor: the actor, the message (0x8000 set
        // unless the sender is shown), the chat type, four numbers, the name (the name actor's, else the actor's) and,
        // shown, the sender's name
        private static BasicPacket TalkNumWork2Name(object actor, ushort messageId, object nameActor, int p0, int p1, int p2, int p3,
                                                    int chatType, bool showSender)
        {
            var w = new PacketWriter(0x027, 0x70);
            w.U32(4, IdOf(actor));
            w.U16(8, TargIdOf(actor));
            w.U16(10, showSender ? messageId : (ushort)(messageId | 0x8000));
            w.U16(12, (ushort)chatType);
            w.U32(16, unchecked((uint)p0));
            w.U32(20, unchecked((uint)p1));
            w.U32(24, unchecked((uint)p2));
            w.U32(28, unchecked((uint)p3));
            w.Name(32, NameOf(nameActor ?? actor), 32);
            if (showSender) w.Name(64, NameOf(actor), 15);
            return w.Packet;
        }
    }
}
