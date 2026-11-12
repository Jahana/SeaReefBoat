// When Wills Collide (WotG 46, Log ID 5, Mission ID 45), a line-for-line port of
// scripts/missions/wotg/46_When_Wills_Collide.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749
// 585.239 89. Section 1 (the mission current). In Grauberg [S]: without the Bottled Punch Bug, a trade of exactly one
// Punch Bug plays the progress event 42 (76, 0, 1267351, 120, 0, 8323092, 0, 0), whose finish gives the key item (the
// Lua's note: no trade completion, so the bug stays); a trigger plays the plain event 29 (89, 6) with the key item,
// else 41 (89, 23, 1756); 29's finish with option 99 sets the char down at -700.042 0.4 -441.301 rot 192 in the Walk
// of Echoes. In the Walk of Echoes: the Ornate Door (_521) at Status 0 plays the progress event 1, whose update with
// option 5 sends the update (119, the char's face, the head, body, hands, main and sub model ids, 1) and whose finish
// sets Status 1; the zone-in at Status 2 plays the plain event 2, whose update with option 5 sends the update (182,
// the face and the same models, 0) and whose finish completes the mission (Whispers of Dawn current); the 32001 finish
// sets Status 2 and sets the char down at -700.063 -17.6 -331.903 rot 64 in the Walk of Echoes. Section 2 (the mission
// completed): the Veridical Conflux plays the plain event 29 (89, 12354, 59449, 120, 0, 8323089, 0, 0), whose finish
// with option 99 sets the char down as above. Kept as the Lua has it: the 32001 finish reads no battlefieldWin (the
// Lua's TODO), and no battlefield file holds the fight (no Walk of Echoes battlefield, no xi.battlefield.id
// constant), so nothing opens 32001 and nothing sets Status 2; the 32001 finish is waived in the flows and pinned by a
// fake check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class WhenWillsCollide : MissionScript
    {
        public WhenWillsCollide() : base(XI.Quests.MissionLog.WoTG, "WHEN_WILLS_COLLIDE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/46_When_Wills_Collide";

        // 29's finish (both sections): option 99 sets the char down at the Walk of Echoes' entrance
        static void ToWalkOfEchoes(IQuestContext q)
        {
            if (q.Option == 99)
            {
                q.Player.SetPos(-700.042, 0.4, -441.301, 192, Zone.WalkOfEchoes);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.WhispersOfDawn,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrade(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BottledPunchBug) &&
                        q.Trade.Matches((Item.PunchBug, 1)))
                    {
                        return QuestReply.ProgressEvent(42, 76, 0, 1267351, 120, 0, 8323092, 0, 0);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.BottledPunchBug))
                    {
                        return QuestReply.Event(29, 89, 6);
                    }
                    else
                    {
                        return QuestReply.Event(41, 89, 23, 1756);
                    }
                })
                .OnEventFinish(Zone.GraubergS, 29, ToWalkOfEchoes)
                .OnEventFinish(Zone.GraubergS, 42, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BottledPunchBug);
                })
                .Npc(Zone.WalkOfEchoes, Npc.WalkOfEchoes._521).OnTrigger(q =>
                {
                    // the Lua's note: the fight's entry (32000) follows, with Status 1 and the key item, lost on entry
                    if (q.Mission.Var("Status") == 0)
                    {
                        return QuestReply.ProgressEvent(1);
                    }

                    return null;
                })
                .OnZoneIn(Zone.WalkOfEchoes, q =>
                {
                    if (q.Mission.Var("Status") == 2)
                    {
                        return QuestReply.Event(2);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.WalkOfEchoes, 1, q =>
                {
                    if (q.Option == 5)
                    {
                        var equip = q.Player.EquipmentModelIds;
                        q.Player.UpdateEvent(119, q.Player.Face, equip.Head, equip.Body, equip.Hands, equip.Main,
                            equip.Sub, 1);
                    }
                })
                .OnEventUpdate(Zone.WalkOfEchoes, 2, q =>
                {
                    if (q.Option == 5)
                    {
                        var equip = q.Player.EquipmentModelIds;
                        q.Player.UpdateEvent(182, q.Player.Face, equip.Head, equip.Body, equip.Hands, equip.Main,
                            equip.Sub, 0);
                    }
                })
                .OnEventFinish(Zone.WalkOfEchoes, 1, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 2, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.WalkOfEchoes, 32001, q =>
                {
                    // the Lua's TODO: check a battlefieldWin local var once the fight's id exists
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(-700.063, -17.6, -331.903, 64, Zone.WalkOfEchoes);
                });

            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.WhenWillsCollide))
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux)
                .OnTrigger(QuestReply.Event(29, 89, 12354, 59449, 120, 0, 8323089, 0, 0))
                .OnEventFinish(Zone.GraubergS, 29, ToWalkOfEchoes);
        }
    }
}
