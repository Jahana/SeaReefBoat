// The Three Magi (Log ID 2, Quest ID 66; BLM AF1), ported from the The Three Magi branches of
// scripts/zones/Heavens_Tower/npcs/Chumimi.lua and scripts/zones/Xarcabard/npcs/qm1.lua (quest rework B3b, Windurst
// group 30; call-only). Chumimi : !pos 0.1 30 21 242; qm1 : !pos -331 -29 -49 112.
// Chumimi: available, main job BLM, main level >= AF1_QUEST_LEVEL: 260 (0, 613, 0, 0, Meteorite, Glowstone); its
// finish: begun, theThreeMagiSupport = the option (3 Koru-Moru, 2 Shantotto, 1 Yoran-Oran). Accepted:
// 261 (0, 0, 0, 0, Meteorite, Glowstone). The trade, accepted: one Glowstone and nothing else: 269; its finish:
// npcUtil.giveItem (Casting Wand); given: the title by the var (3 Professor Koru-Moru Supporter,
// 2 Doctor Shantotto Supporter, else Doctor Yoran-Oran Supporter), the trade taken, needToZone, the var 0,
// Windurst fame 20, completed.
// Her script calls this quest first, then Recollections, then The Root of the Problem (one if-chain in the Lua), and
// plays her 259 when none answers. She has no DefaultActions entry and no container.
// qm1 (Xarcabard): the trigger: Chaos Elemental up: DO_NOT_SENSE; accepted: SENSE_EVIL; else NOTHING_OUT_OF_ORDINARY.
// The trade, accepted: a Faded Crystal (npcUtil.tradeHas, not exact), no Glowstone held, and
// npcUtil.popFromQM (Chaos Elemental, hide 0): the trade confirmed (the crystal only). The Glowstone is the
// Chaos Elemental's drop.
// Kept as the Lua has it: 260 begins on any option and stores it (option 0 or 4+ gives Yoran-Oran's title); 269's
// finish reads no status; the title comes before the trade is taken; DO_NOT_SENSE reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheThreeMagi : QuestScript
    {
        const string Support = "theThreeMagiSupport";

        public TheThreeMagi() : base(QuestLog.Windurst, "THE_THREE_MAGI") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Chumimi).Called()
                    // Chumimi.lua onTrade, the The Three Magi arm
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Trade.HasItemQty(Item.Glowstone, 1) &&
                            q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(269); // Finish Quest "The Three Magi"
                        }

                        return null; // Recollections' arm next
                    })
                    // Chumimi.lua onTrigger, the The Three Magi arms
                    .OnTrigger(q =>
                    {
                        QuestStatus theThreeMagi = q.Quest.Status;
                        int mLvl = q.Player.MainLevel;
                        int mJob = q.Player.MainJob;

                        if (theThreeMagi == QuestStatus.Available &&
                            mJob == Job.Blm &&
                            mLvl >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                        {
                            // Start Quest "The Three Magi"
                            return QuestReply.Event(260, 0, 613, 0, 0, Item.Meteorite, Item.Glowstone);
                        }
                        else if (theThreeMagi == QuestStatus.Accepted)
                        {
                            // During Quest "The Three Magi"
                            return QuestReply.Event(261, 0, 0, 0, 0, Item.Meteorite, Item.Glowstone);
                        }

                        return null; // Recollections' arms next
                    })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.qm1).Called()
                    // qm1.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Trade.Has(Item.FadedCrystal) &&
                            !q.Player.HasItem(Item.Glowstone) &&
                            q.Player.PopFromQm(Mob.Xarcabard.CHAOS_ELEMENTAL, hide: 0))
                        {
                            q.Trade.Confirm();
                        }

                        return null;
                    })
                    // qm1.lua onTrigger
                    .OnTrigger(q =>
                    {
                        if (q.Player.MobIsSpawned(Mob.Xarcabard.CHAOS_ELEMENTAL))
                        {
                            return QuestReply.MessageSpecial(Text.Xarcabard.DO_NOT_SENSE);
                        }

                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            return QuestReply.MessageSpecial(Text.Xarcabard.SENSE_EVIL);
                        }

                        return QuestReply.MessageSpecial(Text.Xarcabard.NOTHING_OUT_OF_ORDINARY);
                    });

            // Chumimi.lua onEventFinish, the The Three Magi arms
            b.AnyState()
                .OnEventFinish(Zone.HeavensTower, 260, q =>
                {
                    // option 3: Koru-Moru -- option 2: Shantotto -- option 1: Yoran-Oran
                    q.Quest.Begin();
                    q.Player.SetCharVar(Support, q.Option);
                })
                .OnEventFinish(Zone.HeavensTower, 269, q =>
                {
                    if (q.Player.GiveItem(Item.CastingWand))
                    {
                        int choosetitle = q.Player.CharVar(Support);

                        if (choosetitle == 3)
                        {
                            q.Player.AddTitle(Title.ProfessorKoruMoruSupporter);
                        }
                        else if (choosetitle == 2)
                        {
                            q.Player.AddTitle(Title.DoctorShantottoSupporter);
                        }
                        else
                        {
                            q.Player.AddTitle(Title.DoctorYoranOranSupporter);
                        }

                        q.Trade.Complete();
                        q.Player.SetNeedToZone(true);
                        q.Player.SetCharVar(Support, 0);
                        q.Player.AddFame(FameArea.Windurst, 20);
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][66] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "THE_THREE_MAGI");
                    }
                });
        }
    }
}
