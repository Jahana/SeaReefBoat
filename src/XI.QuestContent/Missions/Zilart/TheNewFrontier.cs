// The New Frontier (Zilart 1, Log ID 3, Mission ID 0), a line-for-line port of
// scripts/missions/rotz/01_The_New_Frontier.lua (quest rework C3, C5). Norg !zone 252; Tales' Beginning
// !pos -25.784 1.097 -40.953 252. Section 1 (current, the char's rank in its own nation 6 or more): the Norg
// afterZoneIn with the char var [ROZ]TalesBeginning at 0 plays 1 (the short notice), else shows the two Tales'
// Beginning NPCs; 1's update answers the notice (79, VanadielTime, ..., MAX_UINT32, 9044486, 0) while the var is 0,
// else the full cutscene (65, ...). 1's finish on option 0 with the var at 0 sets it to 1, sends the Zilart log page
// and shows the two NPCs; else on option 1 or 0 it clears a set var and hides the NPCs, then completes the mission
// (the Map of Norg, Welcome t'Norg next). Tales_Beginning plays 293, whose finish on option 1 starts 1 (252, ...).
// Section 2 (completed): Norg's _700 plays 5 and Comitiolus 6, replacing their defaults. Kept as the Lua has it:
// [ROZ]TalesBeginning is a plain char var (GameOkPackets' 0x056 reads it by that name); the two NPCs are
// TALES_BEGINNING and the id before it; the finish's second arm reads option 1 or 0, so a full cutscene from the
// Tales' Beginning (option 0, var 1) completes the mission too; section 2 is not gated on the mission being past.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheNewFrontier : MissionScript
    {
        public TheNewFrontier() : base(XI.Quests.MissionLog.Zilart, "THE_NEW_FRONTIER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/01_The_New_Frontier";

        // the char var the Tales' Beginning NPCs are shown by (a plain char var: GameOkPackets' 0x056 reads it)
        const string TalesBeginningVar = "[ROZ]TalesBeginning";

        // utils.MAX_UINT32 as the event's 32-bit parameter
        const int MaxUint32 = unchecked((int)uint.MaxValue);

        // player:enableEntities({ norgID.npc.TALES_BEGINNING, norgID.npc.TALES_BEGINNING - 1 })
        static void EnableTalesBeginning(IQuestContext q)
        {
            int talesBeginning = q.Player.NpcIdOf("TALES_BEGINNING");
            q.Player.EnableEntities(talesBeginning, talesBeginning - 1);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.MapOfNorg,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.WelcomeTnorg,
            });

            b.State(current: true, when: q => q.Player.Rank(q.Player.Nation) >= 6)
                .Npc(Zone.Norg, Npc.Norg.TalesBeginning).OnTrigger(q =>
                    QuestReply.ProgressEvent(293, 0, 0, 838927106, 157421318, 0, 4457218, 292556943, 0))
                .OnAfterZoneIn(Zone.Norg, q =>
                {
                    if (q.Player.CharVar(TalesBeginningVar) == 0)
                    {
                        return QuestReply.ProgressEvent(1, 5, 0, 1751, 292583557, 1615226948, 424215298, 21487749,
                            8262);
                    }
                    else
                    {
                        EnableTalesBeginning(q);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.Norg, 1, q =>
                {
                    if (q.Player.CharVar(TalesBeginningVar) == 0)
                    {
                        // The reply picks the short notice over the full cutscene.
                        q.Player.UpdateEvent(79, q.Player.VanadielTime, 0, 0, 0, MaxUint32, 9044486, 0);
                    }
                    else
                    {
                        // The reply picks the full cutscene.
                        q.Player.UpdateEvent(65, 0, 838927106, 1, 0, 4457218, 292556943, 0);
                    }
                })
                .OnEventFinish(Zone.Norg, 1, q =>
                {
                    // Maybe later. The storyline is postponed until started at the Tales' Beginning.
                    if (q.Option == 0 && q.Player.CharVar(TalesBeginningVar) == 0)
                    {
                        q.Player.SetCharVar(TalesBeginningVar, 1);
                        q.Player.SendPartialMissionLog(MissionLog.Zilart, false);
                        EnableTalesBeginning(q);
                    }
                    // The full cutscene played, at once (option 1) or from the Tales' Beginning.
                    else if (q.Option == 1 || q.Option == 0)
                    {
                        if (q.Player.CharVar(TalesBeginningVar) > 0)
                        {
                            q.Player.SetCharVar(TalesBeginningVar, 0);
                            q.Player.EnableEntities();
                        }

                        q.Mission.Complete();
                    }
                })
                .OnEventFinish(Zone.Norg, 293, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.StartEvent(1, 252, 0, 838927106, 1, 0, 4457218, 292556943, 0);
                    }
                });

            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(QuestReply.Event(5).ReplaceDefault())
                .Npc(Zone.Norg, Npc.Norg.Comitiolus).OnTrigger(QuestReply.Event(6).ReplaceDefault());
        }
    }
}
