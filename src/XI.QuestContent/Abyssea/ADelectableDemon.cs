// A Delectable Demon (Log ID 8, Quest ID 173), a line-for-line port of scripts/quests/abyssea/A_Delectable_Demon.lua
// (quest rework B3b Abyssea). Valkurm Dunes: Cavernous_Maw !pos 367.98 -0.443 -119.874 103 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 55 into Abyssea - Misareaux; no default). Available, with a Traverser
// Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 56 (a Progress),
// whose finish begins it. Accepted, with the Cirein-croin Harpooner title (Cirein-croin's): the Valkurm Dunes zone-in
// plays 57; its update on option 1 shows the key item the finish gives (xi.abyssea.getZoneKIReward: zoneQuestReward for
// the count of maw quests completed); its finish gives that key item, then completes it. Kept as the Lua has it: 56
// begins on any option; 57 plays at any zone-in to Valkurm Dunes, from anywhere, and is returned as a number (a zone-in
// plays the first csid in container order); the key item before the completion; no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class ADelectableDemon : QuestScript
    {
        public ADelectableDemon() : base(QuestLog.Abyssea, "A_DELECTABLE_DEMON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Delectable_Demon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(56);
                })
                .OnEventFinish(Zone.ValkurmDunes, 56, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.CireinCroinHarpooner))
                .OnZoneIn(Zone.ValkurmDunes, q =>
                {
                    return QuestReply.Event(57);
                })
                .OnEventUpdate(Zone.ValkurmDunes, 57, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.ValkurmDunes, 57, q =>
                {
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
