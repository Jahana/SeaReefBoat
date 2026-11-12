// One to be Feared (Sealion's Den, Promathia 6-4's fight), a line-for-line port of
// scripts/battlefields/Sealions_Den/one_to_be_feared.lua (quest rework C3). A BattlefieldMission of 45 minutes for six,
// trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _0w0 (index 0) and left at the
// Airship_Door, granting 1500 experience and the Ultima Undertaker title. The mission is the Promathia log's One to be
// Feared, entered by the char var Mission[6][638]Status at 3. Three phases, 21 Mammet-22 Zeta ids (seven to an area):
// five of them dead plays the event 10 to every player (title Branded by Lightning, healed, set down back on the
// airship, [OTBF]battleCompleted 1); the airship door (onExitTrigger, priority 1001) plays 32003 with its own number
// (npc id - AIRSHIP_DOOR_OFFSET + 1) and twice [OTBF]battleCompleted, whose finish with option 100 to 102 starts the
// event battleCompleted with option - 99 for every party member holding the battlefield effect (or the char alone); its
// update with option 0 spawns Omega (event 1) or Ultima (event 2). Omega dead plays 11 (title Omega Ostracizer,
// [OTBF]battleCompleted 2), Ultima dead wins. Leaving (option 4) resets [OTBF]battleCompleted. The win sets
// battlefieldWin only while the mission is current; the mission's 32001 finish (Missions/Cop/OneToBeFeared.cs) reads
// it. The exit finish's else arm (no party) is unreachable live: getParty is never nil or empty for a PC.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.SealionsDen
{
    public sealed class OneToBeFeared : BattlefieldScript
    {
        public OneToBeFeared() : base("ONE_TO_BE_FEARED") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Sealions_Den/one_to_be_feared";

        static BattlefieldMobId Mammet(int offset) => new BattlefieldMobId("MAMMET_22_ZETA", offset);

        // ZONE_IDS.mob.MAMMET_22_ZETA + 7 * (player:getBattlefield():getArea() - 1) + offset
        static int AreaMobId(IBattlefieldContext q, int offset) =>
            q.Engine.ZoneMobId(Zone.SealionsDen, "MAMMET_22_ZETA").Value +
            7 * (q.Player.CurrentBattlefield.Area - 1) + offset;

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.SealionsDen,
            BattlefieldId = Battlefield.OneToBeFeared,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(45)
            TimeLimit = 45 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.SealionsDen._0w0 },
            ExitNpcs = new[] { Npc.SealionsDen.AirshipDoor },
            MissionArea = XI.Quests.MissionLog.CoP,
            Mission = XI.QuestContent.Mission.Cop.OneToBeFeared,
            RequiredVar = "Mission[6][638]Status",
            RequiredValue = 3,
            GrantXP = 1500,
            Title = Title.UltimaUndertaker,

            OnEventUpdate =
            {
                [1] = (q, csid, option) =>
                {
                    if (option == 0)
                    {
                        // Omega
                        int omegaId = AreaMobId(q, 5);
                        if (!q.Engine.MobById(omegaId).IsSpawned) q.Engine.SpawnMob(omegaId);
                    }
                },
                [2] = (q, csid, option) =>
                {
                    if (option == 0)
                    {
                        // Ultima
                        int ultimaId = AreaMobId(q, 6);
                        if (!q.Engine.MobById(ultimaId).IsSpawned) q.Engine.SpawnMob(ultimaId);
                    }
                },
            },

            OnEventFinish =
            {
                [10] = (q, csid, option) =>
                {
                    q.Player.AddTitle(Title.BrandedByLightning);
                    HealCharacter(q.Player);
                    ReturnToAirship(q.Player);
                    q.Player.SetLocalVar("[OTBF]battleCompleted", 1);
                },
                [11] = (q, csid, option) =>
                {
                    q.Player.AddTitle(Title.OmegaOstracizer);
                    HealCharacter(q.Player);
                    ReturnToAirship(q.Player);
                    q.Player.SetLocalVar("[OTBF]battleCompleted", 2);
                },
            },

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Mammet(0), Mammet(1), Mammet(2), Mammet(3), Mammet(4) },
                        new[] { Mammet(7), Mammet(8), Mammet(9), Mammet(10), Mammet(11) },
                        new[] { Mammet(14), Mammet(15), Mammet(16), Mammet(17), Mammet(18) },
                    },
                    AllDeath = (q, mob) =>
                    {
                        foreach (var player in q.Battlefield.Players()) player.StartEvent(10);
                    },
                },
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Mammet(5) },
                        new[] { Mammet(12) },
                        new[] { Mammet(19) },
                    },
                    Spawned = false,
                    AllDeath = (q, mob) =>
                    {
                        foreach (var player in q.Battlefield.Players()) player.StartEvent(11);
                    },
                },
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { Mammet(6) },
                        new[] { Mammet(13) },
                        new[] { Mammet(20) },
                    },
                    Spawned = false,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        static void HealCharacter(IBattlefieldPlayer player)
        {
            if (!player.IsAlive) return;

            player.SetHP(player.MaxHP);
            player.SetMP(player.MaxMP);
            player.SetTP(0);

            var pet = player.Pet();
            if (pet != null)
            {
                pet.SetHP(pet.MaxHP);
                pet.SetMP(pet.MaxMP);
                pet.SetTP(0);
            }
        }

        static void ReturnToAirship(IBattlefieldPlayer player)
        {
            int area = player.CurrentBattlefield.Area;

            if (area == 1) player.SetPos(-780.010, -103.348, -86.327, 193);
            else if (area == 2) player.SetPos(-140.029, -23.348, -446.376, 193);
            else if (area == 3) player.SetPos(499.969, 56.652, -806.132, 193);

            if (player.IsDead) player.AllowSendRaisePrompt();
        }

        // Mob spawning for phase changes in this battlefield is triggered by the airship door, which is also the
        // battlefield exit (the Lua's NOTE)
        public override IBattlefieldAction OnExitTrigger(IBattlefieldContext q)
        {
            int doorOffset = q.Engine.ZoneNpcId(Zone.SealionsDen, "AIRSHIP_DOOR_OFFSET").Value;

            int doorNumber = q.Npc.Id - doorOffset + 1;
            int completed = q.Player.LocalVar("[OTBF]battleCompleted");

            return q.Engine.ProgressEvent(32003, doorNumber, completed * 2).SetPriority(1001);
        }

        public override void OnEventFinishExit(IBattlefieldContext q, int csid, int option)
        {
            var player = q.Player;

            if (option >= 100 && option <= 102)
            {
                var party = player.Party();

                if (party.Count > 0)
                {
                    foreach (var member in party)
                    {
                        if (member.StatusEffect(BattlefieldConst.EffectBattlefield) != null)
                        {
                            member.StartEvent(member.LocalVar("[OTBF]battleCompleted"), option - 99);
                        }
                    }
                }
                else
                {
                    player.StartEvent(player.LocalVar("[OTBF]battleCompleted"), option - 99);
                }
            }
            else if (option == 4)
            {
                // Leave battlefield.
                if (player.CurrentBattlefield != null)
                {
                    player.LeaveBattlefield(1);
                    player.SetLocalVar("[OTBF]battleCompleted", 0);
                }
            }
        }
    }
}
