// Divine Might (Repeat) (Log ID 5, Quest ID 164), a line-for-line port of scripts/quests/outlands/Divine_Might_Repeat.lua
// (quest rework B3b Outlands, group 4). The Shrine of Ru'Avitau: blank_divine_might !pos -40 -1.6 -148.2. Ro'Maeve:
// QuHau_Spring !pos 0.2 -27.4 67.8. La'Loff Amphitheater: the Divine Might battlefield (32001). See DivineMight.cs for
// the NPCs' other users.
// Not accepted (available or completed) with Divine Might completed: holding fewer of the five earrings (hasItem, any
// container) than NUMBER_OF_DM_EARRINGS (default 1), 57 (the char var DM_Earring); its finish deletes this quest and
// begins it. Accepted: Prog 0: 58 without the Moonlight Ore, else 56 (parchment, illuminink, Ark Pentasphere); Prog 1 with
// the ore: 59; else nil. 59's update with option 2 sends the earrings; its finish: as Divine Might's 55 (options 1-5, given:
// complete, DM_Earring). Qu'Hau Spring, on the full moon from 18:00 to 05:59: illuminink and parchment alone play 7 (the
// Ark Pentasphere, confirmTrade on true); without the ore, a Chunk of Light Ore alone plays 8 (the Moonlight Ore, then
// confirmTrade). 32001 with battlefieldWin == DIVINE_MIGHT and the ore: Prog 1.
// Kept as the Lua has it: 57's finish takes no option (any escape begins it); the ore is never taken (59 completes with it
// held, so the next round skips the light ore); 8 gives the ore through npcUtil.giveKeyItem and confirms whatever it
// returns; Prog 1 without the ore answers nil.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class DivineMightRepeat : QuestScript
    {
        // xi.moonCycle.FULL_MOON (scripts/enum/moon_cycle.lua)
        const int FullMoon = 6;

        // the Lua's earringRewards, [1] .. [5]
        static readonly int[] EarringRewards =
        {
            Item.Suppanomimi,
            Item.KnightsEarring,
            Item.AbyssalEarring,
            Item.BeastlyEarring,
            Item.Bushinomimi,
        };

        public DivineMightRepeat() : base(QuestLog.Outlands, "DIVINE_MIGHT_REPEAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Divine_Might_Repeat";

        // earringRewards[option]: nil outside 1 .. 5
        static int? SelectedReward(int option) =>
            option >= 1 && option <= EarringRewards.Length ? EarringRewards[option - 1] : null;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.PentacidePerpetrator });
            b.Steps(1);

            // status ~= QUEST_ACCEPTED and Divine Might completed
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Outlands, "DIVINE_MIGHT"))
                .Or(QuestStatus.Completed, when: q => q.Player.HasCompletedQuest(QuestLog.Outlands, "DIVINE_MIGHT"))
                .Npc(Zone.TheShrineOfRuavitau, Npc.TheShrineOfRuavitau.blankdivinemight).OnTrigger(q =>
                {
                    int numEarrings = 0;
                    for (int itemId = Item.Suppanomimi; itemId <= Item.Bushinomimi; itemId++)
                    {
                        if (q.Player.HasItem(itemId))
                        {
                            numEarrings = numEarrings + 1;
                        }
                    }

                    if (numEarrings < q.Settings.MainNumber("NUMBER_OF_DM_EARRINGS"))
                    {
                        return QuestReply.ProgressEvent(57, q.Player.CharVar("DM_Earring"));
                    }
                    return null;
                })
                .OnEventFinish(Zone.TheShrineOfRuavitau, 57, q =>
                {
                    q.Player.DelQuest(QuestLog.Outlands, "DIVINE_MIGHT_REPEAT");
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TheShrineOfRuavitau, Npc.TheShrineOfRuavitau.blankdivinemight).OnTrigger(q =>
                {
                    bool hasMoonOre = q.Player.HasKeyItem(KeyItem.MoonlightOre);

                    if (q.Quest.Step == 0)
                    {
                        if (!hasMoonOre)
                        {
                            return QuestReply.Event(58);
                        }
                        else
                        {
                            return QuestReply.Event(56, Item.SheetOfParchment, Item.BottleOfIlluminink, Item.ArkPentasphere);
                        }
                    }
                    else if (hasMoonOre)
                    {
                        return QuestReply.ProgressEvent(59);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.TheShrineOfRuavitau, 59, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.UpdateEvent(Item.Suppanomimi, Item.KnightsEarring, Item.AbyssalEarring, Item.BeastlyEarring, Item.Bushinomimi);
                    }
                })
                .OnEventFinish(Zone.TheShrineOfRuavitau, 59, q =>
                {
                    int? selectedReward = SelectedReward(q.Option);

                    if (selectedReward != null &&
                        q.Player.GiveItem(selectedReward.Value))
                    {
                        q.Quest.Complete();

                        // TODO: Find a way to prevent the need for using a forever charVar here.
                        q.Player.SetCharVar("DM_Earring", selectedReward.Value);
                    }
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).OnTrade(q =>
                {
                    int vanaHour = q.Player.VanadielHour;

                    if (
                        // Player.VanadielMoonCycle (int) = getVanadielMoonCycle()
                        (q.Player.VanadielMoonCycle == FullMoon) &&
                        (vanaHour >= 18 || vanaHour < 6))
                    {
                        if (q.Trade.HasExactly(Item.BottleOfIlluminink, Item.SheetOfParchment))
                        {
                            return QuestReply.ProgressEvent(7, Item.SheetOfParchment, Item.BottleOfIlluminink);
                        }
                        else if (
                            !q.Player.HasKeyItem(KeyItem.MoonlightOre) &&
                            q.Trade.HasExactly(Item.ChunkOfLightOre))
                        {
                            return QuestReply.ProgressEvent(8);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.Romaeve, 7, q =>
                {
                    if (q.Player.GiveItem(Item.ArkPentasphere))
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.Romaeve, 8, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MoonlightOre);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.LaloffAmphitheater, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.DivineMight &&
                        q.Player.HasKeyItem(KeyItem.MoonlightOre))
                    {
                        q.Quest.SetStep(1);
                    }
                });
        }
    }
}
