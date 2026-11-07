// Hide and Go Peak (Log ID 9, Quest ID 57), a line-for-line port of scripts/quests/adoulin/Hide_and_Go_Peak.lua (quest
// rework B3b Adoulin group 3). Marjami Ravine: Toppled Tree !pos 356.272 -59.701 149.99 266 (no script, no default),
// Velkk Cache !pos 278.126 -0.378 -80.672 266 (no script; default LEATHER_SCRAPS_STREWN), the 24 Scalable Areas
// Scalable_Area_0 .. 23 (no script; default SHARP_THORNS_PROTRUDE). The Scalable Areas of Cirdas Caverns, Dho Gates,
// Moh Gates, Morimar Basalt Fields, Sih Gates and Woh Gates (no script; the same default).
// Available: Toppled Tree: 3, whose finish (any option) begins it.
// Accepted: Toppled Tree: Prog 0 4 (an Event), Prog 1 5, Prog 2 6 (an Event), else nothing; Velkk Cache, without the
// Large Strip of Velkk Hide at Prog 0: LEATHER_SCRAPS_STREWN, Prog 1, then the Large Strip (a key item action), else
// nothing; Scalable_Area_2 at Prog 2: 7, else nothing. 5's finish takes the strip, sets Prog 2 and shows
// KEYITEM_LOST; 7's gives the Pair of Velkk Gloves (npcUtil.giveKeyItem), adds Climbing (no message), shows
// YOU_HAVE_LEARNED Climbing and completes it (500 bayld, the Adoulin fame area).
// Completed: each Scalable Area of the seven zones: scalableAreaWarp, an Event: the zone's base (Cirdas 17, Dho 10,
// Marjami 14, Moh 12, Morimar 40, Sih 17, Woh 200) plus the pad's index (npc id - SCALABLE_AREA_OFFSET), with
// option [5] the index + 1.
// Kept as the Lua has it: the offer begins on any option; the key items come before the completion (the Lua's note:
// the reward holds one key item at most); the completion's result is not read; the Velkk Cache's message and Prog
// write run inside its trigger, before the key item action.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class HideAndGoPeak : QuestScript
    {
        public HideAndGoPeak() : base(QuestLog.Adoulin, "HIDE_AND_GO_PEAK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Hide_and_Go_Peak";

        // Scalable Area climbs, available once the player has earned Climbing + the Pair of Velkk Gloves and completed
        // the quest (the zone's eventBase).
        private static readonly Dictionary<int, int> ScalableData = new Dictionary<int, int>
        {
            [Zone.CirdasCaverns] = 17,
            [Zone.DhoGates] = 10,
            [Zone.MarjamiRavine] = 14,
            [Zone.MohGates] = 12,
            [Zone.MorimarBasaltFields] = 40,
            [Zone.SihGates] = 17,
            [Zone.WohGates] = 200,
        };

        private static QuestReply ScalableAreaWarp(IQuestContext q)
        {
            int zoneId = q.Zone;
            int eventBase = ScalableData[zoneId];
            // npc:getID() - zones[zoneId].npc.SCALABLE_AREA_OFFSET
            int padIndex = q.Npc.Id - q.Player.NpcIdOf("SCALABLE_AREA_OFFSET");

            return QuestReply.Event(eventBase + padIndex).Option(5, padIndex + 1);
        }

        // Marjami's 24 pads (the Lua's 'Scalable_Area_' .. i for i = 0, 23)
        private static readonly string[] MarjamiScalablePads =
        {
            Npc.MarjamiRavine.ScalableArea0, Npc.MarjamiRavine.ScalableArea1, Npc.MarjamiRavine.ScalableArea2,
            Npc.MarjamiRavine.ScalableArea3, Npc.MarjamiRavine.ScalableArea4, Npc.MarjamiRavine.ScalableArea5,
            Npc.MarjamiRavine.ScalableArea6, Npc.MarjamiRavine.ScalableArea7, Npc.MarjamiRavine.ScalableArea8,
            Npc.MarjamiRavine.ScalableArea9, Npc.MarjamiRavine.ScalableArea10, Npc.MarjamiRavine.ScalableArea11,
            Npc.MarjamiRavine.ScalableArea12, Npc.MarjamiRavine.ScalableArea13, Npc.MarjamiRavine.ScalableArea14,
            Npc.MarjamiRavine.ScalableArea15, Npc.MarjamiRavine.ScalableArea16, Npc.MarjamiRavine.ScalableArea17,
            Npc.MarjamiRavine.ScalableArea18, Npc.MarjamiRavine.ScalableArea19, Npc.MarjamiRavine.ScalableArea20,
            Npc.MarjamiRavine.ScalableArea21, Npc.MarjamiRavine.ScalableArea22, Npc.MarjamiRavine.ScalableArea23,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            // Note: There are multiple KI rewards here, and A Pair of Velkk Gloves is added prior
            // to the quest complete to cover this.
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
            });

            // the Prog values the Velkk Cache's trigger and 5's finish set
            b.Steps(1, 2);

            b.State(QuestStatus.Available)
                .Npc(Zone.MarjamiRavine, Npc.MarjamiRavine.ToppledTree).OnTrigger(QuestReply.ProgressEvent(3))
                .OnEventFinish(Zone.MarjamiRavine, 3, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.MarjamiRavine, Npc.MarjamiRavine.ToppledTree).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(4);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(6);
                    }

                    return null;
                })
                .Npc(Zone.MarjamiRavine, Npc.MarjamiRavine.ScalableArea2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(7);
                    }

                    return null;
                })
                .Npc(Zone.MarjamiRavine, Npc.MarjamiRavine.VelkkCache).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LargeStripOfVelkkHide) &&
                        q.Quest.Step == 0)
                    {
                        q.Text.Special(Text.MarjamiRavine.LEATHER_SCRAPS_STREWN);
                        q.Quest.SetStep(1);

                        return QuestReply.KeyItem(KeyItem.LargeStripOfVelkkHide);
                    }

                    return null;
                })
                .OnEventFinish(Zone.MarjamiRavine, 5, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LargeStripOfVelkkHide);
                    q.Quest.SetStep(2);

                    q.Text.Special(Text.MarjamiRavine.KEYITEM_LOST, KeyItem.LargeStripOfVelkkHide);
                })
                .OnEventFinish(Zone.MarjamiRavine, 7, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PairOfVelkkGloves);
                    q.Player.AddKeyItem(KeyItem.Climbing);
                    q.Text.Special(Text.MarjamiRavine.YOU_HAVE_LEARNED, KeyItem.Climbing);
                    q.Quest.Complete();
                });

            var completed = b.State(QuestStatus.Completed);
            completed.Npc(Zone.CirdasCaverns, Npc.CirdasCaverns.ScalableArea).OnTrigger(ScalableAreaWarp);
            completed.Npc(Zone.DhoGates, Npc.DhoGates.ScalableArea).OnTrigger(ScalableAreaWarp);
            foreach (string pad in MarjamiScalablePads)
            {
                completed.Npc(Zone.MarjamiRavine, pad).OnTrigger(ScalableAreaWarp);
            }
            completed.Npc(Zone.MohGates, Npc.MohGates.ScalableArea).OnTrigger(ScalableAreaWarp);
            completed.Npc(Zone.MorimarBasaltFields, Npc.MorimarBasaltFields.ScalableArea).OnTrigger(ScalableAreaWarp);
            completed.Npc(Zone.SihGates, Npc.SihGates.ScalableArea).OnTrigger(ScalableAreaWarp);
            completed.Npc(Zone.WohGates, Npc.WohGates.ScalableArea).OnTrigger(ScalableAreaWarp);
        }
    }
}
