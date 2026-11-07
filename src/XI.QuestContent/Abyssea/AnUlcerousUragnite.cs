// An Ulcerous Uragnite (Log ID 8, Quest ID 178), a line-for-line port of
// scripts/quests/abyssea/An_Ulcerous_Uragnite.lua (quest rework B3b Abyssea). North Gustaberg:
// Cavernous_Maw_2 !pos -78 -0.5 600 106 (its script: xi.abyssea.entranceMawOnTrigger, the warp event 908 into
// Abyssea - Grauberg; no default). Available, with a Traverser Stone held (xi.abyssea.getHeldTraverserStones) and
// Dawn of Death accepted or completed: the maw: 0 (a Progress), whose finish begins it. Accepted, with the Amphitrite
// Shucker title (Amphitrite's): the North Gustaberg zone-in plays 1; its update on option 1 shows the key item the
// finish gives (xi.abyssea.getZoneKIReward: zoneQuestReward for the count of maw quests completed); its finish gives
// that key item, then completes it. Kept as the Lua has it: 0 begins on any option; 1 plays at any zone-in to
// North Gustaberg, from anywhere, and is returned as a number (a zone-in plays the first csid in container order); the
// key item before the completion; no reward table; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AnUlcerousUragnite : QuestScript
    {
        public AnUlcerousUragnite() : base(QuestLog.Abyssea, "AN_ULCEROUS_URAGNITE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/An_Ulcerous_Uragnite";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HeldTraverserStones >= 1 &&
                    q.Player.QuestStatus(QuestLog.Abyssea, "DAWN_OF_DEATH") >= QuestStatus.Accepted)
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.CavernousMaw2).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(0);
                })
                .OnEventFinish(Zone.NorthGustaberg, 0, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasTitle(Title.AmphitriteShucker))
                .OnZoneIn(Zone.NorthGustaberg, q =>
                {
                    return QuestReply.Event(1);
                })
                .OnEventUpdate(Zone.NorthGustaberg, 1, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.AbysseaZoneKIReward);
                    }
                })
                .OnEventFinish(Zone.NorthGustaberg, 1, q =>
                {
                    q.Player.GiveKeyItem(q.Player.AbysseaZoneKIReward);
                    q.Quest.Complete();
                });
        }
    }
}
