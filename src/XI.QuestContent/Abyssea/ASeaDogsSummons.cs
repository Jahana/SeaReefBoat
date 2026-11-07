// A Sea Dog's Summons (Log ID 8, Quest ID 180), a line-for-line port of scripts/quests/abyssea/A_Sea_Dogs_Summons.lua
// (quest rework B3b Abyssea). Hall of the Gods: its zone-in. Added by Champions of Abyssea's 342 (no available
// section). The section wants it accepted from 18:00 to 4:59: the Hall of the Gods zone-in plays 6, whose finish
// completes it and adds Death and Rebirth. A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs):
// the Lua's check is VanadielHour() >= 18 and VanadielHour() < 5, which no hour is, so 6 never played; the C# reads it
// as an `or`, as the file's header means and Death and Rebirth's own zone-in has it. Kept as the Lua has it: 6 is
// returned as a number (an Event); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class ASeaDogsSummons : QuestScript
    {
        public ASeaDogsSummons() : base(QuestLog.Abyssea, "A_SEA_DOGS_SUMMONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Sea_Dogs_Summons";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // 18:00 to 4:59 (an `or`; the Lua's `and` was never true: fixed, user ruling 2026-11-04)
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.VanadielHour >= 18 ||
                    q.Player.VanadielHour < 5)
                .OnZoneIn(Zone.HallOfTheGods, q =>
                {
                    return QuestReply.Event(6);
                })
                .OnEventFinish(Zone.HallOfTheGods, 6, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "DEATH_AND_REBIRTH");
                    }
                });
        }
    }
}
