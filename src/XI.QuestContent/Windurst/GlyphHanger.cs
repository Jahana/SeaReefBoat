// Glyph Hanger (Log ID 2, Quest ID 30), a line-for-line port of scripts/quests/windurst/Glyph_Hanger.lua (quest rework
// B3b). Hariga-Origa : !pos -62 -6 105 238; Ipupu : !pos 251.745 -5.5 35.539 115. Hariga-Origa's note goes to Ipupu in
// West Sarutabaruta, whose note goes back; Serukoko and Sohdede talk while the char has no note from Ipupu. Early Bird
// Catches the Bookworm reads its completion. Kept as the Lua file has it: the completion sets the player local var
// 'Quest[2][20]mustZone' (Early Bird's prefix), which Early Bird's getMustZone (a char var) never reads.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class GlyphHanger : QuestScript
    {
        public GlyphHanger() : base(QuestLog.Windurst, "GLYPH_HANGER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Glyph_Hanger";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.MapOfTheHorutotoRuins,
                FameArea = FameArea.Windurst,
                Fame = 20,
                Exp = 2000,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga).OnTrigger(QuestReply.ProgressEvent(381))
                .OnEventFinish(Zone.WindurstWaters, 381, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.NoteFromHarigaOriga);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.NoteFromIpupu) ? QuestReply.ProgressEvent(385) : QuestReply.Event(382))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Serukoko).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.NoteFromIpupu) ? QuestReply.ProgressEvent(383) : null)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Sohdede).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.NoteFromIpupu) ? QuestReply.ProgressEvent(384) : null)
                .OnEventFinish(Zone.WindurstWaters, 385, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.NoteFromIpupu);

                        // Player must zone before being able to flag the next quest
                        q.Player.SetLocalVar("Quest[2][20]mustZone", 1);
                    }
                })
                .Npc(Zone.WestSarutabaruta, Npc.WestSarutabaruta.Ipupu).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.NoteFromHarigaOriga)
                        ? QuestReply.ProgressEvent(47, 0, KeyItem.NoteFromHarigaOriga)
                        : null)
                .OnEventFinish(Zone.WestSarutabaruta, 47, q =>
                {
                    q.Player.DelKeyItem(KeyItem.NoteFromHarigaOriga);
                    q.Player.GiveKeyItem(KeyItem.NoteFromIpupu);
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Player.HasCompletedQuest(QuestLog.Windurst, "CHASING_TALES"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga).OnTrigger(QuestReply.Event(386).ReplaceDefault());
        }
    }
}
