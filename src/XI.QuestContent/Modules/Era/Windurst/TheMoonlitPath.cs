// The Moonlit Path's era module, a line-for-line port of modules/era/lua/quests/windurst/the_moonlit_path.lua (quest
// rework B3b): it removes the Fenrir mount from Leepe-Hoppe's reward menu (the September 6, 2016 version update added it).
// Module:new('era_quest_the_moonlit_path', xi.pre(xi.expansion.ROV)); its onServerStart override wraps section 2's
// Windurst Waters Leepe-Hoppe onTrigger: the quest's own action, with bit 7 (the mount) of the reward menu's hide mask
// (the event's fifth parameter, action.options[5]) set when the action is 846 or 850.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Windurst
{
    public sealed class TheMoonlitPath : QuestModule
    {
        public TheMoonlitPath() : base("era_quest_the_moonlit_path", "modules/era/lua/quests/windurst/the_moonlit_path.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.WINDURST_WATERS]['Leepe-Hoppe'].onTrigger: the quest's state 2 (accepted)
            m.Quest(QuestKey.Windurst.TheMoonlitPath)
                .State(2).Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).WrapOnTrigger((q, baseOnTrigger) =>
                {
                    var action = baseOnTrigger(q);

                    // The fifth event param is the reward menu's hide mask. Bit 7 is the mount.
                    if (action != null && (action.Id == 846 || action.Id == 850))
                    {
                        action = action.WithArg(4, action.Args[4].Number | 1 << 7); // Module change
                    }

                    return action;
                });
        }
    }
}
