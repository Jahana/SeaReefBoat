// Knot Quite There (Log ID 7, Quest ID 27), a line-for-line port of
// scripts/quests/crystalWar/WOTG_WIN_3_Knot_Quite_There.lua (quest rework B3b Crystal War group 2). Windurst Waters
// [S]: Door:Acolyte Hostel (down) !pos 124 -3 222. Sauromugue Champaign [S]: Bulwark Gate !pos -447 -2 342. Southern
// San d'Oria [S]: Door:House (_6eo) !pos 148 -3 24.
// Available, The Tigress Strikes completed and the WotG mission Back to the Beginning completed: the Acolyte Hostel door
// 151, whose finish begins it. Accepted: the door 152 (an Event). Bulwark Gate: Prog 0: 105 (-> Prog 1); 1: 107 / 2:
// 108 (Events); the 108-Knot Quipu alone at Prog 1: 106, whose finish confirms the trade and sets Prog 2. Southern San
// d'Oria [S]: the zone-in from East Ronfaure [S] at Prog 2: 62 (-> Prog 3); _6eo at Prog 3: 63, whose finish completes
// it (a Platinum Beastcoin).
// Kept as the Lua has it: 151 begins on any option; Bulwark Gate answers nil at Prog 3+; the quipu at another Prog falls
// through (nil); 63's finish completes with no check of its result; no completed section. (The Lua header gives Bulwark
// Gate's zone as 91; the section is Sauromugue Champaign [S], 98.)
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class KnotQuiteThere : QuestScript
    {
        public KnotQuiteThere() : base(QuestLog.CrystalWar, "KNOT_QUITE_THERE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_WIN_3_Knot_Quite_There";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.PlatinumBeastcoin,
            });

            // Pick up the quest at the Acolyte Hostel door in Windurst Waters (S).
            // Requires The Tigress Strikes complete and WOTG mission Cait Sith current
            // (i.e. Back to the Beginning completed).
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "THE_TIGRESS_STRIKES") &&
                    q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.BackToTheBeginning))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorAcolyteHosteldown).OnTrigger(QuestReply.ProgressEvent(151))
                .OnEventFinish(Zone.WindurstWatersS, 151, q =>
                {
                    q.Quest.Begin();
                });

            // Escort progression: Bulwark Gate (Sauromugue Champaign [S]) -> zone into
            // Southern San d'Oria (S) from East Ronfaure (S) -> Door:House.
            b.State(QuestStatus.Accepted)
                // Reminder cutscene while the quest is active.
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorAcolyteHosteldown).OnTrigger(QuestReply.Event(152))
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 0)
                    {
                        return QuestReply.ProgressEvent(105);
                    }
                    else if (prog == 1)
                    {
                        // Pre-trade reminder to hand over the 108-Knot Quipu.
                        return QuestReply.Event(107);
                    }
                    else if (prog == 2)
                    {
                        // Post-trade reminder to head to Southern San d'Oria.
                        return QuestReply.Event(108);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (
                        q.Quest.Step == 1 &&
                        q.Trade.HasExactly(Item.OneHundredEightKnotQuipu))
                    {
                        return QuestReply.ProgressEvent(106);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 105, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 106, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS._6eo).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(63);
                    }
                    return null;
                })
                // Approach cutscene with Pattna-Ottna, only when arriving on foot from
                // East Ronfaure (S). (Retail also suppresses it while mounted; not modeled here.)
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    if (
                        q.Player.PreviousZone == Zone.EastRonfaureS &&
                        q.Quest.Step == 2)
                    {
                        return QuestReply.Event(62);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 62, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 63, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
