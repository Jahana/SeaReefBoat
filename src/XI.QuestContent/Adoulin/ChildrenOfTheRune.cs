// Children of the Rune (Log ID 9, Quest ID 119), ported from the quest branches of scripts/zones/Eastern_Adoulin/npcs/
// Octavien.lua and scripts/zones/Yahse_Hunting_Grounds/npcs/Yahse_Wildflower.lua (quest rework B3b prep).
// Octavien : !pos 2.444 -11.439 -16.595 257; Yahse Wildflower : !pos 370.713 0.680 153.281 260. Both scripts call it
// (xi.cs.quest / questEvent); Octavien keeps his standard dialog (27). Its state is the char var RUN_COTR: 1 the offer
// declined, 2 the rune enhancement left unfinished, 3 the reward pending (the inventory was full). As the script has it,
// the triggers on RUN_COTR look at no status, the offer needs ADVANCED_JOB_LEVEL, the update and finishes are keyed on the
// csid (and option) alone, and the reward attempt follows the finish of each of the events he starts (23-29).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class ChildrenOfTheRune : QuestScript
    {
        public ChildrenOfTheRune() : base(QuestLog.Adoulin, "CHILDREN_OF_THE_RUNE") { }

        // RUN_COTR: the offer declined, the rune enhancement left, the reward pending
        private const int Triggered = 1;
        private const int RuneEnhancement = 2;
        private const int RewardPending = 3;

        protected override void Build(QuestScriptBuilder b)
        {
            // 1: after the quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(28));

            // 2: the reward pending (the inventory was full)
            b.AnyState(when: q => q.Player.CharVar("RUN_COTR") == RewardPending)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(29));

            // 3: the rune enhancement left unfinished: straight to it
            b.AnyState(when: q => q.Player.CharVar("RUN_COTR") == RuneEnhancement)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(26, 1));

            // 4: the petal brought: the rune enhancement
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.YahseWildflowerPetal))
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(26));

            // 5: the petal asked for
            b.State(QuestStatus.Accepted)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(25));

            // 6: the offer again, once declined
            b.AnyState(when: q => q.Player.CharVar("RUN_COTR") == Triggered)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(24));

            // 7: the offer, from the advanced job level
            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Octavien).Called().OnTrigger(QuestReply.Event(23));

            // 8: the Yahse Wildflower: the petal, as often as asked
            b.State(QuestStatus.Accepted)
                .Npc(Zone.YahseHuntingGrounds, Npc.YahseHuntingGrounds.YahseWildflower).Called().OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YahseWildflowerPetal);
                    return QuestReply.NoAction();
                });

            // 9: the update and the finishes, keyed on the csid (and option) alone as the script's are; each finish ends
            // with the reward attempt
            b.AnyState()
                // the rune enhancement: HP and MP halved (rounded up), each unless 5 or less
                .OnEventUpdate(Zone.EasternAdoulin, 26, q =>
                {
                    if (q.Option == 1)
                    {
                        int hp = q.Player.HP;
                        if (hp > 5) q.Player.SetHP((hp + 1) / 2);
                        int mp = q.Player.MP;
                        if (mp > 5) q.Player.SetMP((mp + 1) / 2);
                    }
                })
                // the offer: declined (0) or accepted (1)
                .OnEventFinish(Zone.EasternAdoulin, 23, q =>
                {
                    Offer(q);
                    TryReward(q);
                })
                .OnEventFinish(Zone.EasternAdoulin, 24, q =>
                {
                    Offer(q);
                    TryReward(q);
                })
                // the rune enhancement: left (0) or finished (1)
                .OnEventFinish(Zone.EasternAdoulin, 26, q =>
                {
                    if (q.Option == 0) q.Player.SetCharVar("RUN_COTR", RuneEnhancement);
                    else if (q.Option == 1) q.Player.SetCharVar("RUN_COTR", RewardPending);
                    TryReward(q);
                })
                .OnEventFinish(Zone.EasternAdoulin, 25, TryReward)
                .OnEventFinish(Zone.EasternAdoulin, 27, TryReward)
                .OnEventFinish(Zone.EasternAdoulin, 28, TryReward)
                .OnEventFinish(Zone.EasternAdoulin, 29, TryReward);
        }

        private static void Offer(IQuestContext q)
        {
            if (q.Option == 0) q.Player.SetCharVar("RUN_COTR", Triggered);
            else if (q.Option == 1) q.Quest.Begin();
        }

        // the reward when pending and the Sowilo Claymore fits: rune fencer, the gesture, the petal taken, completed with
        // player:completeQuest (no reward table)
        private static void TryReward(IQuestContext q)
        {
            if (q.Player.CharVar("RUN_COTR") != RewardPending) return;
            if (!q.Player.GiveItem(Item.SowiloClaymore)) return;
            q.Player.UnlockJob(Job.Run);
            q.Text.Special(Text.EasternAdoulin.YOU_CAN_NOW_BECOME, 1);
            q.Player.GiveKeyItem(KeyItem.JobGestureRuneFencer);
            q.Player.SetCharVar("RUN_COTR", 0);
            q.Player.DelKeyItem(KeyItem.YahseWildflowerPetal);
            // the raw player:completeQuest, which keeps the quest's own Quest[9][119] vars
            q.Player.CompleteQuest(QuestLog.Adoulin, "CHILDREN_OF_THE_RUNE");
        }
    }
}
