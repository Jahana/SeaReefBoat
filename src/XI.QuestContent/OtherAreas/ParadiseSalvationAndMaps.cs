// Paradise, Salvation, and Maps (Log ID 4, Quest ID 68), a line-for-line port of
// scripts/quests/otherAreas/Paradise_Salvation_and_Maps.lua (quest rework B3b, Other Areas group 7). Tavnazian
// Safehold: Nivorajean !pos 16.712 -22.000 12.552 (a script: 382 / 221 in turn, by the char's local spokenNivorajean);
// the Sacrarium: the Treasure Chest (a script: xi.treasure's).
// Available once CoP's The Savage is completed: Nivorajean 223, whose finish begins the quest. Accepted: the
// Sacrarium's chest, traded to at Option 0 without the Piece of Ripped Floorplans: xi.treasure.onTrade with the
// floorplans as its bypass reward (type 2), then Option = the first of eight chest spots within 2 yalms of the char
// (player:checkDistance), noAction. Nivorajean: with the floorplans: 225 (Option - 1); else, Wait not today
// (VanadielUniqueDay) and Prog above 0: Prog 2: 228, else 227; Wait today: 226 (event); else 224 (event). 225's finish:
// Wait = today, the floorplans taken, Option 0, option 0: Prog 2, else Prog 1. 227: Prog 0, Wait 0. 228: quest:complete
// (2000 exp, 2000 gil, the Map of the Sacrarium).
// Kept as the Lua has it: the chest's treasure trade runs whatever it answers (no lockpick or key: the chest's own
// messages) and its result is not read; Option 0 (no spot within 2 yalms) makes 225's argument -1; Prog 1 or 2 with
// Wait 0 and no floorplans skips the wait.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ParadiseSalvationAndMaps : QuestScript
    {
        public ParadiseSalvationAndMaps() : base(QuestLog.OtherAreas, "PARADISE_SALVATION_AND_MAPS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Paradise_Salvation_and_Maps";

        static readonly double[][] ChestTable =
        {
            new[] {  31.021, -2.000,   99.013 }, // (F-5)
            new[] {  89.034, -2.000,   99.248 }, // (H-5)
            new[] {  88.223, -2.000,  -36.017 }, // (H-9)
            new[] { 177.600,  8.310,  100.000 }, // (J-6)
            new[] { 179.709, -7.693,  -97.007 }, // (J-10)
            new[] { 260.391,  0.000,   21.487 }, // (L-8)
            new[] { 111.451, -2.000, -100.159 }, // (H-11)
            new[] {   8.974, -2.179, -133.075 }, // (F-11)
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfTheSacrarium,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheSavage))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Nivorajean).OnTrigger(QuestReply.ProgressEvent(223))
                .OnEventFinish(Zone.TavnazianSafehold, 223, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Sacrarium, Npc.Sacrarium.TreasureChest).OnTrade(q =>
                {
                    if (q.Quest.Var("Option") == 0 &&
                        !q.Player.HasKeyItem(KeyItem.PieceOfRippedFloorplans))
                    {
                        q.Trade.TreasureTrade(2, KeyItem.PieceOfRippedFloorplans);

                        for (int i = 1; i <= ChestTable.Length; i++)
                        {
                            // player:checkDistance(x, y, z)
                            if (q.Player.DistanceTo(ChestTable[i - 1][0], ChestTable[i - 1][1], ChestTable[i - 1][2]) < 2)
                            {
                                q.Quest.SetVar("Option", i);
                                break;
                            }
                        }

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Nivorajean).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;
                    int wait = q.Quest.Var("Wait");

                    if (q.Player.HasKeyItem(KeyItem.PieceOfRippedFloorplans))
                    {
                        return QuestReply.ProgressEvent(225, q.Quest.Var("Option") - 1);
                    }
                    else if (wait != q.Player.VanadielUniqueDay &&
                             progress > 0)
                    {
                        if (progress == 2)
                        {
                            return QuestReply.ProgressEvent(228); // Correct Coordinates Selected
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(227); // Wrong Corrdinates Selected
                        }
                    }
                    else if (wait == q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(226); // Wait Longer
                    }
                    else
                    {
                        return QuestReply.Event(224); // Reminder
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 225, q =>
                {
                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                    q.Player.DelKeyItem(KeyItem.PieceOfRippedFloorplans);
                    q.Quest.SetVar("Option", 0);
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(2); // Correct Coordinates Selected
                    }
                    else
                    {
                        q.Quest.SetStep(1); // Wrong Coordinates Selected
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 227, q =>
                {
                    q.Quest.SetStep(0);
                    q.Quest.SetVar("Wait", 0);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 228, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
