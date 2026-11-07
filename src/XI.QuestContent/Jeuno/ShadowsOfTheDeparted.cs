// Shadows of the Departed (Log ID 3, Quest ID 88), a line-for-line port of scripts/quests/jeuno/Shadows_of_the_Departed.lua
// (quest rework B3b). _0id : !pos 220 -2.5 340 18; _0gc : !pos 100.005 -3.028 -140.005 16; _0k0 : !pos 260 -2.5
// 180 20. Available once Storms of Fate is completed, with Zilart mission Awakening current at mission status 3 and the
// day past the Timer Storms of Fate set: Ru'Lude Gardens' trigger area 1 plays 161 (the Note Written by Eshantarl,
// begun). Accepted: the Promyvion-Dem, -Holla and -Mea crystals (_0id, _0gc, _0k0) each give their sliver when it is not
// held; area 1 with the three slivers (PROMYVION_HOLLA_SLIVER to + 2) plays 162: completed, the slivers taken,
// YOU_HAND_THE_THREE_SLIVERS, Apocalypse Nigh's Timer = VanadielUniqueDay() + 1 and its mustZone set.
// Kept Lua quirks: the slivers are read and taken by id from PROMYVION_HOLLA_SLIVER (Holla, Dem, Mea in id order); the
// Note Written by Eshantarl is never taken.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ShadowsOfTheDeparted : QuestScript
    {
        public ShadowsOfTheDeparted() : base(QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Shadows_of_the_Departed";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "STORMS_OF_FATE") &&
                    q.Player.CurrentMission(MissionLog.Zilart) == Mission.Zilart.Awakening &&
                    q.Player.MissionStatus(MissionLog.Zilart) == 3 &&
                    q.Player.VanadielUniqueDay >= q.Quest.Var("Timer"))
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    return QuestReply.ProgressEvent(161);
                })
                .OnEventFinish(Zone.RuludeGardens, 161, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.NoteWrittenByEshantarl);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PromyvionDem, Npc.PromyvionDem._0id).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PromyvionDemSliver))
                    {
                        return QuestReply.KeyItem(KeyItem.PromyvionDemSliver);
                    }
                    return null;
                })
                .Npc(Zone.PromyvionHolla, Npc.PromyvionHolla._0gc).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PromyvionHollaSliver))
                    {
                        return QuestReply.KeyItem(KeyItem.PromyvionHollaSliver);
                    }
                    return null;
                })
                .Npc(Zone.PromyvionMea, Npc.PromyvionMea._0k0).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PromyvionMeaSliver))
                    {
                        return QuestReply.KeyItem(KeyItem.PromyvionMeaSliver);
                    }
                    return null;
                })
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    for (int keyItemId = KeyItem.PromyvionHollaSliver; keyItemId <= KeyItem.PromyvionHollaSliver + 2; keyItemId++)
                    {
                        if (!q.Player.HasKeyItem(keyItemId))
                        {
                            return null;
                        }
                    }

                    return QuestReply.ProgressEvent(162);
                })
                .OnEventFinish(Zone.RuludeGardens, 162, q =>
                {
                    if (q.Quest.Complete())
                    {
                        for (int keyItemId = KeyItem.PromyvionHollaSliver; keyItemId <= KeyItem.PromyvionHollaSliver + 2; keyItemId++)
                        {
                            q.Player.DelKeyItem(keyItemId);
                        }

                        q.Text.Special(Text.RuludeGardens.YOU_HAND_THE_THREE_SLIVERS);

                        q.Player.SetQuestVar(QuestLog.Jeuno, "APOCALYPSE_NIGH", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.Jeuno, "APOCALYPSE_NIGH");
                    }
                });
        }
    }
}
