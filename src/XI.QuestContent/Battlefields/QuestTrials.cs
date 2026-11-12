// The shared shape of the quest battlefields of the Cloisters' trials (quest rework C3, Q1): Trial by <element> and
// Trial-Size Trial by <element>, ported from scripts/battlefields/Cloister_of_*/trial*_by_*.lua. The files differ in
// their names, their NPCs, their boss and the quest they belong to; what they share is written once, here. Each
// battlefield keeps its own class (its constant and its LegacyPath, its slot in the containers list) and its own data
// through Trial / TrialSize. The Lua files of Frost, Gales, Storms and Tides (and every trial-size file but Earth's)
// end their fight with onEventFinishWin calling xi.cs.questEvent(player, npc, <the quest's key>, csid, option), which
// runs the C# quest's called 32001 handler; QuestEventTrial and QuestEventTrialSize do the same (the engine's
// QuestEvent). Trial by Fire and Trial-Size Trial by Earth override no such thing and extend BattlefieldScript directly
// or with their own onEventFinishWin.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields
{
    public static class QuestTrials
    {
        // BattlefieldQuest:new({ canLoseExp = false, maxPlayers = 6, timeLimit = utils.minutes(30), index = 0,
        // entryNpc, exitNpc, requiredKeyItems = { the fork }, questArea, quest }) with one group: the boss, its
        // death the win
        public static BattlefieldQuestData Trial(int zone, int battlefield, string entryNpc, string exitNpc,
                                                 int forkKeyItem, XI.Quests.QuestLog questArea, string quest,
                                                 string boss)
        {
            var data = new BattlefieldQuestData
            {
                ZoneId = zone,
                BattlefieldId = battlefield,
                CanLoseExp = false,
                MaxPlayers = 6,
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = 0,
                EntryNpcs = new[] { entryNpc },
                ExitNpcs = new[] { exitNpc },
                RequiredKeyItems = new BattlefieldRequiredKeyItems { Items = { forkKeyItem } },

                QuestArea = questArea,
                Quest = quest,
            };
            data.Groups.Add(WinOnDeath(boss));
            return data;
        }

        // BattlefieldQuest:new({ canLoseExp = false, maxPlayers = 1, levelCap = 20, timeLimit = utils.minutes(15),
        // index, entryNpc, exitNpc, requiredItems = { the mini fork }, questArea, quest }) with one group: the boss
        public static BattlefieldQuestData TrialSize(int zone, int battlefield, int index, string entryNpc,
                                                     string exitNpc, int miniForkItem,
                                                     XI.Quests.QuestLog questArea, string quest, string boss)
        {
            var data = new BattlefieldQuestData
            {
                ZoneId = zone,
                BattlefieldId = battlefield,
                CanLoseExp = false,
                MaxPlayers = 1,
                LevelCap = 20,
                // utils.minutes(15)
                TimeLimit = 15 * 60,
                Index = index,
                EntryNpcs = new[] { entryNpc },
                ExitNpcs = new[] { exitNpc },
                RequiredItems = new BattlefieldRequiredItems { Items = new[] { miniForkItem } },

                QuestArea = questArea,
                Quest = quest,
            };
            data.Groups.Add(WinOnDeath(boss));
            return data;
        }

        // { mobs = { boss }, allDeath = function(battlefield, mob)
        //   battlefield:setStatus(xi.battlefield.status.WON) end }
        private static BattlefieldGroup WinOnDeath(string boss) => new BattlefieldGroup
        {
            Mobs = new[] { boss },
            AllDeath = (q, mob) =>
            {
                q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
            },
        };
    }

    // a Trial by <element> whose onEventFinishWin is xi.cs.questEvent(player, npc, <the quest's key>, csid, option)
    public abstract class QuestEventTrial : BattlefieldScript
    {
        protected QuestEventTrial(string constName) : base(constName) { }

        // the quest's key as xi.cs.questEvent takes it ('sandoria.trial_by_ice')
        protected abstract string QuestKey { get; }

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Engine.QuestEvent(q, QuestKey, csid, option);
    }

    // a Trial-Size Trial by <element>: entryRequirement (a Summoner of level 20 or more)
    public abstract class TrialSizeBattlefield : BattlefieldScript
    {
        protected TrialSizeBattlefield(string constName) : base(constName) { }

        // function content:entryRequirement(player, npc, isRegistrant, trade)
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant) =>
            q.Player.MainJob == Job.Smn && q.Player.MainLevel >= 20;
    }

    // a Trial-Size Trial by <element> whose onEventFinishWin is xi.cs.questEvent(player, npc, <the quest's key>, csid,
    // option)
    public abstract class QuestEventTrialSize : TrialSizeBattlefield
    {
        protected QuestEventTrialSize(string constName) : base(constName) { }

        protected abstract string QuestKey { get; }

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Engine.QuestEvent(q, QuestKey, csid, option);
    }
}
