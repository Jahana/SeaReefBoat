// The Gustaberg Tour (Log ID 1, Quest ID 45), a line-for-line port of scripts/quests/bastok/The_Gustaberg_Tour.lua
// (quest rework B3b). Izabele : !pos -43.9 -10 -2.4 237; Hunting Bear : !pos -235.7 40 424.5 106. Hunting Bear plays 22
// (the completion) only for a party of two or more whose alliance members are all at level 15 or below and within 15
// yalms; otherwise 21. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheGustabergTour : QuestScript
    {
        public TheGustabergTour() : base(QuestLog.Bastok, "THE_GUSTABERG_TOUR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Gustaberg_Tour";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Gil = 500, Title = Title.GustabergTourist });

            b.State(QuestStatus.Available)
                .Npc(Zone.Metalworks, Npc.Metalworks.Izabele).OnTrigger(QuestReply.ProgressEvent(745))
                .OnEventFinish(Zone.Metalworks, 745, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Izabele).OnTrigger(QuestReply.ProgressEvent(746))
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.HuntingBear).OnTrigger(q =>
                {
                    bool flag = true;

                    foreach (var member in q.Player.Alliance)
                    {
                        if (member.MainLevel > 15 || member.Distance > 15)
                        {
                            flag = false;
                        }
                    }

                    return flag && q.Player.PartySize > 1 ? QuestReply.ProgressEvent(22) : QuestReply.ProgressEvent(21);
                })
                .OnEventFinish(Zone.NorthGustaberg, 22, q => q.Quest.Complete());

            b.State(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.Izabele).OnTrigger(QuestReply.Event(747).ReplaceDefault())
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.HuntingBear).OnTrigger(QuestReply.Event(23).ReplaceDefault());
        }
    }
}
