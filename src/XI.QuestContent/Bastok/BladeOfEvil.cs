// Blade of Evil (Log ID 1, Quest ID 59), a line-for-line port of scripts/quests/bastok/DRK_AF3_Blade_of_Evil.lua (quest
// rework B3b). qm1 : !pos 84 -79 77 157. The Beadeaux zone-in from Pashhow Marshlands 122 (Dark Puppet completed, DRK,
// AF3 level) begins it; a Vial of Quadav Mage Blood at Middle Delkfutt's Tower's qm1 pops Gerwitz's Scythe (claimed) and
// its two victims (enmity), the Scythe's death sets Prog 1, and trigger area 8 at Prog 1 plays 14, whose option 0
// completes it. Kept as the Lua file has it: 14 declined (option other than 0) leaves Prog 1, so the next entry of area 8
// plays it again; the zone's own porter cutscene (area 8 - 1 = 7) is the trigger area's fallback and runs after the quest
// (the porter's 7 is queued behind 14). Lua's onTriggerAreaEnter returns quest:progressEvent(14), which the lookup
// performs: the reply overload of OnTriggerAreaEnter.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BladeOfEvil : QuestScript
    {
        public BladeOfEvil() : base(QuestLog.Bastok, "BLADE_OF_EVIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/DRK_AF3_Blade_of_Evil";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.ChaosBurgeonet,
                Title = Title.ParagonOfDarkKnightExcellence,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "DARK_PUPPET") &&
                    q.Player.MainJob == Job.Drk &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.Beadeaux, q => q.Player.PreviousZone == Zone.PashhowMarshlands ? QuestReply.Event(122) : null)
                .OnEventFinish(Zone.Beadeaux, 122, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.MiddleDelkfuttsTower, Npc.MiddleDelkfuttsTower.qm1).OnTrade(q =>
                {
                    if (q.Quest.Step == 0 &&
                        !q.Player.MobIsSpawned(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 0) &&
                        !q.Player.MobIsSpawned(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 1) &&
                        !q.Player.MobIsSpawned(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 2) &&
                        q.Trade.Matches((Item.VialOfQuadavMageBlood, 1)))
                    {
                        q.Trade.Complete();
                        q.Player.SpawnMob(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 0, claim: true);
                        q.Player.SpawnMob(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 1, claim: false);
                        q.Player.SpawnMob(Mob.MiddleDelkfuttsTower.BLADE_OF_EVIL_OFFSET, 2, claim: false);

                        return QuestReply.MessageSpecial(Text.MiddleDelkfuttsTower.SENSE_A_FOUL_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.MiddleDelkfuttsTower, Npc.MiddleDelkfuttsTower.GerwitzsScythe).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnTriggerAreaEnter(Zone.MiddleDelkfuttsTower, 8, q => q.Quest.Step == 1 ? QuestReply.ProgressEvent(14) : null)
                .OnEventFinish(Zone.MiddleDelkfuttsTower, 14, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Complete();
                    }
                });
        }
    }
}
