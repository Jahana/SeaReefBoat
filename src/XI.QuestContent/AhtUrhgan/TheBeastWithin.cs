// The Beast Within (BLU LB; Log ID 6, Quest ID 40), a line-for-line port of
// scripts/quests/ahtUrhgan/BLU_LB_The_Beast_Within.lua (quest rework B3b Aht Urhgan, group 1). Aht Urhgan Whitegate:
// Waoud !pos 65 -6 -78 (no script; default 61), Imperial Whitegate !pos 154 -4 0 (no script; default
// GATE_IS_FIRMLY_CLOSED). Jade Sepulcher: the zone-in, the battlefield The Beast Within
// (scripts/battlefields/Jade_Sepulcher/the_beast_within.lua: index 2, entry _1v0 by the Blue Mage's Testimony
// (requiredItems), BLU 66, one char, requiredVar Prog 2; Raubahn of area 1 is RAUBAHN). Available with Transformations
// completed, main job BLU at level 66: Waoud nil while GetSystemTime() < Timer (Transformations' 5 sets it), else 760,
// whose option 0 begins it. Accepted as a BLU of level 66: Waoud 761 (its update option 0: updateEvent(gil); its finish
// option 1 with 1000 gil: PAY_DIVINATION); a trade holding a Blue Mage's Testimony to Imperial Whitegate: 763 at Prog
// 2, else 762; 762 option 0: Prog 1 and setPos into Jade Sepulcher; 763 option 0: setPos only. Jade Sepulcher: the
// zone-in at Prog 1 plays 2 (Prog 2); the battlefield's 32001 with battlefieldWin 1154 starts 6, whose finish raises a
// level cap of 70 to 75 (YOUR_LEVEL_LIMIT_IS_NOW_75), gives a Scroll of Instant Warp and completes it (Master of
// Ambition).
// Kept as the Lua has it: 760's begin on option 0 (its return value unused); 762 sets Prog 1 at any Prog; the scroll's
// giveItem unchecked (completed with a full bag); 6's finish completes it whatever its option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class TheBeastWithin : QuestScript
    {
        public TheBeastWithin() : base(QuestLog.AhtUrghan, "THE_BEAST_WITHIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/BLU_LB_The_Beast_Within";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.MasterOfAmbition });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "TRANSFORMATIONS") &&
                    q.Player.MainJob == Job.Blu &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (q.Player.SystemTime < q.Quest.Var("Timer"))
                    {
                        return null;
                    }

                    return QuestReply.ProgressEvent(760);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 760, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.MainJob == Job.Blu &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(QuestReply.ProgressEvent(761))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrade(q =>
                {
                    if (q.Trade.ItemQty(Item.BlueMagesTestimony) > 0)
                    {
                        if (q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(763);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(762);
                        }
                    }
                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 761, q =>
                {
                    if (q.Option == 0)
                    {
                        // The event automtically gives the correct option depending on the player's gil.
                        q.Player.UpdateEvent(q.Player.Gil);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 761, q =>
                {
                    if (q.Option != 1)
                    {
                        return;
                    }

                    if (q.Player.Gil < 1000)
                    {
                        return;
                    }

                    q.Player.DelGil(1000);
                    q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 762, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                        // Teleport to Jade Sepulcher
                        q.Player.SetPos(300.280, -0.853, -177.745, 63, Zone.JadeSepulcher);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 763, q =>
                {
                    if (q.Option == 0)
                    {
                        // Teleport to Jade Sepulcher
                        q.Player.SetPos(300.280, -0.853, -177.745, 63, Zone.JadeSepulcher);
                    }
                })
                .OnZoneIn(Zone.JadeSepulcher, q => q.Quest.Step == 1 ? QuestReply.Event(2) : null)
                .OnEventFinish(Zone.JadeSepulcher, 2, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.JadeSepulcher, 6, q =>
                {
                    if (q.Player.LevelCap == 70)
                    {
                        q.Player.SetLevelCap(75);
                        q.Text.Special(Text.JadeSepulcher.YOUR_LEVEL_LIMIT_IS_NOW_75);
                    }

                    // npcUtil.giveItem(player, { xi.item.SCROLL_OF_INSTANT_WARP }): the table form gives the same
                    // as the bare id
                    q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.JadeSepulcher, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.BeastWithin)
                    {
                        q.Player.StartEvent(6);
                    }
                });
        }
    }
}
