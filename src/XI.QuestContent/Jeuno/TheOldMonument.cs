// The Old Monument (Log ID 3, Quest ID 11), a line-for-line port of scripts/quests/jeuno/The_Old_Monument.lua (quest
// rework B3b). Mertaire : !pos -17 0 -61 245; Bki Tbujhja : !pos -22 0 -60 245; Song Runes : !pos -244 16 -280 118.
// Never begun: while it is available (from ADVANCED_JOB_LEVEL) Mertaire plays 102 (Prog 1), then speaks
// MERTAIRE_MALLIEBELL_LEFT; Mataligeat 140. In Buburimu Peninsula the Song Runes play 0 at Prog 1 (Prog 2), then ask for
// a Sheet of Parchment (SONG_RUNES_REQUIRE); the parchment alone at Prog 2 plays 2: SONG_RUNES_WRITING, completed
// (Poetic Parchment, Researcher of Classics), fame +7 x3, the parchment taken.
// Mertaire's messageName(..., 0, 0, 0, 0, true, false) passes showText's showName (true) and turn (false) as Lua booleans
// (QuestArg bools): the NPC does not turn to the char.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheOldMonument : QuestScript
    {
        public TheOldMonument() : base(QuestLog.Jeuno, "THE_OLD_MONUMENT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Old_Monument";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.PoeticParchment, Title = Title.ResearcherOfClassics });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mataligeat).OnTrigger(QuestReply.Event(140))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                    else
                    {
                        return QuestReply.MessageName(Text.LowerJeuno.MERTAIRE_MALLIEBELL_LEFT, 0, 0, 0, 0, true, false);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 102, q => q.Quest.SetStep(1))
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.SongRunes).OnTrade(q =>
                {
                    if (q.Quest.Step == 2 && q.Trade.HasExactly(Item.SheetOfParchment))
                    {
                        return QuestReply.ProgressCutscene(2);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressCutscene(0);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.MessageSpecial(Text.BuburimuPeninsula.SONG_RUNES_REQUIRE, Item.SheetOfParchment);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BuburimuPeninsula, 0, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.BuburimuPeninsula, 2, q =>
                {
                    q.Text.Special(Text.BuburimuPeninsula.SONG_RUNES_WRITING, Item.SheetOfParchment);

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
