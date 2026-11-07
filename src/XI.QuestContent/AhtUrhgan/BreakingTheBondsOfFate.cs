// Breaking the Bonds of Fate (COR LB; Log ID 6, Quest ID 41), a line-for-line port of
// scripts/quests/ahtUrhgan/COR_LB_Breaking_the_Bonds_of_Fate.lua (quest rework B3b Aht Urhgan, group 2). Arrapago Reef: qm6
// !pos 469 -12 112 (no script; default NOTHING_OUT_OF_ORDINARY). Talacca Cove: the battlefield Breaking the Bonds of Fate
// (scripts/battlefields/Talacca_Cove/breaking_the_bonds_of_fate.lua: index 3, entry _1l0 by the Corsair's Testimony
// (requiredItems), COR 66, one char, requiredVar Prog 1; Qultada of area 1 is QULTADA).
// Available with Against All Odds completed, to a COR of level 66: qm6 234, whose option 0 begins it. Accepted as a COR
// of level 66: a trade holding a Corsair's Testimony to qm6: 235 ({ [7] = 1 }), whose option 0 sets Prog 1 and setPos
// into Talacca Cove; qm6's trigger: YOU_MUST_BRING (the testimony). The battlefield's 32001 with battlefieldWin 1091:
// a level cap of 70 raised to 75 (YOUR_LEVEL_LIMIT_IS_NOW_75), a Scroll of Instant Warp, completed (Master of Chance).
// Kept as the Lua has it: 235 sets Prog 1 at any Prog; the scroll's giveItem unchecked.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class BreakingTheBondsOfFate : QuestScript
    {
        public BreakingTheBondsOfFate() : base(QuestLog.AhtUrghan, "BREAKING_THE_BONDS_OF_FATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/COR_LB_Breaking_the_Bonds_of_Fate";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.MasterOfChance });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "AGAINST_ALL_ODDS") &&
                    q.Player.MainJob == Job.Cor &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q => QuestReply.ProgressEvent(234))
                .OnEventFinish(Zone.ArrapagoReef, 234, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.MainJob == Job.Cor &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6)
                    .OnTrade(q =>
                    {
                        if (q.Trade.ItemQty(Item.CorsairsTestimony) > 0)
                        {
                            return QuestReply.ProgressEvent(235).Option(7, 1);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.MessageSpecial(Text.ArrapagoReef.YOU_MUST_BRING, Item.CorsairsTestimony))
                .OnEventFinish(Zone.ArrapagoReef, 235, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                        q.Player.SetPos(-100.166, -7.701, -91.059, 197, Zone.TalaccaCove);
                    }
                })
                .OnEventFinish(Zone.TalaccaCove, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.BreakingTheBondsOfFate)
                    {
                        if (q.Player.LevelCap == 70)
                        {
                            q.Player.SetLevelCap(75);
                            q.Text.Special(Text.TalaccaCove.YOUR_LEVEL_LIMIT_IS_NOW_75);
                        }

                        // npcUtil.giveItem(player, { xi.item.SCROLL_OF_INSTANT_WARP }): the table form gives the same as the bare id
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                        q.Quest.Complete();
                    }
                });
        }
    }
}
