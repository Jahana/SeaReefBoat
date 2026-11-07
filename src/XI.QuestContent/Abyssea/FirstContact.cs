// First Contact (Log ID 8, Quest ID 167), a line-for-line port of scripts/quests/abyssea/First_Contact.lua (quest
// rework B3b Abyssea). La Theine Plateau: qm3 !pos -179 8 254 102 (no script; default STRANGE_ENERGY). Added by Dawn of
// Death's 333 (no available section). The section wants it accepted from 18:00 to 4:59: qm3 11, whose finish completes
// it and adds An Officer and a Pirate. A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the
// Lua's check is VanadielHour() >= 18 and VanadielHour() < 5, which no hour is, so the section never ran; the C# reads
// it as an `or`, as the file's header means and Death and Rebirth's zone-in has it. Kept as the Lua has it: no
// completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class FirstContact : QuestScript
    {
        public FirstContact() : base(QuestLog.Abyssea, "FIRST_CONTACT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/First_Contact";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // 18:00 to 4:59 (an `or`; the Lua's `and` was never true: fixed, user ruling 2026-11-04)
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.VanadielHour >= 18 ||
                    q.Player.VanadielHour < 5)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrigger(QuestReply.ProgressEvent(11))
                .OnEventFinish(Zone.LaTheinePlateau, 11, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "AN_OFFICER_AND_A_PIRATE");
                    }
                });
        }
    }
}
