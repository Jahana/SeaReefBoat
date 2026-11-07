// Give Peace a Chance (Log ID 6, Quest ID 9), a line-for-line port of scripts/quests/ahtUrhgan/Give_Peace_a_Chance.lua
// (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Mishhar !pos -12 -6 29 (no script; default event 590).
// Wajaom Woodlands: qm8 !pos 416.7 -24 220.8 (no script, no default). Mamook: qm4 !pos 347.1 -12.1 -256.4 (no script,
// no default). Available: Mishhar 562, whose finish begins it. Accepted: Mishhar by Prog: 0 587 (an Event), 1 575 (Prog
// 2), 2 588 (an Event), 3 576, 4 589; Wajaom's qm8 at Prog 0 by night (VanadielTOTD() == NIGHT, 20:00-23:59): the
// cutscene 504 (Prog 1); Mamook's qm4 at Prog 2: the cutscene 211 (Prog 3). 576's finish completes it (an Imperial
// Silver Piece), sets the zoning flag and the char var Quest[6][30]Stage to the next JST midnight; when the completion
// fails (a full bag), Prog 4; 589's finish completes it the same way. Kept as the Lua has it: Quest[6][30]Stage is
// Moment of Truth's var (quest 30), and Moment of Truth never reads it (its available check reads Give Peace a Chance's
// status only), nor does any script read needToZone here; 589 leaves Prog 4 when the completion fails again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class GivePeaceAChance : QuestScript
    {
        public GivePeaceAChance() : base(QuestLog.AhtUrghan, "GIVE_PEACE_A_CHANCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Give_Peace_a_Chance";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialSilverPiece,
            });
            b.Steps(1, 2, 3, 4);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mishhar).OnTrigger(QuestReply.ProgressEvent(562))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 562, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mishhar).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 0)
                    {
                        return QuestReply.Event(587);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(575);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(588);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(576);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(589);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 575, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 576, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                        // player:setVar('Quest[6][30]Stage', JstMidnight()): setCharVar's alias, no expiry
                        q.Player.SetCharVar("Quest[6][30]Stage", q.Player.JstMidnight);
                    }
                    else
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 589, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                        q.Player.SetCharVar("Quest[6][30]Stage", q.Player.JstMidnight);
                    }
                })
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.qm8).OnTrigger(q =>
                {
                    if (q.Player.VanadielTotd == Time.Night && q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressCutscene(504);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 504, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Mamook, Npc.Mamook.qm4).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressCutscene(211);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mamook, 211, q =>
                {
                    q.Quest.SetStep(3);
                });
        }
    }
}
