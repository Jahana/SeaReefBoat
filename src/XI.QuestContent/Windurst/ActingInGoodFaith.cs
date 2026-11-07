// Acting in Good Faith (Log ID 2, Quest ID 64), a line-for-line port of
// scripts/quests/windurst/Acting_in_Good_Faith.lua (quest rework B3b, Windurst group 12).
// Gantineux : !pos -83 -9 3 238; Eperdur : !pos 129 -6 96 231; QM1 : !pos -460.85, -1.5, 425.14 (D-5), -24.10, -9.303,
// 258.993 (I-7), -19.624, -1.631, 60.368 (I-10), 256.757, -20.489, 335.920 (M-6). At Windurst fame level 4 and main
// level 10, Gantineux's 10019, option 0, gives the Spirit Incense and begins it. Accepted: Gantineux 10020 (the incense
// held), 10022 (the letter held), else 10021 (Gantineux's Letter); the Eldieme qm1 with the incense: 50
// (SPIRIT_INCENSE_EMITS_PUTRID_ODOR, the incense taken, the qm1 moved to one of four places at random); Eperdur
// (Northern San d'Oria) with the letter: 680, completed (fame 40, the Scroll of Teleport-Mea, Pilgrim to Mea; the
// letter taken). Completed: Gantineux 10023 (replaceDefault).
// Kept as the Lua file has it: 10021 plays whenever neither key item is held, so the letter can be had before the qm1
// (the qm1 then needs the incense, which is still held: 10020 covers that); the qm1's move can land where it stands.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ActingInGoodFaith : QuestScript
    {
        // eldiemeID.npc.QM1_POS (zones/The_Eldieme_Necropolis/IDs.lua:87-93): the qm1's four places, as the Lua numbers
        // (doubles) setPos gets. No generated id carries an IDs.lua position table
        static readonly (double X, double Y, double Z)[] Qm1Pos =
        {
            (-460.850, -1.500, 425.140), // D-5.
            (-24.100, -9.303, 258.993),  // I-7.
            (-19.624, -1.631, 60.368),   // I-10.
            (256.757, -20.489, 335.920), // M-6.
        };

        public ActingInGoodFaith() : base(QuestLog.Windurst, "ACTING_IN_GOOD_FAITH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Acting_in_Good_Faith";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Windurst,
                Item = Item.ScrollOfTeleportMea,
                Title = Title.PilgrimToMea,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 4 &&
                    q.Player.MainLevel >= 10)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Gantineux).OnTrigger(QuestReply.ProgressEvent(10019))
                .OnEventFinish(Zone.WindurstWaters, 10019, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.SpiritIncense);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Gantineux).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SpiritIncense))
                    {
                        return QuestReply.Event(10020);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.GantineuxsLetter))
                    {
                        return QuestReply.Event(10022);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10021);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 10021, q => q.Player.GiveKeyItem(KeyItem.GantineuxsLetter))
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.qm1).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SpiritIncense))
                    {
                        return QuestReply.ProgressEvent(50);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TheEldiemeNecropolis, 50, q =>
                {
                    q.Text.Special(Text.TheEldiemeNecropolis.SPIRIT_INCENSE_EMITS_PUTRID_ODOR, KeyItem.SpiritIncense);
                    q.Player.DelKeyItem(KeyItem.SpiritIncense);

                    // move QM1 randomly on quest completion: npc:setPos(unpack(QM1_POS[math.randomInt(1, 4)])), one
                    // draw of the script RNG (Player.RandomInt is math.randomInt) after the message and the key item,
                    // as in the Lua; setPos with no rotation keeps the qm1's
                    var (x, y, z) = Qm1Pos[q.Player.RandomInt(1, 4) - 1];
                    q.Npc.SetPos(x, y, z);
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.GantineuxsLetter))
                    {
                        return QuestReply.ProgressEvent(680);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 680, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.GantineuxsLetter);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Gantineux).OnTrigger(QuestReply.Event(10023).ReplaceDefault());
        }
    }
}
