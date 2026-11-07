// Flyers for Regine (Log ID 0, Quest ID 16), ported from the quest branches of
// scripts/zones/Port_San_dOria/npcs/Regine.lua, scripts/zones/Southern_San_dOria/npcs/Cletae.lua and
// quests.flyers_for_regine.onTrade (scripts/quests/flyers_for_regine.lua), which the scripts of fifteen NPCs of the
// three San d'Oria cities called (quest rework B3b, San d'Oria group 11; call-only: each script now calls this quest).
// Regine : !pos 68 -9 -74 232. Regine's 510 (2) offers it; option 2 gives the fifteen Magicmart Flyers (a stack of 1
// and one of 14) and adds the quest. Accepted: each of the fifteen NPCs takes a flyer once (FLYER_ACCEPTED, its
// FFR_<NAME> line spoken to the char, its bit of the char var [ffr]deliveryMask, the flyer taken); again FLYER_ALREADY.
// Regine: 603 once the low fifteen bits are set (npcUtil.completeQuest step for step: fame +20, 440 gil at GIL_RATE
// with GIL_OBTAINED, Advertising Executive, the mask var deleted, the raw player:completeQuest, which keeps the quest's
// own Quest[0][16] vars), else 510 (3) when no flyer is held; 10 gil or more for another flyer. Cletae refuses a flyer
// (FLYER_REFUSED). The scripts keep their own answers when the quest does not act: Regine's menu 510 and its shops (510
// options 0 / 1), Cletae's guild shop. The Lua's state is the status and the char var [ffr]deliveryMask (no quest var).
// Stays in Lua: the trigger-area hint (quests.ffr.onTriggerAreaEnter, called by each city's Zone.lua:
// FFR_LOOKS_CURIOUSLY_BASE + offset near an NPC not yet served, while accepted) and quests.ffr.initZone; they read the
// status and the mask only.
// Kept as the Lua has it: 510 option 2 gives the flyers and calls addQuest in any status (completed: the log's current
// bit is set, the status stays completed); 603's finish reads no status; isFull reads only the low 15 bits (a GM mask
// of 65535 or -1 is full); a flyer to an NPC already served shows FLYER_ALREADY and is kept; the NPCs' and Regine's
// trades are tradeHas (an extra item, or more gil, is accepted; only the flyer, or 10 gil, is taken); Cletae refuses
// only exactly one flyer.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Sandoria
{
    public sealed class FlyersForRegine : QuestScript
    {
        // the Lua's npcData (scripts/quests/flyers_for_regine.lua): its keys (ffrId) must match these; the trigger areas and
        // hints stay there, and its npc field is no longer read. Each NPC, its zone, its ffrId (the bit of [ffr]deliveryMask) and its FFR_<NAME> text
        static readonly (ushort Zone, string Npc, int FfrId, ZoneText Line)[] NpcData =
        {
            (Zone.PortSanDoria, Npc.PortSanDoria.Answald, 0, Text.PortSanDoria.FFR_ANSWALD),
            (Zone.PortSanDoria, Npc.PortSanDoria.Prietta, 1, Text.PortSanDoria.FFR_PRIETTA),
            (Zone.PortSanDoria, Npc.PortSanDoria.Miene, 2, Text.PortSanDoria.FFR_MIENE),
            (Zone.PortSanDoria, Npc.PortSanDoria.Portaure, 3, Text.PortSanDoria.FFR_PORTAURE),
            (Zone.PortSanDoria, Npc.PortSanDoria.Auvare, 4, Text.PortSanDoria.FFR_AUVARE),
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Coullene, 5, Text.NorthernSanDoria.FFR_COULLENE),
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilberdrier, 6, Text.NorthernSanDoria.FFR_GUILBERDRIER),
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Boncort, 7, Text.NorthernSanDoria.FFR_BONCORT),
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Capiria, 8, Text.NorthernSanDoria.FFR_CAPIRIA),
            (Zone.NorthernSanDoria, Npc.NorthernSanDoria.Villion, 9, Text.NorthernSanDoria.FFR_VILLION),
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Blendare, 10, Text.SouthernSanDoria.FFR_BLENDARE),
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rosel, 11, Text.SouthernSanDoria.FFR_ROSEL),
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Maugie, 12, Text.SouthernSanDoria.FFR_MAUGIE),
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Adaunel, 13, Text.SouthernSanDoria.FFR_ADAUNEL),
            (Zone.SouthernSanDoria, Npc.SouthernSanDoria.Leuveret, 14, Text.SouthernSanDoria.FFR_LEUVERET),
        };

        // ID.text.FLYER_ALREADY / FLYER_ACCEPTED of the trade's zone (zones[player:getZoneID()])
        static ZoneText FlyerAlready(ushort zone) => zone switch
        {
            Zone.PortSanDoria => Text.PortSanDoria.FLYER_ALREADY,
            Zone.NorthernSanDoria => Text.NorthernSanDoria.FLYER_ALREADY,
            _ => Text.SouthernSanDoria.FLYER_ALREADY,
        };
        static ZoneText FlyerAccepted(ushort zone) => zone switch
        {
            Zone.PortSanDoria => Text.PortSanDoria.FLYER_ACCEPTED,
            Zone.NorthernSanDoria => Text.NorthernSanDoria.FLYER_ACCEPTED,
            _ => Text.SouthernSanDoria.FLYER_ACCEPTED,
        };

        const string DeliveryMask = "[ffr]deliveryMask";

        public FlyersForRegine() : base(QuestLog.Sandoria, "FLYERS_FOR_REGINE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Regine.lua onTrigger: ready to accept quest
            b.State(QuestStatus.Available)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Regine).Called().OnTrigger(QuestReply.Event(510, 2));

            // all 15 flyers delivered
            b.State(QuestStatus.Accepted, when: q => BastokWildcat.IsFull(q.Player.CharVar(DeliveryMask), 15))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Regine).Called().OnTrigger(QuestReply.Event(603));

            // on quest but out of flyers
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasItem(Item.MagicmartFlyer))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Regine).Called().OnTrigger(QuestReply.Event(510, 3));

            var accepted = b.State(QuestStatus.Accepted)
                // Regine.lua onTrade: another flyer for 10 gil
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Regine).Called().OnTrade(q =>
                {
                    // npcUtil.tradeHas(trade, { { 'gil', 10 } })
                    if (q.Trade.HasWithGil(10))
                    {
                        if (q.Player.GiveItem(Item.MagicmartFlyer))
                        {
                            q.Trade.Confirm();
                        }
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                // Cletae.lua onTrade: the leathercraft guild does not take a flyer
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Cletae).Called().OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.MagicmartFlyer))
                    {
                        return QuestReply.MessageSpecial(Text.SouthernSanDoria.FLYER_REFUSED);
                    }
                    return null;
                });

            // quests.flyers_for_regine.onTrade(player, npc, trade, ffrId): one handler per NPC, its ffrId and its line
            foreach (var (zone, npc, ffrId, line) in NpcData)
            {
                accepted.Npc(zone, npc).Called().OnTrade(q =>
                {
                    if (q.Trade.Has(Item.MagicmartFlyer))
                    {
                        int mask = q.Player.CharVar(DeliveryMask);
                        bool alreadyDelivered = (mask & (1 << ffrId)) != 0;

                        if (alreadyDelivered)
                        {
                            q.Text.Special(FlyerAlready(zone));
                        }
                        else
                        {
                            q.Text.Special(FlyerAccepted(zone));
                            // player:showText(npc, ID.text['FFR_' .. string.upper(data.npc)])
                            q.Player.ShowText(line);
                            q.Player.SetCharVar(DeliveryMask, mask | (1 << ffrId));
                            q.Trade.Confirm();
                        }
                        return QuestReply.NoAction();
                    }
                    return null;
                });
            }

            // Regine.lua onEventFinish (csid 510 option 2, csid 603); the shops (510 options 0 / 1) stay in the script
            b.AnyState()
                .OnEventFinish(Zone.PortSanDoria, 510, q =>
                {
                    if (q.Option == 2)
                    {
                        if (q.Player.GiveItems((Item.MagicmartFlyer, 1), (Item.MagicmartFlyer, 14)))
                        {
                            q.Quest.Begin();
                        }
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 603, q =>
                {
                    // npcUtil.completeQuest(player, SANDORIA, FLYERS_FOR_REGINE, { fame = 20, fameArea = SANDORIA,
                    // gil = 440, title = ADVERTISING_EXECUTIVE, var = '[ffr]deliveryMask' }), step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[0][16] vars. The rate's product
                    // reaches the engine as a double, which it rounds half away from zero (llround)
                    int gil = RateAmount(440, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddFame(FameArea.Sandoria, 20);
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.PortSanDoria.GIL_OBTAINED, gil);
                    q.Player.AddTitle(Title.AdvertisingExecutive);
                    q.Player.SetCharVar(DeliveryMask, 0);
                    q.Player.CompleteQuest(QuestLog.Sandoria, "FLYERS_FOR_REGINE");
                });
        }

        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
