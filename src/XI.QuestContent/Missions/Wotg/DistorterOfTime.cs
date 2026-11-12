// Distorter of Time (WotG 24, Log ID 5, Mission ID 23), a line-for-line port of
// scripts/missions/wotg/24_Distorter_of_Time.lua (quest rework C3 T6). One section (the mission current), in
// Beaucedine Glacier [S]. Regal Pawprints 1 plays the progress event 26 (136, 23, 1756) while the char lacks the
// Umbra Bug and the Timer var is not past the Vana'diel day; its finish gives the Umbra Bug. The zone-in plays the
// plain event 19 at Status 1, whose finish completes the mission (The Will of the World current). Ruhotz
// Silvermines' event 10000 sets Status 1 and sets the char down at 51.641 -41.230 98.680 rot 0 in Beaucedine Glacier
// [S]. Kept as the Lua has it: Timer is read but never set in this mission; the only sender of the 10000 is the
// Crystal War Light in the Darkness instance, which sends it to each char when it completes, so a char on this
// mission who clears that instance gets Status 1 and this set-down while the zone's own 10000 finish sets the char
// down in Pashhow Marshlands [S]. No flow statement starts 10000 (the harness has no instance run); fake checks pin
// the finish and the set-down.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class DistorterOfTime : MissionScript
    {
        public DistorterOfTime() : base(XI.Quests.MissionLog.WoTG, "DISTORTER_OF_TIME") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/24_Distorter_of_Time";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TheWillOfTheWorld,
            });

            b.State(current: true)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints1).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.UmbraBug) && q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(26, 136, 23, 1756);
                    }

                    return null;
                })
                .OnZoneIn(Zone.BeaucedineGlacierS, q => q.Mission.Var("Status") == 1 ? QuestReply.Event(19) : null)
                .OnEventFinish(Zone.BeaucedineGlacierS, 19, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 26, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.UmbraBug);
                })
                .OnEventFinish(Zone.RuhotzSilvermines, 10000, q =>
                {
                    q.Mission.SetVar("Status", 1);
                    q.Player.SetPos(51.641, -41.230, 98.680, 0, Zone.BeaucedineGlacierS);
                });
        }
    }
}
