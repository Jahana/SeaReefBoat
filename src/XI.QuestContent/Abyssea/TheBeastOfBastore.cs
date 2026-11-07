// The Beast of Bastore (Log ID 8, Quest ID 172), a line-for-line port of
// scripts/quests/abyssea/The_Beast_of_Bastore.lua (quest rework B3b Abyssea). Jugner Forest:
// Cavernous_Maw_2 !pos 246.318 -0.709 5.706 104 (its script: xi.abyssea.entranceMawOnTrigger, the warp event 47 into
// Abyssea - Vunkerl; no default). Available, with a Traverser Stone held (xi.abyssea.getHeldTraverserStones) and
// Dawn of Death accepted or completed: the maw: 48 (a Progress), whose finish begins it. Accepted, with the Sedna
// Tuskbreaker title (Sedna's): the Jugner Forest zone-in plays 49; its update on option 1 shows the key item the finish
// gives (xi.abyssea.getZoneKIReward: zoneQuestReward for the count of maw quests completed); its finish gives that key
// item, then completes it. Kept as the Lua has it: 48 begins on any option; 49 plays at any zone-in to Jugner Forest,
// from anywhere, and is returned as a number (a zone-in plays the first csid in container order); the key item before
// the completion; no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class TheBeastOfBastore : QuestScript
    {
        public TheBeastOfBastore() : base(QuestLog.Abyssea, "THE_BEAST_OF_BASTORE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/The_Beast_of_Bastore";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.CavernousMaw2).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(48);
                })
                .OnEventFinish(Zone.JugnerForest, 48, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.SednaTuskbreaker))
                .OnZoneIn(Zone.JugnerForest, q =>
                {
                    return QuestReply.Event(49);
                })
                .OnEventUpdate(Zone.JugnerForest, 49, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.JugnerForest, 49, q =>
                {
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
