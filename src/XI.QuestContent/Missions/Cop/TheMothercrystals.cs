// The Mothercrystals (Promathia 1-3, Log ID 6, Mission ID 128), a line-for-line port of
// scripts/missions/cop/1_3_The_Mothercrystals.lua (quest rework C3). The Shattered Telepoints as in Below the Arks.
// Section 1 (the mission current): the telepoint's first branch, at one Light held, the zone's crag without its Light
// and Option 0, stores toPromyvion and plays the entry csid minus one (201 La Theine, 912 Konschtat and Tahrongi),
// whose finish sends the char to the Promyvion; else the helpers' trigger. The Hall's apparatuses _0e3, _0e5 and _0e7
// (no gates); the Hall's zone-in from a telepoint zone at two Lights, the previous zone's crag without its Light and
// Status 0 stores toPromyvion and plays 155, whose finish sets Status 1 and sends the char to the Promyvion; a
// Promyvion's 51 and 52 set Option to its crag; a Spire's 32001 is the helpers' spireEventFinish. The title is Ancient
// Flame Follower; An Invitation West is next. Section 2 (1-3 completed): each telepoint replaces its default with its
// entry csid (913, or 202 in La Theine), whose option 0 sets the char down in the Hall. Kept as the Lua has it: the
// first branch has no Status guard (Below the Arks's does);
// Tahrongi's 918 raises the Lua's error after clearing Option.
using XI.Quests.Authoring;
using H = XI.QuestContent.Missions.Cop.PromyvionHelpers;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheMothercrystals : MissionScript
    {
        public TheMothercrystals() : base(XI.Quests.MissionLog.CoP, "THE_MOTHERCRYSTALS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/1_3_The_Mothercrystals";

        static QuestReply Telepoint(IQuestContext q, int zone)
        {
            if (H.NumCompleted(q.Player) == 1 && !H.HasCompleted(q.Player, zone) && q.Mission.Var("Option") == 0)
            {
                var info = H.InfoOf(zone);
                q.Player.SetLocalVar("toPromyvion", info.Crag);
                return QuestReply.ProgressEvent(info.Entry - 1);
            }

            return H.TelepointOnTrigger(q, zone);
        }

        // a crag's Promyvion: the zone-in, 51 and 52 (Option to the crag)
        static QuestStateBuilder Promyvion(QuestStateBuilder s, ushort promyvion, ushort spire, int crag) => s
            .OnZoneIn(promyvion, H.PromyvionOnZoneIn)
            .OnEventFinish(promyvion, 51, q => q.Mission.SetVar("Option", crag))
            .OnEventFinish(promyvion, 52, q => q.Mission.SetVar("Option", crag))
            .OnEventFinish(spire, 32001, q => H.SpireEventFinish(q, spire));

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.AncientFlameFollower,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.AnInvitationWest,
            });

            var s = b.State(current: true)
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.KonschtatHighlands))
                .OnEventFinish(Zone.KonschtatHighlands, 912, H.SendToZoneOnFinish)
                .OnEventFinish(Zone.KonschtatHighlands, 913, q => H.TelepointEntry(q, Zone.KonschtatHighlands))
                .OnEventFinish(Zone.KonschtatHighlands, 918, q => H.TelepointSealMemory(q, Zone.KonschtatHighlands))

                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.LaTheinePlateau))
                .OnEventFinish(Zone.LaTheinePlateau, 201, H.SendToZoneOnFinish)
                .OnEventFinish(Zone.LaTheinePlateau, 202, q => H.TelepointEntry(q, Zone.LaTheinePlateau))
                .OnEventFinish(Zone.LaTheinePlateau, 212, q => H.TelepointSealMemory(q, Zone.LaTheinePlateau))

                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.ShatteredTelepoint)
                    .OnTrigger(q => Telepoint(q, Zone.TahrongiCanyon))
                .OnEventFinish(Zone.TahrongiCanyon, 912, H.SendToZoneOnFinish)
                .OnEventFinish(Zone.TahrongiCanyon, 913, q => H.TelepointEntry(q, Zone.TahrongiCanyon))
                .OnEventFinish(Zone.TahrongiCanyon, 918, q => H.TelepointSealMemory(q, Zone.TahrongiCanyon))

                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e3).OnTrigger(H.LargeApparatusOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e5).OnTrigger(H.LargeApparatusOnTrigger)
                .Npc(Zone.HallOfTransference, Npc.HallOfTransference._0e7).OnTrigger(H.LargeApparatusOnTrigger)
                .OnZoneIn(Zone.HallOfTransference, q =>
                {
                    var info = H.InfoOf(q.Player.PreviousZone);
                    if (info != null && H.NumCompleted(q.Player) == 2 &&
                        !H.HasCompleted(q.Player, q.Player.PreviousZone) && q.Mission.Var("Status") == 0)
                    {
                        q.Player.SetLocalVar("toPromyvion", info.Crag);
                        return QuestReply.Event(155);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HallOfTransference, 122, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 125, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 128, H.LargeApparatusOnEventFinish)
                .OnEventFinish(Zone.HallOfTransference, 155, q =>
                {
                    // this event only happens once: there is no sealing component
                    q.Mission.SetVar("Status", 1);
                    H.SendToPromyvionZone(q, q.Player.LocalVar("toPromyvion"));
                });

            s = Promyvion(s, Zone.PromyvionDem, Zone.SpireOfDem, H.Dem);
            s = Promyvion(s, Zone.PromyvionHolla, Zone.SpireOfHolla, H.Holla);
            Promyvion(s, Zone.PromyvionMea, Zone.SpireOfMea, H.Mea);

            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheMothercrystals))
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.ShatteredTelepoint)
                    .OnTrigger(QuestReply.Event(913).ReplaceDefault())
                .OnEventFinish(Zone.KonschtatHighlands, 913, q => HallOnOption0(q, -267.194, -40.634, -280.019, 0))
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.ShatteredTelepoint)
                    .OnTrigger(QuestReply.Event(913).ReplaceDefault())
                .OnEventFinish(Zone.TahrongiCanyon, 913, q => HallOnOption0(q, 280.066, -80.635, -67.096, 191))
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.ShatteredTelepoint)
                    .OnTrigger(QuestReply.Event(202).ReplaceDefault())
                .OnEventFinish(Zone.LaTheinePlateau, 202, q => HallOnOption0(q, -266.76, -0.635, 280.058, 0));
        }

        // to the Hall of Transference (R)
        static void HallOnOption0(IQuestContext q, double x, double y, double z, int rot)
        {
            if (q.Option == 0) q.Player.SetPos(x, y, z, rot, 14);
        }
    }
}
