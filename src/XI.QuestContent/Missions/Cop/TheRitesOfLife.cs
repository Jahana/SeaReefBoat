// The Rites of Life (Promathia 1-1, Log ID 6, Mission ID 110), a line-for-line port of
// scripts/missions/cop/1_1_The_Rites_of_Life.lua (quest rework C2). Enter Lower Delkfutt's Tower !pos -286 -20 320
// 126; enter Upper Jeuno !pos 2.2 -3.2 58.4 245; talk to Monberaux !pos -43 0 -1 244. Section 1 (ENABLE_COP 1, the
// CoP log before 1-1): Tales_Beginning plays 53 (3); the Lower Delkfutt's Tower zone-in from Qufim Island with the char
// var [COP]TalesBeginning at 0 plays 22; the afterZoneIn with the var above 0 shows the two Tales' Beginning NPCs. 22's
// finish on option 1 starts the hidden 36, else sets the var to 1, shows the two NPCs and sends the CoP log page with
// its special text; 36, 37 and 38 start the hidden 37, 38 and 39; 39's finish hides the NPCs again and clears the var
// when it is set, then begins the mission with the Status var 1. 53's finish on option 4 starts 22 (0, 0, 0, 1).
// Section 2 (current, Status 1): the Upper Jeuno zone-in plays 2, whose finish sets Status 2. Section 3 (current,
// Status 2): Monberaux plays 10; 10 and 206 start 206 and 207; 207's finish completes the mission (the Mysterious
// Amulet, Below the Arks next). Section 4 (1-1 completed, or Status above 0): Auchefort 8, Baran 17, Colti 21 and
// Rosaline 97 replace their defaults. Kept as the Lua has it: [COP]TalesBeginning is a plain char var, not a mission
// var (GameOkPackets' 0x056 reads it by that name); 53's 3 prints "Chains of Promathia" (the Lua's own guess: a copied
// cutscene); ENABLE_COP is compared with the number 1 (a boolean true fails); the two NPCs are TALES_BEGINNING and the
// id before it; 22's finish reads option 1 only, 53's option 4 only; 36 to 39 are started with { isHidden = true } and
// no other field; section 4 matches from Status 1 on, beside sections 2 and 3; mission:complete keeps the CoP status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheRitesOfLife : MissionScript
    {
        public TheRitesOfLife() : base(XI.Quests.MissionLog.CoP, "THE_RITES_OF_LIFE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/1_1_The_Rites_of_Life";

        // the char var the Tales' Beginning NPCs are shown by (a plain char var: GameOkPackets' 0x056 reads it)
        const string TalesBeginningVar = "[COP]TalesBeginning";

        // player:enableEntities({ lowerDelkfuttsIDs.npc.TALES_BEGINNING, lowerDelkfuttsIDs.npc.TALES_BEGINNING - 1 })
        static void EnableTalesBeginning(IQuestContext q)
        {
            int talesBeginning = q.Player.NpcIdOf("TALES_BEGINNING");
            q.Player.EnableEntities(talesBeginning, talesBeginning - 1);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.MysteriousAmulet,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.BelowTheArks,
            });

            // 1. To start this mission, enter Lower Delkfutt's Tower for a cutscene after installing the Chains of
            // Promathia expansion pack.
            b.State(current: null, when: q =>
                    q.Settings.MainEquals("ENABLE_COP", 1) &&
                    q.Player.CurrentMission(MissionLog.Cop) < Mission.Cop.TheRitesOfLife)
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower.TalesBeginning).OnTrigger(q =>
                {
                    // 3 prints "Chains of Promathia". I guess the cutscene was copy pasted?
                    return QuestReply.ProgressEvent(53, 3);
                })
                .OnZoneIn(Zone.LowerDelkfuttsTower, q =>
                {
                    if (q.Player.PreviousZone == Zone.QufimIsland && q.Player.CharVar(TalesBeginningVar) == 0)
                    {
                        return QuestReply.Event(22);
                    }

                    return null;
                })
                .OnAfterZoneIn(Zone.LowerDelkfuttsTower, q =>
                {
                    if (q.Player.CharVar(TalesBeginningVar) > 0)
                    {
                        EnableTalesBeginning(q);
                    }

                    return null;
                })
                // TODO: These are most likely a series of pos changes that trigger onZoneIn events.  In addition, set
                // status vars after each one so that on disconnect, it will resume at the latest state.  This pattern
                // should also change in section 3
                .OnEventFinish(Zone.LowerDelkfuttsTower, 22, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.StartEventHidden(36);
                    }
                    else
                    {
                        q.Player.SetCharVar(TalesBeginningVar, 1);
                        EnableTalesBeginning(q);

                        // Send packet to update mission log with special text
                        q.Player.SendPartialMissionLog(MissionLog.Cop, false);
                    }
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 36, q =>
                {
                    q.Player.StartEventHidden(37);
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 37, q =>
                {
                    q.Player.StartEventHidden(38);
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 38, q =>
                {
                    q.Player.StartEventHidden(39);
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 39, q =>
                {
                    if (q.Player.CharVar(TalesBeginningVar) > 0)
                    {
                        // Send blank entry to hide all selectively shown NPCs again
                        // see https://github.com/atom0s/XiPackets/blob/main/world/server/0x0077/README.md
                        q.Player.EnableEntities();

                        q.Player.SetCharVar(TalesBeginningVar, 0);
                    }

                    q.Mission.Begin();
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 53, q =>
                {
                    // It seems this parameter changes depending on the input to event 53
                    if (q.Option == 4)
                    {
                        // CoP intro CS with param[3] == 1 does not have the dialog that would eventually lead us to
                        // Tales' Beginning
                        q.Player.StartEvent(22, 0, 0, 0, 1);
                    }
                });

            // 2. After the cutscene has finished, enter Upper Jeuno for another cutscene.
            b.State(current: true, when: q => q.Mission.Var("Status") == 1)
                .OnZoneIn(Zone.UpperJeuno, q =>
                {
                    return QuestReply.Event(2);
                })
                .OnEventFinish(Zone.UpperJeuno, 2, q =>
                {
                    q.Mission.SetVar("Status", 2);
                });

            // 3. After the second cutscene finishes, speak to Monberaux (G-10, inside the Infirmary) for final cutscene
            // and a Mysterious Amulet.
            b.State(current: true, when: q => q.Mission.Var("Status") == 2)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrigger(QuestReply.ProgressEvent(10))
                .OnEventFinish(Zone.UpperJeuno, 10, q =>
                {
                    q.Player.StartEvent(206);
                })
                .OnEventFinish(Zone.UpperJeuno, 206, q =>
                {
                    q.Player.StartEvent(207);
                })
                .OnEventFinish(Zone.UpperJeuno, 207, q =>
                {
                    q.Mission.Complete();
                });

            // 4. After the first cutscene, several NPCs permanently change their default dialogue.
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheRitesOfLife) ||
                    q.Mission.Var("Status") > 0)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Auchefort).OnTrigger(QuestReply.Event(8).ReplaceDefault())
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Baran).OnTrigger(QuestReply.Event(17).ReplaceDefault())
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Colti).OnTrigger(QuestReply.Event(21).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Rosaline).OnTrigger(QuestReply.Event(97).ReplaceDefault());
        }
    }
}
