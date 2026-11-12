// Below the Arks (Promathia 1-2, Log ID 6, Mission ID 118), a line-for-line port of
// scripts/missions/cop/1_2_Below_the_Arks.lua (quest rework C3). Enter the Hall of Transference by the Shattered
// Telepoints (Konschtat !pos 135 19 220 108, La Theine !pos 334 19 -60 102, Tahrongi !pos 179 35 255 117); Pherimociel
// !pos -31.627 1.002 67.956 243. One section (the mission current): Rude's Pherimociel plays 24 at Status 0 (its finish
// sets Status 1) and 25 at Status 1 (replacing the default); High_Wind 33 and Rainhard 34 at Status 1; Upper Jeuno's
// Monberaux 9 at Status 0. The telepoints (zone helpers in PromyvionHelpers) answer only at Status 1; the Hall's gates
// _0e0 to _0e2 and apparatuses _0e3, _0e5 and _0e7 are the helpers'; the Hall's zone-in always plays 108; 122, 125 and
// 128 are the apparatus finishes; 160 sends the char to the Promyvion of the apparatus's crag (named by the NPC, the
// Option left for the zone-in); a Promyvion's 50 sets Option to its
// crag; a Spire's 32001 is the helpers' spireEventFinish.
// No title; The Mothercrystals is next. Kept as the Lua has it: the telepoint's guard is Status == 1 (the helper's own
// first-entry flag reads Below the Arks as current); Tahrongi's MEMORIES_SEALED_OFF is missing, so its 918 raises the
// Lua's error after clearing Option; Pherimociel's 25 and the gates' door text (priority 1000) replace the defaults.
using XI.Quests.Authoring;
using H = XI.QuestContent.Missions.Cop.PromyvionHelpers;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class BelowTheArks : MissionScript
    {
        public BelowTheArks() : base(XI.Quests.MissionLog.CoP, "BELOW_THE_ARKS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/1_2_Below_the_Arks";

        static QuestReply Telepoint(IQuestContext q, int zone) =>
            q.Mission.Var("Status") == 1 ? H.TelepointOnTrigger(q, zone) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheMothercrystals,
            });

            b.State(current: true)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");
                    if (status == 0) return QuestReply.ProgressEvent(24);
                    if (status == 1) return QuestReply.Event(25).ReplaceDefault();
                    return null;
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.HighWind).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(33) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Rainhard).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(34) : null)
                .OnEventFinish(Zone.RuludeGardens, 24, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.Event(9) : null)

                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.KonschtatHighlands))
                .OnEventFinish(Zone.KonschtatHighlands, 913, q => H.TelepointEntry(q, Zone.KonschtatHighlands))
                .OnEventFinish(Zone.KonschtatHighlands, 918,
                    q => H.TelepointSealMemory(q, Zone.KonschtatHighlands))

                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.LaTheinePlateau))
                .OnEventFinish(Zone.LaTheinePlateau, 202, q => H.TelepointEntry(q, Zone.LaTheinePlateau))
                .OnEventFinish(Zone.LaTheinePlateau, 212,
                    q => H.TelepointSealMemory(q, Zone.LaTheinePlateau))

                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.TahrongiCanyon))
                .OnEventFinish(Zone.TahrongiCanyon, 913, q => H.TelepointEntry(q, Zone.TahrongiCanyon))
                .OnEventFinish(Zone.TahrongiCanyon, 918,
                    q => H.TelepointSealMemory(q, Zone.TahrongiCanyon))

                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e0).OnTrigger(H.CermetGateOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e1).OnTrigger(H.CermetGateOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e2).OnTrigger(H.CermetGateOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e3)
                    .OnTrigger(H.LargeApparatusOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e5)
                    .OnTrigger(H.LargeApparatusOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e7)
                    .OnTrigger(H.LargeApparatusOnTrigger)
                .OnZoneIn(Zone.HallOfTransference, q => QuestReply.Event(108))
                .OnEventFinish(Zone.HallOfTransference, 122, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 125, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 128, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 160, q =>
                {
                    // the same event is used by every apparatus: the NPC's name gives the crag (1 to 3)
                    H.SendToPromyvionZone(q, H.ApparatusCrag(q));
                })

                .OnZoneIn(Zone.PromyvionDem, H.PromyvionOnZoneIn)
                .OnEventFinish(Zone.PromyvionDem, 50, q => q.Mission.SetVar("Option", H.Dem))
                .OnEventFinish(Zone.SpireOfDem, 32001, q => H.SpireEventFinish(q, Zone.SpireOfDem))

                .OnZoneIn(Zone.PromyvionHolla, H.PromyvionOnZoneIn)
                .OnEventFinish(Zone.PromyvionHolla, 50, q => q.Mission.SetVar("Option", H.Holla))
                .OnEventFinish(Zone.SpireOfHolla, 32001, q => H.SpireEventFinish(q, Zone.SpireOfHolla))

                .OnZoneIn(Zone.PromyvionMea, H.PromyvionOnZoneIn)
                .OnEventFinish(Zone.PromyvionMea, 50, q => q.Mission.SetVar("Option", H.Mea))
                .OnEventFinish(Zone.SpireOfMea, 32001, q => H.SpireEventFinish(q, Zone.SpireOfMea));
        }
    }
}
