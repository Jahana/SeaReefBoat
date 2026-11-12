// Dungeons and Dancers (WotG 23, Log ID 5, Mission ID 22), a line-for-line port of
// scripts/missions/wotg/23_Dungeons_and_Dancers.lua (quest rework C3 T6). One section (the mission current), in
// Beaucedine Glacier [S]. Regal Pawprints 8 plays the progress event 20 (36, 5, 1756) at Status 0, whose finish sets
// Status 1. Regal Pawprints 1 plays the progress event 25 (136, 23, 1756, 0, 0, 7995412, 0, 0) while the char lacks
// the Aroma Bug and the Timer var is not past the Vana'diel day; its finish gives the Aroma Bug. The zone-in plays
// the plain event 18 at Status 2, whose finish completes the mission (Distorter of Time current, the Umbra Bug
// given). Everbloom Hollow's event 10000 sets Status 2 and sets the char down at -148.078 -61.320 -176.608 rot 32 in
// Beaucedine Glacier [S]. Kept as the Lua has it: Timer is read but never set in this mission; the 10000 finish and
// the set-down position are not reachable by a flow (fake checks pin them).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class DungeonsAndDancers : MissionScript
    {
        public DungeonsAndDancers() : base(XI.Quests.MissionLog.WoTG, "DUNGEONS_AND_DANCERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/23_Dungeons_and_Dancers";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.UmbraBug,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.DistorterOfTime,
            });

            b.State(current: true)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints8).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 0)
                    {
                        return QuestReply.ProgressEvent(20, 36, 5, 1756);
                    }

                    return null;
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints1).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.AromaBug) && q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(25, 136, 23, 1756, 0, 0, 7995412, 0, 0);
                    }

                    return null;
                })
                .OnZoneIn(Zone.BeaucedineGlacierS, q => q.Mission.Var("Status") == 2 ? QuestReply.Event(18) : null)
                .OnEventFinish(Zone.BeaucedineGlacierS, 18, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 20, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 25, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AromaBug);
                })
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(-148.078, -61.320, -176.608, 32, Zone.BeaucedineGlacierS);
                });
        }
    }
}
