// Omens (Navukgo Execution Chamber BCNM), a line-for-line port of
// scripts/battlefields/Navukgo_Execution_Chamber/omens.lua (quest rework C3, Q3b). A BattlefieldQuest of the Aht
// Urhgan quest Omens: 30 minutes for 18, index 2, entered at _1s0 when Quest[6][22]Prog is 0 and left at _1s1 to
// _1s3. Two groups, one list per area, both in superlink group 1: the Immortal Flan (the first of an area, spawned)
// and the added ones (five to an area, not spawned). Each death runs handleDeath: the fight is won once none of the
// area's flans up to numSpawned is spawned and alive. battlefieldEntry spawns one more flan for every three players
// entering after the first (numSpawned is that count).
// Kept Lua quirk, the revisit: a player who leaves and enters again before the battlefield locks runs the entry again
// at each count (4, 7, ...), so a dead and despawned flan is spawned again and numSpawned goes back to that count (2 to
// 1 when the count returns to 4); the win then ignores a living flan +2.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.NavukgoExecutionChamber
{
    public sealed class Omens : BattlefieldScript
    {
        public Omens() : base("OMENS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Navukgo_Execution_Chamber/omens";

        static BattlefieldMobId Flan(int offset) => new BattlefieldMobId("IMMORTAL_FLAN", offset);

        static BattlefieldMobId[] Adds(int first) => new[]
        {
            Flan(first), Flan(first + 1), Flan(first + 2), Flan(first + 3), Flan(first + 4),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.NavukgoExecutionChamber,
            BattlefieldId = Battlefield.Omens,
            MaxPlayers = 18,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 2,
            EntryNpcs = new[] { Npc.NavukgoExecutionChamber._1s0 },
            ExitNpcs = new[]
            {
                Npc.NavukgoExecutionChamber._1s1, Npc.NavukgoExecutionChamber._1s2, Npc.NavukgoExecutionChamber._1s3,
            },

            QuestArea = XI.Quests.QuestLog.AhtUrghan,
            Quest = "OMENS",
            RequiredVar = "Quest[6][22]Prog",
            RequiredValue = 0,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Flan(0) },
                        new[] { Flan(7) },
                        new[] { Flan(14) },
                    },

                    SuperlinkGroup = 1,
                    Death = HandleDeath,
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Adds(1), Adds(8), Adds(15) },

                    Spawned = false,
                    SuperlinkGroup = 1,
                    Death = HandleDeath,
                },
            },
        };

        static int FirstFlan(IBattlefieldContext q)
        {
            int baseId = q.Engine.ZoneMobId(Zone.NavukgoExecutionChamber, "IMMORTAL_FLAN").Value;
            return baseId + (q.Battlefield.Area - 1) * 7;
        }

        static void HandleDeath(IBattlefieldContext q, IBattlefieldMob mob, int deathCount)
        {
            int mobIdOffset = FirstFlan(q);
            int last = mobIdOffset + (int)q.Battlefield.LocalVar("numSpawned");

            for (int mobId = mobIdOffset; mobId <= last; mobId++)
            {
                var mobObj = q.Engine.MobById(mobId);

                if (mobObj != null && mobObj.IsSpawned && !mobObj.IsDead)
                {
                    return;
                }
            }

            q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
        }

        // First Flan is spawned by default and for every 3 members entering after, an additional flan is spawned.
        // This is called on every entry, so every incremented value should be hit (the Lua only hopes so; see the
        // revisit quirk in the class header); mobOffset is additive, the initial mob spawn at a 0-value.
        public override void BattlefieldEntry(IBattlefieldContext q)
        {
            int additionalEntered = q.Battlefield.PlayerCount - 1;

            if (additionalEntered > 0 && additionalEntered % 3 == 0)
            {
                int mobOffset = additionalEntered / 3;
                var flanObj = q.Engine.MobById(FirstFlan(q) + mobOffset);

                if (flanObj != null && !flanObj.IsSpawned)
                {
                    flanObj.Spawn();
                    q.Battlefield.SetLocalVar("numSpawned", mobOffset);
                }
            }
        }
    }
}
