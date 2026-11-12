// Holy Crest (Ghelsba Outpost, the San d'Oria quest The Holy Crest's fight), a line-for-line port of
// scripts/battlefields/Ghelsba_Outpost/holy_crest.lua (quest rework C3, Q3a). !pos -162 -11 78 140. A BattlefieldQuest
// of 15 minutes for six, index 1, always in area 1, entered at Hut_Door (no exit NPC). Its one group is Cyranuce M
// Cutauleon; its death wins the fight. entryRequirement: a char who has completed the quest enters, the rest need the
// Dragon Curse Remedy (the framework still asks for the quest to be accepted or completed). onEventFinishWin is the
// quest's called 32001 handler (xi.cs.questEvent).
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.GhelsbaOutpost
{
    public sealed class HolyCrest : BattlefieldScript
    {
        public HolyCrest() : base("HOLY_CREST") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Ghelsba_Outpost/holy_crest";

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.GhelsbaOutpost,
            BattlefieldId = Battlefield.HolyCrest,
            MaxPlayers = 6,
            // utils.minutes(15)
            TimeLimit = 15 * 60,
            Index = 1,
            Area = 1,
            EntryNpcs = new[] { Npc.GhelsbaOutpost.HutDoor },

            QuestArea = XI.Quests.QuestLog.Sandoria,
            Quest = "THE_HOLY_CREST",

            Groups =
            {
                new BattlefieldGroup
                {
                    MobIds = new[] { new BattlefieldMobId("CYRANUCE_M_CUTAULEON") },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        // function content:entryRequirement(player, npc, isRegistrant, trade): on the quest with the key item, or
        // having completed it
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var questStatus = q.Player.QuestStatus(XI.Quests.QuestLog.Sandoria, "THE_HOLY_CREST");

            if (questStatus == XI.Quests.QuestStatus.Completed)
            {
                return true;
            }

            return q.Player.HasKeyItem(KeyItem.DragonCurseRemedy);
        }

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Engine.QuestEvent(q, QuestKey.Sandoria.TheHolyCrest, csid, option);
    }
}
