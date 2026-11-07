// An Eye for Revenge (Log ID 8, Quest ID 3), a line-for-line port of scripts/quests/abyssea/An_Eye_for_Revenge.lua
// (quest rework B3b Abyssea). Abyssea - La Theine: Curilla !pos -467.7 -3.5 -769.5 132 (no script, no default),
// Lugarhoo (LUGARHOO, popped at qm_lugarhoo with a Filthy Gnole Claw). Available, Lost Memories not completed: Curilla:
// 189 (an Event). Lost Memories completed: Curilla takes the Vial of Lambent Potion (as she answers) and plays 190 (an
// Event), whose finish begins it. Accepted: Curilla without Lugarhoo's Eyeball: 191, with it: 192 (Events); 192's
// finish takes the eyeball, gives 800 cruor (CRUOR_TOTAL with the new total) and completes it (the Scarlet Abyssite of
// Furtherance). Lugarhoo's death while accepted, the eyeball not held: the eyeball (KEYITEM_OBTAINED). Completed:
// Curilla: 193 (a progressEvent). Kept as the Lua has it: the vial goes at the talk, before 190 and whatever its
// option; 190 begins on any option; the eyeball is given without npcUtil (addKeyItem and the message); the cruor before
// the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AnEyeForRevenge : QuestScript
    {
        public AnEyeForRevenge() : base(QuestLog.Abyssea, "AN_EYE_FOR_REVENGE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/An_Eye_for_Revenge";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.ScarletAbyssiteOfFurtherance,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Abyssea, "LOST_MEMORIES") != QuestStatus.Completed)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Curilla).OnTrigger(q =>
                {
                    return QuestReply.Event(189);
                });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Abyssea, "LOST_MEMORIES") == QuestStatus.Completed)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Curilla).OnTrigger(q =>
                {
                    q.Player.DelKeyItem(KeyItem.VialOfLambentPotion);
                    return QuestReply.Event(190);
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 190, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Curilla).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LugarhoosEyeball))
                    {
                        return QuestReply.Event(191);
                    }
                    else
                    {
                        return QuestReply.Event(192);
                    }
                })
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Lugarhoo).OnMobDeath(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LugarhoosEyeball))
                    {
                        q.Player.AddKeyItem(KeyItem.LugarhoosEyeball);
                        q.Text.Special(Text.AbysseaLaTheine.KEYITEM_OBTAINED, KeyItem.LugarhoosEyeball);
                    }
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 192, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LugarhoosEyeball);
                    q.Player.AddCurrency("cruor", 800);
                    q.Text.Special(Text.AbysseaLaTheine.CRUOR_TOTAL, 800, q.Player.Currency("cruor"));
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Curilla).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(193);
                });
        }
    }
}
