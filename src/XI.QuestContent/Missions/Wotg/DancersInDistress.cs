// Dancers in Distress (WotG 9, Log ID 5, Mission ID 8), a line-for-line port of
// scripts/missions/wotg/09_Dancers_in_Distress.lua (quest rework C3 T5). Section 0 (status 0): Southern San d'Oria
// [S]'s Lion Springs sends CONCERNED_FOR_WOUNDED; Raustigne plays the progress event 86 (the char's Campaign
// allegiance, 10, 1, 1, 3, 4, 8, 3), whose finish sets status 1. Section 1 (status 1): Jugner Forest [S]'s Elegant
// Footprints plays the progress event 2; its update at option 19 (the quiz's end) picks a random item 1 to 3 (Lynx
// Meat, Gold Beastcoin, Nyumomo Doll), keeps it in the mission var Option and updates the event with it as the last
// operand; its finish sets status 2 and the mission's must-zone. Section 2 (status 2): a trade of the chosen item to
// Elegant Footprints, when the char need not zone, plays the progress event 3 (the chosen index last); a trigger sends
// LILISETTE_IS_PREPARING with the chosen item; 3's finish completes the mission (Daughter of a Knight current) and then
// the trade. Kept as the Lua has it: the quiz item is looked up by the Option var as a Lua list index, so Option 0 (a
// char set to status 2 by a GM) looks up no item (a nil: the trade matches nothing, the message gets no item). Option
// 0 is also reachable without a GM: finish 2 sets status 2 whether or not update 19 came,
// so a char whose event 2 ends early is stuck there.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class DancersInDistress : MissionScript
    {
        public DancersInDistress() : base(XI.Quests.MissionLog.WoTG, "DANCERS_IN_DISTRESS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/09_Dancers_in_Distress";

        const int AreaId = MissionLog.Wotg;

        static readonly int[] QuizItems = { Item.LynxMeat, Item.GoldBeastcoin, Item.NyumomoDoll };

        // quizItems[index]: Lua lists start at 1, and an index out of the list is nil (0 here: no item)
        static int QuizItem(int index) => index >= 1 && index <= QuizItems.Length ? QuizItems[index - 1] : 0;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.DaughterOfAKnight,
            });

            // 0: Talk to Raustigne
            b.State(current: true, status: 0)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings)
                .OnTrigger(QuestReply.MessageSpecial(Text.SouthernSanDoriaS.CONCERNED_FOR_WOUNDED))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne).OnTrigger(q =>
                    QuestReply.ProgressEvent(86, q.Player.CampaignAllegiance, 10, 1, 1, 3, 4, 8, 3))
                .OnEventFinish(Zone.SouthernSanDoriaS, 86, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                });

            // 1: Talk to Elegant Footprints and fake quiz
            b.State(current: true, status: 1)
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.ElegantFootprints).OnTrigger(QuestReply.ProgressEvent(2))
                .OnEventUpdate(Zone.JugnerForestS, 2, q =>
                {
                    // The options chosen in the quiz are sequential: 19 is the end of the quiz. The last argument is
                    // the chosen item (chosen randomly here)
                    if (q.Option == 19)
                    {
                        int randomChoice = q.Player.RandomInt(1, 3);
                        q.Mission.SetVar("Option", randomChoice);
                        q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, randomChoice);
                    }
                })
                .OnEventFinish(Zone.JugnerForestS, 2, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Mission.SetMustZone();
                });

            // 2: Hand in quiz item
            b.State(current: true, status: 2)
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.ElegantFootprints)
                .OnTrade(q =>
                {
                    int chosenItemIndex = q.Mission.Var("Option");

                    if (!q.Mission.MustZone && q.Trade.Matches((QuizItem(chosenItemIndex), 1)))
                    {
                        return QuestReply.ProgressEvent(3, 0, 0, 0, 0, 0, 0, 0, chosenItemIndex);
                    }

                    return null;
                })
                .OnTrigger(q =>
                    QuestReply.MessageSpecial(Text.JugnerForestS.LILISETTE_IS_PREPARING,
                        QuizItem(q.Mission.Var("Option"))))
                .OnEventFinish(Zone.JugnerForestS, 3, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
