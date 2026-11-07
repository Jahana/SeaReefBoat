// Achieving True Power (Log ID 1, Quest ID 85), a line-for-line port of scripts/quests/bastok/PUP_LB_Achieving_True_Power.lua
// (quest rework B3b). Shamarhaan : Bastok Markets. Shamarhaan's 441 (Puppetmaster Blues completed, main job PUP, main
// level 66; the automaton's model id as its argument) begins it; while accepted (and PUP 66) his 442, and a
// Puppetmaster's Testimony traded plays 443 (Prog 1, the char moved into the Navukgo Execution Chamber); the battlefield
// (scripts/battlefields/Navukgo_Execution_Chamber/achieving_true_power.lua, requiredVar 'Quest[1][85]Prog' at 1: a status
// read, the var name and value stay) and its win's 32001 raise a level cap of 70 to 75 (YOUR_LEVEL_LIMIT_IS_NOW_75), give
// a Scroll of Instant Warp and complete it (Master of Manipulation). Once completed Shamarhaan plays 444 (replaceDefault).
// Lamepaue (Bastok Markets) and the Monstrosity table (globals/monstrosity/teyrnon.lua) read its completion.
// Kept as the Lua file has it: 443 does not take the testimony (the battlefield's requiredItems wears it); the scroll's
// give and quest:complete are unchecked, so with a full inventory the quest still completes without the scroll; 441's
// finish returns quest:begin's value, which the lookup ignores.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AchievingTruePower : QuestScript
    {
        public AchievingTruePower() : base(QuestLog.Bastok, "ACHIEVING_TRUE_POWER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/PUP_LB_Achieving_True_Power";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.MasterOfManipulation });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PUPPETMASTER_BLUES") &&
                    q.Player.MainJob == Job.Pup &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan).OnTrigger(q =>
                    QuestReply.ProgressEvent(441, q.Player.AutomatonModelId))
                .OnEventFinish(Zone.BastokMarkets, 441, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted, when: q => q.Player.MainJob == Job.Pup && q.Player.MainLevel >= 66)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan)
                    .OnTrade(q => q.Trade.ItemQty(Item.PuppetmastersTestimony) > 0 ? QuestReply.ProgressEvent(443) : null)
                    .OnTrigger(q => QuestReply.Event(442))
                .OnEventFinish(Zone.BastokMarkets, 443, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.SetPos(-659.106, -9.126, -194.577, 213, Zone.NavukgoExecutionChamber);
                })
                .OnEventFinish(Zone.NavukgoExecutionChamber, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.AchievingTruePower)
                    {
                        if (q.Player.LevelCap == 70)
                        {
                            q.Player.SetLevelCap(75);
                            q.Text.Special(Text.NavukgoExecutionChamber.YOUR_LEVEL_LIMIT_IS_NOW_75);
                        }

                        // npcUtil.giveItem(player, { xi.item.SCROLL_OF_INSTANT_WARP }): the table form gives the same as the bare id
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                        q.Quest.Complete();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan).OnTrigger(q => QuestReply.Event(444).ReplaceDefault());
        }
    }
}
