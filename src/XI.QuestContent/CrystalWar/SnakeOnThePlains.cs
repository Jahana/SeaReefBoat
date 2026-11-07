// Snake on the Plains (Log ID 7, Quest ID 8), a line-for-line port of
// scripts/quests/crystalWar/WOTG_WIN_0_Snake_on_the_Plains.lua (quest rework B3b Crystal War, group 1). Windurst Waters
// [S]: Miah Riyuh !pos 5.3 -2 37.5 (no script, no default). West Sarutabaruta [S]: Sealed Entrance 1 / 2 / 3 !pos -245
// -18.1 660, 263.6 -6.5 40, -340 1.8 -364.8 (no scripts, no defaults).
// Available: Miah Riyuh: with Steamed Rams or The Fighting Fourth accepted, 122 (an Event); with the Green
// Recommendation Letter, 103; with LetterUsed 1 or an allegiance, 105; else 121 (an Event). 103 on option 0 begins it,
// gives Zonpa-Zippa's All-Purpose Putty, sets LetterUsed and takes the letter; on option 1 sets LetterUsed and takes the
// letter. 105 on option 0 begins it and gives the putty. The entrances: DOOR_OFFSET + 3 (a message). Accepted: Miah
// Riyuh: 122 as above; all three Doors bits: 106; with the putty: 104 ([7] = the bits set); else 121. 104 on option 1
// deletes the quest, takes the putty, Doors 0. 106 on option 0: with no allegiance the Sprinter's Shoes first (nothing
// more when they do not fit); completed (Cobra Unit Mercenary), the Bronze Ribbon of Service (no allegiance),
// allegiance 3, NOW_ALLIED_WITH (3). An entrance with the putty: its bit (Sealed_Entrance_n: bit n - 1) already set:
// DOOR_OFFSET + 2 (putty); else the bit set; all three: the putty taken, DOOR_OFFSET + 4 (putty); else DOOR_OFFSET + 1
// (putty). Without the putty: DOOR_OFFSET + 3. Completed: Miah Riyuh: 122 as above; allegiance 3: 107; else 121
// (events). The entrances: DOOR_OFFSET + 2 (putty), replaceDefault.
// Kept as the Lua has it: LetterUsed survives delQuest (104 resets Doors itself; LetterUsed stays, so the next try is 105
// without a letter); 103 on option 1 spends the letter without beginning; 104's [7] counts bits 0 to 2; the entrance
// handlers read the Doors mask before their bit test, as sealDoor does; the Lua reads an entrance's bit from its name's
// last digit (one C# handler per entrance, with its bit).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class SnakeOnThePlains : QuestScript
    {
        public SnakeOnThePlains() : base(QuestLog.CrystalWar, "SNAKE_ON_THE_PLAINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_WIN_0_Snake_on_the_Plains";

        static bool OtherNationAccepted(IQuestContext q) =>
            q.Player.QuestStatus(QuestLog.CrystalWar, "STEAMED_RAMS") == QuestStatus.Accepted ||
            q.Player.QuestStatus(QuestLog.CrystalWar, "THE_FIGHTING_FOURTH") == QuestStatus.Accepted;

        // utils.mask.getBit(mask, bitNum)
        static bool GetBit(int mask, int bitNum) => (mask & (1 << bitNum)) != 0;

        // utils.mask.isFull(mask, 3): bits 0 to 2 all set
        static bool IsFull(int mask) => (mask & 7) == 7;

        // westSarutabarutaID.text.DOOR_OFFSET + n
        static int DoorText(IQuestContext q, int n) => q.Text.Id(Text.WestSarutabarutaS.DOOR_OFFSET) + n;

        static QuestReply SealDoor(IQuestContext q, int bitNum)
        {
            int doors = q.Quest.Var("Doors");

            if (GetBit(doors, bitNum))
            {
                return QuestReply.MessageSpecial(DoorText(q, 2), KeyItem.ZonpaZippasAllPurposePutty);
            }

            int sealedDoors = doors | (1 << bitNum);
            q.Quest.SetVar("Doors", sealedDoors);

            if (IsFull(sealedDoors))
            {
                q.Player.DelKeyItem(KeyItem.ZonpaZippasAllPurposePutty);

                return QuestReply.MessageSpecial(DoorText(q, 4), KeyItem.ZonpaZippasAllPurposePutty);
            }

            return QuestReply.MessageSpecial(DoorText(q, 1), KeyItem.ZonpaZippasAllPurposePutty);
        }

        // Sealed_Entrance_1/2/3 map to door bits 0/1/2.
        static QuestReply SealedEntrance(IQuestContext q, int bitNum)
        {
            if (q.Player.HasKeyItem(KeyItem.ZonpaZippasAllPurposePutty))
            {
                return SealDoor(q, bitNum);
            }

            return QuestReply.MessageSpecial(DoorText(q, 3));
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.CobraUnitMercenary,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.MiahRiyuh).OnTrigger(q =>
                {
                    if (OtherNationAccepted(q))
                    {
                        return QuestReply.Event(122);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.GreenRecommendationLetter))
                    {
                        return QuestReply.ProgressEvent(103);
                    }
                    else if (q.Quest.Var("LetterUsed") == 1 || q.Player.CampaignAllegiance > 0)
                    {
                        // Already spent a letter on a previous attempt, no second letter needed.
                        return QuestReply.ProgressEvent(105);
                    }
                    else
                    {
                        return QuestReply.Event(121);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 103, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ZonpaZippasAllPurposePutty);
                        q.Quest.SetVar("LetterUsed", 1);
                        q.Player.DelKeyItem(KeyItem.GreenRecommendationLetter);
                    }
                    else if (q.Option == 1)
                    {
                        q.Quest.SetVar("LetterUsed", 1);
                        q.Player.DelKeyItem(KeyItem.GreenRecommendationLetter);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 105, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ZonpaZippasAllPurposePutty);
                    }
                })
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance1).OnTrigger(q => QuestReply.MessageSpecial(DoorText(q, 3)))
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance2).OnTrigger(q => QuestReply.MessageSpecial(DoorText(q, 3)))
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance3).OnTrigger(q => QuestReply.MessageSpecial(DoorText(q, 3)));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.MiahRiyuh).OnTrigger(q =>
                {
                    if (OtherNationAccepted(q))
                    {
                        return QuestReply.Event(122);
                    }

                    int doors = q.Quest.Var("Doors");

                    if (IsFull(doors))
                    {
                        return QuestReply.ProgressEvent(106);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ZonpaZippasAllPurposePutty))
                    {
                        int puttyUsed = 0;
                        for (int bitNum = 0; bitNum <= 2; bitNum++)
                        {
                            if (GetBit(doors, bitNum))
                            {
                                puttyUsed = puttyUsed + 1;
                            }
                        }

                        return QuestReply.ProgressEvent(104).Option(7, puttyUsed);
                    }
                    else
                    {
                        return QuestReply.Event(121);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 104, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.CrystalWar, "SNAKE_ON_THE_PLAINS");
                        q.Player.DelKeyItem(KeyItem.ZonpaZippasAllPurposePutty);
                        q.Quest.SetVar("Doors", 0);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 106, q =>
                {
                    if (q.Option != 0)
                    {
                        return;
                    }

                    bool hasNoAllegiance = q.Player.CampaignAllegiance == 0;

                    if (hasNoAllegiance && !q.Player.GiveItem(Item.SprintersShoes))
                    {
                        return;
                    }

                    if (q.Quest.Complete())
                    {
                        if (hasNoAllegiance)
                        {
                            q.Player.GiveKeyItem(KeyItem.BronzeRibbonOfService);
                        }

                        q.Player.SetCampaignAllegiance(3);
                        q.Text.Special(Text.WindurstWatersS.NOW_ALLIED_WITH, 3);
                    }
                })
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance1).OnTrigger(q => SealedEntrance(q, 0))
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance2).OnTrigger(q => SealedEntrance(q, 1))
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance3).OnTrigger(q => SealedEntrance(q, 2));

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.MiahRiyuh).OnTrigger(q =>
                {
                    if (OtherNationAccepted(q))
                    {
                        return QuestReply.Event(122);
                    }
                    else if (q.Player.CampaignAllegiance == 3)
                    {
                        return QuestReply.Event(107);
                    }
                    else
                    {
                        return QuestReply.Event(121);
                    }
                })
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance1).OnTrigger(q =>
                    QuestReply.MessageSpecial(DoorText(q, 2), KeyItem.ZonpaZippasAllPurposePutty).ReplaceDefault())
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance2).OnTrigger(q =>
                    QuestReply.MessageSpecial(DoorText(q, 2), KeyItem.ZonpaZippasAllPurposePutty).ReplaceDefault())
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.SealedEntrance3).OnTrigger(q =>
                    QuestReply.MessageSpecial(DoorText(q, 2), KeyItem.ZonpaZippasAllPurposePutty).ReplaceDefault());
        }
    }
}
