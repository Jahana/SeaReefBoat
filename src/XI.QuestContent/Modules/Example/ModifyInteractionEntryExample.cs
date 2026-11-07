// The example module, a line-for-line port of modules/example/lua/modify_interaction_entry_example.lua (quest rework
// B3b Aht Urhgan group 6): the how-to for xi.module.modifyInteractionEntry.
// Module:new('modify_interaction_entry_example') (no condition: enabled whenever modules/init.txt lists it; the shipped
// init.txt does not). Its onServerStart override replaces Rock Bottom's section 1 check with one that is always false:
// the quest is no longer offered (blank11 answers with its default, NOTHING_HAPPENS), and a char who has begun it can
// still finish it (the other sections are unchanged).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Example
{
    public sealed class ModifyInteractionEntryExample : QuestModule
    {
        public ModifyInteractionEntryExample()
            : base("modify_interaction_entry_example", "modules/example/lua/modify_interaction_entry_example.lua") { }

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[1].check = function(player, status, vars) return false end:
            // Rock Bottom's state 1 (available)
            m.Quest(QuestKey.AhtUrhgan.RockBottom)
                .State(1).ReplaceCheck(q => false);
        }
    }
}
