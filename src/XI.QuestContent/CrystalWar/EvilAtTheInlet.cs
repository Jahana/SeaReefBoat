// Evil at the Inlet (Log ID 7, Quest ID 21), ported from the quest branches of scripts/zones/Crawlers_Nest_[S]/npcs/
// Rodeupansat.lua and scripts/zones/Vunkerl_Inlet_[S]/npcs/qm6.lua (quest rework B3b Crystal War, draft; call-only).
// Rodeupansat !pos 129.24 -34.353 25.927 171; qm6 !pos -635.385 -51.101 -454.311 83.
// Rodeupansat, in his order: available, the Bronze Ribbon of Service and level 30+: 107 (finish: the quest added, the
// Evil-warding Seal); accepted: the seal: 108; else 109 (finish: npcUtil.completeQuest: the Scroll of Recall-Jugner);
// completed: 110; else his script's 106. qm6: accepted with the seal: 112 (finish: the seal taken); else its script's
// NOTHING_OUT_OF_ORDINARY.
// Kept as the Lua has it: 109 answers at once if the seal is lost any other way; the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class EvilAtTheInlet : QuestScript
    {
        public EvilAtTheInlet() : base(QuestLog.CrystalWar, "EVIL_AT_THE_INLET") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Rodeupansat.lua onTrigger, in its order
            b.AnyState()
                .Npc(Zone.CrawlersNestS, Npc.CrawlersNestS.Rodeupansat).Called().OnTrigger(q =>
                {
                    QuestStatus evilAtTheInlet = q.Quest.Status;

                    // Change to BRASS_RIBBON_OF_SERVICE later when Campaign has been added.
                    if (evilAtTheInlet == QuestStatus.Available &&
                        q.Player.HasKeyItem(KeyItem.BronzeRibbonOfService) &&
                        q.Player.MainLevel >= 30)
                    {
                        return QuestReply.Event(107); // Start quest "Evil at the Inlet"
                    }
                    else if (evilAtTheInlet == QuestStatus.Accepted)
                    {
                        if (q.Player.HasKeyItem(KeyItem.EvilWardingSeal))
                        {
                            return QuestReply.Event(108); // During quest after receiving KI and before losing KI
                        }
                        else
                        {
                            return QuestReply.Event(109); // After losing KI and returning for reward
                        }
                    }
                    else if (evilAtTheInlet == QuestStatus.Completed)
                    {
                        return QuestReply.Event(110); // New standard dialog after "Evil at the Inlet"
                    }
                    return null; // the script's 106 (Standard dialog)
                });

            // Vunkerl_Inlet_[S] qm6.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.EvilWardingSeal))
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm6).Called().OnTrigger(QuestReply.Event(112));

            // the two scripts' onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.CrawlersNestS, 107, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.EvilWardingSeal);
                })
                .OnEventFinish(Zone.CrawlersNestS, 109, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[7][21] vars
                    if (q.Player.GiveItem(Item.ScrollOfRecallJugner))
                    {
                        q.Player.CompleteQuest(QuestLog.CrystalWar, "EVIL_AT_THE_INLET");
                    }
                })
                .OnEventFinish(Zone.VunkerlInletS, 112, q =>
                {
                    q.Player.DelKeyItem(KeyItem.EvilWardingSeal);
                });
        }
    }
}
