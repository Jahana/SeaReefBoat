// Megadrile Menace (Log ID 8, Quest ID 165), a line-for-line port of scripts/quests/abyssea/Megadrile_Menace.lua (quest
// rework B3b Abyssea). Tahrongi Canyon: Cavernous_Maw !pos -28 46 -685 117 (its script:
// xi.abyssea.entranceMawOnTrigger, the warp event 100 into Abyssea - Tahrongi; no default). Available, with a Traverser
// Stone held (xi.abyssea.getHeldTraverserStones) and Dawn of Death accepted or completed: the maw: 38 (a Progress),
// whose finish begins it and gives 50 cruor (CRUOR_TOTAL with the new total). Accepted, with the Glavoid Stampeder
// title (Glavoid's): the Tahrongi Canyon zone-in plays 39; its update on option 1 shows the key item the finish gives
// (xi.abyssea.getZoneKIReward: zoneQuestReward for the count of maw quests completed); its finish gives that key item,
// then completes it. Kept as the Lua has it: 38 begins on any option; 39 plays at any zone-in to Tahrongi Canyon, from
// anywhere, and is returned as a number (a zone-in plays the first csid in container order); the key item before the
// completion (the Lua's NOTE); no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class MegadrileMenace : QuestScript
    {
        public MegadrileMenace() : base(QuestLog.Abyssea, "MEGADRILE_MENACE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Megadrile_Menace";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.CavernousMaw).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(38);
                })
                .OnEventFinish(Zone.TahrongiCanyon, 38, q =>
                {
                    q.Quest.Begin();
                    q.Player.AddCurrency("cruor", 50);
                    q.Text.Special(Text.TahrongiCanyon.CRUOR_TOTAL, 50, q.Player.Currency("cruor"));
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.GlavoidStampeder))
                .OnZoneIn(Zone.TahrongiCanyon, q =>
                {
                    return QuestReply.Event(39);
                })
                .OnEventUpdate(Zone.TahrongiCanyon, 39, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.TahrongiCanyon, 39, q =>
                {
                    // NOTE: Give the key item prior to completing the quest so that we reward the correct
                    // KI!  If we complete first, it'll adjust the total completed count, and be off by one!
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
