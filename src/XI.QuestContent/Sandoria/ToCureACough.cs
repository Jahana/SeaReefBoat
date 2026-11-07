// To Cure a Cough (Log ID 0, Quest ID 20), ported from the quest branches of
// scripts/zones/Southern_San_dOria/npcs/Nenne.lua and Amaura.lua and scripts/zones/Davoi/npcs/qm1.lua (quest rework
// B3b, San d'Oria group 11; call-only: each script now calls this quest). Nenne : !pos -114 -6 102 230; Amaura : !pos
// -85 -6 89 230; qm1 : !pos -115.830 -0.427 -184.289 149. Nenne's 538 (available, her char var toCureaCough 0, The
// Medicine Woman completed: toCureaCough 1). Amaura, once the Diary is read to page 3 (DiaryPage >= 3) or the quest is
// accepted: 645 with neither key item (its finish adds the quest), 646 with the Thyme Moss (finish: the moss taken, the
// Cough Medicine given). Davoi's qm1, accepted and no moss held: the Thyme Moss. Nenne with the Cough Medicine (in any
// status): 647 (finish: A Moss Kind Person, toCureaCough 0, the medicine taken, the Scroll of Treasure, fame +30,
// completed by the raw player:completeQuest, which keeps the quest's own Quest[0][20] vars). The scripts keep their
// own answers when the quest does not act: Nenne's 584, Amaura nothing (her default 642 then plays), the qm1 nothing.
// Amaura's default 642 (DefaultActions) alternates with her script, so the quest's 645 / 646
// play on every second talk.
// Stays in Lua: the Diary's pages (Diary.lua: 639 / 640 / 641 / 722, the char var DiaryPage, which Antreneau reads
// too); they read this quest's status and write no var of it.
// Kept as the Lua has it: the finishes read no status (645 on a completed quest sets the log's current bit, the status
// stays completed; 538 sets its char var whatever the status); Nenne's 647 and Amaura's 646 do not look at the status;
// with the Cough Medicine and no moss Amaura's quest arm is silent; 538 sets only a char var (the quest is begun by
// Amaura's 645).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ToCureACough : QuestScript
    {
        public ToCureACough() : base(QuestLog.Sandoria, "TO_CURE_A_COUGH") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Nenne.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("toCureaCough") == 0 &&
                    q.Player.QuestStatus(QuestLog.Sandoria, "THE_MEDICINE_WOMAN") == QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Nenne).Called().OnTrigger(QuestReply.Event(538));

            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.CoughMedicine))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Nenne).Called().OnTrigger(QuestReply.Event(647));

            // Amaura.lua onTrigger
            b.AnyState(when: q => q.Player.CharVar("DiaryPage") >= 3 || q.Quest.Status == QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura).Called().OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.ThymeMoss) &&
                        !q.Player.HasKeyItem(KeyItem.CoughMedicine))
                    {
                        return QuestReply.Event(645); // need thyme moss for cough med
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ThymeMoss))
                    {
                        return QuestReply.Event(646); // receive cough med for Nenne
                    }
                    return null;
                });

            // Davoi qm1.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.ThymeMoss))
                .Npc(Zone.Davoi, Npc.Davoi.qm1).Called().OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ThymeMoss);
                    return QuestReply.NoAction();
                });

            // Nenne.lua and Amaura.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.SouthernSanDoria, 538, q =>
                {
                    q.Player.SetCharVar("toCureaCough", 1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 647, q =>
                {
                    q.Player.AddTitle(Title.AMossKindPerson);
                    q.Player.SetCharVar("toCureaCough", 0);
                    q.Player.DelKeyItem(KeyItem.CoughMedicine);
                    q.Player.GiveKeyItem(KeyItem.ScrollOfTreasure);
                    q.Player.AddFame(FameArea.Sandoria, 30);
                    // the raw player:completeQuest, which keeps the quest's own Quest[0][20] vars
                    q.Player.CompleteQuest(QuestLog.Sandoria, "TO_CURE_A_COUGH");
                })
                .OnEventFinish(Zone.SouthernSanDoria, 645, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.SouthernSanDoria, 646, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ThymeMoss);
                    q.Player.GiveKeyItem(KeyItem.CoughMedicine);
                });
        }
    }
}
