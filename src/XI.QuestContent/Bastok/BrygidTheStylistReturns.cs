// Brygid the Stylist Returns (Log ID 1, Quest ID 74), a line-for-line port of
// scripts/quests/bastok/Brygid_the_Stylist_Returns.lua (quest rework B3b). Brygid : !pos -90 -4 -108 235. The two
// requests are drawn with the script RNG (math.randomInt) from the pieces the main job can wear, body first, as the Lua
// draws them. Deliberate difference from upstream Lua (user ruling 2026-11-05, kept bugs): a trade while accepted
// before an option is kept looked up option 0, which the table has not, and raised ("attempt to index a nil value");
// the trade now answers nothing for an option the table has not, as the 382 finish keeps only an option the table has.
using System;
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BrygidTheStylistReturns : QuestScript
    {
        public BrygidTheStylistReturns() : base(QuestLog.Bastok, "BRYGID_THE_STYLIST_RETURNS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Brygid_the_Stylist_Returns";

        private static readonly int[] RequestedBodyItems =
        {
            Item.Breastplate, Item.SilverMail, Item.BandedMail, Item.CuirBouilli, Item.RaptorJerkin, Item.PaddedArmor,
            Item.Gambison, Item.WoolGambison, Item.VelvetRobe, Item.SilkCoat, Item.Cloak, Item.WhiteCloak, Item.BeakJerkin,
            Item.CarapaceHarness, Item.PyroRobe, Item.FrostRobe, Item.LinenDoublet, Item.WoolDoublet, Item.IronScaleMail,
            Item.BishopsRobe,
        };

        private static readonly int[] RequestedLegItems =
        {
            Item.Cuisses, Item.SilverHose, Item.Breeches, Item.CuirTrousers, Item.RaptorTrousers, Item.BeakTrousers,
            Item.IronSubligar, Item.CarapaceSubligar, Item.ScorpionSubligar, Item.Hose, Item.WoolHose, Item.VelvetSlops,
            Item.SilkSlops, Item.LinenSlacks, Item.WhiteSlacks, Item.IronCuisses,
        };

        // [option] = (reward, required)
        private static readonly Dictionary<int, (int Reward, int Required)> OptionToItems = new Dictionary<int, (int, int)>
        {
            [1] = (Item.DuendeCotehardie, Item.AriesSubligar),
            [2] = (Item.NokizaruGi, Item.TaurusSubligar),
            [3] = (Item.RappareeHarness, Item.GeminiSubligar),
            [4] = (Item.ShinimushaHaraAte, Item.CancerSubligar),
            [5] = (Item.WyvernMail, Item.LeoSubligar),
            [6] = (Item.ShikareeAketon, Item.VirgoSubligar),
            [7] = (Item.CeriseDoublet, Item.LibraSubligar),
            [8] = (Item.GlamorJupon, Item.ScorpiusSubligar),
            [9] = (Item.GloomBreastplate, Item.SagittariusSubligar),
            [10] = (Item.NimbusDoublet, Item.CapricornusSubligar),
            [11] = (Item.AikidoGi, Item.AquariusSubligar),
            [12] = (Item.ParadeCuirass, Item.PiscesSubligar),
            [13] = (Item.GaudyHarness, Item.OphiuchusSubligar),
        };

        // optionToItems[option], raising for an option the table has not, as the Lua's index of nil does (the trigger's
        // 385 arm and the 383 finish, which only a kept option reaches)
        private static (int Reward, int Required) ItemsOf(int option) =>
            OptionToItems.TryGetValue(option, out var items)
                ? items
                : throw new InvalidOperationException($"optionToItems[{option}] is nil (attempt to index a nil value)");

        // the items of the list the player can wear (the level not checked), then one of them at random
        private static int RandomEquippableItem(IQuestContext q, int[] itemList)
        {
            var equippableItems = new List<int>();
            foreach (int itemId in itemList)
            {
                if (q.Player.CanEquipItem(itemId, false))
                {
                    equippableItems.Add(itemId);
                }
            }

            // an empty list (unreachable: the quest's job mask leaves every job an item) throws here; the Lua's
            // index is nil and its setVar(player, 'requestedBody', nil) errors: both stop before any var is written
            return equippableItems[q.Player.RandomInt(1, equippableItems.Count) - 1];
        }

        // any artifact armor worn, head to feet
        private static bool HasArtifactArmorEquipped(IQuestCheck q)
        {
            for (int equipSlot = Slot.Head; equipSlot <= Slot.Feet; equipSlot++)
            {
                if (q.Player.IsArtifactArmor(q.Player.EquipId(equipSlot)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Title = Title.BastoksSecondBestDressed });

            // not accepted, Brygid the Stylist completed, artifact armor worn: the requests, then the quest (re)begun
            b.State(QuestStatus.Available, when: Opens)
                .Or(QuestStatus.Completed, when: Opens)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Brygid).OnTrigger(q =>
                {
                    int hasRobeEquipped = q.Player.EquipId(Slot.Body) == Item.Robe ? 1 : 0;
                    int requestedBody = RandomEquippableItem(q, RequestedBodyItems);
                    int requestedLegs = RandomEquippableItem(q, RequestedLegItems);

                    q.Quest.SetVar("requestedBody", requestedBody);
                    q.Quest.SetVar("requestedLegs", requestedLegs);

                    return QuestReply.ProgressEvent(380, hasRobeEquipped, requestedBody, requestedLegs, q.Player.MainJob);
                })
                .OnEventFinish(Zone.BastokMarkets, 380, q =>
                {
                    q.Player.DelQuest(QuestLog.Bastok, "BRYGID_THE_STYLIST_RETURNS");
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Brygid)
                    // a deliberate difference from upstream Lua (see the header): no option kept, no answer, no error
                    .OnTrade(q => OptionToItems.TryGetValue(q.Quest.Var("Option"), out var items) &&
                                  q.Trade.Matches((items.Required, 1))
                        ? QuestReply.ProgressEvent(383)
                        : null)
                    .OnTrigger(q =>
                    {
                        int questOption = q.Quest.Var("Option");

                        if (questOption == 0)
                        {
                            if (q.Player.EquipId(Slot.Body) == q.Quest.Var("requestedBody") &&
                                q.Player.EquipId(Slot.Legs) == q.Quest.Var("requestedLegs"))
                            {
                                return QuestReply.ProgressEvent(382);
                            }
                            else
                            {
                                int hasRobeEquipped = q.Player.EquipId(Slot.Body) == Item.Robe ? 1 : 0;
                                int requestedBody = q.Quest.Var("requestedBody");
                                int requestedLegs = q.Quest.Var("requestedLegs");

                                return QuestReply.Event(381, hasRobeEquipped, requestedBody, requestedLegs, q.Player.MainJob);
                            }
                        }
                        else
                        {
                            var optionList = ItemsOf(questOption);

                            return QuestReply.Event(385, 0, optionList.Reward, optionList.Required);
                        }
                    })
                // the reward of the option looked at: (0, option - 1, not held, wearable at the level)
                .OnEventUpdate(Zone.BastokMarkets, 382, q =>
                {
                    int rewardItem = Item.DuendeCotehardie - 1 + q.Option;
                    int canEquipReward = q.Player.CanEquipItem(rewardItem, true) ? 1 : 0;
                    int hasReward = !q.Player.HasItem(rewardItem) ? 1 : 0;

                    q.Player.UpdateEvent(0, q.Option - 1, hasReward, canEquipReward);
                })
                // an option of the table kept (the range check keeps a stray value from locking the client later)
                .OnEventFinish(Zone.BastokMarkets, 382, q =>
                {
                    if (OptionToItems.ContainsKey(q.Option))
                    {
                        q.Quest.SetVar("Option", q.Option);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 383, q =>
                {
                    if (q.Player.GiveItem(ItemsOf(q.Quest.Var("Option")).Reward))
                    {
                        q.Trade.Complete();
                        q.Quest.Complete();
                    }
                });
        }

        private static bool Opens(IQuestCheck q) =>
            q.Player.HasCompletedQuest(QuestLog.Bastok, "BRYGID_THE_STYLIST") && HasArtifactArmorEquipped(q);
    }
}
