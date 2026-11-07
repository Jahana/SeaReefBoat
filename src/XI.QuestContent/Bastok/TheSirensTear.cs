// The Siren's Tear (Log ID 1, Quest ID 0), a line-for-line port of scripts/quests/bastok/The_Sirens_Tear.lua (quest rework
// B3b). Wahid : !pos 26.305 -1 -66.403 234; Otto : !pos -145.929 -7.48 13.701 236; Carmelo : !pos -146.476 -7.48 -10.889
// 236; Echo Hawk : !pos -0.965 5.999 -15.567 234; qm1 (moves) : !pos 309.6 2.6 324 106. Wahid's 81 begins it (Echo Hawk
// plays 5 while it is available); Otto's 5 at Prog 0 and Carmelo's 6 (Prog 1) are flavour; a Siren's Tear traded to Wahid
// plays 82, which completes it (150 gil, fame +10, Tearjerker) and takes the tear. Once completed Carmelo's 19 (Prog under
// 2, no tear held) sets Prog 2. The tear comes from North Gustaberg's qm1, whose own script gives it (no quest read).
// Love and Ice (C#) reads its completion.
// Kept as the Lua file has it: the last section matches accepted and completed, so each tear traded after completion
// plays 82 again and quest:complete pays the reward again (the Lua comment says CLuaBaseEntity::completeQuest completes
// it only once; the reward is npcUtil.completeQuest's); Carmelo's 6 can be replayed at any Prog.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheSirensTear : QuestScript
    {
        public TheSirensTear() : base(QuestLog.Bastok, "THE_SIRENS_TEAR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Sirens_Tear";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 150, Title = Title.Tearjerker });

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMines, Npc.BastokMines.Wahid).OnTrigger(QuestReply.ProgressEvent(81))
                .Npc(Zone.BastokMines, Npc.BastokMines.EchoHawk).OnTrigger(QuestReply.Event(5))
                .OnEventFinish(Zone.BastokMines, 81, q => q.Quest.Begin());

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Otto).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.ProgressEvent(5) : null)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(QuestReply.ProgressEvent(6))
                .OnEventFinish(Zone.PortBastok, 6, q => q.Quest.SetStep(1));

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(q =>
                    q.Quest.Step < 2 && !q.Player.FindItem(Item.SirensTear) ? QuestReply.ProgressEvent(19) : null)
                .OnEventFinish(Zone.PortBastok, 19, q => q.Quest.SetStep(2));

            // Section accepted or completed (status ~= QUEST_AVAILABLE)
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Wahid).OnTrade(q =>
                    q.Trade.Matches((Item.SirensTear, 1)) ? QuestReply.ProgressEvent(82) : null)
                .OnEventFinish(Zone.BastokMines, 82, q =>
                {
                    // This is called even after the initial complete to handle quest var cleanup
                    // CLuaBaseEntity::completeQuest() will only actually complete the quest the
                    // first time the player finishes this.
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
