// Save the Children (Ghelsba Outpost, San d'Oria 1-3's fight), a line-for-line port of
// scripts/battlefields/Ghelsba_Outpost/save_the_children.lua (quest rework C2). !pos -162 -11 78 140. A
// BattlefieldMission of 10 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL,
// always in area 1, entered at Hut_Door. The mission is San d'Oria's Save the Children with its status read from the
// San d'Oria log; a registrant needs it current at status 2, a helper it completed, or current at status 2 or more.
// Its one group, the three Fodderchiefs (by id: FODDERCHIEF_VOKDEK and the two after it), wins the fight when they
// die. Every method is BattlefieldMission's or Battlefield's: the file overrides none. The win sets battlefieldWin
// only while the mission is current, and the mission's 32001 finish (Missions/Sandoria/SaveTheChildren.cs) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.GhelsbaOutpost
{
    public sealed class SaveTheChildren : BattlefieldScript
    {
        public SaveTheChildren() : base("SAVE_THE_CHILDREN") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Ghelsba_Outpost/save_the_children";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.GhelsbaOutpost,
            BattlefieldId = Battlefield.SaveTheChildren,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(10)
            TimeLimit = 10 * 60,
            Index = 0,
            Area = 1,
            EntryNpcs = new[] { Npc.GhelsbaOutpost.HutDoor },
            MissionArea = XI.Quests.MissionLog.Sandoria,
            Mission = XI.QuestContent.Mission.Sandoria.SaveTheChildren,
            MissionStatusArea = XI.Quests.MissionLog.Sandoria,
            RequiredMissionStatus = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    MobIds = new[]
                    {
                        new BattlefieldMobId("FODDERCHIEF_VOKDEK"),
                        new BattlefieldMobId("FODDERCHIEF_VOKDEK", 1),
                        new BattlefieldMobId("FODDERCHIEF_VOKDEK", 2),
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
