// A Fluttery Fiend (Log ID 8, Quest ID 174), a line-for-line port of scripts/quests/abyssea/A_Fluttery_Fiend.lua (quest
// rework B3b Abyssea). Buburimu Peninsula: Cavernous_Maw !pos -344.396 -24.941 52.581 118 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 61 into Abyssea - Attohwa; no default). Available, with a Traverser
// Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 62 (a Progress),
// whose finish begins it. Accepted, with the Itzpapalotl Declawer title (Itzpapalotl's): the Buburimu Peninsula zone-in
// plays 63; its update on option 1 shows the key item the finish gives (xi.abyssea.getZoneKIReward: zoneQuestReward for
// the count of maw quests completed); its finish gives that key item, then completes it. Kept as the Lua has it: 62
// begins on any option; 63 plays at any zone-in to Buburimu Peninsula, from anywhere, and is returned as a number (a
// zone-in plays the first csid in container order); the key item before the completion; no reward table; no completed
// section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AFlutteryFiend : QuestScript
    {
        public AFlutteryFiend() : base(QuestLog.Abyssea, "A_FLUTTERY_FIEND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Fluttery_Fiend";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(62);
                })
                .OnEventFinish(Zone.BuburimuPeninsula, 62, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.ItzpapalotlDeclawer))
                .OnZoneIn(Zone.BuburimuPeninsula, q =>
                {
                    return QuestReply.Event(63);
                })
                .OnEventUpdate(Zone.BuburimuPeninsula, 63, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.BuburimuPeninsula, 63, q =>
                {
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
