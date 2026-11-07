// Personal Hygiene (Log ID 5, Quest ID 10), ported from the quest branches of scripts/zones/Kazham/npcs/Gatih_Mijurabi.lua
// (quest rework B3a). Gatih Mijurabi : !pos 58.249 -13.086 -49.084 250. The quest lives in the NPC script, so the NPC
// calls it (xi.cs.quest / xi.cs.questEvent) and keeps its own event 196 when the quest does not act.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class PersonalHygiene : QuestScript
    {
        public PersonalHygiene() : base(QuestLog.Outlands, "PERSONAL_HYGIENE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // bathed in scent: the offer
            b.State(QuestStatus.Available)
                .Npc(Zone.Kazham, Npc.Kazham.GatihMijurabi).Called().OnTrigger(q =>
                    q.Player.CharVar("BathedInScent") == 1 ? QuestReply.Event(191) : null)
                .OnEventFinish(Zone.Kazham, 191, q => q.Quest.Begin());

            // bathed: 192; no longer bathed: the Mithran Stone
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.GatihMijurabi).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar("BathedInScent") == 1)
                    {
                        return QuestReply.Event(192);
                    }
                    else if (q.Player.CharVar("BathedInScent") == 0)
                    {
                        return QuestReply.Event(193);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Kazham, 193, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.Kazham.ITEM_CANNOT_BE_OBTAINED, Item.MithranStone);
                    }
                    else
                    {
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][10] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "PERSONAL_HYGIENE");
                        q.Player.AddFame(FameArea.Windurst, 70);
                        q.Player.AddItem(Item.MithranStone);
                        q.Text.Special(Text.Kazham.ITEM_OBTAINED, Item.MithranStone);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.GatihMijurabi).Called().OnTrigger(q =>
                    q.Player.CharVar("BathedInScent") == 1 ? QuestReply.Event(195) : null);
        }
    }
}
