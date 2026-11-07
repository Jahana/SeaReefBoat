// Star Struck (Log ID 2, Quest ID 10), a line-for-line port of scripts/quests/windurst/Star_Struck.lua (quest rework
// B3b). Koru-Moru : !pos -120 -6 124 239. Holding a Torn Epistle, Koru-Moru's 197 offers it (option 0: begun).
// Accepted: Koru-Moru's 198, Luuh Koplehn's 200; the Torn Epistle traded alone plays 199 (taken, 50 gil, every time); a
// Meteorite traded alone plays 211, which completes the quest (fame 20, the Compound Eye Circlet), takes the meteorite
// and sets Blast from the Past's mustZone. Completed while The Puppet Master is not: Koru-Moru's 213 (replaceDefault).
// Kept as the Lua file has it: the offer does not take the epistle, and 199 pays for every epistle traded while the
// quest is accepted.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class StarStruck : QuestScript
    {
        public StarStruck() : base(QuestLog.Windurst, "STAR_STRUCK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Star_Struck";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.CompoundEyeCirclet });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.TornEpistle))
                    {
                        return QuestReply.ProgressEvent(197);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 197, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.TornEpistle))
                        {
                            return QuestReply.ProgressEvent(199);
                        }
                        else if (q.Trade.HasExactly(Item.Meteorite))
                        {
                            return QuestReply.ProgressEvent(211);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.Event(198))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.LuuhKoplehn).OnTrigger(QuestReply.Event(200))
                .OnEventFinish(Zone.WindurstWalls, 199, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveCurrency("gil", 50);
                })
                .OnEventFinish(Zone.WindurstWalls, 211, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        q.Player.SetQuestMustZone(QuestLog.Windurst, "BLAST_FROM_THE_PAST");
                    }
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Windurst, "THE_PUPPET_MASTER"))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrigger(QuestReply.Event(213).ReplaceDefault());
        }
    }
}
