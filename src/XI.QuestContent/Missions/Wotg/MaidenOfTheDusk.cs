// Maiden of the Dusk (WotG 51, Log ID 5, Mission ID 50), a line-for-line port of
// scripts/missions/wotg/51_Maiden_of_the_Dusk.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239
// 89; Ornate Door !pos -700 -20.25 -303.398 182. One section (the mission current). In Grauberg [S], the Veridical
// Conflux: at Status 0 the progress event 38 (89, 23, 1756, 0, 0, 0, Option (1 once declined), 0); else without the
// Primal Glow, the progress event 44 (89, 23, 1756, 0, 68032378, 209748094, 1, 0) when the Timer is at or before the
// Vana'diel day, else the plain event 45 with the same operands, once per zone. 38's finish with option 1 sets Status
// 1 and sets the char down at -700.042 0.399 -441.301 rot 192 in the Walk of Echoes, any other option sets Option 1;
// 44's finish sets the Timer to the next day and gives the Primal Glow. In the Walk of Echoes: the Ornate Door (_521)
// at Status 2 plays the progress event 5, whose finish sets Status 3; the zone-in plays the plain event 4 at Status 1,
// 6 at 4, 7 at 5, 8 at 6 and 9 at 7. Updates: 6 with option 6 sends (1, 300, 200, 100, 182, 1, 0, 0); 7 with option 5
// sends (1, 300, 200, 100, 73, 545, 278, 0); 8 with option 5 sends (1, 300, 200, 100, 0, 0, 0, 0). Finishes: 4 sets
// Status 2; 6, 7 and 8 set Status 5, 6 and 7 and set the char down at -700.063 -17.6 -331.903 rot 64 in the Walk of
// Echoes; 9 completes the mission (the Moonshade Earring key item, Where It All Began current); 32001 sets Status 4
// with the same set-down. Kept as the Lua has it: the 32001 finish reads no battlefieldWin (the Lua's TODO), and no
// battlefield file holds xi.battlefield.id.MAIDEN_OF_THE_DUSK (385), so nothing opens 32001 and nothing sets Status 4;
// the 32001 finish is waived in the flows and pinned by a fake check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class MaidenOfTheDusk : MissionScript
    {
        public MaidenOfTheDusk() : base(XI.Quests.MissionLog.WoTG, "MAIDEN_OF_THE_DUSK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/51_Maiden_of_the_Dusk";

        // the set-down of the finishes 6, 7, 8 and 32001
        static void SetDown(IQuestContext q) => q.Player.SetPos(-700.063, -17.6, -331.903, 64, Zone.WalkOfEchoes);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.MoonshadeEarring,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.WhereItAllBegan,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 0)
                    {
                        int hasDeclined = q.Mission.Var("Option");

                        return QuestReply.ProgressEvent(38, 89, 23, 1756, 0, 0, 0, hasDeclined, 0);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.PrimalGlow))
                    {
                        // the Lua's note: the wait for the key item again starts only after its first replacement
                        if (q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(44, 89, 23, 1756, 0, 68032378, 209748094, 1, 0);
                        }
                        else
                        {
                            return QuestReply.Event(45, 89, 23, 1756, 0, 68032378, 209748094, 1, 0).OncePerZone();
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.GraubergS, 38, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Mission.SetVar("Status", 1);
                        q.Player.SetPos(-700.042, 0.399, -441.301, 192, Zone.WalkOfEchoes);
                    }
                    else
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.GraubergS, 44, q =>
                {
                    q.Mission.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Player.GiveKeyItem(KeyItem.PrimalGlow);
                })
                .Npc(Zone.WalkOfEchoes, Npc.WalkOfEchoes._521).OnTrigger(q =>
                {
                    // the Lua's TODO: the fight's entry needs the Primal Glow and Status 3, and takes the key item
                    if (q.Mission.Var("Status") == 2)
                    {
                        return QuestReply.ProgressEvent(5);
                    }

                    return null;
                })
                .OnZoneIn(Zone.WalkOfEchoes, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 1)
                    {
                        return QuestReply.Event(4);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(6);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.Event(7);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.Event(8);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.Event(9);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.WalkOfEchoes, 6, q =>
                {
                    if (q.Option == 6)
                    {
                        q.Player.UpdateEvent(1, 300, 200, 100, 182, 1, 0, 0);
                    }
                })
                .OnEventUpdate(Zone.WalkOfEchoes, 7, q =>
                {
                    if (q.Option == 5)
                    {
                        q.Player.UpdateEvent(1, 300, 200, 100, 73, 545, 278, 0);
                    }
                })
                .OnEventUpdate(Zone.WalkOfEchoes, 8, q =>
                {
                    if (q.Option == 5)
                    {
                        q.Player.UpdateEvent(1, 300, 200, 100, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.WalkOfEchoes, 4, q =>
                {
                    q.Mission.SetVar("Status", 2);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 5, q =>
                {
                    q.Mission.SetVar("Status", 3);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 6, q =>
                {
                    q.Mission.SetVar("Status", 5);
                    SetDown(q);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 7, q =>
                {
                    q.Mission.SetVar("Status", 6);
                    SetDown(q);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 8, q =>
                {
                    q.Mission.SetVar("Status", 7);
                    SetDown(q);
                })
                .OnEventFinish(Zone.WalkOfEchoes, 9, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.WalkOfEchoes, 32001, q =>
                {
                    // the Lua's TODO: check a battlefieldWin local var set to the fight's id
                    q.Mission.SetVar("Status", 4);
                    SetDown(q);
                });
        }
    }
}
