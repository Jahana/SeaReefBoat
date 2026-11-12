// Ancient Flames Beckon (Spire of Holla), a port of scripts/battlefields/Spire_of_Holla/ancient_flames_beckon.lua
// (quest rework C3); the hooks are in AncientFlamesBeckon. Entered at _0h0 and left at _0h1 to _0h3. One group: the
// three Wreakers (by id: WREAKER, +1 and +2, one to an area) win the fight when they die.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SpireOfHolla
{
    public sealed class AncientFlamesBeckonSpireOfHolla : AncientFlamesBeckon
    {
        public AncientFlamesBeckonSpireOfHolla() : base("ANCIENT_FLAMES_BECKON_SPIRE_OF_HOLLA") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Spire_of_Holla/ancient_flames_beckon";

        protected override int Crag => XI.QuestContent.Missions.Cop.PromyvionHelpers.Holla;
        protected override int Light => KeyItem.LightOfHolla;

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.SpireOfHolla,
            BattlefieldId = Battlefield.AncientFlamesBeckonSpireOfHolla,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SpireOfHolla._0h0 },
            ExitNpcs = new[] { Npc.SpireOfHolla._0h1, Npc.SpireOfHolla._0h2, Npc.SpireOfHolla._0h3 },
            GrantXP = 1500,
            GrantXPLockout = true,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("WREAKER") },
                        new[] { new BattlefieldMobId("WREAKER", 1) },
                        new[] { new BattlefieldMobId("WREAKER", 2) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };
    }
}
