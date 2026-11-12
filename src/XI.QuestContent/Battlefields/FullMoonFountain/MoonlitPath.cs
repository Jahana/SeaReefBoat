// The Moonlit Path (Full Moon Fountain BCNM), a line-for-line port of
// scripts/battlefields/Full_Moon_Fountain/moonlit_path.lua (quest rework C3, Q1). A BattlefieldQuest of the Windurst
// quest The Moonlit Path: 30 minutes for six, no experience lost, index 0, entered at MS_Entrance with the Moon
// Bauble (not consumed) and left at Moon_Spiral. Its one group is Fenrir Prime and the two ids after it (one list per
// area); its death wins the fight. Every method is BattlefieldQuest's or Battlefield's: the file overrides none.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.FullMoonFountain
{
    public sealed class MoonlitPath : BattlefieldScript
    {
        public MoonlitPath() : base("MOONLIT_PATH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Full_Moon_Fountain/moonlit_path";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.FullMoonFountain,
            BattlefieldId = Battlefield.MoonlitPath,
            CanLoseExp = false,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.FullMoonFountain.MSEntrance },
            ExitNpcs = new[] { Npc.FullMoonFountain.MoonSpiral },
            RequiredKeyItems = new BattlefieldRequiredKeyItems { Items = { KeyItem.MoonBauble }, Keep = true },

            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "THE_MOONLIT_PATH",

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("FENRIR_PRIME") },
                        new[] { new BattlefieldMobId("FENRIR_PRIME", 1) },
                        new[] { new BattlefieldMobId("FENRIR_PRIME", 2) },
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
