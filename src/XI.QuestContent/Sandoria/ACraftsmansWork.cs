// A Craftsman's Work (Log ID 0, Quest ID 94, DRG AF1), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Miaux.lua (quest rework B3b, San d'Oria group 15; call-only).
// Miaux : !pos -169.127 2.999 158.677 231 (no default action; his script's 11).
// A DRG of main level AF1_QUEST_LEVEL or more with this quest available: 73 while the char var
// has_seen_drgaf1_quest_already is 0, else 71 (the short version). Accepted without the Altepa Polishing Stone: 69;
// with it: 70. Else his script's 11. 73 with option 2: has_seen_drgaf1_quest_already 1 (declined); 73 or 71 with
// option 1: begun, has_seen_drgaf1_quest_already 0, aCraftsmanWork 1. 70's finish: the Peregrine (npcUtil.giveItem);
// given: aCraftsmanWork 0, the stone taken, San d'Oria fame 20, completed (the raw player:completeQuest, which keeps
// the quest's own Quest[0][94] vars).
// Stay Lua (char var / key item only, they never name the quest): Eastern Altepa Desert's qm (aCraftsmanWork 1 and
// Decurio_I_IIIKilled 0: pops Decurio I-III, FEEL_A_HOSTILE_GAZE; Decurio_I_IIIKilled 1: the Altepa Polishing Stone,
// aCraftsmanWork 2, the kill var 0; else REMNANTS_OF_A_PAST_AGE) and Decurio I-III's death (aCraftsmanWork 1: the kill
// var 1). Read only: roe_records, Vingijard, Chasing Quotas (main's ChasingQuotas.cs: offered once this is completed;
// its accepted Miaux arms answer first, as its container does in the Lua).
// Kept as the Lua has it: the accepted arms read no job or level; 73 / 71 / 70's finishes read no status; 70 with no
// free slot does nothing (npcUtil.giveItem's message only).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ACraftsmansWork : QuestScript
    {
        const string Seen = "has_seen_drgaf1_quest_already";
        const string Work = "aCraftsmanWork";

        public ACraftsmansWork() : base(QuestLog.Sandoria, "A_CRAFTSMANS_WORK") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Miaux.lua onTrigger, the quest arms (his else, 11, stays in his script)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Miaux).Called().OnTrigger(q =>
                {
                    QuestStatus status = q.Quest.Status;

                    if (q.Player.MainJob == Job.Drg &&
                        q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                        status == QuestStatus.Available)
                    {
                        if (q.Player.CharVar(Seen) == 0)
                        {
                            return QuestReply.Event(73);
                        }
                        else
                        {
                            return QuestReply.Event(71); // the short version after the big cutscene
                        }
                    }
                    else if (status == QuestStatus.Accepted &&
                        !q.Player.HasKeyItem(KeyItem.AltepaPolishingStone))
                    {
                        return QuestReply.Event(69);
                    }
                    else if (status == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(70);
                    }

                    return null;
                });

            b.AnyState()
                // Miaux.lua onEventFinish 73, 71, 70
                .OnEventFinish(Zone.NorthernSanDoria, 73, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.SetCharVar(Seen, 1); // declined
                    }
                    else if (q.Option == 1)
                    {
                        Accept(q);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 71, q =>
                {
                    if (q.Option == 1)
                    {
                        Accept(q);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 70, q =>
                {
                    if (q.Player.GiveItem(Item.Peregrine))
                    {
                        q.Player.SetCharVar(Work, 0);
                        q.Player.DelKeyItem(KeyItem.AltepaPolishingStone);
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        q.Player.CompleteQuest(QuestLog.Sandoria, "A_CRAFTSMANS_WORK"); // the raw player:completeQuest
                    }
                });
        }

        static void Accept(IQuestContext q)
        {
            q.Quest.Begin();
            q.Player.SetCharVar(Seen, 0);
            q.Player.SetCharVar(Work, 1);
        }
    }
}
