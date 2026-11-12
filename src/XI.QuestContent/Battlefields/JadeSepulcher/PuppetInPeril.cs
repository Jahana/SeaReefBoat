// Puppet in Peril (Jade Sepulcher, ToAU 29's fight), a line-for-line port of
// scripts/battlefields/Jade_Sepulcher/puppet_in_peril.lua (quest rework C3 T3). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, index 4, only area 1 allowed, entered
// at _1v0 and left at _1v1, _1v2 or _1v3. The mission is ToAU's Puppet in Peril with its status read from the ToAU
// log; a registrant needs status 1. Its one group, Lancelord Gaheel Ja (one list per area: the id and the ids 1 and 2
// after it), wins the fight when it dies. The win sets battlefieldWin only while the mission is current, and the
// mission's 32001 finish (Missions/Toau/PuppetInPeril.cs) reads it.
// NOTE (Lua): Lancelord Gaheel Ja is missing logic in its mob script for text displayed throughout the fight at minimum
// and may be missing additional mob mechanics.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.JadeSepulcher
{
    public sealed class PuppetInPeril : BattlefieldScript
    {
        public PuppetInPeril() : base("PUPPET_IN_PERIL") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Jade_Sepulcher/puppet_in_peril";

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.JadeSepulcher,
            BattlefieldId = Battlefield.PuppetInPeril,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 4,
            AllowedAreas = new[] { 1 },
            EntryNpcs = new[] { Npc.JadeSepulcher._1v0 },
            ExitNpcs = new[]
            {
                Npc.JadeSepulcher._1v1,
                Npc.JadeSepulcher._1v2,
                Npc.JadeSepulcher._1v3,
            },
            MissionArea = XI.Quests.MissionLog.ToAU,
            Mission = XI.QuestContent.Mission.Toau.PuppetInPeril,
            MissionStatusArea = XI.Quests.MissionLog.ToAU,
            RequiredMissionStatus = 1,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("LANCELORD_GAHEEL_JA") },
                        new[] { new BattlefieldMobId("LANCELORD_GAHEEL_JA", 1) },
                        new[] { new BattlefieldMobId("LANCELORD_GAHEEL_JA", 2) },
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
