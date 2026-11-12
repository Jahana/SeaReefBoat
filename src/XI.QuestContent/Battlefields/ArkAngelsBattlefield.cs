// The shared shape of the five Ark Angels battlefields (quest rework C3, C6): ported from
// scripts/battlefields/LaLoff_Amphitheater/ark_angels_1.lua to ark_angels_5.lua. The files differ in their constant,
// index, entry NPC, shard and Ark Angel, and the third and fifth add the pets' groups; what they share is written once,
// here. Each battlefield keeps its own class (its constant and its LegacyPath, its slot in the containers list). A
// BattlefieldMission of the Zilart log's Ark Angels, entered at Zilart status 1: 30 minutes for six, trusts allowed, no
// experience lost, capped at xi.settings.main.MAX_LEVEL, entered at its qm1_<n> and left at qm2. The entry wants the
// fight's shard not held (entryRequirement). Its first group is the Ark Angel, one id to an area (the zone's
// ARK_ANGEL_<xx> + 0 to 2), whose death wins the fight. A loss plays 32002 (0, 0, 0, 0, 0, the exit position, 180) and
// the exit qm2 plays 32003 (0, 0, 0, 0, the exit position, 180) to a char in a battlefield (nothing otherwise), the
// exit position being the char's battlefield's id minus Ark Angels 1's (the fight's own id but read from the
// battlefield, not the file). The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Zilart/ArkAngels.cs) reads it.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields
{
    public abstract class ArkAngelsBattlefield : BattlefieldScript
    {
        readonly int shard_;

        protected ArkAngelsBattlefield(string constName, int battlefield, int index, string entryNpc, int shard,
                                       string arkAngel, IEnumerable<BattlefieldGroup> petGroups)
            : base(constName)
        {
            shard_ = shard;

            var data = new BattlefieldMissionData
            {
                ZoneId = Zone.LaloffAmphitheater,
                BattlefieldId = battlefield,
                CanLoseExp = false,
                IsMission = true,
                AllowTrusts = true,
                MaxPlayers = 6,
                LevelCapSetting = "MAX_LEVEL",
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = index,
                EntryNpcs = new[] { entryNpc },
                ExitNpcs = new[] { Npc.LaloffAmphitheater.qm2 },
                MissionArea = XI.Quests.MissionLog.Zilart,
                Mission = XI.QuestContent.Mission.Zilart.ArkAngels,
                MissionStatusArea = XI.Quests.MissionLog.Zilart,
                RequiredMissionStatus = 1,

                Groups =
                {
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId(arkAngel) },
                            new[] { new BattlefieldMobId(arkAngel, 1) },
                            new[] { new BattlefieldMobId(arkAngel, 2) },
                        },

                        AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                    },
                },
            };

            data.Groups.AddRange(petGroups);
            Data = data;
        }

        public sealed override BattlefieldData Data { get; }

        // one of a pet's groups: the three areas' pets (the Ark Angel's id plus first, + 1, + 2), not spawned at start
        protected static BattlefieldGroup PetGroup(string arkAngel, int first) => new BattlefieldGroup
        {
            AreaMobIds = new[]
            {
                new[] { new BattlefieldMobId(arkAngel, first) },
                new[] { new BattlefieldMobId(arkAngel, first + 1) },
                new[] { new BattlefieldMobId(arkAngel, first + 2) },
            },

            Spawned = false,
        };

        public sealed override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant) =>
            !q.Player.HasKeyItem(shard_);

        public sealed override void OnBattlefieldLoss(IBattlefieldContext q)
        {
            int exitPosition = q.Battlefield.Id - Battlefield.ArkAngels1;
            q.Player.StartEvent(32002, 0, 0, 0, 0, 0, exitPosition, 180);
        }

        // content.onExitTrigger(player, npc): the exit NPC's onTrigger
        public sealed override IBattlefieldAction OnExitTrigger(IBattlefieldContext q)
        {
            var battlefield = q.Player.CurrentBattlefield;
            if (battlefield != null)
            {
                int exitPosition = battlefield.Id - Battlefield.ArkAngels1;
                return q.Engine.Event(32003, 0, 0, 0, 0, exitPosition, 180);
            }

            return null;
        }
    }
}
