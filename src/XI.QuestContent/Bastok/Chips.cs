// Chips (Log ID 1, Quest ID 82), a line-for-line port of scripts/quests/bastok/Chips.lua (quest rework B3b). Ghebi Damomohe
// : !pos 15.535 -0.111 -7.603 245; Cid : !pos -12 -12 1 237. Ghebi Damomohe's 169 (the quest's mustZone clear, and CoP
// One to be Feared completed, or current with its Status at 1 or more) begins it on option 0, or sets the mustZone on
// option 1; a Carmine, a Cyan and a Gray Chip traded to Cid play 883 (884 once completed), which completes it (CCB
// Polymer) and gives San d'Oria and Bastok fame +5 each; with no free slot, or a CCB Polymer held, the trade shows
// ITEM_CANNOT_BE_OBTAINED instead.
// Kept as the Lua file has it: repeatable through Cid's 884; the refusal returns player:messageSpecial's own return
// (nothing), so the lookup sees no action.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Chips : QuestScript
    {
        public Chips() : base(QuestLog.Bastok, "CHIPS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Chips";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.CcbPolymer });

            b.State(QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.GhebiDamomohe).OnTrigger(q =>
                {
                    if (!q.Quest.MustZone &&
                        (q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.OneToBeFeared) ||
                         (q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.OneToBeFeared &&
                          q.Player.MissionVar(MissionLog.Cop, Mission.Cop.OneToBeFeared, "Status") >= 1)))
                    {
                        return QuestReply.ProgressEvent(169);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 169, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else if (q.Option == 1)
                    {
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrade(q => ChipsTrade(q, 883))
                .OnEventFinish(Zone.Metalworks, 883, Finish);

            b.State(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrade(q => ChipsTrade(q, 884))
                .OnEventFinish(Zone.Metalworks, 884, Finish);
        }

        // both sections' Cid onTrade, but for the event
        static QuestReply ChipsTrade(IQuestContext q, int csid)
        {
            if (q.Trade.Matches((Item.CarmineChip, 1), (Item.CyanChip, 1), (Item.GrayChip, 1)))
            {
                if (q.Player.FreeSlotCount == 0 || q.Player.HasItem(Item.CcbPolymer))
                {
                    // return player:messageSpecial(...): the message, and nil to the lookup
                    q.Text.Special(Text.Metalworks.ITEM_CANNOT_BE_OBTAINED, Item.CcbPolymer);
                    return null;
                }
                else
                {
                    return QuestReply.ProgressEvent(csid);
                }
            }
            return null;
        }

        // both sections' finish (883 / 884)
        static void Finish(IQuestContext q)
        {
            if (q.Quest.Complete())
            {
                q.Trade.Complete();
                q.Player.AddFame(FameArea.Sandoria, 5);
                q.Player.AddFame(FameArea.Bastok, 5);
            }
        }
    }
}
