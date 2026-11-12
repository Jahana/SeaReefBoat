// Darkness Named (Promathia 3-5, Log ID 6, Mission ID 358), a line-for-line port of
// scripts/missions/cop/3_5_Darkness_Named.lua (quest rework C3). Door: Neptune's Spire !pos 35 0 -15 245; Monberaux
// !pos -42 0 -2 244. Section 1 (the mission current): Lower Jeuno's Aldo (Status 0) plays 7, Harnek (0) 13 and
// Sattal-Mansal (0) 11; Ghebi Damomohe (1) plays 54, whose finish sets 2, and (2) plays 53; a Carmine, Cyan or Gray
// Chip traded at 2 without the Psoxja Pass plays 52 (the gil reward baked into it), whose finish takes the chip, gives
// the gil and the Pass and sets 3. Upper Jeuno's Monberaux (0) plays 82, whose finish takes the Mysterious Amulet, says
// LEND_PRISHE_AMULET and sets 1; (1) plays 3 once; (5) plays 75, whose finish completes the mission. The Shrouded
// Maw's zone-in (3) plays 2, whose finish sets 4; the 32001 finish sets 5 at 4 when the win was Darkness Named
// (Battlefields/TheShroudedMaw/DarknessNamed.cs sets battlefieldWin). Section 2 (3-5 completed): Tavnazian Safehold's
// Chemioue (282), Justinius (129) and Parelbriaux (296) play their events replacing the default, Arquil (292) and
// Despachiaire (315) too while Slanderous Utterings is not completed. Sheltering Doubt is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class DarknessNamed : MissionScript
    {
        public DarknessNamed() : base(XI.Quests.MissionLog.CoP, "DARKNESS_NAMED") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/3_5_Darkness_Named";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.ShelteringDoubt,
            });

            b.State(current: true)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.Event(7).ImportantEvent() : null)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.GhebiDamomohe).OnTrade(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PsoxjaPass) &&
                        q.Mission.Var("Status") == 2 &&
                        (q.Trade.Matches((Item.CarmineChip, 1)) ||
                         q.Trade.Matches((Item.CyanChip, 1)) ||
                         q.Trade.Matches((Item.GrayChip, 1))))
                    {
                        // ToDo Uncaptured CS 51 seems to imply you traded the wrong item
                        return QuestReply.ProgressEvent(52, GilReward(q));
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(54);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(53).ImportantEvent();
                    }

                    return null;
                })
                // Some of Harnek's events duplicate dialogue but with the Neptune's Spire door first opening
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Harnek).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.Event(13).ImportantEvent() : null)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.SattalMansal).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.Event(11).ImportantEvent() : null)
                .OnEventFinish(Zone.LowerJeuno, 52, q =>
                {
                    q.Trade.Complete();

                    // Silent since the gil reward is baked into the CS
                    q.Player.AddGil(GilReward(q));
                    q.Player.GiveKeyItem(KeyItem.PsoxjaPass);
                    q.Mission.SetVar("Status", 3);
                })
                .OnEventFinish(Zone.LowerJeuno, 54, q => q.Mission.SetVar("Status", 2))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(82);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(3).ImportantOnce();
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(75);
                    }

                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 75, q => q.Mission.Complete())
                .OnEventFinish(Zone.UpperJeuno, 82, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MysteriousAmulet);
                    q.Text.Special(Text.UpperJeuno.LEND_PRISHE_AMULET, KeyItem.MysteriousAmulet);
                    q.Mission.SetVar("Status", 1);
                })
                .OnZoneIn(Zone.TheShroudedMaw, q => q.Mission.Var("Status") == 3 ? QuestReply.Event(2) : null)
                .OnEventFinish(Zone.TheShroudedMaw, 2, q => q.Mission.SetVar("Status", 4))
                .OnEventFinish(Zone.TheShroudedMaw, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.DarknessNamed &&
                        q.Mission.Var("Status") == 4)
                    {
                        q.Mission.SetVar("Status", 5);
                    }
                });

            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DarknessNamed))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Chemioue)
                    .OnTrigger(QuestReply.Event(282).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius)
                    .OnTrigger(QuestReply.Event(129).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Parelbriaux)
                    .OnTrigger(QuestReply.Event(296).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Arquil).OnTrigger(q =>
                    !q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.SlanderousUtterings)
                        ? QuestReply.Event(292).ReplaceDefault()
                        : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                    !q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.SlanderousUtterings)
                        ? QuestReply.Event(315).ReplaceDefault()
                        : null);
        }

        // xi.settings.main.GIL_RATE * 500, a number the cutscene and the gil both take whole (the engine's llround)
        private static int GilReward(IQuestContext q) =>
            (int)System.Math.Round(q.Settings.MainNumber("GIL_RATE") * 500, System.MidpointRounding.AwayFromZero);
    }
}
