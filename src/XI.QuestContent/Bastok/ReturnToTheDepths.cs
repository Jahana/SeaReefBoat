// Return to the Depths (Log ID 1, Quest ID 78), a line-for-line port of scripts/quests/bastok/Return_to_the_Depths.lua
// (quest rework B3b). Ayame : !pos 132 -18 33 237; Rakorok : !pos 158 13 -42 11. Ayame's 879 (Bastok fame level 5, A
// Question of Faith completed) begins it; the Oldton Movalpolos zone-in 40 (Prog 1); a Misareaux Garlic to Muckvix in
// Lower Jeuno (99: Prog 2, KI Letter from Muckvix, 2000 gil); Magriffon in Kazham (299: Prog 3, the letter deleted; 10000
// gil traded: 301, Prog 4, KI Providence Pot and KI Letter from Magriffon); Muckvix's 100 (Prog 5, KI Pungent Providence
// Pot, the pot and letter deleted); the Oldton zone-in 41 (Prog 6); Tarnotik's 42 (Prog 7); a Bottle of Ahriman Tears
// to Tarnotik (44: Prog 8, the bottle taken, the char moved into Mine Shaft #2716); the burning circle _0d0's 5 (Prog 9);
// the battlefield (scripts/battlefields/Mine_Shaft_2716/return_to_the_depths.lua, requiredVar 'Quest[1][78]Prog' at 9:
// a status read, so the var name and value stay) and its win's 32001 (Prog 10, 10000 gil); Ayame's 881 completes it
// (Bowyer Ring, Goblin in Disguise, fame +50). Once completed: Ayame 882, Tarnotik 45 (oncePerZone) and 44 for a bottle
// (moved into the Mine Shaft again), Ravorara's 313 for the Pungent Providence Pot (option 1: 1000 gil, the pot deleted).
// Kept as the Lua file has it: the zone-ins have no prevZone test; Tarnotik takes a bottle at any Prog from 7, and 44's
// finish sets Prog 8 again; the Pungent Providence Pot is kept until Ravorara takes it after completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ReturnToTheDepths : QuestScript
    {
        public ReturnToTheDepths() : base(QuestLog.Bastok, "RETURN_TO_THE_DEPTHS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Return_to_the_Depths";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Bastok,
                Item = Item.BowyerRing,
                Title = Title.GoblinInDisguise,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Bastok) >= 5 &&
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "A_QUESTION_OF_FAITH"))
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(QuestReply.ProgressEvent(879))
                .OnEventFinish(Zone.Metalworks, 879, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(880);
                    }
                    else if (progress == 10)
                    {
                        return QuestReply.ProgressEvent(881);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 881, q => q.Quest.Complete())
                .OnZoneIn(Zone.OldtonMovalpolos, q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(40);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(41);
                    }
                    return null;
                })
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Tarnotik)
                    .OnTrade(q =>
                        q.Quest.Step >= 7 && q.Trade.Matches((Item.BottleOfAhrimanTears, 1)) ? QuestReply.ProgressEvent(44) : null)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 6)
                        {
                            return QuestReply.ProgressEvent(42, 11, 131456, 384, 0, Item.MisareauxGarlic);
                        }
                        else if (progress >= 7)
                        {
                            return QuestReply.Event(43).OncePerZone();
                        }
                        return null;
                    })
                .OnEventFinish(Zone.OldtonMovalpolos, 40, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.OldtonMovalpolos, 41, q => q.Quest.SetStep(6))
                .OnEventFinish(Zone.OldtonMovalpolos, 42, q => q.Quest.SetStep(7))
                .OnEventFinish(Zone.OldtonMovalpolos, 44, q =>
                {
                    q.Quest.SetStep(8);
                    q.Trade.Complete();
                    q.Player.SetPos(-93.657, -120.000, -583.561, 232, Zone.MineShaft2716);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Muckvix)
                    .OnTrade(q =>
                        q.Quest.Step == 1 && q.Trade.Matches((Item.MisareauxGarlic, 1))
                            ? QuestReply.ProgressEvent(99, 1, 1, 3, 0, Item.MisareauxGarlic)
                            : null)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 2)
                        {
                            return QuestReply.Event(46).OncePerZone();
                        }
                        else if (progress == 4 &&
                                 q.Player.HasKeyItem(KeyItem.LetterFromMagriffon) &&
                                 q.Player.HasKeyItem(KeyItem.ProvidencePot))
                        {
                            return QuestReply.ProgressEvent(100);
                        }
                        else if (progress == 5)
                        {
                            return QuestReply.Event(47).OncePerZone();
                        }
                        return null;
                    })
                .OnEventFinish(Zone.LowerJeuno, 99, q =>
                {
                    q.Quest.SetStep(2);
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.LetterFromMuckvix);
                    q.Player.GiveCurrency("gil", 2000);
                })
                .OnEventFinish(Zone.LowerJeuno, 100, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.GiveKeyItem(KeyItem.PungentProvidencePot);
                    q.Player.DelKeyItem(KeyItem.ProvidencePot);
                    q.Player.DelKeyItem(KeyItem.LetterFromMagriffon);
                })
                .Npc(Zone.Kazham, Npc.Kazham.Magriffon)
                    .OnTrade(q =>
                        q.Quest.Step == 3 && q.Trade.Gil == 10000
                            ? QuestReply.ProgressEvent(301, 1, KeyItem.ProvidencePot)
                            : null)
                    .OnTrigger(q =>
                        q.Quest.Step == 2 && q.Player.HasKeyItem(KeyItem.LetterFromMuckvix)
                            ? QuestReply.ProgressEvent(299, 75, KeyItem.ProvidencePot, 10000)
                            : null)
                .OnEventFinish(Zone.Kazham, 299, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.DelKeyItem(KeyItem.LetterFromMuckvix);
                })
                .OnEventFinish(Zone.Kazham, 301, q =>
                {
                    q.Quest.SetStep(4);
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.ProvidencePot);
                    q.Player.GiveKeyItem(KeyItem.LetterFromMagriffon);
                })
                .Npc(Zone.MineShaft2716, Npc.MineShaft2716._0d0).OnTrigger(q =>
                    q.Quest.Step == 8 ? QuestReply.ProgressEvent(5, 1, 131456, 384, 0, Item.MisareauxGarlic) : null)
                .OnEventFinish(Zone.MineShaft2716, 5, q => q.Quest.SetStep(9))
                .OnEventFinish(Zone.MineShaft2716, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ReturnToTheDepths)
                    {
                        q.Quest.SetStep(10);
                        q.Player.GiveCurrency("gil", 10000);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q => QuestReply.Event(882))
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Tarnotik)
                    .OnTrade(q => q.Trade.Matches((Item.BottleOfAhrimanTears, 1)) ? QuestReply.Event(44) : null)
                    .OnTrigger(q => QuestReply.Event(45).OncePerZone())
                .OnEventFinish(Zone.OldtonMovalpolos, 44, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetPos(-93.657, -120.000, -583.561, 232, Zone.MineShaft2716);
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Ravorara).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.PungentProvidencePot)
                        ? QuestReply.ProgressEvent(313, 0, 0, 0, KeyItem.PungentProvidencePot)
                        : QuestReply.Event(314))
                .OnEventFinish(Zone.PortBastok, 313, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveCurrency("gil", 1000);
                        q.Player.DelKeyItem(KeyItem.PungentProvidencePot);
                    }
                });
        }
    }
}
