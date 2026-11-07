// Led Astray (Log ID 6, Quest ID 65), a line-for-line port of scripts/quests/ahtUrhgan/Led_Astray.lua (quest rework B3b
// Aht Urhgan, draft). Aht Urhgan Whitegate: Mhasbaf !pos 54.7 -6 11.4 (no script; default 542), Tohka Telposkha !pos
// 22.1 0 22.8 (no script; default 663), Rubahah !pos -96.7 0 -14.7 (script: the shop), Cacaroon !pos -72 0 -82.3 (no
// script; default 248); trigger areas 7 (69 0 7 .. 73 0 11) and 8 (10 2 -96 .. 14 2 -92). Nashmau: Tataroon !pos -25.2
// 0 -39 (no script; default 270). Available (no other condition): Mhasbaf 808, whose finish begins it. Accepted, by
// Prog (one section each): Mhasbaf 834 once per zone at Prog 0, 1, 2, 4 and plain at Prog 3; Prog 0: area 7: 809 (Prog
// 1); Prog 1: Tohka Telposkha 810 (Prog 2); Prog 2: Rubahah 811 until Stage bit 1, Cacaroon 812 until Stage bit 2; each
// finish sets its bit, or, with the other bit set, Prog 3 and Stage 0; Prog 3: area 8: 813 (the Letter from Bernahn,
// Prog 4); Prog 4: Nashmau's Tataroon 305, whose finish completes it (an Imperial Silver Piece) and takes the letter.
// Kept as the Lua has it: Mhasbaf's 834 is not once per zone at Prog 3; 808 begins it on any option; the trigger-area
// replies come at every entry while their Prog holds; Rubahah and Cacaroon return nil once their bit is set (the shop /
// 248).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class LedAstray : QuestScript
    {
        public LedAstray() : base(QuestLog.AhtUrghan, "LED_ASTRAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Led_Astray";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialSilverPiece,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf).OnTrigger(QuestReply.ProgressEvent(808))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 808, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf)
                    .OnTrigger(QuestReply.Event(834).OncePerZone())
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 7, q => QuestReply.ProgressEvent(809))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 809, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf)
                    .OnTrigger(QuestReply.Event(834).OncePerZone())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.TohkaTelposkha)
                    .OnTrigger(QuestReply.ProgressEvent(810))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 810, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf)
                    .OnTrigger(QuestReply.Event(834).OncePerZone())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Rubahah).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Stage", 1))
                    {
                        return QuestReply.ProgressEvent(811);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Stage", 2))
                    {
                        return QuestReply.ProgressEvent(812);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 811, q =>
                {
                    if (q.Quest.IsVarBitsSet("Stage", 2))
                    {
                        q.Quest.SetStep(3);
                        q.Quest.SetVar("Stage", 0);
                    }
                    else
                    {
                        q.Quest.SetVarBit("Stage", 1);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 812, q =>
                {
                    if (q.Quest.IsVarBitsSet("Stage", 1))
                    {
                        q.Quest.SetStep(3);
                        q.Quest.SetVar("Stage", 0);
                    }
                    else
                    {
                        q.Quest.SetVarBit("Stage", 2);
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf).OnTrigger(QuestReply.Event(834))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 8, q => QuestReply.ProgressEvent(813))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 813, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromBernahn);
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf)
                    .OnTrigger(QuestReply.Event(834).OncePerZone())
                .Npc(Zone.Nashmau, Npc.Nashmau.Tataroon).OnTrigger(QuestReply.ProgressEvent(305))
                .OnEventFinish(Zone.Nashmau, 305, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LetterFromBernahn);
                    }
                });
        }
    }
}
