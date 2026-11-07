// The Missing Piece (Log ID 5, Quest ID 193), a line-for-line port of scripts/quests/outlands/The_Missing_Piece.lua (quest
// rework B3b Outlands, group 5). Rabao: Alfesar !pos 23.2 8.8 39 (no script; default 52). Quicksand Caves: qm5 (no
// script; default NOTHING_OUT_OF_ORDINARY; five spots, positionTable). Northern San d'Oria: Charlaimagnat !pos 124.6 6.5
// 111.8 (no script; default 702).
// Offered at Selbina-Rabao fame level 4 from level 10: Alfesar 6 (Ancient Tablet Fragment) begins it. Accepted: Alfesar
// Prog 0: 7 (the fragment); Prog 1: 8 (fragment, Tablet of Ancient Magic, Letter from Alfesar), whose finish takes the
// fragment, adds the tablet and the letter (addKeyItem: no message), sets Prog 2 and says ACCEPTED_KEYITEM (0, tablet,
// letter); Prog 2: 9 (0, tablet); else nil. Quicksand Caves' qm5 at Prog 0: Prog 1, the qm moved to another of its five
// spots (npcUtil.pickNewPosition), and the fragment (a keyItem reply); else nil. Charlaimagnat: Prog 2: 703, whose finish
// sets Wait = now + 60, adds Acquirer of Ancient Arcanum, takes the tablet and the letter and sets Prog 3; Prog 3 before
// Wait: 704; Prog 3 from Wait on: 705, whose finish runs quest:complete (the Scroll of Teleport-Altep) and on true adds
// San d'Oria and Bastok fame 20 each; else nil. Completed: Alfesar 11 (replaceDefault).
// Kept as the Lua has it: 8's finish adds the key items with addKeyItem and one message; positionTable's [3] (759, 0,
// -573) differs from the header's 749; qm5 moves (npc:setPos at once, Npc.SetPos) before the reply plays (the trigger's
// own NPC); pickNewPosition draws math.randomInt(1, 0) once before it counts the table. qm5's spawn spot (781.7, -16,
// -825) is none of the table's, so the first move keeps index 1 as "current" (the main flow's draw: [4], 451 -16 -739).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheMissingPiece : QuestScript
    {
        // the Lua's positionTable, [1] .. [5]
        static readonly double[][] PositionTable =
        {
            new double[] { 770,   0,  -419 },
            new double[] { 657,   0,  -537 },
            new double[] { 759,   0,  -573 },
            new double[] { 451, -16,  -739 },
            new double[] { 787, -16,  -819 },
        };

        public TheMissingPiece() : base(QuestLog.Outlands, "THE_MISSING_PIECE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/The_Missing_Piece";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.ScrollOfTeleportAltep });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.SelbinaRabao) >= 4 &&
                    q.Player.MainLevel >= 10)
                .Npc(Zone.Rabao, Npc.Rabao.Alfesar).OnTrigger(QuestReply.ProgressEvent(6, KeyItem.AncientTabletFragment))
                .OnEventFinish(Zone.Rabao, 6, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Rabao, Npc.Rabao.Alfesar).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(7, KeyItem.AncientTabletFragment); // Reminder to get KI
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(8, KeyItem.AncientTabletFragment, KeyItem.TabletOfAncientMagic, KeyItem.LetterFromAlfesar); // Player has returned with KI
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(9, 0, KeyItem.TabletOfAncientMagic); // Reminder to go to Sandy
                    }
                    return null;
                })
                .OnEventFinish(Zone.Rabao, 8, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AncientTabletFragment);
                    q.Player.AddKeyItem(KeyItem.TabletOfAncientMagic);
                    q.Player.AddKeyItem(KeyItem.LetterFromAlfesar);
                    q.Quest.SetStep(2);
                    q.Text.Special(Text.Rabao.ACCEPTED_KEYITEM, 0, KeyItem.TabletOfAncientMagic, KeyItem.LetterFromAlfesar);
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm5).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                        // npcUtil.pickNewPosition(npc:getID(), positionTable): allowCurrentPosition false
                        var newPosition = NpcUtil.PickNewPosition(q, q.Npc, PositionTable);
                        q.Npc.SetPos(newPosition[0], newPosition[1], newPosition[2]);
                        return QuestReply.KeyItem(KeyItem.AncientTabletFragment);
                    }
                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Charlaimagnat).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(703); // Player has turned in the KIs from Alfesar
                    }
                    else if (
                        progress == 3 &&
                        q.Player.SystemTime < q.Quest.Var("Wait"))
                    {
                        return QuestReply.Event(704); // Player has not finished the wait
                    }
                    else if (
                        progress == 3 &&
                        q.Player.SystemTime >= q.Quest.Var("Wait"))
                    {
                        return QuestReply.ProgressEvent(705); // Player has finished the wait. Quest finished
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 703, q =>
                {
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60);
                    q.Player.AddTitle(Title.AcquirerOfAncientArcanum);
                    q.Player.DelKeyItem(KeyItem.TabletOfAncientMagic);
                    q.Player.DelKeyItem(KeyItem.LetterFromAlfesar);
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 705, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        q.Player.AddFame(FameArea.Bastok, 20);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.Alfesar).OnTrigger(QuestReply.Event(11).ReplaceDefault());
        }
    }
}
