// Faded Promises (Log ID 1, Quest ID 73), a line-for-line port of scripts/quests/bastok/Faded_Promises.lua (quest
// rework B3b). Romualdo : !pos 133 -19 -36 237; Ayame : !pos 133 -19 34 237; Kagetora : !pos -96 -2 29 236; Alois :
// !pos 96 -20 14 237. At Prog 1 the Treasure Chest of Palborough Mines gives the Diary of Mukunda (xi.treasure.onTrade,
// bypass type 2), taking the trade; Kagetora's 296 sets Prog 2, Ayame's 804 Prog 3, Alois' 805 completes it.
// Deliberate difference from upstream Lua (user ruling 2026-11-05, kept bugs): Kagetora's 296 plays whenever the diary
// is held, and the Lua's finish set Prog 2 at any Prog, so at Prog 3 it stepped back to 2 (Ayame's 804 again). The
// finish now moves Prog forward only (to 2 from below 2); 296 still plays at Prog 2 and 3, as a reminder. Each other
// arm reads its own Prog (Ayame 0 / 2, the chest 1, Alois 3).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class FadedPromises : QuestScript
    {
        public FadedPromises() : base(QuestLog.Bastok, "FADED_PROMISES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Faded_Promises";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.Fukuro,
                Title = Title.AssassinReject,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Nin &&
                    q.Player.MainLevel >= 20 &&
                    q.Player.FameLevel(FameArea.Norg) >= 4)
                .Npc(Zone.Metalworks, Npc.Metalworks.Romualdo).OnTrigger(QuestReply.ProgressEvent(802))
                .OnEventFinish(Zone.Metalworks, 802, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Alois).OnTrigger(q =>
                    q.Quest.Step == 3 ? QuestReply.ProgressEvent(805) : null)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(803);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(804);
                    }
                    return null;
                })
                // 803 cancels with a very big option number: only option 0 sets Prog 1
                .OnEventFinish(Zone.Metalworks, 803, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 804, q => q.Quest.SetStep(3))
                .OnEventFinish(Zone.Metalworks, 805, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.DiaryOfMukunda);
                    }
                })
                .Npc(Zone.PalboroughMines, Npc.PalboroughMines.TreasureChest).OnTrade(q =>
                {
                    if (q.Quest.Step == 1 && !q.Player.HasKeyItem(KeyItem.DiaryOfMukunda))
                    {
                        q.Trade.TreasureTrade(2, KeyItem.DiaryOfMukunda);

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Kagetora).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.DiaryOfMukunda) ? QuestReply.ProgressEvent(296) : null)
                .OnEventFinish(Zone.PortBastok, 296, q =>
                {
                    // a deliberate difference from upstream Lua (see the header): Prog never steps back from 3 to 2
                    if (q.Quest.Step < 2)
                    {
                        q.Quest.SetStep(2);
                    }
                });
        }
    }
}
