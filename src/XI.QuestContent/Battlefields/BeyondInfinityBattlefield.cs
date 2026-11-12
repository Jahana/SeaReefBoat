// The shared shape of the four Beyond Infinity battlefields (quest rework C3, Q3a): ported from
// scripts/battlefields/{Balgas_Dais,Horlais_Peak,QuBia_Arena,Waughroon_Shrine}/beyond_infinity.lua. The files differ in
// their zone, constant, index, Soul Gem texts and Atori-Tutori ids; what they share is written once, here. Each
// battlefield keeps its own class (its constant and its LegacyPath, its slot in the containers list).
// A BattlefieldQuest of the Jeuno quest Beyond Infinity: 10 minutes for six, trusts allowed, capped at
// xi.settings.main.MAX_LEVEL, no experience lost, entered at BC_Entrance with the Soul Gem Clasp and left at
// Burning_Circle. The clasp is needed (taken on entry, from the registrant alone; SOUL_GEM_REACTS names the clasp and
// the Soul Gem). Its one group is Atori-Tutori, one id to an area; its death wins the fight. Every method is
// BattlefieldQuest's or Battlefield's: the files override none.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields
{
    public abstract class BeyondInfinityBattlefield : BattlefieldScript
    {
        protected BeyondInfinityBattlefield(string constName, int zone, int battlefield, int index, string entryNpc,
                                            string exitNpc, ZoneText soulGemReacts, ZoneText lostKeyitem)
            : base(constName)
        {
            Data = new BattlefieldQuestData
            {
                ZoneId = zone,
                BattlefieldId = battlefield,
                CanLoseExp = false,
                AllowTrusts = true,
                MaxPlayers = 6,
                LevelCapSetting = "MAX_LEVEL",
                // utils.minutes(10)
                TimeLimit = 10 * 60,
                Index = index,
                EntryNpcs = new[] { entryNpc },
                ExitNpcs = new[] { exitNpc },
                RequiredKeyItems = new BattlefieldRequiredKeyItems
                {
                    Items = { KeyItem.SoulGemClasp },
                    OnlyInitiator = true,
                    Message = soulGemReacts,
                    MessageArgs = new[] { KeyItem.SoulGemClasp, KeyItem.SoulGem },
                    DeleteMessage = lostKeyitem,
                },

                QuestArea = XI.Quests.QuestLog.Jeuno,
                Quest = "BEYOND_INFINITY",

                Groups =
                {
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId("ATORI_TUTORI") },
                            new[] { new BattlefieldMobId("ATORI_TUTORI", 1) },
                            new[] { new BattlefieldMobId("ATORI_TUTORI", 2) },
                        },

                        AllDeath = (q, mob) =>
                        {
                            q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                        },
                    },
                },
            };
        }

        public sealed override BattlefieldData Data { get; }
    }
}
