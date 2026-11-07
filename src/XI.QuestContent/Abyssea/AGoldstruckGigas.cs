// A Goldstruck Gigas (Log ID 8, Quest ID 163), a line-for-line port of scripts/quests/abyssea/A_Goldstruck_Gigas.lua
// (quest rework B3b Abyssea). La Theine Plateau: Cavernous_Maw !pos -557.9 0.001 637.846 102 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 218 into Abyssea - La Theine; no default). Available, with a
// Traverser Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 9 (a
// Progress), whose finish begins it. Accepted, with the Briareus Feller title (Briareus's): the La Theine Plateau
// zone-in plays 10; its update on option 1 shows the key item the finish gives (xi.abyssea.getZoneKIReward:
// zoneQuestReward for the count of maw quests completed); its finish gives that key item, then completes it. Kept as
// the Lua has it: 9 begins on any option; 10 plays at any zone-in to La Theine Plateau, from anywhere, and is returned
// as a number (a zone-in plays the first csid in container order); the key item before the completion (the Lua's NOTE);
// no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AGoldstruckGigas : QuestScript
    {
        public AGoldstruckGigas() : base(QuestLog.Abyssea, "A_GOLDSTRUCK_GIGAS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Goldstruck_Gigas";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(9);
                })
                .OnEventFinish(Zone.LaTheinePlateau, 9, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.BriareusFeller))
                .OnZoneIn(Zone.LaTheinePlateau, q =>
                {
                    return QuestReply.Event(10);
                })
                .OnEventUpdate(Zone.LaTheinePlateau, 10, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.LaTheinePlateau, 10, q =>
                {
                    // NOTE: Give the key item prior to completing the quest so that we reward the correct
                    // KI!  If we complete first, it'll adjust the total completed count, and be off by one!
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
