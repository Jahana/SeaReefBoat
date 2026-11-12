// Shadow Lord Battle (Throne Room, the nations' 5-2 Shadow Lord fight), a line-for-line port of
// scripts/battlefields/Throne_Room/shadow_lord_battle.lua (quest rework C3 N4). !pos -111 -6 0.1 165. A
// BattlefieldMission of 30 minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL,
// entered at _4l1 (index 0) and left at _4l2, _4l3 or _4l4, granting the Shadow Banisher title. The mission is the
// nation's SHADOW_LORD (15) of the char's own log, entered at status 3. Phase 1 (SHADOW_LORD_RANK_5_OFFSET + area - 1)
// dead (death): every player of the battlefield gets 32004 (area). onEventFinishBattlefield (32004's and 32005's
// finish: the framework routes both there, and neither the Lua's hook nor this one reads the csid): unless phase 2
// (OFFSET + area + 2) is already up, phase 1 is despawned and phase 2 spawned, with the player's enmity
// (updateEnmity). Phase 2 (not spawned at the start) dead (death): WON. onBattlefieldTick (after the base tick), while
// LOCKED and phase 2 alive: with its local var [ShadowLord]Defeated set, once GetSystemTime reaches
// [ShadowLord]DefeatTime (or Defeated + 30 when the mobskill never stamped it) phase 2 is made killable and set to 0
// HP. The win sets battlefieldWin only while the mission is current; the missions' 32001 finishes
// (Missions/*/XarcabardLandOfTruths.cs, TheShadowLord.cs, TheShadowAwaits.cs) read it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.ThroneRoom
{
    public sealed class ShadowLordBattle : BattlefieldScript
    {
        public ShadowLordBattle() : base("SHADOW_LORD_BATTLE") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Throne_Room/shadow_lord_battle";

        const string ShadowLord = Mob.ThroneRoom.SHADOW_LORD_RANK_5_OFFSET;

        static BattlefieldMobId S(int offset) => new BattlefieldMobId(ShadowLord, offset);

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.ThroneRoom,
            BattlefieldId = Battlefield.ShadowLordBattle,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.ThroneRoom._4l1 },
            ExitNpcs = new[] { Npc.ThroneRoom._4l2, Npc.ThroneRoom._4l3, Npc.ThroneRoom._4l4 },
            // xi.mission.id.nation.SHADOW_LORD, of the char's nation's log (missionArea nil)
            Mission = 15,
            RequiredMissionStatus = 3,
            Title = Title.ShadowBanisher,

            Groups =
            {
                // Phase 1
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { S(0) },
                        new[] { S(1) },
                        new[] { S(2) },
                    },
                    Death = (q, mob, deathCount) =>
                    {
                        var players = q.Battlefield.Players();

                        foreach (var player in players)
                        {
                            player.StartEvent(32004, q.Battlefield.Area);
                        }
                    },
                },

                // Phase 2
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { S(3) },
                        new[] { S(4) },
                        new[] { S(5) },
                    },
                    Spawned = false,
                    Death = (q, mob, deathCount) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        public override void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option)
        {
            var battlefield = q.Player.CurrentBattlefield;
            int area = battlefield.Area;
            int offset = q.Engine.ZoneMobId(Zone.ThroneRoom, ShadowLord).Value;
            int phaseTwoId = offset + area + 2;
            var phaseTwo = q.Engine.MobById(phaseTwoId);

            if (phaseTwo != null && phaseTwo.IsSpawned)
            {
                return;
            }

            q.Engine.DespawnMob(offset + area - 1);

            // The Lua's comment: "first phase dies, spawn second phase ID, make him engage, and disable magic, auto
            // attack, and abilities (all he does is case Implode by script)". The Lua and this code only spawn him and
            // call updateEnmity with the player.
            var mob = q.Engine.SpawnMob(phaseTwoId);
            if (mob != null)
            {
                mob.UpdateEnmity(q.Player);
            }
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            var battlefield = q.Battlefield;
            if (battlefield.Status != BattlefieldConst.StatusLocked)
            {
                return;
            }

            var phaseTwo = q.Engine.MobById(
                q.Engine.ZoneMobId(Zone.ThroneRoom, ShadowLord).Value + battlefield.Area + 2);
            if (phaseTwo == null || !phaseTwo.IsAlive)
            {
                return;
            }

            uint defeated = phaseTwo.LocalVar("[ShadowLord]Defeated");
            if (defeated == 0)
            {
                return;
            }

            uint defeatTime = phaseTwo.LocalVar("[ShadowLord]DefeatTime");

            // The mobskill stamps DefeatTime when the animation starts.
            // A wipe before his next tp move leaves it unset.
            if (defeatTime == 0)
            {
                defeatTime = defeated + 30;
            }

            if (q.Engine.GetSystemTime() < defeatTime)
            {
                return;
            }

            phaseTwo.SetUnkillable(false);
            phaseTwo.SetHP(0);
        }
    }
}
