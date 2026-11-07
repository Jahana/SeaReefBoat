// Vanishing Act (Log ID 6, Quest ID 62), a line-for-line port of scripts/quests/ahtUrhgan/Vanishing_Act.lua (quest
// rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Ulamaal, Qutiba, Fochacha (no scripts, no defaults), trigger
// area 2 (-96 -7 121 .. -64 -5 137; the zone's own switch plays 44 there at the char var vanishingactCS 3, and the
// zone's onEventFinish for 44 sets vanishingactCS 4 and the char at -80 -6 122 too). Wajaom Woodlands: the Harvesting
// Points (their script: xi.helm.onTrade / onTrigger; globals/hobbies/helm/logic.lua gives the Rainbow Berry itself to
// a char with this quest accepted whose harvest does not break the sickle). Available with Delivering the Goods
// completed: Qutiba / Ulamaal: 52 / 53 (Events) while the char must zone or Stage is later than now, else 42;
// Fochacha 47 (an Event); 42's option 0: Stage 0, begun. Accepted: Fochacha 43 at Prog 0 (Prog 1), 48 at Prog 1, else
// 49 (Events); Ulamaal / Qutiba 45 with the Rainbow Berry, else 54 (an Event); area 2 at Prog 1: 44 (the char set at
// -80 -6 122, Prog 2); a Harvesting Point's trade matching one Sickle at Prog 2 without the berry: the local var
// questItem 1, 507 ([0] 4294966520), whose finish gives the berry when questItem is 1 and clears it; 45's finish
// completes it (an Imperial Silver Piece) and, when it does, sets the zoning flag, takes the berry and sets A Taste
// of Honey's Stage to now + 60. Completed: Ulamaal 58 (the local var recipe 58; its updates walk recipe 58 -> 1 -> 2
// -> 3 -> 0 with their updateEvent values), Fochacha 59. The era module vanishing_act replaces 45's finish (Stage =
// JstMidnight()). Kept as the Lua has it: 42's other options leave it available; 44 sets the position before Prog;
// 507's [0] is a uint32 (0xFFFFFD18): the C# passes the same 32 bits as an int (-776; the engine's ParseEvent takes
// (uint)ToIntegral).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class VanishingAct : QuestScript
    {
        public VanishingAct() : base(QuestLog.AhtUrghan, "VANISHING_ACT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Vanishing_Act";

        // 507's [0] = 4294966520 (0xFFFFFD18), which an int holds only as its 32 bits
        const int HarvestParam = unchecked((int)4294966520u);

        static void RecipeUpdate(IQuestContext q, int startValue)
        {
            if (q.Player.LocalVar("recipe") == startValue)
            {
                q.Player.SetLocalVar("recipe", 1);
                q.Player.UpdateEvent(5579, 1, 255, 0, 67108863, 5976652, 4095, 0);
            }
            else if (q.Player.LocalVar("recipe") == 1)
            {
                q.Player.SetLocalVar("recipe", 2);
                q.Player.UpdateEvent(4096, 615, 936, 1523, 67108863, 5976652, 4, 0);
            }
            else if (q.Player.LocalVar("recipe") == 2)
            {
                q.Player.SetLocalVar("recipe", 3);
                q.Player.UpdateEvent(2214, 2237, 4509, 5568);
            }
            else if (q.Player.LocalVar("recipe") == 3)
            {
                q.Player.SetLocalVar("recipe", 0);
                q.Player.UpdateEvent(5575);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialSilverPiece,
            });
            b.Steps(1, 2);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "DELIVERING_THE_GOODS"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone ||
                        q.Quest.Var("Stage") > q.Player.SystemTime)
                    {
                        return QuestReply.Event(52);
                    }

                    return QuestReply.ProgressEvent(42); // Starts Quest
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone || q.Quest.Var("Stage") > q.Player.SystemTime)
                    {
                        return QuestReply.Event(53);
                    }

                    return QuestReply.ProgressEvent(42); // Starts Quest
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha).OnTrigger(q => QuestReply.Event(47))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 42, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Stage", 0);
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted (the era module vanishing_act replaces 45 here: state 2)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(43);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(48);
                    }

                    return QuestReply.Event(49);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RainbowBerry))
                    {
                        return QuestReply.ProgressEvent(45);
                    }
                    else
                    {
                        return QuestReply.Event(54);
                    }
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RainbowBerry))
                    {
                        return QuestReply.ProgressEvent(45);
                    }
                    else
                    {
                        return QuestReply.Event(54);
                    }
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 2,
                    q => q.Quest.Step == 1 ? QuestReply.ProgressEvent(44) : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 43, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 44, q =>
                {
                    // the char set down in its own zone, rotation 5 (the 4-argument SetPos)
                    q.Player.SetPos(-80, -6, 122, 5);
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 45, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                        q.Player.DelKeyItem(KeyItem.RainbowBerry);
                        // xi.quest.setVar(player, AHT_URHGAN, A_TASTE_OF_HONEY, 'Stage',
                        // GetSystemTime() + 60): 1 minute wait time
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "A_TASTE_OF_HONEY", "Stage", q.Player.SystemTime + 60);
                    }
                })
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.HarvestingPoint).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.Sickle, 1)) &&
                        q.Quest.Step == 2 &&
                        !q.Player.HasKeyItem(KeyItem.RainbowBerry))
                    {
                        q.Player.SetLocalVar("questItem", 1);
                        return QuestReply.ProgressEvent(507).Option(0, HarvestParam);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 507, q =>
                {
                    if (q.Player.LocalVar("questItem") == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.RainbowBerry);
                        q.Player.SetLocalVar("questItem", 0);
                    }
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q =>
                {
                    q.Player.SetLocalVar("recipe", 58);
                    return QuestReply.Event(58);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha).OnTrigger(q => QuestReply.Event(59))
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 58, q => RecipeUpdate(q, 58));
        }
    }
}
