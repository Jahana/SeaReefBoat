// A Place to Return (Promathia 6-2, Log ID 6), a line-for-line port of
// scripts/missions/cop/6_2_A_Place_to_Return.lua (quest rework C3). The mission current. Ru'Lud Gardens: trigger area 1
// plays 10048 at Status 0 (Status 1); at Status 1 Pherimociel plays 28 and Auchefort 10055; Adolie, Chapi Galepilai,
// Colti and Neraf-Najiruf (once per zone) play 158, 12, 22 and 31 whatever the status. Misareaux Coast's _0p0 at Status
// 1 plays the cutscene 10 at Option 7 (its finish completes the mission), else with none of the three Wardens spawned
// spawns and claims them all (no action: the default is suppressed); each Warden's death at Status 1 sets its Option
// bit (Aglaia 0, Euphrosyne 1, Thalia 2). Behaviour ported as is:
// _0p0 at Option 3 with none of the Wardens spawned pops all three again.
// More Questions than Answers is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class APlaceToReturn : MissionScript
    {
        public APlaceToReturn() : base(XI.Quests.MissionLog.CoP, "A_PLACE_TO_RETURN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/6_2_A_Place_to_Return";

        static void WarderDeath(IQuestContext q, int bit)
        {
            if (q.Mission.Var("Status") == 1) q.Mission.SetVarBit("Option", bit);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.MoreQuestionsThanAnswers,
            });

            b.State(current: true)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(28) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Auchefort).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(10055) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Adolie).OnTrigger(QuestReply.Event(158))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.ChapiGalepilai).OnTrigger(QuestReply.Event(12))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Colti).OnTrigger(QuestReply.Event(22))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NerafNajiruf).OnTrigger(QuestReply.Event(31).OncePerZone())
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(10048) : null)
                .OnEventFinish(Zone.RuludeGardens, 10048, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p0).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") != 1)
                    {
                        return null;
                    }

                    if (q.Mission.Var("Option") == 7)
                    {
                        return QuestReply.ProgressCutscene(10);
                    }

                    if (!q.Player.MobIsSpawned(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 0) &&
                        !q.Player.MobIsSpawned(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 1) &&
                        !q.Player.MobIsSpawned(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 2))
                    {
                        q.Player.SpawnMob(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 0, claim: true);
                        q.Player.SpawnMob(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 1, claim: true);
                        q.Player.SpawnMob(Mob.MisareauxCoast.PM6_2_MOB_OFFSET, 2, claim: true);
                        return QuestReply.NoAction();
                    }

                    return null;
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.WarderAglaia).OnMobDeath(q => WarderDeath(q, 0))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.WarderEuphrosyne).OnMobDeath(q => WarderDeath(q, 1))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.WarderThalia).OnMobDeath(q => WarderDeath(q, 2))
                .OnEventFinish(Zone.MisareauxCoast, 10, q => q.Mission.Complete());
        }
    }
}
