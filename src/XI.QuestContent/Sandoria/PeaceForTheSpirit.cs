// Peace for the Spirit (Log ID 0, Quest ID 86, RDM AF3), ported from the quest branches of
// scripts/zones/Chateau_dOraguille/npcs/Curilla.lua, scripts/zones/Southern_San_dOria/npcs/Sharzalion.lua and Daggao.lua,
// scripts/zones/FeiYin/npcs/Dry_Fountain.lua and Zone.lua, scripts/zones/Garlaige_Citadel/npcs/Oaken_Box.lua and
// scripts/zones/Northern_San_dOria/Zone.lua (quest rework B3b, San d'Oria group 13; call-only: each script calls the
// quest at the spot its branch was). Curilla : !pos 27 0.1 0.1 233; Sharzalion : !pos 95 0 111 230; Daggao : !pos 89 0
// 119 230. The quest's state is the status and the char var peaceForTheSpiritCS (not a quest var).
// Curilla: 109 for an RDM of main level AF2_QUEST_LEVEL (a setting compared as a number) with Enveloped in Darkness
// completed (option 1 begins it); 114 for anyone else once Enveloped in Darkness is completed; accepted: 108 (CS 0 / 1),
// 113 (CS 2-4, 6...), 51 (CS 5); completed: 52. Sharzalion: 69 (Enveloped in Darkness completed, available); accepted:
// 64 (CS 0; finish CS 1), 65 (CS 1), 66 (CS 2 / 3; finish CS 3). Fei'Yin's zone-in spawns Miser Murphy (no claim, no
// enmity, no cutscene) while CS is 1 and no Antique Coin is held; the Dry Fountain, accepted, takes exactly one item, an
// Antique Coin: 17 (finish: tradeComplete, CS 2). Daggao: 72 at CS 3 (finish CS 4), 73 at CS 5. Garlaige Citadel's
// Oaken Box: BOXES_SCATTERED while accepted with the Nail Puller held; at CS 4 without it and with the Guardian Statue not
// spawned (a dead statue is still spawned until its body goes), the statue spawns claimed; accepted, a trade holding the
// Nail Puller (tradeHas: other items ride along): 14 (finish: the puller taken, CS 5). Northern San d'Oria's zone-in at
// CS 5 with a free slot: 49, whose finish completes the quest (npcUtil.completeQuest step for step: Warlock's Chapeau,
// stopping when it cannot be given; Paragon of Red Mage Excellence; fame = 20 with no fameArea, so no fame; the raw
// player:completeQuest, which keeps the quest's own Quest[0][86] vars) and sets CS 0 only when that completion gave the
// chapeau.
// The scripts keep their own answers when the quest does not act: Curilla's Trust branch (573, Trust: San d'Oria, before
// this quest's call) and 530, Sharzalion's 15, Daggao's 60, the Dry Fountain's DRIED_UP_FOUNTAIN, the Oaken Box's
// FEELS_WRONG (the statue alive, before this quest's call) and BOXES_HERE, both zones' other zone-in work (Northern San
// d'Oria's mog house event; Fei'Yin's ACP mission 29 and Pieuje's Decision's 19, after this spawn).
// Kept as the Lua has it: Daggao, the Oaken Box's spawn and both zone-ins read CS in any status; 49 completes the quest
// from any status (from completed too: the chapeau, rare and held, is refused and CS stays 5); 109 begins it only on
// option 1.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class PeaceForTheSpirit : QuestScript
    {
        const string Cs = "peaceForTheSpiritCS";

        public PeaceForTheSpirit() : base(QuestLog.Sandoria, "PEACE_FOR_THE_SPIRIT") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Curilla.lua onTrigger, after the Trust branch, in its order; Sharzalion, the Dry Fountain, the Oaken Box's trade
            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).Called().OnTrigger(q =>
                {
                    int questStatus = q.Player.CharVar(Cs);
                    if (questStatus == 5)
                    {
                        return QuestReply.Event(51);
                    }
                    else if (questStatus > 1)
                    {
                        return QuestReply.Event(113);
                    }
                    else
                    {
                        return QuestReply.Event(108);
                    }
                })
                // Sharzalion.lua onTrigger's accepted arms (else the script's 15)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).Called().OnTrigger(q =>
                {
                    int peaceForTheSpiritCS = q.Player.CharVar(Cs);
                    if (peaceForTheSpiritCS == 0)
                    {
                        return QuestReply.Event(64);
                    }
                    else if (peaceForTheSpiritCS == 1)
                    {
                        return QuestReply.Event(65);
                    }
                    else if (peaceForTheSpiritCS == 2 || peaceForTheSpiritCS == 3)
                    {
                        return QuestReply.Event(66);
                    }
                    return null;
                })
                // Dry_Fountain.lua onTrade
                .Npc(Zone.Feiyin, Npc.Feiyin.DryFountain).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.AntiqueCoin, 1) &&
                        q.Trade.ItemCount == 1)
                    {
                        return QuestReply.Event(17);
                    }
                    return null;
                })
                // Oaken_Box.lua onTrade: npcUtil.tradeHas(trade, xi.item.NAIL_PULLER)
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.OakenBox).Called().OnTrade(q =>
                {
                    if (q.Trade.Has(Item.NailPuller))
                    {
                        return QuestReply.Event(14);
                    }
                    return null;
                });

            // Curilla: start
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Rdm &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                    q.Player.QuestStatus(QuestLog.Sandoria, "ENVELOPED_IN_DARKNESS") == QuestStatus.Completed)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).Called().OnTrigger(QuestReply.Event(109));

            // Curilla: default dialogue after Peace for the Spirit
            b.State(QuestStatus.Completed)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).Called().OnTrigger(QuestReply.Event(52));

            // Curilla: default dialogue after Enveloped in Darkness; Sharzalion's 69
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "ENVELOPED_IN_DARKNESS") == QuestStatus.Completed)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).Called().OnTrigger(QuestReply.Event(114))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).Called().OnTrigger(QuestReply.Event(69));

            b.AnyState()
                // Daggao.lua onTrigger (else the script's 60)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Daggao).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar(Cs) == 3)
                    {
                        return QuestReply.Event(72);
                    }
                    else if (q.Player.CharVar(Cs) == 5)
                    {
                        return QuestReply.Event(73);
                    }
                    return null;
                })
                // Oaken_Box.lua onTrigger, after its FEELS_WRONG (the statue alive), which stays in the script; else BOXES_HERE
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.OakenBox).Called().OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.NailPuller) &&
                        q.Quest.Status == QuestStatus.Accepted)
                    {
                        return QuestReply.MessageSpecial(Text.GarlaigeCitadel.BOXES_SCATTERED, Item.NailPuller);
                    }
                    else if (q.Player.CharVar(Cs) == 4 &&
                        !q.Player.HasItem(Item.NailPuller) &&
                        !q.Player.MobIsSpawned(Mob.GarlaigeCitadel.GUARDIAN_STATUE))
                    {
                        // SpawnMob(ID.mob.GUARDIAN_STATUE):updateClaim(player)
                        q.Player.SpawnMob(Mob.GarlaigeCitadel.GUARDIAN_STATUE, 0, claim: true);
                        return QuestReply.NoAction();
                    }
                    return null;
                });

            // FeiYin Zone.lua onZoneIn: Miser Murphy (RDM AF), no cutscene; the zone's ACP and Pieuje's Decision arms follow
            b.AnyState(when: q => q.Player.CharVar(Cs) == 1 && !q.Player.HasItem(Item.AntiqueCoin))
                .OnZoneIn(Zone.Feiyin, q =>
                {
                    // SpawnMob(ID.mob.MISER_MURPHY)
                    q.Player.SpawnMob(Mob.Feiyin.MISER_MURPHY);
                    return null;
                });

            // Northern_San_dOria Zone.lua onZoneIn, before the mog house event
            b.AnyState(when: q => q.Player.CharVar(Cs) == 5 && q.Player.FreeSlotCount >= 1)
                .OnZoneIn(Zone.NorthernSanDoria, q => QuestReply.Event(49));

            b.AnyState()
                // Curilla.lua onEventFinish
                .OnEventFinish(Zone.ChateauDoraguille, 109, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                // Sharzalion.lua and Daggao.lua onEventFinish
                .OnEventFinish(Zone.SouthernSanDoria, 64, q => q.Player.SetCharVar(Cs, 1))
                .OnEventFinish(Zone.SouthernSanDoria, 66, q => q.Player.SetCharVar(Cs, 3))
                .OnEventFinish(Zone.SouthernSanDoria, 72, q => q.Player.SetCharVar(Cs, 4))
                // Dry_Fountain.lua onEventFinish: player:tradeComplete()
                .OnEventFinish(Zone.Feiyin, 17, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar(Cs, 2);
                })
                // Oaken_Box.lua onEventFinish: player:confirmTrade()
                .OnEventFinish(Zone.GarlaigeCitadel, 14, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetCharVar(Cs, 5);
                })
                // Northern_San_dOria Zone.lua onEventFinish: CS 0 only when npcUtil.completeQuest gave the reward
                .OnEventFinish(Zone.NorthernSanDoria, 49, q =>
                {
                    if (CompleteWithReward(q))
                    {
                        q.Player.SetCharVar(Cs, 0);
                    }
                });
        }

        // npcUtil.completeQuest(player, SANDORIA, PEACE_FOR_THE_SPIRIT, { item = 12513, fame = 20,
        // title = PARAGON_OF_RED_MAGE_EXCELLENCE }), step for step: not quest:complete, whose cleanup would clear the
        // quest's own Quest[0][86] vars. No fameArea, so no fame
        static bool CompleteWithReward(IQuestContext q)
        {
            if (!q.Player.GiveItem(Item.WarlocksChapeau))
            {
                return false; // no free slot (or the chapeau held): npcUtil.completeQuest returns false
            }

            q.Player.AddTitle(Title.ParagonOfRedMageExcellence);
            q.Player.CompleteQuest(QuestLog.Sandoria, "PEACE_FOR_THE_SPIRIT");
            return true;
        }
    }
}
