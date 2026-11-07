// A Beaked Blusterer (Log ID 8, Quest ID 176), a line-for-line port of scripts/quests/abyssea/A_Beaked_Blusterer.lua
// (quest rework B3b Abyssea). South Gustaberg: Cavernous_Maw !pos 340 -0.5 -680 107 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 914 into Abyssea - Altepa; no default). Available, with a Traverser
// Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 0 (a Progress),
// whose finish begins it. Accepted, with the Bennu Deplumer title (Bennu's): the South Gustaberg zone-in plays 1; its
// update on option 1 shows the key item the finish gives (xi.abyssea.getZoneKIReward: zoneQuestReward for the count of
// maw quests completed); its finish gives that key item, then completes it. Kept as the Lua has it: 0 begins on any
// option; 1 plays at any zone-in to South Gustaberg, from anywhere, and is returned as a number (a zone-in plays the
// first csid in container order); the key item before the completion (the Lua's NOTE); no reward table; no completed
// section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class ABeakedBlusterer : QuestScript
    {
        public ABeakedBlusterer() : base(QuestLog.Abyssea, "A_BEAKED_BLUSTERER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Beaked_Blusterer";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.SouthGustaberg, Npc.SouthGustaberg.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(0);
                })
                .OnEventFinish(Zone.SouthGustaberg, 0, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.BennuDeplumer))
                .OnZoneIn(Zone.SouthGustaberg, q =>
                {
                    return QuestReply.Event(1);
                })
                .OnEventUpdate(Zone.SouthGustaberg, 1, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.SouthGustaberg, 1, q =>
                {
                    // NOTE: Give the key item prior to completing the quest so that we reward the correct
                    // KI!  If we complete first, it'll adjust the total completed count, and be off by one!
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
