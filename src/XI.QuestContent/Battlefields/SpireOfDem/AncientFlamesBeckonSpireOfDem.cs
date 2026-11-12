// Ancient Flames Beckon (Spire of Dem), a port of scripts/battlefields/Spire_of_Dem/ancient_flames_beckon.lua (quest
// rework C3); the hooks are in AncientFlamesBeckon. Entered at _0j0 and left at _0j1 to _0j3. Two groups: the three
// Progenerators (by id: PROGENERATOR, +5 and +10) win the fight when they die; the twelve mobs after them, four to an
// area (+1 to +4, +6 to +9, +11 to +14), are not spawned at the start.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SpireOfDem
{
    public sealed class AncientFlamesBeckonSpireOfDem : AncientFlamesBeckon
    {
        public AncientFlamesBeckonSpireOfDem() : base("ANCIENT_FLAMES_BECKON_SPIRE_OF_DEM") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Spire_of_Dem/ancient_flames_beckon";

        protected override int Crag => XI.QuestContent.Missions.Cop.PromyvionHelpers.Dem;
        protected override int Light => KeyItem.LightOfDem;

        static BattlefieldMobId[] Ids(params int[] offsets)
        {
            var ids = new BattlefieldMobId[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                ids[i] = offsets[i] == 0
                    ? new BattlefieldMobId("PROGENERATOR")
                    : new BattlefieldMobId("PROGENERATOR", offsets[i]);
            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.SpireOfDem,
            BattlefieldId = Battlefield.AncientFlamesBeckonSpireOfDem,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SpireOfDem._0j0 },
            ExitNpcs = new[] { Npc.SpireOfDem._0j1, Npc.SpireOfDem._0j2, Npc.SpireOfDem._0j3 },
            GrantXP = 1500,
            GrantXPLockout = true,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(5), Ids(10) },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(1, 2, 3, 4), Ids(6, 7, 8, 9), Ids(11, 12, 13, 14) },
                    Spawned = false,
                },
            },
        };
    }
}
