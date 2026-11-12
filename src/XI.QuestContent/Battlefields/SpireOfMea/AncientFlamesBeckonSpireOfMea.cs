// Ancient Flames Beckon (Spire of Mea), a port of scripts/battlefields/Spire_of_Mea/ancient_flames_beckon.lua (quest
// rework C3); the hooks are in AncientFlamesBeckon. Entered at _0l0 and left at _0l1 to _0l3. One group: the three
// Delvers (by id: DELVER, +1 and +2, one to an area) win the fight when they die.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SpireOfMea
{
    public sealed class AncientFlamesBeckonSpireOfMea : AncientFlamesBeckon
    {
        public AncientFlamesBeckonSpireOfMea() : base("ANCIENT_FLAMES_BECKON_SPIRE_OF_MEA") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Spire_of_Mea/ancient_flames_beckon";

        protected override int Crag => XI.QuestContent.Missions.Cop.PromyvionHelpers.Mea;
        protected override int Light => KeyItem.LightOfMea;

        public override BattlefieldData Data { get; } = new BattlefieldData
        {
            ZoneId = Zone.SpireOfMea,
            BattlefieldId = Battlefield.AncientFlamesBeckonSpireOfMea,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SpireOfMea._0l0 },
            ExitNpcs = new[] { Npc.SpireOfMea._0l1, Npc.SpireOfMea._0l2, Npc.SpireOfMea._0l3 },
            GrantXP = 1500,
            GrantXPLockout = true,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("DELVER") },
                        new[] { new BattlefieldMobId("DELVER", 1) },
                        new[] { new BattlefieldMobId("DELVER", 2) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };
    }
}
