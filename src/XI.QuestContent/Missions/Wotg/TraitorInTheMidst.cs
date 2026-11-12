// Traitor in the Midst (WotG 18, Log ID 5, Mission ID 17), a line-for-line port of
// scripts/missions/wotg/18_Traitor_in_the_Midst.lua (quest rework C3 T6). One section (the mission current), in
// Beaucedine Glacier [S]. The pawprint number n is the last character of the NPC's name (Regal Pawprints 3 to 7, a
// minigame each): the trigger plays the progress event n + 5 (8 to 12) while the Status var's bit n - 3 is clear. Each
// game's update with option 1 stores the system time in the mission local var Timer when it is 0, and option 2 clears
// it; its finish with option 1 sets Status bit n - 3 when the time since Timer is at least 20 s for pawprints 3 to 5 (0
// for 6 and 7), then clears Timer whatever the option. Regal Pawprints 1 plays the progress event 14 (136, 300, 200,
// 100, 0, 9306117, 0, 0) at Status 31, whose finish completes the mission (Betrayal at Beaucedine current). Kept as
// the Lua has it: a finish with no update has Timer 0, so the elapsed time is the whole system time and passes;
// pawprint 5 is under the `<= 5` rule (20 s), with 6 and 7 at once; a set pawprint, and Regal Pawprints 1 below Status
// 31, give no reply of this script (the framework's DefaultActions answers).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TraitorInTheMidst : MissionScript
    {
        public TraitorInTheMidst() : base(XI.Quests.MissionLog.WoTG, "TRAITOR_IN_THE_MIDST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/18_Traitor_in_the_Midst";

        // tonumber(string.sub(npc:getName(), -1))
        static int PawprintNum(IQuestContext q) => q.Npc.Name[q.Npc.Name.Length - 1] - '0';

        static QuestReply GameOnTrigger(IQuestContext q)
        {
            int pawprintNum = PawprintNum(q);

            if (!q.Mission.IsVarBitsSet("Status", pawprintNum - 3))
            {
                return QuestReply.ProgressEvent(pawprintNum + 5);
            }

            return null;
        }

        static void GameOnEventUpdate(IQuestContext q)
        {
            if (q.Option == 1 && q.Mission.LocalVar("Timer") == 0)
            {
                q.Mission.SetLocalVar("Timer", q.Player.SystemTime);
            }
            else if (q.Option == 2)
            {
                q.Mission.SetLocalVar("Timer", 0);
            }
        }

        static void GameOnEventFinish(IQuestContext q)
        {
            int pawprintNum = PawprintNum(q);
            int requiredTime = pawprintNum <= 5 ? 20 : 0;

            if (q.Option == 1 && q.Player.SystemTime - q.Mission.LocalVar("Timer") >= requiredTime)
            {
                q.Mission.SetVarBit("Status", pawprintNum - 3);
            }

            q.Mission.SetLocalVar("Timer", 0);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.BetrayalAtBeaucedine,
            });

            b.State(current: true)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints3).OnTrigger(GameOnTrigger)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints4).OnTrigger(GameOnTrigger)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints5).OnTrigger(GameOnTrigger)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints6).OnTrigger(GameOnTrigger)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints7).OnTrigger(GameOnTrigger)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints1).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 31)
                    {
                        return QuestReply.ProgressEvent(14, 136, 300, 200, 100, 0, 9306117, 0, 0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.BeaucedineGlacierS, 8, GameOnEventUpdate)
                .OnEventUpdate(Zone.BeaucedineGlacierS, 9, GameOnEventUpdate)
                .OnEventUpdate(Zone.BeaucedineGlacierS, 10, GameOnEventUpdate)
                .OnEventUpdate(Zone.BeaucedineGlacierS, 11, GameOnEventUpdate)
                .OnEventUpdate(Zone.BeaucedineGlacierS, 12, GameOnEventUpdate)
                .OnEventFinish(Zone.BeaucedineGlacierS, 8, GameOnEventFinish)
                .OnEventFinish(Zone.BeaucedineGlacierS, 9, GameOnEventFinish)
                .OnEventFinish(Zone.BeaucedineGlacierS, 10, GameOnEventFinish)
                .OnEventFinish(Zone.BeaucedineGlacierS, 11, GameOnEventFinish)
                .OnEventFinish(Zone.BeaucedineGlacierS, 12, GameOnEventFinish)
                .OnEventFinish(Zone.BeaucedineGlacierS, 14, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
