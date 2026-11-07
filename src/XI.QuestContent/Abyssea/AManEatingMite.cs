// A Man-eating Mite (Log ID 8, Quest ID 177), a line-for-line port of scripts/quests/abyssea/A_Man-eating_Mite.lua
// (quest rework B3b Abyssea). Xarcabard: Cavernous_Maw !pos 270 -9 -70 112 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 204 into Abyssea - Uleguerand; no default). Available, with a
// Traverser Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 58 (a
// Progress), whose finish begins it. Accepted, with the Resheph Eradicator title (Resheph's): the Xarcabard zone-in
// plays 59; its update on option 1 shows the key item the finish gives (xi.abyssea.getZoneKIReward: zoneQuestReward for
// the count of maw quests completed); its finish gives that key item, then completes it. Kept as the Lua has it: 58
// begins on any option; 59 plays at any zone-in to Xarcabard, from anywhere, and is returned as a number (a zone-in
// plays the first csid in container order); the key item before the completion; no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AManEatingMite : QuestScript
    {
        public AManEatingMite() : base(QuestLog.Abyssea, "A_MAN_EATING_MITE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Man-eating_Mite";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.Xarcabard, Npc.Xarcabard.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(58);
                })
                .OnEventFinish(Zone.Xarcabard, 58, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.ReshephEradicator))
                .OnZoneIn(Zone.Xarcabard, q =>
                {
                    return QuestReply.Event(59);
                })
                .OnEventUpdate(Zone.Xarcabard, 59, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.Xarcabard, 59, q =>
                {
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
