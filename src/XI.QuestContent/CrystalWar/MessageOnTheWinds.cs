// Message on the Winds (Log ID 7, Quest ID 1), a line-for-line port of
// scripts/quests/crystalWar/Message_on_the_Winds.lua (quest rework B3b Crystal War group 11). Metalworks: Romualdo !pos
// 135.035 -18.475 -37.419 (no script; default 705). Batallia Downs [S]: Romualdo !pos 54.304 -13.836 141.055 (default
// 102). Grauberg [S]: Childerich !pos -312.844 16.23 -514.973 (default 10), qm3 !pos 439.398 -40.221 79.386 (default
// NOTHING_OUT_OF_ORDINARY).
// Available, level 20+: Metalworks Romualdo: 952, whose finish sets Prog 1 (the quest not begun). Prog 1, any status:
// Batallia Downs [S] Romualdo: 109, whose finish begins it and sets Prog 2. Accepted: Prog 2: Romualdo [S] 110 (no
// finish); Childerich 1 (-> Prog 3). Prog 3: Childerich 2; qm3 3 (-> Prog 4). Prog 4: Childerich 4 (-> Prog 5). Prog 5:
// Childerich 5; Romualdo [S] 111, whose finish completes it (fame 20 Bastok, the Smart Grenade) and then sets PostCS 1.
// Completed, PostCS 1: Metalworks Romualdo 953 (-> PostCS 2). PostCS 2: Childerich 6, whose finish sets PostCS 0 and
// the title Windtalker. Completed: Romualdo [S] 112 (an Event, replaceDefault); with Windtalker: Childerich 7 (the
// same).
// Kept as the Lua has it: 952 answers again at Prog 1 while available; the Prog 1 section has no status check (109
// plays at Prog 1 while completed too, and its begin leaves the completed status); PostCS is written after the
// completion's cleanup; 110 / 2 / 5 have no finish; PostCS 0 is written, not deleted.
// Rival containers: the Metalworks' Romualdo also answers Stamp Hunt (726, Progress, while its bit 4 is unset) and
// Faded Promises (802, Progress, a NIN with Norg fame 4): 952 and 953 tie with them and rotate by the last tied event
// (golden flow #rivals, with Stamp Hunt). Batallia Downs [S]'s Romualdo, Childerich and qm3 have no script and no other
// quest line: their reply kinds are pinned in tests/XI.Verify.Quests/CrystalWarGroup11.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class MessageOnTheWinds : QuestScript
    {
        public MessageOnTheWinds() : base(QuestLog.CrystalWar, "MESSAGE_ON_THE_WINDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Message_on_the_Winds";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.SmartGrenade,
            });

            // Section: Talk to Romualdo at the Cannonry in the Metalworks (second floor, K-9).
            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= 20)
                .Npc(Zone.Metalworks, Npc.Metalworks.Romualdo).OnTrigger(QuestReply.ProgressEvent(952))
                .OnEventFinish(Zone.Metalworks, 952, q =>
                {
                    q.Quest.SetStep(1); // Quest doesn't appear in log yet
                });

            // Section: Travel to the past and speak to him again, this time in Batallia Downs (S) (I-7).
            b.AnyState(step: 1)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Romualdo).OnTrigger(QuestReply.ProgressEvent(109))
                .OnEventFinish(Zone.BatalliaDownsS, 109, q =>
                {
                    q.Quest.Begin(); // Quest now appears in log
                    q.Quest.SetStep(2);
                });

            // Section: Speak with Childerich in Grauberg (S) at the west side of the house at (E-12).
            b.State(QuestStatus.Accepted, step: 2)
                // Optional dialogue
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Romualdo).OnTrigger(QuestReply.ProgressEvent(110))
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.ProgressEvent(1))
                .OnEventFinish(Zone.GraubergS, 1, q =>
                {
                    q.Quest.SetStep(3);
                });

            // Check the ??? at J-9.
            b.State(QuestStatus.Accepted, step: 3)
                // Optional dialogue
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.ProgressEvent(2))
                .Npc(Zone.GraubergS, Npc.GraubergS.qm3).OnTrigger(QuestReply.ProgressEvent(3))
                .OnEventFinish(Zone.GraubergS, 3, q =>
                {
                    q.Quest.SetStep(4);
                });

            // Speak with Childerich.
            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.ProgressEvent(4))
                .OnEventFinish(Zone.GraubergS, 4, q =>
                {
                    q.Quest.SetStep(5);
                });

            // New default text for Childerich
            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.ProgressEvent(5))
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Romualdo).OnTrigger(QuestReply.ProgressEvent(111))
                .OnEventFinish(Zone.BatalliaDownsS, 111, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("PostCS", 1);
                    }
                });

            // Section: Post quest cutscene with Romualdo
            b.State(QuestStatus.Completed, when: q => q.Quest.Var("PostCS") == 1)
                .Npc(Zone.Metalworks, Npc.Metalworks.Romualdo).OnTrigger(QuestReply.ProgressEvent(953))
                .OnEventFinish(Zone.Metalworks, 953, q =>
                {
                    q.Quest.SetVar("PostCS", 2);
                });

            // Post quest cutscene with Childerich
            b.State(QuestStatus.Completed, when: q => q.Quest.Var("PostCS") == 2)
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.ProgressEvent(6))
                .OnEventFinish(Zone.GraubergS, 6, q =>
                {
                    q.Quest.SetVar("PostCS", 0);
                    q.Player.AddTitle(Title.Windtalker);
                });

            // New default text for Romualdo (S)
            b.State(QuestStatus.Completed)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Romualdo)
                .OnTrigger(QuestReply.Event(112).ReplaceDefault());

            // New default text for Childerich
            b.State(QuestStatus.Completed, when: q => q.Player.HasTitle(Title.Windtalker))
                .Npc(Zone.GraubergS, Npc.GraubergS.Childerich).OnTrigger(QuestReply.Event(7).ReplaceDefault());
        }
    }
}
