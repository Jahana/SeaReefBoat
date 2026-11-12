// The shared shape of the six Cloisters' Waking the Beast battlefields (quest rework C3, Q2): ported from
// scripts/battlefields/Cloister_of_{Flames,Frost,Gales,Storms,Tides,Tremors}/waking_the_beast.lua. The files differ in
// their zone, constant, index, NPCs and their avatar and elemental; what they share is written once, here. Each
// battlefield keeps its own class (its constant and its LegacyPath, its slot in the containers list). Full Moon
// Fountain's Waking the Beast is a different file (its fight is closures): FullMoonFountain/WakingTheBeast.cs.
// A BattlefieldQuest of the Other Areas quest Waking the Beast: 30 minutes for up to 18, no experience lost, entered
// with the Rainbow Resonator, and repeatable (a completed char's win sets battlefieldWin too: user ruling 2026-11-05,
// kept bugs). Its first group is the avatar alone: when it dies its elementals (the four mob ids after it) die too,
// and the fight is won; its second group is the avatar and its elemental, party and superlinked.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields
{
    public abstract class WakingTheBeastBattlefield : BattlefieldScript
    {
        protected WakingTheBeastBattlefield(string constName, int zone, int battlefield, int index, string entryNpc,
                                            string exitNpc, string avatar, string elemental) : base(constName)
        {
            Data = new BattlefieldQuestData
            {
                ZoneId = zone,
                BattlefieldId = battlefield,
                CanLoseExp = false,
                MaxPlayers = 18,
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = index,
                EntryNpcs = new[] { entryNpc },
                ExitNpcs = new[] { exitNpc },

                QuestArea = XI.Quests.QuestLog.OtherAreas,
                Quest = "WAKING_THE_BEAST",
                Repeatable = true,

                Groups =
                {
                    new BattlefieldGroup
                    {
                        Mobs = new[] { avatar },
                        AllDeath = (q, mob) =>
                        {
                            // when avatar defeated then all elementals should also die
                            for (int i = 1; i <= 4; i++)
                            {
                                var e = q.Engine.MobById(mob.Id + i);
                                if (e != null && e.IsAlive)
                                {
                                    e.SetHP(0);
                                }
                            }

                            // giving the player the eye key item is done in the WTB quest script
                            q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                        },
                    },

                    new BattlefieldGroup
                    {
                        Mobs = new[] { avatar, elemental },
                        IsParty = true,
                        Superlink = true,
                    },
                },
            };
        }

        public sealed override BattlefieldData Data { get; }

        // function content:entryRequirement(player, npc, isRegistrant, trade): the registrant must be doing the quest
        // (holding the Rainbow Resonator); the others help if they hold it or have completed the quest
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            bool hasQuestItem = q.Player.HasKeyItem(KeyItem.RainbowResonator);
            var qd = (BattlefieldQuestData)Data;
            var status = q.Player.QuestStatus(qd.QuestArea, qd.Quest);
            bool prevCompletedQuest = status == XI.Quests.QuestStatus.Completed;

            if (isRegistrant)
            {
                return hasQuestItem;
            }

            return hasQuestItem || prevCompletedQuest;
        }
    }
}
