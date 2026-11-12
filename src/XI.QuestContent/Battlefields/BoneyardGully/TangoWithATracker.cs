// Tango with a Tracker (Boneyard Gully quest battlefield), a line-for-line port of
// scripts/battlefields/Boneyard_Gully/tango_with_a_tracker.lua (quest rework C3, Q3c). A BattlefieldQuest of the
// other-areas quest Tango with a Tracker: 30 minutes for 6, index 5, entered at _081 with the Letter from Shikaree X
// (taken from the initiator only; BURSTS_INTO_FLAMES when it is missing) and left at _082, _084 or _086.
// checkRequirements replaces the framework's: the registrant is held to Battlefield.checkRequirements (the entry check
// and the key item, not the quest status of BattlefieldQuest), the rest of the party to the entry check and either the
// letter or the completed quest. The two Shikarees (Y, X) of each area die together (superlinked, their death wins
// the fight), with X's rabbit. Their skillchain AI is Shared/ShikareeSkillchain (stride 6, two Shikarees), run by
// onBattlefieldTick after the base tick. Kept as the Lua has it (named in the shared class): its reset loop resets X
// and the id after X (Requiem's +3 mob, not in this fight), never Y, so Y's magic stays off after a chain.
using System.Collections.Generic;
using XI.QuestContent.Battlefields.Shared;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BoneyardGully
{
    public sealed class TangoWithATracker : BattlefieldScript
    {
        public TangoWithATracker() : base("TANGO_WITH_A_TRACKER") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Boneyard_Gully/tango_with_a_tracker";

        const string Y = "SHIKAREE_Y_ROS_TWT";
        const string X = "SHIKAREE_X_ROS_TWT";

        // xi.mobSkill
        const int Shadowstitch = 18;
        const int Evisceration = 25;
        const int VorpalScythe = 101;
        const int SpiralHell = 104;

        static readonly ShikareeSkillchain Ai = new ShikareeSkillchain
        {
            ZoneId = Zone.BoneyardGully,
            Stride = 6,
            FirstMob = Y,
            Slots = 2,
            ResetFrom = 1,
            ResetTo = 2,
            RequireAllSlots = true,
            MessageOffsets = new[]
            {
                (X, Text.BoneyardGully.SHIKAREE_X_OFFSET),
                (Y, Text.BoneyardGully.SHIKAREE_Y_OFFSET),
            },
            Flat = new Dictionary<string, ShikareeStep[]>
            {
                // Shikaree X initiated: X Evisceration -> Y Vorpal Scythe
                [X] = new[] { new ShikareeStep(X, Evisceration), new ShikareeStep(Y, VorpalScythe) },
                // Shikaree Y initiated: Y Spiral Hell -> X Shadowstitch
                [Y] = new[] { new ShikareeStep(Y, SpiralHell), new ShikareeStep(X, Shadowstitch) },
            },
        };

        static BattlefieldMobId[] Pair(int first) => new[]
        {
            new BattlefieldMobId(Y, first),
            new BattlefieldMobId(X, first),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.BoneyardGully,
            BattlefieldId = Battlefield.TangoWithATracker,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 5,
            EntryNpcs = new[] { Npc.BoneyardGully._081 },
            ExitNpcs = new[] { Npc.BoneyardGully._082, Npc.BoneyardGully._084, Npc.BoneyardGully._086 },
            QuestArea = XI.Quests.QuestLog.OtherAreas,
            Quest = "TANGO_WITH_A_TRACKER",
            RequiredKeyItems = new BattlefieldRequiredKeyItems
            {
                Items = { KeyItem.LetterFromShikareeX },
                Message = Text.BoneyardGully.BURSTS_INTO_FLAMES,
                OnlyInitiator = true,
            },

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Pair(0), Pair(6), Pair(12) },
                    SuperlinkGroup = 1,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                // Shikaree X's rabbit
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId(X, 2) },
                        new[] { new BattlefieldMobId(X, 8) },
                        new[] { new BattlefieldMobId(X, 14) },
                    },
                    SuperlinkGroup = 1,
                },
            },
        };

        // The registrant must have the Shikaree key item to initiate the battlefield; the other players can enter
        // with the key item or with the quest completed before.
        public override bool CheckRequirements(IBattlefieldContext q, bool isRegistrant)
        {
            if (isRegistrant)
            {
                return q.Framework.CheckRequirements(isRegistrant);
            }

            if (!q.Framework.IsValidEntry())
            {
                return false;
            }

            return q.Player.HasKeyItem(KeyItem.LetterFromShikareeX) ||
                q.Player.HasCompletedQuest(XI.Quests.QuestLog.OtherAreas, "TANGO_WITH_A_TRACKER");
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            Ai.Tick(q);
        }
    }
}
