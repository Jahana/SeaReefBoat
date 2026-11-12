// Waking Dreams (The Shrouded Maw avatar battlefield), a line-for-line port of
// scripts/battlefields/The_Shrouded_Maw/waking_dreams.lua (quest rework C3, Q3c). A BattlefieldQuest of the Windurst
// quest Waking Dreams: 30 minutes for 6 (no experience lost), index 2, entered at MC_Entrance with the Vial of Dream
// Incense (not consumed on entry) and left at Memento_Circle. Each area has a Diabolos (its death wins the fight) and
// six superlinked Diremites; setupBattlefield closes the doors of the area's eight tiles and onBattlefieldTick runs
// the Diremite respawn and pit logic, both in Shared/DiremitePits (the same as Darkness Named's).
// The eighth entry update parameter (csParam8) and the eighth win parameter pick Diabolos's lines: 5 for a char who has
// completed the quest, 4 for one who has completed Dawn, else 0. A win sets battlefieldWin only while the quest is
// accepted, and the cutscene is never offered to skip (checkSkipCutscene is false, the seventh parameter 0).
// onEventFinishWin replaces the framework's: the vial is taken and the Whisper of Dreams given, with the title.
using XI.QuestContent.Battlefields.Shared;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TheShroudedMaw
{
    public sealed class WakingDreams : BattlefieldScript
    {
        public WakingDreams() : base("WAKING_DREAMS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/The_Shrouded_Maw/waking_dreams";

        static BattlefieldMobId[] Mites(int first) => new[]
        {
            new BattlefieldMobId("DIABOLOS_WD", first),
            new BattlefieldMobId("DIABOLOS_WD", first + 1),
            new BattlefieldMobId("DIABOLOS_WD", first + 2),
            new BattlefieldMobId("DIABOLOS_WD", first + 3),
            new BattlefieldMobId("DIABOLOS_WD", first + 4),
            new BattlefieldMobId("DIABOLOS_WD", first + 5),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.TheShroudedMaw,
            BattlefieldId = Battlefield.WakingDreams,
            CanLoseExp = false,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 2,
            EntryNpcs = new[] { Npc.TheShroudedMaw.MCEntrance },
            ExitNpcs = new[] { Npc.TheShroudedMaw.MementoCircle },
            RequiredKeyItems = new BattlefieldRequiredKeyItems
            {
                Items = { KeyItem.VialOfDreamIncense },
                Keep = true,
            },
            QuestArea = XI.Quests.QuestLog.Windurst,
            Quest = "WAKING_DREAMS",

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("DIABOLOS_WD", 0) },
                        new[] { new BattlefieldMobId("DIABOLOS_WD", 7) },
                        new[] { new BattlefieldMobId("DIABOLOS_WD", 14) },
                    },
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Mites(1), Mites(8), Mites(15) },
                    Superlink = true,
                },
            },
        };

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            DiremitePits.CloseTiles(q);
        }

        // Retail never offers to skip either cutscene, even on a rematch.
        public override bool CheckSkipCutscene(IBattlefieldContext q)
        {
            return false;
        }

        // 0 is silent for players still in the missions. 4 is a first clear with CoP done. 5 is a rematch.
        static int Speech(IBattlefieldContext q)
        {
            if (q.Player.HasCompletedQuest(XI.Quests.QuestLog.Windurst, "WAKING_DREAMS"))
            {
                return 5;
            }

            if (q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.Dawn))
            {
                return 4;
            }

            return 0;
        }

        public override int OnEntryEventUpdate(IBattlefieldContext q, int csid, int option)
        {
            Data.CsParam8 = Speech(q);

            return base.OnEntryEventUpdate(q, csid, option);
        }

        public override void OnBattlefieldWin(IBattlefieldContext q)
        {
            var player = q.Player;
            var battlefield = q.Battlefield;

            if (player.QuestStatus(XI.Quests.QuestLog.Windurst, "WAKING_DREAMS") == XI.Quests.QuestStatus.Accepted)
            {
                player.SetLocalVar("battlefieldWin", battlefield.Id);
            }

            var record = battlefield.Record();

            player.StartEvent(32001, battlefield.Area, record.ClearTime, record.PartySize, battlefield.TimeInside,
                player.ZoneId, Data.Index, 0, Speech(q));
        }

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            var player = q.Player;

            player.DelKeyItem(KeyItem.VialOfDreamIncense);
            player.GiveKeyItem(KeyItem.WhisperOfDreams);
            player.AddTitle(Title.HeirToTheRealmOfDreams);
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            DiremitePits.Tick(q, "DIABOLOS_WD");
        }
    }
}
