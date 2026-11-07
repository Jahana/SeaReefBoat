// Exit the Gambler (Log ID 0, Quest ID 101), a line-for-line port of scripts/quests/sandoria/Exit_the_Gambler.lua (quest
// rework B3b, San d'Oria group 7). Aurege : !pos -156.253 11.999 253.691 231; Guilberdrier : !pos -159.082 12.000 253.794
// 231; Varchet : !pos 116.484 -1 91.554 230. Never begun: available at Stage 0, Aurege's or Guilberdrier's 521 sets
// Stage 1. At Stage 1 (whatever the status): Varchet's 638 (once per zone) while Prog is 0 and no mustZone, else 643;
// Aurege's GAMBLING_IS_THE_RUIN and Guilberdrier's WHO_DOES_HE_THINK, or at Prog 1 their 516 / 518 (completed from
// available: 2000 exp, fame +20, the Map of King Ranperre's Tomb, Daybreak Gambler; the vars cleared). Prog 1 comes from
// Varchet's own script: a won dice game (5 gil, the seeded RNG) at Stage 1 sets this quest's Prog 1
// (xi.quest.setVar, `Quest[0][101]Prog`) and shows VARCHET_KEEP_PROMISE. Completed: Aurege's 514, Guilberdrier's
// HE_WAS_JUST_HERE and Varchet's NOBODY_ONE_WANTS_TO_PLAY, each replacing the default. Nonterene's script reads the
// status (stays). Varchet's script reads Stage and writes Prog in its own 519 finish (Varchet.lua:47-53): it stays in
// Lua, as the C# quest keeps the container Quest[0][101] and the names Stage and Prog. A call in place would make
// Varchet both a container NPC of this quest (section 2 and 3 onTrigger) and a Called() one, which the validator
// refuses. Kept as the Lua has it: the quest is completed without ever being accepted; nothing in it sets mustZone;
// section 2 ignores the status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ExitTheGambler : QuestScript
    {
        public ExitTheGambler() : base(QuestLog.Sandoria, "EXIT_THE_GAMBLER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Exit_the_Gambler";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Exp = 2000,
                KeyItem = KeyItem.MapOfKingRanperresTomb,
                Title = Title.DaybreakGambler,
            });

            b.State(QuestStatus.Available, when: q => q.Quest.Var("Stage") == 0)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Aurege).OnTrigger(QuestReply.ProgressEvent(521))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilberdrier).OnTrigger(QuestReply.ProgressEvent(521))
                .OnEventFinish(Zone.NorthernSanDoria, 521, q => q.Quest.SetVar("Stage", 1));

            b.AnyState(when: q => q.Quest.Var("Stage") == 1)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Aurege).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(516);
                    }
                    else
                    {
                        // Reminder
                        return QuestReply.MessageText(Text.NorthernSanDoria.GAMBLING_IS_THE_RUIN);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilberdrier).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(518);
                    }
                    else
                    {
                        // Reminder
                        return QuestReply.MessageText(Text.NorthernSanDoria.WHO_DOES_HE_THINK);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 516, q => q.Quest.Complete())
                .OnEventFinish(Zone.NorthernSanDoria, 518, q => q.Quest.Complete())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Varchet).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 &&
                        !q.Quest.MustZone)
                    {
                        return QuestReply.Event(638).OncePerZone();
                    }
                    else
                    {
                        return QuestReply.Event(643);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Aurege).OnTrigger(QuestReply.Event(514).ReplaceDefault())
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilberdrier).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.HE_WAS_JUST_HERE).ReplaceDefault())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Varchet).OnTrigger(QuestReply.MessageText(Text.SouthernSanDoria.NOBODY_ONE_WANTS_TO_PLAY).ReplaceDefault());
        }
    }
}
