// A Minstrel in Despair (Log ID 3, Quest ID 12), a line-for-line port of scripts/quests/jeuno/A_Minstrel_In_Despair.lua
// (quest rework B3b). Mertaire : !pos -17 0 -61 245. Never begun: while it is available (The Old Monument completed)
// the Song Runes of Buburimu Peninsula write a new Poetic Parchment for a Sheet of Parchment (2: the sheet taken, the
// Poetic Parchment, SONG_RUNES_WRITING; a failsafe for a lost parchment) and otherwise ask for one; Bki Tbujhja 182,
// Mataligeat 144, Mertaire speaks MERTAIRE_MALLIEBELL_LEFT; a trade holding the Poetic Parchment plays 101: completed
// (2100 gil), fame +7 x3, the trade confirmed.
// Mertaire's messageName(..., true, false): showName, no turn (QuestArg bools), as in TheOldMonument.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AMinstrelInDespair : QuestScript
    {
        public AMinstrelInDespair() : base(QuestLog.Jeuno, "A_MINSTREL_IN_DESPAIR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/A_Minstrel_In_Despair";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 2100 });

            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.Jeuno, "THE_OLD_MONUMENT") == QuestStatus.Completed)
                // Failsafe incase the player loses their Poetic Parchment
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.SongRunes).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.SheetOfParchment))
                    {
                        return QuestReply.ProgressCutscene(2);
                    }
                    return null;
                })
                .OnTrigger(QuestReply.MessageSpecial(Text.BuburimuPeninsula.SONG_RUNES_REQUIRE, Item.SheetOfParchment))
                .OnEventFinish(Zone.BuburimuPeninsula, 2, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveItem(Item.PoeticParchment);
                    q.Text.Special(Text.BuburimuPeninsula.SONG_RUNES_WRITING, Item.SheetOfParchment);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrigger(QuestReply.Event(182))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mataligeat).OnTrigger(QuestReply.Event(144))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrade(q =>
                {
                    if (q.Trade.Has(Item.PoeticParchment))
                    {
                        return QuestReply.ProgressEvent(101);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    return QuestReply.MessageName(Text.LowerJeuno.MERTAIRE_MALLIEBELL_LEFT, 0, 0, 0, 0, true, false);
                })
                .OnEventFinish(Zone.LowerJeuno, 101, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
