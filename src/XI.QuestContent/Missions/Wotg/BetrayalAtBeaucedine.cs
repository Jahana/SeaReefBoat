// Betrayal at Beaucedine (WotG 19, Log ID 5, Mission ID 18), a line-for-line port of
// scripts/missions/wotg/19_Betrayal_at_Beaucedine.lua (quest rework C3 T6). Regal Pawprints !pos -15.066 -40.249
// -217.276 136. One section (the mission current), in Beaucedine Glacier [S]. Regal Pawprints 2 at Status 0 plays
// the progress event 15 (136, 300, 200, 100, 0, 9306122, 0, 0), whose finish sets Status 1. At Status 1 it spawns
// Count Halphas on the char's claim and sends UNWANTED_ATTENTION when he is not spawned, else NOW_IS_NOT_THE_TIME
// when the char is engaged, else UNUSUAL_PRESENCE; Count Halphas's death sets Status 2 at Status 1. At Status 2 the
// pawprint plays the progress event 16 (0, 300, 200, 100), whose finish sets Status 3 and sets the char down at
// -15.036 -40.355 -219.202 rot 189 in the same zone. The zone-in plays the plain event 30 at Status 3, whose finish
// completes the mission (On Thin Ice current). Kept as the Lua has it: the mob is spawned with the char's claim
// (updateClaim); the 'player:getZone() nil' guard has no arm. The main flow's trace shows the set-down at the tick
// after 16's finish; a fake check pins it too.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class BetrayalAtBeaucedine : MissionScript
    {
        public BetrayalAtBeaucedine() : base(XI.Quests.MissionLog.WoTG, "BETRAYAL_AT_BEAUCEDINE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/19_Betrayal_at_Beaucedine";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.OnThinIce,
            });

            b.State(current: true)
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RegalPawprints2).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(15, 136, 300, 200, 100, 0, 9306122, 0, 0);
                    }
                    else if (missionStatus == 1)
                    {
                        int mobId = q.Player.MobIdByName(Npc.BeaucedineGlacierS.CountHalphas);

                        if (!q.Player.MobIsSpawnedById(mobId))
                        {
                            q.Player.SpawnMobById(mobId, claim: true);

                            return QuestReply.MessageSpecial(Text.BeaucedineGlacierS.UNWANTED_ATTENTION);
                        }
                        else
                        {
                            if (q.Player.IsEngaged)
                            {
                                return QuestReply.MessageSpecial(Text.BeaucedineGlacierS.NOW_IS_NOT_THE_TIME);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.BeaucedineGlacierS.UNUSUAL_PRESENCE);
                            }
                        }
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(16, 0, 300, 200, 100, 0, 0, 0, 0);
                    }

                    return null;
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.CountHalphas).OnMobDeath(q =>
                {
                    if (q.Mission.Var("Status") == 1)
                    {
                        q.Mission.SetVar("Status", 2);
                    }
                })
                .OnZoneIn(Zone.BeaucedineGlacierS, q => q.Mission.Var("Status") == 3 ? QuestReply.Event(30) : null)
                .OnEventFinish(Zone.BeaucedineGlacierS, 15, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 16, q =>
                {
                    q.Mission.SetVar("Status", 3);
                    q.Player.SetPos(-15.036, -40.355, -219.202, 189, Zone.BeaucedineGlacierS);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 30, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
