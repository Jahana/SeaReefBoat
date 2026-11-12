// Purple, The New Black (La Vaule [S], WotG 7's fight), a line-for-line port of
// scripts/battlefields/La_Vaule_[S]/purple_the_new_black.lua (quest rework C3 T5). A BattlefieldMission of 30 minutes
// for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, index 1, every area allowed (the
// Lua gives no area list), entered at _2d1 and left at _2d3, _2d5 or _2d7. The mission is WotG's Purple, The New Black
// with its status read from the WotG log; a registrant needs status 1. Its one group, Galarhigg (one list per area: the
// id and the ids 1 and 2 after it), wins the fight when it dies. The win sets battlefieldWin only while the mission is
// current, and the mission's 32001 finish (Missions/Wotg/PurpleTheNewBlack.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaVauleS
{
    public sealed class PurpleTheNewBlack : BattlefieldScript
    {
        public PurpleTheNewBlack() : base("PURPLE_THE_NEW_BLACK") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/La_Vaule_[S]/purple_the_new_black";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.LaVauleS,
            BattlefieldId = Battlefield.PurpleTheNewBlack,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.LaVauleS._2d1 },
            ExitNpcs = new[]
            {
                Npc.LaVauleS._2d3,
                Npc.LaVauleS._2d5,
                Npc.LaVauleS._2d7,
            },
            MissionArea = XI.Quests.MissionLog.WoTG,
            Mission = XI.QuestContent.Mission.Wotg.PurpleTheNewBlack,
            MissionStatusArea = XI.Quests.MissionLog.WoTG,
            RequiredMissionStatus = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("GALARHIGG") },
                        new[] { new BattlefieldMobId("GALARHIGG", 1) },
                        new[] { new BattlefieldMobId("GALARHIGG", 2) },
                    },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };
    }
}
