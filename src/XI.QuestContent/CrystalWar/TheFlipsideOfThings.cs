// The Flipside of Things (Log ID 7, Quest ID 11), a line-for-line port of
// scripts/quests/crystalWar/The_Flipside_of_Things.lua (quest rework B3b Crystal War group 11). Garlaige Citadel [S]:
// Rarcasmeault !pos 146.175 -6 93.04 (no script, no default), _qm2 !pos -69.588 -1.415 57.695 (default 5).
// Available: Rarcasmeault 6, whose finish on option 1 begins it. Accepted: Rarcasmeault: the Firepower Case: 9, whose
// finish completes it (2000 exp, 2000 gil, the Map of Vunkerl Inlet) and takes the case; else 8 (an Event). _qm2: no
// case: 7, whose finish gives the case; else nil (5). Completed: Rarcasmeault 4 (an Event, replaceDefault).
// Kept as the Lua has it: 9's finish takes the case whether or not the completion did (a key item reward cannot fail).
// Rival containers: none. Rarcasmeault and _qm2 have no script and no other quest line: the reply kinds are pinned in
// tests/XI.Verify.Quests/CrystalWarGroup11.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheFlipsideOfThings : QuestScript
    {
        public TheFlipsideOfThings() : base(QuestLog.CrystalWar, "THE_FLIPSIDE_OF_THINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/The_Flipside_of_Things";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfVunkerlInlet,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Rarcasmeault).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(6);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 6, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Rarcasmeault).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FirepowerCase))
                    {
                        return QuestReply.ProgressEvent(9);
                    }
                    else
                    {
                        return QuestReply.Event(8); // This is their default action until the quest is completed.
                    }
                })
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.FirepowerCase))
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 7, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FirepowerCase);
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 9, q =>
                {
                    q.Quest.Complete();
                    q.Player.DelKeyItem(KeyItem.FirepowerCase);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Rarcasmeault)
                .OnTrigger(QuestReply.Event(4).ReplaceDefault());
        }
    }
}
